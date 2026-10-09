import { opggUrl, parseRiotId } from '../shared/opgg.js';

export class StatsError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}
const changed = () => new StatsError(502, 'Cấu trúc dữ liệu OP.GG đã thay đổi hoặc chưa có thống kê. Vui lòng mở hồ sơ OP.GG để kiểm tra.');
const sameId = (a, b) => a.toLocaleLowerCase('en') === b.toLocaleLowerCase('en');

// Read JSON only. Never evaluate JavaScript supplied by the upstream page.
export function flightRecords(text) {
  const buffer = Buffer.from(text), records = [];
  let cursor = 0;
  while (cursor < buffer.length) {
    const colon = buffer.indexOf(58, cursor);
    if (colon < 0 || !/^[a-f\d]*$/i.test(buffer.subarray(cursor, colon).toString())) break;
    const start = colon + 1;
    // Flight text rows use a byte length and have no trailing newline.
    if (buffer[start] === 84) {
      const comma = buffer.indexOf(44, start);
      const hex = buffer.subarray(start + 1, comma).toString();
      if (comma < 0 || !/^[a-f\d]+$/i.test(hex)) break;
      cursor = comma + 1 + parseInt(hex, 16);
      continue;
    }
    let end = buffer.indexOf(10, start);
    if (end < 0) end = buffer.length;
    if ([91, 123].includes(buffer[start])) {
      try { records.push(JSON.parse(buffer.subarray(start, end).toString('utf8'))); } catch { /* malformed row */ }
    }
    cursor = end + 1;
  }
  return records;
}
function findObject(value, predicate) {
  if (!value || typeof value !== 'object') return null;
  if (!Array.isArray(value) && predicate(value)) return value;
  for (const child of Object.values(value)) {
    const found = findObject(child, predicate);
    if (found) return found;
  }
  return null;
}

export function parseProfile(html, expectedId) {
  let flight = '';
  for (const match of html.matchAll(/self\.__next_f\.push\((\[.*?\])\)<\/script>/gs)) {
    try { const entry = JSON.parse(match[1]); if (entry[0] === 1 && typeof entry[1] === 'string') flight += entry[1]; } catch { /* not a JSON flight record */ }
  }
  const records = flightRecords(flight);
  const profile = findObject(records, (item) => typeof item.gameName === 'string' && typeof item.tagLine === 'string' && typeof item.policy === 'string');
  if (!profile) {
    if (/NEXT_HTTP_ERROR_FALLBACK;404/.test(flight)) throw new StatsError(404, 'Không tìm thấy hồ sơ OP.GG. Kiểm tra Riot ID của bạn.');
    throw changed();
  }
  if (!sameId(`${profile.gameName}#${profile.tagLine}`, expectedId)) throw new StatsError(409, 'Riot ID trên OP.GG đã đổi hoặc không khớp. Hãy kiểm tra lại hồ sơ.');
  if (profile.policy !== 'PUBLIC') throw new StatsError(403, 'Hồ sơ OP.GG chưa công khai. Hãy kiểm tra quyền chia sẻ trên OP.GG.');
  const context = findObject(records, (item) => item.queueId === 'competitive' && typeof item.seasonId === 'string' && Array.isArray(item.seasonOptions));
  const season = context?.seasonOptions.find((item) => item.value === context.seasonId);
  if (!season || !/^[a-f\d-]{36}$/i.test(context.seasonId) || typeof season.label !== 'string') throw changed();
  const scripts = [...html.matchAll(/<script[^>]+src="([^"]+)"/g)].map((match) => match[1]).filter((url) => {
    try { const parsed = new URL(url); return parsed.origin === 'https://c-valorant-web-v2.op.gg' && parsed.pathname.startsWith('/prod/_next/static/chunks/') && parsed.pathname.endsWith('.js'); } catch { return false; }
  });
  // Shared profile chunks appear immediately before the profile page entry point.
  const pageIndex = scripts.findIndex((url) => /\/profile\/.*\/page-/.test(url));
  if (pageIndex < 0) throw changed();
  return { profile, seasonId: context.seasonId, scope: `${season.label} · Competitive`, scripts: scripts.slice(0, pageIndex + 1).reverse() };
}

export function findStatsAction(script) {
  return script.match(/createServerReference\)\("([a-f\d]{40,64})",[^)]{0,180}?"getPlayerStatistics"\)/)?.[1] || null;
}

export function normalizeStatistics(data, context, requestedId, now = new Date()) {
  if (!data || !sameId(`${data.gameName}#${data.tagLine}`, requestedId) || data.platform !== 'pc') throw changed();
  const raw = data.playerStatistics;
  if (!raw) throw new StatsError(404, 'OP.GG chưa có thống kê Competitive cho mùa hiện tại.');
  if (!Number.isSafeInteger(raw.gameCount) || raw.gameCount < 0) throw changed();
  const keys = ['wins', 'draws', 'defeats', 'rounds', 'damage', 'headShots', 'bodyShots', 'legShots', 'kills', 'deaths', 'assists'];
  for (const key of keys) if (raw[key] != null && (!Number.isFinite(raw[key]) || raw[key] < 0)) throw changed();
  if (raw.wins != null && raw.wins > raw.gameCount) throw changed();
  if ([raw.wins, raw.draws, raw.defeats].every(Number.isFinite) && raw.wins + raw.draws + raw.defeats !== raw.gameCount) throw changed();
  const ratio = (numerator, denominator, percent = false) => raw.gameCount > 0 && Number.isFinite(numerator) && Number.isFinite(denominator) && denominator > 0
    ? Math.round(numerator / denominator * (percent ? 100 : 1) * 100) / 100 : null;
  const sum = (...values) => values.every(Number.isFinite) ? values.reduce((a, b) => a + b, 0) : null;
  return {
    riotId: requestedId, source: 'opgg', queue: 'competitive', scope: context.scope,
    fetchedAt: now.toISOString(), sourceUpdatedAt: Number.isFinite(Date.parse(context.profile.lastUpdatedAt)) ? context.profile.lastUpdatedAt : null,
    matches: raw.gameCount,
    winRate: ratio(raw.wins, raw.gameCount, true),
    headshotRate: ratio(raw.headShots, sum(raw.headShots, raw.bodyShots, raw.legShots), true),
    damagePerRound: ratio(raw.damage, raw.rounds),
    kda: ratio(sum(raw.kills, raw.assists), raw.deaths),
  };
}

async function readLimited(response, maxBytes) {
  const reader = response.body?.getReader();
  if (!reader) throw changed();
  const parts = []; let size = 0;
  try {
    while (true) {
      const { value, done } = await reader.read(); if (done) break;
      size += value.byteLength;
      if (size > maxBytes) { await reader.cancel(); throw changed(); }
      parts.push(Buffer.from(value));
    }
  } finally { reader.releaseLock(); }
  return Buffer.concat(parts).toString('utf8');
}

export function createOpggService({ fetcher = fetch, clock = () => Date.now() } = {}) {
  const cache = new Map(), pending = new Map();
  let actionCache = null, attempts = [], active = 0;
  async function request(url, options = {}) {
    const response = await fetcher(url, { ...options, redirect: 'error', signal: AbortSignal.any([AbortSignal.timeout(10000), ...(options.signal ? [options.signal] : [])]) });
    if (!response.ok) {
      const status = [403, 404, 429].includes(response.status) ? response.status : 502;
      const messages = { 403: 'OP.GG từ chối truy cập hồ sơ. Hãy mở hồ sơ để kiểm tra.', 404: 'Không tìm thấy hồ sơ trên OP.GG.', 429: 'OP.GG đang giới hạn truy vấn. Vui lòng thử lại sau.' };
      throw new StatsError(status, messages[status] || 'OP.GG tạm thời không phản hồi. Vui lòng thử lại.');
    }
    return readLimited(response, 4 * 1024 * 1024);
  }
  async function load(riotId) {
    const signal = AbortSignal.timeout(25000);
    const url = opggUrl(riotId);
    const html = await request(url, { signal, headers: { Accept: 'text/html', 'Accept-Language': 'en' } });
    const context = parseProfile(html, riotId);
    const version = context.scripts.join('|');
    let action = actionCache?.version === version ? actionCache.id : null;
    if (!action) {
      // Bounded discovery of the read-only action shipped in the public page.
      for (const scriptUrl of context.scripts.slice(0, 12)) {
        const script = await request(scriptUrl, { signal });
        action = findStatsAction(script);
        if (action) { actionCache = { version, id: action }; break; }
      }
    }
    if (!action) throw changed();
    const body = JSON.stringify([{ gameName: context.profile.gameName, tagLine: context.profile.tagLine, seasonId: context.seasonId, queueId: 'competitive' }]);
    const response = await request(url, { signal, method: 'POST', headers: { 'Next-Action': action, 'Content-Type': 'text/plain;charset=UTF-8', Accept: 'text/x-component', Origin: 'https://op.gg' }, body });
    const data = flightRecords(response).find((item) => item && typeof item.gameName === 'string' && Object.hasOwn(item, 'playerStatistics'));
    return normalizeStatistics(data, context, riotId, new Date(clock()));
  }
  return {
    async get(value) {
      let riotId;
      try { riotId = parseRiotId(value).riotId; } catch (error) { throw new StatsError(400, error.message); }
      const hit = cache.get(riotId);
      if (hit && hit.until > clock()) {
        if (hit.error) throw hit.error;
        return hit.data;
      }
      if (pending.has(riotId)) return pending.get(riotId);
      attempts = attempts.filter((time) => time > clock() - 60000);
      if (active >= 2 || attempts.length >= 30) throw new StatsError(429, 'Đang có nhiều yêu cầu đồng bộ. Vui lòng thử lại sau.');
      attempts.push(clock()); active++;
      const task = (async () => {
        try {
          const data = await load(riotId);
          cache.set(riotId, { data, until: clock() + 10 * 60000 });
          return data;
        } catch (error) {
          const safe = error instanceof StatsError ? error : new StatsError(502, 'Không kết nối được OP.GG hoặc truy vấn quá thời gian chờ. Vui lòng thử lại.');
          cache.set(riotId, { error: safe, until: clock() + 30000 });
          throw safe;
        } finally {
          active--; pending.delete(riotId);
          while (cache.size > 200) cache.delete(cache.keys().next().value);
        }
      })();
      pending.set(riotId, task);
      return task;
    },
  };
}
