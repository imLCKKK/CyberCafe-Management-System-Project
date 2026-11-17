import assert from 'node:assert/strict';
import { cdp, evaluate, until, socket, go, click, input, screenshot } from './cdp.mjs';

const api = process.env.VOUCHERS_LIVE_API_URL;
if (!api) throw Error('Use run-vouchers-live.mjs with a disposable SQL database.');
const code = `UI_${Date.now()}`;
let token;
async function request(path = '', method = 'GET', body, accessToken = token) {
  return fetch(`${api}/api/admin/vouchers${path}`, {
    method, headers: { ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}), 'Content-Type': 'application/json' },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
}
async function login(username, role) {
  await go('/admin/auth', 'document.querySelector(".auth-form")');
  await input('#auth-identifier', username);
  await input('#auth-password', `${role}-check-password-2026!`);
  await click('Đăng nhập', 'form.auth-form button[type="submit"]');
  await until('!document.querySelector(".auth-form") && Boolean(document.querySelector(".admin-shell"))');
  token = await evaluate('JSON.parse(localStorage.getItem("cybermanagement.auth.v1")).accessToken');
}
async function logout() {
  await evaluate('document.querySelector(\'button[aria-label="Đăng xuất"]\').click()');
  await until('Boolean(document.querySelector(".auth-form"))');
}
const ready = () => until('Boolean(document.querySelector(".admin-module table")) && !document.body.innerText.includes("Đang tải voucher…")');
const formInput = (name, value) => input(`dialog [name="${name}"]`, value);
try {
  await cdp('Runtime.enable'); await cdp('Page.enable'); await cdp('Network.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
  assert.equal((await request('', 'GET', undefined, null)).status, 401);
  await login('admin', 'Admin');
  await click('Voucher', 'a.nav-item');
  await ready();
  assert.equal(await evaluate('location.pathname'), '/admin/promotions');
  assert.ok(await evaluate('document.body.innerText.includes("Chưa có voucher.")'));
  await until('document.body.innerText.includes("Đã kết nối cập nhật trực tiếp.")');
  await click('Thêm voucher');
  for (const [name, value] of Object.entries({
    code, name: 'Voucher giao diện', discountValue: '12.25', minOrderAmount: '100000',
    maxDiscountAmount: '30000', startDate: '2026-11-17T00:00', endDate: '2027-01-01T00:00',
    totalUsageLimit: '100', perCustomerUsageLimit: '2',
  })) await formInput(name, value);
  await click('Lưu thông tin', 'dialog button[type=submit]');
  await until('!document.querySelector("dialog[open]") && document.body.innerText.includes("12.25%")');
  let list = await request().then((response) => response.json());
  const created = list.find((row) => row.code === code);
  assert.equal(created.discountValue, 12.25); assert.equal(created.totalUsageLimit, 100);
  assert.equal(created.perCustomerUsageLimit, 2); assert.equal(created.minOrderAmount, 100000);
  assert.equal(created.maxDiscountAmount, 30000); assert.equal(created.startDate, '2026-11-16T17:00:00+00:00');
  await cdp('Page.reload'); await ready();
  await until(`document.body.innerText.includes(${JSON.stringify(code)})`);
  assert.ok(await evaluate(`document.body.innerText.includes(${JSON.stringify(code)})`));
  await click('Sửa', `button[aria-label="Sửa ${code}"]`);
  assert.equal(await evaluate('document.querySelector(\'dialog [name="startDate"]\').value'), '2026-11-17T00:00');
  await formInput('discountType', 'Fixed'); await formInput('discountValue', '25000');
  await formInput('name', 'Đã lưu cố định');
  for (const name of ['maxDiscountAmount', 'totalUsageLimit', 'perCustomerUsageLimit']) await formInput(name, '');
  await click('Lưu thông tin', 'dialog button[type=submit]');
  await until('!document.querySelector("dialog[open]") && document.body.innerText.includes("Đã lưu cố định")');
  let saved = await request(`/${created.id}`).then((response) => response.json());
  assert.equal(saved.discountType, 'Fixed'); assert.equal(saved.discountValue, 25000);
  for (const name of ['maxDiscountAmount', 'totalUsageLimit', 'perCustomerUsageLimit']) assert.equal(saved[name], null);
  await click('Tắt', `button[aria-label="Tắt ${code}"]`);
  await until(`Boolean(document.querySelector('button[aria-label="Bật ${code}"]'))`);
  assert.equal((await request(`/${created.id}`).then((response) => response.json())).isActive, false);
  await click('Bật', `button[aria-label="Bật ${code}"]`);
  await until(`Boolean(document.querySelector('button[aria-label="Tắt ${code}"]'))`);
  // A separate HTTP client changes SQL; the open UI must refetch without manual reload.
  const changed = await request(`/${created.id}`, 'PUT', { ...saved, id: undefined, name: 'Cập nhật từ máy khác', isActive: true });
  assert.equal(changed.status, 200);
  await until('document.body.innerText.includes("Cập nhật từ máy khác")');
  await click('Thêm voucher');
  for (const [name, value] of Object.entries({ code, name: 'Trùng mã', discountValue: '10' })) await formInput(name, value);
  await click('Lưu thông tin', 'dialog button[type=submit]');
  await until('Boolean(document.querySelector("dialog [role=alert]"))');
  assert.ok(await evaluate('document.querySelector("dialog [role=alert]").textContent.includes("đã tồn tại")'));
  assert.equal((await request().then((response) => response.json())).length, 1);
  assert.equal(await evaluate('document.querySelector(\'dialog [name="name"]\').value'), 'Trùng mã');
  await click('Đóng', 'dialog button');
  await screenshot('vouchers-admin-desktop.png');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true });
  await screenshot('vouchers-admin-mobile.png');
  assert.equal(await evaluate('document.documentElement.scrollWidth <= innerWidth'), true);
  await click('Sửa', `button[aria-label="Sửa ${code}"]`);
  assert.equal(await evaluate('document.documentElement.scrollWidth <= innerWidth'), true);
  await screenshot('vouchers-form-mobile.png');
  await click('Đóng', 'dialog button');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
  await cdp('Network.setBlockedURLs', { urls: ['*/api/admin/vouchers*'] });
  await click('Tải lại');
  await until('Boolean(document.querySelector(".admin-module [role=alert]"))');
  assert.equal(await evaluate('document.querySelectorAll(".admin-module tbody tr").length'), 0);
  await cdp('Network.setBlockedURLs', { urls: [] });
  await click('Thử lại'); await ready();
  await logout(); await login('cashier01', 'Cashier');
  await go('/admin/promotions'); await ready();
  assert.equal(await evaluate('[...document.querySelectorAll(".admin-module button")].some(button => ["Thêm voucher", "Sửa", "Tắt", "Bật"].includes(button.textContent.trim()))'), false);
  assert.equal((await request('', 'POST', { ...saved, id: undefined })).status, 403);
  assert.equal((await request(`/${created.id}`, 'PUT', { ...saved, id: undefined })).status, 403);
  assert.equal((await request(`/${created.id}/status`, 'PUT', { isActive: false })).status, 403);
  await screenshot('vouchers-cashier-readonly.png');
  await logout(); await login('waiter01', 'Waiter');
  assert.equal(await evaluate('Boolean(document.querySelector(\'a[href="/admin/promotions"]\'))'), false);
  await go('/admin/promotions', 'document.querySelector(".admin-shell")');
  await until('location.pathname !== "/admin/promotions"');
  assert.equal((await request()).status, 403);
  console.log('PASS voucher browser/API/SQL: create percent, edit fixed/null limits, reload persistence, toggle, realtime, server conflict, outage recovery, mobile, Cashier read-only, Waiter denied, anonymous 401.');
} finally { socket.close(); }
