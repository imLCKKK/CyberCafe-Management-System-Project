import assert from 'node:assert/strict';
import { beforeEach, afterEach, test } from 'node:test';
import { storeSession, markSessionVerified, clearVerifiedSession } from '../src/shared/auth/api.js';
import { getSessionHome, getSessions, openSession, closeSession, validateSession } from '../src/shared/sessionApi.js';

const originals = { fetch: globalThis.fetch, localStorage: globalThis.localStorage, window: globalThis.window };
const row = () => ({ gamingSessionId: 42, computerId: 12, customerId: 7, computerName: 'SQL-PC',
  typeName: 'SQL Premium', cpu: null, ram: null, gpu: null, startTimeUtc: '2026-11-18T00:00:00Z',
  endTimeUtc: null, lastChargedAtUtc: null, pricePerHourSnapshot: 10000, totalCost: 0, status: 'Active' });
let requests;
beforeEach(() => {
  const storage = new Map();
  requests = [];
  globalThis.localStorage = { getItem: key => storage.get(key) ?? null,
    setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) };
  globalThis.window = new EventTarget();
  const session = { accessToken: 'checks', accountType: 'employee' };
  storeSession(session); markSessionVerified(session);
});
afterEach(() => { Object.assign(globalThis, originals); clearVerifiedSession(); });
const reply = body => { globalThis.fetch = async (url, options) => { requests.push({ url, options }); return Response.json(body); }; };

test('Home preserves server machine, session, snapshot and balance', async () => {
  reply({ walletBalance: 50000, session: row() });
  const home = await getSessionHome();
  assert.equal(home.session.computerName, 'SQL-PC');
  assert.equal(home.session.pricePerHourSnapshot, 10000);
  assert.equal(requests[0].url, '/api/client/home');
});
test('Home empty session stays null', async () => {
  reply({ walletBalance: 0, session: null });
  assert.deepEqual(await getSessionHome(), { walletBalance: 0, session: null });
});
test('Home rejects missing session and invalid wallet instead of showing demo', async () => {
  for (const value of [{ walletBalance: 0 }, { walletBalance: -1, session: null }, { walletBalance: '500', session: row() }]) {
    reply(value); await assert.rejects(getSessionHome(), { status: 200 });
  }
});
test('Open sends only source IDs and retains the retry key', async () => {
  reply(row());
  await openSession(12, 7, 'retry-open'); await openSession(12, 7, 'retry-open');
  for (const request of requests) {
    assert.deepEqual(JSON.parse(request.options.body), { computerId: 12, customerId: 7 });
    assert.equal(request.options.headers.get('Idempotency-Key'), 'retry-open');
  }
});
test('Close sends no amount, end time or actor', async () => {
  reply({ ...row(), status: 'Ended', endTimeUtc: '2026-11-18T01:00:00Z', totalCost: 10000 });
  await closeSession(42, 'retry-close');
  assert.equal(requests[0].url, '/api/admin/sessions/42/close');
  assert.deepEqual(JSON.parse(requests[0].options.body), {});
  assert.equal(requests[0].options.headers.get('Idempotency-Key'), 'retry-close');
});
test('Session validation rejects invalid timestamps, fees and active snapshot', () => {
  for (const corrupt of [{ pricePerHourSnapshot: null }, { totalCost: -1 }, { startTimeUtc: '2026-11-18' },
    { status: 'Paused' }, { status: 'Ended' }, { endTimeUtc: '2026-11-18T01:00:00Z' }]) {
    assert.throws(() => validateSession({ ...row(), ...corrupt }), { status: 200 });
  }
});
test('Session list uses API and rejects malformed arrays', async () => {
  reply([row()]); assert.equal((await getSessions()).length, 1);
  reply({ items: [row()] }); await assert.rejects(getSessions(), { status: 200 });
});
test('Admin list keeps both legacy and billable sessions while Home requires an active snapshot', async () => {
  const active = row();
  const legacy = { ...row(), gamingSessionId: 43, computerId: 13, pricePerHourSnapshot: null };
  reply([legacy, active]);
  assert.deepEqual(await getSessions(), [legacy, active]);
  reply({ walletBalance: 50000, session: legacy });
  await assert.rejects(getSessionHome(), { status: 200 });
});
test('API conflict remains an error, with no local success fallback', async () => {
  globalThis.fetch = async () => Response.json({ detail: 'Máy đã có phiên.' }, { status: 409 });
  await assert.rejects(openSession(12, 7, 'conflict'), { status: 409, message: 'Máy đã có phiên.' });
});
