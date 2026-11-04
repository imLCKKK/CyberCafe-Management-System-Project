const API_BASE_URL = (import.meta.env?.VITE_API_BASE_URL || '/api').replace(/\/+$/, '');
export const AUTH_STORAGE_KEY = 'cybermanagement.auth.v1';

export const AUTH_EXPIRED_EVENT = 'cybermanagement:auth-expired';

let verifiedSession = null;

function sessionsMatch(first, second) {
  return first?.accessToken === second?.accessToken && first?.accountType === second?.accountType;
}

export function readStoredSession() {
  try {
    const stored = JSON.parse(localStorage.getItem(AUTH_STORAGE_KEY) || 'null');
    return stored?.accessToken && ['employee', 'customer'].includes(stored.accountType) ? stored : null;
  } catch {
    return null;
  }
}

export function storeSession(session) {
  if (!sessionsMatch(verifiedSession, session)) verifiedSession = null;
  localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
}

export function clearStoredSession() {
  localStorage.removeItem(AUTH_STORAGE_KEY);
  verifiedSession = null;
}

export function clearStoredSessionIfMatches(session) {
  if (!sessionsMatch(readStoredSession(), session)) return false;
  clearStoredSession();
  return true;
}

export function isSessionVerified(session) {
  return sessionsMatch(verifiedSession, session);
}

export function clearVerifiedSession(session) {
  if (session && !sessionsMatch(verifiedSession, session)) return;
  verifiedSession = null;
}

export function markSessionVerified(session) {
  if (!sessionsMatch(readStoredSession(), session)) return false;
  verifiedSession = { accessToken: session.accessToken, accountType: session.accountType };
  return true;
}

export async function apiRequest(path, options = {}) {
  const { auth, session: requestedSession, allowUnverifiedSession, ...requestOptions } = options;
  const session = auth === false ? null : requestedSession || readStoredSession();
  if (auth !== false && !allowUnverifiedSession && (session || verifiedSession) && !sessionsMatch(session, verifiedSession)) {
    const error = new Error('Phiên đăng nhập đang được xác minh. Vui lòng thử lại.');
    error.code = 'AUTH_SESSION_UNVERIFIED';
    throw error;
  }
  const headers = new Headers(requestOptions.headers || {});
  headers.set('Accept', 'application/json');
  if (requestOptions.body !== undefined) headers.set('Content-Type', 'application/json');
  if (session?.accessToken) headers.set('Authorization', `Bearer ${session.accessToken}`);

  const response = await fetch(`${API_BASE_URL}${path}`, { ...requestOptions, headers });
  const contentType = response.headers.get('content-type') || '';
  const body = response.status === 204 ? null : contentType.includes('application/json')
    ? await response.json()
    : await response.text();

  if (response.status === 401 && session?.accessToken && clearStoredSessionIfMatches(session)) {
    window.dispatchEvent(new CustomEvent(AUTH_EXPIRED_EVENT, { detail: { accountType: session.accountType } }));
  }
  if (!response.ok) {
    const message = body?.message || body?.title || (typeof body === 'string' && body) || `Yêu cầu thất bại (${response.status}).`;
    const error = new Error(message);
    error.status = response.status;
    error.body = body;
    throw error;
  }
  return body;
}

export async function loginEmployee(credentials) {
  return apiRequest('/auth/login', { method: 'POST', auth: false, body: JSON.stringify(credentials) });
}

export async function loginCustomer(credentials) {
  return apiRequest('/auth/customer/login', { method: 'POST', auth: false, body: JSON.stringify(credentials) });
}

export function getCurrentPrincipal(accountType, session = readStoredSession()) {
  return apiRequest(accountType === 'customer' ? '/auth/customer/me' : '/auth/me', {
    session,
    allowUnverifiedSession: true,
  });
}

export function logout(accountType) {
  return apiRequest(accountType === 'customer' ? '/auth/customer/logout' : '/auth/logout', { method: 'POST' });
}