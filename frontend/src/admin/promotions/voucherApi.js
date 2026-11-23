import { ApiError, apiRequest } from '../../shared/apiClient.js';

const route = '/admin/vouchers';
const vietnamOffset = 7 * 60 * 60 * 1000;

export const voucherInputDate = (value) => new Date(Date.parse(value) + vietnamOffset).toISOString().slice(0, 23);

function number(value, label, { optional = false, integer = true, min = 0, max = Number.MAX_SAFE_INTEGER } = {}) {
  if (String(value ?? '').trim() === '') {
    if (optional) return null;
    throw new Error(`${label} không được để trống.`);
  }
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < min || parsed > max || (integer && !Number.isSafeInteger(parsed)))
    throw new Error(`${label} không hợp lệ${integer ? ' (cần số nguyên trong giới hạn an toàn)' : ''}.`);
  return parsed;
}

export function voucherPayload(form) {
  const code = form.code.trim();
  const name = form.name.trim();
  if (!/^[a-z0-9_-]{1,50}$/i.test(code))
    throw new Error('Mã voucher phải có 1–50 ký tự: chữ không dấu, số, - hoặc _.');
  if (!name || name.length > 150) throw new Error('Tên voucher phải có 1–150 ký tự.');
  if (!['Percent', 'Fixed'].includes(form.discountType)) throw new Error('Loại giảm không hợp lệ.');
  const percent = form.discountType === 'Percent';
  const discountValue = number(form.discountValue, 'Giá trị giảm', { integer: !percent, min: percent ? 0.01 : 1, max: percent ? 100 : Number.MAX_SAFE_INTEGER });
  if (percent && Math.abs(discountValue * 100 - Math.round(discountValue * 100)) > 1e-8)
    throw new Error('Phần trăm giảm có tối đa 2 chữ số thập phân.');
  const start = new Date(`${form.startDate}+07:00`);
  const end = new Date(`${form.endDate}+07:00`);
  if (!Number.isFinite(start.getTime()) || !Number.isFinite(end.getTime()) || start >= end)
    throw new Error('Ngày bắt đầu phải hợp lệ và trước ngày kết thúc.');
  return {
    code, name, discountType: form.discountType, discountValue,
    minOrderAmount: number(form.minOrderAmount, 'Ngưỡng đơn tối thiểu'),
    maxDiscountAmount: number(form.maxDiscountAmount, 'Trần giảm', { optional: true, min: 1 }),
    startDate: start.toISOString(), endDate: end.toISOString(),
    totalUsageLimit: number(form.totalUsageLimit, 'Hạn mức toàn hệ thống', { optional: true, min: 1, max: 2147483647 }),
    perCustomerUsageLimit: number(form.perCustomerUsageLimit, 'Hạn mức mỗi khách', { optional: true, min: 1, max: 2147483647 }),
    isActive: Boolean(form.isActive),
  };
}

function readVoucher(row) {
  const money = (value) => Number.isSafeInteger(value) && value >= 0;
  const limit = (value) => value === null || (Number.isSafeInteger(value) && value > 0 && value <= 2147483647);
  if (!row || !Number.isSafeInteger(row.id) || row.id <= 0
    || typeof row.code !== 'string' || typeof row.name !== 'string'
    || !['Percent', 'Fixed'].includes(row.discountType)
    || !Number.isFinite(row.discountValue) || row.discountValue <= 0
    || (row.discountType === 'Fixed' ? !money(row.discountValue) : row.discountValue > 100)
    || !money(row.minOrderAmount) || !(row.maxDiscountAmount === null || money(row.maxDiscountAmount))
    || !limit(row.totalUsageLimit) || !limit(row.perCustomerUsageLimit)
    || !Number.isFinite(Date.parse(row.startDate)) || !Number.isFinite(Date.parse(row.endDate))
    || Date.parse(row.startDate) >= Date.parse(row.endDate) || typeof row.isActive !== 'boolean')
    throw new ApiError('Dữ liệu voucher không hợp lệ hoặc vượt giới hạn hiển thị số an toàn.', 200);
  return row;
}

export const voucherApi = {
  async list(signal) {
    const rows = await apiRequest(route, { signal });
    if (!Array.isArray(rows)) throw new ApiError('Danh sách voucher máy chủ trả về không hợp lệ.', 200);
    return rows.map(readVoucher);
  },
  create: (body) => apiRequest(route, { method: 'POST', body }).then(readVoucher),
  update: (id, body) => apiRequest(`${route}/${id}`, { method: 'PUT', body }).then(readVoucher),
  setStatus: (id, isActive) => apiRequest(`${route}/${id}/status`, { method: 'PUT', body: { isActive } }).then(readVoucher),
};
