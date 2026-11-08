import assert from 'node:assert/strict';
import { afterEach, beforeEach, test } from 'node:test';
import { clearStoredSession, markSessionVerified, storeSession } from './auth/api.js';
import { createCustomer, customerDate, getCustomerProfile, getCustomerWallet, getCustomers, walletMoney, walletTypeLabel } from './customerWalletApi.js';

const original = { fetch: globalThis.fetch, storage: globalThis.localStorage, window: globalThis.window };
const profile = { customerId: 7, fullName: 'SQL Customer', phone: '0900000007', email: null, status: 'Active', createdAtUtc: '2026-11-08T00:00:00Z' };
const wallet = { customerId: 7, balance: 125000.5, transactions: [{ transactionId: 3, amount: -25000, balanceAfter: 125000.5, type: 'OrderPayment', description: null, createdAtUtc: '2026-11-08T00:00:00Z' }], page: 1, pageSize: 20, totalCount: 1 };
let requests;

beforeEach(() => {
  const storage = new Map();
  globalThis.localStorage = { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) };
  globalThis.window = { dispatchEvent() {} };
  const session = { accountType: 'customer', accessToken: 'test-only-verified-token', customerId: 999 };
  storeSession(session);
  markSessionVerified(session);
  requests = [];
});
afterEach(() => {
  clearStoredSession();
  globalThis.fetch = original.fetch;
  if (original.storage === undefined) delete globalThis.localStorage; else globalThis.localStorage = original.storage;
  if (original.window === undefined) delete globalThis.window; else globalThis.window = original.window;
});
function respond(body, status = 200) {
  globalThis.fetch = async (url, options) => { requests.push({ url, options }); return Response.json(body, { status }); };
}

test('own profile and wallet use /me and verified JWT, without a browser customer ID', async () => {
  respond(profile);
  assert.deepEqual(await getCustomerProfile(), profile);
  assert.equal(requests[0].url, '/api/customers/me');
  assert.equal(requests[0].options.headers.get('Authorization'), 'Bearer test-only-verified-token');
  respond(wallet);
  assert.deepEqual(await getCustomerWallet(), wallet);
  assert.equal(requests[1].url, '/api/customers/me/wallet?page=1&pageSize=20');
});

test('create whitelists fields, trims identity and never sends a starting wallet balance', async () => {
  respond(profile, 201);
  await createCustomer({ fullName: ' SQL Customer ', phone: ' 0900000007 ', email: ' ', password: 'Test-password-2026!', walletBalance: 999999, status: 'Admin' });
  assert.equal(requests[0].url, '/api/admin/customers');
  assert.equal(requests[0].options.method, 'POST');
  assert.deepEqual(JSON.parse(requests[0].options.body), { fullName: 'SQL Customer', phone: '0900000007', email: null, password: 'Test-password-2026!' });
});

test('an admin customer response for another ID is rejected', async () => {
  respond(profile);
  await assert.rejects(getCustomerProfile(8), /không hợp lệ/);
  respond(wallet);
  await assert.rejects(getCustomerWallet({ customerId: 8 }), /không hợp lệ/);
});

test('malformed or duplicate ledger rows cannot appear as a valid wallet', async () => {
  for (const bad of [null, { ...wallet, balance: '125000.5' }, { ...wallet, transactions: [wallet.transactions[0], wallet.transactions[0]], totalCount: 2 }, { ...wallet, transactions: [{ ...wallet.transactions[0], amount: '25000' }] }]) {
    respond(bad);
    await assert.rejects(getCustomerWallet(), /không hợp lệ/);
  }
});

test('empty SQL pages stay empty and server errors reject instead of returning cached money', async () => {
  respond({ items: [], page: 1, pageSize: 20, totalCount: 0 });
  assert.deepEqual((await getCustomers()).items, []);
  respond(wallet);
  assert.equal((await getCustomerWallet()).balance, 125000.5);
  respond({ title: 'Internal SQL information' }, 500);
  await assert.rejects(getCustomerWallet(), error => error.status === 500 && !error.message.includes('Internal SQL'));
  globalThis.fetch = async () => { throw new TypeError('Network unavailable'); };
  await assert.rejects(getCustomerWallet(), /Không thể kết nối máy chủ/);
});

test('historical fractional VND is retained and timestamps display in Vietnam', () => {
  assert.match(walletMoney(125000.5), /125.000,5/);
  assert.match(customerDate('2026-11-08T00:00:00Z'), /07:00/);
  assert.equal(walletTypeLabel('GamingCharge'), 'Tiền giờ');
  assert.equal(walletTypeLabel('OrderRefund'), 'Hoàn tiền đơn');
  assert.equal(walletTypeLabel('LegacyType'), 'LegacyType');
});
