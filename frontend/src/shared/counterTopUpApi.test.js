import assert from 'node:assert/strict';
import { afterEach, beforeEach, test } from 'node:test';
import { clearStoredSession, markSessionVerified, storeSession } from './auth/api.js';
import { approveCounterTopUp, beginCounterTopUpIntent, clearCounterTopUpIntent, COUNTER_TOPUP_AMOUNTS, createCounterTopUp, getAdminCounterTopUps, getCounterTopUpOptions, getOwnCounterTopUps, readCounterTopUpIntent } from './counterTopUpApi.js';

const original = { fetch: globalThis.fetch, storage: globalThis.localStorage, window: globalThis.window };
const idempotencyKey = '4c51e69b-41d5-4025-b52d-7bcb184df8a0';
const anotherKey = '9cc18d31-de9a-48e3-bc72-b43034020a17';
const request = { requestId: 3, customerId: 7, customerName: 'Khách SQL', customerPhone: '0900000007', amount: 100000, bonusAmount: 15000, totalAmount: 115000, status: 'Pending', createdAtUtc: '2026-11-14T00:00:00Z', approvedAtUtc: null, approvedByEmployeeId: null, transactionId: null, balanceAfter: null };
const approved = { ...request, status: 'Approved', approvedAtUtc: '2026-11-14T00:01:00Z', approvedByEmployeeId: 1, transactionId: 8, balanceAfter: 115000 };
const options = COUNTER_TOPUP_AMOUNTS.map((amount, index) => ({ amount, bonusAmount: [0, 0, 0, 5000, 15000, 35000, 50000, 100000][index], totalAmount: amount + [0, 0, 0, 5000, 15000, 35000, 50000, 100000][index] }));
const page = items => ({ items, page: 1, pageSize: 20, totalCount: items.length });
let requests;
beforeEach(() => {
  const storage = new Map();
  globalThis.localStorage = { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) };
  globalThis.window = { dispatchEvent() {} };
  const session = { accountType: 'customer', accessToken: 'counter-test-only-verified-token' };
  storeSession(session); markSessionVerified(session);
  requests = [];
});
afterEach(() => {
  clearStoredSession(); globalThis.fetch = original.fetch;
  if (original.storage === undefined) delete globalThis.localStorage; else globalThis.localStorage = original.storage;
  if (original.window === undefined) delete globalThis.window; else globalThis.window = original.window;
});
function respond(body, status = 200) {
  globalThis.fetch = async (url, config) => { requests.push({ url, config }); return Response.json(body, { status }); };
}

test('server options include all eight denominations and three authoritative amounts', async () => {
  respond(options);
  assert.deepEqual(await getCounterTopUpOptions(), options);
  assert.equal(requests[0].url, '/api/customers/me/wallet/counter-topups/options');
  assert.deepEqual(options.map(row => row.totalAmount), [10000, 20000, 30000, 55000, 115000, 235000, 350000, 600000]);
  for (const bad of [options.slice(1), [...options].reverse(), options.map(row => ({ ...row, totalAmount: row.amount })), options.map(row => ({ ...row, bonusAmount: '0' }))]) {
    respond(bad); await assert.rejects(getCounterTopUpOptions(), /không hợp lệ/);
  }
});
test('create sends only cash amount with UUID header and verified JWT to /me', async () => {
  respond(request, 201);
  assert.deepEqual(await createCounterTopUp({ customerId: 7, amount: 100000, idempotencyKey, bonusAmount: 99999 }), request);
  assert.equal(requests[0].url, '/api/customers/me/wallet/counter-topups');
  assert.equal(requests[0].config.method, 'POST');
  assert.equal(requests[0].config.headers.get('Idempotency-Key'), idempotencyKey);
  assert.equal(requests[0].config.headers.get('Authorization'), 'Bearer counter-test-only-verified-token');
  assert.deepEqual(JSON.parse(requests[0].config.body), { amount: 100000 });
  await assert.rejects(createCounterTopUp({ customerId: 7, amount: 150000, idempotencyKey }), /không hợp lệ/);
  await assert.rejects(createCounterTopUp({ customerId: 7, amount: 100000, idempotencyKey: 'not-a-key' }), /không hợp lệ/);
  assert.equal(requests.length, 1);
});
test('own requests reject another customer and malformed snapshots or duplicate rows', async () => {
  respond(page([request]));
  assert.deepEqual(await getOwnCounterTopUps({ customerId: 7 }), page([request]));
  assert.equal(requests[0].url, '/api/customers/me/wallet/counter-topups?page=1&pageSize=20');
  await assert.rejects(getOwnCounterTopUps({ customerId: 9 }), /không hợp lệ/);
  for (const rows of [[request, request], [{ ...request, totalAmount: 100000 }], [{ ...request, transactionId: 7 }], [{ ...approved, transactionId: null }], [{ ...request, amount: '100000' }]]) {
    respond(page(rows)); await assert.rejects(getOwnCounterTopUps({ customerId: 7 }), /không hợp lệ/);
  }
});
test('approval requires explicit cash confirmation and does not send gift or credit values', async () => {
  respond(approved);
  assert.deepEqual(await approveCounterTopUp(request, true), approved);
  assert.equal(requests[0].url, '/api/admin/topups/3/approve');
  assert.deepEqual(JSON.parse(requests[0].config.body), { cashReceived: true, cashAmount: 100000 });
  await assert.rejects(approveCounterTopUp(request, false), /đã thu đủ tiền mặt/);
  await assert.rejects(approveCounterTopUp(approved, true), /đã thu đủ tiền mặt/);
  assert.equal(requests.length, 1);
  respond({ ...approved, totalAmount: 100000, bonusAmount: 0 });
  await assert.rejects(approveCounterTopUp(request, true), /không hợp lệ/);
});
test('historical approved zero gift snapshots remain unchanged on reads and retry', async () => {
  const historical = { ...approved, bonusAmount: 0, totalAmount: 100000, balanceAfter: 100000 };
  respond(page([historical]));
  assert.equal((await getAdminCounterTopUps({ status: 'Approved' })).items[0].bonusAmount, 0);
  assert.equal(requests[0].url, '/api/admin/topups?page=1&pageSize=20&status=Approved');
  respond(historical);
  assert.deepEqual(await createCounterTopUp({ customerId: 7, amount: 100000, idempotencyKey }), historical);
  respond(page([request]));
  await assert.rejects(getAdminCounterTopUps({ status: 'Approved' }), /không hợp lệ/);
});
test('network and server failures never return a saved wallet or fake approval', async () => {
  respond({ title: 'sensitive SQL failure' }, 500);
  await assert.rejects(approveCounterTopUp(request, true), error => error.status === 500 && !error.message.includes('sensitive'));
  globalThis.fetch = async () => { throw new TypeError('offline'); };
  await assert.rejects(createCounterTopUp({ customerId: 7, amount: 100000, idempotencyKey }), /Không thể kết nối máy chủ/);
  respond({ detail: 'Một yêu cầu đang chờ.' }, 409);
  await assert.rejects(createCounterTopUp({ customerId: 7, amount: 100000, idempotencyKey }), error => error.status === 409);
});
test('ambiguous create persists and retries exactly the same key and amount after reload', async () => {
  const intent = beginCounterTopUpIntent(7, 100000, localStorage, () => idempotencyKey);
  globalThis.fetch = async () => { throw new TypeError('response lost after SQL commit'); };
  await assert.rejects(createCounterTopUp({ customerId: 7, ...intent }));
  assert.deepEqual(readCounterTopUpIntent(7), intent);
  assert.deepEqual(beginCounterTopUpIntent(7, 500000, localStorage, () => anotherKey), intent);
  respond(request);
  await createCounterTopUp({ customerId: 7, ...readCounterTopUpIntent(7) });
  assert.equal(requests[0].config.headers.get('Idempotency-Key'), idempotencyKey);
  assert.deepEqual(JSON.parse(requests[0].config.body), { amount: 100000 });
  clearCounterTopUpIntent(7, anotherKey);
  assert.deepEqual(readCounterTopUpIntent(7), intent);
  clearCounterTopUpIntent(7, idempotencyKey);
  assert.equal(readCounterTopUpIntent(7), null);
});
test('intent storage separates customers and refuses to send if persistence is unavailable or corrupt', () => {
  const intent = beginCounterTopUpIntent(7, 50000, localStorage, () => idempotencyKey);
  assert.equal(readCounterTopUpIntent(8), null);
  const other = beginCounterTopUpIntent(8, 500000, localStorage, () => anotherKey);
  assert.notDeepEqual(other, intent);
  assert.throws(() => beginCounterTopUpIntent(9, 10000, { getItem: () => null, setItem: () => { throw Error('denied'); } }, () => idempotencyKey), /Chưa gửi yêu cầu/);
  assert.throws(() => readCounterTopUpIntent(9, { getItem: () => '{broken' }), /Khóa yêu cầu cũ không hợp lệ/);
});
