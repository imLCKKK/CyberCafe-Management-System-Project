import assert from 'node:assert/strict';
import { afterEach, beforeEach, test } from 'node:test';
import { apiRequest, ApiError } from '../src/shared/apiClient.js';
import { AUTH_STORAGE_KEY, readStoredSession, storeSession, markSessionVerified, clearVerifiedSession } from '../src/shared/auth/api.js';
import { getComputerData, updateComputerStatus, saveComputer, saveComputerType, getMaintenances,
  saveMaintenance, completeMaintenance, deleteComputer } from '../src/admin/computers/computerApi.js';
import { computerFixture } from './computer-fixture.mjs';

const originalFetch = globalThis.fetch;
const originalStorage = globalThis.localStorage;
const originalWindow = globalThis.window;
function signIn(accessToken) {
  const session = { accessToken, accountType: 'employee' };
  storeSession(session);
  markSessionVerified(session);
}
let fixture;
let requests;
beforeEach(() => {
  clearVerifiedSession();
  globalThis.window = new EventTarget();
  fixture = computerFixture();
  requests = [];
  const values = new Map();
  globalThis.localStorage = {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: (key) => values.delete(key),
  };
  globalThis.fetch = async (url, options) => {
    requests.push({ url, ...options });
    const result = url.endsWith('/computer-types') ? fixture.types
      : url.endsWith('/computer-rooms') ? fixture.rooms : fixture.computers;
    return Response.json(result);
  };
});
afterEach(() => { globalThis.fetch = originalFetch; globalThis.localStorage = originalStorage; globalThis.window = originalWindow; clearVerifiedSession(); });

test('SQL-shaped arrays preserve IDs, nullable specs, prices and flags without generating demo rows', async () => {
  signIn('test-only-token');
  const result = await getComputerData();
  assert.deepEqual(result.computers.map((row) => row.ComputerId), [101, 102, 103, 104]);
  assert.equal(result.computers[1].HasOpenSession, true);
  assert.equal(result.computers[2].HasOpenMaintenance, true);
  assert.equal(result.computers[3].Status, 'LegacyOffline');
  assert.equal(result.types[0].CPU, null);
  assert.equal(result.types[0].PricePerHour, 12345);
  assert.equal(result.rooms[0].RoomName, 'API Room A');
  assert.equal(requests.length, 3);
  assert.ok(requests.every((request) => request.headers.get('Authorization') === 'Bearer test-only-token'));
});

test('empty SQL arrays stay empty', async () => {
  fixture.computers = []; fixture.types = []; fixture.rooms = [];
  assert.deepEqual(await getComputerData(), { computers: [], types: [], rooms: [] });
});

test('invalid arrays, duplicates, flags and missing room/type links fail without demo fallback', async () => {
  for (const corrupt of [
    (data) => { data.computers = {}; },
    (data) => { data.computers.push(data.computers[0]); },
    (data) => { data.computers[0].hasOpenSession = 'false'; },
    (data) => { data.types[0].pricePerHour = '12345'; },
    (data) => { data.rooms = []; },
    (data) => { data.types = []; },
  ]) {
    fixture = computerFixture(); corrupt(fixture);
    await assert.rejects(getComputerData(), ApiError);
  }
});

test('PATCH sends only the desired and expected status, waits for response and uses server record', async () => {
  const record = fixture.computers[0];
  globalThis.fetch = async (url, options) => {
    requests.push({ url, ...options });
    return Response.json({ ...record, status: 'Maintenance' });
  };
  const saved = await updateComputerStatus({ ComputerId: 101, RoomId: 11, ComputerTypeId: 21, Status: 'Available', ComputerName: 'Ignored client value' });
  assert.deepEqual(JSON.parse(requests[0].body), { status: 'Maintenance', expectedStatus: 'Available' });
  assert.equal(requests[0].url, '/api/admin/computers/101/status');
  assert.equal(requests[0].method, 'PATCH');
  assert.equal(saved.ComputerName, 'API-PC-01');
  assert.equal(saved.Status, 'Maintenance');
});

test('PATCH rejects wrong IDs and uncommitted target states', async () => {
  for (const result of [{ ...fixture.computers[0], status: 'Available' }, { ...fixture.computers[0], computerId: 999, status: 'Maintenance' }]) {
    globalThis.fetch = async () => Response.json(result);
    await assert.rejects(updateComputerStatus({ ComputerId: 101, Status: 'Available' }), ApiError);
  }
});

test('401 with no body expires the token; 403 preserves it', async () => {
  for (const status of [403, 401]) {
    signIn('old-token');
    globalThis.fetch = async () => new Response(null, { status });
    await assert.rejects(apiRequest('/auth/me'), (error) => error.status === status);
    assert.equal(readStoredSession()?.accessToken ?? null, status === 401 ? null : 'old-token');
  }
});

test('an older failed request cannot delete a new login token', async () => {
  signIn('old-token');
  globalThis.fetch = async () => {
    signIn('new-token');
    return new Response(null, { status: 401 });
  };
  await assert.rejects(apiRequest('/auth/me'), ApiError);
  assert.equal(JSON.parse(globalThis.localStorage.getItem(AUTH_STORAGE_KEY)).accessToken, 'new-token');
});

test('ProblemDetails conflicts surface; internal server details stay hidden', async () => {
  globalThis.fetch = async () => Response.json({ detail: 'Trạng thái máy đã thay đổi.' }, {
    status: 409, headers: { 'Content-Type': 'application/problem+json' },
  });
  await assert.rejects(apiRequest('/admin/computers/101/status'), (error) => error.status === 409 && error.message.includes('đã thay đổi'));
  globalThis.fetch = async () => Response.json({ detail: 'SQL secret connection detail' }, { status: 500 });
  await assert.rejects(apiRequest('/admin/computers'), (error) => error.status === 500 && !error.message.includes('secret'));
});

test('network/invalid JSON fail explicitly, cancellation is preserved', async () => {
  globalThis.fetch = async () => { throw new TypeError('Failed to fetch'); };
  await assert.rejects(getComputerData(), (error) => error.status === 0);
  globalThis.fetch = async () => new Response('<html>proxy failure</html>', { status: 200 });
  await assert.rejects(apiRequest('/admin/computers'), ApiError);
  globalThis.fetch = async () => { throw new DOMException('Aborted', 'AbortError'); };
  await assert.rejects(apiRequest('/admin/computers'), (error) => error.name === 'AbortError');
});
test('computer requests wait for the shared session to be verified', async () => {
  storeSession({ accessToken: 'unverified-token', accountType: 'employee' });
  await assert.rejects(getComputerData(), (error) => error.code === 'AUTH_SESSION_UNVERIFIED');
  assert.equal(requests.length, 0);
});

test('computer CRUD sends metadata without injected status, flags or financial data', async () => {
  const row = fixture.computers[0];
  let sent;
  globalThis.fetch = async (url, options) => { sent = { url, ...options }; return Response.json(row); };
  const saved = await saveComputer(101, { ComputerName: ' API-PC-01 ', RoomId: '11', ComputerTypeId: '21',
    Status: 'Occupied', HasOpenSession: true, WalletBalance: 1 });
  assert.equal(saved.ComputerId, 101);
  assert.equal(sent.method, 'PUT');
  assert.deepEqual(JSON.parse(sent.body), { computerName: 'API-PC-01', roomId: 11, computerTypeId: 21 });
  globalThis.fetch = async () => Response.json({ ...row, computerId: 102 });
  await assert.rejects(saveComputer(101, saved), ApiError);
  globalThis.fetch = async () => new Response(null, { status: 204 });
  await deleteComputer(101);
});

test('type updates normalize optional specs and preserve server ID/price', async () => {
  let sent;
  globalThis.fetch = async (_url, options) => { sent = JSON.parse(options.body); return Response.json(fixture.types[0]); };
  const result = await saveComputerType(21, { TypeName: ' API Standard ', PricePerHour: '12345', CPU: ' ', RAM: ' 32 GB ' });
  assert.equal(result.PricePerHour, 12345);
  assert.deepEqual(sent, { typeName: 'API Standard', pricePerHour: 12345, cpu: null, ram: '32 GB', gpu: null, description: null });
  globalThis.fetch = async () => Response.json({ ...fixture.types[0], pricePerHour: '12345' });
  await assert.rejects(saveComputerType(21, { TypeName: 'Type', PricePerHour: 12345 }), ApiError);
});

test('maintenance rejects another machine record and malformed completion rather than reporting success', async () => {
  const record = { maintenanceId: 8, computerId: 101, employeeId: 7, description: 'Clean',
    startTime: '2026-11-14T10:00:00Z', endTime: null, status: 'InProgress' };
  let sent;
  globalThis.fetch = async (_url, options) => { sent = JSON.parse(options.body); return Response.json(record); };
  await saveMaintenance(101, null, ' Clean ');
  assert.deepEqual(sent, { description: 'Clean' });
  globalThis.fetch = async () => Response.json(record);
  await assert.rejects(completeMaintenance(101, 8), ApiError);
  globalThis.fetch = async () => Response.json([{ ...record, computerId: 102 }]);
  await assert.rejects(getMaintenances(101), ApiError);
  globalThis.fetch = async () => Response.json([{ ...record, startTime: 'invalid' }]);
  await assert.rejects(getMaintenances(101), ApiError);
  globalThis.fetch = async () => Response.json({ ...record, endTime: '2026-11-14T11:00:00Z', status: 'Completed' });
  assert.equal((await completeMaintenance(101, 8)).status, 'Completed');
});
