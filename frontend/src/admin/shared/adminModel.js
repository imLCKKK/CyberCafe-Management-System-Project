import { getReceipts, nextId } from '../../client/shared/demoModel.js';
import { remainingStock } from '../../client/ordering/recommendation.js';

export { money, dateTime } from '../../client/shared/demoModel.js';
export const normalize = (value) => String(value ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase().trim();
export const matches = (query, ...values) => normalize(values.join(' ')).includes(normalize(query));
const actor = (db) => db.accounts.find((row) => row.role === 'Admin' && row.is_active)?.id;
function check(condition, message) { if (!condition) throw new Error(message); }
function integer(value, min = 0, max = Number.MAX_SAFE_INTEGER) { return Number.isSafeInteger(Number(value)) && String(value).trim() !== '' && Number(value) >= min && Number(value) <= max; }
function text(value, max = 100) { return typeof value === 'string' && value.trim().length > 0 && value.trim().length <= max && !/[\u0000-\u001f]/.test(value); }
function audited(db, action, subject, now) { return { ...db, audit_log: [...(db.audit_log || []), { id: nextId(db.audit_log || []), action, subject: String(subject), account_id: actor(db), recorded_at: now.toISOString() }] }; }

function changeWallet(db, customerId, amount, type, source, now) {
  const customer = db.customers.find((row) => row.id === customerId);
  check(customer && Number.isSafeInteger(amount) && customer.wallet_balance + amount >= 0, 'Số dư ví không hợp lệ hoặc không đủ.');
  const balance = customer.wallet_balance + amount;
  check(Number.isSafeInteger(balance), 'Số dư vượt giới hạn số nguyên an toàn.');
  return { ...db, customers: db.customers.map((row) => row.id === customerId ? { ...row, wallet_balance: balance, wallet_version: row.wallet_version + 1 } : row), wallet_transactions: [...db.wallet_transactions, { id: nextId(db.wallet_transactions), customer_id: customerId, amount, balance_after: balance, transaction_type: type, top_up_id: null, order_id: null, order_item_id: null, gaming_session_id: null, tournament_registration_id: null, ...source, recorded_at: now.toISOString(), recorded_by_account_id: actor(db) }] };
}

export async function hashInitialPassword(password) {
  check(typeof password === 'string' && password.length >= 8 && password.length <= 128, 'Mật khẩu ban đầu cần 8–128 ký tự.');
  const salt = [...crypto.getRandomValues(new Uint8Array(16))].map((value) => value.toString(16).padStart(2, '0')).join('');
  const encoder = new TextEncoder();
  const key = await crypto.subtle.importKey('raw', encoder.encode(password), 'PBKDF2', false, ['deriveBits']);
  const bits = await crypto.subtle.deriveBits({ name: 'PBKDF2', hash: 'SHA-256', salt: encoder.encode(salt), iterations: 100000 }, key, 256);
  return `pbkdf2:100000:${salt}:${[...new Uint8Array(bits)].map((value) => value.toString(16).padStart(2, '0')).join('')}`;
}

export function saveAccount(db, form, kind, passwordHash, now = new Date()) {
  const existing = db.accounts.find((row) => row.id === Number(form.id));
  const role = kind === 'customer' ? 'Customer' : form.role;
  check(kind === 'customer' ? role === 'Customer' : kind === 'staff' && ['Staff', 'Admin'].includes(role), 'Vai trò không hợp lệ.');
  check(!existing || (kind === 'customer') === (existing.role === 'Customer'), 'Không thể đổi tài khoản khách thành nhân viên hoặc ngược lại.');
  check(text(form.display_name) && /^0\d{9,10}$/.test(form.phone), 'Nhập tên tối đa 100 ký tự và số điện thoại 10–11 số bắt đầu bằng 0.');
  check(!db.accounts.some((row) => row.id !== existing?.id && row.phone === form.phone), 'Số điện thoại đã được sử dụng.');
  if (existing?.role === 'Admin' && role !== 'Admin') check(db.accounts.some((row) => row.id !== existing.id && row.role === 'Admin' && row.is_active), 'Phải giữ ít nhất một quản trị viên đang hoạt động.');
  check(existing || passwordHash?.startsWith('pbkdf2:'), 'Chưa thiết lập mật khẩu ban đầu.');
  const id = existing?.id || nextId(db.accounts);
  const record = { ...existing, id, phone: form.phone, display_name: form.display_name.trim(), role, password_hash: passwordHash || existing?.password_hash, is_active: existing?.is_active ?? true, must_change_password: existing?.must_change_password ?? true, created_at: existing?.created_at || now.toISOString(), created_by_account_id: existing?.created_by_account_id ?? actor(db) };
  let result = { ...db, accounts: existing ? db.accounts.map((row) => row.id === id ? record : row) : [...db.accounts, record] };
  if (!existing && role === 'Customer') result.customers = [...db.customers, { id: nextId(db.customers), account_id: id, wallet_balance: 0, wallet_version: 0 }];
  if (kind === 'staff') {
    check(text(form.position || 'Nhân viên') && text(form.shift || 'Linh hoạt'), 'Chức danh hoặc ca làm không hợp lệ.');
    const profile = { id, account_id: id, position: form.position || 'Nhân viên', shift: form.shift || 'Linh hoạt', joined_at: existing?.created_at || now.toISOString() };
    result.staff_profiles = [...(db.staff_profiles || []).filter((row) => row.account_id !== id), profile];
  }
  return audited(result, existing ? 'Cập nhật tài khoản' : 'Tạo tài khoản', record.display_name, now);
}

export function toggleAccount(db, accountId, now = new Date()) {
  const account = db.accounts.find((row) => row.id === accountId);
  check(account, 'Không tìm thấy tài khoản.');
  const customer = db.customers.find((row) => row.account_id === accountId);
  if (account.is_active) {
    check(!customer || !db.gaming_sessions.some((row) => row.customer_id === customer.id && row.status === 'Active'), 'Khách đang có phiên chơi; chưa thể khóa tài khoản.');
    check(account.role !== 'Admin' || db.accounts.some((row) => row.id !== accountId && row.role === 'Admin' && row.is_active), 'Không thể khóa quản trị viên hoạt động cuối cùng.');
  }
  return audited({ ...db, accounts: db.accounts.map((row) => row.id === accountId ? { ...row, is_active: !row.is_active } : row) }, account.is_active ? 'Khóa tài khoản' : 'Mở tài khoản', account.display_name, now);
}

export function createTopUp(db, customerId, amount, now = new Date()) {
  check(db.customers.some((row) => row.id === customerId) && integer(amount, 10000, 2000000) && Number(amount) % 1000 === 0, 'Chọn khách và số tiền 10.000–2.000.000đ, bội số 1.000đ.');
  check(!db.top_ups.some((row) => row.customer_id === customerId && ['Pending', 'CreditedUnpaid'].includes(row.status)), 'Khách đang có yêu cầu nạp hoặc khoản chờ thu.');
  const record = { id: nextId(db.top_ups), customer_id: customerId, amount: Number(amount), status: 'Pending', requested_by_customer: false, requested_at: now.toISOString(), credited_by_account_id: null, credited_at: null, collected_by_account_id: null, collected_at: null, collection_method: null };
  return audited({ ...db, top_ups: [...db.top_ups, record] }, 'Tạo yêu cầu nạp ví', record.id, now);
}

export function processTopUp(db, id, action, method = 'Cash', now = new Date()) {
  const row = db.top_ups.find((item) => item.id === id);
  check(row, 'Không tìm thấy yêu cầu nạp.');
  let result = db, updated;
  if (action === 'credit') {
    check(row.status === 'Pending' && !db.wallet_transactions.some((entry) => entry.top_up_id === id && entry.transaction_type === 'TopUpCredit'), 'Yêu cầu này không thể nạp thêm lần nữa.');
    check(!db.top_ups.some((entry) => entry.id !== id && entry.customer_id === row.customer_id && entry.status === 'CreditedUnpaid'), 'Hãy thu xong khoản nạp trước của khách.');
    result = changeWallet(db, row.customer_id, row.amount, 'TopUpCredit', { top_up_id: id }, now);
    updated = { ...row, status: 'CreditedUnpaid', credited_at: now.toISOString(), credited_by_account_id: actor(db) };
  } else if (action === 'collect') {
    check(row.status === 'CreditedUnpaid' && ['Cash', 'BankTransfer'].includes(method), 'Chỉ xác nhận thu cho khoản đã cộng ví và chưa thu tiền.');
    updated = { ...row, status: 'Collected', collected_at: now.toISOString(), collected_by_account_id: actor(db), collection_method: method };
  } else {
    check(action === 'reject' && row.status === 'Pending', 'Chỉ từ chối yêu cầu chưa được cộng ví.');
    updated = { ...row, status: 'Rejected' };
  }
  return audited({ ...result, top_ups: db.top_ups.map((entry) => entry.id === id ? updated : entry) }, `Nạp ví: ${action}`, id, now);
}

export function saveProduct(db, form, now = new Date()) {
  const existing = db.products.find((row) => row.id === Number(form.id));
  check(text(form.name) && !db.products.some((row) => row.id !== existing?.id && normalize(row.name) === normalize(form.name)), 'Tên món trống, trùng hoặc dài hơn 100 ký tự.');
  check(db.product_categories.some((row) => row.id === Number(form.category_id)) && integer(form.current_price, 0, 10000000) && integer(form.cost_price, 0, 10000000) && integer(form.min_stock, 0, 100000), 'Danh mục, giá hoặc ngưỡng tồn kho không hợp lệ.');
  const record = { ...existing, id: existing?.id || nextId(db.products), name: form.name.trim(), category_id: Number(form.category_id), current_price: Number(form.current_price), cost_price: Number(form.cost_price), min_stock: Number(form.min_stock), is_active: form.is_active !== false, stock_quantity: existing ? remainingStock(db, existing) : 0 };
  return audited({ ...db, products: existing ? db.products.map((row) => row.id === record.id ? record : row) : [...db.products, record] }, existing ? 'Cập nhật sản phẩm' : 'Tạo sản phẩm', record.name, now);
}

export function saveCategory(db, form, now = new Date()) {
  const existing = db.product_categories.find((row) => row.id === Number(form.id));
  check(text(form.name, 50) && !db.product_categories.some((row) => row.id !== existing?.id && normalize(row.name) === normalize(form.name)), 'Tên danh mục trống, trùng hoặc quá 50 ký tự.');
  const record = { id: existing?.id || nextId(db.product_categories), name: form.name.trim() };
  return audited({ ...db, product_categories: existing ? db.product_categories.map((row) => row.id === record.id ? record : row) : [...db.product_categories, record] }, 'Lưu danh mục', record.name, now);
}

export function moveInventory(db, form, now = new Date()) {
  const product = db.products.find((row) => row.id === Number(form.product_id));
  check(product && ['In', 'Out', 'Adjust'].includes(form.type) && integer(form.quantity, form.type === 'Adjust' ? 0 : 1, 100000), 'Mặt hàng, loại phiếu hoặc số lượng không hợp lệ.');
  check(text(form.reason, 250), 'Nhập lý do/nhà cung cấp tối đa 250 ký tự.');
  const before = remainingStock(db, product);
  const quantity = Number(form.quantity);
  const delta = form.type === 'Adjust' ? quantity - before : form.type === 'Out' ? -quantity : quantity;
  check(delta !== 0 && before + delta >= 0, 'Số lượng không thay đổi hoặc xuất quá số còn lại.');
  check(form.type !== 'In' || integer(form.unit_cost, 0, 10000000), 'Giá nhập không hợp lệ.');
  const movement = { id: nextId(db.inventory_movements || []), product_id: product.id, type: form.type, quantity: delta, before, after: before + delta, unit_cost: form.type === 'In' ? Number(form.unit_cost) : product.cost_price || 0, reason: form.reason.trim(), recorded_at: now.toISOString(), account_id: actor(db) };
  return audited({ ...db, products: db.products.map((row) => row.id === product.id ? { ...row, stock_quantity: movement.after, ...(form.type === 'In' ? { cost_price: movement.unit_cost } : {}) } : row), inventory_movements: [...(db.inventory_movements || []), movement] }, `Kho: ${form.type}`, product.name, now);
}

export function processOrderItem(db, id, action, reason = '', now = new Date()) {
  const item = db.order_items.find((row) => row.id === id);
  check(item?.status === 'Pending' && ['confirm', 'cancel'].includes(action), 'Chỉ xử lý món đang chờ; món này có thể đã được xử lý.');
  const order = db.orders.find((row) => row.id === item.order_id);
  check(order, 'Không tìm thấy đơn món.');
  if (action === 'confirm') return audited({ ...db, order_items: db.order_items.map((row) => row.id === id ? { ...row, status: 'Confirmed', confirmed_at: now.toISOString(), confirmed_by_account_id: actor(db) } : row), order_item_cancel_requests: db.order_item_cancel_requests.map((row) => row.order_item_id === id && row.status === 'Pending' ? { ...row, status: 'Rejected', handled_at: now.toISOString(), handled_by_account_id: actor(db) } : row) }, 'Xác nhận món', id, now);
  check(text(reason, 250), 'Nhập lý do hủy món.');
  check(order.payment_method !== 'Cash' || order.payment_status !== 'Paid', 'Đơn tiền mặt đã thu cần xử lý hoàn tiền tại quầy.');
  let result = db;
  if (order.payment_method !== 'Cash') {
    check(db.wallet_transactions.some((row) => row.order_id === order.id && row.transaction_type === 'OrderDebit' && row.customer_id === order.customer_id), 'Chưa có giao dịch trừ ví cho đơn này; không thể tự hoàn tiền.');
    check(!db.wallet_transactions.some((row) => row.order_item_id === id && row.transaction_type === 'OrderItemRefund'), 'Món đã được hoàn tiền.');
    result = changeWallet(db, order.customer_id, item.quantity * item.unit_price_snapshot, 'OrderItemRefund', { order_item_id: id }, now);
  }
  const product = db.products.find((row) => row.id === item.product_id);
  const before = product ? remainingStock(db, product) : 0;
  return audited({ ...result,
    products: result.products.map((row) => row.id === item.product_id ? { ...row, stock_quantity: before + item.quantity } : row),
    order_items: db.order_items.map((row) => row.id === id ? { ...row, status: 'Cancelled', cancelled_at: now.toISOString(), cancelled_by_account_id: actor(db), cancel_reason: reason.trim() } : row),
    inventory_movements: product ? [...(db.inventory_movements || []), { id: nextId(db.inventory_movements || []), product_id: product.id, type: 'Return', quantity: item.quantity, before, after: before + item.quantity, unit_cost: product.cost_price || 0, reason: `Hoàn tồn món hủy #GM${order.id}`, recorded_at: now.toISOString(), account_id: actor(db) }] : db.inventory_movements,
    order_item_cancel_requests: db.order_item_cancel_requests.map((row) => row.order_item_id === id && row.status === 'Pending' ? { ...row, status: 'Approved', handled_at: now.toISOString(), handled_by_account_id: actor(db) } : row),
  }, 'Hủy món và hoàn tồn', id, now);
}

export function rejectCancellation(db, requestId, now = new Date()) {
  const request = db.order_item_cancel_requests.find((row) => row.id === requestId);
  check(request?.status === 'Pending', 'Yêu cầu đã được xử lý.');
  return audited({ ...db, order_item_cancel_requests: db.order_item_cancel_requests.map((row) => row.id === requestId ? { ...row, status: 'Rejected', handled_at: now.toISOString(), handled_by_account_id: actor(db) } : row) }, 'Từ chối hủy món', requestId, now);
}

export function collectCashOrder(db, id, now = new Date()) {
  const order = db.orders.find((row) => row.id === id);
  const items = db.order_items.filter((row) => row.order_id === id && row.status !== 'Cancelled');
  check(order?.payment_method === 'Cash' && order.payment_status === 'PendingCollection' && items.length > 0, 'Đơn không còn khoản tiền mặt chờ thu.');
  check(items.every((row) => row.status === 'Confirmed'), 'Xác nhận tất cả món trước khi thu tiền mặt.');
  return audited({ ...db, orders: db.orders.map((row) => row.id === id ? { ...row, payment_status: 'Paid', cash_collected_at: now.toISOString(), cash_collected_by: actor(db) } : row) }, 'Thu tiền mặt đơn món', id, now);
}

export function allReceipts(db) { return db.customers.flatMap((customer) => getReceipts(db, customer.id).map((row) => ({ ...row, customer, account: db.accounts.find((account) => account.id === customer.account_id) }))).sort((a, b) => new Date(b.session.started_at) - new Date(a.session.started_at)); }
export function foodComputerIds(db) { return new Set(db.order_items.filter((item) => item.status === 'Pending').map((item) => db.gaming_sessions.find((session) => session.id === db.orders.find((order) => order.id === item.order_id)?.gaming_session_id)?.computer_id).filter(Boolean)); }

export function dayKey(value) {
  if (!value || !Number.isFinite(Date.parse(value))) return '';
  const parts = Object.fromEntries(new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date(value)).map((part) => [part.type, part.value]));
  return `${parts.year}-${parts.month}-${parts.day}`;
}
export function addDays(day, delta) { return new Date(Date.parse(`${day}T12:00:00+07:00`) + delta * 86400000).toISOString().slice(0, 10); }

export function reportData(db, { from, to }, computers = []) {
  check(/^\d{4}-\d{2}-\d{2}$/.test(from) && /^\d{4}-\d{2}-\d{2}$/.test(to) && Number.isFinite(Date.parse(from)) && Number.isFinite(Date.parse(to)) && from <= to, 'Khoảng ngày không hợp lệ.');
  check(dayKey(`${from}T12:00:00+07:00`) === from && dayKey(`${to}T12:00:00+07:00`) === to, 'Ngày không tồn tại trong lịch.');
  check((Date.parse(to) - Date.parse(from)) / 86400000 <= 365, 'Chọn khoảng thời gian tối đa 366 ngày.');
  const within = (time) => { const day = dayKey(time); return day && day >= from && day <= to; };
  const timeline = [];
  for (let day = from; day <= to; day = addDays(day, 1)) timeline.push({ day, name: day.slice(5).split('-').reverse().join('/'), gaming: 0, food: 0, total: 0 });
  const buckets = new Map(timeline.map((row) => [row.day, row]));
  for (const row of db.wallet_transactions) {
    if (!within(row.recorded_at)) continue;
    const bucket = buckets.get(dayKey(row.recorded_at));
    if (row.transaction_type === 'GamingCharge') bucket.gaming -= row.amount;
    if (['OrderDebit', 'OrderItemRefund'].includes(row.transaction_type)) bucket.food -= row.amount;
  }
  let cashCollected = 0;
  for (const order of db.orders.filter((row) => row.payment_method === 'Cash' && row.payment_status === 'Paid' && within(row.cash_collected_at))) {
    const amount = db.order_items.filter((row) => row.order_id === order.id && row.status !== 'Cancelled').reduce((sum, row) => sum + row.quantity * row.unit_price_snapshot, 0);
    buckets.get(dayKey(order.cash_collected_at)).food += amount;
    cashCollected += amount;
  }
  timeline.forEach((row) => { row.total = row.gaming + row.food; });
  const gaming = timeline.reduce((sum, row) => sum + row.gaming, 0);
  const food = timeline.reduce((sum, row) => sum + row.food, 0);
  const topProducts = db.products.map((product) => {
    let quantity = 0, amount = 0;
    for (const item of db.order_items.filter((row) => row.product_id === product.id && row.status !== 'Cancelled')) {
      const order = db.orders.find((row) => row.id === item.order_id);
      if (!order || (order.payment_method === 'Cash' ? order.payment_status !== 'Paid' || !within(order.cash_collected_at) : !within(order.placed_at))) continue;
      quantity += item.quantity; amount += item.quantity * item.unit_price_snapshot;
    }
    return { id: product.id, name: product.name, quantity, amount };
  }).filter((row) => row.quantity > 0).sort((a, b) => b.quantity - a.quantity || b.amount - a.amount);
  const receipts = allReceipts(db);
  const occupied = computers.filter((pc) => pc.Status === 'Occupied').length;
  return { timeline, gaming, food, total: gaming + food, cashCollected, topProducts,
    deposits: db.top_ups.filter((row) => row.status === 'Collected' && within(row.collected_at)).reduce((sum, row) => sum + row.amount, 0),
    purchases: (db.inventory_movements || []).filter((row) => row.type === 'In' && within(row.recorded_at)).reduce((sum, row) => sum + row.quantity * row.unit_cost, 0),
    refunds: db.wallet_transactions.filter((row) => row.transaction_type === 'OrderItemRefund' && within(row.recorded_at)).reduce((sum, row) => sum + row.amount, 0),
    cashDue: receipts.reduce((sum, row) => sum + row.cashDue, 0),
    sessions: db.gaming_sessions.filter((row) => within(row.started_at)).length,
    occupancy: computers.length ? Math.round(occupied / computers.length * 100) : 0,
  };
}

export function csvText(headers, rows) {
  const escape = (value) => { const raw = String(value ?? ''); const safe = typeof value !== 'number' && /^[=+\-@\t\r]/.test(raw) ? `'${raw}` : raw; return `"${safe.replace(/"/g, '""')}"`; };
  return '\uFEFF' + [headers, ...rows].map((row) => row.map(escape).join(',')).join('\r\n');
}
