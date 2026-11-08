import { ApiError, apiRequest } from './apiClient.js';

const positiveId = (value) => Number.isSafeInteger(value) && value > 0;
const date = (value) => typeof value === 'string' && Number.isFinite(Date.parse(value));
const nullableText = (value) => value === null || typeof value === 'string';
const validProfile = (value) => value && positiveId(value.customerId)
  && typeof value.fullName === 'string' && value.fullName.trim()
  && typeof value.phone === 'string' && value.phone.trim()
  && nullableText(value.email) && typeof value.status === 'string' && value.status.trim()
  && date(value.createdAtUtc);
const invalid = () => new ApiError('Dữ liệu khách hàng hoặc ví từ máy chủ không hợp lệ. Vui lòng tải lại.', 200);

function pageQuery(page, pageSize) {
  if (!positiveId(page) || !positiveId(pageSize) || pageSize > 100) throw invalid();
  return new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
}

function validatePage(value, rows, page, pageSize, predicate, id) {
  if (!value || value.page !== page || value.pageSize !== pageSize
    || !Number.isSafeInteger(value.totalCount) || value.totalCount < 0
    || !Array.isArray(rows) || rows.length > pageSize || value.totalCount < rows.length
    || !rows.every(predicate) || new Set(rows.map((row) => row[id])).size !== rows.length) throw invalid();
}

export async function getCustomers({ page = 1, pageSize = 20, search = '', signal } = {}) {
  const query = pageQuery(page, pageSize);
  query.set('search', search.trim());
  const result = await apiRequest(`/admin/customers?${query}`, { signal });
  validatePage(result, result?.items, page, pageSize, validProfile, 'customerId');
  return result;
}

export async function getCustomerProfile(customerId, signal) {
  if (customerId !== undefined && !positiveId(customerId)) throw invalid();
  const result = await apiRequest(customerId === undefined ? '/customers/me' : `/admin/customers/${customerId}`, { signal });
  if (!validProfile(result) || (customerId !== undefined && result.customerId !== customerId)) throw invalid();
  return result;
}

export async function createCustomer(form, signal) {
  const fullName = form.fullName?.trim() || '';
  const phone = form.phone?.trim() || '';
  const email = form.email?.trim() || null;
  if (!fullName || fullName.length > 150) throw new Error('Họ và tên cần từ 1 đến 150 ký tự.');
  if (phone.length > 20 || !/^\+?\d{8,20}$/.test(phone)) throw new Error('Số điện thoại cần 8–20 chữ số và tối đa 20 ký tự, có thể bắt đầu bằng dấu +.');
  if (email && (email.length > 150 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email))) throw new Error('Email không hợp lệ.');
  if (typeof form.password !== 'string' || form.password.length < 12 || form.password.length > 128)
    throw new Error('Mật khẩu ban đầu cần từ 12 đến 128 ký tự.');
  const result = await apiRequest('/admin/customers', {
    method: 'POST', signal, body: { fullName, phone, email, password: form.password },
  });
  if (!validProfile(result)) throw invalid();
  return result;
}

export async function getCustomerWallet({ customerId, page = 1, pageSize = 20, signal } = {}) {
  if (customerId !== undefined && !positiveId(customerId)) throw invalid();
  const query = pageQuery(page, pageSize);
  const path = customerId === undefined ? '/customers/me/wallet' : `/admin/customers/${customerId}/wallet`;
  const result = await apiRequest(`${path}?${query}`, { signal });
  validatePage(result, result?.transactions, page, pageSize, (row) => row && positiveId(row.transactionId)
    && Number.isFinite(row.amount) && Number.isFinite(row.balanceAfter) && row.balanceAfter >= 0
    && typeof row.type === 'string' && row.type.trim() && nullableText(row.description) && date(row.createdAtUtc), 'transactionId');
  if (!positiveId(result.customerId) || (customerId !== undefined && result.customerId !== customerId)
    || !Number.isFinite(result.balance) || result.balance < 0) throw invalid();
  return result;
}

export const walletMoney = (value) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 2 }).format(value);
export const customerDate = (value) => new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value));
export const customerStatus = (value) => ({ Active: 'Đang hoạt động', Locked: 'Đã khóa', Inactive: 'Ngừng hoạt động' }[value] || value);

export const walletTypeLabel = (value) => ({
  TopUp: 'Nạp tiền', CounterTopUp: 'Nạp tiền tại quầy', GamingCharge: 'Tiền giờ',
  OrderPayment: 'Thanh toán đơn', OrderRefund: 'Hoàn tiền đơn', Adjustment: 'Điều chỉnh ví',
}[value] || value);
