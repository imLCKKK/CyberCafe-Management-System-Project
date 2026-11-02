import test from 'node:test';
import assert from 'node:assert/strict';
import { createClientDemo } from '../../client/shared/demoData.js';
import { createDemoData } from '../../client/home/mockData.js';
import { isValidHomeData } from '../../client/home/homeModel.js';
import { getReceipts, placeCashOrder, placeClientOrder, requestItemCancel } from '../../client/shared/demoModel.js';
import { pickWeightedProduct, remainingStock } from '../../client/ordering/recommendation.js';
import { ensureAdminData } from './adminData.js';
import { addDays, allReceipts, collectCashOrder, createTopUp, csvText, dayKey, hashInitialPassword, moveInventory, processOrderItem, processTopUp, rejectCancellation, reportData, saveAccount, saveCategory, saveProduct, toggleAccount } from './adminModel.js';
import { generateTeams, registerAdminParticipant, saveTournament, transitionTournament } from './tournamentAdminModel.js';

const now = new Date('2026-10-21T14:00:00+07:00');
const seed = () => ensureAdminData(createClientDemo(now), now);
const range = { from: '2026-10-01', to: '2026-10-21' };
function assertWallets(db) {
  for (const customer of db.customers) {
    let balance = 0;
    const rows = db.wallet_transactions.filter((row) => row.customer_id === customer.id).sort((a, b) => Date.parse(a.recorded_at) - Date.parse(b.recorded_at) || a.id - b.id);
    for (const row of rows) { balance += row.amount; assert.equal(row.balance_after, balance, `Customer ${customer.id}, transaction ${row.id}`); }
    assert.equal(customer.wallet_balance, balance);
  }
}

test('admin initialization preserves client records, is idempotent, and all seeded wallets reconcile', () => {
  const original = createClientDemo(now), copy = structuredClone(original);
  const db = ensureAdminData(original, now);
  assert.deepEqual(original, copy);
  assert.deepEqual(db.orders.find((row) => row.id === 1048), original.orders[0]);
  assert.equal(db.customers[0].wallet_balance, 153000);
  assert.equal(db.customers.length, 5);
  assert.equal(ensureAdminData(db, now), db);
  assertWallets(db);
  assert.equal(allReceipts(db).length, db.gaming_sessions.length);
  assert.ok(allReceipts(db).every((receipt) => receipt.paidTotal + receipt.cashDue === receipt.total));
});

test('wallet cancellation refunds the price snapshot once and restores reserved stock atomically', () => {
  let db = seed();
  db = requestItemCancel(db, 2, now);
  const before = structuredClone(db), stock = remainingStock(db, db.products[2]);
  db.products[2].current_price = 99000;
  const result = processOrderItem(db, 2, 'cancel', 'Khách đổi ý', now);
  assert.equal(result.customers[0].wallet_balance, before.customers[0].wallet_balance + 15000);
  assert.equal(remainingStock(result, result.products[2]), stock + 1);
  assert.equal(result.order_item_cancel_requests.find((row) => row.order_item_id === 2).status, 'Approved');
  assert.equal(result.inventory_movements.at(-1).type, 'Return');
  assert.equal(db.order_items[1].status, 'Pending');
  assert.throws(() => processOrderItem(result, 2, 'cancel', 'Lặp lại', now));
  assert.throws(() => processOrderItem(db, 1, 'cancel', 'Món đã xác nhận', now));
  assertWallets(result);
});

test('cash confirmation and collection do not change wallet and cannot be collected twice', () => {
  let db = placeCashOrder(seed(), { 1: 1 }, now);
  const order = db.orders.at(-1), item = db.order_items.at(-1);
  const prior = reportData(db, range).total;
  assert.throws(() => collectCashOrder(db, order.id, now), /Xác nhận/);
  db = processOrderItem(db, item.id, 'confirm', '', now);
  const result = collectCashOrder(db, order.id, now);
  assert.equal(result.customers[0].wallet_balance, db.customers[0].wallet_balance);
  assert.equal(result.wallet_transactions.length, db.wallet_transactions.length);
  assert.equal(result.orders.at(-1).payment_status, 'Paid');
  assert.equal(reportData(result, range).total, prior + 28000);
  assert.equal(getReceipts(result)[0].cashDue, 0);
  assert.throws(() => collectCashOrder(result, order.id, now));
});

test('cancelling pending cash removes the obligation without refunding wallet', () => {
  const db = placeCashOrder(seed(), { 1: 1 }, now);
  const result = processOrderItem(db, db.order_items.at(-1).id, 'cancel', 'Hết hàng', now);
  assert.deepEqual(result.wallet_transactions, db.wallet_transactions);
  assert.equal(getReceipts(result)[0].cashDue, 0);
  assert.throws(() => collectCashOrder(result, db.orders.at(-1).id, now));
});

test('cancellation rejection is one time and an uncharged order cannot mint a wallet refund', () => {
  let db = requestItemCancel(seed(), 2, now);
  const request = db.order_item_cancel_requests.at(-1);
  const rejected = rejectCancellation(db, request.id, now);
  assert.equal(rejected.order_items.find((row) => row.id === 2).status, 'Pending');
  assert.deepEqual(rejected.wallet_transactions, db.wallet_transactions);
  assert.throws(() => rejectCancellation(rejected, request.id, now));
  db.wallet_transactions = db.wallet_transactions.filter((row) => row.order_id !== 1048);
  assert.throws(() => processOrderItem(db, 2, 'cancel', 'Không có giao dịch gốc', now), /Chưa có giao dịch/);
});

test('topup credits once, collection never credits again, and deposits do not count as revenue', () => {
  let db = seed();
  const revenue = reportData(db, range).total, balance = db.customers[1].wallet_balance;
  db = createTopUp(db, db.customers[1].id, 100000, now);
  const id = db.top_ups.at(-1).id;
  assert.throws(() => createTopUp(db, db.customers[1].id, 100000, now));
  db = processTopUp(db, id, 'credit', 'Cash', now);
  assert.equal(db.customers[1].wallet_balance, balance + 100000);
  assert.throws(() => processTopUp(db, id, 'credit', 'Cash', now));
  const result = processTopUp(db, id, 'collect', 'BankTransfer', now);
  assert.equal(result.customers[1].wallet_balance, balance + 100000);
  assert.equal(result.wallet_transactions.length, db.wallet_transactions.length);
  assert.throws(() => processTopUp(result, id, 'collect', 'Cash', now));
  assert.equal(reportData(result, range).total, revenue);
  assertWallets(result);
});

test('stock receives, outflow and count adjustments preserve history and forbid negative stock', () => {
  let db = seed();
  const before = remainingStock(db, db.products[0]);
  db = moveInventory(db, { product_id: 1, type: 'In', quantity: 5, unit_cost: 10000, reason: 'Nhà cung cấp A' }, now);
  assert.equal(remainingStock(db, db.products[0]), before + 5);
  const snapshot = structuredClone(db.inventory_movements[0]);
  assert.throws(() => moveInventory(db, { product_id: 1, type: 'Out', quantity: 9999, reason: 'Hao hụt' }, now));
  db = moveInventory(db, { product_id: 1, type: 'Adjust', quantity: 0, reason: 'Kiểm kê hết hàng' }, now);
  assert.equal(remainingStock(db, db.products[0]), 0);
  assert.deepEqual(db.inventory_movements[0], snapshot);
  assert.equal(reportData(db, range).purchases, 50000);
  assert.throws(() => moveInventory(db, { product_id: 1, type: 'Adjust', quantity: -1, reason: 'Sai' }, now));
});

test('archived products cannot be ordered or drawn and editing price preserves history', () => {
  const db = seed(), product = db.products[0];
  const updated = saveProduct(db, { ...product, current_price: 99000, is_active: false }, now);
  assert.equal(updated.order_items[0].unit_price_snapshot, 28000);
  assert.throws(() => placeClientOrder(updated, { 1: 1 }, now));
  const only = { ...updated, products: [updated.products[0]] };
  assert.equal(pickWeightedProduct(only), null);
  assert.throws(() => saveProduct(db, { ...product, name: db.products[1].name }, now));
  assert.throws(() => saveCategory(db, { name: db.product_categories[0].name }, now));
});

test('account creation hashes passwords and protects duplicates, active sessions, and the last admin', async () => {
  const db = seed();
  const hash = await hashInitialPassword('0934567890');
  const result = saveAccount(db, { display_name: 'Khách mới', phone: '0934567890' }, 'customer', hash, now);
  assert.equal(result.accounts.at(-1).must_change_password, true);
  assert.ok(!JSON.stringify(result).includes('"password":"0934567890"'));
  assert.equal(result.customers.at(-1).wallet_balance, 0);
  assert.throws(() => saveAccount(result, { display_name: 'Trùng', phone: '0934567890' }, 'customer', hash, now));
  assert.throws(() => toggleAccount(db, db.accounts.find((row) => row.role === 'Admin').id, now));
  assert.throws(() => toggleAccount(db, db.customers[0].account_id, now));
  assert.throws(() => saveAccount(db, { display_name: 'Sai vai trò', phone: '0934567891', role: 'Customer' }, 'staff', hash, now));
});

test('reports reconcile every bucket, handle empty ranges, zero occupancy, and Vietnam date boundaries', () => {
  const db = seed(), report = reportData(db, range, []);
  assert.equal(report.total, report.gaming + report.food);
  assert.equal(report.total, report.timeline.reduce((sum, row) => sum + row.total, 0));
  assert.equal(report.occupancy, 0);
  assert.equal(dayKey('2026-10-20T18:30:00Z'), '2026-10-21');
  assert.equal(addDays('2026-10-21', -1), '2026-10-20');
  assert.equal(reportData(db, { from: '2030-01-01', to: '2030-01-07' }).total, 0);
  assert.equal(reportData(db, { from: '2030-01-01', to: '2030-01-07' }).timeline.length, 7);
  assert.throws(() => reportData(db, { from: '2026-02-31', to: '2026-03-05' }));
  assert.throws(() => reportData(db, { from: '2026-10-22', to: '2026-10-21' }));
  assert.throws(() => reportData(db, { from: '2025-01-01', to: '2026-10-21' }));
});

test('refund is recognized on the refund day, not retroactively on the order day', () => {
  const db = seed(), refundTime = new Date('2026-10-22T09:00:00+07:00');
  const result = processOrderItem(db, 2, 'cancel', 'Khách hủy sau kỳ báo cáo', refundTime);
  assert.equal(reportData(result, { from: '2026-10-22', to: '2026-10-22' }).food, -15000);
  assert.equal(reportData(result, { from: '2026-10-21', to: '2026-10-21' }).food, reportData(db, { from: '2026-10-21', to: '2026-10-21' }).food);
});

test('CSV escapes spreadsheet formulas, quotes and line breaks while retaining numeric values', () => {
  const result = csvText(['Tên', 'Tiền'], [['=HYPERLINK("x")', -100], ['Mì, "bò"\nngon', 5000]]);
  assert.ok(result.startsWith('\uFEFF'));
  assert.ok(result.includes('"\'=HYPERLINK(""x"")"'));
  assert.ok(result.includes('"-100"'));
  assert.ok(result.includes('"Mì, ""bò""\nngon"'));
});

function tournamentSeed(count = 10) {
  const db = createDemoData(now);
  db.Tournaments[0].Status = 'Closed';
  db.Customers = Array.from({ length: count }, (_, index) => ({ ...db.Customers[0], CustomerId: index + 1, Phone: `09888888${String(index).padStart(2, '0')}` }));
  db.TournamentRegistrations = Array.from({ length: count }, (_, index) => ({ RegistrationId: index + 1, TournamentId: 1, CustomerId: index + 1, RankId: index % 9 + 1, PreferredRole: 'Flex', RiotId: `Player${index}#VN`, WinRate: null, HoursPlayed: null, SkillScore: null, Status: 'Registered', RegisteredAt: now.toISOString() }));
  for (const row of db.TournamentRegistrations) row.OpggSnapshot = { riotId: row.RiotId, source: 'opgg', queue: 'competitive', scope: 'V26 ACT5 · Competitive', fetchedAt: now.toISOString(), sourceUpdatedAt: null, rank: { tier: 14, name: 'Gold', division: 3, label: 'Gold 3' }, matches: 0, winRate: null, headshotRate: null, damagePerRound: null, kda: null };
  return db;
}

test('team generation assigns everyone once into full teams and preserves valid client data', () => {
  const db = tournamentSeed(), copy = structuredClone(db);
  const result = generateTeams(db, 1);
  assert.equal(result.Teams.length, 2);
  assert.equal(new Set(result.TeamMembers.map((row) => row.RegistrationId)).size, 10);
  for (const team of result.Teams) {
    assert.equal(result.TeamMembers.filter((row) => row.TeamId === team.TeamId).length, 5);
    assert.equal(team.AvgSkillScore, 14, 'Uses OP.GG tier instead of the different declared RankIds');
  }
  assert.ok(isValidHomeData(result));
  assert.deepEqual(db, copy);
  assert.throws(() => generateTeams(result, 1));
  assert.throws(() => generateTeams(tournamentSeed(11), 1));
  assert.throws(() => transitionTournament(result, 1, 'Open'));
  const pending = tournamentSeed(); pending.TournamentRegistrations[0].OpggSnapshot = null;
  assert.throws(() => generateTeams(pending, 1), /đồng bộ rank OP.GG/);
});

test('tournament dates use explicit Vietnam time and reject invalid schedules', () => {
  const db = createDemoData(now);
  const form = { TournamentName: 'Giải mới', GameId: 1, RegistrationStart: '2026-10-22T08:00', RegistrationEnd: '2026-10-23T08:00', StartTime: '2026-10-24T08:00' };
  const result = saveTournament(db, form);
  assert.equal(result.Tournaments.at(-1).RegistrationStart, '2026-10-22T01:00:00.000Z');
  assert.equal(result.Tournaments.at(-1).Status, 'Draft');
  assert.ok(isValidHomeData(result));
  assert.throws(() => saveTournament(db, { ...form, StartTime: '2026-10-21T08:00' }));
});

test('admin registration shares the legacy tournament data, preserves wallets and rejects duplicate players', () => {
  const common = seed(), legacy = createDemoData(now);
  const customer = common.customers[1], account = common.accounts.find((row) => row.id === customer.account_id);
  const form = { riotId: 'TestPlayer#VN', rankId: 4, role: 'Flex' };
  const result = registerAdminParticipant(legacy, 1, account, customer, form, now);
  assert.ok(isValidHomeData(result));
  assert.equal(result.Customers[0].WalletBalance, legacy.Customers[0].WalletBalance);
  assert.equal(common.customers[1].wallet_balance, customer.wallet_balance);
  assert.equal(result.TournamentRegistrations[0].CustomerId, result.Customers.find((row) => row.Phone === account.phone).CustomerId);
  assert.throws(() => registerAdminParticipant(result, 1, account, customer, { ...form, riotId: 'Other#VN' }, now));
  assert.throws(() => registerAdminParticipant(result, 1, common.accounts[0], common.customers[0], form, now));
  const closed = transitionTournament(result, 1, 'Closed');
  assert.throws(() => registerAdminParticipant(closed, 1, account, customer, form, now));
});

test('cash revenue uses collection time rather than order time across reporting periods', () => {
  let db = placeCashOrder(seed(), { 1: 1 }, now);
  const orderId = db.orders.at(-1).id;
  db = processOrderItem(db, db.order_items.at(-1).id, 'confirm', '', now);
  const before = reportData(db, { from: '2026-10-21', to: '2026-10-21' }).food;
  const result = collectCashOrder(db, orderId, new Date('2026-10-22T00:30:00+07:00'));
  assert.equal(reportData(result, { from: '2026-10-21', to: '2026-10-21' }).food, before);
  assert.equal(reportData(result, { from: '2026-10-22', to: '2026-10-22' }).food, 28000);
  assert.equal(reportData(result, { from: '2026-10-22', to: '2026-10-22' }).topProducts[0].quantity, 1);
});
