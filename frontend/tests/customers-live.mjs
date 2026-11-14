import assert from 'node:assert/strict';
import { cdp, evaluate, until, socket, go, click, input, screenshot, errors } from './cdp.mjs';

// These checks use real UI logins, HTTP/JWT and isolated SQL. No fetch interception or demo identity.
const apiUrl = process.env.CUSTOMERS_LIVE_API_URL;
if (!apiUrl) throw Error('Run tests/run-customers-live.mjs with AUTH_CHECKS_SQL_CONNECTION.');
const responses = [];
socket.addEventListener('message', ({ data }) => {
  const message = JSON.parse(data);
  if (message.method === 'Network.responseReceived') {
    const { url, status } = message.params.response;
    const pathname = new URL(url).pathname;
    if (pathname.startsWith('/api/')) responses.push({ path: pathname, status });
  }
});
let adminToken;

async function adminRequest(path, method = 'GET') {
  const response = await fetch(`${apiUrl}/api/admin/__checks/${path}`, {
    method, headers: { Authorization: `Bearer ${adminToken}` },
  });
  assert.equal(response.status, method === 'GET' ? 200 : 204);
  return method === 'GET' ? response.json() : null;
}
const sqlSnapshot = () => adminRequest('customers-sql');

async function login(accountType, identifier, password) {
  const route = accountType === 'customer' ? '/client/auth' : '/admin/auth';
  await go(route, 'document.querySelector(".auth-form")');
  await input('#auth-identifier', identifier);
  await input('#auth-password', password);
  await click('Đăng nhập', 'form.auth-form button[type="submit"]');
  await until(`location.pathname !== ${JSON.stringify(route)} && Boolean(document.querySelector(${JSON.stringify(accountType === 'customer' ? '.client-shell' : '.admin-shell')}))`);
}

async function endSession(accountType) {
  await evaluate(`(async () => {
    const { logout, clearStoredSession } = await import('/src/shared/auth/api.js');
    await logout(${JSON.stringify(accountType)});
    clearStoredSession();
  })()`);
}

async function browserRequest(path, options = {}) {
  return evaluate(`(async () => {
    const { apiRequest } = await import('/src/shared/auth/api.js');
    try { return { status: 200, body: await apiRequest(${JSON.stringify(path)}, ${JSON.stringify(options)}) }; }
    catch (error) { return { status: error.status, body: error.body }; }
  })()`);
}

function hasMoney(expression, amount) {
  return `${expression}.replace(/[^0-9]/g, '').includes(${JSON.stringify(String(amount))})`;
}

try {
  await cdp('Runtime.enable'); await cdp('Page.enable'); await cdp('Network.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });

  await login('employee', 'admin', 'Admin-check-password-2026!');
  adminToken = await evaluate('JSON.parse(localStorage.getItem("cybermanagement.auth.v1")).accessToken');
  const original = await sqlSnapshot();
  const walletCustomer = original.customers.find(row => row.phone === '0908000001');
  const emptyCustomer = original.customers.find(row => row.phone === '0908000002');
  assert.equal(walletCustomer.walletBalance, 123456);
  assert.equal(emptyCustomer.walletBalance, 0);

  await go('/admin/customers', 'document.body.innerText.includes("SQL Wallet Customer")');
  assert.equal(await evaluate('Boolean(document.querySelector(".cafe-demo-note"))'), false);
  await click('Thêm khách hàng');
  await until('Boolean(document.querySelector("dialog[open] form"))');
  await input('dialog input[name="fullName"]', 'SQL Created From UI');
  await input('dialog input[name="phone"]', '0908000099');
  await input('dialog input[name="email"]', 'created@sql.example');
  await input('dialog input[name="password"]', 'Created-check-password-2026!');
  await evaluate('document.querySelector("dialog form button[type=submit]").click()');
  await until('!document.querySelector("dialog[open]") && document.body.innerText.includes("SQL Created From UI")');
  const createdSnapshot = await sqlSnapshot();
  const created = createdSnapshot.customers.find(row => row.phone === '0908000099');
  assert.ok(created, 'UI form must insert a real SQL customer');
  assert.equal(created.fullName, 'SQL Created From UI');
  assert.equal(created.email, 'created@sql.example');
  assert.equal(created.walletBalance, 0);
  assert.equal(created.status, 'Active');
  assert.equal(createdSnapshot.transactions.filter(row => row.customerId === created.customerId).length, 0);
  const beforeReload = responses.length;
  await cdp('Page.reload');
  await until('document.body.innerText.includes("SQL Created From UI")');
  assert.deepEqual(await sqlSnapshot(), createdSnapshot);
  assert.ok(responses.slice(beforeReload).some(row => row.path === '/api/admin/customers' && row.status === 200));
  await screenshot('customers-live-admin-created-reload.png');
  console.log('PASS admin UI create -> independent SQL profile/zero balance -> reload reads persisted SQL');

  await click('SQL Wallet Customer', 'button.admin-link');
  await until('document.querySelector("dialog[open]")?.innerText.includes("SQL order payment") && document.querySelector("dialog[open]")?.innerText.includes("wallet@sql.example")');
  assert.ok(await evaluate(hasMoney('document.querySelector("dialog[open]").innerText', walletCustomer.walletBalance)));
  assert.ok(responses.some(row => row.path === `/api/admin/customers/${walletCustomer.customerId}` && row.status === 200));
  assert.ok(responses.some(row => row.path === `/api/admin/customers/${walletCustomer.customerId}/wallet` && row.status === 200));
  await cdp('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
  await cdp('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
  await until('!document.querySelector("dialog[open]")');
  await click('SQL Created From UI', 'button.admin-link');
  await until('document.querySelector("dialog[open]")?.innerText.includes("Chưa có giao dịch ví") && document.querySelector("dialog[open]")?.innerText.includes("created@sql.example")');
  assert.ok(await evaluate('document.querySelector("dialog[open]").innerText.includes("created@sql.example")'));
  await cdp('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
  await cdp('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
  await until('!document.querySelector("dialog[open]")');
  console.log('PASS admin profile dialog reads SQL profile/wallet and a new customer has empty ledger');

  await endSession('employee');
  await login('employee', 'waiter01', 'Waiter-check-password-2026!');
  await go('/admin/customers', 'document.body.innerText.includes("SQL Created From UI")');
  assert.equal(await evaluate('[...document.querySelectorAll("button")].some(button => button.textContent.includes("Thêm khách hàng"))'), false);
  const forbiddenCreate = await browserRequest('/admin/customers', {
    method: 'POST', body: JSON.stringify({ fullName: 'Forbidden', phone: '0908000088', password: 'Forbidden-check-password-2026!' }),
  });
  assert.equal(forbiddenCreate.status, 403);
  assert.equal((await browserRequest(`/admin/customers/${walletCustomer.customerId}/wallet`)).status, 403);
  assert.deepEqual(await sqlSnapshot(), createdSnapshot, 'Forbidden browser requests must not change SQL');
  console.log('PASS waiter reads profiles, cannot create/read wallet, forged requests return 403 and SQL stays unchanged');

  await endSession('employee');
  await login('customer', '0908000001', 'Customer-check-password-2026!');
  await go('/client/account', 'document.querySelector(".account-page") && document.body.innerText.includes("wallet@sql.example")');
  assert.ok(await evaluate('document.querySelector(".account-page").innerText.includes("SQL Wallet Customer")'));
  assert.equal(await evaluate('Boolean(document.querySelector(".account-page input[type=password]"))'), false);
  assert.equal(await evaluate('Boolean(document.querySelector(".account-page .home-demo-strip"))'), false);
  const ownProfile = await browserRequest('/customers/me');
  assert.equal(ownProfile.body.customerId, walletCustomer.customerId);
  assert.equal(ownProfile.body.fullName, walletCustomer.fullName);
  assert.equal((await browserRequest(`/admin/customers/${emptyCustomer.customerId}`)).status, 403);
  assert.equal((await browserRequest(`/admin/customers/${emptyCustomer.customerId}/wallet`)).status, 403);

  await go('/client/wallet', 'document.querySelector(".wallet-page") && document.body.innerText.includes("SQL order payment")');
  const ownWallet = await browserRequest('/customers/me/wallet');
  assert.equal(ownWallet.body.customerId, walletCustomer.customerId);
  assert.equal(ownWallet.body.balance, walletCustomer.walletBalance);
  assert.equal(ownWallet.body.totalCount, 2);
  const sqlLedger = createdSnapshot.transactions.filter(row => row.customerId === walletCustomer.customerId)
    .sort((a, b) => b.transactionId - a.transactionId);
  assert.deepEqual(ownWallet.body.transactions.map(({ transactionId, amount, balanceAfter, type, description }) => ({ transactionId, amount, balanceAfter, type, description })),
    sqlLedger.map(({ transactionId, amount, balanceAfter, type, description }) => ({ transactionId, amount, balanceAfter, type, description })));
  assert.ok(await evaluate(hasMoney('document.querySelector(".wallet-page").innerText', 123456)));
  assert.ok(await evaluate('document.querySelector(".wallet-page").innerText.includes("SQL counter collection")'));
  assert.equal(await evaluate('Boolean(document.querySelector(".wallet-page .home-demo-strip"))'), false);
  const beforeWalletReload = responses.length;
  await cdp('Page.reload');
  await until('document.querySelector(".wallet-page") && document.body.innerText.includes("SQL order payment")');
  assert.ok(responses.slice(beforeWalletReload).some(row => row.path === '/api/customers/me/wallet' && row.status === 200));
  await screenshot('customers-live-own-wallet.png');
  console.log('PASS customer profile/wallet belongs to JWT customer; UI balance and signed ledger match independent SQL after reload');

  await adminRequest('wallet-failure?enabled=true', 'POST');
  await cdp('Page.reload');
  await until('Boolean(document.querySelector(".wallet-page [role=alert]"))');
  assert.equal(await evaluate('document.querySelector(".wallet-page").innerText.includes("SQL order payment")'), false);
  assert.equal(await evaluate(hasMoney('document.querySelector(".wallet-page").innerText', 123456)), false);
  await screenshot('customers-live-wallet-error.png');
  await adminRequest('wallet-failure?enabled=false', 'POST');
  await click('Thử lại', '.wallet-page button');
  await until('document.querySelector(".wallet-page").innerText.includes("SQL order payment")');
  assert.ok(await evaluate(hasMoney('document.querySelector(".wallet-page").innerText', 123456)));
  assert.deepEqual(await sqlSnapshot(), createdSnapshot, 'Read failures/retries must not mutate wallet or ledger');
  console.log('PASS real API 503 hides wallet/ledger with visible error; retry restores SQL values and no demo fallback');

  await endSession('customer');
  await login('customer', '0908000002', 'Customer-check-password-2026!');
  await go('/client/wallet', 'document.querySelector(".wallet-page") && document.body.innerText.includes("Chưa có giao dịch")');
  const emptyWallet = await browserRequest('/customers/me/wallet');
  assert.equal(emptyWallet.body.customerId, emptyCustomer.customerId);
  assert.equal(emptyWallet.body.balance, 0);
  assert.equal(emptyWallet.body.totalCount, 0);
  assert.deepEqual(emptyWallet.body.transactions, []);
  assert.equal(await evaluate('document.querySelector(".wallet-page").innerText.includes("SQL order payment")'), false);
  await screenshot('customers-live-empty-wallet.png');
  console.log('PASS second customer has own zero wallet and empty ledger; prior customer data is absent');

  for (const apiPath of ['/api/auth/login', '/api/admin/customers', '/api/auth/customer/login', '/api/customers/me', '/api/customers/me/wallet']) {
    assert.ok(responses.some(row => row.path === apiPath && [200, 201].includes(row.status)), `Missing real API response: ${apiPath}`);
  }
  assert.ok(responses.some(row => row.path === '/api/admin/customers' && row.status === 201));
  assert.ok(responses.some(row => row.path === '/api/customers/me/wallet' && row.status === 503));
  assert.deepEqual(errors.filter(message => !message.includes('403') && !message.includes('503')), []);
} finally { socket.close(); }
