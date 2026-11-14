import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Monitor, Search, LayoutGrid, List, Wrench, CircleCheck, Users, Cpu, MemoryStick, CircuitBoard } from 'lucide-react';
import { statusLabels, currency } from './mockData';
import { PageHeading, StatCard, Station, StatusBadge, Modal } from './components/CafeUI';
import { updateComputerStatus } from './computerApi';
import { useComputerData } from './useComputerData';
import { getAccessToken } from '../../shared/auth/api';
import '../shared/cafe.css';

const realtimeLabels = {
  idle: 'Cập nhật trực tiếp chưa kết nối',
  connecting: 'Đang kết nối cập nhật trực tiếp...',
  connected: 'Đã kết nối cập nhật trực tiếp',
  reconnecting: 'Mất kết nối trực tiếp, đang kết nối lại...',
  disconnected: 'Chưa kết nối được cập nhật trực tiếp, đang thử lại...',
  unauthorized: 'Cập nhật trực tiếp yêu cầu đăng nhập lại',
  forbidden: 'Bạn không có quyền kết nối cập nhật trực tiếp',
};

export function ComputersPage() {
  const { computers, rooms, types, permissions, loading, refreshing, error, realtimeStatus, reload, applyComputer } = useComputerData();
  const [params, setParams] = useSearchParams();
  const [search, setSearch] = useState('');
  const [room, setRoom] = useState('all');
  const [type, setType] = useState('all');
  const [status, setStatus] = useState('all');
  const [view, setView] = useState('grid');
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState('');
  const [actionError, setActionError] = useState('');
  const pending = useRef(null);
  useEffect(() => () => pending.current?.abort(), []);
  useEffect(() => { if (loading) { setNotice(''); setActionError(''); } }, [loading]);
  const roomById = useMemo(() => new Map(rooms.map((row) => [row.RoomId, row])), [rooms]);
  const typeById = useMemo(() => new Map(types.map((row) => [row.ComputerTypeId, row])), [types]);
  const selected = computers.find((pc) => pc.ComputerId === Number(params.get('computer')));
  const selectedType = selected && typeById.get(selected.ComputerTypeId);
  const canUpdate = permissions.includes('computer.status.update');
  const counts = { Available: 0, Occupied: 0, Maintenance: 0, Other: 0 };
  let readyCount = 0;
  for (const pc of computers) {
    counts[Object.hasOwn(statusLabels, pc.Status) ? pc.Status : 'Other']++;
    if (pc.Status === 'Available' && !pc.HasOpenSession && !pc.HasOpenMaintenance) readyCount++;
  }
  const searchText = search.trim().toLocaleLowerCase('vi');
  const filtered = computers.filter((pc) =>
    pc.ComputerName.toLocaleLowerCase('vi').includes(searchText)
    && (room === 'all' || pc.RoomId === Number(room))
    && (type === 'all' || pc.ComputerTypeId === Number(type))
    && (status === 'all' || (status === 'Other' ? !Object.hasOwn(statusLabels, pc.Status) : pc.Status === status)));
  const select = (pc) => setParams(pc ? { computer: String(pc.ComputerId) } : {});
  const resetFilters = () => { setSearch(''); setRoom('all'); setType('all'); setStatus('all'); };

  async function updateMaintenance() {
    if (pending.current || !selected || !canUpdate) return;
    const controller = new AbortController();
    const requestToken = getAccessToken();
    pending.current = controller;
    setSaving(true);
    setNotice('');
    setActionError('');
    try {
      const saved = await updateComputerStatus(selected, controller.signal);
      if (!controller.signal.aborted && getAccessToken() === requestToken) {
        applyComputer(saved);
        select(null);
        setNotice(`Đã cập nhật trạng thái ${saved.ComputerName}.`);
      }
    } catch (failure) {
      if (!controller.signal.aborted && getAccessToken() === requestToken) {
        setActionError(failure.message);
        if ([401, 403, 404, 409, 200].includes(failure.status)) reload();
      }
    } finally {
      if (!controller.signal.aborted) setSaving(false);
      pending.current = null;
    }
  }

  return (
    <section className="cafe-page cafe-computers" aria-busy={loading || refreshing}>
      <PageHeading eyebrow="KHÔNG GIAN LÀM VIỆC / MÁY TÍNH" title="Quản lý máy tính"
        description="Theo dõi trạng thái và quản lý toàn bộ máy tại quán.">
        <button className="cafe-button" onClick={() => { setNotice(''); reload(); }} disabled={loading || refreshing || saving}>Tải lại</button>
      </PageHeading>
      <p className="cafe-muted" role="status" aria-live="polite" data-realtime-status={realtimeStatus}>
        {realtimeLabels[realtimeStatus]}{refreshing ? ' · Đang cập nhật danh sách máy...' : ''}
      </p>
      {notice && !loading && !error && <div className="cafe-notice" role="status">{notice}
        <button className="cafe-text-button" onClick={() => setNotice('')}>Đóng</button>
      </div>}
      {actionError && !selected && !error && <p className="cafe-error" role="alert">{actionError}</p>}
      {loading ? <article className="cafe-panel cafe-empty" role="status" aria-live="polite">
        Đang tải danh sách máy...
      </article> : error ? <article className="cafe-panel cafe-empty">
        <h2>{error.status === 401 ? 'Yêu cầu đăng nhập' : 'Không tải được danh sách máy'}</h2>
        <p className="cafe-error" role="alert">{error.message}</p>
        {[401, 403].includes(error.status)
          ? <Link className="cafe-button primary" to="/admin/auth">Đăng nhập nhân viên</Link>
          : <button className="cafe-button" onClick={reload}>Thử lại</button>}
      </article> : <>
        <div className="cafe-stats">
          <StatCard label="Tổng số máy" value={computers.length} icon={Monitor}><span>Trong {rooms.length} phòng máy</span></StatCard>
          <StatCard label="Đang sử dụng" value={counts.Occupied} icon={Users}>
            <span className="cafe-blue">{computers.length ? Math.round(counts.Occupied / computers.length * 100) : 0}% công suất</span>
          </StatCard>
          <StatCard label="Sẵn sàng sử dụng" value={readyCount} icon={CircleCheck}><span className="cafe-positive">Có thể đón khách</span></StatCard>
          <StatCard label="Đang bảo trì" value={counts.Maintenance} icon={Wrench}><span className="cafe-muted">Tạm ngừng phục vụ</span></StatCard>
        </div>
        <article className="cafe-panel">
          <div className="cafe-panel-heading">
            <div><h2>Danh sách máy tính</h2><p>{filtered.length} / {computers.length} máy <span>·</span> Dữ liệu hệ thống</p></div>
            <div className="cafe-view-switch">
              <button aria-label="Dạng lưới" aria-pressed={view === 'grid'} onClick={() => setView('grid')}><LayoutGrid size={17} /></button>
              <button aria-label="Dạng bảng" aria-pressed={view === 'list'} onClick={() => setView('list')}><List size={18} /></button>
            </div>
          </div>
          <div className="cafe-filters">
            <label className="cafe-search"><Search size={17} /><input aria-label="Tìm kiếm tên máy" placeholder="Tìm kiếm tên máy..."
              value={search} onChange={(event) => setSearch(event.target.value)} /></label>
            <select aria-label="Lọc phòng máy" value={room} onChange={(event) => setRoom(event.target.value)}>
              <option value="all">Tất cả phòng</option>
              {rooms.map((row) => <option key={row.RoomId} value={row.RoomId}>{row.RoomName}</option>)}
            </select>
            <select aria-label="Lọc loại máy" value={type} onChange={(event) => setType(event.target.value)}>
              <option value="all">Tất cả loại máy</option>
              {types.map((row) => <option key={row.ComputerTypeId} value={row.ComputerTypeId}>{row.TypeName}</option>)}
            </select>
          </div>
          <div className="cafe-tabs" aria-label="Lọc trạng thái">
            <button aria-pressed={status === 'all'} onClick={() => setStatus('all')}>Tất cả <span>{computers.length}</span></button>
            {Object.entries({ ...statusLabels, ...(counts.Other ? { Other: 'Khác' } : {}) }).map(([key, label]) =>
              <button key={key} aria-pressed={status === key} onClick={() => setStatus(key)}>
                <i className={key.toLowerCase()} />{label}<span>{counts[key]}</span>
              </button>)}
          </div>
          {filtered.length === 0 ? <div className="cafe-empty">
            <Monitor size={36} /><h3>{computers.length ? 'Không tìm thấy máy tính' : 'Chưa có máy tính'}</h3>
            {computers.length > 0 && <><p>Thử tên máy khác hoặc đặt lại bộ lọc.</p>
              <button className="cafe-button" onClick={resetFilters}>Đặt lại bộ lọc</button></>}
          </div> : view === 'grid' ? <div className="cafe-computer-grid">
            {filtered.map((pc) => <Station key={pc.ComputerId} computer={pc}
              room={roomById.get(pc.RoomId).RoomName} type={typeById.get(pc.ComputerTypeId).TypeName}
              onClick={() => { if (!saving) { setActionError(''); select(pc); } }} />)}
          </div> : <div className="cafe-table-wrap" tabIndex={0} role="region" aria-label="Danh sách máy tính"><table className="cafe-table">
            <thead><tr><th>Tên máy</th><th>Phòng</th><th>Loại máy</th><th>Giá / giờ</th><th>Trạng thái</th><th /></tr></thead>
            <tbody>{filtered.map((pc) => <tr key={pc.ComputerId}>
              <td><strong>{pc.ComputerName}</strong></td><td>{roomById.get(pc.RoomId).RoomName}</td>
              <td>{typeById.get(pc.ComputerTypeId).TypeName}</td><td>{currency(typeById.get(pc.ComputerTypeId).PricePerHour)}</td>
              <td><StatusBadge status={pc.Status} /></td><td><button className="cafe-text-button" disabled={saving}
                aria-label={`Chi tiết ${pc.ComputerName}`} onClick={() => { setActionError(''); select(pc); }}>Chi tiết</button></td>
            </tr>)}</tbody>
          </table></div>}
          <div className="cafe-computer-footer"><span>Dữ liệu được lưu trên hệ thống quản lý</span><span>{filtered.length} máy</span></div>
        </article>
        {selected && <Modal title={selected.ComputerName} onClose={() => select(null)}>
          <div className="cafe-computer-detail">
            <StatusBadge status={selected.Status} />
            <p>{roomById.get(selected.RoomId).RoomName} · {selectedType.TypeName}</p>
            <div className="cafe-specs">
              <span><Cpu size={18} />{selectedType.CPU || 'Chưa có thông tin CPU'}</span>
              <span><MemoryStick size={18} />{selectedType.RAM || 'Chưa có thông tin RAM'}</span>
              <span><CircuitBoard size={18} />{selectedType.GPU || 'Chưa có thông tin GPU'}</span>
            </div>
            <div className="cafe-detail-row"><span>Giá sử dụng</span><strong>{currency(selectedType.PricePerHour)} / giờ</strong></div>
            {selected.Status === 'Occupied' || selected.HasOpenSession ? <p className="cafe-detail-note">
              Máy đang được sử dụng hoặc còn phiên chưa kết thúc. Cần kiểm tra và kết thúc phiên trước khi thay đổi trạng thái.
            </p> : !Object.hasOwn(statusLabels, selected.Status) ? <p className="cafe-detail-note">
              Trạng thái máy cần được kiểm tra trước khi tiếp tục phục vụ.
            </p> : selected.HasOpenMaintenance ? <p className="cafe-detail-note">
              Máy còn hồ sơ bảo trì chưa kết thúc. Cần hoàn tất hồ sơ trước khi đưa máy vào sử dụng.
            </p> : null}
            {!canUpdate && <p className="cafe-detail-note">Bạn có quyền xem thông tin máy.</p>}
            {actionError && <p className="cafe-error" role="alert">{actionError}</p>}
            {canUpdate && <div className="cafe-dialog-actions"><button className="cafe-button primary" onClick={updateMaintenance}
              disabled={saving || selected.HasOpenSession || selected.Status === 'Occupied'
                || !Object.hasOwn(statusLabels, selected.Status) || (selected.Status === 'Maintenance' && selected.HasOpenMaintenance)}>
              <Wrench size={15} />{saving ? 'Đang lưu...' : selected.Status === 'Maintenance' ? 'Đưa máy vào sử dụng' : 'Chuyển bảo trì'}
            </button></div>}
          </div>
        </Modal>}
      </>}
    </section>
  );
}
