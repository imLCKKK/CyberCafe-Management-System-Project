import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import path from 'node:path';
import { createSignalRClient } from '../src/shared/realtime/sharedRealtime.js';
import { cdp, evaluate, until, socket, go, sleep, origin, outputDir, screenshot } from './cdp.mjs';

// Real browser SDK -> Vite WebSocket proxy -> Kestrel -> isolated SQL. No Fetch interception.
const api = process.env.REALTIME_LIVE_API_URL;
if (!api) throw Error('Run through tests/run-realtime-live.mjs.');
async function request(route, token, method = 'GET', body) {
  const response = await fetch(api + route, { method, headers: {
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(body ? { 'Content-Type': 'application/json' } : {}),
  }, body: body ? JSON.stringify(body) : undefined });
  const text = await response.text();
  return { status: response.status, body: text ? JSON.parse(text) : null };
}
async function login(username) {
  const result = await request('/api/auth/login', null, 'POST', { username: username === 'Waiter' ? 'waiter01' : 'admin', password: `${username}-check-password-2026!` });
  assert.equal(result.status, 200); return result.body.accessToken;
}
async function waitFor(predicate, label, timeout = 30000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { if (await predicate()) return; await sleep(40); }
  throw Error(`Timed out: ${label}`);
}
// Second independent tab/CDP socket; sessionStorage and hub connection stay separate.
async function openPeer() {
  const { targetId } = await cdp('Target.createTarget', { url: 'about:blank' });
  const targets = await fetch(`http://127.0.0.1:${process.env.FRONTEND_CDP_PORT}/json/list`).then(r => r.json());
  const ws = new WebSocket(targets.find(target => target.id === targetId).webSocketDebuggerUrl);
  await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = reject; });
  let serial = 0;
  const pending = new Map();
  ws.onmessage = ({ data }) => {
    const message = JSON.parse(data);
    const task = pending.get(message.id);
    if (!task) return;
    pending.delete(message.id); clearTimeout(task.timer);
    message.error ? task.reject(Error(message.error.message)) : task.resolve(message.result);
  };
  const send = (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++serial;
    const timer = setTimeout(() => { pending.delete(id); reject(Error(`Peer CDP timeout: ${method}`)); }, 15000);
    pending.set(id, { resolve, reject, timer }); ws.send(JSON.stringify({ id, method, params }));
  });
  const read = async expression => {
    const result = await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);
    return result.result.value;
  };
  await send('Runtime.enable'); await send('Page.enable'); await send('Network.enable');
  return { send, read, close: async () => { ws.close(); await cdp('Target.closeTarget', { targetId }); } };
}
const connected = 'document.querySelector("[data-realtime-status]")?.dataset.realtimeStatus === "connected" && document.querySelector(".cafe-computers")?.getAttribute("aria-busy") === "false"';
const statusIs = status => `document.querySelector(".cafe-station")?.classList.contains(${JSON.stringify(status.toLowerCase())})`;
const bootstrap = token => `(async () => { (await import('/src/shared/staffSession.js')).setStaffAccessToken(${JSON.stringify(token)}); })()`;
let peer, customer;
let patchAck = null;
let observerReads = 0;
const samples = [];
const onMessage = ({ data }) => {
  const message = JSON.parse(data);
  if (message.method === 'Network.responseReceived' && /\/api\/admin\/computers\/\d+\/status$/.test(message.params.response.url)
    && message.params.response.status === 200) patchAck = performance.now();
};
try {
  const admin = await login('Admin');
  const waiter = await login('Waiter');
  const customerLogin = await request('/api/auth/customer/login', null, 'POST', { phone: '0900000010', password: 'Customer-check-password-2026!' });
  assert.equal(customerLogin.status, 200);
  let customerReady = false;
  const customerMessages = [];
  customer = createSignalRClient(customerLogin.body.accessToken, api + '/hubs/realtime');
  customer.subscribe({ events: ['ComputerChanged', 'CheckBarrier'], onReady() { customerReady = true; },
    onEvent(name, message) { customerMessages.push({ name, message }); } });
  await waitFor(() => customerReady, 'customer real SDK ready');
  assert.equal((await request('/api/admin/computers', customerLogin.body.accessToken)).status, 403);
  assert.equal((await request('/hubs/realtime/negotiate?negotiateVersion=1', null, 'POST')).status, 401);

  await cdp('Runtime.enable'); await cdp('Page.enable'); await cdp('Network.enable');
  socket.addEventListener('message', onMessage);
  await go('/client/home'); await evaluate(bootstrap(admin)); await go('/admin/computers'); await until(connected);
  const rows = (await request('/api/admin/computers', admin)).body;
  assert.equal(rows.length, 1);
  const computerId = rows[0].computerId;
  peer = await openPeer();
  await peer.send('Page.navigate', { url: origin + '/client/home' });
  await waitFor(() => peer.read('Boolean(document.querySelector(".home-heading"))'), 'peer page loaded');
  await peer.read(bootstrap(waiter));
  await peer.send('Page.navigate', { url: origin + '/admin/computers' });
  await waitFor(() => peer.read(connected), 'peer real hub connected');
  await peer.read(`globalThis.__pageMarker = 'no-f5'; globalThis.__realtimeStates = [];
    new MutationObserver(() => { const state = document.querySelector('[data-realtime-status]')?.dataset.realtimeStatus;
      if (state && globalThis.__realtimeStates.at(-1) !== state) globalThis.__realtimeStates.push(state); })
    .observe(document.querySelector('.cafe-computers'), {subtree:true,attributes:true});`);
  // Keep B's detail dialog open to verify event refetch also updates the dialog.
  await peer.read('document.querySelector(".cafe-station").click()');
  await waitFor(() => peer.read('Boolean(document.querySelector("dialog[open]"))'), 'observer dialog');
  assert.equal(await peer.read('Boolean(document.querySelector(".cafe-dialog-actions"))'), false);
  assert.equal((await request(`/api/admin/computers/${computerId}/status`, waiter, 'PATCH', { status: 'Maintenance', expectedStatus: 'Available' })).status, 403);
  // Observe authoritative reads, without wrapping/replacing fetch or the SDK.
  await peer.read(`new PerformanceObserver(list => { globalThis.__computerReads = (globalThis.__computerReads || 0)
    + list.getEntries().filter(entry => new URL(entry.name).pathname === '/api/admin/computers').length; })
    .observe({type:'resource', buffered:true});`);
  observerReads = await peer.read('globalThis.__computerReads || 0');

  for (let round = 1; round <= 10; round++) {
    const target = round % 2 ? 'Maintenance' : 'Available';
    await until(connected);
    await evaluate('document.querySelector(".cafe-station").click()');
    await until('Boolean(document.querySelector("dialog .cafe-dialog-actions button:not([disabled])"))');
    patchAck = null;
    const started = performance.now();
    await evaluate('document.querySelector("dialog .cafe-dialog-actions button").click()');
    await waitFor(() => peer.read(statusIs(target)), `observer round ${round}`);
    const visibleAt = performance.now();
    await waitFor(() => patchAck !== null, 'PATCH response');
    const sample = { round, status: target, clickToVisibleMs: Math.round(visibleAt - started),
      responseToVisibleMs: Math.max(0, Math.round(visibleAt - patchAck)) };
    samples.push(sample);
    assert.ok(sample.clickToVisibleMs <= 2000, `Loopback round ${round} exceeds 2s`);
    assert.equal(await peer.read('globalThis.__pageMarker'), 'no-f5');
    assert.equal(await peer.read(`document.querySelector('dialog .cafe-badge')?.classList.contains('${target.toLowerCase()}')`), true);
    await until('!document.querySelector("dialog")');
  }
  await waitFor(async () => (await peer.read('globalThis.__computerReads || 0')) >= observerReads + 10, 'observer API refetches');
  const barrier = await request('/api/admin/__checks/customer-barrier', admin, 'POST');
  assert.equal(barrier.status, 200);
  await waitFor(() => customerMessages.some(entry => entry.name === 'CheckBarrier' && entry.message.eventId === barrier.body.eventId), 'ordered customer barrier');
  assert.equal(customerMessages.filter(entry => entry.name === 'ComputerChanged').length, 0);

  // Force B offline, save while it is disconnected, then require an API refetch on reconnect.
  await peer.send('Network.emulateNetworkConditions', { offline: true, latency: 0, downloadThroughput: -1, uploadThroughput: -1 });
  // API restart closes the real WebSocket deterministically, including existing offline sockets.
  const before = (await request('/api/admin/__checks/instance', admin)).body.instanceId;
  assert.equal((await request('/api/admin/__checks/restart', admin, 'POST')).status, 202);
  await waitFor(async () => { try { const r = await request('/api/admin/__checks/instance', admin); return r.status === 200 && r.body.instanceId !== before; } catch { return false; } }, 'new Kestrel instance');
  assert.equal((await request('/api/admin/computers', admin)).body[0].status, 'Available', 'SQL state survives host restart');
  assert.equal((await request(`/api/admin/computers/${computerId}/status`, admin, 'PATCH', { status: 'Maintenance', expectedStatus: 'Available' })).status, 200);
  await peer.send('Network.emulateNetworkConditions', { offline: false, latency: 0, downloadThroughput: -1, uploadThroughput: -1 });
  await waitFor(() => peer.read(`(${connected}) && (${statusIs('Maintenance')})`), 'restart + reconnect + missed change refetch', 45000);
  assert.equal(await peer.read('globalThis.__pageMarker'), 'no-f5');
  const states = await peer.read('globalThis.__realtimeStates');
  assert.ok(states.some(state => ['reconnecting', 'disconnected', 'connecting'].includes(state)), 'B observed an interrupted connection');
  await until(`(${connected}) && (${statusIs('Maintenance')})`);
  await evaluate('document.querySelector(".cafe-station").click()');
  await until('Boolean(document.querySelector("dialog .cafe-dialog-actions button:not([disabled])"))');
  await evaluate('document.querySelector("dialog .cafe-dialog-actions button").click()');
  await waitFor(() => peer.read(statusIs('Available')), 'post restart live event');
  await screenshot('realtime-live-admin.png');
  const result = { passed: true, source: 'Two browser tabs on ONE PC, real SDK/Kestrel/SQL; NOT physical LAN acceptance',
    rounds: samples, maxClickToVisibleMs: Math.max(...samples.map(sample => sample.clickToVisibleMs)),
    restart: 'SQL persisted, B refetched missed change and received next event without F5',
    authorization: 'Waiter observes but PATCH 403; customer gets no computer event; anonymous negotiate 401',
    reconnectStates: states, lanAccepted: false };
  await writeFile(path.join(outputDir, 'realtime-live.json'), JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
} finally {
  customer?.dispose();
  socket.removeEventListener('message', onMessage);
  await peer?.send('Network.emulateNetworkConditions', { offline: false, latency: 0, downloadThroughput: -1, uploadThroughput: -1 }).catch(() => {});
  await peer?.close();
  await evaluate(`(async () => { (await import('/src/shared/staffSession.js')).clearStaffAccessToken(); })()`).catch(() => {});
  socket.close();
}
