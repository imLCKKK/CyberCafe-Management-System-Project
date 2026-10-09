import React, { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ArrowDown, ArrowUp, CalendarDays, Check, Crosshair, ExternalLink, Gamepad2, Info, RefreshCw, Search, Trophy, Users } from 'lucide-react';
import { useHomeData } from '../home/useHomeData';
import { dateTime, money, registerTournament, statusLabels } from '../home/homeModel';
import { CURRENT_CUSTOMER_ID } from '../home/mockData';
import HomeDialog from '../home/HomeDialog';
import { fetchParticipantStats, filterParticipants, getParticipants, roles, statColumns, opggUrl } from './tournamentModel';
import '../home/home.css';
import './tournaments.css';

const statsApi = import.meta.env.VITE_VALORANT_API_BASE_URL?.trim() || '/api';
const pageSize = 10;
const metric = (value, suffix) => value == null ? '—' : `${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 }).format(value)}${suffix}`;
const keyFor = (row) => `${row.RegistrationId}:${row.RiotId}:${row.RegisteredAt}`;

function StatsSource({ stats, loading }) {
  if (!stats) return <small role="status">{loading ? 'Đang lấy thống kê OP.GG…' : 'Chưa đồng bộ OP.GG'}</small>;
  return <><small>OP.GG · {stats.matches} trận · {stats.scope}</small><small>Lấy dữ liệu: {dateTime(stats.fetchedAt)}</small><small>OP.GG cập nhật: {stats.sourceUpdatedAt ? dateTime(stats.sourceUpdatedAt) : 'Không rõ thời điểm'}</small></>;
}

function OpggNote() {
  return <div className="tour-note"><Info size={17}/><p>Nguồn: <a className="tour-link" href="https://op.gg/valorant" target="_blank" rel="noopener noreferrer">OP.GG</a> · Thống kê Competitive mùa hiện tại của hồ sơ PC công khai; dữ liệu được lưu tạm 10 phút. Thời điểm lấy dữ liệu có thể khác thời điểm OP.GG cập nhật hồ sơ. KDA = (kills + assists) / deaths; HS% = headshots / tổng số phát trúng; Damage / round = tổng sát thương / số vòng. Chỉ số thiếu hoặc mẫu số bằng 0 hiển thị “—”.</p></div>;
}

export function TournamentsPage() {
  const { db, warning, commit } = useHomeData();
  const [params, setParams] = useSearchParams();
  const tournaments = db.Tournaments.filter((row) => row.Status !== 'Draft' && db.Games.some((game) => game.GameId === row.GameId && game.GameName.toUpperCase() === 'VALORANT'));
  const tournament = tournaments.find((row) => row.TournamentId === Number(params.get('tournament'))) || tournaments[0];
  const [query, setQuery] = useState('');
  const [role, setRole] = useState('');
  const [sort, setSort] = useState({ key: 'RegisteredAt', direction: -1 });
  const [page, setPage] = useState(1);
  const [modal, setModal] = useState(false);
  const [form, setForm] = useState({ riotId: '', rankId: '', role: 'Flex' });
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [stats, setStats] = useState({});
  const [syncErrors, setSyncErrors] = useState({});
  const [busy, setBusy] = useState(false);
  const [now, setNow] = useState(() => new Date());
  const request = useRef(null);
  useEffect(() => { const timer = setInterval(() => setNow(new Date()), 30000); return () => clearInterval(timer); }, []);
  useEffect(() => {
    setPage(1); setModal(false); setError(''); setNotice('');
    request.current?.abort(); request.current = null; setBusy(false);
    return () => { request.current?.abort(); request.current = null; };
  }, [tournament?.TournamentId]);
  const participants = getParticipants(db, tournament?.TournamentId).map((row) => ({ ...row, stats: stats[keyFor(row)] }));
  const filtered = filterParticipants(participants, query, role, sort.key, sort.direction);
  const pages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const currentPage = Math.min(page, pages);
  const visible = filtered.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  const mine = participants.find((row) => row.isMe);
  const game = db.Games.find((row) => row.GameId === tournament?.GameId);
  const customer = db.Customers.find((row) => row.CustomerId === CURRENT_CUSTOMER_ID);
  const open = tournament?.Status === 'Open' && now >= new Date(tournament.RegistrationStart) && now < new Date(tournament.RegistrationEnd);
  const canRegister = open && !mine && tournament?.EntryFee === 0 && customer?.Status === 'Active' && !warning;

  function submit(event) {
    event.preventDefault();
    try {
      commit((data) => registerTournament(data, tournament.TournamentId, form));
      setModal(false); setError(''); setNotice('Đã lưu đăng ký trên trình duyệt. Đang tải thống kê OP.GG cho hồ sơ của bạn.');
      setQuery(''); setRole(''); setPage(1);
    } catch (err) { setError(err.message); }
  }

  async function sync(rows = visible, announce = true) {
    if (request.current) return;
    const controller = new AbortController();
    request.current = controller;
    setBusy(true); setError(''); setNotice('');
    let succeeded = 0;
    try {
      // Only sync the visible page, sequentially, to avoid bursts of provider calls.
      for (const row of rows.filter((item) => item.RiotId)) {
        if (controller.signal.aborted) break;
        const key = keyFor(row);
        const timeout = setTimeout(() => controller.abort(), 30000);
        try {
          const result = await fetchParticipantStats(row, { baseUrl: statsApi, signal: controller.signal });
          if (controller.signal.aborted) break;
          setStats((old) => ({ ...old, [key]: result }));
          setSyncErrors((old) => ({ ...old, [key]: '' }));
          succeeded++;
        } catch (err) {
          if (controller.signal.aborted) {
            if (request.current === controller) {
              setError('Đồng bộ quá thời gian chờ. Vui lòng thử lại.');
              setSyncErrors((old) => ({ ...old, [key]: 'Đồng bộ quá thời gian chờ. Bấm Đồng bộ OP.GG để thử lại.' }));
            }
            break;
          }
          setSyncErrors((old) => ({ ...old, [key]: err.message }));
        } finally { clearTimeout(timeout); }
      }
      if (!controller.signal.aborted && announce) setNotice(`Đã cập nhật ${succeeded}/${rows.filter((row) => row.RiotId).length} hồ sơ trên trang này.`);
    } finally {
      if (request.current === controller) { setBusy(false); request.current = null; }
    }
  }

  const visibleKeys = visible.filter((row) => row.RiotId).map(keyFor).join('|');
  const autoSync = useRef(null);
  autoSync.current = () => {
    const missing = visible.filter((row) => row.RiotId && !stats[keyFor(row)] && !syncErrors[keyFor(row)]);
    if (missing.length) void sync(missing, false);
  };
  useEffect(() => {
    if (busy || !visibleKeys) return;
    // Defer until effects settle; React StrictMode cancels the first mount timer.
    // Failed rows require an explicit retry, never an automatic request loop.
    const timer = setTimeout(() => autoSync.current(), 0);
    return () => clearTimeout(timer);
  }, [visibleKeys, busy]);

  function toggleSort(key) { setSort((old) => ({ key, direction: old.key === key ? -old.direction : -1 })); setPage(1); }

  return <div className="tournaments-page">
    <div className="tour-demo">Bản trải nghiệm · Giải đấu và đăng ký được lưu trên trình duyệt</div>
    <div className="tour-heading"><div><span className="tour-eyebrow">TRAM ARENA / VALORANT</span><h1>Giải đấu cộng đồng</h1><p>Tìm đồng đội. Thể hiện kỹ năng. Sẵn sàng thi đấu.</p></div>{tournaments.length > 0 && <select className="tour-select" aria-label="Chọn giải đấu" value={tournament.TournamentId} onChange={(event) => setParams({ tournament: event.target.value })}>{tournaments.map((row) => <option key={row.TournamentId} value={row.TournamentId}>{row.TournamentName}</option>)}</select>}</div>
    {warning && <div className="tour-note tour-error" role="alert">{warning}</div>}
    {error && !modal && <div className="tour-note tour-error" role="alert">{error}</div>}
    {notice && <div className="tour-note tour-notice" role="status"><Check size={17}/>{notice}</div>}
    {!tournament ? <div className="tour-empty"><Trophy size={40}/><h3>Chưa có giải đấu Valorant</h3><p>Lịch thi đấu sẽ xuất hiện khi được công bố.</p></div> : <>
      <section className="tour-hero" aria-labelledby="tour-title"><Trophy className="tour-hero-art" size={240} strokeWidth={.8} aria-hidden="true"/><div className="tour-hero-copy"><span className="tour-status"><span aria-hidden="true">●</span>{open ? 'Đang mở đăng ký' : tournament.Status === 'Open' ? (now < new Date(tournament.RegistrationStart) ? 'Sắp mở đăng ký' : 'Đã hết hạn đăng ký') : statusLabels[tournament.Status] || tournament.Status}</span><h2 id="tour-title">{tournament.TournamentName}</h2><div className="tour-meta"><span><Gamepad2 size={16}/>VALORANT</span><span><Users size={16}/>{game?.TeamSize} vs {game?.TeamSize}</span><span><CalendarDays size={16}/>{dateTime(tournament.StartTime)}</span></div><div className="tour-actions"><button className="tour-button primary" disabled={!canRegister} onClick={() => { setError(''); setModal(true); }}>{mine ? <><Check size={16}/>Bạn đã đăng ký</> : 'Đăng ký tham gia'}</button><span className="tour-muted">{tournament.EntryFee === 0 ? 'Miễn phí tham gia' : `${money(tournament.EntryFee)} / người · Đăng ký tại quầy`}</span></div></div></section>
      <div className="tour-facts"><div className="tour-fact"><span>Người chơi đã đăng ký</span><strong>{participants.length} người</strong></div><div className="tour-fact"><span>Hạn đăng ký</span><strong>{dateTime(tournament.RegistrationEnd)}</strong></div><div className="tour-fact"><span>Hồ sơ đã tải thống kê</span><strong>{participants.filter((row) => row.stats).length} / {participants.length}</strong></div></div>
      <section className="tour-panel" aria-labelledby="participants-title"><div className="tour-section-heading"><div><h2 id="participants-title">Danh sách người đăng ký</h2><p>Thống kê Competitive · Phạm vi và thời gian cập nhật theo từng hồ sơ.</p></div><button className="tour-button" onClick={() => sync()} disabled={busy || !statsApi || !visible.some((row) => row.RiotId)} title={!statsApi ? 'Chưa kết nối dịch vụ thống kê' : 'Cập nhật các hồ sơ trên trang đang xem'}><RefreshCw size={15}/>{busy ? 'Đang đồng bộ…' : 'Đồng bộ OP.GG'}</button></div>
        <div className="tour-toolbar"><label className="tour-search"><Search size={17}/><input aria-label="Tìm người chơi hoặc Riot ID" placeholder="Tìm người chơi, Riot ID…" value={query} onChange={(event) => { setQuery(event.target.value); setPage(1); }}/></label><select className="tour-select" aria-label="Lọc vai trò" value={role} onChange={(event) => { setRole(event.target.value); setPage(1); }}><option value="">Tất cả vai trò</option>{roles.map((item) => <option key={item}>{item}</option>)}</select></div>
        <div className="tour-table-wrap"><table className="tour-table"><caption className="home-sr-only">Người đăng ký và thống kê Valorant của {tournament.TournamentName}</caption><thead><tr><th scope="col">Người chơi / Riot ID</th><th scope="col">Hạng khai báo</th><th scope="col">Vai trò</th>{statColumns.map(([key, label]) => <th scope="col" key={key} aria-sort={sort.key === key ? sort.direction === 1 ? 'ascending' : 'descending' : 'none'}><button onClick={() => toggleSort(key)}>{label}{sort.key === key && (sort.direction === 1 ? <ArrowUp size={12}/> : <ArrowDown size={12}/>)}</button></th>)}<th scope="col">Thống kê / nguồn</th></tr></thead><tbody>{visible.map((row) => <tr key={row.RegistrationId}><td><div className="tour-player"><span className="tour-avatar" aria-hidden="true">{row.name.split(' ').slice(-1)[0].slice(0, 2).toUpperCase()}</span><div><strong>{row.name}{row.isMe && <span className="tour-me">BẠN</span>}</strong><small>{row.RiotId || 'Chưa có Riot ID'} · {statusLabels[row.Status] || row.Status}</small></div></div></td><td>{row.rank}</td><td>{row.PreferredRole}</td>{statColumns.map(([key, , suffix]) => <td key={key} className={`tour-metric ${row.stats?.[key] == null ? 'missing' : ''}`}>{metric(row.stats?.[key], suffix)}</td>)}<td className="tour-sync">{row.RiotId && <a className="tour-link" href={opggUrl(row.RiotId)} target="_blank" rel="noopener noreferrer">Mở OP.GG <ExternalLink size={12}/></a>}<StatsSource stats={row.stats} loading={busy && !syncErrors[keyFor(row)]}/>{syncErrors[keyFor(row)] && <small role="status">{row.stats ? 'Đang hiển thị bản trước. ' : ''}{syncErrors[keyFor(row)]}</small>}</td></tr>)}</tbody></table></div>
        {!visible.length && <div className="tour-empty"><Crosshair size={34}/><h3>{participants.length ? 'Không tìm thấy người chơi' : 'Sẵn sàng ghi tên vào đấu trường?'}</h3><p>{participants.length ? 'Thử tên, Riot ID hoặc vai trò khác.' : 'Hãy là người đầu tiên đăng ký. Hồ sơ của bạn sẽ xuất hiện tại đây.'}</p>{participants.length > 0 && <button className="tour-button" onClick={() => { setQuery(''); setRole(''); }}>Xóa bộ lọc</button>}</div>}
        <div className="tour-pagination"><span>{filtered.length ? `${(currentPage - 1) * pageSize + 1}–${Math.min(currentPage * pageSize, filtered.length)}` : '0'} / {filtered.length} người chơi</span><div><button className="tour-button" disabled={currentPage <= 1} onClick={() => setPage(currentPage - 1)}>Trước</button><span>{currentPage} / {pages}</span><button className="tour-button" disabled={currentPage >= pages} onClick={() => setPage(currentPage + 1)}>Sau</button></div></div>
      </section>
      <OpggNote/>
    </>}
    {modal && tournament && <HomeDialog title="Đăng ký giải đấu" onClose={() => { setModal(false); setError(''); }}><form className="tour-form" onSubmit={submit}><p className="tour-muted">{tournament.TournamentName} · Đăng ký trải nghiệm được lưu trên trình duyệt.</p>{error && <div className="tour-note tour-error" role="alert">{error}</div>}<label>Riot ID<input required maxLength={128} placeholder="Tên người chơi#VN1" value={form.riotId} onChange={(event) => setForm({ ...form, riotId: event.target.value })}/><small>Dùng đúng Riot ID của bạn. Nhập ID chưa liên kết hay xác minh tài khoản Riot.</small></label><label>Hạng hiện tại<select required value={form.rankId} onChange={(event) => setForm({ ...form, rankId: event.target.value })}><option value="">Chọn hạng của bạn</option>{db.GameRanks.filter((rank) => rank.GameId === tournament.GameId).map((rank) => <option key={rank.RankId} value={rank.RankId}>{rank.RankName}</option>)}</select></label><label>Vai trò sở trường<select value={form.role} onChange={(event) => setForm({ ...form, role: event.target.value })}>{roles.map((item) => <option key={item}>{item}</option>)}</select></label><div className="tour-actions"><button type="button" className="tour-button" onClick={() => { setModal(false); setError(''); }}>Để sau</button><button className="tour-button primary" type="submit">Xác nhận đăng ký</button></div></form></HomeDialog>}
  </div>;
}
