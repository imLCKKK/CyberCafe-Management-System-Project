// Gọi API ví/hồ sơ của khách đăng nhập (S1-M-1).
// Quy ước: API lỗi thì ném ApiError để trang hiện lỗi; KHÔNG quay về dữ liệu localStorage.
// Khi Đại bàn giao API client chung (S1-D-2), chỉ cần đổi hàm request() bên dưới sang client đó.

export class ApiError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}

async function request(path, options = {}) {
  let response;
  try {
    response = await fetch(path, { credentials: 'include', headers: { 'Content-Type': 'application/json' }, ...options });
  } catch {
    throw new ApiError(0, 'Không kết nối được máy chủ API. Hãy kiểm tra API đang chạy.');
  }
  if (response.ok) return response.status === 204 ? null : response.json();

  let message = `Máy chủ trả về lỗi ${response.status}.`;
  if (response.status === 401) message = 'Bạn chưa đăng nhập hoặc phiên đã hết hạn.';
  else if (response.status === 403) message = 'Tài khoản này không có quyền xem trang ví của khách.';
  else {
    try {
      const body = await response.json();
      if (body.errors) message = Object.values(body.errors).flat().join(' ');
      else if (body.detail || body.title) message = body.detail || body.title;
    } catch { /* body không phải JSON: giữ thông báo mặc định */ }
  }
  throw new ApiError(response.status, message);
}

// Chuyển JSON camelCase của API sang dạng trang Ví đang dùng, để không phải viết lại toàn bộ giao diện.
const toTransaction = (r) => ({
  id: r.id, customer_id: r.customerId, transaction_type: r.transactionType,
  amount: Number(r.amount), balance_after: Number(r.balanceAfter),
  top_up_id: r.topUpId, gaming_session_id: r.gamingSessionId, order_id: r.orderId,
  order_item_id: r.orderItemId, tournament_registration_id: r.tournamentRegistrationId,
  recorded_at: r.recordedAt,
});
const toTopUp = (r) => ({
  id: r.id, customer_id: r.customerId, amount: Number(r.amount),
  requested_by_customer: r.requestedByCustomer, status: r.status,
  requested_at: r.requestedAt, collection_method: r.collectionMethod,
});

export const getClientProfile = () => request('/api/client/profile');

export async function getClientWallet() {
  const data = await request('/api/client/wallet');
  return {
    walletBalance: Number(data.walletBalance),
    transactions: data.transactions.map(toTransaction),
    topUps: data.topUps.map(toTopUp),
  };
}

export const requestClientTopUp = (amount) =>
  request('/api/client/top-ups', { method: 'POST', body: JSON.stringify({ amount }) });
