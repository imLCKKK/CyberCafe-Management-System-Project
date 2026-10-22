import { validateStats } from '../../client/tournaments/tournamentModel.js';
const next = (rows, key) => Math.max(0, ...rows.map((row) => row[key])) + 1;
const check = (condition, message) => { if (!condition) throw new Error(message); };

export function saveTournament(db, form) {
  const id = Number(form.TournamentId);
  const existing = db.Tournaments.find((row) => row.TournamentId === id);
  check(!existing || ['Draft', 'Open', 'Closed'].includes(existing.Status), 'Giải đã chia đội hoặc kết thúc không thể đổi thông tin.');
  const name = String(form.TournamentName || '').trim();
  check(name.length > 0 && name.length <= 100, 'Tên giải cần 1–100 ký tự.');
  const parse = (value) => new Date(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value) ? `${value}:00+07:00` : value);
  const start = parse(form.RegistrationStart), end = parse(form.RegistrationEnd), play = parse(form.StartTime);
  check([start, end, play].every((date) => Number.isFinite(date.getTime())) && start < end && end <= play, 'Thời gian cần theo thứ tự: mở đăng ký < đóng đăng ký ≤ thi đấu.');
  const record = { ...existing, TournamentId: existing?.TournamentId || next(db.Tournaments, 'TournamentId'), TournamentName: name, GameId: Number(form.GameId), CreatedBy: existing?.CreatedBy ?? null, RegistrationStart: start.toISOString(), RegistrationEnd: end.toISOString(), StartTime: play.toISOString(), EntryFee: 0, Status: existing?.Status || 'Draft' };
  check(db.Games.some((game) => game.GameId === record.GameId && game.IsActive), 'Chọn trò chơi đang hoạt động.');
  return { ...db, Tournaments: existing ? db.Tournaments.map((row) => row.TournamentId === id ? record : row) : [...db.Tournaments, record] };
}

export function transitionTournament(db, id, status) {
  const tournament = db.Tournaments.find((row) => row.TournamentId === id);
  const allowed = { Draft: ['Open', 'Cancelled'], Open: ['Closed', 'Cancelled'], Closed: ['Open', 'Cancelled'], TeamsAssigned: ['Finished', 'Cancelled'] };
  check(tournament && allowed[tournament.Status]?.includes(status), 'Chuyển trạng thái giải đấu không hợp lệ.');
  check(status !== 'Cancelled' || tournament.EntryFee === 0, 'Giải thu phí phải hoàn lệ phí trước khi hủy.');
  return { ...db, Tournaments: db.Tournaments.map((row) => row.TournamentId === id ? { ...row, Status: status } : row), TournamentRegistrations: status === 'Cancelled' ? db.TournamentRegistrations.map((row) => row.TournamentId === id ? { ...row, Status: 'Cancelled' } : row) : db.TournamentRegistrations };
}

export function generateTeams(db, id) {
  const tournament = db.Tournaments.find((row) => row.TournamentId === id);
  check(tournament?.Status === 'Closed', 'Đóng đăng ký trước khi chia đội.');
  const size = db.Games.find((game) => game.GameId === tournament.GameId)?.TeamSize;
  const registrations = db.TournamentRegistrations.filter((row) => row.TournamentId === id && row.Status === 'Registered');
  check(Number.isInteger(size) && size > 0 && registrations.length >= size * 2 && registrations.length % size === 0, `Cần ít nhất ${size * 2} người và số người chia hết cho ${size}; không bỏ người đăng ký.`);
  check(new Set(registrations.map((row) => row.CustomerId)).size === registrations.length, 'Danh sách có khách đăng ký trùng, cần kiểm tra trước khi chia đội.');
  check(!db.Teams.some((row) => row.TournamentId === id), 'Giải đã có đội, không thể chia hai lần.');
  const scores = new Map();
  for (const row of registrations) {
    let stats;
    try { stats = validateStats(row.OpggSnapshot, row.RiotId); } catch { /* Rank must come from a successful lookup. */ }
    check(stats?.rank, `Cần đồng bộ rank OP.GG cho ${row.RiotId || `đăng ký #${row.RegistrationId}`} trước khi chia đội.`);
    scores.set(row.RegistrationId, stats.rank.tier);
  }
  const score = (row) => scores.get(row.RegistrationId);
  const sorted = [...registrations].sort((a, b) => score(b) - score(a) || a.RegistrationId - b.RegistrationId);
  const count = sorted.length / size;
  const teams = Array.from({ length: count }, (_, index) => ({ TeamId: next(db.Teams, 'TeamId') + index, TournamentId: id, TeamName: `Đội ${index + 1}`, AvgSkillScore: 0 }));
  const members = sorted.map((row, index) => {
    const round = Math.floor(index / count), column = index % count;
    const team = teams[round % 2 ? count - column - 1 : column];
    team.AvgSkillScore += score(row) / size;
    return { TeamId: team.TeamId, RegistrationId: row.RegistrationId, AssignedRole: row.PreferredRole };
  });
  return { ...db, Tournaments: db.Tournaments.map((row) => row.TournamentId === id ? { ...row, Status: 'TeamsAssigned' } : row), TournamentRegistrations: db.TournamentRegistrations.map((row) => registrations.some((registration) => registration.RegistrationId === row.RegistrationId) ? { ...row, Status: 'Assigned' } : row), Teams: [...db.Teams, ...teams], TeamMembers: [...db.TeamMembers, ...members] };
}

export function registerAdminParticipant(db, tournamentId, account, customer, form, now = new Date()) {
  const tournament = db.Tournaments.find((row) => row.TournamentId === tournamentId);
  check(tournament?.Status === 'Open' && now >= new Date(tournament.RegistrationStart) && now <= new Date(tournament.RegistrationEnd), 'Giải không trong thời gian mở đăng ký.');
  check(tournament.EntryFee === 0 && account?.is_active && customer, 'Chỉ hỗ trợ đăng ký giải miễn phí cho khách đang hoạt động.');
  const riotId = String(form.riotId || '').trim();
  check(/^[^#\u0000-\u001f]{1,100}#[^#\s]{1,20}$/.test(riotId), 'Riot ID cần dạng Tên#TAG.');
  check(['Duelist', 'Initiator', 'Controller', 'Sentinel', 'Flex'].includes(form.role), 'Vai trò không hợp lệ.');
  const existing = db.Customers.find((row) => row.Phone === account.phone);
  const customerId = existing?.CustomerId || next(db.Customers, 'CustomerId');
  check(!db.TournamentRegistrations.some((row) => row.TournamentId === tournamentId && row.Status !== 'Cancelled' && (row.CustomerId === customerId || row.RiotId?.toLowerCase() === riotId.toLowerCase())), 'Khách hoặc Riot ID đã đăng ký giải này.');
  const record = { RegistrationId: next(db.TournamentRegistrations, 'RegistrationId'), TournamentId: tournamentId, CustomerId: customerId, RankId: null, PreferredRole: form.role, RiotId: riotId, WinRate: null, HoursPlayed: null, SkillScore: null, Status: 'Registered', RegisteredAt: now.toISOString() };
  return { ...db, Customers: existing ? db.Customers : [...db.Customers, { CustomerId: customerId, FullName: account.display_name, Username: account.phone, Phone: account.phone, Email: '', WalletBalance: customer.wallet_balance, Status: 'Active', CreatedAt: account.created_at }], TournamentRegistrations: [...db.TournamentRegistrations, record] };
}
