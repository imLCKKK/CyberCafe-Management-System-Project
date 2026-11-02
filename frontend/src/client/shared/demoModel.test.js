import test from 'node:test';
import assert from 'node:assert/strict';
import { createClientDemo, DEMO_PASSWORD } from './demoData.js';
import { currentClient, getReceipts, isValidClientDemo, placeClientOrder, preparePasswordChange, requestItemCancel, requestTopUp, simulateTopUp } from './demoModel.js';

const now = new Date('2026-10-20T15:00:00+07:00');
const seed = () => createClientDemo(now);
function assertLedger(db) {
  let balance = 0;
  const sources = ['top_up_id', 'gaming_session_id', 'order_id', 'order_item_id', 'tournament_registration_id'];
  for (const row of [...db.wallet_transactions].sort((a, b) => new Date(a.recorded_at) - new Date(b.recorded_at) || a.id - b.id)) {
    balance += row.amount;
    assert.equal(row.balance_after, balance);
    assert.equal(sources.filter((key) => row[key] !== null).length, 1);
  }
  assert.equal(balance, currentClient(db).customer.wallet_balance);
}
test('mock records reconcile the complete wallet ledger and session receipts', () => {
  const db = seed(); assert.ok(isValidClientDemo(db)); assertLedger(db);
  assert.equal(getReceipts(db).reduce((sum, r) => sum + r.total, 0), 147000);
  assert.equal(db.wallet_transactions.reduce((sum, r) => sum + r.amount, 0), 153000);
});

test('stored data rejects invalid display dates, duplicate IDs and missing customer accounts', () => {
  for (const table of ['accounts', 'orders', 'top_ups', 'gaming_sessions', 'wallet_transactions']) {
    const db = seed();
    const field = Object.keys(db[table][0]).find((key) => key.endsWith('_at') && db[table][0][key]);
    assert.ok(field, `Date fixture for ${table}`);
    db[table][0][field] = 'broken-date';
    assert.equal(isValidClientDemo(db), false, `${table}.${field}`);
  }
  const duplicate = seed(); duplicate.products.push({ ...duplicate.products[0] });
  assert.equal(isValidClientDemo(duplicate), false);
  const orphan = seed(); orphan.customers.push({ id: 999, account_id: 999, wallet_balance: 0, wallet_version: 0 });
  assert.equal(isValidClientDemo(orphan), false);
});
test('ordering is atomic, snapshots current prices and conserves wallet versus receipts', () => {
  const db = seed(); const copy = structuredClone(db);
  const result = placeClientOrder(db, { 1: 2, 3: 1 }, now);
  assert.equal(currentClient(result).customer.wallet_balance, 82000);
  assert.equal(getReceipts(result)[0].total - getReceipts(db)[0].total, 71000);
  assert.equal(result.order_items.at(-1).unit_price_snapshot, 15000);
  assert.deepEqual(db, copy); assertLedger(result);
  for (const cart of [{ 1: 99 }, { 999: 1 }, { 1: -1 }, { 1: 1.5 }, {}]) assert.throws(() => placeClientOrder(db, cart, now));
  const ended = structuredClone(db); ended.gaming_sessions[0].status = 'Ended';
  assert.throws(() => placeClientOrder(ended, { 1: 1 }, now));
});
test('customer cancellation requests do not cancel items or refund money and cannot duplicate', () => {
  const db = seed(); const result = requestItemCancel(db, 2, now);
  assert.equal(result.order_items.find((r) => r.id === 2).status, 'Pending');
  assert.equal(currentClient(result).customer.wallet_balance, 153000);
  assert.equal(result.wallet_transactions.length, db.wallet_transactions.length);
  assert.throws(() => requestItemCancel(result, 2, now));
  assert.throws(() => requestItemCancel(db, 1, now));
  const ended = structuredClone(db); ended.gaming_sessions[0].status = 'Ended';
  assert.equal(requestItemCancel(ended, 2, now).order_item_cancel_requests.at(-1).status, 'Pending');
});
test('top-up request, one-time credit and collection respect separate stages and block unpaid repeats', () => {
  let db = requestTopUp(seed(), 100000, now); const id = db.top_ups.at(-1).id;
  assert.equal(currentClient(db).customer.wallet_balance, 153000);
  assert.throws(() => requestTopUp(db, 50000, now));
  db = simulateTopUp(db, id, 'credit', 'Cash', now);
  assert.equal(currentClient(db).customer.wallet_balance, 253000);
  assert.equal(db.top_ups.at(-1).status, 'CreditedUnpaid');
  assert.throws(() => simulateTopUp(db, id, 'credit', 'Cash', now));
  assert.throws(() => requestTopUp(db, 50000, now));
  db = simulateTopUp(db, id, 'collect', 'BankTransfer', now);
  assert.equal(currentClient(db).customer.wallet_balance, 253000);
  assert.equal(db.top_ups.at(-1).collection_method, 'BankTransfer');
  assert.throws(() => simulateTopUp(db, id, 'collect', 'Cash', now));
  assert.equal(requestTopUp(db, 50000, now).top_ups.at(-1).status, 'Pending'); assertLedger(db);
  const empty = seed(); empty.customers[0].wallet_balance = 0;
  assert.throws(() => requestTopUp(empty, 50000, now));
});
test('password change verifies the current password and persists only a salted hash', async () => {
  const db = seed();
  await assert.rejects(preparePasswordChange(db, 'wrong', 'NewPass@2026', 'NewPass@2026'));
  await assert.rejects(preparePasswordChange(db, DEMO_PASSWORD, 'short', 'short'));
  await assert.rejects(preparePasswordChange(db, DEMO_PASSWORD, 'NewPass@2026', 'mismatch'));
  const transform = await preparePasswordChange(db, DEMO_PASSWORD, 'NewPass@2026', 'NewPass@2026');
  const result = transform(db);
  assert.notEqual(result.accounts[0].password_hash, db.accounts[0].password_hash);
  assert.ok(!JSON.stringify(result).includes('NewPass@2026'));
  await preparePasswordChange(result, 'NewPass@2026', 'Another@2026', 'Another@2026');
  assert.throws(() => transform(result));
});
