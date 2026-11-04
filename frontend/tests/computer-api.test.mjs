import assert from 'node:assert/strict';
import { afterEach, beforeEach, test } from 'node:test';
import { apiRequest, ApiError } from '../src/shared/apiClient.js';
import { STAFF_TOKEN_KEY, getStaffAccessToken, setStaffAccessToken, clearStaffAccessToken, subscribeStaffSession } from '../src/shared/staffSession.js';
import { getComputerData, updateComputerStatus } from '../src/admin/computers/computerApi.js';
import { computerFixture } from './computer-fixture.mjs';

const originalFetch = globalThis.fetch;
const originalStorage = globalThis.sessionStorage;
let fixture;
let requests;
beforeEach(() => {
  fixture = computerFixture();
  requests = [];
  const values = new Map();
  globalThis.sessionStorage = {
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
afterEach(() => { globalThis.fetch = originalFetch; globalThis.sessionStorage = originalStorage; });

test('SQL-shaped arrays preserve IDs, nullable specs, prices and flags without generating demo rows', async () => {
  setStaffAccessToken('test-only-token');
  const result = await getComputerData();
  assert.deepEqual(result.computers.map((row) => row.ComputerId), [101, 102, 103, 104]);
  assert.equal(result.computers[1].HasOpenSession, true);
  assert.equal(result.computers[2].HasOpenMaintenance, true);
  assert.equal(result.computers[3].Status, 'LegacyOffline');
  assert.equal(result.types[0].CPU, null);
  assert.equal(result.types[0].PricePerHour, 12345);
  assert.equal(result.rooms[0].RoomName, 'API Room A');
  assert.equal(requests.length, 3);
  assert.ok(requests.every((request) => request.headers.Authorization === 'Bearer test-only-token'));
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
    setStaffAccessToken('old-token');
    globalThis.fetch = async () => new Response(null, { status });
    await assert.rejects(apiRequest('/auth/me'), (error) => error.status === status);
    assert.equal(getStaffAccessToken(), status === 401 ? null : 'old-token');
  }
});

test('an older failed request cannot delete a new login token', async () => {
  setStaffAccessToken('old-token');
  globalThis.fetch = async () => {
    setStaffAccessToken('new-token');
    return new Response(null, { status: 401 });
  };
  await assert.rejects(apiRequest('/auth/me'), ApiError);
  assert.equal(globalThis.sessionStorage.getItem(STAFF_TOKEN_KEY), 'new-token');
});

test('session subscribers observe login/logout and can unsubscribe', () => {
  const tokens = [];
  const unsubscribe = subscribeStaffSession(() => tokens.push(getStaffAccessToken()));
  setStaffAccessToken('first-session');
  setStaffAccessToken('second-session');
  clearStaffAccessToken();
  unsubscribe();
  setStaffAccessToken('later-session');
  assert.deepEqual(tokens, ['first-session', 'second-session', null]);
});

test('ProblemDetails conflicts surface; internal server details stay hidden', async () => {
  globalThis.fetch = async () => Response.json({ detail: 'Trạng thái máy đã thay đổi.' }, { status: 409 });
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
