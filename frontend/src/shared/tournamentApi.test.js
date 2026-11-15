import assert from 'node:assert/strict';
import { test } from 'node:test';
import { clearTournamentCreateKey, createTournament, tournamentCreateKey, tournamentTransitionKey, transitionTournament, updateTournament } from './tournamentApi.js';
import { clearStoredSession, getCurrentPrincipal, markSessionVerified, storeSession } from './auth/api.js';

test('create intent survives retry, is scoped to employee and clears only its own key', () => {
  const rows = new Map();
  const storage = {
    getItem: key => rows.get(key) ?? null,
    setItem: (key, value) => rows.set(key, value),
    removeItem: key => rows.delete(key),
  };
  const request = { gameId: 1, name: 'Test' };
  const first = tournamentCreateKey(1, request, storage);
  assert.deepEqual(tournamentCreateKey(1, request, storage), first);
  assert.deepEqual(tournamentCreateKey('1', request, storage), first);
  assert.deepEqual(tournamentCreateKey('001', request, storage), first);
  assert.notEqual(tournamentCreateKey(2, request, storage).key, first.key);
  clearTournamentCreateKey({ ...first, key: 'other-key' }, storage);
  assert.equal(tournamentCreateKey(1, request, storage).key, first.key);
  clearTournamentCreateKey(first, storage);
  assert.notEqual(tournamentCreateKey(1, request, storage).key, first.key);
});

test('create intent rejects invalid employee IDs before accessing storage', () => {
  const storage = { getItem: () => { throw new Error('storage accessed'); } };
  for (const employeeId of [undefined, null, '', ' ', '1.5', '1e2', '-1', '0', '9007199254740992', 0, -1, 1.5, NaN, Infinity, true, {}]) {
    assert.throws(() => tournamentCreateKey(employeeId, {}, storage), /Không xác định được nhân viên tạo giải/);
  }
});

test('create intent refuses to send when storage is unavailable', () => {
  const storage = { getItem: () => null, setItem: () => { throw new Error('denied'); } };
  assert.throws(() => tournamentCreateKey(1, { gameId: 1 }, storage), /Chưa gửi yêu cầu/);
});

test('transition retries retain their key while different versions or employees use separate keys', () => {
  const rows = new Map();
  const storage = {
    getItem: key => rows.get(key) ?? null,
    setItem: (key, value) => rows.set(key, value),
    removeItem: key => rows.delete(key),
  };
  const request = { toStatus: 'RegistrationOpen', expectedVersion: 'AAAAAAAAAAE=' };
  const first = tournamentTransitionKey('1', 42, request, storage);
  assert.deepEqual(tournamentTransitionKey(1, 42, request, storage), first);
  assert.notEqual(tournamentTransitionKey(1, 42, { ...request, expectedVersion: 'AAAAAAAAAAI=' }, storage).key, first.key);
  assert.notEqual(tournamentTransitionKey(2, 42, request, storage).key, first.key);
  assert.notEqual(tournamentTransitionKey(1, 43, request, storage).key, first.key);
  clearTournamentCreateKey(first, storage);
  assert.notEqual(tournamentTransitionKey(1, 42, request, storage).key, first.key);
});

test('create sends the persisted key and update sends expectedVersion', async () => {
  const original = { fetch: globalThis.fetch, storage: globalThis.localStorage, window: globalThis.window };
  const rows = new Map();
  globalThis.localStorage = {
    getItem: key => rows.get(key) ?? null,
    setItem: (key, value) => rows.set(key, value),
    removeItem: key => rows.delete(key),
  };
  globalThis.window = { dispatchEvent() {} };
  const calls = [];
  globalThis.fetch = async (url, config) => {
    calls.push({ url, config });
    if (url === '/api/auth/me') {
      return Response.json({ employeeId: '1', username: 'admin', role: 'Admin', permissions: ['tournament.manage'] });
    }
    return Response.json({ id: 1 });
  };
  try {
    const session = { accountType: 'employee', accessToken: 'test-token' };
    storeSession(session);
    const principal = await getCurrentPrincipal('employee', session);
    markSessionVerified(session);
    const body = { gameId: 1, name: 'Test' };
    const intent = tournamentCreateKey(principal.employeeId, body);
    await createTournament(body, intent.key);
    await updateTournament(1, { ...body, expectedVersion: 'AAAAAAAAAAE=' });
    const transition = { toStatus: 'RegistrationOpen', expectedVersion: 'AAAAAAAAAAE=' };
    const transitionIntent = tournamentTransitionKey(principal.employeeId, 1, transition);
    await transitionTournament(1, transition, transitionIntent.key);
    assert.equal(calls[1].url, '/api/admin/tournaments');
    assert.equal(calls[1].config.method, 'POST');
    assert.equal(calls[1].config.headers.get('Idempotency-Key'), intent.key);
    assert.deepEqual(JSON.parse(calls[1].config.body), body);
    assert.equal(JSON.parse(calls[2].config.body).expectedVersion, 'AAAAAAAAAAE=');
    assert.equal(calls[3].url, '/api/admin/tournaments/1/transitions');
    assert.equal(calls[3].config.headers.get('Idempotency-Key'), transitionIntent.key);
    assert.deepEqual(JSON.parse(calls[3].config.body), transition);
  } finally {
    clearStoredSession();
    globalThis.fetch = original.fetch;
    if (original.storage === undefined) delete globalThis.localStorage; else globalThis.localStorage = original.storage;
    if (original.window === undefined) delete globalThis.window; else globalThis.window = original.window;
  }
});
