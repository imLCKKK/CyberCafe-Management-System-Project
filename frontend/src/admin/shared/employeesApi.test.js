import assert from 'node:assert/strict';
import { afterEach, beforeEach, test } from 'node:test';
import { markSessionVerified, storeSession } from '../../shared/auth/api.js';
import { employeesApi, newEmployeeCreateKey } from './employeesApi.js';

const originalFetch = globalThis.fetch;
const originalStorage = globalThis.localStorage;
const storage = new Map();
let request;

beforeEach(() => {
  storage.clear();
  globalThis.localStorage = {
    getItem: (key) => storage.get(key) ?? null,
    setItem: (key, value) => storage.set(key, value),
    removeItem: (key) => storage.delete(key),
  };
  const session = { accessToken: 'employee-test-token', accountType: 'employee' };
  storeSession(session);
  markSessionVerified(session);
});

afterEach(() => {
  globalThis.fetch = originalFetch;
  if (originalStorage === undefined) delete globalThis.localStorage;
  else globalThis.localStorage = originalStorage;
});

test('employee mutations send an Idempotency-Key header', async () => {
  const requests = [];
  globalThis.fetch = async (url, options) => {
    requests.push({ url, options });
    return Response.json({ employeeId: 7 });
  };

  const key = newEmployeeCreateKey();
  await employeesApi.update(7, { fullName: 'Updated' }, key);
  await employeesApi.lock(7, key);
  await employeesApi.unlock(7, key);
  await employeesApi.changePassword({ newPassword: 'Updated-password-2026!' }, key);

  assert.equal(requests.length, 4);
  for (const entry of requests)
    assert.equal(entry.options.headers.get('Idempotency-Key'), key);
});

test('employee create sends a valid Idempotency-Key header', async () => {
  globalThis.fetch = async (url, options) => {
    request = { url, options };
    return Response.json({ employeeId: 7 });
  };

  const key = newEmployeeCreateKey();
  await employeesApi.create({ username: '0900000000' }, key);

  assert.match(key, /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  assert.equal(request.url, '/api/employees');
  assert.equal(request.options.headers.get('Idempotency-Key'), key);
  assert.deepEqual(JSON.parse(request.options.body), { username: '0900000000' });
});
