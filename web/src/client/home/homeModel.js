import { createDemoData, CURRENT_CUSTOMER_ID } from './mockData.js';

function normalizeRiotId(value) {
  if (typeof value !== 'string' || value.length > 128 || /[\u0000-\u001f\u007f]/u.test(value)) return null;
  const parts = value.split('#');
  if (parts.length !== 2) return null;
  const [name, tag] = parts.map((part) => part.trim());
  return name && tag && !/\s/u.test(tag) ? `${name}#${tag}` : null;
}

// Validate stored records before rendering. Nullable history fields must survive reloads.
export function isValidHomeData(db) {
  if (!db || typeof db !== 'object') return false;
  const sample = createDemoData();
  const nullable = new Set(['OldStatus', 'EmployeeId', 'InvoiceId', 'SessionId', 'PromotionId', 'CreatedBy', 'WalletTransactionId', 'EndTime', 'LastChargedAt', 'SkillScore', 'ChangedBy', 'WinRate', 'HoursPlayed']);
  const extraShapes = {
    InventoryTransactions: { InvTransId: 1, ProductId: 1, EmployeeId: null, Type: '', Quantity: 1, ReferenceId: 1, CreatedAt: '' },
    TournamentRegistrations: { RegistrationId: 1, TournamentId: 1, CustomerId: 1, RankId: 1, PreferredRole: '', WinRate: 1, HoursPlayed: 1, SkillScore: null, Status: '', RegisteredAt: '' },
    Teams: { TeamId: 1, TournamentId: 1, TeamName: '', AvgSkillScore: 1 },
    TeamMembers: { TeamId: 1, RegistrationId: 1, AssignedRole: '' },
  };
  const recordsValid = Object.entries(sample).every(([table, rows]) => Array.isArray(db[table]) && db[table].every((row) => {
    if (!row || typeof row !== 'object') return false;
    return Object.entries(rows[0] || extraShapes[table]).every(([key, value]) => {
      if (!(key in row)) return false;
      if (row[key] === null) return nullable.has(key);
      if (/(Time|Date|At|Start|End)$/.test(key)) return typeof row[key] === 'string' && Number.isFinite(Date.parse(row[key]));
      if (value === null || typeof value === 'number' || key.endsWith('Id') || key === 'SkillScore') return Number.isFinite(row[key]);
      return typeof row[key] === typeof value;
    });
  }));
  return recordsValid
    && db.Customers.some((row) => row.CustomerId === CURRENT_CUSTOMER_ID && row.WalletBalance >= 0)
    && db.TournamentRegistrations.every((row) => row.RiotId === undefined || normalizeRiotId(row.RiotId) !== null)
    && db.Products.every((row) => Number.isInteger(row.Stock) && row.Stock >= 0 && row.Price >= 0);
}

export const money = (value) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value ?? 0);
export const dateTime = (value) => new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value));
export const duration = (seconds) => [Math.floor(seconds / 3600), Math.floor(seconds / 60) % 60, seconds % 60].map((v) => String(v).padStart(2, '0')).join(':');
export const statusLabels = { Idle: 'Chưa có phiên', Active: 'Đang chơi', Pending: 'Chờ xác nhận', Preparing: 'Đang chuẩn bị', Ready: 'Sẵn sàng phục vụ', Delivered: 'Đã phục vụ', Completed: 'Hoàn tất', Cancelled: 'Đã hủy', Paid: 'Đã thanh toán', Unpaid: 'Chưa thanh toán', Registered: 'Đã đăng ký', Assigned: 'Đã xếp đội', Open: 'Đang mở đăng ký', Closed: 'Đã đóng đăng ký', TeamsAssigned: 'Đã xếp đội', Finished: 'Đã kết thúc' };
const nextId = (rows, key) => Math.max(0, ...rows.map((row) => row[key])) + 1;

export function getHomeSummary(db, now = new Date(), customerId = CURRENT_CUSTOMER_ID) {
  const customer = db.Customers.find((row) => row.CustomerId === customerId);
  const session = db.GamingSessions.filter((row) => row.CustomerId === customerId && row.Status === 'Active' && !row.EndTime).sort((a, b) => new Date(b.StartTime) - new Date(a.StartTime))[0];
  const computer = db.Computers.find((row) => row.ComputerId === session?.ComputerId);
  const room = db.ComputerRooms.find((row) => row.RoomId === computer?.RoomId);
  const type = db.ComputerTypes.find((row) => row.ComputerTypeId === computer?.ComputerTypeId);
  const elapsed = session ? Math.max(0, Math.floor((now - new Date(session.StartTime)) / 1000)) : 0;
  // Estimate only the uncharged interval; this display never debits the wallet.
  const uncharged = session ? Math.max(0, now - new Date(session.LastChargedAt || session.StartTime)) / 3600000 : 0;
  const sessionCost = Math.round((session?.TotalCost || 0) + uncharged * (type?.PricePerHour || 0));
  const orders = db.Orders.filter((row) => row.CustomerId === customerId && session && row.SessionId === session.SessionId && row.Status !== 'Cancelled');
  const orderTotal = orders.reduce((sum, row) => sum + row.TotalAmount, 0);
  const unpaidOrders = orders.reduce((sum, row) => sum + (db.Invoices.some((invoice) => invoice.InvoiceId === row.InvoiceId && invoice.Status === 'Paid') ? 0 : row.TotalAmount), 0);
  const remainingHours = type?.PricePerHour ? Math.max(0, (customer?.WalletBalance || 0) - unpaidOrders - sessionCost) / type.PricePerHour : null;
  const products = db.Products.filter((row) => row.IsActive && db.Categories.some((category) => category.CategoryId === row.CategoryId && category.IsActive));
  const tournament = db.Tournaments.filter((row) => ['Open', 'Closed', 'TeamsAssigned'].includes(row.Status) && new Date(row.StartTime) > now && db.Games.some((game) => game.GameId === row.GameId && game.IsActive)).sort((a, b) => new Date(a.StartTime) - new Date(b.StartTime))[0];
  return { customer, session, computer, room, type, elapsed, sessionCost, orderTotal, unpaidOrders, total: sessionCost + orderTotal, remainingHours, products, tournament };
}

export function getActivities(db, customerId = CURRENT_CUSTOMER_ID) {
  return [
    ...db.Orders.filter((r) => r.CustomerId === customerId).map((r) => ({ key: `order-${r.OrderId}`, kind: 'order', id: r.OrderId, title: `Đơn món #GM${r.OrderId}`, subtitle: `${db.OrderDetails.filter((d) => d.OrderId === r.OrderId).reduce((s, d) => s + d.Quantity, 0)} món · Phục vụ tại máy`, time: r.OrderTime, amount: r.TotalAmount, status: r.Status, record: r })),
    ...db.WalletTransactions.filter((r) => r.CustomerId === customerId).map((r) => ({ key: `wallet-${r.TransactionId}`, kind: 'wallet', id: r.TransactionId, title: r.Type === 'Deposit' ? 'Nạp tiền vào ví' : 'Giao dịch ví', subtitle: r.Description, time: r.CreatedAt, amount: r.Amount, status: 'Completed', record: r })),
    ...db.Invoices.filter((r) => r.CustomerId === customerId).map((r) => ({ key: `invoice-${r.InvoiceId}`, kind: 'invoice', id: r.InvoiceId, title: `Hóa đơn #HD${r.InvoiceId}`, subtitle: 'Chi tiết sử dụng dịch vụ', time: r.InvoiceDate, amount: r.TotalAmount, status: r.Status, record: r })),
  ].sort((a, b) => new Date(b.time) - new Date(a.time));
}

export function placeOrder(db, cart, now = new Date()) {
  const { customer, session, computer, products } = getHomeSummary(db, now);
  if (customer?.Status !== 'Active' || !session || computer?.Status !== 'Occupied') throw new Error('Bạn cần tài khoản và phiên chơi đang hoạt động để gọi món.');
  const entries = Object.entries(cart).filter(([, quantity]) => quantity !== 0);
  if (!entries.length) throw new Error('Hãy chọn ít nhất một món.');
  const items = entries.map(([id, quantity]) => {
    const product = products.find((p) => p.ProductId === Number(id));
    if (!product || !Number.isInteger(quantity) || quantity < 1 || quantity > product.Stock) throw new Error('Món đã hết hoặc số lượng không hợp lệ. Vui lòng kiểm tra lại giỏ hàng.');
    return { product, quantity };
  });
  const orderId = nextId(db.Orders, 'OrderId');
  const time = now.toISOString();
  const order = { OrderId: orderId, CustomerId: customer.CustomerId, SessionId: session.SessionId, EmployeeId: null, InvoiceId: null, OrderTime: time, Status: 'Pending', TotalAmount: items.reduce((sum, { product, quantity }) => sum + product.Price * quantity, 0) };
  return {
    ...db,
    Orders: [...db.Orders, order],
    Products: db.Products.map((product) => ({ ...product, Stock: product.Stock - (items.find((item) => item.product.ProductId === product.ProductId)?.quantity || 0) })),
    OrderDetails: [...db.OrderDetails, ...items.map(({ product, quantity }, i) => ({ OrderDetailId: nextId(db.OrderDetails, 'OrderDetailId') + i, OrderId: orderId, ProductId: product.ProductId, Quantity: quantity, UnitPrice: product.Price }))],
    OrderStatusHistory: [...db.OrderStatusHistory, { HistoryId: nextId(db.OrderStatusHistory, 'HistoryId'), OrderId: orderId, OldStatus: null, NewStatus: 'Pending', ChangedBy: null, ChangedAt: time }],
    InventoryTransactions: [...db.InventoryTransactions, ...items.map(({ product, quantity }, i) => ({ InvTransId: nextId(db.InventoryTransactions, 'InvTransId') + i, ProductId: product.ProductId, EmployeeId: null, Type: 'Sale', Quantity: -quantity, ReferenceId: orderId, CreatedAt: time }))],
  };
}

export function depositDemo(db, amount, now = new Date()) {
  const customer = db.Customers.find((r) => r.CustomerId === CURRENT_CUSTOMER_ID);
  if (customer?.Status !== 'Active') throw new Error('Tài khoản hiện không thể nạp tiền.');
  if (!Number.isSafeInteger(amount) || amount < 10000 || amount > 2000000 || amount % 1000 !== 0) throw new Error('Nhập từ 10.000đ đến 2.000.000đ, theo bội số 1.000đ.');
  const balance = customer.WalletBalance + amount;
  return { ...db, Customers: db.Customers.map((r) => r.CustomerId === customer.CustomerId ? { ...r, WalletBalance: balance } : r), WalletTransactions: [...db.WalletTransactions, { TransactionId: nextId(db.WalletTransactions, 'TransactionId'), CustomerId: customer.CustomerId, Amount: amount, BalanceAfter: balance, Type: 'Deposit', Description: 'Nạp tiền mô phỏng · Không có giao dịch thật', CreatedAt: now.toISOString() }] };
}

export function registerTournament(db, tournamentId, form, now = new Date()) {
  const tournament = db.Tournaments.find((r) => r.TournamentId === tournamentId);
  const customer = db.Customers.find((r) => r.CustomerId === CURRENT_CUSTOMER_ID);
  if (customer?.Status !== 'Active') throw new Error('Tài khoản hiện không thể đăng ký.');
  if (!tournament || tournament.Status !== 'Open' || now < new Date(tournament.RegistrationStart) || now >= new Date(tournament.RegistrationEnd)) throw new Error('Giải đấu hiện không nhận đăng ký.');
  if (tournament.EntryFee > 0) throw new Error('Vui lòng liên hệ quầy để thanh toán lệ phí và đăng ký giải này.');
  if (db.TournamentRegistrations.some((r) => r.TournamentId === tournamentId && r.CustomerId === customer.CustomerId && r.Status !== 'Cancelled')) throw new Error('Bạn đã đăng ký giải đấu này.');
  const rank = db.GameRanks.find((r) => r.RankId === Number(form.rankId) && r.GameId === tournament.GameId);
  if (!rank || !['Duelist', 'Initiator', 'Controller', 'Sentinel', 'Flex'].includes(form.role)) throw new Error('Vui lòng chọn hạng và vai trò hợp lệ.');
  const riotId = normalizeRiotId(form.riotId);
  if (!riotId) throw new Error('Nhập tên người chơi theo dạng Tên#TAG, ví dụ MinhAnh#VN1.');
  // RiotId is a demo extension pending backend/schema support. Unknown tracker
  // statistics stay null; submitting a name does not claim a successful sync.
  return { ...db, TournamentRegistrations: [...db.TournamentRegistrations, { RegistrationId: nextId(db.TournamentRegistrations, 'RegistrationId'), TournamentId: tournamentId, CustomerId: customer.CustomerId, RankId: rank.RankId, PreferredRole: form.role, RiotId: riotId, WinRate: null, HoursPlayed: null, SkillScore: null, Status: 'Registered', RegisteredAt: now.toISOString() }] };
}

