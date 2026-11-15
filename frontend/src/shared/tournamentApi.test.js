import assert from 'node:assert/strict';
import { test } from 'node:test';
import { clearTournamentCreateKey, createTournament, tournamentCreateKey, updateTournament } from './tournamentApi.js';
import { clearStoredSession, markSessionVerified, storeSession } from './auth/api.js';

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
  assert.notEqual(tournamentCreateKey(2, request, storage).key, first.key);
  clearTournamentCreateKey({ ...first, key: 'other-key' }, storage);
  assert.equal(tournamentCreateKey(1, request, storage).key, first.key);
  clearTournamentCreateKey(first, storage);
  assert.notEqual(tournamentCreateKey(1, request, storage).key, first.key);
});

test('create intent refuses to send when storage is unavailable', () => {
  const storage = { getItem: () => null, setItem: () => { throw new Error('denied'); } };
  assert.throws(() => tournamentCreateKey(1, { gameId: 1 }, storage), /Chưa gửi yêu cầu/);
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
    return Response.json({ id: 1 });
  };
  try {
    const session = { accountType: 'employee', accessToken: 'test-token' };
    storeSession(session);
    markSessionVerified(session);
    const body = { gameId: 1, name: 'Test' };
    const intent = tournamentCreateKey(1, body);
    await createTournament(body, intent.key);
    await updateTournament(1, { ...body, expectedVersion: 'AAAAAAAAAAE=' });
    assert.equal(calls[0].config.headers.get('Idempotency-Key'), intent.key);
    assert.equal(JSON.parse(calls[1].config.body).expectedVersion, 'AAAAAAAAAAE=');
  } finally {
    clearStoredSession();
    globalThis.fetch = original.fetch;
    if (original.storage === undefined) delete globalThis.localStorage; else globalThis.localStorage = original.storage;
    if (original.window === undefined) delete globalThis.window; else globalThis.window = original.window;
  }
});
