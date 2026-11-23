import assert from 'node:assert/strict';
import { afterEach, beforeEach, test } from 'node:test';
import { clearVerifiedSession, markSessionVerified, storeSession } from '../../shared/auth/api.js';
import { voucherApi, voucherInputDate, voucherPayload } from './voucherApi.js';

const original = { fetch: globalThis.fetch, storage: globalThis.localStorage, window: globalThis.window };
const form = () => ({
  code: ' sale10 ', name: ' Voucher ', discountType: 'Percent', discountValue: '12.25',
  minOrderAmount: '100000', maxDiscountAmount: '', startDate: '2026-11-17T22:00:00.123',
  endDate: '2026-11-18T22:00:00.123', totalUsageLimit: '', perCustomerUsageLimit: '', isActive: true,
});
beforeEach(() => {
  const storage = new Map();
  globalThis.localStorage = { getItem: (key) => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value), removeItem: (key) => storage.delete(key) };
  globalThis.window = { dispatchEvent() {} };
  const session = { accessToken: 'voucher-test-token', accountType: 'employee' };
  clearVerifiedSession(); storeSession(session); markSessionVerified(session);
});
afterEach(() => {
  clearVerifiedSession(); globalThis.fetch = original.fetch;
  for (const [key, value] of [['localStorage', original.storage], ['window', original.window]]) {
    if (value === undefined) delete globalThis[key]; else globalThis[key] = value;
  }
});

test('percent payload preserves optional nulls and Vietnam date including milliseconds', () => {
  const payload = voucherPayload(form());
  assert.equal(payload.code, 'sale10'); assert.equal(payload.name, 'Voucher');
  assert.equal(payload.discountValue, 12.25);
  assert.equal(payload.startDate, '2026-11-17T15:00:00.123Z');
  assert.equal(voucherInputDate(payload.startDate), form().startDate);
  for (const key of ['maxDiscountAmount', 'totalUsageLimit', 'perCustomerUsageLimit']) assert.equal(payload[key], null);
});
test('fixed payload preserves caps and separate system/customer limits', () => {
  const payload = voucherPayload({ ...form(), discountType: 'Fixed', discountValue: '15000', maxDiscountAmount: '12000', totalUsageLimit: '100', perCustomerUsageLimit: '2', isActive: false });
  assert.equal(payload.discountValue, 15000); assert.equal(payload.maxDiscountAmount, 12000);
  assert.equal(payload.totalUsageLimit, 100); assert.equal(payload.perCustomerUsageLimit, 2);
  assert.equal(payload.isActive, false);
});
test('invalid input cannot silently become zero, unlimited, rounded money or wrong dates', () => {
  for (const change of [
    { code: 'bad code' }, { name: '  ' }, { discountValue: '' }, { discountValue: '101' },
    { discountValue: '10.001' }, { discountType: 'Fixed', discountValue: '1.5' },
    { minOrderAmount: '' }, { minOrderAmount: '-1' }, { minOrderAmount: '9007199254740993' },
    { maxDiscountAmount: '0' }, { totalUsageLimit: '0' }, { totalUsageLimit: '2147483648' },
    { perCustomerUsageLimit: '1.5' }, { startDate: 'invalid' }, { endDate: form().startDate },
  ]) assert.throws(() => voucherPayload({ ...form(), ...change }), undefined, JSON.stringify(change));
});
test('create/update/status use authenticated voucher API and status changes only status', async () => {
  const calls = [];
  const payload = voucherPayload(form());
  const row = { ...payload, id: 8 };
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options });
    return Response.json(options.method === 'GET' ? [row] : row);
  };
  await voucherApi.list(); await voucherApi.create(payload);
  await voucherApi.update(8, payload); await voucherApi.setStatus(8, false);
  assert.deepEqual(calls.map(({ url, options }) => [url, options.method]), [
    ['/api/admin/vouchers', 'GET'], ['/api/admin/vouchers', 'POST'],
    ['/api/admin/vouchers/8', 'PUT'], ['/api/admin/vouchers/8/status', 'PUT'],
  ]);
  assert.equal(calls[1].options.headers.get('Authorization'), 'Bearer voucher-test-token');
  assert.deepEqual(JSON.parse(calls[2].options.body), payload);
  assert.deepEqual(JSON.parse(calls[3].options.body), { isActive: false });
});
test('malformed or unsafe server money is rejected rather than displayed or saved rounded', async () => {
  for (const data of [{}, [{ ...voucherPayload(form()), id: 8, minOrderAmount: Number.MAX_SAFE_INTEGER + 1 }]]) {
    globalThis.fetch = async () => Response.json(data);
    await assert.rejects(voucherApi.list(), { status: 200 });
  }
});
test('server conflict message is preserved and failed list never falls back to demo', async () => {
  globalThis.fetch = async () => new Response(JSON.stringify({ detail: 'Mã voucher SALE10 đã tồn tại.' }), { status: 409, headers: { 'Content-Type': 'application/problem+json' } });
  await assert.rejects(voucherApi.create(voucherPayload(form())), { status: 409, message: 'Mã voucher SALE10 đã tồn tại.' });
  globalThis.fetch = async () => { throw new TypeError('Network unavailable'); };
  await assert.rejects(voucherApi.list(), { status: 0 });
});
