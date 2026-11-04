// Contract for S1-D-2: persist only the access token; identity/permissions come from /auth/me.
export const STAFF_TOKEN_KEY = 'nexora.staff.accessToken';
const listeners = new Set();
let sessionReason = null;

export const getStaffSessionReason = () => sessionReason;

export function subscribeStaffSession(listener) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function getStaffAccessToken() {
  try {
    return globalThis.sessionStorage?.getItem(STAFF_TOKEN_KEY) || null;
  } catch {
    return null;
  }
}

export function setStaffAccessToken(token) {
  if (typeof token !== 'string' || !token.trim()) throw new Error('Token đăng nhập không hợp lệ.');
  globalThis.sessionStorage.setItem(STAFF_TOKEN_KEY, token);
  sessionReason = null;
  for (const listener of listeners) listener();
}

export function clearStaffAccessToken(reason = null) {
  sessionReason = reason;
  try {
    globalThis.sessionStorage?.removeItem(STAFF_TOKEN_KEY);
  } catch {
    // A request without usable storage will require login again.
  }
  for (const listener of listeners) listener();
}
