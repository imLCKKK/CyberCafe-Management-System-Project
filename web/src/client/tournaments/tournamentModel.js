import { CURRENT_CUSTOMER_ID } from '../home/mockData.js';
export { opggUrl } from '../../../../shared/opgg.js';

export const roles = ['Duelist', 'Initiator', 'Controller', 'Sentinel', 'Flex'];
export const statColumns = [['winRate', 'Tỷ lệ thắng', '%'], ['headshotRate', 'Headshot', '%'], ['damagePerRound', 'Damage / round', ''], ['kda', 'KDA', '']];

export function getParticipants(db, tournamentId) {
  return db.TournamentRegistrations.filter((row) => row.TournamentId === tournamentId && row.Status !== 'Cancelled').map((row) => ({
    ...row,
    name: db.Customers.find((customer) => customer.CustomerId === row.CustomerId)?.FullName || 'Người chơi',
    rank: db.GameRanks.find((rank) => rank.RankId === row.RankId)?.RankName || 'Chưa có hạng',
    isMe: row.CustomerId === CURRENT_CUSTOMER_ID,
  }));
}

export function filterParticipants(rows, query, role, sortKey, direction) {
  const search = query.trim().toLocaleLowerCase('vi');
  return rows.filter((row) => (!search || `${row.name} ${row.RiotId || ''}`.toLocaleLowerCase('vi').includes(search)) && (!role || row.PreferredRole === role)).sort((a, b) => {
    if (sortKey === 'RegisteredAt') return direction * (Date.parse(a.RegisteredAt) - Date.parse(b.RegisteredAt));
    const left = a.stats?.[sortKey], right = b.stats?.[sortKey];
    if (left == null) return right == null ? 0 : 1;
    if (right == null) return -1;
    return direction * (left - right);
  });
}

// Normalized response from our OP.GG backend adapter.
export function validateStats(payload, riotId) {
  const invalid = () => { throw new Error('Dữ liệu thống kê trả về không hợp lệ.'); };
  if (!payload || payload.riotId !== riotId || payload.source !== 'opgg'
    || payload.queue !== 'competitive' || typeof payload.scope !== 'string' || !payload.scope.trim()
    || payload.scope.length > 160 || !Number.isFinite(Date.parse(payload.fetchedAt))
    || !Number.isInteger(payload.matches) || payload.matches < 0
    || (payload.sourceUpdatedAt !== null && (typeof payload.sourceUpdatedAt !== 'string' || !Number.isFinite(Date.parse(payload.sourceUpdatedAt))))) invalid();
  for (const [key] of statColumns) {
    const value = payload[key];
    if (value !== null && (typeof value !== 'number' || !Number.isFinite(value) || value < 0 || (key.endsWith('Rate') && value > 100))) invalid();
    if (payload.matches === 0 && value !== null) invalid();
  }
  return Object.fromEntries(['riotId', 'source', 'queue', 'scope', 'fetchedAt', 'sourceUpdatedAt', 'matches', ...statColumns.map(([key]) => key)].map((key) => [key, payload[key]]));
}

export async function fetchParticipantStats(row, { baseUrl = '/api', signal, fetcher = fetch } = {}) {
  if (!baseUrl) throw new Error('Chưa kết nối dịch vụ OP.GG. Hãy chạy lại npm run dev.');
  const response = await fetcher(`${baseUrl.replace(/\/$/, '')}/valorant/opgg/stats?${new URLSearchParams({ riotId: row.RiotId })}`, {
    headers: { Accept: 'application/json' }, credentials: 'include', signal,
  });
  // Static preview/old dev servers can return 404 or index.html for /api.
  // That is a missing backend, not an OP.GG player lookup failure.
  if (!response.headers.get('content-type')?.includes('application/json')) {
    throw new Error('Trang đang mở chưa kết nối backend OP.GG hoặc cấu hình API chưa đúng. Chạy lại npm run dev và mở đúng địa chỉ Vite hiển thị, rồi tải lại trang.');
  }
  if (!response.ok) {
    const messages = { 403: 'Hồ sơ OP.GG chưa công khai hoặc OP.GG từ chối truy cập.', 404: 'Chưa tìm thấy hồ sơ hoặc dữ liệu thi đấu trên OP.GG.', 429: 'Đã đạt giới hạn truy vấn. Vui lòng thử lại sau.' };
    const body = response.headers.get('content-type')?.includes('application/json') ? await response.json().catch(() => null) : null;
    throw new Error(typeof body?.message === 'string' && body.message.length <= 300 ? body.message : messages[response.status] || 'Dịch vụ OP.GG chưa sẵn sàng. Hãy chạy lại npm run dev và thử lại.');
  }
  return validateStats(await response.json(), row.RiotId);
}
