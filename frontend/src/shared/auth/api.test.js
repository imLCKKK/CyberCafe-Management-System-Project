import assert from 'node:assert/strict';
import { afterEach, beforeEach, test } from 'node:test';
import {
  AUTH_EXPIRED_EVENT,
  apiRequest,
  clearStoredSession,
  getCurrentPrincipal,
  loginCustomer,
  loginEmployee,
  readStoredSession,
  storeSession,
} from './api.js';

const originalFetch = globalThis.fetch;
const originalStorage = globalThis.localStorage;
const originalWindow = globalThis.window;
const storage = new Map();
let lastRequest;
let dispatchedEvent;

beforeEach(() => {
  storage.clear();
  dispatchedEvent = null;
  globalThis.localStorage = {
    getItem: (key) => storage.get(key) ?? null,
    setItem: (key, value) => storage.set(key, value),
    removeItem: (key) => storage.delete(key),
  };
  globalThis.window = { dispatchEvent: (event) => { dispatchedEvent = event; } };
});

afterEach(() => {
  globalThis.fetch = originalFetch;
  if (originalStorage === undefined) delete globalThis.localStorage;
  else globalThis.localStorage = originalStorage;
  if (originalWindow === undefined) delete globalThis.window;
  else globalThis.window = originalWindow;
});

test('đăng nhập nhân viên gửi đúng request và không kèm bearer token đã lưu', async () => {
  globalThis.fetch = async (url, options) => {
    lastRequest = { url, options };
    return Response.json({ accessToken: 'staff-token' });
  };

  const result = await loginEmployee({ username: 'admin', password: 'secret' });

  assert.equal(lastRequest.url, '/api/auth/login');
  assert.equal(lastRequest.options.headers.get('Authorization'), null);
  assert.deepEqual(JSON.parse(lastRequest.options.body), { username: 'admin', password: 'secret' });
  assert.equal(result.accessToken, 'staff-token');
});

test('đăng nhập khách gọi đúng endpoint và gửi số điện thoại', async () => {
  globalThis.fetch = async (url, options) => {
    lastRequest = { url, options };
    return Response.json({ accessToken: 'customer-token' });
  };

  await loginCustomer({ phone: '0900000000', password: 'secret' });

  assert.equal(lastRequest.url, '/api/auth/customer/login');
  assert.deepEqual(JSON.parse(lastRequest.options.body), { phone: '0900000000', password: 'secret' });
});

test('phản hồi 401 xóa token đã lưu và thông báo loại tài khoản', async () => {
  storeSession({ accessToken: 'expired-token', accountType: 'customer' });
  globalThis.fetch = async () => Response.json({ message: 'Token expired.' }, { status: 401 });

  await assert.rejects(getCurrentPrincipal('customer'), { status: 401 });
  assert.equal(readStoredSession(), null);
  assert.equal(dispatchedEvent.type, AUTH_EXPIRED_EVENT);
  assert.deepEqual(dispatchedEvent.detail, { accountType: 'customer' });
});

test('phản hồi 403 được báo lỗi nhưng không xóa phiên hợp lệ', async () => {
  storeSession({ accessToken: 'valid-token', accountType: 'employee' });
  globalThis.fetch = async () => Response.json({ message: 'Forbidden.' }, { status: 403 });

  await assert.rejects(getCurrentPrincipal('employee'), { status: 403, message: 'Forbidden.' });
  assert.equal(readStoredSession().accessToken, 'valid-token');
  assert.equal(dispatchedEvent, null);
});

test('clearStoredSession xóa access token đã lưu', () => {
  storeSession({ accessToken: 'token', accountType: 'employee' });
  clearStoredSession();
  assert.equal(readStoredSession(), null);
});

test('request API dùng chung tự gắn bearer token đã lưu', async () => {
  storeSession({ accessToken: 'staff-token', accountType: 'employee' });
  globalThis.fetch = async (url, options) => {
    lastRequest = { url, options };
    return Response.json({ ok: true });
  };

  await apiRequest('/computers');

  assert.equal(lastRequest.url, '/api/computers');
  assert.equal(lastRequest.options.headers.get('Authorization'), 'Bearer staff-token');
});