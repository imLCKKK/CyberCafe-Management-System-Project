import assert from 'node:assert/strict';
import { cdp, evaluate, until, errors, socket, go, click, input, press, screenshot } from './cdp.mjs';
import { computerFixture } from './computer-fixture.mjs';

// Fixtures exercise UI/API contracts; they do not prove SQL transactions or LAN synchronization.
const fixture = computerFixture();
fixture.permissions.push('computer.manage', 'maintenance.view', 'maintenance.manage');
const histories = new Map();
const failures = [];
const canceledRequests = new Set();
let nextId = 200;
let nextMaintenanceId = 1;

async function intercept({ requestId, request }) {
  const path = new URL(request.url).pathname;
  const method = request.method;
  let status = 200;
  let body;
  const data = request.postData ? JSON.parse(request.postData) : {};
  const conflict = (detail) => { status = 409; body = { detail }; };
  if (path.startsWith('/hubs/')) status = 503;
  else if (path === '/api/auth/login') body = { accessToken: 'management-test-token', expiresAtUtc: new Date(Date.now() + 3600000).toISOString() };
  else if (path === '/api/auth/me') body = { employeeId: 7, username: 'management', role: 'Admin', permissions: fixture.permissions };
  else if (path === '/api/auth/logout') status = 204;
  else if (path === '/api/admin/computer-rooms') body = fixture.rooms;
  else if (path.startsWith('/api/admin/computer-types')) {
    const id = Number(path.split('/')[4]);
    const row = fixture.types.find((type) => type.computerTypeId === id);
    if (method === 'GET') body = fixture.types;
    else if (method === 'DELETE') {
      if (fixture.computers.some((computer) => computer.computerTypeId === id)) conflict('Không thể xóa loại máy đang được tham chiếu.');
      else { fixture.types = fixture.types.filter((type) => type.computerTypeId !== id); status = 204; }
    } else {
      body = { ...data, computerTypeId: id || ++nextId };
      if (row) Object.assign(row, body);
      else fixture.types.push(body);
      if (method === 'POST') status = 201;
    }
  } else if (path.includes('/maintenances')) {
    const id = Number(path.split('/')[4]);
    const maintenanceId = Number(path.split('/')[6]);
    const computer = fixture.computers.find((row) => row.computerId === id);
    const records = histories.get(id) || [];
    if (method === 'GET') body = records;
    else if (path.endsWith('/complete')) {
      body = records.find((row) => row.maintenanceId === maintenanceId);
      body.status = 'Completed'; body.endTime ||= new Date().toISOString();
      computer.status = 'Available'; computer.hasOpenMaintenance = false;
    } else if (method === 'PUT') {
      body = records.find((row) => row.maintenanceId === maintenanceId);
      body.description = data.description;
    } else if (computer.hasOpenMaintenance) conflict('Máy đang có hồ sơ bảo trì.');
    else {
      body = { maintenanceId: nextMaintenanceId++, computerId: id, employeeId: 7, description: data.description,
        startTime: new Date().toISOString(), endTime: null, status: 'InProgress' };
      records.push(body); histories.set(id, records);
      computer.status = 'Maintenance'; computer.hasOpenMaintenance = true;
      status = 201;
    }
  } else if (path.startsWith('/api/admin/computers')) {
    const id = Number(path.split('/')[4]);
    const row = fixture.computers.find((computer) => computer.computerId === id);
    if (method === 'GET') body = fixture.computers;
    else if (method === 'DELETE') {
      if (histories.get(id)?.length) conflict('Không thể xóa máy đã có lịch sử phiên hoặc bảo trì.');
      else { fixture.computers = fixture.computers.filter((computer) => computer.computerId !== id); status = 204; }
    } else if (method === 'PATCH') { Object.assign(row, { status: data.status }); body = row; }
    else {
      body = { ...data, computerId: id || ++nextId, status: row?.status || 'Available', hasOpenSession: false, hasOpenMaintenance: false };
      if (row) Object.assign(row, body);
      else fixture.computers.push(body);
      if (method === 'POST') status = 201;
    }
  } else { status = 404; body = { detail: `Unexpected fixture path ${path}` }; }
  await cdp('Fetch.fulfillRequest', { requestId, responseCode: status,
    responseHeaders: [{ name: 'Content-Type', value: 'application/json' }],
    body: status === 204 ? '' : Buffer.from(JSON.stringify(body ?? {})).toString('base64') });
}
const onMessage = ({ data }) => {
  const message = JSON.parse(data);
  if (message.method === 'Network.loadingFailed' && message.params.canceled) canceledRequests.add(message.params.requestId);
  if (message.method === 'Fetch.requestPaused') void intercept(message.params).catch((error) =>
    failures.push({ message: error.message, networkId: message.params.networkId }));
};
const field = (label, value) => evaluate(`(() => {
  const label = [...document.querySelectorAll('dialog label')].find(row => row.firstChild.textContent.trim() === ${JSON.stringify(label)});
  const element = label?.querySelector('input, select, textarea');
  if (!element) throw Error('Missing form field ' + ${JSON.stringify(label)});
  const prototype = element.tagName === 'SELECT' ? HTMLSelectElement.prototype : element.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
  Object.getOwnPropertyDescriptor(prototype, 'value').set.call(element, ${JSON.stringify(String(value))});
  element.dispatchEvent(new Event(element.tagName === 'SELECT' ? 'change' : 'input', { bubbles: true }));
})()`);
const openComputer = async (name) => {
  await until(`Array.from(document.querySelectorAll('.cafe-station strong')).some(row => row.textContent === ${JSON.stringify(name)})`);
  await evaluate(`Array.from(document.querySelectorAll('.cafe-station')).find(row => row.querySelector('strong').textContent === ${JSON.stringify(name)}).click()`);
  await until(`document.querySelector('dialog[open] h2')?.textContent === ${JSON.stringify(name)}`);
};
const close = async () => { await press('Escape'); await until('!document.querySelector("dialog[open]")'); };

try {
  await cdp('Runtime.enable'); await cdp('Page.enable'); await cdp('Network.enable');
  socket.addEventListener('message', onMessage);
  await cdp('Fetch.enable', { patterns: [{ urlPattern: '*/api/*' }, { urlPattern: '*/hubs/realtime/*' }] });
  await go('/admin/auth', 'document.body');
  await evaluate("localStorage.removeItem('cybermanagement.auth.v1')");
  await go('/admin/computers', 'document.querySelector(".auth-form")');
  await input('#auth-identifier', 'management'); await input('#auth-password', 'Fixture-only-2026!');
  await click('Đăng nhập', 'form.auth-form button[type="submit"]');
  await until('document.querySelectorAll(".cafe-station").length === 4');

  await click('Quản lý loại máy'); await click('Thêm loại máy');
  await field('Tên loại máy', 'CRUD Pro'); await field('Giá mỗi giờ (đồng)', 30000); await field('CPU', 'Test CPU');
  await click('Lưu loại máy'); await until('document.querySelector("dialog").innerText.includes("Sửa CRUD Pro")');
  const type = fixture.types.find((row) => row.typeName === 'CRUD Pro');
  assert.equal(type.pricePerHour, 30000); await close();
  await click('Thêm máy tính'); await field('Tên máy', 'CRUD PC'); await field('Loại máy', type.computerTypeId);
  await click('Lưu máy'); await until('!document.querySelector("dialog[open]") && document.querySelectorAll(".cafe-station").length === 5');
  await cdp('Page.reload'); await openComputer('CRUD PC');
  await click('Sửa máy'); await field('Tên máy', 'CRUD PC edited'); await click('Lưu máy');
  await until('document.querySelector("dialog h2")?.textContent === "CRUD PC edited"'); await close();

  await click('Quản lý loại máy'); await click('Sửa CRUD Pro'); await click('Xóa loại máy'); await click('Xác nhận xóa loại máy');
  await until('document.querySelector("dialog").innerText.includes("Không thể xóa loại máy đang được tham chiếu")');
  assert.equal(fixture.types.includes(type), true); await close();
  await openComputer('CRUD PC edited'); await until('document.querySelector(".maintenance-panel")?.innerText.includes("Chưa có hồ sơ bảo trì")');
  await field('Mô tả bảo trì', 'Clean fans'); await click('Bắt đầu bảo trì');
  await until('document.querySelector(".maintenance-list")?.innerText.includes("Clean fans")');
  assert.equal(fixture.computers.find((row) => row.computerName === 'CRUD PC edited').status, 'Maintenance');
  await click('Sửa mô tả'); await field('Sửa mô tả bảo trì', 'Cleaned fans'); await click('Lưu mô tả');
  await until('document.querySelector(".maintenance-list")?.innerText.includes("Cleaned fans")');
  await click('Hoàn tất bảo trì'); await until('document.querySelector(".maintenance-list")?.innerText.includes("Đã kết thúc")');
  await click('Sửa máy'); await click('Xóa máy'); await click('Xác nhận xóa máy');
  await until('document.querySelector("dialog").innerText.includes("Không thể xóa máy đã có lịch sử")');
  await press('Escape'); await until('document.querySelector("dialog h2")?.textContent === "CRUD PC edited"'); await close();

  await click('Thêm máy tính'); await field('Tên máy', 'Disposable PC'); await click('Lưu máy');
  await until('!document.querySelector("dialog[open]")'); await openComputer('Disposable PC');
  await click('Sửa máy'); await click('Xóa máy'); await click('Xác nhận xóa máy');
  await until('!document.querySelector("dialog[open]") && document.querySelectorAll(".cafe-station").length === 5');
  assert.equal(fixture.computers.some((row) => row.computerName === 'Disposable PC'), false);

  await click('Quản lý loại máy'); await click('Thêm loại máy');
  await field('Tên loại máy', 'Disposable type'); await field('Giá mỗi giờ (đồng)', 10000); await click('Lưu loại máy');
  await until('document.querySelector("dialog").innerText.includes("Sửa Disposable type")');
  await click('Sửa Disposable type'); await click('Xóa loại máy'); await click('Xác nhận xóa loại máy');
  await until('document.querySelector(".computer-type-list") && !document.querySelector("dialog").innerText.includes("Disposable type")');
  await close();

  await cdp('Emulation.setDeviceMetricsOverride', { width: 320, height: 800, deviceScaleFactor: 1, mobile: true });
  await openComputer('CRUD PC edited');
  assert.equal(await evaluate('document.querySelector("dialog").scrollWidth <= document.querySelector("dialog").clientWidth'), true);
  await screenshot('scrum24-maintenance-320.png'); await close();
  fixture.permissions = ['computer.view'];
  await cdp('Page.reload'); await until('document.querySelectorAll(".cafe-station").length === 5');
  assert.equal(await evaluate('document.body.innerText.includes("Thêm máy tính") || document.body.innerText.includes("Quản lý loại máy")'), false);
  await openComputer('CRUD PC edited');
  assert.equal(await evaluate('Boolean(document.querySelector(".maintenance-panel")) || document.querySelector("dialog").innerText.includes("Sửa máy")'), false);
  assert.deepEqual(failures.filter((error) =>
    !(error.message.includes('Invalid InterceptionId') && canceledRequests.has(error.networkId))), []);
  assert.deepEqual(errors, []);
  console.log('PASS computer/type CRUD, maintenance lifecycle/history, referenced deletes, permissions and 320px UI (HTTP fixtures).');
} finally {
  await evaluate("localStorage.removeItem('cybermanagement.auth.v1')").catch(() => {});
  await cdp('Fetch.disable'); socket.removeEventListener('message', onMessage); socket.close();
}
