import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { createServer } from 'vite';
import jsQR from 'jsqr';
import { createZaloPayPaymentQr } from './zaloPayPaymentQr.js';

// Internal display/QR fixture, not proof of a provider payment.
const payment = {
  id: 3, customerId: 7, purpose: 'TopUp', sourceId: 3, expectedAmountVnd: 50000,
  status: 'Pending', createdAtUtc: '2099-11-17T00:00:00Z', expiresAtUtc: '2099-11-17T00:15:00Z',
  merchantTransactionId: '991117_internal_fixture_3', requiresReconciliation: false,
  appliedAtUtc: null, walletTransactionId: null, paymentId: null, settlementFailureReason: null,
  paymentUrl: 'https://qcgateway.zalopay.vn/openinapp?order=AA+bb/cc==&opaque=%2B%2F%3D&v=1&v=2',
};
const now = Date.parse('2099-11-17T00:01:00Z');
let server, ZaloPayPaymentQr, ZaloPayPaymentCard;
before(async () => {
  server = await createServer({ root: fileURLToPath(new URL('../../../', import.meta.url)), configFile: false,
    optimizeDeps: { noDiscovery: true }, server: { middlewareMode: true, hmr: false }, appType: 'custom' });
  ({ ZaloPayPaymentQr, ZaloPayPaymentCard } = await server.ssrLoadModule('/src/client/wallet/ZaloPayPaymentCard.jsx'));
});
after(async () => { await server?.close(); });
const renderQr = (row = payment, enabled = true) => renderToStaticMarkup(React.createElement(ZaloPayPaymentQr, { payment: row, enabled, now }));
const renderCard = row => renderToStaticMarkup(React.createElement(ZaloPayPaymentCard, { payment: row, enabled: true, now }));

// Rasterize the SVG actually emitted by React and decode with an independent QR reader.
function decodeRenderedQr(html) {
  const size = Number(/viewBox="0 0 (\d+) \d+"/.exec(html)?.[1]);
  assert.ok(Number.isSafeInteger(size) && size > 8);
  const path = /<path\b[^>]*\bd="([^"]+)"/.exec(html)?.[1];
  assert.ok(path);
  const scale = 6, width = size * scale;
  const pixels = new Uint8ClampedArray(width * width * 4).fill(255);
  const runs = [...path.matchAll(/M(\d+) (\d+)h(\d+)v1h-(\d+)z/g)];
  assert.equal(runs.map(run => run[0]).join(''), path);
  for (const [, xText, yText, lengthText, closingText] of runs) {
    const x = Number(xText), y = Number(yText), length = Number(lengthText);
    assert.equal(lengthText, closingText);
    assert.ok(x >= 4 && y >= 4 && x + length <= size - 4 && y < size - 4, 'Keep a four-module quiet zone');
    for (let pixelY = y * scale; pixelY < (y + 1) * scale; pixelY++) {
      for (let pixelX = x * scale; pixelX < (x + length) * scale; pixelX++) {
        const offset = (pixelY * width + pixelX) * 4;
        pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = 0;
      }
    }
  }
  const decoded = jsQR(pixels, width, width, { inversionAttempts: 'dontInvert' });
  assert.ok(decoded, 'Independent reader must recognize the rendered SVG');
  return decoded.data;
}

test('TopUp and Order QR decode to exact provider URLs including opaque plus/base64/duplicate query values', () => {
  for (const purpose of ['TopUp', 'Order']) {
    for (const host of ['qcgateway.zalopay.vn', 'sbgateway.zalopay.vn', 'sbcheckout.zalopay.vn']) {
      const row = { ...payment, purpose, paymentUrl: payment.paymentUrl.replace('qcgateway.zalopay.vn', host) };
      const html = renderQr(row);
      assert.equal(decodeRenderedQr(html), row.paymentUrl);
      assert.match(html, /shape-rendering="crispEdges"/);
      assert.match(html, /<rect[^>]*fill="#fff"/);
      assert.match(html, /<path[^>]*fill="#000"/);
      assert.match(html, /aria-label="Mã QR ZaloPay Sandbox cho giao dịch #3"/);
      assert.match(html, /Quét bằng app ZaloPay Sandbox/);
    }
  }
});

test('terminal, reconciliation, disabled and expired payments cannot offer a QR or checkout link', () => {
  for (const row of ['Succeeded', 'Failed', 'Expired'].map(status => ({ ...payment, status }))
    .concat([{ ...payment, requiresReconciliation: true }, { ...payment, settlementFailureReason: 'internal_fixture_review' },
      { ...payment, expiresAtUtc: new Date(now).toISOString() }, { ...payment, expiresAtUtc: 'invalid' }])) {
    assert.equal(renderQr(row), '');
    assert.ok(!renderCard(row).includes('Mở ZaloPay Sandbox'));
  }
  assert.equal(renderQr(payment, false), '');
  assert.equal(renderToStaticMarkup(React.createElement(ZaloPayPaymentQr, { payment, now })), '');
  const expires = Date.parse(payment.expiresAtUtc);
  assert.ok(createZaloPayPaymentQr(payment, true, expires - 1));
  assert.equal(createZaloPayPaymentQr(payment, true, expires), null);
});

test('unsafe hosts and oversized/unencodable URLs do not interrupt the saved payment', () => {
  for (const paymentUrl of [null, '', 'https://gateway.zalopay.vn/openinapp?order=fixture',
    'https://qcgateway.zalopay.vn.evil.test/openinapp', 'http://qcgateway.zalopay.vn/openinapp',
    'https://user@qcgateway.zalopay.vn/openinapp', 'https://qcgateway.zalopay.vn:8443/openinapp',
    'https://qcgateway.zalopay.vn/openinapp#fragment', 'javascript:alert(1)',
    `https://qcgateway.zalopay.vn/openinapp?order=${'X'.repeat(2048)}`,
    `https://qcgateway.zalopay.vn/openinapp?order=${'😀'.repeat(600)}`]) {
    assert.equal(renderQr({ ...payment, paymentUrl }), '');
  }
  assert.equal(payment.status, 'Pending');
});

test('receipt success cannot claim wallet credit until committed ledger link; Order uses its own payment link', () => {
  const receipt = { ...payment, status: 'Succeeded' };
  const waiting = renderCard(receipt);
  assert.match(waiting, /Đã thanh toán · đang ghi nhận/);
  assert.match(waiting, /Chờ ghi có vào ví/);
  assert.ok(!waiting.includes('Đã cộng vào ví'));
  assert.ok(!waiting.includes('Mã QR'));
  const applied = { ...receipt, appliedAtUtc: '2099-11-17T00:01:01Z', walletTransactionId: 8 };
  assert.match(renderCard(applied), /Đã cộng vào ví/);
  assert.match(renderCard(applied), /Giao dịch ví/);
  assert.ok(!renderCard(applied).includes('Kiểm tra trạng thái'));
  assert.match(renderCard({ ...receipt, requiresReconciliation: true }), /Cần kiểm tra thanh toán/);
  const order = { ...receipt, purpose: 'Order', sourceId: 42, appliedAtUtc: '2099-11-17T00:01:01Z', paymentId: 9 };
  assert.match(renderCard(order), /Thanh toán đơn #42/);
  assert.match(renderCard(order), /Đã ghi nhận thanh toán đơn/);
  assert.ok(!renderCard(order).includes('Đã cộng vào ví'));
});

test('pending redirect link only opens the exact URL; display identities are escaped', () => {
  const html = renderCard({ ...payment, merchantTransactionId: '<script>internal-fixture</script>' });
  assert.match(html, /Chờ thanh toán/);
  assert.match(html, /Mở ZaloPay Sandbox/);
  assert.match(html, /target="_blank" rel="noopener noreferrer"/);
  assert.match(html, /&lt;script&gt;internal-fixture&lt;\/script&gt;/);
  assert.ok(!html.includes('<script>'));
});
