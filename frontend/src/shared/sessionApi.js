import { ApiError, apiRequest } from './apiClient.js';

const id = value => Number.isSafeInteger(value) && value > 0;
const date = value => typeof value === 'string' && /(?:Z|[+-]\d{2}:\d{2})$/i.test(value) && Number.isFinite(Date.parse(value));
const invalid = () => new ApiError('Dữ liệu phiên từ máy chủ không hợp lệ. Vui lòng tải lại.', 200);

function validateSessionRow(row) {
  if (!row || !id(row.gamingSessionId) || !id(row.computerId) || !id(row.customerId)
    || typeof row.computerName !== 'string' || !row.computerName.trim() || typeof row.typeName !== 'string'
    || !date(row.startTimeUtc) || (row.endTimeUtc !== null && !date(row.endTimeUtc))
    || (row.lastChargedAtUtc !== null && !date(row.lastChargedAtUtc))
    || !Number.isSafeInteger(row.totalCost) || row.totalCost < 0
    || typeof row.status !== 'string'
    || (row.pricePerHourSnapshot !== null && !id(row.pricePerHourSnapshot))) throw invalid();
  return row;
}

export function sessionReconciliationReason(row) {
  if (!['Active', 'Ended', 'Cancelled'].includes(row.status)
    || (row.status === 'Active') !== (row.endTimeUtc === null)
    || (row.endTimeUtc !== null && Date.parse(row.endTimeUtc) < Date.parse(row.startTimeUtc)))
    return 'Trạng thái và thời điểm của phiên không nhất quán, cần đối soát.';
  if (row.status === 'Active' && row.pricePerHourSnapshot === null)
    return 'Phiên thiếu giá giờ đã lưu, cần đối soát trước khi đóng.';
  return null;
}

export function validateSession(row) {
  const session = validateSessionRow(row);
  if (sessionReconciliationReason(session)) throw invalid();
  return session;
}

export async function getSessionHome(signal) {
  const data = await apiRequest('/client/home', { signal });
  if (!data || !Number.isSafeInteger(data.walletBalance) || data.walletBalance < 0
    || !Object.hasOwn(data, 'session')) throw invalid();
  if (data.session !== null) {
    validateSession(data.session);
    if (data.session.status !== 'Active') throw invalid();
  }
  return data;
}

export async function getSessions(signal) {
  const rows = await apiRequest('/admin/sessions', { signal });
  if (!Array.isArray(rows)) throw invalid();
  return rows.map(validateSessionRow);
}

export async function openSession(computerId, customerId, key, signal) {
  if (!id(computerId) || !id(customerId) || !key) throw invalid();
  return validateSession(await apiRequest('/admin/sessions', {
    method: 'POST', body: { computerId, customerId }, headers: { 'Idempotency-Key': key }, signal,
  }));
}

export async function closeSession(sessionId, key, signal) {
  if (!id(sessionId) || !key) throw invalid();
  return validateSession(await apiRequest(`/admin/sessions/${sessionId}/close`, {
    method: 'POST', body: {}, headers: { 'Idempotency-Key': key }, signal,
  }));
}
