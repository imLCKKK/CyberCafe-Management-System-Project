import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ArrowDown, ArrowUp, CalendarDays, Check, Crosshair, ExternalLink, Gamepad2, Info, Search, Trophy, Users } from 'lucide-react';
import { useAuth } from '../../shared/auth/AuthContext';
import { expireSession, getAccessToken } from '../../shared/auth/api';
import { useRealtime } from '../../shared/realtime/useRealtime';
import { getTournamentRanks, getTournamentRegistrations, getTournaments, refreshRegistrationStats, registerTournament } from '../../shared/tournamentApi';
import { dateTime, money, statusLabels } from '../home/homeModel';
import HomeDialog from '../shared/Dialog';
import { RiotIdFields, OpggPrivacyNote } from '../../shared/RiotIdFields';
import { filterParticipants, roles, statColumns, opggUrl, validRankSelection } from './tournamentModel';
import { RegistrationStats } from '../../shared/RegistrationStats';
import '../home/home.css';
import './tournaments.css';

const pageSize = 10;
const eventNames = ['TournamentChanged', 'RegistrationChanged'];
const metric = (value, suffix) => value == null ? '—' : `${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 }).format(value)}${suffix}`;
function OpggNote() {
  return <div className="tour-note"><Info size={17}/><p>Nguồn: <a className="tour-link" href="https://op.gg/valorant" target="_blank" rel="noopener noreferrer">OP.GG</a> · Thống kê Competitive mùa hiện tại của hồ sơ PC công khai; bản đã đồng bộ được lưu theo đăng ký; adapter có thể trả bản cache trong 10 phút. Thời điểm lấy dữ liệu có thể khác thời điểm OP.GG cập nhật hồ sơ. KDA = (kills + assists) / deaths; HS% = headshots / tổng số phát trúng; Damage / round = tổng sát thương / số vòng. Chỉ số thiếu hoặc mẫu số bằng 0 hiển thị “—”.</p></div>;
}

export function TournamentsPage() {
  const { principal, loading: authLoading, accessToken } = useAuth();
  const [params, setParams] = useSearchParams();
  const [tournaments, setTournaments] = useState([]);
  const [registrations, setRegistrations] = useState([]);
  const [ranks, setRanks] = useState([]);
  const [loading, setLoading] = useState(true);
  const [query, setQuery] = useState('');
  const [role, setRole] = useState('');
  const [sort, setSort] = useState({ key: 'RegisteredAt', direction: -1 });
  const [page, setPage] = useState(1);
  const [modal, setModal] = useState(false);
  const [form, setForm] = useState({ riotId: '', role: 'Flex', rankId: '' });
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [refreshVersion, setRefreshVersion] = useState(0);
  const request = useRef(null);


  const reload = useCallback(() => setRefreshVersion((version) => version + 1), []);
  const realtimeStatus = useRealtime({
    enabled: principal?.type === 'customer',
    accessToken,
    events: eventNames,
    onEvent: reload,
    onReady: reload,
    onAuthFailure: (code) => {
      if (getAccessToken() !== accessToken) return;
      if (code === 401) expireSession({ accessToken, accountType: 'customer' });
      else reload();
    },
  });

  useEffect(() => {
    if (authLoading) return undefined;
    const controller = new AbortController();
    setLoading(true);
    setError('');
    getTournaments('client', controller.signal)
      .then((rows) => { if (!controller.signal.aborted) setTournaments(rows); })
      .catch((cause) => { if (!controller.signal.aborted) setError(cause.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [accessToken, authLoading, principal, refreshVersion]);

  const requestedId = Number(params.get('tournament'));
  const tournament = tournaments.find((row) => row.id === requestedId) || tournaments[0];
  useEffect(() => {
    setPage(1); setModal(false); setError(''); setNotice('');
    setRegistrations([]); setRanks([]);
    return () => { request.current?.abort(); request.current = null; };
  }, [accessToken, tournament?.id]);

  useEffect(() => {
    if (!tournament) return undefined;
    const controller = new AbortController();
    Promise.all([
      getTournamentRegistrations('client', tournament.id, controller.signal),
      getTournamentRanks('client', tournament.gameId, controller.signal),
    ])
      .then(([rows, availableRanks]) => {
        if (!controller.signal.aborted) { setRegistrations(rows); setRanks(availableRanks); }
      })
      .catch((cause) => { if (!controller.signal.aborted) setError(cause.message); });
    return () => controller.abort();
  }, [accessToken, refreshVersion, tournament?.id, tournament?.gameId]);

  const isValorant = tournament?.gameName.trim().toLowerCase() === 'valorant';
  const rankId = validRankSelection(form.rankId, ranks);

  const participants = registrations.map((row) => ({
    RegistrationId: row.id,
    RiotId: row.riotId,
    RegisteredAt: row.registeredAtUtc,
    name: row.customerName,
    rank: row.opggSnapshot?.stats.rank?.label || '—',
    PreferredRole: row.preferredRole,
    Status: row.status,
    stats: row.opggSnapshot?.stats,
    snapshot: row.opggSnapshot,
    isMe: row.isCurrentCustomer,
  }));
  const filtered = filterParticipants(participants, query, role, sort.key, sort.direction);
  const pages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const currentPage = Math.min(page, pages);
  const visible = filtered.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  const mine = participants.find((row) => row.isMe);
  const open = tournament?.status === 'RegistrationOpen'
    && Date.now() >= Date.parse(tournament.registrationStartUtc)
    && Date.now() < Date.parse(tournament.registrationEndUtc);
  const canRegister = open && !mine && ranks.length > 0;

  async function submit(event) {
    event.preventDefault();
    if (!tournament || request.current) return;
    const controller = new AbortController();
    request.current = controller;
    try {
      const saved = await registerTournament(tournament.id, {
        riotId: form.riotId,
        rankId: Number(rankId),
        preferredRole: form.role,
        expectedVersion: tournament.version,
      });
      if (controller.signal.aborted) return;
      setModal(false); setError(''); setNotice('Đăng ký đã được lưu. Danh sách được cập nhật từ máy chủ.');
      setQuery(''); setRole(''); setPage(1); reload();
      if (isValorant) {
        try {
          await refreshRegistrationStats('client', tournament.id, saved.id, controller.signal);
          if (!controller.signal.aborted) reload();
        } catch (cause) {
          if (!controller.signal.aborted) setError(`Đã lưu đăng ký. Đồng bộ OP.GG thất bại: ${cause.message}`);
        }
      }
    } catch (cause) {
      if (!controller.signal.aborted) setError(cause.message);
    } finally {
      if (request.current === controller) request.current = null;
    }
  }

  function toggleSort(key) { setSort((old) => ({ key, direction: old.key === key ? -old.direction : -1 })); setPage(1); }

  return <div className="tournaments-page">
    <div className="tour-heading"><div><span className="tour-eyebrow">NEXORA ARENA / GIẢI ĐẤU</span><h1>Giải đấu cộng đồng</h1><p>Tìm đồng đội. Thể hiện kỹ năng. Sẵn sàng thi đấu.</p></div>{tournaments.length > 0 && <select className="tour-select" aria-label="Chọn giải đấu" value={tournament?.id || ''} onChange={(event) => setParams({ tournament: event.target.value })}>{tournaments.map((row) => <option key={row.id} value={row.id}>{row.name}</option>)}</select>}</div>
    {loading && <p role="status">Đang tải giải đấu từ máy chủ…</p>}
    {error && !modal && <div className="tour-note tour-error" role="alert">{error}</div>}
    {notice && <div className="tour-note tour-notice" role="status"><Check size={17}/>{notice}</div>}
    {!tournament ? <div className="tour-empty"><Trophy size={40}/><h3>Chưa có giải đấu</h3><p>Lịch thi đấu sẽ xuất hiện khi được công bố.</p></div> : <>
      <section className="tour-hero" aria-labelledby="tour-title"><Trophy className="tour-hero-art" size={240} strokeWidth={.8} aria-hidden="true"/><div className="tour-hero-copy"><span className="tour-status"><span aria-hidden="true">●</span>{open ? 'Đang mở đăng ký' : tournament.status === 'RegistrationOpen' ? (Date.now() < Date.parse(tournament.registrationStartUtc) ? 'Sắp mở đăng ký' : 'Đã hết hạn đăng ký') : statusLabels[tournament.status] || tournament.status}</span><h2 id="tour-title">{tournament.name}</h2><div className="tour-meta"><span><Gamepad2 size={16}/>{tournament.gameName}</span><span><Users size={16}/>{tournament.teamSize} vs {tournament.teamSize}</span><span><CalendarDays size={16}/>{dateTime(tournament.startTimeUtc)}</span></div><div className="tour-actions"><button className="tour-button primary" disabled={!canRegister} onClick={() => { setError(''); setModal(true); }}>{mine ? <><Check size={16}/>Bạn đã đăng ký</> : 'Đăng ký tham gia'}</button><span className="tour-muted">{tournament.entryFee === 0 ? 'Miễn phí tham gia' : `${money(tournament.entryFee)} / người · Thanh toán bằng ví`}</span></div></div></section>
      <div className="tour-facts"><div className="tour-fact"><span>Người chơi đã đăng ký</span><strong>{participants.length} người</strong></div><div className="tour-fact"><span>Hạn đăng ký</span><strong>{dateTime(tournament.registrationEndUtc)}</strong></div><div className="tour-fact"><span>Đồng bộ realtime</span><strong>{realtimeStatus === 'connected' ? 'Đã kết nối' : 'Đang cập nhật qua máy chủ'}</strong></div></div>
      <section className="tour-panel" aria-labelledby="participants-title"><div className="tour-section-heading"><div><h2 id="participants-title">Danh sách người đăng ký</h2><p>Thống kê Competitive · Phạm vi và thời gian cập nhật theo từng hồ sơ.</p></div></div>
        <div className="tour-toolbar"><label className="tour-search"><Search size={17}/><input aria-label="Tìm người chơi hoặc Riot ID" placeholder="Tìm người chơi, Riot ID…" value={query} onChange={(event) => { setQuery(event.target.value); setPage(1); }}/></label><select className="tour-select" aria-label="Lọc vai trò" value={role} onChange={(event) => { setRole(event.target.value); setPage(1); }}><option value="">Tất cả vai trò</option>{roles.map((item) => <option key={item}>{item}</option>)}</select></div>
        <div className="tour-table-wrap" tabIndex={0} role="region" aria-label="Danh sách người đăng ký giải đấu"><table className="tour-table"><caption className="home-sr-only">Người đăng ký và thống kê của {tournament.name}</caption><thead><tr><th scope="col">Người chơi / Riot ID</th><th scope="col">Rank OP.GG</th><th scope="col">Vai trò</th>{statColumns.map(([key, label]) => <th scope="col" key={key} aria-sort={sort.key === key ? sort.direction === 1 ? 'ascending' : 'descending' : 'none'}><button onClick={() => toggleSort(key)}>{label}{sort.key === key && (sort.direction === 1 ? <ArrowUp size={12}/> : <ArrowDown size={12}/>)}</button></th>)}<th scope="col">Thống kê / nguồn</th></tr></thead><tbody>{visible.map((row) => <tr key={row.RegistrationId}><td><div className="tour-player"><span className="tour-avatar" aria-hidden="true">{row.name.split(' ').slice(-1)[0].slice(0, 2).toUpperCase()}</span><div><strong>{row.name}{row.isMe && <span className="tour-me">BẠN</span>}</strong><small>{row.RiotId || 'Chưa có Riot ID'} · {statusLabels[row.Status] || row.Status}</small></div></div></td><td>{row.rank}</td><td>{row.PreferredRole}</td>{statColumns.map(([key, , suffix]) => <td key={key} className={`tour-metric ${row.stats?.[key] == null ? 'missing' : ''}`}>{metric(row.stats?.[key], suffix)}</td>)}<td className="tour-sync">{row.RiotId && isValorant && <a className="tour-link" href={opggUrl(row.RiotId)} target="_blank" rel="noopener noreferrer">Mở OP.GG <ExternalLink size={12}/></a>}{isValorant ? <RegistrationStats audience="client" tournamentId={tournament.id} registrationId={row.RegistrationId}
          snapshot={row.snapshot} canRefresh={row.isMe && !!row.RiotId && ['RegistrationOpen', 'RegistrationClosed'].includes(tournament.status)}
          onSaved={(saved) => setRegistrations((rows) => rows.map((item) => item.id === row.RegistrationId ? { ...item, opggSnapshot: saved } : item))}/> : <small>Thống kê chỉ hỗ trợ VALORANT.</small>}</td></tr>)}</tbody></table></div>
        {!visible.length && <div className="tour-empty"><Crosshair size={34}/><h3>{participants.length ? 'Không tìm thấy người chơi' : 'Sẵn sàng ghi tên vào đấu trường?'}</h3><p>{participants.length ? 'Thử tên, Riot ID hoặc vai trò khác.' : 'Hãy là người đầu tiên đăng ký. Hồ sơ của bạn sẽ xuất hiện tại đây.'}</p>{participants.length > 0 && <button className="tour-button" onClick={() => { setQuery(''); setRole(''); }}>Xóa bộ lọc</button>}</div>}
        <div className="tour-pagination"><span>{filtered.length ? `${(currentPage - 1) * pageSize + 1}–${Math.min(currentPage * pageSize, filtered.length)}` : '0'} / {filtered.length} người chơi</span><div><button className="tour-button" disabled={currentPage <= 1} onClick={() => setPage(currentPage - 1)}>Trước</button><span>{currentPage} / {pages}</span><button className="tour-button" disabled={currentPage >= pages} onClick={() => setPage(currentPage + 1)}>Sau</button></div></div>
      </section>
      {isValorant && <OpggNote/>}
    </>}
    {modal && tournament && <HomeDialog title="Đăng ký giải đấu" onClose={() => { setModal(false); setError(''); }}><form className="tour-form" onSubmit={submit}><p className="tour-muted">{tournament.name} · {tournament.entryFee === 0 ? 'Đăng ký miễn phí.' : `Phí ${money(tournament.entryFee)} sẽ được trừ từ ví. Hiện chưa hỗ trợ hủy đăng ký hoặc hoàn phí.`}</p>{error && <div className="tour-note tour-error" role="alert">{error}</div>}<RiotIdFields value={form.riotId} onChange={(riotId) => setForm({ ...form, riotId })}/><OpggPrivacyNote/><label>Hạng hiện tại<select required value={rankId} onChange={(event) => setForm({ ...form, rankId: event.target.value })}>{ranks.map((item) => <option key={item.rankId} value={item.rankId}>{item.rankName}</option>)}</select></label><label>Vai trò sở trường<select value={form.role} onChange={(event) => setForm({ ...form, role: event.target.value })}>{roles.map((item) => <option key={item}>{item}</option>)}</select></label><div className="tour-actions"><button type="button" className="tour-button" onClick={() => { setModal(false); setError(''); }}>Để sau</button><button className="tour-button primary" type="submit" disabled={!ranks.length}>Xác nhận đăng ký</button></div></form></HomeDialog>}
  </div>;
}
