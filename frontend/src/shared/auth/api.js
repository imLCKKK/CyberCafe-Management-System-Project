const API_BASE_URL = (import.meta.env?.VITE_API_BASE_URL || '/api').replace(/\/+$/, '');
export const AUTH_STORAGE_KEY = 'cybermanagement.auth.v1';

export const AUTH_EXPIRED_EVENT = 'cybermanagement:auth-expired';

export function readStoredSession() {
  try {
    const stored = JSON.parse(localStorage.getItem(AUTH_STORAGE_KEY) || 'null');
    return stored?.accessToken && ['employee', 'customer'].includes(stored.accountType) ? stored : null;
  } catch {
    return null;
  }
}

export function storeSession(session) {
  localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
}

export function clearStoredSession() {
  localStorage.removeItem(AUTH_STORAGE_KEY);
}

export async function apiRequest(path, options = {}) {
  const session = options.auth === false ? null : readStoredSession();
  const headers = new Headers(options.headers || {});
  headers.set('Accept', 'application/json');
  if (options.body !== undefined) headers.set('Content-Type', 'application/json');
  if (session?.accessToken) headers.set('Authorization', `Bearer ${session.accessToken}`);

  const response = await fetch(`${API_BASE_URL}${path}`, { ...options, headers });
  const contentType = response.headers.get('content-type') || '';
  const body = response.status === 204 ? null : contentType.includes('application/json')
    ? await response.json()
    : await response.text();

  if (response.status === 401 && session?.accessToken) {
    clearStoredSession();
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

export function getCurrentPrincipal(accountType) {
  return apiRequest(accountType === 'customer' ? '/auth/customer/me' : '/auth/me');
}

export function logout(accountType) {
  return apiRequest(accountType === 'customer' ? '/auth/customer/logout' : '/auth/logout', { method: 'POST' });
}