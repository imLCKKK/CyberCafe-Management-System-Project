export function parseRiotId(value) {
  if (typeof value !== 'string' || value.length > 128 || /[\u0000-\u001f\u007f]/u.test(value)) throw new Error('Nhập Riot ID theo dạng Tên#TAG.');
  const parts = value.split('#').map((part) => part.trim());
  if (parts.length !== 2 || !parts[0] || !parts[1] || /\s/u.test(parts[1])) throw new Error('Nhập Riot ID theo dạng Tên#TAG.');
  return { gameName: parts[0], tagLine: parts[1], riotId: parts.join('#') };
}

export function opggUrl(value) {
  if (!value) return null;
  const { gameName, tagLine } = parseRiotId(value);
  return `https://op.gg/valorant/profile/${encodeURIComponent(gameName)}-${encodeURIComponent(tagLine)}`;
}

// Normalized response from our OP.GG backend adapter.
export function validateStats(payload, riotId) {
  const invalid = () => { throw new Error('Dữ liệu thống kê trả về không hợp lệ.'); };
  if (!payload || payload.riotId !== riotId || payload.source !== 'opgg'
    || payload.queue !== 'competitive' || typeof payload.scope !== 'string' || !payload.scope.trim()
    || payload.scope.length > 160 || !Number.isFinite(Date.parse(payload.fetchedAt))
    || (payload.matches !== null && (!Number.isInteger(payload.matches) || payload.matches < 0))
    || (payload.sourceUpdatedAt !== null && (typeof payload.sourceUpdatedAt !== 'string' || !Number.isFinite(Date.parse(payload.sourceUpdatedAt))))) invalid();
  for (const key of ['winRate', 'headshotRate', 'damagePerRound', 'kda']) {
    const value = payload[key];
    if (value !== null && (typeof value !== 'number' || !Number.isFinite(value) || value < 0 || (key.endsWith('Rate') && value > 100))) invalid();
    if ((payload.matches === 0 || payload.matches === null) && value !== null) invalid();
  }
  if (!Object.hasOwn(payload, 'rank')) invalid();
  const rank = payload.rank;
  if (rank !== null && (!Number.isInteger(rank.tier) || rank.tier < 0 || rank.tier > 27 || typeof rank.name !== 'string' || !rank.name.trim() || rank.name.length > 50 || !Number.isInteger(rank.division) || rank.division < 0 || rank.division > 3 || rank.label !== `${rank.name}${rank.division ? ` ${rank.division}` : ''}`)) invalid();
  return { ...Object.fromEntries(['riotId', 'source', 'queue', 'scope', 'fetchedAt', 'sourceUpdatedAt', 'matches', 'winRate', 'headshotRate', 'damagePerRound', 'kda'].map((key) => [key, payload[key]])), rank };
}
