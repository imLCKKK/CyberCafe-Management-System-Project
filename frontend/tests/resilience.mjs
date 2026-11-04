import assert from 'node:assert/strict';
import { cdp, evaluate, until, errors, socket, go, click, screenshot } from './cdp.mjs';
const key = 'tram.client.dbml.demo.v2';
// 'wallet' và 'account' đã đọc từ API nên không còn lỗi do localStorage hỏng; xem wallet-customers-api.mjs.
const routes = ['home', 'ordering', 'invoices'];
try {
  await cdp('Runtime.enable'); await cdp('Page.enable');
  await go('/client/home');
  for (const raw of ['{invalid JSON', '{"schema_version":2}', 'null']) {
    await evaluate(`localStorage.setItem('${key}', ${JSON.stringify(raw)})`);
    for (const route of routes) {
      await go(`/client/${route}`);
      assert.ok(await evaluate("document.querySelector('[role=alert]')?.textContent.includes('Không đọc được')"), `Corrupt storage warning: ${route}`);
    }
  }
  await click('Khôi phục bản mẫu'); await click('Khôi phục', 'dialog button');
  await until("!document.querySelector('dialog[open]')");
  assert.ok(await evaluate(`JSON.parse(localStorage.getItem('${key}')).customers.length > 0`));
  // Simulate another tab removing orders while the detail dialog is open.
  await go('/client/ordering');
  await evaluate("document.querySelectorAll('.client-page-tabs button')[2].click()");
  await until("Boolean(document.querySelector('.client-orders-list .home-text-button'))");
  await evaluate("document.querySelector('.client-orders-list .home-text-button').click()");
  await until("Boolean(document.querySelector('dialog[open]'))");
  await evaluate(`(() => { const db = JSON.parse(localStorage.getItem('${key}')); db.orders = []; db.order_items = []; db.order_item_cancel_requests = []; localStorage.setItem('${key}', JSON.stringify(db)); window.dispatchEvent(new StorageEvent('storage', { key: '${key}' })); })()`);
  await until("!document.querySelector('dialog[open]') && Boolean(document.querySelector('.client-empty-state'))");
  // Empty lists and access restrictions should still render all client pages.
  for (const scenario of ['no-session', 'zero-wallet', 'locked', 'must-change-password', 'empty-lists']) {
    await evaluate(`(async () => {
      const { createClientDemo } = await import('/src/client/shared/demoData.js');
      const db = createClientDemo();
      const scenario = ${JSON.stringify(scenario)};
      if (scenario === 'no-session') db.gaming_sessions.forEach(row => { row.status = 'Ended'; row.ended_at = new Date().toISOString(); });
      if (scenario === 'zero-wallet') db.customers[0].wallet_balance = 0;
      if (scenario === 'locked') db.accounts[0].is_active = false;
      if (scenario === 'must-change-password') db.accounts[0].must_change_password = true;
      if (scenario === 'empty-lists') { db.orders = []; db.order_items = []; db.order_item_cancel_requests = []; db.wallet_transactions = []; db.top_ups = []; db.products = []; }
      localStorage.setItem('${key}', JSON.stringify(db));
    })()`);
    for (const route of routes) {
      await go(`/client/${route}`);
      assert.equal(await evaluate('/NaN|Invalid Date/.test(document.body.innerText)'), false, `${scenario}: ${route}`);
      if (route === 'ordering') assert.ok(await evaluate("document.querySelector('.order-cart-body > .home-button.primary').disabled"));
    }
  }
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, checks: ['corrupt JSON and incomplete storage across three client routes', 'restore sample', 'remote deletion of selected order', '15 route/scenario renders: no session, zero wallet, locked, first password change, empty lists'] }));
} finally { socket.close(); }
