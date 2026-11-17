import assert from 'node:assert/strict';
import { cdp, evaluate, until, socket, go, click, input, screenshot, errors } from './cdp.mjs';

// Reuse the disposable live SQL host; never replace fetch or inject auth/API fixtures.
const apiUrl = process.env.COMPUTERS_LIVE_API_URL;
if (!apiUrl) throw Error('Run via tests/run-computers-live.mjs staff-live to use an isolated SQL database.');
const requests = new Map();
const responses = [];
const demoKey = 'tram.client.dbml.demo.v2';
let adminToken;
socket.addEventListener('message', ({ data }) => {
  const message = JSON.parse(data);
  if (message.method === 'Network.requestWillBeSent') {
    const { requestId, request } = message.params;
    if (new URL(request.url).pathname.startsWith('/api/')) {
      requests.set(requestId, { path: new URL(request.url).pathname, method: request.method, body: request.postData });
    }
  }
  if (message.method === 'Network.responseReceived') {
    const request = requests.get(message.params.requestId);
    if (request) responses.push({ ...request, status: message.params.response.status });
  }
});

async function sqlSnapshot() {
  const response = await fetch(`${apiUrl}/api/admin/__checks/employees-sql`, {
    headers: { Authorization: `Bearer ${adminToken}` },
  });
  assert.equal(response.status, 200);
  return response.json();
}
async function assertApiProfile(saved) {
  const response = await fetch(`${apiUrl}/api/employees/${saved.employeeId}`, {
    headers: { Authorization: `Bearer ${adminToken}` },
  });
  assert.equal(response.status, 200);
  const profile = await response.json();
  for (const key of ['employeeId', 'fullName', 'phone', 'roleName', 'version']) {
    assert.equal(profile[key], saved[key], `GET profile must match fresh SQL: ${key}`);
  }
}
async function roleId(name) {
  return evaluate(`Number([...document.querySelector("dialog select[name=role]").options].find(option => option.textContent === ${JSON.stringify(name)}).value)`);
}
async function fillForm(name, phone, role) {
  await input('dialog [name=display_name]', name);
  await input('dialog [name=phone]', phone);
  await input('dialog [name=role]', role);
  await input('dialog [name=position]', 'Thu ngân');
  await input('dialog [name=shift]', '08:00–16:00');
}
async function openCreate() {
  await click('Thêm nhân viên');
  await until('Boolean(document.querySelector("dialog select[name=role]"))');
}
const submit = () => evaluate('document.querySelector("dialog[open] form").requestSubmit()');
const demoSnapshot = () => evaluate(`localStorage.getItem(${JSON.stringify(demoKey)})`);
const rowText = (name) => evaluate(`[...document.querySelectorAll("tbody tr")].find(row => row.textContent.includes(${JSON.stringify(name)}))?.textContent`);
async function reloadAndRead(name, phone, role) {
  const before = responses.length;
  await cdp('Page.reload', { ignoreCache: true });
  await until(`document.querySelector("tbody")?.textContent.includes(${JSON.stringify(name)})`);
  const text = await rowText(name);
  assert.ok(text.includes(phone));
  assert.ok(text.includes(role));
  for (const path of ['/api/auth/me', '/api/employees']) {
    assert.ok(responses.slice(before).some(row => row.path === path && row.method === 'GET' && row.status === 200), `Reload must read ${path} from the server`);
  }
}

try {
  await cdp('Runtime.enable'); await cdp('Page.enable'); await cdp('Network.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 900, deviceScaleFactor: 1, mobile: false });
  await go('/admin/auth', 'document.querySelector(".auth-form")');
  await input('#auth-identifier', 'admin');
  await input('#auth-password', 'Admin-check-password-2026!');
  await click('Đăng nhập', 'form.auth-form button[type=submit]');
  await until('location.pathname !== "/admin/auth" && Boolean(document.querySelector(".admin-shell"))');
  await go('/admin/staff', 'document.querySelectorAll("tbody tr").length === 5');
  adminToken = await evaluate('JSON.parse(localStorage.getItem("cybermanagement.auth.v1")).accessToken');
  const initial = await sqlSnapshot();
  const adminId = initial.employees.find(row => row.username === 'admin').employeeId;
  const demoBefore = await demoSnapshot();

  await openCreate();
  const waiterId = await roleId('Waiter');
  await fillForm('SQL Staff Created', '0900017011', waiterId);
  await input('dialog [name=password]', 'Staff-live-password-2026!');
  await submit();
  await until('!document.querySelector("dialog[open]") && document.querySelector("tbody")?.textContent.includes("SQL Staff Created")');
  const created = await sqlSnapshot();
  assert.equal(created.employees.length, initial.employees.length + 1);
  const saved = created.employees.find(row => row.username === '0900017011');
  assert.ok(saved);
  assert.equal(saved.fullName, 'SQL Staff Created');
  assert.equal(saved.phone, '0900017011');
  assert.equal(saved.roleId, waiterId);
  assert.equal(saved.roleName, 'Waiter');
  assert.equal(created.audits.length, initial.audits.length + 1);
  assert.equal(created.audits.at(-1).employeeId, adminId);
  assert.equal(created.audits.at(-1).entityId, saved.employeeId);
  assert.equal(created.audits.at(-1).action, 'Created');
  const post = responses.find(row => row.method === 'POST' && row.path === '/api/employees');
  assert.equal(post?.status, 201);
  assert.equal(JSON.parse(post.body).roleId, waiterId);
  await assertApiProfile(saved);
  assert.equal(await demoSnapshot(), demoBefore, 'Creating an employee must not modify the demo localStorage database');
  await reloadAndRead('SQL Staff Created', '0900017011', 'Waiter');
  assert.equal(await demoSnapshot(), demoBefore);
  console.log('PASS real staff create -> POST 201 -> independent SQL/GET read -> browser reload preserves name/phone/role; demo storage unchanged');

  await evaluate(`(() => {
    const row = [...document.querySelectorAll("tbody tr")].find(row => row.textContent.includes("SQL Staff Created"));
    [...row.querySelectorAll("button")].find(button => button.textContent === "Chỉnh sửa").click();
  })()`);
  await until('Boolean(document.querySelector("dialog select[name=role]"))');
  assert.equal(await evaluate('document.querySelector("dialog select[name=role]").value'), String(waiterId));
  const managerId = await roleId('Manager');
  await fillForm('SQL Staff Updated', '0900017022', managerId);
  await submit();
  await until('!document.querySelector("dialog[open]") && document.querySelector("tbody")?.textContent.includes("SQL Staff Updated")');
  const updated = await sqlSnapshot();
  const changed = updated.employees.find(row => row.employeeId === saved.employeeId);
  assert.equal(updated.employees.length, created.employees.length);
  assert.equal(changed.fullName, 'SQL Staff Updated');
  assert.equal(changed.phone, '0900017022');
  assert.equal(changed.username, saved.username);
  assert.equal(changed.roleId, managerId);
  assert.equal(changed.roleName, 'Manager');
  assert.notEqual(changed.version, saved.version);
  const put = responses.find(row => row.method === 'PUT' && row.path === `/api/employees/${saved.employeeId}`);
  assert.equal(put?.status, 200);
  assert.equal(JSON.parse(put.body).roleId, managerId);
  assert.equal(JSON.parse(put.body).expectedVersion, saved.version);
  assert.equal(updated.audits.length, created.audits.length + 1);
  assert.equal(updated.audits.at(-1).employeeId, adminId);
  assert.equal(updated.audits.at(-1).entityId, saved.employeeId);
  assert.equal(updated.audits.at(-1).action, 'Updated');
  await assertApiProfile(changed);
  assert.equal(await demoSnapshot(), demoBefore, 'Editing an employee/role must not modify the demo localStorage database');
  await reloadAndRead('SQL Staff Updated', '0900017022', 'Manager');
  assert.equal(await rowText('SQL Staff Created'), undefined);
  assert.deepEqual(await sqlSnapshot(), updated);
  assert.equal(await demoSnapshot(), demoBefore);
  await screenshot('staff-live-sql-after-reload.png');
  console.log('PASS real staff edit + role change -> PUT 200 with expectedVersion -> independent SQL/GET read -> reload preserves new profile and role');

  await openCreate();
  await fillForm('Rejected duplicate', '0900017011', waiterId);
  await input('dialog [name=password]', 'Staff-live-password-2026!');
  await submit();
  await until('Boolean(document.querySelector("dialog [role=alert]"))');
  assert.equal(responses.filter(row => row.method === 'POST' && row.path === '/api/employees').at(-1)?.status, 400);
  assert.equal(await evaluate('document.querySelector("dialog [name=display_name]").value'), 'Rejected duplicate');
  assert.equal(await evaluate('Boolean(document.querySelector(".admin-module [role=status]"))'), false, 'A rejected save must not display success');
  assert.deepEqual(await sqlSnapshot(), updated, 'Rejected save must not change employees or audit');
  assert.equal(await demoSnapshot(), demoBefore, 'Rejected save must not fall back to localStorage');
  await reloadAndRead('SQL Staff Updated', '0900017022', 'Manager');
  assert.equal(await rowText('Rejected duplicate'), undefined);

  const denied = await fetch(`${apiUrl}/api/employees`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: 'anonymous', fullName: 'Anonymous', password: 'Denied-password-2026!', roleId: waiterId }),
  });
  assert.equal(denied.status, 401);
  assert.deepEqual(await sqlSnapshot(), updated);
  assert.deepEqual(errors.filter(message => !message.includes('400')), []);
  console.log('PASS duplicate API 400 keeps form input, no false success/local fallback or SQL/audit change; anonymous write 401 leaves SQL unchanged');
} finally {
  socket.close();
}
