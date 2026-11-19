import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { cdp, evaluate, until, socket, errors, go, input, click, screenshot, sleep } from './cdp.mjs';
import { computerFixture } from './computer-fixture.mjs';

// UI/SDK fixtures complement the real transaction and SignalR checks in --sessions-only.
const fixture = computerFixture();
fixture.permissions.push('computer.manage', 'maintenance.view', 'maintenance.manage',
  'session.view', 'session.start', 'session.end', 'invoice.create', 'customer.view');
const sdk = await readFile(new URL('./realtime-signalr-fixture.mjs', import.meta.url), 'utf8');
const requests = [];
const failures = [];
const canceled = new Set();
let session = null;
let homeStatus = 200;
let balance = 77777;
const maintenance = new Map();
const profile = { customerId: 7, fullName: 'SQL customer', phone: '0900024001', email: null, status: 'Active', createdAtUtc: '2026-11-18T00:00:00Z' };
const makeSession = pc => ({ gamingSessionId: 240, computerId: pc.computerId, customerId: 7,
  computerName: pc.computerName, typeName: 'SQL type', cpu: 'API CPU', ram: '32 GB', gpu: 'API GPU',
  startTimeUtc: new Date(Date.now() - 90000).toISOString(), endTimeUtc: null, lastChargedAtUtc: null,
  pricePerHourSnapshot: 12345, totalCost: 321, status: 'Active',
  openedByEmployeeId: 7, closedByActorType: null, closeReason: null, invoiceId: null });
const legacySession = { ...makeSession(fixture.computers[1]), gamingSessionId: 241,
  customerId: 8, pricePerHourSnapshot: null, totalCost: 0 };
const legacyComputer = fixture.computers[3];
Object.assign(legacyComputer, { status: 'Occupied', hasOpenSession: true });
const endedWithoutTime = { ...makeSession(legacyComputer), gamingSessionId: 242, customerId: 9, status: 'Ended' };

async function handle({ requestId, request }) {
  if (request.url.includes('microsoft_signalr.js') || request.url.includes('/@microsoft/signalr/')) {
    await fulfill(200, sdk, 'application/javascript'); return;
  }
  const url = new URL(request.url);
  const path = url.pathname;
  const body = request.postData ? JSON.parse(request.postData) : null;
  requests.push({ path, method: request.method, body, headers: request.headers });
  let status = 200;
  let result = [];
  if (path.endsWith('/login')) result = { accessToken: path.includes('/customer/') ? 'customer-fixture' : 'staff-fixture', expiresAtUtc: new Date(Date.now() + 3600000).toISOString() };
  else if (path === '/api/auth/me') result = { employeeId: 7, username: 'fixture', role: 'Admin', permissions: fixture.permissions };
  else if (path === '/api/auth/customer/me') result = profile;
  else if (path === '/api/admin/computers') {
    if (request.method === 'POST') {
      result = { ...body, computerId: 105, status: 'Available', hasOpenSession: false, hasOpenMaintenance: false };
      fixture.computers.push(result);
    } else result = fixture.computers;
  } else if (/\/computers\/\d+\/maintenance/.test(path)) {
    const id = Number(path.split('/')[4]);
    const pc = fixture.computers.find(row => row.computerId === id);
    if (request.method === 'GET') result = maintenance.get(id) || [];
    else if (path.endsWith('/complete')) {
      pc.status = 'Available'; pc.hasOpenMaintenance = false;
      (maintenance.get(id) || []).forEach(row => { row.status = 'Completed'; row.endTimeUtc = new Date().toISOString(); });
      result = pc;
    } else {
      pc.status = 'Maintenance'; pc.hasOpenMaintenance = true;
      maintenance.set(id, [{ maintenanceId: 1, computerId: id, employeeId: 7, description: body.description, status: 'InProgress', startTimeUtc: new Date().toISOString(), endTimeUtc: null }]);
      result = pc;
    }
  } else if (/\/computers\/\d+$/.test(path) && request.method === 'PUT') {
    const pc = fixture.computers.find(row => row.computerId === Number(path.split('/').at(-1)));
    Object.assign(pc, body); result = pc;
  } else if (path === '/api/admin/computer-types') {
    if (request.method === 'POST') { result = { ...body, computerTypeId: 25 }; fixture.types.push(result); }
    else result = fixture.types;
  } else if (path === '/api/admin/computer-rooms') result = fixture.rooms;
  else if (path === '/api/admin/customers') result = { items: [profile], page: 1, pageSize: 20, totalCount: 1 };
  else if (path === '/api/admin/sessions') {
    if (request.method === 'POST') {
      const pc = fixture.computers.find(row => row.computerId === body.computerId);
      session = makeSession(pc); pc.status = 'Occupied'; pc.hasOpenSession = true; result = session;
    } else result = [legacySession, endedWithoutTime, ...(session ? [session] : [])];
  } else if (/\/sessions\/\d+\/close$/.test(path)) {
    const pc = fixture.computers.find(row => row.computerId === session.computerId);
    result = { ...session, status: 'Ended', endTimeUtc: new Date().toISOString(), closedByActorType: 'Staff', invoiceId: 19 };
    session = null; pc.status = 'Available'; pc.hasOpenSession = false;
  } else if (path === '/api/client/home') { status = homeStatus; result = { walletBalance: balance, session }; }
  else if (path === '/api/customers/me/wallet') result = { customerId: 7, balance, page: 1, pageSize: 20, totalCount: 0, transactions: [] };
  else if (path === '/api/customers/me') result = profile;
  else if (path === '/api/auth/logout' || path === '/api/auth/customer/logout') status = 204;
  await fulfill(status, JSON.stringify(status >= 400 ? { detail: 'Fixture API failure' } : result));
  async function fulfill(responseCode, data, contentType = 'application/json') {
    await cdp('Fetch.fulfillRequest', { requestId, responseCode,
      responseHeaders: [{ name: 'Content-Type', value: contentType }], body: Buffer.from(data || '').toString('base64') });
  }
}
const listener = ({ data }) => {
  const message = JSON.parse(data);
  if (message.method === 'Network.loadingFailed' && message.params.canceled) canceled.add(message.params.requestId);
  if (message.method === 'Fetch.requestPaused') handle(message.params).catch(error =>
    failures.push({ message: error.message, networkId: message.params.networkId }));
};
const open = async name => {
  await evaluate(`Array.from(document.querySelectorAll('.cafe-station')).find(row=>row.querySelector('strong').textContent===${JSON.stringify(name)}).click()`);
  await until('document.querySelector("dialog[open]") && !document.querySelector("dialog").textContent.includes("Đang tải phiên")');
};
const select = async (selector, value) => evaluate(`(() => { const node=document.querySelector(${JSON.stringify(selector)}); node.value=${JSON.stringify(String(value))}; node.dispatchEvent(new Event('change',{bubbles:true})); })()`);
const emit = async name => evaluate(`globalThis.__realtimeConnections.at(-1).emit(${JSON.stringify(name)}, {entityId:240})`);
try {
  await cdp('Runtime.enable'); await cdp('Page.enable'); await cdp('Network.enable');
  socket.addEventListener('message', listener);
  await cdp('Fetch.enable', { patterns: [
    { urlPattern: '*/api/*', requestStage: 'Request' }, { urlPattern: '*microsoft_signalr.js*', requestStage: 'Request' },
    { urlPattern: '*/@microsoft/signalr/*', requestStage: 'Request' },
  ] });
  await go('/admin/auth', 'document.querySelector(".auth-form")');
  await input('#auth-identifier', 'fixture'); await input('#auth-password', 'Fixture-Only-2026!');
  await click('Đăng nhập', 'form button');
  await until('document.querySelectorAll(".cafe-station").length === 4');
  await open('API-PC-01');
  await until(`Boolean(document.querySelector('dialog select option[value="7"]'))`);
  await select('dialog select:has(option[value="7"])', 7);
  await click('Mở phiên', 'dialog button');
  await until('!document.querySelector("dialog")');
  assert.equal(session.computerName, 'API-PC-01');
  await open('API-PC-01'); await click('Đóng phiên và xuất hóa đơn', 'dialog button');
  await until('!document.querySelector("dialog")');
  assert.equal(session, null);
  const close = requests.find(row => row.path.endsWith('/close'));
  assert.equal(close.path, '/api/admin/sessions/240/close');
  assert.deepEqual(close.body, {});
  assert.ok(Object.keys(close.headers).some(name => name.toLowerCase() === 'idempotency-key'));

  await open(legacyComputer.computerName);
  await until('document.querySelector("dialog [role=alert]")?.textContent.includes("thời điểm của phiên không nhất quán")');
  assert.equal(await evaluate('Array.from(document.querySelectorAll("dialog button")).some(button => button.textContent.includes("Đóng phiên và xuất hóa đơn"))'), false);
  await evaluate('document.querySelector(\'dialog button[aria-label="Đóng"]\').click()');
  await until('!document.querySelector("dialog") && !new URLSearchParams(location.search).has("computer")');
  await open('API-PC-02');
  await until('document.querySelector("dialog [role=alert]")?.textContent.includes("cần đối soát")');
  assert.equal(await evaluate('Array.from(document.querySelectorAll("dialog button")).some(button => button.textContent.includes("Đóng phiên và xuất hóa đơn"))'), false);

  await evaluate('localStorage.clear()');
  await go('/client/auth', 'document.querySelector(".auth-form")');
  await input('#auth-identifier', profile.phone); await input('#auth-password', 'Fixture-Only-2026!');
  await click('Đăng nhập', 'form button');
  await until('document.body.textContent.includes("Chưa có phiên chơi") && Boolean(document.querySelector(".home-balance"))');
  assert.ok(await evaluate('document.querySelector(".home-balance").textContent.includes("77.777")'));
  session = makeSession(fixture.computers[0]);
  await emit('SessionChanged');
  await until('document.getElementById("session-title")?.textContent.includes("API-PC-01")');
  assert.ok(await evaluate('document.querySelector(".home-session-footer").textContent.includes("321")'));
  balance = 44444;
  await evaluate('globalThis.__realtimeConnections.at(-1).reconnecting(new Error("offline"))');
  await evaluate('globalThis.__realtimeConnections.at(-1).reconnected("restored"); globalThis.__realtimeConnections.at(-1).emit("RealtimeReady")');
  await until('document.querySelector(".home-balance")?.textContent.includes("44.444")');
  homeStatus = 500; await emit('SessionChanged');
  await until('Boolean(document.querySelector(".home-dashboard-grid > section.home-alert"))');
  assert.equal(await evaluate('Boolean(document.querySelector(".home-session-footer"))'), false, 'API error cannot leave demo or stale fees visible');
  homeStatus = 200; session = null;
  await click('Thử lại phiên');
  await until('document.body.textContent.includes("Chưa có phiên chơi")');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 320, height: 900, deviceScaleFactor: 1, mobile: false });
  await screenshot('scrum24-home-320');
  assert.ok(await evaluate('document.documentElement.scrollWidth <= innerWidth + 1'));
  await sleep(100);
  assert.deepEqual(failures.filter(error => !canceled.has(error.networkId)), []);
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, source: 'HTTP/SDK fixtures', checks: [
    'legacy session needs reconciliation while a valid session can still be opened and closed',
    'Home API empty/data/error/retry; authoritative fees and balance; event/reconnect refetch; 320px',
  ] }));
} finally {
  await evaluate('localStorage.removeItem("cybermanagement.auth.v1")');
  socket.removeEventListener('message', listener); await cdp('Fetch.disable'); socket.close();
}
