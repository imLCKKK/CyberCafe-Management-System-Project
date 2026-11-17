import assert from 'node:assert/strict';
import { afterEach, beforeEach, test } from 'node:test';
import { clearStoredSession, markSessionVerified, storeSession } from './auth/api.js';
import { beginZaloPayTopUpIntent, clearZaloPayTopUpIntent, createZaloPayTopUp, getOwnZaloPayPayments,
  getZaloPayConfig, getZaloPayPayment, isSandboxCheckoutUrl, paymentCanBePaid, paymentIsApplied,
  paymentShouldPoll, paymentCustomerId, readZaloPayTopUpIntent, syncZaloPayPayment } from './zaloPayPaymentApi.js';

const original = { fetch: globalThis.fetch, storage: globalThis.localStorage, window: globalThis.window };
const key = '4c51e69b-41d5-4025-b52d-7bcb184df8a0';
const otherKey = '9cc18d31-de9a-48e3-bc72-b43034020a17';
const pending = { id: 3, customerId: 7, purpose: 'TopUp', sourceId: 3, expectedAmountVnd: 50000,
  status: 'Pending', paymentUrl: 'https://qcgateway.zalopay.vn/openinapp?order=internal-fixture-only',
  merchantTransactionId: '991117_internal_fixture_3', createdAtUtc: '2099-11-17T00:00:00Z', expiresAtUtc: '2099-11-17T00:15:00Z',
  providerTransactionId: null, providerAmountVnd: null, verifiedAtUtc: null, requiresReconciliation: false,
  failureReason: null, appliedAtUtc: null, walletTransactionId: null, paymentId: null, settlementFailureReason: null };
const succeeded = { ...pending, status: 'Succeeded', providerTransactionId: 'internal-fixture-provider-3',
  providerAmountVnd: 50000, verifiedAtUtc: '2099-11-17T00:01:00Z' };
const applied = { ...succeeded, appliedAtUtc: '2099-11-17T00:01:01Z', walletTransactionId: 8 };
const page = items => ({ items, page: 1, pageSize: 20, totalCount: items.length });
let requests;
beforeEach(() => {
  const storage = new Map();
  globalThis.localStorage = { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) };
  globalThis.window = { dispatchEvent() {} };
  const session = { accountType: 'customer', accessToken: 'zalopay-internal-fixture-verified-token' };
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

test('server config is required and malformed/disabled responses never invent enabled sandbox', async () => {
  const config = { enabled: false, minAmountVnd: 1, maxAmountVnd: 999999999 };
  respond(config); assert.deepEqual(await getZaloPayConfig(), config);
  assert.equal(requests[0].url, '/api/customers/me/payments/zalopay/config');
  for (const bad of [null, { ...config, enabled: 'true' }, { ...config, minAmountVnd: 0 }, { ...config, maxAmountVnd: 0 }]) {
    respond(bad); await assert.rejects(getZaloPayConfig(), /không hợp lệ/);
  }
  respond({ title: 'provider failure' }, 503); await assert.rejects(getZaloPayConfig(), error => error.status === 503);
});

test('create sends only integer amount and persisted UUID using the verified /me session', async () => {
  respond(pending);
  assert.deepEqual(await createZaloPayTopUp({ customerId: '7', amountVnd: 50000, idempotencyKey: key, bonusAmount: 900000, customer: 99 }), pending);
  assert.equal(requests[0].url, '/api/customers/me/payments/zalopay/topups');
  assert.equal(requests[0].config.method, 'POST');
  assert.equal(requests[0].config.headers.get('Idempotency-Key'), key);
  assert.equal(requests[0].config.headers.get('Authorization'), 'Bearer zalopay-internal-fixture-verified-token');
  assert.deepEqual(JSON.parse(requests[0].config.body), { amountVnd: 50000 });
  for (const amountVnd of [0, -1, 100.5, '50000', 1000000000, NaN])
    await assert.rejects(createZaloPayTopUp({ customerId: 7, amountVnd, idempotencyKey: key }), /không hợp lệ/);
  await assert.rejects(createZaloPayTopUp({ customerId: 7, amountVnd: 50000, idempotencyKey: 'not-uuid' }), /không hợp lệ/);
  assert.equal(requests.length, 1);
});

test('own intent pages reject another account, wrong purpose, duplicates and malformed receipt/application links', async () => {
  respond(page([pending]));
  assert.deepEqual(await getOwnZaloPayPayments({ customerId: '7', purpose: 'TopUp' }), page([pending]));
  assert.equal(requests[0].url, '/api/customers/me/payments/zalopay?page=1&pageSize=20&purpose=TopUp');
  for (const rows of [[{ ...pending, customerId: 8 }], [{ ...pending, purpose: 'Order' }], [pending, pending],
    [{ ...pending, expectedAmountVnd: '50000' }], [{ ...pending, walletTransactionId: 8 }],
    [{ ...succeeded, providerAmountVnd: 1 }], [{ ...applied, paymentId: 4 }],
    [{ ...applied, appliedAtUtc: null }], [{ ...pending, expiresAtUtc: 'invalid' }]]) {
    respond(page(rows)); await assert.rejects(getOwnZaloPayPayments({ customerId: 7, purpose: 'TopUp' }), /không hợp lệ/);
  }
  // A mismatched/late receipt can legitimately be held for reconciliation; never render it as credited.
  const review = { ...succeeded, requiresReconciliation: true, providerAmountVnd: 1 };
  respond(page([review])); assert.deepEqual((await getOwnZaloPayPayments({ customerId: 7 })).items, [review]);
  assert.equal(paymentIsApplied(review), false);
  for (const customerId of [0, -1, '', null, undefined, 'bad', '7.5', true]) assert.throws(() => paymentCustomerId(customerId), /không hợp lệ/);
});

test('get and sync reuse the saved ID and cannot create or apply payments in the browser', async () => {
  respond(succeeded);
  assert.deepEqual(await getZaloPayPayment({ customerId: 7, paymentId: 3 }), succeeded);
  assert.equal(paymentIsApplied(succeeded), false);
  respond(applied);
  assert.deepEqual(await syncZaloPayPayment({ customerId: 7, paymentId: 3 }), applied);
  assert.equal(paymentIsApplied(applied), true);
  assert.deepEqual(requests.map(row => [row.url, row.config.method]), [
    ['/api/customers/me/payments/zalopay/3', 'GET'], ['/api/customers/me/payments/zalopay/3/sync', 'POST']]);
  assert.equal(requests[1].config.body, undefined);
  respond({ ...applied, id: 4 }); await assert.rejects(syncZaloPayPayment({ customerId: 7, paymentId: 3 }), /không hợp lệ/);
  const order = { ...succeeded, purpose: 'Order', sourceId: 42, appliedAtUtc: '2099-11-17T00:01:01Z', paymentId: 10 };
  respond(page([order])); assert.equal(paymentIsApplied((await getOwnZaloPayPayments({ customerId: 7, purpose: 'Order' })).items[0]), true);
});

test('topup intent survives timeout/500/reload and retries preserve both UUID and amount per customer', async () => {
  const saved = beginZaloPayTopUpIntent('7', 50000, localStorage, () => key);
  respond({ title: 'ambiguous server failure' }, 500);
  await assert.rejects(createZaloPayTopUp({ customerId: 7, ...saved }), error => error.status === 500);
  assert.deepEqual(readZaloPayTopUpIntent(7), saved);
  assert.deepEqual(beginZaloPayTopUpIntent(7, 100000, localStorage, () => otherKey), saved);
  assert.equal(readZaloPayTopUpIntent(8), null);
  clearZaloPayTopUpIntent(7, otherKey); assert.deepEqual(readZaloPayTopUpIntent(7), saved);
  respond(pending); await createZaloPayTopUp({ customerId: 7, ...readZaloPayTopUpIntent(7) });
  assert.deepEqual(requests.map(row => row.config.headers.get('Idempotency-Key')), [key, key]);
  assert.deepEqual(requests.map(row => JSON.parse(row.config.body)), [{ amountVnd: 50000 }, { amountVnd: 50000 }]);
  clearZaloPayTopUpIntent(7, key); assert.equal(readZaloPayTopUpIntent(7), null);
  assert.throws(() => beginZaloPayTopUpIntent(7, 50000, { getItem: () => null, setItem: () => { throw Error('denied'); } }, () => key), /Chưa gửi yêu cầu/);
  assert.throws(() => readZaloPayTopUpIntent(7, { getItem: () => '{broken' }), /đã lưu không hợp lệ/);
});

test('polling is bounded, receipt success keeps reconciling until applied, and review/terminal statuses stop', () => {
  const now = Date.parse('2099-11-17T00:01:00Z');
  assert.equal(paymentShouldPoll(pending, now, now + 2500), true);
  assert.equal(paymentShouldPoll(pending, now, now + 600000), false);
  assert.equal(paymentShouldPoll(succeeded, now, now + 2500), true);
  assert.equal(paymentShouldPoll(succeeded, now, now + 120000), false);
  for (const row of [applied, { ...pending, status: 'Failed' }, { ...pending, status: 'Expired' },
    { ...succeeded, requiresReconciliation: true }, { ...succeeded, settlementFailureReason: 'internal_fixture_review' }])
    assert.equal(paymentShouldPoll(row, now, now + 2500), false);
  assert.equal(paymentCanBePaid(pending, true, now), true);
  assert.equal(paymentCanBePaid(pending, true, Date.parse(pending.expiresAtUtc)), false);
  assert.equal(paymentCanBePaid({ ...pending, requiresReconciliation: true }, true, now), false);
});

test('checkout links allow exact sandbox HTTPS hosts only', () => {
  assert.equal(isSandboxCheckoutUrl(pending.paymentUrl), true);
  for (const value of ['https://gateway.zalopay.vn/pay', 'http://qcgateway.zalopay.vn/pay',
    'https://qcgateway.zalopay.vn.evil.test/pay', 'https://user@qcgateway.zalopay.vn/pay',
    'https://qcgateway.zalopay.vn:8443/pay', 'https://qcgateway.zalopay.vn/pay#fragment', 'javascript:alert(1)', null])
    assert.equal(isSandboxCheckoutUrl(value), false);
});
