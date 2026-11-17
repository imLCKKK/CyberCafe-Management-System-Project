import { ApiError, apiRequest } from './apiClient.js';

const base = '/customers/me/payments/zalopay';
const id = value => Number.isSafeInteger(value) && value > 0;
const money = value => id(value) && value <= 999999999;
const date = value => typeof value === 'string' && Number.isFinite(Date.parse(value));
const optionalDate = value => value == null || date(value);
const optionalText = value => value == null || typeof value === 'string';
const uuid = value => typeof value === 'string' && value !== '00000000-0000-0000-0000-000000000000'
  && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
const invalid = () => new ApiError('Dữ liệu thanh toán ZaloPay từ máy chủ không hợp lệ. Vui lòng tải lại.', 200);

export function paymentCustomerId(value) {
  const normalized = typeof value === 'string' && /^\d+$/.test(value) ? Number(value) : value;
  if (!id(normalized)) throw invalid();
  return normalized;
}

export function isSandboxCheckoutUrl(value) {
  if (typeof value !== 'string' || !value || value.length > 2048 || /[\u0000-\u0020\u007f]/.test(value)) return false;
  try {
    const url = new URL(value);
    return url.protocol === 'https:' && ['qcgateway.zalopay.vn', 'sbgateway.zalopay.vn', 'sbcheckout.zalopay.vn'].includes(url.hostname)
      && !url.username && !url.password && !url.hash && (!url.port || url.port === '443');
  } catch { return false; }
}

export function paymentIsApplied(payment) {
  return payment?.status === 'Succeeded' && !payment.requiresReconciliation && !payment.settlementFailureReason
    && date(payment.appliedAtUtc)
    && (payment.purpose === 'TopUp' ? id(payment.walletTransactionId) : payment.purpose === 'Order' && id(payment.paymentId));
}

export function paymentNeedsReview(payment) {
  return Boolean(payment?.requiresReconciliation || payment?.settlementFailureReason);
}

export function paymentCanBePaid(payment, enabled, now = Date.now()) {
  return enabled === true && payment?.status === 'Pending' && !paymentNeedsReview(payment)
    && date(payment.expiresAtUtc) && Date.parse(payment.expiresAtUtc) > now && isSandboxCheckoutUrl(payment.paymentUrl);
}

export function paymentShouldPoll(payment, startedAt, now = Date.now()) {
  if (paymentNeedsReview(payment) || paymentIsApplied(payment) || !Number.isFinite(startedAt)) return false;
  if (payment?.status === 'Succeeded') return now < startedAt + 120000;
  return payment?.status === 'Pending' && date(payment.expiresAtUtc)
    && now < Math.min(startedAt + 600000, Date.parse(payment.expiresAtUtc) + 30000);
}

function checkPayment(payment, customerId, purpose) {
  if (!payment || !id(payment.id) || !id(payment.customerId) || !id(payment.sourceId)
    || payment.customerId !== paymentCustomerId(customerId) || !['TopUp', 'Order'].includes(payment.purpose)
    || (purpose && payment.purpose !== purpose) || !money(payment.expectedAmountVnd)
    || !['Pending', 'Succeeded', 'Failed', 'Expired'].includes(payment.status)
    || !date(payment.createdAtUtc) || !date(payment.expiresAtUtc)
    || Date.parse(payment.expiresAtUtc) <= Date.parse(payment.createdAtUtc)
    || typeof payment.merchantTransactionId !== 'string' || !payment.merchantTransactionId.trim()
    || (payment.paymentUrl != null && !isSandboxCheckoutUrl(payment.paymentUrl))
    || !optionalText(payment.providerTransactionId) || !optionalText(payment.failureReason)
    || !optionalText(payment.settlementFailureReason) || typeof payment.requiresReconciliation !== 'boolean'
    || (payment.providerAmountVnd != null && !money(payment.providerAmountVnd))
    || !optionalDate(payment.verifiedAtUtc) || !optionalDate(payment.appliedAtUtc)
    || (payment.walletTransactionId != null && !id(payment.walletTransactionId))
    || (payment.paymentId != null && !id(payment.paymentId))) throw invalid();
  if (payment.status === 'Succeeded' && (!date(payment.verifiedAtUtc) || !payment.providerTransactionId?.trim()
    || (!payment.requiresReconciliation && payment.providerAmountVnd !== payment.expectedAmountVnd))) throw invalid();
  if ((payment.appliedAtUtc != null || payment.walletTransactionId != null || payment.paymentId != null)
    && (!paymentIsApplied(payment) || (payment.purpose === 'TopUp' ? payment.paymentId != null : payment.walletTransactionId != null))) throw invalid();
  return payment;
}

export async function getZaloPayConfig(signal) {
  const config = await apiRequest(`${base}/config`, { signal });
  if (!config || typeof config.enabled !== 'boolean' || !money(config.minAmountVnd)
    || !money(config.maxAmountVnd) || config.minAmountVnd > config.maxAmountVnd) throw invalid();
  return config;
}

export async function getOwnZaloPayPayments({ customerId, purpose, page = 1, pageSize = 20, signal } = {}) {
  const ownId = paymentCustomerId(customerId);
  if (!id(page) || !id(pageSize) || pageSize > 100 || (purpose !== undefined && !['TopUp', 'Order'].includes(purpose))) throw invalid();
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (purpose) query.set('purpose', purpose);
  const result = await apiRequest(`${base}?${query}`, { signal });
  if (!result || result.page !== page || result.pageSize !== pageSize || !Number.isSafeInteger(result.totalCount)
    || result.totalCount < 0 || !Array.isArray(result.items) || result.items.length > pageSize
    || result.totalCount < result.items.length || new Set(result.items.map(row => row?.id)).size !== result.items.length) throw invalid();
  result.items.forEach(row => checkPayment(row, ownId, purpose));
  return result;
}

export async function createZaloPayTopUp({ customerId, amountVnd, idempotencyKey, signal }) {
  const ownId = paymentCustomerId(customerId);
  if (!money(amountVnd) || !uuid(idempotencyKey)) throw invalid();
  const result = checkPayment(await apiRequest(`${base}/topups`, {
    method: 'POST', signal, headers: { 'Idempotency-Key': idempotencyKey }, body: { amountVnd },
  }), ownId, 'TopUp');
  if (result.expectedAmountVnd !== amountVnd) throw invalid();
  return result;
}

export async function getZaloPayPayment({ customerId, paymentId, signal }) {
  const ownId = paymentCustomerId(customerId);
  if (!id(paymentId)) throw invalid();
  const result = checkPayment(await apiRequest(`${base}/${paymentId}`, { signal }), ownId);
  if (result.id !== paymentId) throw invalid();
  return result;
}

export async function syncZaloPayPayment({ customerId, paymentId, signal }) {
  const ownId = paymentCustomerId(customerId);
  if (!id(paymentId)) throw invalid();
  const result = checkPayment(await apiRequest(`${base}/${paymentId}/sync`, { method: 'POST', signal }), ownId);
  if (result.id !== paymentId) throw invalid();
  return result;
}

const intentKey = customerId => `cybermanagement.zalopay-topup.intent.v1:${paymentCustomerId(customerId)}`;
export function readZaloPayTopUpIntent(customerId, storage = globalThis.localStorage) {
  const key = intentKey(customerId);
  let stored;
  try { stored = storage.getItem(key); }
  catch { throw new Error('Không thể lưu yêu cầu nạp trên trình duyệt. Hãy cho phép lưu trữ trước khi nạp.'); }
  if (stored == null) return null;
  try {
    const intent = JSON.parse(stored);
    if (!intent || !money(intent.amountVnd) || !uuid(intent.idempotencyKey)) throw invalid();
    return { amountVnd: intent.amountVnd, idempotencyKey: intent.idempotencyKey };
  } catch { throw new Error('Yêu cầu nạp đã lưu không hợp lệ. Hãy nhờ nhân viên kiểm tra trước khi nạp tiếp.'); }
}

export function beginZaloPayTopUpIntent(customerId, amountVnd, storage = globalThis.localStorage, createUuid = () => globalThis.crypto.randomUUID()) {
  const existing = readZaloPayTopUpIntent(customerId, storage);
  if (existing) return existing;
  if (!money(amountVnd)) throw invalid();
  const intent = { amountVnd, idempotencyKey: createUuid() };
  if (!uuid(intent.idempotencyKey)) throw invalid();
  try { storage.setItem(intentKey(customerId), JSON.stringify(intent)); }
  catch { throw new Error('Không thể lưu yêu cầu nạp. Chưa gửi yêu cầu; hãy cho phép lưu trữ trên trình duyệt.'); }
  return intent;
}

export function clearZaloPayTopUpIntent(customerId, idempotencyKey, storage = globalThis.localStorage) {
  if (readZaloPayTopUpIntent(customerId, storage)?.idempotencyKey === idempotencyKey) storage.removeItem(intentKey(customerId));
}
