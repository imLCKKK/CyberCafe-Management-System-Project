import { ApiError, apiRequest } from './apiClient.js';

export const COUNTER_TOPUP_AMOUNTS = [10000, 20000, 30000, 50000, 100000, 200000, 300000, 500000];
const id = value => Number.isSafeInteger(value) && value > 0;
const money = value => Number.isSafeInteger(value) && value >= 0 && value <= 999999999;
const date = value => typeof value === 'string' && Number.isFinite(Date.parse(value));
const uuid = value => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
const invalid = () => new ApiError('Dữ liệu yêu cầu nạp tiền từ máy chủ không hợp lệ. Vui lòng tải lại.', 200);
const amount = value => COUNTER_TOPUP_AMOUNTS.includes(value);
const validAmounts = value => value && amount(value.amount) && money(value.bonusAmount)
  && money(value.totalAmount) && value.totalAmount === value.amount + value.bonusAmount;
const validRequest = row => validAmounts(row) && id(row.requestId) && id(row.customerId)
  && typeof row.customerName === 'string' && row.customerName.trim()
  && typeof row.customerPhone === 'string' && row.customerPhone.trim() && date(row.createdAtUtc)
  && (row.status === 'Pending'
    ? row.approvedAtUtc === null && row.approvedByEmployeeId === null && row.transactionId === null && row.balanceAfter === null
    : row.status === 'Approved' && date(row.approvedAtUtc) && id(row.approvedByEmployeeId)
      && id(row.transactionId) && Number.isFinite(row.balanceAfter) && row.balanceAfter >= 0);

function checkRow(row, customerId) {
  if (!validRequest(row) || (customerId !== undefined && row.customerId !== customerId)) throw invalid();
  return row;
}
function queryPage(page, pageSize) {
  if (!id(page) || !id(pageSize) || pageSize > 100) throw invalid();
  return new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
}
function checkPage(result, page, pageSize, customerId, status) {
  if (!result || result.page !== page || result.pageSize !== pageSize || !Number.isSafeInteger(result.totalCount)
    || result.totalCount < 0 || !Array.isArray(result.items) || result.items.length > pageSize
    || result.totalCount < result.items.length || new Set(result.items.map(row => row?.requestId)).size !== result.items.length) throw invalid();
  result.items.forEach(row => {
    checkRow(row, customerId);
    if (status && status !== 'All' && row.status !== status) throw invalid();
  });
  return result;
}

export async function getCounterTopUpOptions(signal) {
  const result = await apiRequest('/customers/me/wallet/counter-topups/options', { signal });
  if (!Array.isArray(result) || result.length !== COUNTER_TOPUP_AMOUNTS.length
    || !result.every(validAmounts)
    || result.some((row, index) => row.amount !== COUNTER_TOPUP_AMOUNTS[index])) throw invalid();
  return result;
}
export async function getOwnCounterTopUps({ customerId, page = 1, pageSize = 20, signal } = {}) {
  if (!id(customerId)) throw invalid();
  return checkPage(await apiRequest(`/customers/me/wallet/counter-topups?${queryPage(page, pageSize)}`, { signal }), page, pageSize, customerId);
}
export async function createCounterTopUp({ customerId, amount: cashAmount, idempotencyKey, signal }) {
  if (!id(customerId) || !amount(cashAmount) || !uuid(idempotencyKey)) throw invalid();
  const result = checkRow(await apiRequest('/customers/me/wallet/counter-topups', {
    method: 'POST', signal, headers: { 'Idempotency-Key': idempotencyKey }, body: { amount: cashAmount },
  }), customerId);
  if (result.amount !== cashAmount) throw invalid();
  return result;
}
export async function getAdminCounterTopUps({ page = 1, pageSize = 20, status = 'Pending', signal } = {}) {
  if (!['Pending', 'Approved', 'All'].includes(status)) throw invalid();
  const query = queryPage(page, pageSize);
  query.set('status', status);
  return checkPage(await apiRequest(`/admin/topups?${query}`, { signal }), page, pageSize, undefined, status);
}
export async function approveCounterTopUp(request, cashReceived, signal) {
  checkRow(request);
  if (cashReceived !== true || request.status !== 'Pending') throw new Error('Chỉ xác nhận sau khi đã thu đủ tiền mặt của khách.');
  const result = checkRow(await apiRequest(`/admin/topups/${request.requestId}/approve`, {
    method: 'POST', signal, body: { cashReceived: true, cashAmount: request.amount },
  }), request.customerId);
  if (result.requestId !== request.requestId || result.amount !== request.amount
    || result.bonusAmount !== request.bonusAmount || result.totalAmount !== request.totalAmount
    || result.status !== 'Approved') throw invalid();
  return result;
}

const intentKey = customerId => `cybermanagement.counter-topup.intent.v1:${customerId}`;
export function readCounterTopUpIntent(customerId, storage = globalThis.localStorage) {
  if (!id(customerId)) throw invalid();
  let stored;
  try { stored = storage.getItem(intentKey(customerId)); }
  catch { throw new Error('Không thể lưu khóa gửi lại trên trình duyệt. Hãy cho phép lưu trữ trước khi gửi yêu cầu.'); }
  if (stored === null) return null;
  try {
    const intent = JSON.parse(stored);
    if (!intent || !amount(intent.amount) || !uuid(intent.idempotencyKey)) throw invalid();
    return { amount: intent.amount, idempotencyKey: intent.idempotencyKey };
  } catch { throw new Error('Khóa yêu cầu cũ không hợp lệ. Hãy nhờ nhân viên kiểm tra yêu cầu trước khi nạp tiếp.'); }
}
export function beginCounterTopUpIntent(customerId, cashAmount, storage = globalThis.localStorage, createUuid = () => globalThis.crypto.randomUUID()) {
  const existing = readCounterTopUpIntent(customerId, storage);
  if (existing) return existing;
  if (!amount(cashAmount)) throw invalid();
  const intent = { amount: cashAmount, idempotencyKey: createUuid() };
  if (!uuid(intent.idempotencyKey)) throw invalid();
  try { storage.setItem(intentKey(customerId), JSON.stringify(intent)); }
  catch { throw new Error('Không thể lưu khóa gửi lại. Chưa gửi yêu cầu; hãy cho phép lưu trữ trên trình duyệt.'); }
  return intent;
}
export function clearCounterTopUpIntent(customerId, idempotencyKey, storage = globalThis.localStorage) {
  if (readCounterTopUpIntent(customerId, storage)?.idempotencyKey === idempotencyKey) storage.removeItem(intentKey(customerId));
}
