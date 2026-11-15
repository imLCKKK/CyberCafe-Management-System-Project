import { ApiError, apiRequest } from '../apiClient.js';

const id = value => Number.isSafeInteger(value) && value > 0;
const moneyValue = value => Number.isSafeInteger(value) && value >= 0;
const invalid = () => new ApiError('Dữ liệu hóa đơn không hợp lệ hoặc vượt giới hạn hiển thị chính xác. Vui lòng tải lại và liên hệ nhân viên.', 200);

export function validateInvoice(row) {
  if (!row || !id(row.id) || !id(row.customerId) || typeof row.customerName !== 'string'
    || !Array.isArray(row.orderIds) || !row.orderIds.every(id) || typeof row.issuedBy !== 'string'
    || (row.gamingSessionId !== null && !id(row.gamingSessionId))
    || !['Staff', 'System'].includes(row.actorType)
    || (row.actorType === 'Staff' ? !id(row.employeeId) : row.employeeId !== null)
    || !Number.isFinite(Date.parse(row.issuedAt)) || !['Pending', 'Paid', 'Cancelled'].includes(row.status)
    || !['subtotal', 'discount', 'grossAmount', 'cancelledAmount', 'refundAmount', 'paidAmount', 'amountDue', 'overpaidAmount'].every(key => moneyValue(row[key]))
    || !Array.isArray(row.lines) || !Array.isArray(row.payments)) throw invalid();
  if (row.lines.some(line => !id(line.id) || typeof line.description !== 'string' || !id(line.quantity)
    || !['unitPrice', 'subtotal', 'discountAmount', 'amount', 'cancelledAmount'].every(key => moneyValue(line[key]))
    || line.amount + line.discountAmount !== line.subtotal || line.cancelledAmount > line.amount)) throw invalid();
  if (row.payments.some(payment => !id(payment.id) || !['Payment', 'Refund'].includes(payment.type)
    || !['Wallet', 'Cash', 'ZaloPay'].includes(payment.method)
    || !['Pending', 'Succeeded', 'Failed', 'Expired'].includes(payment.status) || !moneyValue(payment.amount))) throw invalid();
  if (row.grossAmount !== row.subtotal - row.discount) throw invalid();
  return row;
}

export async function listInvoices(admin, signal) {
  const rows = await apiRequest(`/${admin ? 'admin' : 'client'}/invoices`, { signal });
  if (!Array.isArray(rows)) throw invalid();
  return rows.map(validateInvoice);
}
export async function getInvoice(admin, invoiceId, signal) {
  if (!id(invoiceId)) throw invalid();
  return validateInvoice(await apiRequest(`/${admin ? 'admin' : 'client'}/invoices/${invoiceId}`, { signal }));
}
export async function issueInvoice(body, operationKey) {
  return validateInvoice(await apiRequest('/admin/invoices', {
    method: 'POST', body, headers: { 'Idempotency-Key': operationKey },
  }));
}
export const invoiceMoney = value => `${new Intl.NumberFormat('vi-VN').format(value)} đ`;
export const invoiceDate = value => new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
export const invoiceStatus = value => ({ Pending: 'Còn phải thu', Paid: 'Đã thanh toán', Cancelled: 'Đã hủy' }[value] || value);
