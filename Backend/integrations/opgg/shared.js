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
