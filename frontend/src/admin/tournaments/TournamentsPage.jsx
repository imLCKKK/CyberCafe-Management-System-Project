import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Plus, Trophy, Users } from 'lucide-react';
import { useAuth } from '../../shared/auth/AuthContext';
import { getAccessToken, expireSession } from '../../shared/auth/api';
import { useRealtime } from '../../shared/realtime/useRealtime';
import {
  clearTournamentCreateKey, createTournament, getTournamentGames, getTournamentRegistrations, getTournaments,
  tournamentCreateKey, tournamentTransitionKey, transitionTournament, updateTournament,
} from '../../shared/tournamentApi';
import { AdminPage, DetailDialog, Filter, FormDialog, Notice, Panel, Row, Status, Table, Toolbar, StatCard } from '../shared/AdminUI';
import { dateTime, matches, money } from '../shared/adminModel';

const hourOffset = 7 * 60 * 60 * 1000;
const inputDate = (value) => new Date(Date.parse(value) + hourOffset).toISOString().slice(0, 16);
const utcDate = (value) => new Date(`${value}:00+07:00`).toISOString();
const eventNames = ['TournamentChanged', 'RegistrationChanged'];

export function TournamentsPage() {
  const { principal, loading: authLoading, accessToken } = useAuth();
  const [tournaments, setTournaments] = useState([]);
  const [games, setGames] = useState([]);
  const [registrations, setRegistrations] = useState([]);
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('all');
  const [editing, setEditing] = useState(undefined);
  const [selectedId, setSelectedId] = useState(null);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [refreshVersion, setRefreshVersion] = useState(0);

  const reload = useCallback(() => setRefreshVersion((version) => version + 1), []);
  const connectionStatus = useRealtime({
    enabled: principal?.type === 'employee' && principal.permissions?.includes('tournament.view'),
    accessToken,
    events: eventNames,
    onEvent: reload,
    onReady: reload,
    onAuthFailure: (code) => {
      if (getAccessToken() !== accessToken) return;
      if (code === 401) expireSession({ accessToken, accountType: 'employee' });
      else reload();
    },
  });

  useEffect(() => {
    if (authLoading) return undefined;
    const controller = new AbortController();
    setLoading(true);
    setError('');
    Promise.all([getTournaments('admin', controller.signal), getTournamentGames('admin', controller.signal)])
      .then(([rows, availableGames]) => {
        setTournaments(rows);
        setGames(availableGames);
        setSelectedId((current) => current && rows.some((row) => row.id === current) ? current : null);
      })
      .catch((cause) => { if (!controller.signal.aborted) setError(cause.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [accessToken, authLoading, principal, refreshVersion]);

  useEffect(() => {
    if (!selectedId) { setRegistrations([]); return undefined; }
    const controller = new AbortController();
    getTournamentRegistrations('admin', selectedId, controller.signal)
      .then(setRegistrations)
      .catch((cause) => { if (!controller.signal.aborted) setError(cause.message); });
    return () => controller.abort();
  }, [accessToken, selectedId, refreshVersion]);

  const filtered = useMemo(() => tournaments.filter((row) =>
    matches(query, row.name, row.id) && (status === 'all' || row.status === status)), [tournaments, query, status]);
  const selected = tournaments.find((row) => row.id === selectedId);
  const canManage = principal?.permissions?.includes('tournament.manage') === true;
  const fields = [
    { name: 'gameId', label: 'Trò chơi', options: games.filter((game) => game.isActive).map((game) => [game.gameId, game.gameName]) },
    { name: 'name', label: 'Tên giải đấu', maxLength: 150 },
    { name: 'registrationStartUtc', label: 'Mở đăng ký (giờ Việt Nam)', type: 'datetime-local' },
    { name: 'registrationEndUtc', label: 'Đóng đăng ký (giờ Việt Nam)', type: 'datetime-local' },
    { name: 'startTimeUtc', label: 'Bắt đầu thi đấu (giờ Việt Nam)', type: 'datetime-local' },
    { name: 'entryFee', label: 'Lệ phí mỗi người (VND)', type: 'number', min: 0, max: 999999999, step: 1 },
  ];
  const initial = editing
    ? {
      gameId: String(editing.gameId), name: editing.name,
      registrationStartUtc: inputDate(editing.registrationStartUtc),
      registrationEndUtc: inputDate(editing.registrationEndUtc),
      startTimeUtc: inputDate(editing.startTimeUtc), entryFee: String(editing.entryFee),
    }
    : {
      gameId: String(games.find((game) => game.isActive)?.gameId || ''),
      name: '',
      registrationStartUtc: inputDate(new Date().toISOString()),
      registrationEndUtc: inputDate(new Date(Date.now() + 86400000).toISOString()),
      startTimeUtc: inputDate(new Date(Date.now() + 2 * 86400000).toISOString()),
      entryFee: '0',
    };

  async function save(form) {
    const payload = {
      gameId: Number(form.gameId),
      name: form.name,
      registrationStartUtc: utcDate(form.registrationStartUtc),
      registrationEndUtc: utcDate(form.registrationEndUtc),
      startTimeUtc: utcDate(form.startTimeUtc),
      entryFee: Number(form.entryFee),
    };
    if (editing) await updateTournament(editing.id, { ...payload, expectedVersion: editing.version });
    else {
      const intent = tournamentCreateKey(principal?.employeeId, payload);
      await createTournament(payload, intent.key);
      clearTournamentCreateKey(intent);
    }
    setNotice(editing ? 'Đã cập nhật giải đấu.' : 'Đã tạo giải đấu ở trạng thái bản nháp.');
    reload();
  }

  async function changeStatus(tournament, toStatus) {
    try {
      const request = { toStatus, expectedVersion: tournament.version };
      const intent = tournamentTransitionKey(principal?.employeeId, tournament.id, request);
      await transitionTournament(tournament.id, request, intent.key);
      clearTournamentCreateKey(intent);
      setNotice('Đã xử lý yêu cầu chuyển trạng thái. Danh sách sẽ được cập nhật.');
      setError('');
      reload();
    } catch (cause) { setError(cause.message); }
  }

  const statusOptions = [
    ['all', 'Tất cả trạng thái'], ['Draft', 'Bản nháp'],
    ['RegistrationOpen', 'Mở đăng ký'], ['RegistrationClosed', 'Đóng đăng ký'],
  ];
  const openCount = tournaments.filter((row) => row.status === 'RegistrationOpen').length;
  const registrationCount = tournaments.reduce((sum, row) => sum + row.registeredCount, 0);

  return <AdminPage live title="Quản lý giải đấu" description="Quản lý giải và danh sách đăng ký từ dữ liệu máy chủ." actions={canManage && <button className="cafe-button primary" onClick={() => setEditing(null)}><Plus size={16}/>Tạo giải đấu</button>}>
    <Notice notice={notice} error={error} clear={() => { setNotice(''); setError(''); }}/>
    <div className="cafe-stats">
      <StatCard label="Tổng giải đấu" value={tournaments.length} icon={Trophy}/>
      <StatCard label="Mở đăng ký" value={openCount} icon={Trophy}/>
      <StatCard label="Lượt đăng ký" value={registrationCount} icon={Users}/>
    </div>
    <Panel title="Danh sách giải đấu" description={connectionStatus === 'connected' ? 'Đồng bộ realtime đang hoạt động.' : 'Danh sách được đọc trực tiếp từ SQL.'}>
      <Toolbar query={query} onQuery={setQuery} placeholder="Tìm tên hoặc mã giải đấu…" onReset={() => { setQuery(''); setStatus('all'); }}>
        <Filter label="Trạng thái giải" value={status} onChange={setStatus} options={statusOptions}/>
      </Toolbar>
      {loading && <p role="status">Đang tải giải đấu…</p>}
      <Table rows={filtered} idKey="id" resetKey={query + status} columns={[
        { key: 'name', label: 'Giải đấu', sortValue: (row) => row.name, render: (row) => <><button className="admin-link" onClick={() => setSelectedId(row.id)}>{row.name}</button><small>#{row.id} · {row.gameName} · {row.entryFee === 0 ? 'Miễn phí' : `${money(row.entryFee)} / người`}</small></> },
        { key: 'time', label: 'Lịch đăng ký / Thi đấu', render: (row) => <>{dateTime(row.registrationEndUtc)}<small>Thi đấu: {dateTime(row.startTimeUtc)}</small></> },
        { key: 'count', label: 'Đăng ký', render: (row) => `${row.registeredCount} người` },
        { key: 'status', label: 'Trạng thái', render: (row) => <Status value={row.status}/> },
        { key: 'actions', label: '', render: (row) => <div className="admin-actions"><button className="cafe-button" onClick={() => setSelectedId(row.id)}>Quản lý</button>{canManage && ['Draft', 'RegistrationOpen', 'RegistrationClosed'].includes(row.status) && <button className="cafe-button" onClick={() => setEditing(row)}>Sửa</button>}</div> },
      ]}/>
    </Panel>
    {editing !== undefined && <FormDialog
      title={editing ? 'Chỉnh sửa giải đấu' : 'Tạo giải đấu'}
      fields={fields} initial={initial}
      description="Giải mới ở trạng thái bản nháp. Phí được nhập theo VND nguyên và thanh toán bằng ví."
      onClose={() => setEditing(undefined)}
      onSave={save}/>}
    {selected && <DetailDialog title={selected.name} onClose={() => setSelectedId(null)}>
      <Notice notice={notice} error={error} clear={() => { setNotice(''); setError(''); }}/>
      <Row label="Trạng thái"><Status value={selected.status}/></Row>
      <Row label="Trò chơi">{selected.gameName} · đội {selected.teamSize} người</Row>
      <Row label="Lệ phí">{selected.entryFee === 0 ? 'Miễn phí' : `${money(selected.entryFee)} / người`}</Row>
      <Row label="Mở / đóng đăng ký">{dateTime(selected.registrationStartUtc)} → {dateTime(selected.registrationEndUtc)}</Row>
      <Row label="Thi đấu">{dateTime(selected.startTimeUtc)}</Row>
      <div className="admin-actions" style={{ marginTop: 18 }}>
        {canManage && selected.status === 'Draft' && <button className="cafe-button primary" onClick={() => void changeStatus(selected, 'RegistrationOpen')}>Mở đăng ký</button>}
        {canManage && selected.status === 'RegistrationOpen' && <button className="cafe-button" onClick={() => void changeStatus(selected, 'RegistrationClosed')}>Đóng đăng ký</button>}
        {canManage && selected.status === 'RegistrationClosed' && Date.parse(selected.registrationEndUtc) > Date.now()
          && <button className="cafe-button primary" onClick={() => void changeStatus(selected, 'RegistrationOpen')}>Mở lại đăng ký</button>}
      </div>
      <h3 className="admin-section-title">Người tham gia ({registrations.length})</h3>
      <Table rows={registrations} idKey="id" columns={[
        { key: 'customerName', label: 'Người chơi' },
        { key: 'riotId', label: 'Riot ID' },
        { key: 'rankName', label: 'Hạng' },
        { key: 'preferredRole', label: 'Vai trò' },
        { key: 'status', label: 'Trạng thái', render: (row) => <Status value={row.status}/> },
        { key: 'registeredAtUtc', label: 'Thời gian', render: (row) => dateTime(row.registeredAtUtc) },
      ]}/>
    </DetailDialog>}
  </AdminPage>;
}
