import assert from 'node:assert/strict';
import { afterEach, beforeEach, test } from 'node:test';
import { clearVerifiedSession, markSessionVerified, storeSession } from '../auth/api.js';
import * as invoiceApi from './invoiceApi.js';
import { getInvoice, issueInvoice, listInvoices, validateInvoice } from './invoiceApi.js';

const original = { fetch: globalThis.fetch, storage: globalThis.localStorage, window: globalThis.window };
const storage = new Map();
const fixture = () => ({ id: 1, customerId: 7, customerName: 'Invoice customer', orderIds: [12], gamingSessionId: null,
  actorType: 'Staff', employeeId: 3, issuedBy: 'Cashier', issuedAt: '2026-11-15T12:00:00Z', subtotal: 100000, discount: 10000,
  grossAmount: 90000, cancelledAmount: 0, refundAmount: 0, paidAmount: 90000, amountDue: 0, overpaidAmount: 0, status: 'Paid',
  lines: [{ id: 1, description: 'Snapshot food', quantity: 1, unitPrice: 100000, subtotal: 100000, discountAmount: 10000, amount: 90000, cancelledAmount: 0 }], payments: [] });
beforeEach(() => {
  storage.clear(); clearVerifiedSession();
  globalThis.localStorage = { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) };
  globalThis.window = { dispatchEvent() {} };
  const session = { accessToken: 'invoice-test-token', accountType: 'employee' };
  storeSession(session); markSessionVerified(session);
});
afterEach(() => {
  clearVerifiedSession(); globalThis.fetch = original.fetch;
  if (original.storage === undefined) delete globalThis.localStorage; else globalThis.localStorage = original.storage;
  if (original.window === undefined) delete globalThis.window; else globalThis.window = original.window;
});
test('admin/client routes use authenticated API once without accepting customerId', async () => {
  const calls = [];
  globalThis.fetch = async (url, options) => { calls.push({ url, options }); return Response.json([fixture()]); };
  await listInvoices(true); await listInvoices(false);
  assert.deepEqual(calls.map(call => call.url), ['/api/admin/invoices', '/api/client/invoices']);
  assert.equal(calls[0].options.headers.get('Authorization'), 'Bearer invoice-test-token');
  assert.ok(calls.every(call => !call.url.includes('customerId')));
});
test('lost response retry preserves issuance key and source payload', async () => {
  const calls = []; let fail = true;
  globalThis.fetch = async (url, options) => {
    calls.push({ url, key: options.headers.get('Idempotency-Key'), body: options.body });
    if (fail) { fail = false; throw new TypeError('Lost response'); }
    return Response.json(fixture());
  };
  const body = { kind: 'Orders', orderIds: [12], gamingSessionId: null };
  await assert.rejects(issueInvoice(body, 'stable-key'), { status: 0 });
  assert.equal((await issueInvoice(body, 'stable-key')).id, 1);
  assert.deepEqual(calls[0], calls[1]);
  assert.equal(calls[0].url, '/api/admin/invoices');
});
test('API error never falls back to demo invoices', async () => {
  globalThis.fetch = async () => new Response(JSON.stringify({ detail: 'Unavailable' }), { status: 503, headers: { 'Content-Type': 'application/problem+json' } });
  await assert.rejects(listInvoices(false), { status: 503 });
});
test('empty SQL list stays empty and detail retains server snapshot', async () => {
  globalThis.fetch = async () => Response.json([]); assert.deepEqual(await listInvoices(false), []);
  globalThis.fetch = async () => Response.json(fixture());
  const row = await getInvoice(false, 1); assert.equal(row.lines[0].description, 'Snapshot food'); assert.equal(row.lines[0].unitPrice, 100000);
});
test('reject unsafe money or malformed payment instead of rounding/NaN', () => {
  assert.throws(() => validateInvoice({ ...fixture(), paidAmount: Number.MAX_SAFE_INTEGER + 1 }));
  assert.throws(() => validateInvoice({ ...fixture(), payments: [{}] }));
  assert.throws(() => validateInvoice({ ...fixture(), grossAmount: 99999 }));
});

const confirmationRefund = () => ({ id: 2, type: 'Refund', method: 'ZaloPay', status: 'Succeeded', amount: 20000,
  walletTransactionId: null, paidAt: '2026-11-16T17:03:00Z', paymentTime: '2026-11-16T17:03:00Z',
  confirmedAt: '2026-11-16T17:03:00Z', providerOccurredAt: null, paymentTimeBasis: 'Confirmation' });

test('confirmation refund preserves nullable provider metadata', async () => {
  const refund = confirmationRefund();
  globalThis.fetch = async () => Response.json({ ...fixture(), payments: [refund] });
  assert.deepEqual((await getInvoice(true, 1)).payments[0], refund);
});
test('reject invalid or inconsistent successful payment timestamps', () => {
  for (const change of [{ paymentTime: null }, { confirmedAt: null }, { confirmedAt: 'invalid' },
    { providerOccurredAt: 'invalid' }, { paymentTimeBasis: 'Unknown' },
    { confirmedAt: '2026-11-16T17:04:00Z' }]) {
    assert.throws(() => validateInvoice({ ...fixture(), payments: [{ ...confirmationRefund(), ...change }] }), undefined, JSON.stringify(change));
  }
});
test('confirmation label uses Vietnam time and never claims provider time', () => {
  assert.equal(typeof invoiceApi.invoicePaymentTiming, 'function');
  const lines = invoiceApi.invoicePaymentTiming(confirmationRefund()).join(' ');
  assert.ok(lines.includes('Ghi nhận theo thời điểm hệ thống xác nhận'));
  assert.ok(lines.includes('17/11/2026') && lines.includes('00:03:00') && lines.includes('UTC+07:00'));
  assert.ok(lines.includes('Chưa có thời điểm giao dịch từ nhà cung cấp'));
  const later = invoiceApi.invoicePaymentTiming({ ...confirmationRefund(), providerOccurredAt: '2026-11-16T17:00:00Z' }).join(' ');
  assert.ok(later.includes('Ghi nhận theo thời điểm hệ thống xác nhận'));
  assert.ok(!later.includes('Chưa có thời điểm giao dịch từ nhà cung cấp'));
  assert.deepEqual(invoiceApi.invoicePaymentTiming({ ...confirmationRefund(), status: 'Pending', paymentTime: null, confirmedAt: null, paymentTimeBasis: null }), []);
});
