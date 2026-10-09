import test from 'node:test';
import assert from 'node:assert/strict';
import { createOpggService, findStatsAction, flightRecords, normalizeStatistics, parseProfile } from './opgg.js';
import { createStatsMiddleware } from './statsMiddleware.js';
import { opggUrl } from '../shared/opgg.js';

const seasonId = '8102cd81-43a0-d0d7-bd59-47b8fe9bed1b';
const profile = { gameName: 'Player', tagLine: 'VN1', policy: 'PUBLIC', platform: 'pc', lastUpdatedAt: '2026-10-04T10:55:31Z' };
const raw = { gameName: 'Player', tagLine: 'VN1', platform: 'pc', playerStatistics: { gameCount: 48, wins: 25, draws: 1, defeats: 22, rounds: 1012, damage: 166157, headShots: 523, bodyShots: 1497, legShots: 112, kills: 858, deaths: 667, assists: 192 } };
const context = { profile, scope: 'V26 - ACT5 · Competitive' };
const actionId = 'a'.repeat(40);
const script = `createServerReference)("${'b'.repeat(40)}",x.callServer,void 0,x.findSourceMapURL,"getPlayerProfile"),a=(0,x.createServerReference)("${actionId}",x.callServer,void 0,x.findSourceMapURL,"getPlayerStatistics")`;
const cdn = 'https://c-valorant-web-v2.op.gg/prod/_next/static/chunks/';
function html(record = profile) {
  const tree = ['$', 'div', null, { profile: record, children: { queueId: 'competitive', seasonId, seasonOptions: [{ label: 'V26 - ACT5', value: seasonId }] } }];
  // Include a byte-counted text row directly before JSON, as the live page does.
  const text = 'Tiếng Việt';
  const flight = `:HL["style"]\n2:T${Buffer.byteLength(text).toString(16)},${text}3:${JSON.stringify(tree)}\n`;
  return `<script src="${cdn}shared-test.js"></script><script src="${cdn}app/%5Blocale%5D/valorant/profile/%5Bplayer%5D/page-test.js"></script><script>self.__next_f.push(${JSON.stringify([1, flight])})</script>`;
}

test('parses public page identity and season without evaluating script; rejects private and mismatched identity', () => {
  const result = parseProfile(html(), 'Player#VN1');
  assert.equal(result.seasonId, seasonId);
  assert.equal(result.scope, context.scope);
  assert.equal(result.scripts.length, 2);
  assert.throws(() => parseProfile(html({ ...profile, policy: 'PRIVATE' }), 'Player#VN1'), { status: 403 });
  assert.throws(() => parseProfile(html(), 'Other#VN1'), { status: 409 });
  assert.throws(() => parseProfile('<html>login or challenge</html>', 'Player#VN1'), { status: 502 });
  assert.deepEqual(flightRecords('1:{"x":4}\n2:not-json\n'), [{ x: 4 }]);
  assert.equal(findStatsAction(script), actionId, 'must not select the adjacent profile action');
  assert.equal(findStatsAction('createServerReference)("abc",x,"patchPlayerPolicy")'), null);
});

test('calculates all four metrics from aggregate totals with draw included and correct KDA', () => {
  const result = normalizeStatistics(raw, context, 'Player#VN1', new Date('2026-10-09T00:00:00Z'));
  assert.equal(result.winRate, 52.08);
  assert.equal(result.headshotRate, 24.53);
  assert.equal(result.damagePerRound, 164.19);
  assert.equal(result.kda, 1.57);
  assert.equal(result.source, 'opgg');
  assert.notEqual(result.fetchedAt, result.sourceUpdatedAt);
  const missing = normalizeStatistics({ ...raw, playerStatistics: { gameCount: 1, wins: 0, deaths: 0, kills: 1, assists: 0 } }, context, 'Player#VN1');
  assert.equal(missing.winRate, 0);
  assert.equal(missing.kda, null);
  assert.equal(missing.damagePerRound, null);
  assert.equal(missing.headshotRate, null);
  const empty = normalizeStatistics({ ...raw, playerStatistics: { gameCount: 0 } }, context, 'Player#VN1');
  for (const key of ['winRate', 'headshotRate', 'damagePerRound', 'kda']) assert.equal(empty[key], null);
  assert.throws(() => normalizeStatistics({ ...raw, playerStatistics: { ...raw.playerStatistics, wins: 999 } }, context, 'Player#VN1'));
  assert.throws(() => normalizeStatistics(raw, context, 'Wrong#VN1'));
});

test('full adapter flow discovers read-only action, caches results, deduplicates work and refreshes after TTL', async () => {
  let calls = [], now = Date.parse('2026-10-09T00:00:00Z');
  const service = createOpggService({ clock: () => now, fetcher: async (url, options) => {
    calls.push(url);
    assert.equal(options.redirect, 'error');
    if (url.startsWith(cdn)) return new Response(script);
    assert.equal(url, opggUrl('Player#VN1'));
    if (options.method === 'POST') {
      assert.equal(options.headers['Next-Action'], actionId);
      assert.deepEqual(JSON.parse(options.body), [{ gameName: 'Player', tagLine: 'VN1', seasonId, queueId: 'competitive' }]);
      return new Response(`0:{"a":"$@1"}\n1:${JSON.stringify(raw)}\n`);
    }
    return new Response(html());
  } });
  const [a, b] = await Promise.all([service.get('Player#VN1'), service.get('Player#VN1')]);
  assert.deepEqual(a, b);
  assert.equal(calls.length, 3);
  await service.get('Player#VN1'); assert.equal(calls.length, 3);
  now += 600001;
  await service.get('Player#VN1'); assert.equal(calls.length, 5, 'reuse action for unchanged asset version');
});

test('upstream denial is surfaced and briefly cached; no authentication bypass or retry loop', async () => {
  let calls = 0;
  const service = createOpggService({ fetcher: async () => { calls++; return new Response('', { status: 403 }); } });
  await assert.rejects(service.get('Player#VN1'), { status: 403 });
  await assert.rejects(service.get('Player#VN1'), { status: 403 });
  assert.equal(calls, 1);
  await assert.rejects(service.get('https://evil.example'), { status: 400 });
  assert.equal(calls, 1);
});

test('HTTP handler returns normalized JSON, validation errors, and disallows mutation methods', async () => {
  const middleware = createStatsMiddleware({ get: async (id) => {
    assert.equal(id, 'Player#VN1'); return { source: 'opgg' };
  } });
  const response = () => ({ headers: {}, statusCode: 200, setHeader(k, v) { this.headers[k] = v; }, end(body) { this.body = body; } });
  const ok = response();
  await middleware({ url: '/api/valorant/opgg/stats?riotId=Player%23VN1', method: 'GET' }, ok, () => assert.fail());
  assert.equal(JSON.parse(ok.body).source, 'opgg');
  assert.equal(ok.headers['Cache-Control'], 'no-store');
  const denied = response();
  await middleware({ url: '/api/valorant/opgg/stats', method: 'POST' }, denied, () => assert.fail());
  assert.equal(denied.statusCode, 405);
  const invalid = response();
  await createStatsMiddleware()({ url: '/api/valorant/opgg/stats', method: 'GET' }, invalid, () => assert.fail());
  assert.equal(invalid.statusCode, 400);
});
