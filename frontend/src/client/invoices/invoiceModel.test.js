import test from 'node:test';
import assert from 'node:assert/strict';
import { createClientDemo } from '../shared/demoData.js';
import { currentClient, getReceipts, money, placeCashOrder } from '../shared/demoModel.js';
import { filterReceipts, orderPaymentText, receiptDateKey, receiptText } from './invoiceModel.js';

const now = new Date('2026-10-21T12:00:00+07:00');
const seed = () => createClientDemo(now);

test('old wallet orders reconcile payments, actual refunds and the session ledger without mutation', () => {
  const db = seed();
  const copy = structuredClone(db);
  const receipts = getReceipts(db);
  assert.equal(receipts.reduce((sum, row) => sum + row.total, 0), 147000);
  assert.equal(receipts.reduce((sum, row) => sum + row.walletPaid, 0), 147000);
  assert.equal(receipts.reduce((sum, row) => sum + row.refunds, 0), 22000);
  for (const receipt of receipts) {
    assert.equal(receipt.cashDue, 0);
    assert.equal(receipt.cashPaid, 0);
    assert.equal(receipt.paymentState, 'Paid');
    assert.equal(receipt.paidTotal + receipt.cashDue, receipt.total);
    assert.equal(-receipt.transactions.reduce((sum, row) => sum + row.amount, 0), receipt.walletPaid);
  }
  assert.deepEqual(db, copy);
});

test('mixed cash and wallet payments distinguish collected cash, pending cash and cancelled portions', () => {
  let db = placeCashOrder(seed(), { 1: 2, 3: 1 }, now);
  const pendingId = db.orders.at(-1).id;
  db.order_items.find((item) => item.order_id === pendingId && item.product_id === 3).status = 'Cancelled';
  db = placeCashOrder(db, { 2: 1 }, now);
  const paidId = db.orders.at(-1).id;
  db.orders.find((order) => order.id === paidId).payment_status = 'Paid';
  const receipt = getReceipts(db)[0];
  assert.equal(receipt.cashDue, 56000);
  assert.equal(receipt.cashPaid, 35000);
  assert.equal(receipt.cashCancelled, 15000);
  assert.equal(receipt.walletPaid, getReceipts(seed())[0].total);
  assert.equal(receipt.total, receipt.walletPaid + receipt.cashPaid + receipt.cashDue);
  assert.equal(receipt.paidTotal, receipt.walletPaid + 35000);
  assert.equal(receipt.paymentState, 'Partial');
  assert.equal(currentClient(db).customer.wallet_balance, 153000);
  assert.match(orderPaymentText(receipt.orderDetails.find((order) => order.id === paidId)), /Đã thu/);
  const text = receiptText(receipt, db, db.accounts[0], now);
  for (const fragment of [`Đã thu tiền mặt: ${money(35000)}`, `Tiền mặt chờ thu: ${money(56000)}`, `Món tiền mặt hủy không thu: -${money(15000)}`]) assert.ok(text.includes(fragment), fragment);
  assert.ok(!text.includes('Các khoản đã được trừ ví'));
});

test('cancelled wallet items are only deducted after an actual refund is recorded', () => {
  const db = seed();
  db.wallet_transactions = db.wallet_transactions.filter((row) => row.transaction_type !== 'OrderItemRefund');
  const receipt = getReceipts(db)[0];
  assert.equal(receipt.refunds, 0);
  assert.equal(receipt.refundPending, 22000);
  assert.equal(receipt.total, getReceipts(seed())[0].total + 22000);
  assert.ok(receiptText(receipt, db, db.accounts[0], now).includes('chưa trừ khỏi tổng ghi nhận'));
});

test('date filtering is inclusive by the Vietnam start date, combined with payment and accent insensitive search', () => {
  let db = seed();
  db.gaming_sessions[0].started_at = '2026-10-20T18:30:00Z'; // 01:30 on the 21st locally.
  db.gaming_sessions[1].started_at = '2026-10-20T16:30:00Z';
  db = placeCashOrder(db, { 1: 1 }, now);
  const receipts = getReceipts(db);
  assert.equal(receiptDateKey('2026-10-20T18:30:00Z'), '2026-10-21');
  assert.deepEqual(filterReceipts(receipts, db, { from: '2026-10-21', to: '2026-10-21' }).map((row) => row.id), [1027]);
  assert.equal(filterReceipts(receipts, db, { query: 'mi xao bo' }).length, 2);
  assert.equal(filterReceipts(receipts, db, { query: 'GM 1047', payment: 'due' }).length, 1);
  assert.equal(filterReceipts(receipts, db, { query: '#GM1047', session: 'Ended' }).length, 0);
  assert.equal(filterReceipts(receipts, db, { payment: 'paid' })[0].id, 1026);
  assert.equal(filterReceipts(receipts, db, { from: '2026-10-22', to: '2026-10-21' }).length, 0);
});

test('exports preserve purchase prices and include item status, payment breakdown and wallet history', () => {
  const db = seed();
  db.products[0].current_price = 999000;
  const receipt = getReceipts(db)[0];
  const text = receiptText(receipt, db, db.accounts[0], now);
  for (const fragment of ['Điện thoại:', 'Thời gian chơi:', 'Đơn giá lúc bắt đầu:', 'Chờ xác nhận', 'Đã hủy', 'TỔNG HỢP', 'GIAO DỊCH VÍ TRONG PHIÊN', 'Số dư sau giao dịch:', money(28000)]) assert.ok(text.includes(fragment), fragment);
  assert.ok(!text.includes(money(999000)));
  assert.ok(!text.includes('undefined'));
});

test('receipt and wallet history exclude another customer even when source IDs are present', () => {
  const db = seed();
  db.gaming_sessions.push({ ...db.gaming_sessions[0], id: 5000, customer_id: 2 });
  db.orders.push({ id: 5000, customer_id: 2, gaming_session_id: 5000, total_amount: 99000 });
  db.order_items.push({ id: 5000, order_id: 5000, product_id: 1, quantity: 1, unit_price_snapshot: 99000, status: 'Confirmed' });
  db.wallet_transactions.push({ id: 5000, customer_id: 2, gaming_session_id: 1027, amount: -99000, recorded_at: now.toISOString() });
  const receipts = getReceipts(db);
  assert.equal(receipts.length, 2);
  assert.equal(receipts.reduce((sum, row) => sum + row.total, 0), 147000);
  assert.ok(receipts.every((receipt) => receipt.transactions.every((row) => row.customer_id === 1)));
});
