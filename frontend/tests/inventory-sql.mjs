import assert from 'node:assert/strict';
import { cdp, evaluate, until, socket, go, input, click, screenshot, errors } from './cdp.mjs';

const sessionKey = 'cybermanagement.auth.v1';
const ready = 'document.querySelector("tbody") && document.body.innerText.includes("TEST_Nước suối")';
async function login(identifier, customer = false) {
  await go(customer ? '/client/auth' : '/admin/auth', 'document.querySelector(".auth-form")');
  await input('#auth-identifier', identifier);
  await input('#auth-password', 'Inventory-check-only-2026!');
  await click('Đăng nhập', 'form.auth-form button[type="submit"]');
  await until(`Boolean(document.querySelector(${JSON.stringify(customer ? '.client-layout, .client-shell, .home-heading' : '.admin-shell')}))`);
}
async function signOut() {
  await evaluate(`document.querySelector('[aria-label="Đăng xuất"]').click()`);
  await until('Boolean(document.querySelector(".auth-form"))');
}
async function apiStatus() {
  return evaluate(`(async () => {
    const session = JSON.parse(localStorage.getItem('${sessionKey}') || 'null');
    const response = await fetch('/api/admin/inventory', { headers: session ? { Authorization: 'Bearer ' + session.accessToken } : {} });
    return { status: response.status, body: response.ok ? await response.json() : null };
  })()`);
}
try {
  await cdp('Page.enable');
  await cdp('Runtime.enable');
  await go('/admin/auth', 'document.querySelector(".auth-form")');
  await evaluate('localStorage.clear()');
  await go('/admin/inventory', 'document.querySelector(".auth-form")');
  assert.equal((await apiStatus()).status, 401);
  await login('admin');
  await go('/admin/inventory', ready);
  const sql = await apiStatus();
  assert.equal(sql.status, 200);
  assert.deepEqual(sql.body.map(item => item.stockQuantity), [20, 0, 5]);
  assert.equal(await evaluate('document.querySelectorAll("tbody tr").length'), 3);
  assert.ok(await evaluate('document.body.innerText.includes("Ngừng bán")'));
  assert.equal(await evaluate('document.body.innerText.includes("Tạo phiếu kho")'), false);
  await screenshot('scrum13-inventory-sql.png');
  await cdp('Page.reload');
  await until(ready);
  assert.deepEqual((await apiStatus()).body, sql.body, 'Reload must read the same persisted SQL rows');
  await input('input[placeholder="Tìm mã hoặc tên sản phẩm…"]', 'TEST_Nước suối');
  await until('document.querySelectorAll("tbody tr").length === 1');
  await input('input[placeholder="Tìm mã hoặc tên sản phẩm…"]', 'khong-co-san-pham-nay');
  await until('document.body.innerText.includes("Không có sản phẩm phù hợp bộ lọc.")');
  await input('input[placeholder="Tìm mã hoặc tên sản phẩm…"]', '');
  await signOut();
  await login('manager01');
  await go('/admin/inventory', ready);
  assert.equal((await apiStatus()).status, 200);
  await signOut();
  for (const username of ['cashier01', 'waiter01', 'tech01']) {
    await login(username);
    assert.equal(await evaluate('Boolean(document.querySelector("a[href=\\"/admin/inventory\\"]"))'), false);
    assert.equal((await apiStatus()).status, 403);
    await go('/admin/inventory', 'document.querySelector(".admin-shell")');
    assert.notEqual(await evaluate('location.pathname'), '/admin/inventory');
    await signOut();
  }
  await login('0900000013', true);
  assert.equal((await apiStatus()).status, 403);
  // Clear the real customer session before a second real employee login.
  await evaluate('localStorage.clear()');
  await go('/admin/auth', 'document.querySelector(".auth-form")');
  await login('admin');

  // Only the following state checks inject responses; SQL/auth cases above use the real API.
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    const originalFetch = window.fetch.bind(window);
    window.fetch = async (url, options) => {
      if (new URL(typeof url === 'string' ? url : url.url, location.href).pathname === '/api/admin/inventory') {
        const state = localStorage.getItem('__inventoryUiState');
        if (state === 'empty') return Response.json([]);
        if (state === 'error') return Response.json({ title: 'Injected database error' }, { status: 500 });
        if (state === 'loading') await new Promise(resolve => setTimeout(resolve, 800));
      }
      return originalFetch(url, options);
    };
  })()` });
  await evaluate("localStorage.setItem('__inventoryUiState', 'empty')");
  await go('/admin/inventory', 'document.body.innerText.includes("Chưa có sản phẩm trong kho.")');
  assert.equal(await evaluate('document.body.innerText.includes("TEST_Nước suối")'), false);
  await evaluate("localStorage.setItem('__inventoryUiState', 'error')");
  await cdp('Page.reload');
  await until('document.body.innerText.includes("Không tải được tồn kho.")');
  assert.equal(await evaluate('Boolean(document.querySelector("tbody"))'), false, 'API error must not render demo rows');
  await evaluate("localStorage.setItem('__inventoryUiState', 'loading')");
  await click('Tải lại');
  await until('document.body.innerText.includes("Đang tải tồn kho")');
  await until(ready);
  await evaluate("localStorage.removeItem('__inventoryUiState')");
  assert.deepEqual(errors, []);
  console.log('Inventory browser: real SQL/login/permissions/reload passed; injected loading/empty/error states passed.');
} finally { socket.close(); }
