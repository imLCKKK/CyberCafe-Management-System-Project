import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { cdp, evaluate, until, socket, errors, go, input, click, press, sleep } from './cdp.mjs';
import { computerFixture } from './computer-fixture.mjs';

// Browser UI checks use controlled SDK/HTTP fixtures. Actual hub authorization is checked in API.Checks.
const sdkFixture = await readFile(new URL('./realtime-signalr-fixture.mjs', import.meta.url), 'utf8');
const fixture = computerFixture();
const requests = [];
const interceptionErrors = [];
const canceledRequests = new Set();
const heldReads = [];
let holdReads = false;
let loginToken = 'browser-test-token';
async function handleRequest({ requestId, request }) {
  if (request.url.includes('microsoft_signalr.js') || request.url.includes('/@microsoft/signalr/')) {
    await cdp('Fetch.fulfillRequest', { requestId, responseCode: 200,
      responseHeaders: [{ name: 'Content-Type', value: 'application/javascript' }],
      body: Buffer.from(sdkFixture).toString('base64') });
    return;
  }
  const path = new URL(request.url).pathname;
  requests.push(path);
  const body = structuredClone(path === '/api/auth/login'
    ? { accessToken: loginToken, expiresAtUtc: new Date(Date.now() + 3600000).toISOString() }
    : path === '/api/auth/logout' ? null : path === '/api/auth/me'
    ? { employeeId: '7', username: 'fixture-staff', role: 'Admin', permissions: fixture.permissions }
    : path.endsWith('/computer-types') ? fixture.types
      : path.endsWith('/computer-rooms') ? fixture.rooms : fixture.computers);
  if (path === '/api/admin/computers' && holdReads)
    await new Promise((resolve) => heldReads.push(resolve));
  await cdp('Fetch.fulfillRequest', { requestId, responseCode: 200,
    responseHeaders: [{ name: 'Content-Type', value: 'application/json' }],
    body: Buffer.from(JSON.stringify(body)).toString('base64') });
}
const onMessage = ({ data }) => {
  const message = JSON.parse(data);
  if (message.method === 'Network.loadingFailed' && message.params.canceled) canceledRequests.add(message.params.requestId);
  if (message.method === 'Fetch.requestPaused') handleRequest(message.params).catch((error) =>
    interceptionErrors.push({message:error.message, networkId:message.params.networkId}));
};
const reads = () => requests.filter((path) => path === '/api/admin/computers').length;
async function waitForNode(predicate) {
  for (let attempt = 0; attempt < 150; attempt++) { if (predicate()) return; await sleep(20); }
  throw new Error('Timed out waiting for the queued computer read.');
}
const idle = () => until('document.querySelector(".cafe-computers")?.getAttribute("aria-busy") === "false"');
const emit = () => evaluate('globalThis.__realtimeConnections.at(-1).emit("ComputerChanged", {eventId:"fixture-event",type:"ComputerChanged",entityId:101})');
async function login() {
  await input('#auth-identifier', 'fixture-staff');
  await input('#auth-password', 'Fixture-Only-2026!');
  await click('Đăng nhập', 'form.auth-form button[type="submit"]');
}

try {
  await cdp('Runtime.enable'); await cdp('Page.enable'); await cdp('Network.enable');
  await cdp('Network.setCacheDisabled', { cacheDisabled: true });
  socket.addEventListener('message', onMessage);
  await cdp('Fetch.enable', { patterns: [
    { urlPattern: '*/api/*', requestStage: 'Request' },
    { urlPattern: '*microsoft_signalr.js*', requestStage: 'Request' },
    { urlPattern: '*/@microsoft/signalr/*', requestStage: 'Request' },
  ] });
  await go('/admin/auth', 'document.querySelector(".auth-form")');
  await evaluate("sessionStorage.setItem('nexora.staff.accessToken', 'obsolete-token')");
  await login();
  await until('document.querySelectorAll(".cafe-station").length === 4 && document.querySelector("[data-realtime-status]")?.dataset.realtimeStatus === "connected"');
  await idle();
  assert.equal(await evaluate('globalThis.__realtimeConnections.length'), 1);
  assert.equal(await evaluate('globalThis.__realtimeConnections[0].starts'), 1);
  assert.ok(reads() >= 2 && reads() <= 3, 'Initial group readiness should refetch without a connection loop');
  await evaluate('globalThis.__pageMarker = "no-reload"');
  await input('[aria-label="Tìm kiếm tên máy"]', 'API-PC-01');
  await until('document.querySelectorAll(".cafe-station").length === 1');
  await evaluate('document.querySelector(".cafe-station").click()');
  await until('Boolean(document.querySelector("dialog[open]"))');
  fixture.computers[0].status = 'Maintenance';
  await emit();
  await until('document.querySelector("dialog .cafe-badge")?.classList.contains("maintenance")');
  await idle();
  assert.equal(await evaluate('globalThis.__pageMarker'), 'no-reload');
  assert.equal(await evaluate(`document.querySelector('[aria-label="Tìm kiếm tên máy"]').value`), 'API-PC-01');
  await press('Escape'); await until('!document.querySelector("dialog")');

  const beforeBurst = reads();
  holdReads = true;
  await emit();
  await waitForNode(() => heldReads.length === 1);
  fixture.computers[0].status = 'Available';
  await evaluate('for(let index=0;index<20;index++) globalThis.__realtimeConnections[0].emit("ComputerChanged", {entityId:101})');
  assert.equal(reads(), beforeBurst + 1, 'Only one GET may be in flight');
  heldReads.shift()();
  await waitForNode(() => heldReads.length === 1 && reads() === beforeBurst + 2);
  holdReads = false; heldReads.shift()();
  await until('document.querySelector(".cafe-station")?.classList.contains("available")'); await idle();
  assert.equal(reads(), beforeBurst + 2, 'A burst should leave one trailing authoritative read');
  assert.equal(await evaluate('globalThis.__realtimeConnections.length'), 1);

  await evaluate('globalThis.__realtimeConnections[0].reconnecting(new Error("offline"))');
  await until('document.querySelector("[data-realtime-status]")?.dataset.realtimeStatus === "reconnecting"');
  fixture.computers[0].status = 'Maintenance';
  await evaluate('globalThis.__realtimeConnections[0].reconnected("recovered"); globalThis.__realtimeConnections[0].emit("RealtimeReady")');
  await until('document.querySelector(".cafe-station")?.classList.contains("maintenance")'); await idle();
  assert.equal(await evaluate('globalThis.__pageMarker'), 'no-reload');

  await evaluate(`(async () => { globalThis.__extraRelease = (await import('/src/shared/realtime/sharedRealtime.js')).subscribeRealtime('browser-test-token', {events:['ComputerChanged'],onEvent:()=>{globalThis.__extraEvents=(globalThis.__extraEvents||0)+1}}); })()`);
  assert.equal(await evaluate('globalThis.__realtimeConnections.length'), 1);
  await emit(); await idle();
  assert.equal(await evaluate('globalThis.__extraEvents'), 1);
  await evaluate('globalThis.__extraRelease()');
  assert.equal(await evaluate('globalThis.__realtimeConnections[0].stops'), 0);

  await evaluate(`(() => {
    globalThis.__oldCallback = globalThis.__realtimeConnections[0].handler('ComputerChanged');
    const key = 'cybermanagement.auth.v1', oldValue = localStorage.getItem(key);
    const newValue = JSON.stringify({accessToken:'browser-new-token',accountType:'employee'});
    localStorage.setItem(key, newValue);
    dispatchEvent(new StorageEvent('storage', {key, oldValue, newValue, storageArea:localStorage}));
  })()`);
  await until('globalThis.__realtimeConnections.length === 2 && document.querySelector("[data-realtime-status]")?.dataset.realtimeStatus === "connected"');
  await idle();
  assert.equal(await evaluate('globalThis.__realtimeConnections[0].stops'), 1);
  assert.equal(await evaluate('globalThis.__realtimeConnections[1].options.accessTokenFactory()'), 'browser-new-token');
  const afterNewToken = reads();
  await evaluate('globalThis.__oldCallback({entityId:101}); globalThis.__realtimeConnections[0].closed({statusCode:401})');
  assert.equal(reads(), afterNewToken);
  assert.equal(await evaluate(`JSON.parse(localStorage.getItem('cybermanagement.auth.v1')).accessToken`), 'browser-new-token');

  await evaluate('globalThis.__realtimeConnections[1].policy.nextRetryDelayInMilliseconds({previousRetryCount:0,retryReason:{statusCode:401}})');
  await until('document.body.innerText.includes("Phiên đăng nhập đã hết hạn")');
  assert.equal(await evaluate(`localStorage.getItem('cybermanagement.auth.v1')`), null);
  assert.equal(await evaluate('globalThis.__realtimeConnections[1].stops'), 1);
  assert.equal(await evaluate('document.querySelectorAll(".cafe-station").length'), 0);

  loginToken = 'browser-third-token';
  await login();
  await until('globalThis.__realtimeConnections.length === 3 && document.querySelector("[data-realtime-status]")?.dataset.realtimeStatus === "connected"'); await idle();
  await evaluate('globalThis.__realtimeConnections[2].policy.nextRetryDelayInMilliseconds({previousRetryCount:0,retryReason:{statusCode:403}})');
  await until('document.querySelector("[data-realtime-status]")?.dataset.realtimeStatus === "forbidden"'); await idle();
  assert.equal(await evaluate('globalThis.__realtimeConnections.length'), 3);
  assert.equal(await evaluate(`JSON.parse(localStorage.getItem('cybermanagement.auth.v1')).accessToken`), 'browser-third-token');
  await evaluate('globalThis.__realtimeConnections[2].emit("ComputerChanged", {entityId:101})');
  assert.equal(await evaluate('globalThis.__realtimeConnections.length'), 3);
  await evaluate(`document.querySelector('[aria-label="Đăng xuất"]').click()`);
  await until('location.pathname === "/admin/auth" && Boolean(document.querySelector(".auth-form"))');
  assert.equal(await evaluate('globalThis.__realtimeConnections[2].stops'), 1);
  assert.equal(await evaluate(`localStorage.getItem('cybermanagement.auth.v1')`), null);
  assert.deepEqual(interceptionErrors.filter(error =>
    !(error.message.includes('Invalid InterceptionId') && canceledRequests.has(error.networkId))), []);
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, source: 'SDK and HTTP fixtures; not LAN', checks: [
    'RealtimeReady and event refetch without F5 or dialog loss', 'single connection per token/shared subscribers',
    '20-event burst coalesces into one trailing read', 'reconnect/refetch keeps filters',
    'UI login uses shared auth session and ignores legacy token', 'account change/logout stops old client/late callbacks ignored', '401 clears session/403 stops retries',
  ] }));
} finally {
  holdReads = false; heldReads.splice(0).forEach((resolve) => resolve());
  await evaluate("localStorage.removeItem('cybermanagement.auth.v1'); sessionStorage.removeItem('nexora.staff.accessToken')").catch(() => {});
  await cdp('Fetch.disable').catch(() => {});
  await cdp('Network.setCacheDisabled', { cacheDisabled: false }).catch(() => {});
  socket.removeEventListener('message', onMessage);
  socket.close();
}
