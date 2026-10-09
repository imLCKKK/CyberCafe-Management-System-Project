import test from 'node:test';
import assert from 'node:assert/strict';
import { createDemoData } from './mockData.js';
import { depositDemo, duration, getActivities, getHomeSummary, isValidHomeData, placeOrder, registerTournament } from './homeModel.js';

const now = new Date('2026-10-08T09:15:00.000Z');
const fresh = () => createDemoData(now);
const form = { rankId: '4', role: 'Flex', riotId: ' Minh Anh #VN1 ' };

test('joins the current customer, session, room and computer type', () => {
  const summary = getHomeSummary(fresh(), now);
  assert.equal(summary.customer.FullName, 'Nguyễn Minh Anh');
  assert.equal(summary.computer.ComputerId, 12);
  assert.equal(summary.room.Floor, 2);
  assert.equal(summary.type.PricePerHour, 12000);
  assert.equal(duration(summary.elapsed), '02:15:00');
  assert.equal(summary.sessionCost, 27000);
  assert.equal(summary.orderTotal, 43000);
  assert.equal(summary.total, 70000);
});

test('estimates only the uncharged interval without altering wallet or ledger', () => {
  const db = fresh();
  const before = JSON.stringify(db);
  const later = new Date(now.getTime() + 3600000);
  assert.equal(getHomeSummary(db, later).sessionCost, 39000);
  assert.equal(JSON.stringify(db), before);
});

test('excludes cancelled orders and paid invoices from unpaid orders', () => {
  const db = fresh();
  db.Orders.push({ ...db.Orders[0], OrderId: 2000, Status: 'Cancelled', TotalAmount: 100000 });
  db.Orders[0].InvoiceId = 1026;
  const summary = getHomeSummary(db, now);
  assert.equal(summary.orderTotal, 43000);
  assert.equal(summary.unpaidOrders, 0);
});

test('no active session produces no running costs and prevents ordering', () => {
  const db = fresh();
  db.GamingSessions[0].Status = 'Completed';
  assert.equal(getHomeSummary(db, now).total, 0);
  assert.equal(getHomeSummary(db, now).remainingHours, null);
  assert.throws(() => placeOrder(db, { 1: 1 }, now), /phiên chơi/);
});

test('creates related order, detail, history and inventory records atomically', () => {
  const db = fresh();
  const before = JSON.stringify(db);
  const next = placeOrder(db, { 1: 2, 3: 1 }, now);
  const order = next.Orders.at(-1);
  assert.equal(order.TotalAmount, 71000);
  assert.equal(order.Status, 'Pending');
  assert.equal(order.SessionId, 1027);
  assert.equal(next.Products[0].Stock, 18);
  assert.equal(next.Products[2].Stock, 29);
  assert.equal(next.OrderDetails.filter((row) => row.OrderId === order.OrderId).length, 2);
  assert.equal(next.OrderStatusHistory.at(-1).OrderId, order.OrderId);
  assert.equal(next.InventoryTransactions.at(-1).ReferenceId, order.OrderId);
  assert.equal(next.InventoryTransactions.at(-1).Quantity, -1);
  assert.equal(next.Customers[0].WalletBalance, 153000);
  assert.equal(JSON.stringify(db), before);
});

test('rejects empty, missing, sold out, excessive, negative and fractional quantities', () => {
  for (const cart of [{}, { 99: 1 }, { 6: 1 }, { 1: 21 }, { 1: -1 }, { 1: 1.5 }, { 1: '2' }]) {
    assert.throws(() => placeOrder(fresh(), cart, now));
  }
});

test('inactive products and categories cannot be ordered', () => {
  const db = fresh();
  db.Categories[0].IsActive = false;
  assert.throws(() => placeOrder(db, { 1: 1 }, now));
  db.Products[2].IsActive = false;
  assert.throws(() => placeOrder(db, { 3: 1 }, now));
});

test('deposit updates wallet and balance-after ledger together', () => {
  const db = fresh();
  const next = depositDemo(db, 100000, now);
  assert.equal(next.Customers[0].WalletBalance, 253000);
  assert.equal(next.WalletTransactions.at(-1).BalanceAfter, 253000);
  assert.equal(next.WalletTransactions.at(-1).Amount, 100000);
  assert.equal(db.Customers[0].WalletBalance, 153000);
  for (const value of [0, -1, 9999, 2000001, 10001, NaN, Infinity, '100000']) assert.throws(() => depositDemo(db, value, now));
});

test('registration saves Riot ID without inventing tracker statistics, ML scores or teams', () => {
  const next = registerTournament(fresh(), 1, form, now);
  const registration = next.TournamentRegistrations[0];
  assert.equal(registration.RankId, 4);
  assert.equal(registration.RiotId, 'Minh Anh#VN1');
  assert.equal(registration.WinRate, null);
  assert.equal(registration.HoursPlayed, null);
  assert.equal(registration.SkillScore, null);
  assert.equal(next.Teams.length, 0);
  assert.throws(() => registerTournament(next, 1, form, now), /đã đăng ký/);
});

test('registration rejects closed windows, a different game rank and invalid Riot IDs', () => {
  const db = fresh();
  assert.throws(() => registerTournament(db, 1, form, new Date(db.Tournaments[0].RegistrationEnd)));
  assert.throws(() => registerTournament(db, 1, form, new Date('2020-01-01')));
  db.GameRanks[3].GameId = 999;
  assert.throws(() => registerTournament(db, 1, form, now));
  for (const invalid of [{ riotId: '' }, { riotId: 'Player' }, { riotId: '#VN1' }, { riotId: 'Player#' }, { riotId: 'Player#VN#1' }, { riotId: 'Player#VN 1' }, { riotId: 'Player\n#VN1' }, { riotId: null }, { role: 'unknown' }]) assert.throws(() => registerTournament(fresh(), 1, { ...form, ...invalid }, now));
});

test('paid tournaments require counter handling, never silently debit wallet', () => {
  const db = fresh();
  db.Tournaments[0].EntryFee = 50000;
  assert.throws(() => registerTournament(db, 1, form, now), /lệ phí/);
  assert.equal(db.Customers[0].WalletBalance, 153000);
});

test('blocked accounts cannot place orders, deposit or register', () => {
  const db = fresh();
  db.Customers[0].Status = 'Blocked';
  assert.throws(() => placeOrder(db, { 1: 1 }, now));
  assert.throws(() => depositDemo(db, 50000, now));
  assert.throws(() => registerTournament(db, 1, form, now));
});

test('activities are sorted newest first and scoped to the customer', () => {
  const db = fresh();
  db.Orders.push({ ...db.Orders[0], CustomerId: 2, OrderId: 9999 });
  const rows = getActivities(db);
  assert.deepEqual(rows.map((r) => r.kind), ['order', 'wallet', 'invoice']);
  assert.equal(rows.some((r) => r.id === 9999), false);
});

test('all demo mutations survive JSON storage round-trip including nullable history', () => {
  let db = fresh();
  assert.equal(isValidHomeData(db), true);
  db = placeOrder(db, { 1: 1 }, now);
  db = depositDemo(db, 50000, now);
  db = registerTournament(db, 1, form, now);
  assert.equal(isValidHomeData(JSON.parse(JSON.stringify(db))), true);
});

test('malformed stored data is rejected before rendering', () => {
  for (const invalid of [null, {}, [], { ...fresh(), Orders: null }, { ...fresh(), Customers: [] }]) assert.equal(isValidHomeData(invalid), false);
  const db = fresh();
  db.GamingSessions[0].StartTime = 'not-a-date';
  assert.equal(isValidHomeData(db), false);
  db.GamingSessions[0].StartTime = now.toISOString();
  db.Products[0].Stock = -1;
  assert.equal(isValidHomeData(db), false);
});
