import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import path from 'node:path';
import { cdp, evaluate, until, socket, go, click, input, screenshot, outputDir } from './cdp.mjs';

const api = process.env.INVOICES_LIVE_API_URL;
if (!api) throw Error('Use run-invoices-live.mjs with a disposable SQL database.');
const password = 'Invoice-check-only-2026!';
const responses = [];
socket.addEventListener('message', ({ data }) => {
  const message = JSON.parse(data);
  if (message.method === 'Network.responseReceived') responses.push(message.params.response);
});
async function login(customer, name) {
  await go(customer ? '/client/auth' : '/admin/auth', 'document.querySelector(".auth-form")');
  await input('#auth-identifier', name); await input('#auth-password', password);
  await click('Đăng nhập', 'form.auth-form button[type="submit"]');
  await until(`location.pathname !== ${JSON.stringify(customer ? '/client/auth' : '/admin/auth')} && !document.querySelector('.auth-form')`);
  await go(customer ? '/client/invoices' : '/admin/invoices', 'document.querySelector(".invoice-workspace")');
  await until('!document.body.innerText.includes("Đang tải hóa đơn…")');
}
async function logout() {
  await evaluate(`document.querySelector('dialog[open] .home-icon-button')?.click()`);
  await evaluate(`document.querySelector('button[aria-label="Đăng xuất"]').click()`);
  await until('!localStorage.getItem("cybermanagement.auth.v1") && Boolean(document.querySelector(".auth-form"))');
}
try {
  await cdp('Runtime.enable'); await cdp('Page.enable'); await cdp('Network.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1050, deviceScaleFactor: 1, mobile: false });
  await login(false, 'admin');
  assert.ok(await evaluate('document.body.innerText.includes("Chưa có hóa đơn được phát hành")'));
  const token = await evaluate('JSON.parse(localStorage.getItem("cybermanagement.auth.v1")).accessToken');
  const headers = { Authorization: `Bearer ${token}` };
  const sourceResponse = await fetch(`${api}/__checks/invoices/source`, { headers });
  assert.equal(sourceResponse.status, 200);
  const source = await sourceResponse.json();
  await click('Phát hành'); await input('.sql-invoice-issue input', source.orderId);
  await click('Phát hành hóa đơn', '.sql-invoice-issue button');
  await until('Boolean(document.querySelector("dialog[open] .invoice-snapshot"))');
  assert.ok(await evaluate('document.querySelector("dialog .invoice-snapshot").textContent.includes("Food at order time")'));
  assert.ok(await evaluate('document.querySelector("dialog .invoice-snapshot").textContent.includes("90.000 đ")'));
  const issued = await fetch(`${api}/api/admin/invoices`, { headers }).then(response => response.json());
  assert.equal(issued.length, 1); assert.equal(issued[0].paidAmount, 90000); assert.equal(issued[0].lines[0].unitPrice, 100000);
  const changed = await fetch(`${api}/__checks/invoices/change-product`, { method: 'POST', headers });
  assert.equal(changed.status, 200);
  await cdp('Page.reload'); await until('document.querySelectorAll(".sql-invoice-table tbody tr").length === 1');
  await evaluate('document.querySelector(".sql-invoice-table tbody button").click()');
  await until('Boolean(document.querySelector("dialog .invoice-snapshot"))');
  assert.ok(await evaluate('document.querySelector("dialog .invoice-snapshot").textContent.includes("Food at order time")'));
  assert.equal(await evaluate('document.querySelector("dialog .invoice-snapshot").textContent.includes("Changed after issue")'), false);
  await screenshot('invoices-live-admin-detail.png');
  await evaluate('window.__invoicePrintCalls = 0; window.print = () => { window.__invoicePrintCalls++; };');
  await click('In hóa đơn', 'dialog button');
  assert.equal(await evaluate('window.__invoicePrintCalls'), 1);
  await cdp('Emulation.setEmulatedMedia', { media: 'print' });
  await screenshot('invoices-live-print.png');
  const pdf = await cdp('Page.printToPDF', { printBackground: true, preferCSSPageSize: true });
  const bytes = Buffer.from(pdf.data, 'base64'); assert.ok(bytes.length > 10000);
  await writeFile(path.join(outputDir, 'invoices-live-snapshot.pdf'), bytes);
  await cdp('Emulation.setEmulatedMedia', { media: 'screen' });
  const printResponse = await fetch(`${api}/api/admin/invoices/${issued[0].id}/print`, { headers });
  assert.equal(printResponse.status, 200);
  const html = await printResponse.text(); assert.ok(html.includes('Food at order time')); assert.ok(!html.includes('Changed after issue'));
  await logout(false); await login(true, '0900031001');
  await until('document.querySelectorAll(".sql-invoice-table tbody tr").length === 1');
  assert.equal(await evaluate('[...document.querySelectorAll(".invoice-workspace button")].some(button => button.textContent.trim() === "Phát hành")'), false);
  await screenshot('invoices-live-customer-desktop.png');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true });
  await screenshot('invoices-live-customer-mobile.png');
  assert.equal(await evaluate('document.documentElement.scrollWidth <= innerWidth'), true);
  await evaluate('document.querySelector(".sql-invoice-table tbody button").click()');
  await until('Boolean(document.querySelector("dialog .invoice-snapshot"))');
  await screenshot('invoices-live-customer-mobile-detail.png');
  assert.equal(await evaluate('document.documentElement.scrollWidth <= innerWidth'), true);
  await logout(true); await login(true, '0900031002');
  assert.ok(await evaluate('document.body.innerText.includes("Chưa có hóa đơn được phát hành")'));
  assert.equal(await evaluate('document.querySelectorAll(".sql-invoice-table tbody tr").length'), 0);
  await cdp('Network.setBlockedURLs', { urls: ['*/api/client/invoices*'] });
  await click('Tải lại'); await until('Boolean(document.querySelector(".invoice-workspace [role=alert]"))');
  assert.equal(await evaluate('document.querySelectorAll(".sql-invoice-table tbody tr").length'), 0);
  await cdp('Network.setBlockedURLs', { urls: [] });
  await click('Thử lại'); await until('document.body.innerText.includes("Chưa có hóa đơn được phát hành")');
  assert.ok(responses.some(response => new URL(response.url).pathname === '/api/admin/invoices' && response.status === 201));
  assert.ok(responses.some(response => new URL(response.url).pathname === '/api/client/invoices' && response.status === 200));
  console.log('PASS invoice live SQL: real login, UI issue POST 201, persisted reload, price-change snapshot, admin/client ownership, PDF print, mobile, empty/error/recovery.');
} finally { socket.close(); }
