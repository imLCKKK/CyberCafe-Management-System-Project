import assert from 'node:assert/strict';
import { cdp, evaluate, until, errors, socket, go, click, input, press, sleep, screenshot } from './cdp.mjs';
import { computerFixture } from './computer-fixture.mjs';

// HTTP fixtures verify UI behavior only. Actual SQL persistence/concurrency is covered by API.Checks.
const fixture = computerFixture();
const tokenKey = 'nexora.staff.accessToken';
const requests = [];
const interceptionErrors = [];
const canceledRequests = new Set();
const waitingLists = [];
const waitingPatches = [];
let holdList = false;
let holdPatch = false;
let listStatus = 200;
let identityStatus = 200;
let patchMode = 'success';
let malformed = false;

async function handleRequest({ requestId, request }) {
  const path = new URL(request.url).pathname;
  if (path.startsWith('/hubs/realtime/')) {
    await cdp('Fetch.fulfillRequest', { requestId, responseCode: 503 });
    return;
  }
  requests.push({ path, method: request.method, body: request.postData });
  let status = 200;
  let body;
  const authorization = Object.entries(request.headers).find(([key]) => key.toLowerCase() === 'authorization')?.[1];
  if (authorization !== 'Bearer browser-test-token') status = 401;
  else if (path === '/api/auth/me') {
    status = identityStatus;
    body = { employeeId: '7', username: 'fixture-staff', role: 'Admin', permissions: fixture.permissions };
  } else if (request.method === 'PATCH') {
    if (holdPatch) await new Promise((resolve) => waitingPatches.push(resolve));
    if (!fixture.permissions.includes('computer.status.update')) status = 403;
    else if (patchMode === 'failure') status = 500;
    else {
      const record = fixture.computers.find((row) => row.computerId === Number(path.split('/')[4]));
      const change = JSON.parse(request.postData);
      if (patchMode === 'conflict') {
        record.status = 'Occupied'; record.hasOpenSession = true;
        status = 409; body = { detail: 'Trạng thái máy đã thay đổi. Vui lòng tải lại dữ liệu.' };
      } else if (record.status !== change.expectedStatus) status = 409;
      else { record.status = change.status; body = record; }
    }
  } else {
    if (path === '/api/admin/computers' && holdList) await new Promise((resolve) => waitingLists.push(resolve));
    status = listStatus;
    body = path.endsWith('/computer-types') ? fixture.types
      : path.endsWith('/computer-rooms') ? fixture.rooms : fixture.computers;
    if (malformed && path.endsWith('/computer-types')) body = [{ ...fixture.types[0], pricePerHour: 'invalid' }];
  }
  await cdp('Fetch.fulfillRequest', {
    requestId, responseCode: status,
    responseHeaders: [{ name: 'Content-Type', value: 'application/json' }],
    body: Buffer.from(JSON.stringify(status === 401 || status === 403 ? null : body ?? { title: 'Fixture failure' })).toString('base64'),
  });
}
const onMessage = ({ data }) => {
  const message = JSON.parse(data);
  if (message.method === 'Network.loadingFailed' && message.params.canceled)
    canceledRequests.add(message.params.requestId);
  if (message.method === 'Fetch.requestPaused') handleRequest(message.params).catch((error) =>
    interceptionErrors.push({ message: error.message, networkId: message.params.networkId, url: message.params.request.url }));
};
async function waitForNode(predicate) {
  for (let attempt = 0; attempt < 150; attempt++) { if (predicate()) return; await sleep(20); }
  throw new Error('Timed out waiting for intercepted API request.');
}
const ready = () => until('document.querySelectorAll(".cafe-station").length === 4');
const open = async (name) => {
  await evaluate(`Array.from(document.querySelectorAll('.cafe-station')).find(row => row.querySelector('strong').textContent === ${JSON.stringify(name)}).click()`);
  try {
    await until(`document.querySelector('dialog[open] .cafe-dialog-title h2')?.textContent === ${JSON.stringify(name)}`);
  } catch (error) {
    const state = await evaluate('({ search: location.search, dialogs: Array.from(document.querySelectorAll("dialog")).map(row => ({ open: row.open, title: row.querySelector("h2")?.textContent })) })');
    throw new Error(`${error.message}; dialog state: ${JSON.stringify(state)}`);
  }
};
const close = async () => {
  await press('Escape');
  await until('!document.querySelector("dialog")');
};

try {
  await cdp('Runtime.enable'); await cdp('Page.enable'); await cdp('Network.enable');
  socket.addEventListener('message', onMessage);
  await cdp('Fetch.enable', { patterns: [
    { urlPattern: '*/api/*', requestStage: 'Request' },
    { urlPattern: '*/hubs/realtime/*', requestStage: 'Request' },
  ] });
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 900, deviceScaleFactor: 1, mobile: false });
  await go('/client/home');
  await evaluate(`sessionStorage.removeItem(${JSON.stringify(tokenKey)}); localStorage.setItem('net-cafe.admin.computers.v1', JSON.stringify([{ComputerId:1,ComputerName:'LOCAL-DEMO-SHOULD-NOT-APPEAR',RoomId:1,ComputerTypeId:1,Status:'Available'}]));`);
  await go('/admin/computers');
  await until('document.body.innerText.includes("Vui lòng đăng nhập nhân viên")');
  assert.equal(requests.length, 0, 'An anonymous page should request login before loading protected data');
  assert.equal(await evaluate('document.querySelectorAll(".cafe-station").length'), 0);

  await evaluate(`sessionStorage.setItem(${JSON.stringify(tokenKey)}, 'browser-test-token')`);
  holdList = true;
  await go('/admin/computers');
  await waitForNode(() => waitingLists.length === 1);
  assert.ok(await evaluate('document.body.innerText.includes("Đang tải danh sách máy")'));
  assert.equal(await evaluate('document.querySelectorAll(".cafe-stat").length'), 0);
  holdList = false; waitingLists.splice(0).forEach((resolve) => resolve());
  await ready();
  assert.equal(await evaluate('document.querySelector(".cafe-computers").innerText.includes("LOCAL-DEMO")'), false);
  assert.equal(await evaluate('Boolean(document.querySelector(".has-food, .cafe-session"))'), false);
  assert.deepEqual(await evaluate(`Array.from(document.querySelectorAll('select[aria-label="Lọc phòng máy"] option')).map(row => row.value)`), ['all', '11', '12']);
  assert.deepEqual(await evaluate(`Array.from(document.querySelectorAll('select[aria-label="Lọc loại máy"] option')).map(row => row.value)`), ['all', '21', '22']);
  await input('[aria-label="Lọc phòng máy"]', '12');
  await until('document.querySelectorAll(".cafe-station").length === 2');
  await input('[aria-label="Lọc loại máy"]', '21');
  await until('document.body.innerText.includes("Không tìm thấy máy tính")');
  await click('Đặt lại bộ lọc'); await ready();
  await input('[aria-label="Tìm kiếm tên máy"]', 'api-pc-01');
  await until('document.querySelectorAll(".cafe-station").length === 1');
  await input('[aria-label="Tìm kiếm tên máy"]', ''); await ready();
  await evaluate(`document.querySelector('button[aria-label="Dạng bảng"]').click()`);
  await until('document.querySelectorAll("tbody tr").length === 4');
  assert.ok(await evaluate('document.querySelector("tbody").innerText.includes("12.345")'));
  await evaluate(`document.querySelector('button[aria-label="Dạng lưới"]').click()`); await ready();
  for (const name of ['API-PC-02', 'API-PC-03', 'API-PC-04']) {
    await open(name);
    assert.equal(await evaluate('document.querySelector("dialog .cafe-dialog-actions button").disabled'), true);
    await close();
  }
  await open('API-PC-01');
  assert.ok(await evaluate('document.querySelector("dialog").innerText.includes("Chưa có thông tin CPU")'));
  holdPatch = true;
  await click('Chuyển bảo trì', 'dialog button');
  await waitForNode(() => waitingPatches.length === 1);
  assert.equal(await evaluate('document.querySelector("dialog .cafe-dialog-actions button").disabled'), true);
  assert.equal(await evaluate('Boolean(document.querySelector(".cafe-notice"))'), false);
  await evaluate('document.querySelector("dialog .cafe-dialog-actions button").click()');
  assert.equal(requests.filter((request) => request.method === 'PATCH').length, 1);
  holdPatch = false; waitingPatches.splice(0).forEach((resolve) => resolve());
  await until('!document.querySelector("dialog[open]") && document.body.innerText.includes("Đã cập nhật trạng thái API-PC-01")');
  assert.deepEqual(JSON.parse(requests.find((request) => request.method === 'PATCH').body), { status: 'Maintenance', expectedStatus: 'Available' });
  await cdp('Page.reload'); await ready();
  await open('API-PC-01');
  assert.ok(await evaluate('document.querySelector("dialog .cafe-badge").classList.contains("maintenance")'));
  await close();

  fixture.permissions = ['computer.view'];
  await cdp('Page.reload'); await ready(); await open('API-PC-01');
  assert.equal(await evaluate('Boolean(document.querySelector("dialog .cafe-dialog-actions button"))'), false);
  await close();
  fixture.permissions.push('computer.status.update');
  await cdp('Page.reload'); await ready(); await open('API-PC-01');
  patchMode = 'failure';
  await click('Đưa máy vào sử dụng', 'dialog button');
  await until('document.querySelector("dialog [role=alert]")?.textContent.includes("Máy chủ đang gặp lỗi")');
  assert.equal(await evaluate('Boolean(document.querySelector(".cafe-notice"))'), false);
  assert.equal(fixture.computers[0].status, 'Maintenance');
  patchMode = 'conflict';
  await click('Đưa máy vào sử dụng', 'dialog button');
  await until('document.querySelector("dialog .cafe-badge")?.classList.contains("occupied")');
  assert.equal(await evaluate('document.querySelector("dialog .cafe-dialog-actions button").disabled'), true);
  assert.equal(await evaluate('Boolean(document.querySelector(".cafe-notice"))'), false);
  await close();

  listStatus = 500;
  await cdp('Page.reload');
  await until('document.body.innerText.includes("Máy chủ đang gặp lỗi")');
  assert.equal(await evaluate('document.querySelectorAll(".cafe-station, .cafe-stat").length'), 0);
  listStatus = 200;
  await click('Thử lại'); await ready();
  malformed = true;
  await click('Tải lại');
  await until('document.body.innerText.includes("Dữ liệu danh sách máy, phòng hoặc loại máy không hợp lệ")');
  assert.equal(await evaluate('document.querySelectorAll(".cafe-station").length'), 0);
  malformed = false;
  fixture.computers = [];
  await click('Thử lại');
  await until('document.body.innerText.includes("Chưa có máy tính")');
  assert.equal(await evaluate('document.querySelectorAll(".cafe-station").length'), 0);

  identityStatus = 403;
  await click('Tải lại');
  await until('document.body.innerText.includes("Bạn không có quyền thực hiện thao tác này")');
  assert.equal(await evaluate('document.querySelectorAll(".cafe-stat").length'), 0);
  identityStatus = 401;
  await click('Tải lại');
  await until('document.body.innerText.includes("Phiên đăng nhập đã hết hạn")');
  assert.equal(await evaluate(`sessionStorage.getItem(${JSON.stringify(tokenKey)})`), null);
  assert.equal(await evaluate('document.querySelector(".cafe-computers a").getAttribute("href")'), '/admin/auth');

  for (const width of [390, 320]) {
    await cdp('Emulation.setDeviceMetricsOverride', { width, height: 900, deviceScaleFactor: 1, mobile: false });
    identityStatus = 200;
    fixture.computers = computerFixture().computers;
    await evaluate(`(async () => { (await import('/src/shared/staffSession.js')).setStaffAccessToken('browser-test-token'); })()`);
    await ready();
    assert.ok(await evaluate('document.documentElement.scrollWidth <= innerWidth'), `Overflow at ${width}`);
    await open('API-PC-01');
    assert.ok(await evaluate('document.documentElement.scrollWidth <= innerWidth'), `Modal overflow at ${width}`);
    await screenshot(`computers-api-${width}.png`);
    await close();
  }
  assert.deepEqual(interceptionErrors.filter((error) =>
    !(error.message.includes('Invalid InterceptionId') && canceledRequests.has(error.networkId))), []);
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, source: 'HTTP fixtures (not SQL)', checks: [
    'login required/loading/no demo fallback', 'API rooms/types/search/list/nullable specs',
    'open-session/maintenance/unknown states blocked', 'pending/double-click/PATCH payload/reload',
    'read-only permissions/500/409 refetch/401/403', 'malformed data/empty/retry', '390/320px layouts',
  ] }));
} finally {
  waitingLists.splice(0).forEach((resolve) => resolve());
  waitingPatches.splice(0).forEach((resolve) => resolve());
  await evaluate(`sessionStorage.removeItem(${JSON.stringify(tokenKey)}); localStorage.removeItem('net-cafe.admin.computers.v1')`).catch(() => {});
  await cdp('Fetch.disable').catch(() => {});
  socket.removeEventListener('message', onMessage);
  socket.close();
}
