import assert from 'node:assert/strict';
import { cdp, evaluate, until, socket, go, click, input, screenshot, errors } from './cdp.mjs';

// No Fetch interception, injected identity, or fetch replacement. All data comes from SQL.
const apiUrl = process.env.COMPUTERS_LIVE_API_URL;
if (!apiUrl) throw Error('Run via tests/run-computers-live.mjs to use an isolated SQL database.');
const responses = [];
socket.addEventListener('message', ({ data }) => {
  const message = JSON.parse(data);
  if (message.method === 'Network.responseReceived') {
    const { url, status } = message.params.response;
    if (new URL(url).pathname.startsWith('/api/')) responses.push({ path: new URL(url).pathname, status });
  }
});
let adminToken;
async function sqlSnapshot() {
  const response = await fetch(`${apiUrl}/api/admin/__checks/computers-sql`, {
    headers: { Authorization: `Bearer ${adminToken}` },
  });
  assert.equal(response.status, 200);
  return response.json();
}
async function login(username, role) {
  await go('/admin/auth', 'document.querySelector(".auth-form")');
  await input('#auth-identifier', username);
  await input('#auth-password', `${role}-check-password-2026!`);
  await click('Đăng nhập', 'form.auth-form button[type="submit"]');
  await until('location.pathname !== "/admin/auth" && Boolean(document.querySelector(".admin-shell"))');
  await go('/admin/computers', 'document.querySelectorAll(".cafe-station").length === 1');
}
async function openComputer() {
  await evaluate('document.querySelector(".cafe-station").click()');
  await until('document.querySelector("dialog[open] h2")?.textContent === "SQL-PC-01"');
}

try {
  await cdp('Runtime.enable'); await cdp('Page.enable'); await cdp('Network.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 900, deviceScaleFactor: 1, mobile: false });
  await login('admin', 'Admin');
  adminToken = await evaluate('JSON.parse(localStorage.getItem("cybermanagement.auth.v1")).accessToken');
  const initial = await sqlSnapshot();
  assert.equal(initial.length, 1);
  assert.equal(initial[0].status, 'Available');
  assert.equal(await evaluate('document.querySelector(".cafe-station strong").textContent'), initial[0].computerName);
  assert.ok(await evaluate(`document.querySelector('select[aria-label="Lọc phòng máy"]').textContent.includes('SQL room')`));
  assert.ok(await evaluate(`document.querySelector('select[aria-label="Lọc loại máy"]').textContent.includes('SQL type')`));
  await openComputer();
  await click('Chuyển bảo trì', 'dialog button');
  await until('!document.querySelector("dialog[open]") && document.body.innerText.includes("Đã cập nhật trạng thái SQL-PC-01")');
  const saved = await sqlSnapshot();
  assert.deepEqual(saved, [{ ...initial[0], status: 'Maintenance' }]);
  const beforeReload = responses.length;
  await cdp('Page.reload');
  await until('document.querySelectorAll(".cafe-station").length === 1');
  await openComputer();
  assert.equal(await evaluate('document.querySelector("dialog .cafe-badge").classList.contains("maintenance")'), true);
  assert.deepEqual(await sqlSnapshot(), saved);
  assert.ok(responses.slice(beforeReload).some(row => row.path === '/api/auth/me' && row.status === 200));
  assert.ok(responses.slice(beforeReload).some(row => row.path === '/api/admin/computers' && row.status === 200));
  await screenshot('computers-live-admin-reload.png');
  console.log('PASS real UI login -> SQL lists -> UI PATCH 200 -> SQL Maintenance -> reload GET and UI Maintenance');

  // End the real session before logging in with a different SQL-seeded account.
  await evaluate(`(async () => {
    const { logout } = await import('/src/shared/auth/api.js');
    await logout('employee');
    localStorage.removeItem('cybermanagement.auth.v1');
  })()`);
  await login('waiter01', 'Waiter');
  const permissions = await evaluate(`(async () => {
    const { apiRequest } = await import('/src/shared/auth/api.js');
    return (await apiRequest('/auth/me')).permissions;
  })()`);
  assert.ok(permissions.includes('computer.view'));
  assert.ok(!permissions.includes('computer.status.update'));
  await openComputer();
  assert.equal(await evaluate('Boolean(document.querySelector("dialog .cafe-dialog-actions button"))'), false);
  const beforeDenied = await sqlSnapshot();
  const status = await evaluate(`(async () => {
    const { apiRequest } = await import('/src/shared/auth/api.js');
    try {
      await apiRequest('/admin/computers/${initial[0].computerId}/status', {
        method: 'PATCH', body: JSON.stringify({ status: 'Available', expectedStatus: 'Maintenance' })
      });
      return 200;
    } catch (error) { return error.status; }
  })()`);
  assert.equal(status, 403);
  assert.deepEqual(await sqlSnapshot(), beforeDenied, 'Denied PATCH must leave every Computer column unchanged in SQL');
  await cdp('Page.reload');
  await until('document.querySelectorAll(".cafe-station").length === 1');
  await openComputer();
  assert.equal(await evaluate('document.querySelector("dialog .cafe-badge").classList.contains("maintenance")'), true);
  await screenshot('computers-live-readonly-denied.png');
  for (const path of ['/api/auth/login', '/api/auth/me', '/api/admin/computers', '/api/admin/computer-types', '/api/admin/computer-rooms']) {
    assert.ok(responses.some(row => row.path === path && row.status === 200), `Missing real response: ${path}`);
  }
  assert.ok(responses.some(row => row.path.endsWith('/status') && row.status === 200));
  assert.ok(responses.some(row => row.path.endsWith('/status') && row.status === 403));
  // Chrome may report the expected forbidden request as a console error.
  assert.deepEqual(errors.filter(message => !message.includes('403')), []);
  console.log('PASS real read-only login -> view allowed -> forged PATCH 403 -> fresh SQL snapshot unchanged -> reload unchanged');
} finally { socket.close(); }
