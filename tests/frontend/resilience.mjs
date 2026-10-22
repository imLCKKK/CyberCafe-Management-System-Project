import assert from 'node:assert/strict';
import { cdp, evaluate, until, errors, socket, go, click, screenshot } from './cdp.mjs';
const key = 'tram.client.dbml.demo.v2';
const routes = ['home', 'ordering', 'wallet', 'invoices', 'account'];
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
  // A structurally valid saved record with an invalid date must not crash a route.
  await evaluate(`(() => { const db = JSON.parse(localStorage.getItem('${key}')); db.accounts[0].created_at = 'broken-date'; localStorage.setItem('${key}', JSON.stringify(db)); })()`);
  await go('/client/account');
  assert.ok(await evaluate("Boolean(document.querySelector('[role=alert]'))"), 'Invalid persisted date must be rejected before rendering');
  await click('Khôi phục bản mẫu'); await click('Khôi phục', 'dialog button');
  // Simulate another tab removing orders while the detail dialog is open.
  await go('/client/ordering');
  await evaluate("document.querySelectorAll('.client-page-tabs button')[2].click()");
  await until("Boolean(document.querySelector('.client-orders-list .home-text-button'))");
  await evaluate("document.querySelector('.client-orders-list .home-text-button').click()");
  await until("Boolean(document.querySelector('dialog[open]'))");
  await evaluate(`(() => { const db = JSON.parse(localStorage.getItem('${key}')); db.orders = []; db.order_items = []; db.order_item_cancel_requests = []; localStorage.setItem('${key}', JSON.stringify(db)); window.dispatchEvent(new StorageEvent('storage', { key: '${key}' })); })()`);
  await until("!document.querySelector('dialog[open]') && Boolean(document.querySelector('.client-empty-state'))");
  // Storage failures must leave the previous profile unchanged and display an error.
  await go('/client/account');
  await evaluate("window.__originalSetItem = Storage.prototype.setItem; Storage.prototype.setItem = () => { throw new DOMException('Quota exceeded', 'QuotaExceededError'); }");
  await evaluate("(() => { const el = document.querySelector('input[autocomplete=name]'); Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(el, 'Không lưu được'); el.dispatchEvent(new Event('input', { bubbles:true })); })()");
  await click('Lưu thay đổi');
  await until("document.querySelector('[role=alert]')?.textContent.includes('Không thể lưu')");
  assert.notEqual(await evaluate(`JSON.parse(localStorage.getItem('${key}')).accounts[0].display_name`), 'Không lưu được');
  await evaluate('Storage.prototype.setItem = window.__originalSetItem');
  await screenshot('storage-error.png');
  // Empty lists and access restrictions should still render all client pages.
  for (const scenario of ['no-session', 'zero-wallet', 'locked', 'must-change-password', 'empty-lists']) {
    await evaluate(`(async () => {
      const { createClientDemo } = await import('/web/src/client/shared/demoData.js');
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
  console.log(JSON.stringify({ passed: true, checks: ['corrupt JSON and incomplete storage across five client routes', 'restore sample', 'invalid persisted date', 'remote deletion of selected order', 'quota failure preserves saved state', '25 route/scenario renders: no session, zero wallet, locked, first password change, empty lists'] }));
} finally { socket.close(); }
