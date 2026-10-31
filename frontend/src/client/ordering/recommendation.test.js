import test from 'node:test';
import assert from 'node:assert/strict';
import { createClientDemo } from '../shared/demoData.js';
import { currentClient, getReceipts, placeCashOrder, placeClientOrder } from '../shared/demoModel.js';
import { pickWeightedProduct, remainingStock } from './recommendation.js';

test('recommendation uses remaining portions as weights and excludes sold out items', () => {
  const db = createClientDemo();
  db.products = db.products.slice(0, 3).map((product, index) => ({ ...product, stock_quantity: [1, 3, 0][index] }));
  assert.equal(pickWeightedProduct(db, 'all', () => 0)?.id, 1);
  assert.equal(pickWeightedProduct(db, 'all', () => .249)?.id, 1);
  assert.equal(pickWeightedProduct(db, 'all', () => .25)?.id, 2);
  assert.equal(pickWeightedProduct(db, 'all', () => .999)?.id, 2);
  assert.equal(pickWeightedProduct(db, 2, () => .5), null);
});

test('ordering reduces stock and refuses more portions than remain', () => {
  const db = createClientDemo();
  const result = placeClientOrder(db, { 7: 2 });
  assert.equal(remainingStock(result, result.products.find((product) => product.id === 7)), 3);
  assert.equal(remainingStock(db, db.products.find((product) => product.id === 7)), 5);
  assert.throws(() => placeClientOrder(result, { 7: 4 }), /không còn đủ/);
});

test('older saved demo products derive stock without dropping existing orders', () => {
  const db = createClientDemo();
  db.products = db.products.map(({ stock_quantity, ...product }) => product);
  const result = placeClientOrder(db, { 7: 2 });
  assert.equal(remainingStock(result, result.products.find((product) => product.id === 7)), 3);
  assert.equal(remainingStock(result, result.products.find((product) => product.id === 1)), 16);
});

test('cash checkout reserves stock and records pending collection without debiting the wallet', () => {
  const db = createClientDemo();
  const copy = structuredClone(db);
  const result = placeCashOrder(db, { 1: 2 });
  assert.equal(result.orders.at(-1).payment_method, 'Cash');
  assert.equal(result.orders.at(-1).payment_status, 'PendingCollection');
  assert.equal(result.orders.at(-1).total_amount, 56000);
  assert.equal(remainingStock(result, result.products[0]), 14);
  assert.deepEqual(result.wallet_transactions, db.wallet_transactions);
  assert.deepEqual(currentClient(result).customer, currentClient(db).customer);
  assert.deepEqual(db, copy);
  const receipt = getReceipts(result).find((row) => row.id === currentClient(db).session.id);
  assert.equal(receipt.cashDue, 56000);
  assert.equal(receipt.total, getReceipts(db)[0].total + 56000);
  result.order_items.at(-1).status = 'Cancelled';
  assert.equal(getReceipts(result)[0].cashDue, 0);
  assert.equal(getReceipts(result)[0].total, getReceipts(db)[0].total);
});

test('cash checkout works without wallet funds and rechecks availability and session', () => {
  const db = createClientDemo();
  db.customers[0].wallet_balance = 0;
  assert.equal(placeCashOrder(db, { 1: 1 }).orders.at(-1).total_amount, 28000);
  assert.throws(() => placeCashOrder(db, { 7: 6 }), /không còn đủ/);
  db.gaming_sessions[0].status = 'Ended';
  assert.throws(() => placeCashOrder(db, { 1: 1 }), /phiên chơi/);
});
