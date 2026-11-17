import assert from 'node:assert/strict';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { createServer } from 'vite';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('..', import.meta.url));
const server = await createServer({ root, optimizeDeps: { noDiscovery: true }, server: { middlewareMode: true }, appType: 'custom', ssr: { noExternal: ['react-router-dom', 'react-router'], resolve: { conditions: ['module', 'module-sync', 'node', 'development'] } } });
try {
  const { ZaloPayPaymentCard } = await server.ssrLoadModule('/src/client/wallet/ZaloPayPaymentCard.jsx');
  const now = Date.parse('2026-11-18T00:00:00Z');
  const pending = { id: 23, customerId: 1, sourceId: 23, purpose: 'TopUp', expectedAmountVnd: 20000,
    status: 'Pending', merchantTransactionId: '261118_fixture', createdAtUtc: new Date(now).toISOString(),
    expiresAtUtc: new Date(now + 900000).toISOString(), paymentUrl: 'https://qcgateway.zalopay.vn/pay/v2/?order=fixture', requiresReconciliation: false };
  const render = payment => renderToStaticMarkup(React.createElement(ZaloPayPaymentCard, { payment, enabled: true, now }));
  assert.match(render(pending), /Mã QR ZaloPay Sandbox/);
  const verified = { ...pending, status: 'Succeeded', verifiedAtUtc: new Date(now + 1000).toISOString() };
  assert.match(render(verified), /đang ghi nhận/);
  assert.doesNotMatch(render(verified), /Đã cộng vào ví|<svg role="img"/);
  const applied = { ...verified, appliedAtUtc: new Date(now + 1100).toISOString(), walletTransactionId: 9 };
  assert.match(render(applied), /Đã cộng vào ví/);
  assert.doesNotMatch(render(applied), /Kiểm tra trạng thái|Mã QR/);
  assert.match(render({ ...verified, purpose: 'Order', appliedAtUtc: applied.appliedAtUtc, paymentId: 7 }), /Đã ghi nhận thanh toán đơn/);
  for (const payment of [{ ...pending, status: 'Failed' }, { ...pending, status: 'Expired' }, { ...verified, requiresReconciliation: true }])
    assert.doesNotMatch(render(payment), /Mã QR|Đã cộng vào ví|href="https:/);
  assert.match(render({ ...verified, requiresReconciliation: true }), /không thanh toán lại/);
  console.log('ZaloPay payment rendering passed: pending QR, verified versus applied, Order receipt, failed/expired/manual review.');
} finally { await server.close(); }
