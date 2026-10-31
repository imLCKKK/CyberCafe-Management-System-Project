import { DEMO_ACCOUNT_ID, DEMO_CUSTOMER_ID, DEMO_SCHEMA_VERSION } from './demoData.js';
import { remainingStock } from '../ordering/recommendation.js';

export const money = (value = 0) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value);
export const dateTime = (value) => value ? new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)) : '—';
export const minutesLabel = (start, end = new Date()) => { const minutes = Math.max(0, Math.floor((new Date(end) - new Date(start)) / 60000)); return `${Math.floor(minutes / 60)} giờ ${String(minutes % 60).padStart(2, '0')} phút`; };
export const transactionLabels = { TopUpCredit: 'Nạp tiền vào ví', GamingCharge: 'Phí phiên chơi', OrderDebit: 'Thanh toán đơn món', OrderItemRefund: 'Hoàn tiền món', TournamentFee: 'Lệ phí giải đấu', TournamentRefund: 'Hoàn phí giải đấu' };
export const labels = { Pending: 'Chờ xác nhận', Confirmed: 'Đã xác nhận', Cancelled: 'Đã hủy', Approved: 'Đã duyệt', Rejected: 'Đã từ chối', CreditedUnpaid: 'Đã nạp · chờ thu tiền', Collected: 'Đã thu tiền', Active: 'Đang chơi', Ended: 'Đã kết thúc', Mixed: 'Đã xử lý một phần', Paid: 'Đã trừ ví' };
export const nextId = (rows) => Math.max(0, ...rows.map((r) => r.id)) + 1;
export function currentClient(db) {
  const customer = db.customers.find((r) => r.id === DEMO_CUSTOMER_ID);
  const account = db.accounts.find((r) => r.id === customer?.account_id);
  const session = db.gaming_sessions.find((r) => r.customer_id === customer?.id && r.status === 'Active');
  const computer = db.computers.find((r) => r.id === session?.computer_id);
  const type = db.computer_types.find((r) => r.id === computer?.computer_type_id);
  return { customer, account, session, computer, type };
}
export function isValidClientDemo(db) {
  const tables = ['accounts', 'customers', 'computer_types', 'computers', 'gaming_sessions', 'products', 'product_categories', 'orders', 'order_items', 'order_item_cancel_requests', 'top_ups', 'wallet_transactions'];
  if (!db || db.schema_version !== DEMO_SCHEMA_VERSION || !tables.every((table) => Array.isArray(db[table]) && db[table].every((row) => row && Number.isSafeInteger(row.id)) && new Set(db[table].map((row) => row.id)).size === db[table].length)) return false;
  // Stored data can be edited, truncated, or come from an older demo. Validate all
  // timestamps used by the pages before Intl.DateTimeFormat can throw at render.
  if (!tables.every((table) => db[table].every((row) => Object.entries(row).every(([field, value]) => !field.endsWith('_at') || value == null || (typeof value === 'string' && Number.isFinite(Date.parse(value))))))) return false;
  if (!db.customers.every((row) => db.accounts.some((account) => account.id === row.account_id))) return false;
  const { account, customer } = currentClient(db);
  return Boolean(account && typeof account.display_name === 'string' && typeof account.password_hash === 'string' && customer && Number.isSafeInteger(customer.wallet_balance) && customer.wallet_balance >= 0 && Number.isSafeInteger(customer.wallet_version)
    && db.products.every((r) => typeof r.name === 'string' && Number.isSafeInteger(r.current_price) && r.current_price >= 0)
    && db.gaming_sessions.every((r) => Number.isFinite(Date.parse(r.started_at)) && Number.isSafeInteger(r.charged_amount))
    && db.wallet_transactions.every((r) => Number.isSafeInteger(r.amount) && Number.isSafeInteger(r.balance_after) && r.balance_after >= 0 && Number.isFinite(Date.parse(r.recorded_at))));
}
function requireActive(db, balanceRequired = true) {
  const data = currentClient(db);
  if (!data.account?.is_active || data.account.must_change_password || !data.session) throw new Error('Cần tài khoản đang hoạt động, đã đổi mật khẩu lần đầu và có phiên chơi đang hoạt động.');
  if (balanceRequired && data.customer.wallet_balance <= 0) throw new Error('Cần ví còn số dư để thực hiện thao tác này.');
  return data;
}
function walletChange(db, amount, type, source, now, actor = null) {
  const { customer } = currentClient(db);
  const balance = customer.wallet_balance + amount;
  if (!Number.isSafeInteger(amount) || !Number.isSafeInteger(balance) || balance < 0) throw new Error('Số dư ví không đủ. Hãy nạp thêm tiền tại quầy.');
  return { ...db, customers: db.customers.map((r) => r.id === customer.id ? { ...r, wallet_balance: balance, wallet_version: r.wallet_version + 1 } : r), wallet_transactions: [...db.wallet_transactions, { id: nextId(db.wallet_transactions), customer_id: customer.id, transaction_type: type, amount, balance_after: balance, top_up_id: null, gaming_session_id: null, order_id: null, order_item_id: null, tournament_registration_id: null, recorded_by_account_id: actor, recorded_at: now.toISOString(), ...source }] };
}
export function placeClientOrder(db, cart, now = new Date()) {
  return createOrder(db, cart, 'Wallet', now);
}
export function placeCashOrder(db, cart, now = new Date()) {
  return createOrder(db, cart, 'Cash', now);
}
function createOrder(db, cart, paymentMethod, now) {
  const { customer, session } = requireActive(db, paymentMethod === 'Wallet');
  const entries = Object.entries(cart).filter(([, qty]) => qty !== 0);
  if (!entries.length) throw new Error('Hãy chọn ít nhất một món cho giỏ hàng.');
  const items = entries.map(([id, quantity]) => {
    const product = db.products.find((r) => r.id === Number(id));
    if (!product || product.is_active === false || !Number.isSafeInteger(quantity) || quantity < 1 || quantity > 99) throw new Error('Món đã ngừng bán hoặc số lượng không hợp lệ (từ 1 đến 99 phần).');
    if (quantity > remainingStock(db, product)) throw new Error(`${product.name} không còn đủ số lượng. Hãy cập nhật giỏ hàng.`);
    return { product, quantity };
  });
  const total = items.reduce((sum, { product, quantity }) => sum + product.current_price * quantity, 0);
  const id = nextId(db.orders);
  const result = paymentMethod === 'Wallet' ? walletChange(db, -total, 'OrderDebit', { order_id: id }, now) : db;
  return { ...result, products: db.products.map((product) => {
    const ordered = items.find((item) => item.product.id === product.id);
    return ordered ? { ...product, stock_quantity: remainingStock(db, product) - ordered.quantity } : product;
  }), orders: [...db.orders, { id, customer_id: customer.id, gaming_session_id: session.id, total_amount: total, placed_at: now.toISOString(), payment_method: paymentMethod, payment_status: paymentMethod === 'Cash' ? 'PendingCollection' : 'Paid' }], order_items: [...db.order_items, ...items.map(({ product, quantity }, i) => ({ id: nextId(db.order_items) + i, order_id: id, product_id: product.id, quantity, unit_price_snapshot: product.current_price, status: 'Pending', confirmed_by_account_id: null, confirmed_at: null, cancelled_by_account_id: null }))] };
}
export function requestItemCancel(db, itemId, now = new Date()) {
  const { customer, account } = currentClient(db);
  if (!account.is_active) throw new Error('Tài khoản hiện không hoạt động.');
  const item = db.order_items.find((r) => r.id === itemId);
  const order = db.orders.find((r) => r.id === item?.order_id && r.customer_id === customer.id);
  if (!order || item.status !== 'Pending') throw new Error('Chỉ gửi yêu cầu cho món đang chờ xác nhận.');
  if (db.order_item_cancel_requests.some((r) => r.order_item_id === itemId && r.status === 'Pending')) throw new Error('Yêu cầu hủy món này đang chờ nhân viên xử lý.');
  return { ...db, order_item_cancel_requests: [...db.order_item_cancel_requests, { id: nextId(db.order_item_cancel_requests), order_item_id: itemId, requested_by_customer_id: customer.id, status: 'Pending', requested_at: now.toISOString(), handled_by_account_id: null, handled_at: null }] };
}
export function requestTopUp(db, amount, now = new Date()) {
  const { customer } = requireActive(db);
  if (!Number.isSafeInteger(amount) || amount < 10000 || amount > 2000000 || amount % 1000 !== 0) throw new Error('Nhập từ 10.000đ đến 2.000.000đ, theo bội số 1.000đ.');
  if (db.top_ups.some((r) => r.customer_id === customer.id && ['Pending', 'CreditedUnpaid'].includes(r.status))) throw new Error('Hãy hoàn tất yêu cầu đang chờ hoặc khoản chưa thu tiền tại quầy trước khi nạp tiếp.');
  return { ...db, top_ups: [...db.top_ups, { id: nextId(db.top_ups), customer_id: customer.id, amount, requested_by_customer: true, status: 'Pending', requested_at: now.toISOString(), credited_by_account_id: null, credited_at: null, collected_by_account_id: null, collected_at: null, collection_method: null }] };
}
// These actions are exposed only inside the explicitly labelled demo staff simulator.
export function simulateTopUp(db, id, action, method = 'Cash', now = new Date()) {
  const row = db.top_ups.find((r) => r.id === id && r.customer_id === DEMO_CUSTOMER_ID);
  if (!row) throw new Error('Không tìm thấy yêu cầu nạp.');
  let result = db, updated;
  if (action === 'credit' && row.status === 'Pending') {
    if (db.top_ups.some((r) => r.customer_id === row.customer_id && r.status === 'CreditedUnpaid')) throw new Error('Cần thu xong khoản đã nạp trước.');
    result = walletChange(db, row.amount, 'TopUpCredit', { top_up_id: row.id }, now, 2);
    updated = { ...row, status: 'CreditedUnpaid', credited_by_account_id: 2, credited_at: now.toISOString() };
  } else if (action === 'reject' && row.status === 'Pending') updated = { ...row, status: 'Rejected' };
  else if (action === 'collect' && row.status === 'CreditedUnpaid' && ['Cash', 'BankTransfer'].includes(method)) updated = { ...row, status: 'Collected', collected_by_account_id: 2, collected_at: now.toISOString(), collection_method: method };
  else throw new Error('Yêu cầu đã được xử lý hoặc thao tác không hợp lệ.');
  return { ...result, top_ups: db.top_ups.map((r) => r.id === id ? updated : r) };
}
export function orderStatus(db, id) {
  const items = db.order_items.filter((r) => r.order_id === id);
  return items.every((r) => r.status === 'Cancelled') ? 'Cancelled' : items.every((r) => r.status !== 'Pending') ? 'Confirmed' : items.some((r) => r.status !== 'Pending') ? 'Mixed' : 'Pending';
}
export function getReceipts(db, customerId = DEMO_CUSTOMER_ID) {
  return db.gaming_sessions.filter((r) => r.customer_id === customerId).map((session) => {
    const orders = db.orders.filter((r) => r.gaming_session_id === session.id && r.customer_id === customerId);
    const items = db.order_items.filter((r) => orders.some((o) => o.id === r.order_id));
    const transactions = db.wallet_transactions.filter((row) => row.customer_id === customerId && (
      row.gaming_session_id === session.id || orders.some((order) => order.id === row.order_id) || items.some((item) => item.id === row.order_item_id)
    )).sort((a, b) => new Date(a.recorded_at) - new Date(b.recorded_at) || a.id - b.id);
    const orderDetails = orders.map((order) => {
      const lines = items.filter((item) => item.order_id === order.id);
      const cash = order.payment_method === 'Cash';
      const cancelled = lines.filter((item) => item.status === 'Cancelled').reduce((sum, item) => sum + item.quantity * item.unit_price_snapshot, 0);
      const refunds = transactions.filter((row) => row.transaction_type === 'OrderItemRefund' && lines.some((item) => item.id === row.order_item_id)).reduce((sum, row) => sum + row.amount, 0);
      const net = order.total_amount - refunds - (cash ? cancelled : 0);
      return { ...order, items: lines, cash, cancelled, refunds, net,
        refundPending: cash ? 0 : Math.max(0, cancelled - refunds),
        walletPaid: cash ? 0 : net,
        cashPaid: cash && order.payment_status === 'Paid' ? net : 0,
        cashDue: cash && order.payment_status !== 'Paid' ? net : 0,
      };
    }).sort((a, b) => new Date(a.placed_at) - new Date(b.placed_at) || a.id - b.id);
    const sum = (key) => orderDetails.reduce((total, order) => total + order[key], 0);
    const foodGross = sum('total_amount');
    const refunds = sum('refunds');
    const cashCancelled = orderDetails.filter((order) => order.cash).reduce((total, order) => total + order.cancelled, 0);
    const food = sum('net');
    const walletPaid = session.charged_amount + sum('walletPaid');
    const cashPaid = sum('cashPaid');
    const cashDue = sum('cashDue');
    const paidTotal = walletPaid + cashPaid;
    const times = [session.started_at, session.last_charged_at, session.ended_at, ...orders.map((order) => order.placed_at), ...transactions.map((row) => row.recorded_at), ...items.map((item) => item.cancelled_at)].filter((value) => value && Number.isFinite(Date.parse(value)));
    const updatedAt = times.sort((a, b) => new Date(b) - new Date(a))[0];
    return { id: session.id, session, orders, orderDetails, items, transactions, foodGross, refunds, cashCancelled, food, walletPaid, cashPaid, cashDue, paidTotal,
      refundPending: sum('refundPending'), paymentState: cashDue > 0 ? (paidTotal > 0 ? 'Partial' : 'Unpaid') : 'Paid',
      total: session.charged_amount + food, updatedAt, computer: db.computers.find((r) => r.id === session.computer_id) };
  }).sort((a, b) => new Date(b.session.started_at) - new Date(a.session.started_at));
}
export function renameAccount(db, displayName) {
  const name = displayName.trim();
  if (!name || name.length > 100 || /[\u0000-\u001f]/u.test(name)) throw new Error('Tên hiển thị cần từ 1 đến 100 ký tự hợp lệ.');
  return { ...db, accounts: db.accounts.map((r) => r.id === DEMO_ACCOUNT_ID ? { ...r, display_name: name } : r) };
}
async function hashPassword(password, salt, iterations) {
  const bytes = new TextEncoder();
  const key = await crypto.subtle.importKey('raw', bytes.encode(password), 'PBKDF2', false, ['deriveBits']);
  const bits = await crypto.subtle.deriveBits({ name: 'PBKDF2', hash: 'SHA-256', salt: bytes.encode(salt), iterations }, key, 256);
  return [...new Uint8Array(bits)].map((v) => v.toString(16).padStart(2, '0')).join('');
}
export async function preparePasswordChange(db, currentPassword, newPassword, confirm) {
  const { account } = currentClient(db);
  if (!account.is_active) throw new Error('Tài khoản hiện không hoạt động.');
  if (newPassword.length < 8 || newPassword.length > 128) throw new Error('Mật khẩu mới cần từ 8 đến 128 ký tự.');
  if (newPassword !== confirm) throw new Error('Hai mật khẩu mới chưa khớp.');
  if (newPassword === currentPassword) throw new Error('Hãy chọn mật khẩu mới khác mật khẩu hiện tại.');
  const [algorithm, count, salt, expected] = account.password_hash.split(':');
  if (algorithm !== 'pbkdf2' || await hashPassword(currentPassword, salt, Number(count)) !== expected) throw new Error('Mật khẩu hiện tại chưa đúng.');
  const newSalt = [...crypto.getRandomValues(new Uint8Array(16))].map((v) => v.toString(16).padStart(2, '0')).join('');
  const passwordHash = `pbkdf2:100000:${newSalt}:${await hashPassword(newPassword, newSalt, 100000)}`;
  return (latest) => {
    if (currentClient(latest).account.password_hash !== account.password_hash) throw new Error('Mật khẩu vừa thay đổi ở tab khác. Hãy thử lại.');
    return { ...latest, accounts: latest.accounts.map((r) => r.id === account.id ? { ...r, password_hash: passwordHash, must_change_password: false } : r) };
  };
}
