import assert from 'node:assert/strict';
import { cdp, evaluate, until, socket, go, input, click, press, screenshot, errors } from './cdp.mjs';

const ready = 'document.querySelectorAll(".admin-table tbody tr").length > 0';
async function login(identifier, customer = false, password = 'Admin-check-password-2026!') {
  await go(customer ? '/client/auth' : '/admin/auth', 'document.querySelector(".auth-form")');
  await input('#auth-identifier', identifier);
  await input('#auth-password', password);
  await click('Đăng nhập', 'form.auth-form button[type="submit"]');
  await until(customer ? 'Boolean(document.querySelector(".home-heading"))' : 'Boolean(document.querySelector(".admin-shell"))');
}
async function clearSession() {
  await evaluate("localStorage.removeItem('cybermanagement.auth.v1')");
}
async function api(path, method = 'GET', body) {
  return evaluate(`(async () => {
    const session = JSON.parse(localStorage.getItem('cybermanagement.auth.v1'));
    const response = await fetch('/api' + ${JSON.stringify(path)}, { method: ${JSON.stringify(method)},
      headers: { Authorization: 'Bearer ' + session.accessToken, 'Content-Type': 'application/json' },
      body: ${body === undefined ? 'undefined' : JSON.stringify(JSON.stringify(body))} });
    const text = await response.text();
    return { status: response.status, body: text ? JSON.parse(text) : null };
  })()`);
}

try {
  await cdp('Page.enable'); await cdp('Runtime.enable');
  await cdp('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-reduced-motion', value: 'reduce' }] });
  await go('/admin/auth', 'document.querySelector(".auth-form")');
  await evaluate('localStorage.clear()');
  await login('admin');
  await go('/admin/products', ready);
  assert.equal(await evaluate('document.querySelectorAll(".admin-table tbody tr").length'), 4);
  const stocked = (await api('/admin/products')).body.find(p => p.name === 'TEST_Menu_Stocked');
  const categoryId = stocked.categoryId;

  // Real UI CRUD, followed by reload from SQL in a fresh request.
  await click('Danh mục');
  await click('Thêm danh mục');
  await input('dialog:last-of-type input[name="name"]', 'TEST_UI_Category');
  await click('Lưu thông tin', 'dialog:last-of-type button');
  await until('document.querySelectorAll("dialog").length === 1 && document.body.innerText.includes("TEST_UI_Category")');
  await press('Escape');
  await until('!document.querySelector("dialog")');
  await until('[...document.querySelectorAll("button")].some(b => b.textContent === "Thêm sản phẩm" && !b.disabled)');
  await click('Thêm sản phẩm');
  await input('dialog input[name="name"]', 'TEST_UI_Product');
  await input('dialog input[name="current_price"]', '45000');
  await click('Lưu thông tin', 'dialog button');
  await until('!document.querySelector("dialog") && document.body.innerText.includes("TEST_UI_Product")');
  const created = (await api('/admin/products')).body.find(p => p.name === 'TEST_UI_Product');
  assert.equal(created.stockQuantity, 0);
  assert.equal(created.currentPrice, 45000);
  await cdp('Page.reload'); await until(ready);
  assert.equal((await api('/admin/products')).body.find(p => p.id === created.id).name, 'TEST_UI_Product');
  await input('input[placeholder="Tìm tên hoặc mã sản phẩm…"]', 'TEST_UI_Product');
  await until('document.querySelectorAll("tbody tr").length === 1');
  await click('Chỉnh sửa');
  await input('dialog input[name="current_price"]', '47000');
  await click('Lưu thông tin', 'dialog button');
  await until('!document.querySelector("dialog") && document.body.innerText.includes("47.000")');
  await click('Ngừng bán');
  await until('document.querySelector("tbody .admin-status")?.textContent === "Ngừng bán"');
  assert.equal((await api('/admin/products')).body.find(p => p.id === created.id).isActive, false);
  await input('select[aria-label="Trạng thái sản phẩm"]', 'inactive');
  await until('document.querySelectorAll("tbody tr").length === 1');
  await screenshot('catalog-admin-sql.png');

  // Read-only staff sees data, with no mutation controls; the API also refuses writes.
  await clearSession();
  await login('cashier01', false, 'Cashier-check-password-2026!');
  await go('/admin/products', ready);
  assert.equal(await evaluate('[...document.querySelectorAll("button")].some(b => b.textContent === "Thêm sản phẩm" || b.textContent === "Chỉnh sửa")'), false);
  assert.equal((await api('/admin/products', 'POST', { name: 'Forbidden', categoryId, currentPrice: 100, isActive: true })).status, 403);

  // Customer menu, stock-aware suggestion, cart, and Home handoff use the SQL IDs.
  await clearSession();
  await login('0900000010', true, 'Catalog-check-password-2026!');
  await until('document.querySelector(".home-food-section")?.innerText.includes("TEST_Menu_Stocked")');
  await evaluate('document.querySelector(".home-food-section .home-add").click()');
  await until('document.querySelectorAll(".order-cart-line").length === 1');
  assert.equal(await evaluate('document.querySelector(".order-cart-line strong").textContent'), 'TEST_Menu_Stocked');
  assert.equal(await evaluate('document.querySelector(".order-cart-line").innerText.includes("30.000")'), true);
  assert.equal(await evaluate('document.querySelectorAll(".ordering-food-grid article").length'), 2);
  assert.equal(await evaluate('[...document.querySelectorAll(".ordering-food-grid article")].find(a => a.innerText.includes("TEST_Menu_Empty")).querySelector("button").disabled'), true);
  assert.equal(await evaluate('document.querySelector(".ordering-food-grid").innerText.includes("TEST_Menu_Inactive")'), false);
  assert.equal(await evaluate('document.querySelector(".ordering-food-grid").innerText.includes("TEST_Category_Inactive")'), false);
  await input('input[placeholder="Tìm món bạn thích…"]', 'no-matching-menu');
  await until('document.querySelectorAll(".ordering-food-grid article").length === 0');
  await input('input[placeholder="Tìm món bạn thích…"]', '');
  await click('Hôm nay ăn gì?');
  await click('Quay chọn món ngay');
  await until('Boolean(document.querySelector("dialog"))');
  assert.equal(await evaluate('document.querySelector("dialog h3").textContent'), 'TEST_Menu_Stocked');
  await click('Thêm vào giỏ', 'dialog button');
  await until('!document.querySelector("dialog")');
  assert.equal(await evaluate('document.querySelector(".client-count").textContent'), '2');
  assert.equal(await evaluate('document.querySelector(".order-cart-body .home-button.primary").disabled'), true);
  await screenshot('catalog-menu-cart-sql.png');

  // Only error-state cases below inject responses; CRUD/auth/menu above use the real SQL host.
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    const original = window.fetch.bind(window);
    window.fetch = async (url, options) => {
      if (new URL(url, location.href).pathname === '/api/menu') {
        const state = localStorage.getItem('__catalogState');
        if (state === 'error') return Response.json({ detail: 'TEST_Menu_Unavailable' }, { status: 503 });
        if (state === 'empty') return Response.json({ categories: [], products: [] });
        if (state === 'loading') await new Promise(resolve => setTimeout(resolve, 1000));
      }
      return original(url, options);
    };
  })()` });
  await evaluate("localStorage.setItem('__catalogState', 'error')");
  await go('/client/ordering', 'document.body.innerText.includes("TEST_Menu_Unavailable")');
  assert.equal(await evaluate('document.querySelectorAll(".ordering-food-grid article").length'), 0);
  assert.equal(await evaluate('document.body.innerText.includes("Mì xào bò")'), false);
  await evaluate("localStorage.setItem('__catalogState', 'empty')");
  await cdp('Page.reload'); await until('document.body.innerText.includes("Chưa tìm thấy món")');
  await evaluate("localStorage.setItem('__catalogState', 'loading')");
  await cdp('Page.reload'); await until('document.body.innerText.includes("Đang tải thực đơn")');
  await until('document.querySelectorAll(".ordering-food-grid article").length === 2');
  await evaluate("localStorage.removeItem('__catalogState')");
  assert.deepEqual(errors, []);
  console.log('Catalog browser passed: real SQL CRUD/reload, read-only permissions, Home/menu/suggestion/cart, no mock fallback; injected error/empty/loading.');
} catch (error) {
  await screenshot('catalog-failure.png');
  throw error;
} finally { socket.close(); }
