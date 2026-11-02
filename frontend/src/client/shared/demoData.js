// Browser-only demo records mirror the supplied DBML, including source foreign keys.
export const DEMO_CUSTOMER_ID = 1;
export const DEMO_ACCOUNT_ID = 1;
export const DEMO_PASSWORD = 'Tram@2026';
export const DEMO_SCHEMA_VERSION = 2;
export function createClientDemo(now = new Date()) {
  const at = (minutes) => new Date(now.getTime() + minutes * 60000).toISOString();
  const password_hash = 'pbkdf2:100000:tram-demo-v2:180ba9bf44ed596557b9b2ecc7f933da944c56bbf121e450c95f4fd671ca3f03';
  const transaction = (id, transaction_type, amount, balance_after, minutes, source) => ({ id, customer_id: 1, transaction_type, amount, balance_after, top_up_id: null, gaming_session_id: null, order_id: null, order_item_id: null, tournament_registration_id: null, recorded_by_account_id: null, recorded_at: at(minutes), ...source });
  const item = (id, order_id, product_id, quantity, unit_price_snapshot, status, minutes) => ({ id, order_id, product_id, quantity, unit_price_snapshot, status, confirmed_by_account_id: status === 'Confirmed' ? 2 : null, confirmed_at: status === 'Confirmed' ? at(minutes) : null, cancelled_by_account_id: status === 'Cancelled' ? 2 : null, cancelled_at: status === 'Cancelled' ? at(minutes) : null });
  const topUp = (id, amount, minutes) => ({ id, customer_id: 1, amount, requested_by_customer: false, status: 'Collected', requested_at: at(minutes), credited_by_account_id: 2, credited_at: at(minutes), collected_by_account_id: 2, collected_at: at(minutes + 1), collection_method: id === 1 ? 'Cash' : 'BankTransfer' });
  return {
    schema_version: DEMO_SCHEMA_VERSION,
    accounts: [
      { id: 1, phone: '0901234567', display_name: 'Nguyễn Minh Anh', password_hash, role: 'Customer', must_change_password: false, is_active: true, created_by_account_id: 2, created_at: at(-43200) },
      { id: 2, phone: '0900000002', display_name: 'Trần Gia Hân', password_hash: 'demo-disabled', role: 'Staff', must_change_password: false, is_active: true, created_by_account_id: null, created_at: at(-86400) },
    ],
    customers: [{ id: 1, account_id: 1, wallet_balance: 153000, wallet_version: 8 }],
    computer_types: ['Normal', 'FPS', 'Couple', 'FPS+', 'Stream'].map((name, i) => ({ id: i + 1, name, hourly_rate: [8000, 12000, 15000, 16000, 22000][i] })),
    computers: [{ id: 12, code: 'MÁY 12', computer_type_id: 2, is_under_maintenance: false }, { id: 8, code: 'MÁY 08', computer_type_id: 2, is_under_maintenance: false }],
    gaming_sessions: [
      { id: 1027, customer_id: 1, computer_id: 12, hourly_rate_snapshot: 12000, started_at: at(-135), last_charged_at: at(-5), charged_amount: 26000, status: 'Active', ended_at: null, end_reason: null, ended_by_account_id: null },
      { id: 1026, customer_id: 1, computer_id: 8, hourly_rate_snapshot: 12000, started_at: at(-1575), last_charged_at: at(-1455), charged_amount: 24000, status: 'Ended', ended_at: at(-1455), end_reason: 'CustomerLogout', ended_by_account_id: null },
    ],
    product_categories: [{ id: 1, name: 'Món chính' }, { id: 2, name: 'Đồ uống' }, { id: 3, name: 'Ăn vặt' }],
    products: [
      { id: 1, category_id: 1, name: 'Mì xào bò', current_price: 28000, stock_quantity: 16 },
      { id: 2, category_id: 1, name: 'Cơm gà xối mỡ', current_price: 35000, stock_quantity: 9 },
      { id: 3, category_id: 2, name: 'Trà đào cam sả', current_price: 15000, stock_quantity: 24 },
      { id: 4, category_id: 2, name: 'Cà phê sữa đá', current_price: 20000, stock_quantity: 12 },
      { id: 5, category_id: 3, name: 'Khoai tây chiên', current_price: 22000, stock_quantity: 7 },
      { id: 6, category_id: 2, name: 'Nước tăng lực', current_price: 18000, stock_quantity: 20 },
      { id: 7, category_id: 1, name: 'Bánh mì trứng', current_price: 26000, stock_quantity: 5 },
      { id: 8, category_id: 3, name: 'Gà viên chiên', current_price: 25000, stock_quantity: 13 },
      { id: 9, category_id: 2, name: 'Trà chanh mát lạnh', current_price: 12000, stock_quantity: 18 },
    ],
    orders: [{ id: 1048, customer_id: 1, gaming_session_id: 1027, total_amount: 43000, placed_at: at(-10) }, { id: 1047, customer_id: 1, gaming_session_id: 1027, total_amount: 48000, placed_at: at(-35) }, { id: 1046, customer_id: 1, gaming_session_id: 1026, total_amount: 28000, placed_at: at(-1500) }],
    order_items: [item(1, 1048, 1, 1, 28000, 'Confirmed', -8), item(2, 1048, 3, 1, 15000, 'Pending', -10), item(3, 1047, 7, 1, 26000, 'Confirmed', -33), item(4, 1047, 5, 1, 22000, 'Cancelled', -30), item(5, 1046, 1, 1, 28000, 'Confirmed', -1498)],
    order_item_cancel_requests: [{ id: 1, order_item_id: 4, requested_by_customer_id: 1, status: 'Approved', requested_at: at(-32), handled_by_account_id: 2, handled_at: at(-30) }],
    top_ups: [...[topUp(1, 200000, -4320), topUp(2, 100000, -140)], { id: 3, customer_id: 1, amount: 50000, requested_by_customer: true, status: 'Rejected', requested_at: at(-120), credited_by_account_id: null, credited_at: null, collected_by_account_id: null, collected_at: null, collection_method: null }],
    wallet_transactions: [
      transaction(1, 'TopUpCredit', 200000, 200000, -4320, { top_up_id: 1, recorded_by_account_id: 2 }),
      transaction(2, 'OrderDebit', -28000, 172000, -1500, { order_id: 1046 }),
      transaction(3, 'GamingCharge', -24000, 148000, -1455, { gaming_session_id: 1026 }),
      transaction(4, 'TopUpCredit', 100000, 248000, -140, { top_up_id: 2, recorded_by_account_id: 2 }),
      transaction(5, 'OrderDebit', -48000, 200000, -35, { order_id: 1047 }),
      transaction(6, 'OrderItemRefund', 22000, 222000, -30, { order_item_id: 4 }),
      transaction(7, 'OrderDebit', -43000, 179000, -10, { order_id: 1048 }),
      transaction(8, 'GamingCharge', -26000, 153000, -5, { gaming_session_id: 1027 }),
    ],
    tournaments: [], tournament_registrations: [], player_stats_snapshots: [], team_generation_runs: [], team_proposals: [], teams: [], team_members: [],
  };
}

// Presentation assets are kept outside database records.
export const productArt = { 1: '🍜', 2: '🍗', 3: '🍑', 4: '☕', 5: '🍟', 6: '🥤', 7: '🥪', 8: '🍿', 9: '🍋' };
