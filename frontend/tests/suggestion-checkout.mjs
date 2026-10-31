import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import { cdp, evaluate, until, screenshot, errors, socket, sleep, origin, outputDir } from './cdp.mjs';
const key = 'tram.client.dbml.demo.v2';
const data = () => evaluate(`JSON.parse(localStorage.getItem('${key}'))`);
async function press(key) {
  await cdp('Input.dispatchKeyEvent', { type: 'keyDown', key, code: key, windowsVirtualKeyCode: key === 'Escape' ? 27 : 9 });
  await cdp('Input.dispatchKeyEvent', { type: 'keyUp', key, code: key, windowsVirtualKeyCode: key === 'Escape' ? 27 : 9 });
}
try {
  await cdp('Runtime.enable');
  await cdp('Page.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1360, height: 1000, deviceScaleFactor: 1, mobile: false });
  await cdp('Page.navigate', { url: origin + '/client/ordering' });
  await until("Boolean(document.querySelector('.today-teaser'))");
  await evaluate('localStorage.clear()');
  await cdp('Page.reload');
  await until("Boolean(document.querySelector('.today-teaser'))");
  await evaluate("document.querySelector('.today-teaser').click()");
  await evaluate("document.querySelector('.today-experience-actions .primary').focus(); document.querySelector('.today-experience-actions .primary').click()");
  const offset = () => evaluate("new DOMMatrixReadOnly(getComputedStyle(document.querySelector('.today-wheel-track')).transform).m41");
  await sleep(350); const first = await offset();
  await sleep(1000); const second = await offset();
  await sleep(1000); const third = await offset();
  assert.ok(first < 0 && second < first && third < second, 'Wheel moves forward');
  assert.ok(Math.abs(second - first) > Math.abs(third - second), 'Wheel decelerates');
  await until("Boolean(document.querySelector('dialog.today-result-dialog[open]'))");
  await sleep(320);
  assert.equal(await evaluate("document.querySelector('.today-prize-hero h3').textContent === document.querySelector('.today-outcome > strong').textContent"), true);
  assert.equal(await evaluate("document.querySelectorAll('.today-result-methods input').length"), 2);
  assert.equal(await evaluate("document.querySelector('.today-result-methods input:checked').value"), 'cash');
  await screenshot('suggestion-result-cash-desktop.png');
  await evaluate("document.querySelector('.today-prize-quantity .home-stepper button:last-child').click()");
  assert.equal(await evaluate("document.querySelector('.today-prize-quantity .home-stepper > span').textContent"), '2');
  await evaluate("document.querySelector('.today-result-methods input[value=transfer]').click()");
  assert.equal(await evaluate("document.querySelector('.today-checkout-submit').disabled"), true);
  assert.ok(await evaluate("document.querySelector('.today-transfer-detail').textContent.includes('Hiện chưa thể chuyển khoản')"));
  assert.equal(await data(), null, 'Selecting transfer must not create an order');
  await screenshot('suggestion-result-transfer-desktop.png');
  await press('Escape');
  await until("!document.querySelector('dialog[open]')");
  await evaluate("document.querySelector('.today-experience-actions .home-button:not(.primary)').click()");
  await until("Boolean(document.querySelector('dialog[open]'))");
  await evaluate("document.querySelector('.today-prize-quantity .home-stepper button:last-child').click()");
  await evaluate("document.querySelector('.today-checkout-submit').click(); document.querySelector('.today-checkout-submit')?.click()");
  await until("Boolean(document.querySelector('.today-checkout-success'))");
  const result = await data();
  assert.equal(result.orders.length, 4, 'Double click must create exactly one order');
  assert.equal(result.orders.at(-1).payment_method, 'Cash');
  assert.equal(result.orders.at(-1).payment_status, 'PendingCollection');
  assert.equal(result.customers[0].wallet_balance, 153000);
  assert.equal(result.wallet_transactions.length, 8);
  const item = result.order_items.at(-1);
  assert.equal(item.quantity, 2);
  assert.equal(result.orders.at(-1).total_amount, item.unit_price_snapshot * 2);
  await screenshot('suggestion-cash-success.png');
  await evaluate("document.querySelector('.today-checkout-success .primary').click()");
  await until("Boolean(document.querySelector('.client-orders-list'))");
  assert.ok(await evaluate("document.querySelector('.client-orders-list article:first-child').textContent.includes('tiền mặt · chờ thu')"));
  await evaluate("document.querySelector('.client-orders-list article:first-child .home-text-button').click()");
  await until("Boolean(document.querySelector('dialog[open]'))");
  assert.ok(await evaluate("document.querySelector('dialog').textContent.includes('Tiền mặt cần thanh toán')"));
  assert.ok(await evaluate("!document.querySelector('dialog').textContent.includes('Đã trừ ví khi đặt')"));
  await press('Escape');
  await evaluate("document.querySelector('a[href=\"/client/home\"]').click()");
  await until("Boolean(document.querySelector('.home-wallet'))");
  assert.ok(await evaluate("document.querySelector('.home-wallet').textContent.includes('Đơn món phiên này · ghi nhận')"));
  assert.ok(await evaluate("!document.querySelector('.home-wallet').textContent.includes('Đơn món phiên này · đã trừ ví')"));
  await evaluate("document.querySelector('a[href=\"/client/invoices\"]').click()");
  await until("Boolean(document.querySelector('.invoices-page'))");
  assert.ok(await evaluate("document.querySelector('.invoices-page > .home-info-box').textContent.includes('tiền mặt chờ thu')"));
  await cdp('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: false });
  await cdp('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-reduced-motion', value: 'reduce' }] });
  await cdp('Page.navigate', { url: origin + '/client/ordering' });
  await until("Boolean(document.querySelector('.today-teaser'))");
  await evaluate("document.querySelector('.today-teaser').click()");
  await evaluate("document.querySelector('.today-experience-actions .primary').click()");
  await until("Boolean(document.querySelector('dialog[open]'))");
  assert.ok(await evaluate("(() => { const dialog = document.querySelector('dialog'); const box = dialog.getBoundingClientRect(); return box.left >= 0 && box.right <= innerWidth && box.top >= 0 && box.bottom <= innerHeight && dialog.scrollWidth <= dialog.clientWidth; })()"));
  await screenshot('suggestion-result-mobile-top.png');
  await evaluate("document.querySelector('dialog').scrollTop = document.querySelector('dialog').scrollHeight");
  await screenshot('suggestion-result-mobile-payment.png');
  for (let index = 0; index < 10; index++) {
    await press('Tab');
    assert.ok(await evaluate("!document.activeElement.matches('button, input, a, select, textarea') || Boolean(document.activeElement.closest('dialog'))"), 'Focus must not reach background controls');
  }
  // Ordering the very last portion must keep the success dialog open.
  // React updates between native events; repeat asynchronously until the stock cap.
  for (let index = 0; index < 30; index++) {
    if (await evaluate("document.querySelector('.today-prize-quantity .home-stepper button:last-child').disabled")) break;
    await evaluate("document.querySelector('.today-prize-quantity .home-stepper button:last-child').click()");
    await sleep(10);
  }
  await evaluate("document.querySelector('.today-checkout-submit').click()");
  await until("Boolean(document.querySelector('.today-checkout-success'))");
  const exhausted = await data();
  assert.equal(exhausted.products.find((product) => product.id === exhausted.order_items.at(-1).product_id).stock_quantity, 0);
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, checks: ['automatic matching result dialog', 'two wallet style payment choices', 'quantity and total', 'transfer unavailable without creating orders', 'escape and reopen', 'cash order without wallet debit or duplicate', 'order detail and receipt payment status', 'mobile layout and keyboard focus', 'success stays open when stock reaches zero'] }));
} finally {
  socket.close();
}

