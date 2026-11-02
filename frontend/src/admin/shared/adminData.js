import { nextId } from '../../client/shared/demoModel.js';

// Extend a saved client demo without resetting its orders or wallet history.
export function ensureAdminData(source, now = new Date()) {
  if (source.admin_schema_version === 1) return source;
  const db = structuredClone(source);
  const parts = Object.fromEntries(new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(now).map((part) => [part.type, part.value]));
  const at = (days, hour = 12) => new Date(Date.UTC(Number(parts.year), Number(parts.month) - 1, Number(parts.day) - days, hour - 7)).toISOString();
  db.inventory_movements ||= [];
  db.staff_profiles ||= [];
  db.audit_log ||= [];
  db.products = db.products.map((product) => ({ ...product, is_active: product.is_active !== false, cost_price: product.cost_price ?? Math.round(product.current_price * .55 / 1000) * 1000, min_stock: product.min_stock ?? 8 }));
  if (!db.accounts.some((account) => account.role === 'Admin')) db.accounts.push({ id: nextId(db.accounts), phone: '0900000001', display_name: 'Quản trị viên', role: 'Admin', is_active: true, password_hash: 'demo-disabled', must_change_password: false, created_at: at(60), created_by_account_id: null });
  for (const [index, name] of ['Trần Hoàng Nam', 'Lê Gia Huy', 'Phạm Tuấn Kiệt', 'Võ Ngọc Linh'].entries()) {
    const accountId = nextId(db.accounts);
    const customerId = nextId(db.customers);
    db.accounts.push({ id: accountId, phone: `098765430${index}`, display_name: name, role: 'Customer', is_active: true, password_hash: 'demo-disabled', must_change_password: false, created_at: at(30 + index), created_by_account_id: null });
    const customer = { id: customerId, account_id: accountId, wallet_balance: 500000, wallet_version: 0 };
    db.customers.push(customer);
    const topUpId = nextId(db.top_ups);
    db.top_ups.push({ id: topUpId, customer_id: customerId, amount: 500000, status: 'Collected', requested_by_customer: false, requested_at: at(15), credited_at: at(15), collected_at: at(15), credited_by_account_id: 2, collected_by_account_id: 2, collection_method: index % 2 ? 'BankTransfer' : 'Cash' });
    const transaction = (type, amount, time, fields) => {
      customer.wallet_balance += amount;
      customer.wallet_version++;
      db.wallet_transactions.push({ id: nextId(db.wallet_transactions), customer_id: customerId, transaction_type: type, amount, balance_after: customer.wallet_balance, recorded_at: time, recorded_by_account_id: 2, top_up_id: null, order_id: null, order_item_id: null, gaming_session_id: null, tournament_registration_id: null, ...fields });
    };
    customer.wallet_balance = 0;
    transaction('TopUpCredit', 500000, at(15), { top_up_id: topUpId });
    for (let day = 14 - index; day >= 1; day -= 4) {
      const sessionId = nextId(db.gaming_sessions);
      const charge = 18000 + index * 6000;
      db.gaming_sessions.push({ id: sessionId, customer_id: customerId, computer_id: index % 2 ? 8 : 12, hourly_rate_snapshot: charge, started_at: at(day, 10), last_charged_at: at(day, 11), ended_at: at(day, 11), charged_amount: charge, status: 'Ended', end_reason: 'CustomerLogout', ended_by_account_id: null });
      const product = db.products[(day + index) % db.products.length];
      if (!product) continue;
      const cash = day % 3 === 0;
      const orderId = nextId(db.orders);
      db.orders.push({ id: orderId, customer_id: customerId, gaming_session_id: sessionId, total_amount: product.current_price, placed_at: at(day, 10), payment_method: cash ? 'Cash' : 'Wallet', payment_status: 'Paid', ...(cash ? { cash_collected_at: at(day, 11), cash_collected_by: 2 } : {}) });
      db.order_items.push({ id: nextId(db.order_items), order_id: orderId, product_id: product.id, quantity: 1, unit_price_snapshot: product.current_price, status: 'Confirmed', confirmed_by_account_id: 2, confirmed_at: at(day, 10), cancelled_by_account_id: null, cancelled_at: null });
      if (!cash) transaction('OrderDebit', -product.current_price, at(day, 10), { order_id: orderId });
      transaction('GamingCharge', -charge, at(day, 11), { gaming_session_id: sessionId });
    }
  }
  for (const account of db.accounts.filter((row) => ['Admin', 'Staff'].includes(row.role))) db.staff_profiles.push({ id: account.id, account_id: account.id, shift: account.role === 'Admin' ? 'Linh hoạt' : '08:00–16:00', position: account.role === 'Admin' ? 'Quản lý' : 'Thu ngân', joined_at: account.created_at });
  db.admin_schema_version = 1;
  return db;
}
