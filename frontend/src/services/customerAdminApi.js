// API quản lý khách hàng của NHÂN VIÊN (S1-M-1). Quyền kiểm tra ở server (customer.*, wallet.*).
import { request } from './http';
import { toWalletTransaction } from './walletTypes';

export { newIdempotencyKey } from './http';

export const listCustomers = () => request('/api/admin/customers');
export const createCustomer = (body) => request('/api/admin/customers', { method: 'POST', body });
export const updateCustomer = (id, body) => request(`/api/admin/customers/${id}`, { method: 'PUT', body });
export const changeCustomerStatus = (id, status, reason) =>
  request(`/api/admin/customers/${id}/status`, { method: 'PUT', body: { status, reason } });
// idempotencyKey bắt buộc: gửi lại request (mạng chập chờn, bấm lại) với CÙNG khóa chỉ cộng ví một lần.
export const staffTopUp = (id, amount, collectionMethod, idempotencyKey) =>
  request(`/api/admin/customers/${id}/wallet/top-ups`, {
    method: 'POST',
    headers: { 'Idempotency-Key': idempotencyKey },
    body: { amount, collectionMethod },
  });
export async function listWalletTransactions(id) {
  const rows = await request(`/api/admin/customers/${id}/wallet-transactions`);
  return rows.map(toWalletTransaction);
}
