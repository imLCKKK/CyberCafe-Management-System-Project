import test from 'node:test';
import assert from 'node:assert/strict';
import { createDemoData } from '../home/mockData.js';
import { registerTournament, isValidHomeData } from '../home/homeModel.js';
import { fetchParticipantStats, filterParticipants, getParticipants, opggUrl, validateStats } from './tournamentModel.js';

const now = new Date('2026-10-09T12:00:00Z');
const form = { riotId: 'Player #VN1', rankId: '4', role: 'Duelist' };
const payload = { riotId: 'Player#VN1', source: 'opgg', queue: 'competitive', scope: '20 trận gần nhất', fetchedAt: now.toISOString(), sourceUpdatedAt: now.toISOString(), matches: 20, winRate: 55, headshotRate: 25.5, damagePerRound: 145.2, kda: 1.6 };

test('registration shares Home storage, survives reload and never fabricates stats', () => {
  const db = registerTournament(createDemoData(now), 1, form, now);
  const reloaded = JSON.parse(JSON.stringify(db));
  assert.ok(isValidHomeData(reloaded));
  const [row] = getParticipants(reloaded, 1);
  assert.equal(row.RiotId, 'Player#VN1');
  assert.equal(row.rank, 'Gold');
  assert.equal(row.isMe, true);
  assert.equal(row.WinRate, null);
  assert.equal(row.stats, undefined);
  assert.throws(() => registerTournament(db, 1, form, now), /đã đăng ký/);
  assert.deepEqual(getParticipants(db, 2), []);
  reloaded.TournamentRegistrations[0].Status = 'Cancelled';
  assert.deepEqual(getParticipants(reloaded, 1), []);
});

test('filters and sorts numerical metrics, preserving zero and keeping missing values last', () => {
  const rows = [
    { name: 'A', RiotId: 'a#VN', PreferredRole: 'Duelist', stats: { winRate: null } },
    { name: 'B', RiotId: 'b#VN', PreferredRole: 'Flex', stats: { winRate: 0 } },
    { name: 'C', RiotId: 'c#VN', PreferredRole: 'Duelist', stats: { winRate: 80 } },
  ];
  assert.deepEqual(filterParticipants(rows, '', '', 'winRate', -1).map((r) => r.name), ['C', 'B', 'A']);
  assert.deepEqual(filterParticipants(rows, '', '', 'winRate', 1).map((r) => r.name), ['B', 'C', 'A']);
  assert.equal(filterParticipants(rows, 'C#vn', 'Duelist', 'winRate', -1).length, 1);
  assert.equal(filterParticipants(rows, 'C#vn', 'Flex', 'winRate', -1).length, 0);
});

test('rejects wrong identities, malformed metrics, missing scope and invented empty-history zeros', () => {
  assert.deepEqual(validateStats(payload, payload.riotId), payload);
  for (const change of [{ riotId: 'Other#VN' }, { winRate: 101 }, { kda: '1.6' }, { headshotRate: -1 }, { damagePerRound: undefined }, { fetchedAt: 'invalid' }, { scope: '' }, { queue: 'all' }, { matches: 0 }]) {
    assert.throws(() => validateStats({ ...payload, ...change }, payload.riotId));
  }
  assert.equal(validateStats({ ...payload, matches: 0, winRate: null, headshotRate: null, damagePerRound: null, kda: null }, payload.riotId).winRate, null);
});

test('API client uses backend session and handles unavailable, forbidden, rate limited and HTML responses', async () => {
  const row = { RegistrationId: 7, RiotId: payload.riotId };
  await assert.rejects(fetchParticipantStats(row, { baseUrl: '' }), /Chưa kết nối/);
  const result = await fetchParticipantStats(row, { baseUrl: '/api/', fetcher: async (url, options) => {
    assert.equal(url, '/api/valorant/opgg/stats?riotId=Player%23VN1');
    assert.equal(options.credentials, 'include');
    return new Response(JSON.stringify(payload), { headers: { 'content-type': 'application/json' } });
  } });
  assert.equal(result.kda, 1.6);
  for (const status of [401, 403, 404, 429, 500]) await assert.rejects(fetchParticipantStats(row, { baseUrl: '/api', fetcher: async () => new Response('', { status }) }));
  await assert.rejects(fetchParticipantStats(row, { baseUrl: '/api', fetcher: async () => new Response('<html/>', { headers: { 'content-type': 'text/html' } }) }), /cấu hình/);
  assert.equal(opggUrl('A B#VN/1'), 'https://op.gg/valorant/profile/A%20B-VN%2F1');
});

test('a static-server 404 is not mislabeled as a missing OP.GG profile', async () => {
  const row = { RiotId: 'dylannx#1603' };
  for (const status of [200, 404]) {
    await assert.rejects(fetchParticipantStats(row, { fetcher: async () => new Response('<html>Not found</html>', { status, headers: { 'content-type': 'text/html' } }) }), /chưa kết nối backend OP.GG/);
  }
  await assert.rejects(fetchParticipantStats(row, { fetcher: async () => new Response(JSON.stringify({ message: 'OP.GG chưa có thống kê Competitive cho mùa hiện tại.' }), { status: 404, headers: { 'content-type': 'application/json' } }) }), /chưa có thống kê Competitive/);
});
