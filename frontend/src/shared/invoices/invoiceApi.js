import { ApiError, apiRequest } from '../apiClient.js';

const id = value => Number.isSafeInteger(value) && value > 0;
const moneyValue = value => Number.isSafeInteger(value) && value >= 0;
const timestamp = value => typeof value === 'string' && /(?:Z|[+-]\d{2}:\d{2})$/i.test(value) && Number.isFinite(Date.parse(value));
const validPaymentTimes = payment => {
  if (payment.providerOccurredAt != null && !timestamp(payment.providerOccurredAt)) return false;
  if (payment.status !== 'Succeeded') return true;
  return timestamp(payment.paymentTime) && timestamp(payment.confirmedAt)
    && ['Source', 'Confirmation'].includes(payment.paymentTimeBasis)
    && (payment.paymentTimeBasis !== 'Confirmation' || Date.parse(payment.paymentTime) === Date.parse(payment.confirmedAt));
};
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
    || !['Pending', 'Succeeded', 'Failed', 'Expired'].includes(payment.status) || !moneyValue(payment.amount)
    || !validPaymentTimes(payment))) throw invalid();
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

// Giữ riêng mốc ghi nhận, mốc xác nhận và thời điểm do provider cung cấp.
export function invoicePaymentTiming(payment) {
  if (payment.status !== 'Succeeded') return [];
  const format = value => `${new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23',
  }).format(new Date(value))} UTC+07:00`;
  const lines = [
    `${payment.paymentTimeBasis === 'Confirmation' ? 'Ghi nhận theo thời điểm hệ thống xác nhận' : 'Ghi nhận theo thời điểm nguồn'}: ${format(payment.paymentTime)}`,
    `Hệ thống xác nhận: ${format(payment.confirmedAt)}`,
  ];
  if (payment.method === 'ZaloPay') {
    lines.push(payment.providerOccurredAt
      ? `Thời điểm giao dịch từ nhà cung cấp: ${format(payment.providerOccurredAt)}`
      : 'Chưa có thời điểm giao dịch từ nhà cung cấp.');
    if (payment.paymentTimeBasis === 'Confirmation')
      lines.push('Mốc ghi nhận dùng thời điểm hệ thống xác nhận; không khẳng định thời điểm giao dịch thực tế.');
  }
  return lines;
}
