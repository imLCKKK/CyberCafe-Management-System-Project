// API quản lý khách hàng của NHÂN VIÊN (S1-M-1). Quyền kiểm tra ở server (customer.*, wallet.*).
import { request } from './http';
import { toWalletTransaction } from './walletTypes';

const staff = (path, options = {}) => request(path, { kind: 'staff', ...options });

export const listCustomers = () => staff('/api/admin/customers');
export const createCustomer = (body) => staff('/api/admin/customers', { method: 'POST', body });
export const updateCustomer = (id, body) => staff(`/api/admin/customers/${id}`, { method: 'PUT', body });
export const changeCustomerStatus = (id, status, reason) =>
  staff(`/api/admin/customers/${id}/status`, { method: 'PUT', body: { status, reason } });
export const staffTopUp = (id, amount, collectionMethod) =>
  staff(`/api/admin/customers/${id}/wallet/top-ups`, { method: 'POST', body: { amount, collectionMethod } });
export async function listWalletTransactions(id) {
  const rows = await staff(`/api/admin/customers/${id}/wallet-transactions`);
  return rows.map(toWalletTransaction);
}
