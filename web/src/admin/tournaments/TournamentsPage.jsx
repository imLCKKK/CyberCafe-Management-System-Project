import React, { useState } from 'react';
import {
	ArrowDownToLine,
	CalendarDays,
	ChevronDown,
	ChevronLeft,
	ChevronRight,
	CircleDollarSign,
	Clock3,
	Eye,
	Gamepad2,
	Medal,
	MoreHorizontal,
	Pencil,
	Plus,
	Search,
	SlidersHorizontal,
	Swords,
	Trophy,
	Users,
	X,
} from 'lucide-react';
import { games, mockData } from './mockData';
import './tournaments.css';

const pageSize = 6;
const statuses = [
	{ id: 'registration', label: 'Đang mở đăng ký' },
	{ id: 'upcoming', label: 'Sắp diễn ra' },
	{ id: 'live', label: 'Đang thi đấu' },
	{ id: 'completed', label: 'Đã kết thúc' },
	{ id: 'cancelled', label: 'Đã hủy' },
];

const emptyTournament = {
	name: '',
	gameId: String(games[0].id),
	registrationStart: '',
	registrationEnd: '',
	startTime: '',
	entryFee: '0',
	status: 'upcoming',
};

const money = (amount) => new Intl.NumberFormat('vi-VN', {
	style: 'currency',
	currency: 'VND',
	maximumFractionDigits: 0,
}).format(amount || 0);

const dateTime = (value) => new Intl.DateTimeFormat('vi-VN', {
	day: '2-digit',
	month: '2-digit',
	year: 'numeric',
	hour: '2-digit',
	minute: '2-digit',
}).format(new Date(value));

const registrationPeriod = (tournament) => `${dateTime(tournament.registrationStart)} - ${dateTime(tournament.registrationEnd)}`;

const statusLabel = (status) => statuses.find((item) => item.id === status)?.label || status;

const performanceSummary = (registration) => [
	registration.winRate == null ? 'WR —' : `WR ${registration.winRate.toFixed(1)}%`,
	registration.hoursPlayed == null ? null : `${registration.hoursPlayed} giờ`,
	registration.skillScore == null ? null : `Skill ${registration.skillScore.toFixed(1)}`,
].filter(Boolean).join(' · ');

function dateTimeInput(date) {
	const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
	return local.toISOString().slice(0, 16);
}

function getNewTournamentForm() {
	const now = new Date();
	const tomorrow = new Date(now);
	tomorrow.setDate(now.getDate() + 1);
	const nextDay = new Date(now);
	nextDay.setDate(now.getDate() + 2);
	return {
		...emptyTournament,
		registrationStart: dateTimeInput(now),
		registrationEnd: dateTimeInput(tomorrow),
		startTime: dateTimeInput(nextDay),
	};
}

function TournamentModal({ mode, tournament, onClose, onSave }) {
	const initialForm = mode === 'create'
		? getNewTournamentForm()
		: { ...tournament, gameId: String(tournament.gameId), entryFee: String(tournament.entryFee) };
	const [form, setForm] = useState(initialForm);
	const isForm = mode === 'create' || mode === 'edit';
	const title = mode === 'create' ? 'Tạo giải đấu' : mode === 'edit' ? 'Chỉnh sửa giải đấu' : tournament.name;

	function updateField(event) {
		const { name, value } = event.target;
		setForm((current) => ({ ...current, [name]: value }));
	}

	function handleSubmit(event) {
		event.preventDefault();
		const selectedGame = games.find((game) => game.id === Number(form.gameId));
		onSave({
			...form,
			gameId: Number(form.gameId),
			gameName: selectedGame.name,
			teamSize: selectedGame.teamSize,
			entryFee: Number(form.entryFee),
		});
	}

	return (
		<div className="tournament-backdrop" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
			<section className={`tournament-modal ${isForm ? 'tournament-form-modal' : 'tournament-detail-modal'}`} role="dialog" aria-modal="true" aria-labelledby="tournament-modal-title">
				<div className="tournament-modal-heading">
					<div>
						<span className="tournament-kicker">ĐẤU TRƯỜNG NET CAFE</span>
						<h2 id="tournament-modal-title">{title}</h2>
					</div>
					<button className="tournament-icon-button" type="button" aria-label="Đóng" onClick={onClose}><X size={19} /></button>
				</div>

				{isForm ? (
					<form onSubmit={handleSubmit}>
						<div className="tournament-form-grid">
							<label className="tournament-field tournament-field-wide">
								<span>Tên giải đấu</span>
								<input name="name" value={form.name} onChange={updateField} placeholder="Ví dụ: Đấu trường cuối tuần" required autoFocus />
							</label>
							<label className="tournament-field">
								<span>Tựa game</span>
								<select name="gameId" value={form.gameId} onChange={updateField}>
									{games.filter((game) => game.active).map((game) => <option key={game.id} value={game.id}>{game.name} · {game.teamSize}v{game.teamSize}</option>)}
								</select>
							</label>
							<label className="tournament-field">
								<span>Phí tham gia (VNĐ)</span>
								<input name="entryFee" type="number" min="0" step="1000" value={form.entryFee} onChange={updateField} required />
							</label>
							<label className="tournament-field">
								<span>Bắt đầu đăng ký</span>
								<input name="registrationStart" type="datetime-local" value={form.registrationStart} onChange={updateField} required />
							</label>
							<label className="tournament-field">
								<span>Đóng đăng ký</span>
												<input name="registrationEnd" type="datetime-local" min={form.registrationStart} value={form.registrationEnd} onChange={updateField} required />
							</label>
							<label className="tournament-field">
								<span>Thời gian thi đấu</span>
												<input name="startTime" type="datetime-local" min={form.registrationEnd} value={form.startTime} onChange={updateField} required />
							</label>
							<label className="tournament-field">
								<span>Trạng thái</span>
								<select name="status" value={form.status} onChange={updateField}>
									{statuses.map((status) => <option key={status.id} value={status.id}>{status.label}</option>)}
								</select>
							</label>
						</div>
						<div className="tournament-form-hint"><Users size={15} /> Quy mô đội được lấy theo tựa game đã chọn.</div>
						<div className="tournament-modal-footer">
							<button className="tournament-button tournament-button-quiet" type="button" onClick={onClose}>Hủy</button>
							<button className="tournament-button tournament-button-primary" type="submit">{mode === 'create' ? 'Tạo giải đấu' : 'Lưu thay đổi'}</button>
						</div>
					</form>
				) : (
					<>
						<div className="tournament-detail-banner">
							<div className="tournament-game-mark"><Gamepad2 size={23} /></div>
							<div className="tournament-banner-copy">
								<span>{tournament.gameName} · {tournament.teamSize}v{tournament.teamSize}</span>
								<h3>{tournament.name}</h3>
							</div>
							<span className={`tournament-status status-${tournament.status}`}>{statusLabel(tournament.status)}</span>
						</div>
						<div className="tournament-detail-metrics">
							<div><Users size={17} /><span>Người đăng ký</span><b>{tournament.registeredCount}</b></div>
							<div><Swords size={17} /><span>{tournament.teamSize === 1 ? 'Vận động viên' : 'Đội đã ghép'}</span><b>{tournament.teamCount}</b></div>
							<div><CircleDollarSign size={17} /><span>Phí tham gia</span><b>{tournament.entryFee ? money(tournament.entryFee) : 'Miễn phí'}</b></div>
						</div>
						<div className="tournament-detail-schedule">
							<div><CalendarDays size={16} /><span>Thi đấu</span><b>{dateTime(tournament.startTime)}</b></div>
							<div><Clock3 size={16} /><span>Thời gian đăng ký</span><b>{registrationPeriod(tournament)}</b></div>
						</div>
						<div className="tournament-detail-columns">
							<section className="tournament-subpanel">
								<div className="tournament-subpanel-heading"><h3>Người đăng ký</h3><span>{tournament.registeredCount}</span></div>
								{tournament.registrations.length ? <div className="tournament-registration-list">
									{tournament.registrations.map((registration) => <div className="tournament-registration" key={`${registration.name}-${registration.registeredAt}`}>
										<div className="tournament-mini-avatar">{registration.name.split(' ').slice(-1)[0][0]}</div>
										<div><b>{registration.name}</b><small>{registration.rank} · {registration.role}</small><small className="tournament-registration-performance">{performanceSummary(registration)}</small></div>
										<span>{registration.status}</span>
									</div>)}
								</div> : <div className="tournament-empty-state">Chưa có đăng ký.</div>}
							</section>
							<section className="tournament-subpanel">
								<div className="tournament-subpanel-heading"><h3>{tournament.teamSize === 1 ? 'Người chơi' : 'Đội thi đấu'}</h3><span>{tournament.teamCount}</span></div>
								{tournament.teams.length ? <div className="tournament-team-list">
									{tournament.teams.map((team) => <div className="tournament-team" key={team.name}>
										<div className="tournament-team-icon"><Medal size={16} /></div>
										<div><b>{team.name}</b><small>{team.memberCount}/{tournament.teamSize} thành viên</small></div>
										<strong>{team.avgSkillScore?.toFixed(1) ?? '—'}</strong>
									</div>)}
								</div> : <div className="tournament-empty-state">Chưa xếp đội.</div>}
							</section>
						</div>
						<div className="tournament-modal-footer">
							<button className="tournament-button tournament-button-quiet" type="button" onClick={onClose}>Đóng</button>
							<button className="tournament-button tournament-button-primary" type="button" onClick={() => onSave(tournament, 'edit')}><Pencil size={15} />Chỉnh sửa giải</button>
						</div>
					</>
				)}
			</section>
		</div>
	);
}

export function TournamentsPage() {
	const [tournaments, setTournaments] = useState(mockData);
	const [query, setQuery] = useState('');
	const [statusFilter, setStatusFilter] = useState('all');
	const [gameFilter, setGameFilter] = useState('all');
	const [page, setPage] = useState(1);
	const [modal, setModal] = useState(null);
	const [menuFor, setMenuFor] = useState(null);

	const filteredTournaments = tournaments
		.filter((tournament) => {
			const searchable = `${tournament.name} ${tournament.gameName}`.toLocaleLowerCase('vi');
			return searchable.includes(query.trim().toLocaleLowerCase('vi'))
				&& (statusFilter === 'all' || tournament.status === statusFilter)
				&& (gameFilter === 'all' || tournament.gameId === Number(gameFilter));
		})
		.sort((a, b) => new Date(a.startTime) - new Date(b.startTime));
	const openCount = tournaments.filter((tournament) => tournament.status === 'registration').length;
	const liveCount = tournaments.filter((tournament) => tournament.status === 'live').length;
	const activeCount = tournaments.filter((tournament) => ['registration', 'upcoming', 'live'].includes(tournament.status)).length;
	const activeRegistrations = tournaments.filter((tournament) => ['registration', 'upcoming', 'live'].includes(tournament.status)).reduce((sum, tournament) => sum + tournament.registeredCount, 0);
	const pageCount = Math.max(1, Math.ceil(filteredTournaments.length / pageSize));
	const visibleTournaments = filteredTournaments.slice((page - 1) * pageSize, page * pageSize);
	const firstResult = filteredTournaments.length ? (page - 1) * pageSize + 1 : 0;
	const lastResult = Math.min(page * pageSize, filteredTournaments.length);

	function updateFilter(setter, value) {
		setter(value);
		setPage(1);
	}

	function saveTournament(tournament, nextMode) {
		if (nextMode === 'edit') {
			setModal({ mode: 'edit', tournament });
			return;
		}
		if (modal.mode === 'create') {
			const nextId = Math.max(0, ...tournaments.map((item) => item.id)) + 1;
			setTournaments((current) => [{ ...tournament, id: nextId, registeredCount: 0, teamCount: 0, registrations: [], teams: [] }, ...current]);
		} else {
			setTournaments((current) => current.map((item) => item.id === tournament.id ? { ...item, ...tournament } : item));
		}
		setModal(null);
	}

	function cancelTournament(tournament) {
		if (window.confirm(`Hủy giải "${tournament.name}"?`)) {
			setTournaments((current) => current.map((item) => item.id === tournament.id ? { ...item, status: 'cancelled' } : item));
			setMenuFor(null);
		}
	}

	function exportTournaments() {
		const headers = ['Mã giải', 'Tên giải', 'Game', 'Bắt đầu đăng ký', 'Đóng đăng ký', 'Thời gian thi đấu', 'Phí tham gia', 'Trạng thái', 'Người đăng ký', 'Số đội'];
		const rows = filteredTournaments.map((tournament) => [tournament.id, tournament.name, tournament.gameName, tournament.registrationStart, tournament.registrationEnd, tournament.startTime, tournament.entryFee, statusLabel(tournament.status), tournament.registeredCount, tournament.teamCount]);
		const csv = [headers, ...rows].map((row) => row.map((value) => `"${String(value).replaceAll('"', '""')}"`).join(',')).join('\n');
		const url = URL.createObjectURL(new Blob(['\ufeff', csv], { type: 'text/csv;charset=utf-8' }));
		const link = document.createElement('a');
		link.href = url;
		link.download = 'danh-sach-giai-dau.csv';
		link.click();
		URL.revokeObjectURL(url);
	}

	return (
		<section className="page tournaments-page">
			<div className="tournaments-content">
				<div className="tournaments-heading">
					<div>
						<div className="tournaments-eyebrow">CỘNG ĐỒNG & THI ĐẤU</div>
						<h2>Giải đấu</h2>
						<p>Lên lịch giải, theo dõi đăng ký và sắp xếp đội thi đấu.</p>
					</div>
					<div className="tournaments-heading-actions">
						<button className="tournament-button tournament-button-outline" type="button" onClick={exportTournaments}><ArrowDownToLine size={16} />Xuất danh sách</button>
						<button className="tournament-button tournament-button-primary" type="button" onClick={() => setModal({ mode: 'create' })}><Plus size={17} />Tạo giải đấu</button>
					</div>
				</div>

				<div className="tournament-stats">
					<article className="tournament-stat"><div className="tournament-stat-icon stat-teal"><Trophy size={19} /></div><span>Giải đang hoạt động</span><strong>{activeCount}</strong><small>Còn lịch hoặc đang nhận đăng ký</small></article>
					<article className="tournament-stat"><div className="tournament-stat-icon stat-lime"><Users size={19} /></div><span>Đang mở đăng ký</span><strong>{openCount}</strong><small>Khách có thể tham gia</small></article>
					<article className="tournament-stat"><div className="tournament-stat-icon stat-amber"><Swords size={19} /></div><span>Đang thi đấu</span><strong>{liveCount}</strong><small>Giải đang diễn ra tại quán</small></article>
					<article className="tournament-stat"><div className="tournament-stat-icon stat-blue"><Gamepad2 size={19} /></div><span>Lượt đăng ký đang hiệu lực</span><strong>{activeRegistrations}</strong><small>Ở các giải chưa kết thúc</small></article>
				</div>

				<section className="tournament-list-panel">
					<div className="tournament-list-heading">
						<div><h3>Lịch thi đấu</h3><p>{filteredTournaments.length} giải đấu phù hợp</p></div>
						<label className="tournament-search"><Search size={17} /><input value={query} onChange={(event) => updateFilter(setQuery, event.target.value)} placeholder="Tìm tên giải hoặc game..." aria-label="Tìm giải đấu" />{query && <button type="button" aria-label="Xóa tìm kiếm" onClick={() => updateFilter(setQuery, '')}><X size={15} /></button>}</label>
					</div>

					<div className="tournament-toolbar">
						<div className="tournament-tabs" role="tablist" aria-label="Lọc trạng thái giải đấu">
							{[['all', 'Tất cả'], ['registration', 'Mở đăng ký'], ['upcoming', 'Sắp diễn ra'], ['live', 'Đang thi đấu'], ['completed', 'Đã kết thúc'], ['cancelled', 'Đã hủy']].map(([value, label]) => <button key={value} type="button" role="tab" aria-selected={statusFilter === value} className={statusFilter === value ? 'selected' : ''} onClick={() => updateFilter(setStatusFilter, value)}>{label}</button>)}
						</div>
						<label className="tournament-game-filter"><SlidersHorizontal size={15} /><span>Game</span><select value={gameFilter} onChange={(event) => updateFilter(setGameFilter, event.target.value)} aria-label="Lọc theo game"><option value="all">Tất cả game</option>{games.map((game) => <option key={game.id} value={game.id}>{game.name}</option>)}</select><ChevronDown size={14} /></label>
					</div>

					<div className="tournament-table-wrap">
						<table className="tournament-table">
							<thead><tr><th>Giải đấu</th><th>Thời gian thi đấu</th><th>Thời gian đăng ký</th><th>Đội / người chơi</th><th>Phí tham gia</th><th>Trạng thái</th><th><span className="sr-only">Thao tác</span></th></tr></thead>
							<tbody>
								{visibleTournaments.map((tournament) => <tr key={tournament.id}>
									<td><button className="tournament-identity" type="button" onClick={() => setModal({ mode: 'view', tournament })}><span className="tournament-game-mark"><Gamepad2 size={17} /></span><span><b>{tournament.name}</b><small>{tournament.gameName} · {tournament.teamSize}v{tournament.teamSize}</small></span></button></td>
									<td><span className="tournament-date-primary">{dateTime(tournament.startTime).split(' ')[0]}</span><small className="tournament-cell-subtext">{dateTime(tournament.startTime).split(' ').slice(1).join(' ')}</small></td>
									<td><span className="tournament-date-primary">{dateTime(tournament.registrationEnd).split(' ')[0]}</span><small className="tournament-cell-subtext">Đóng đăng ký</small></td>
									<td><span className="tournament-count-primary">{tournament.teamCount} <small>{tournament.teamSize === 1 ? 'người chơi' : 'đội'}</small></span><small className="tournament-cell-subtext">{tournament.registeredCount} người đăng ký</small></td>
									<td className="tournament-fee">{tournament.entryFee ? money(tournament.entryFee) : 'Miễn phí'}</td>
									<td><span className={`tournament-status status-${tournament.status}`}><i />{statusLabel(tournament.status)}</span></td>
									<td className="tournament-row-actions"><button className="tournament-icon-button" type="button" aria-label={`Xem ${tournament.name}`} title="Xem chi tiết" onClick={() => setModal({ mode: 'view', tournament })}><Eye size={16} /></button><div className="tournament-menu-wrap"><button className="tournament-icon-button" type="button" aria-label={`Thao tác với ${tournament.name}`} title="Thao tác" onClick={() => setMenuFor(menuFor === tournament.id ? null : tournament.id)}><MoreHorizontal size={18} /></button>{menuFor === tournament.id && <div className="tournament-row-menu"><button type="button" onClick={() => { setModal({ mode: 'edit', tournament }); setMenuFor(null); }}><Pencil size={14} />Chỉnh sửa</button>{!['completed', 'cancelled'].includes(tournament.status) && <button className="tournament-cancel-action" type="button" onClick={() => cancelTournament(tournament)}><X size={14} />Hủy giải đấu</button>}</div>}</div></td>
								</tr>)}
								{!visibleTournaments.length && <tr><td className="tournament-empty" colSpan="7"><Search size={22} /><b>Không tìm thấy giải đấu</b><span>Thử thay đổi từ khóa hoặc bộ lọc.</span></td></tr>}
							</tbody>
						</table>
					</div>
					<footer className="tournament-pagination"><span>Hiển thị <b>{firstResult}–{lastResult}</b> trong <b>{filteredTournaments.length}</b> giải</span><div><button type="button" aria-label="Trang trước" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}><ChevronLeft size={17} /></button><span>Trang <b>{page}</b> / {pageCount}</span><button type="button" aria-label="Trang sau" disabled={page >= pageCount} onClick={() => setPage((current) => current + 1)}><ChevronRight size={17} /></button></div></footer>
				</section>
			</div>
			{modal && <TournamentModal mode={modal.mode} tournament={modal.tournament} onClose={() => setModal(null)} onSave={saveTournament} />}
		</section>
	);
}
