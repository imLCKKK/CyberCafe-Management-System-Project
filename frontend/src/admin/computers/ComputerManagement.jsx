import React, { useEffect, useRef, useState } from 'react';
import { Modal } from './components/CafeUI';
import { currency } from './mockData';
import { getAccessToken } from '../../shared/auth/api';
import { completeMaintenance, deleteComputer, deleteComputerType, getMaintenances,
  saveComputer, saveComputerType, saveMaintenance } from './computerApi';
import './management.css';

function useMutation(onSaved) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const pending = useRef(null);
  useEffect(() => () => pending.current?.abort(), []);
  async function run(action, message) {
    if (pending.current) return false;
    const controller = new AbortController();
    const token = getAccessToken();
    pending.current = controller;
    setBusy(true);
    setError('');
    try {
      await action(controller.signal);
      if (!controller.signal.aborted && getAccessToken() === token) {
        onSaved(message);
        return true;
      }
    } catch (failure) {
      if (!controller.signal.aborted && getAccessToken() === token) setError(failure.message);
    } finally {
      if (!controller.signal.aborted) setBusy(false);
      pending.current = null;
    }
    return false;
  }
  return { busy, error, run };
}

export function ComputerEditor({ computer, rooms, types, onClose, onSaved }) {
  const [draft, setDraft] = useState(() => computer || {
    ComputerName: '', RoomId: rooms[0]?.RoomId || '', ComputerTypeId: types[0]?.ComputerTypeId || '',
  });
  const [confirmDelete, setConfirmDelete] = useState(false);
  const { busy, error, run } = useMutation((message) => { onSaved(message); onClose(); });
  const field = (key) => ({ value: draft[key], onChange: (event) => setDraft({ ...draft, [key]: event.target.value }) });
  return <Modal title={computer ? `Sửa ${computer.ComputerName}` : 'Thêm máy tính'} onClose={onClose}>
    <form className="cafe-form computer-management" onSubmit={(event) => {
      event.preventDefault(); void run((signal) => saveComputer(computer?.ComputerId, draft, signal), 'Đã lưu máy tính.');
    }}>
      <fieldset disabled={busy}>
        <label>Tên máy<input autoFocus required maxLength={100} {...field('ComputerName')} /></label>
        <label>Phòng máy<select required {...field('RoomId')}>
          <option value="" disabled>Chọn phòng máy</option>
          {rooms.map((row) => <option key={row.RoomId} value={row.RoomId}>{row.RoomName}</option>)}
        </select></label>
        <label>Loại máy<select required {...field('ComputerTypeId')}>
          <option value="" disabled>Chọn loại máy</option>
          {types.map((row) => <option key={row.ComputerTypeId} value={row.ComputerTypeId}>{row.TypeName}</option>)}
        </select></label>
      </fieldset>
      {error && <p className="cafe-error" role="alert">{error}</p>}
      {confirmDelete ? <div className="computer-delete-confirm" role="alert">
        <p>Xóa {computer.ComputerName}? Máy có lịch sử phiên hoặc bảo trì sẽ được giữ lại.</p>
        <button type="button" className="cafe-button" disabled={busy} onClick={() => setConfirmDelete(false)}>Giữ máy</button>
        <button type="button" className="cafe-button" disabled={busy} onClick={() => {
          void run((signal) => deleteComputer(computer.ComputerId, signal), 'Đã xóa máy tính.');
        }}>Xác nhận xóa máy</button>
      </div> : <div className="cafe-dialog-actions">
        {computer && <button type="button" className="cafe-button" disabled={busy} onClick={() => setConfirmDelete(true)}>Xóa máy</button>}
        <button className="cafe-button primary" disabled={busy || !rooms.length || !types.length}>{busy ? 'Đang lưu...' : 'Lưu máy'}</button>
      </div>}
    </form>
  </Modal>;
}

const emptyType = { TypeName: '', PricePerHour: '', CPU: '', RAM: '', GPU: '', Description: '' };

export function ComputerTypesDialog({ types, onClose, onSaved }) {
  const [draft, setDraft] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const { busy, error, run } = useMutation(onSaved);
  const field = (key) => ({ value: draft[key] ?? '', onChange: (event) => setDraft({ ...draft, [key]: event.target.value }) });
  const finish = async (action, message) => { if (await run(action, message)) { setDraft(null); setConfirmDelete(false); } };
  return <Modal title="Quản lý loại máy" onClose={onClose}>
    {draft ? <form className="cafe-form computer-management" onSubmit={(event) => {
      event.preventDefault(); void finish((signal) => saveComputerType(draft.ComputerTypeId, draft, signal), 'Đã lưu loại máy.');
    }}>
      <fieldset disabled={busy}>
        <label>Tên loại máy<input autoFocus required maxLength={100} {...field('TypeName')} /></label>
        <label>Giá mỗi giờ (đồng)<input type="number" required min="1" max="9007199254740991" step="1" {...field('PricePerHour')} /></label>
        <label>CPU<input maxLength={100} {...field('CPU')} /></label>
        <label>RAM<input maxLength={50} {...field('RAM')} /></label>
        <label>GPU<input maxLength={100} {...field('GPU')} /></label>
        <label>Mô tả<textarea maxLength={255} {...field('Description')} /></label>
      </fieldset>
      {confirmDelete ? <div className="computer-delete-confirm" role="alert">
        <p>Xóa loại máy {draft.TypeName}? Loại máy đang được sử dụng sẽ được giữ lại.</p>
        <button type="button" className="cafe-button" disabled={busy} onClick={() => setConfirmDelete(false)}>Giữ loại máy</button>
        <button type="button" className="cafe-button" disabled={busy} onClick={() => {
          void finish((signal) => deleteComputerType(draft.ComputerTypeId, signal), 'Đã xóa loại máy.');
        }}>Xác nhận xóa loại máy</button>
      </div> : <div className="cafe-dialog-actions">
        <button type="button" className="cafe-button" disabled={busy} onClick={() => setDraft(null)}>Danh sách</button>
        {draft.ComputerTypeId && <button type="button" className="cafe-button" disabled={busy} onClick={() => setConfirmDelete(true)}>Xóa loại máy</button>}
        <button className="cafe-button primary" disabled={busy}>{busy ? 'Đang lưu...' : 'Lưu loại máy'}</button>
      </div>}
    </form> : <div className="computer-management">
      <button className="cafe-button primary" onClick={() => { setDraft({ ...emptyType }); setConfirmDelete(false); }}>Thêm loại máy</button>
      {!types.length && <p>Chưa có loại máy.</p>}
      <ul className="computer-type-list">{types.map((row) => <li key={row.ComputerTypeId}>
        <div><strong>{row.TypeName}</strong><p>{currency(row.PricePerHour)} / giờ</p></div>
        <button className="cafe-button" onClick={() => { setDraft({ ...row }); setConfirmDelete(false); }}>Sửa {row.TypeName}</button>
      </li>)}</ul>
    </div>}
    {error && <p className="cafe-error" role="alert">{error}</p>}
  </Modal>;
}

export function MaintenancePanel({ computer, permissions, onSaved }) {
  const canView = permissions.includes('maintenance.view');
  const canManage = permissions.includes('maintenance.manage');
  const [state, setState] = useState({ records: [], loading: true, error: '' });
  const [version, setVersion] = useState(0);
  const [description, setDescription] = useState('');
  const [editing, setEditing] = useState(null);
  const { busy, error, run } = useMutation((message) => { setVersion((value) => value + 1); onSaved(message); });
  useEffect(() => {
    if (!canView) return;
    const controller = new AbortController();
    const token = getAccessToken();
    setState({ records: [], loading: true, error: '' });
    getMaintenances(computer.ComputerId, controller.signal).then((records) => {
      if (!controller.signal.aborted && getAccessToken() === token) setState({ records, loading: false, error: '' });
    }).catch((failure) => {
      if (!controller.signal.aborted && getAccessToken() === token) setState({ records: [], loading: false, error: failure.message });
    });
    return () => controller.abort();
  }, [computer, canView, version]);
  if (!canView && !canManage) return null;
  const canStart = !computer.HasOpenSession && !computer.HasOpenMaintenance && ['Available', 'Maintenance'].includes(computer.Status);
  const submit = async (event) => {
    event.preventDefault();
    if (await run((signal) => saveMaintenance(computer.ComputerId, editing, description, signal),
      editing ? 'Đã lưu hồ sơ bảo trì.' : 'Đã bắt đầu bảo trì.')) { setDescription(''); setEditing(null); }
  };
  return <section className="computer-management maintenance-panel" aria-label="Hồ sơ bảo trì">
    <h3>Hồ sơ bảo trì</h3>
    {canView && (state.loading ? <p role="status">Đang tải hồ sơ bảo trì...</p>
      : state.error ? <div><p role="alert" className="cafe-error">{state.error}</p>
        <button className="cafe-button" onClick={() => setVersion((value) => value + 1)}>Tải lại hồ sơ</button></div>
        : <ul className="maintenance-list">{!state.records.length && <li>Chưa có hồ sơ bảo trì.</li>}
          {state.records.map((row) => <li key={row.maintenanceId}>
            <strong>#{row.maintenanceId} · {row.endTime ? 'Đã kết thúc' : row.status === 'InProgress' ? 'Đang bảo trì' : 'Cần đối soát'}</strong>
            <p>{row.description || 'Không có mô tả'}</p>
            <small>Nhân viên #{row.employeeId} · {new Date(row.startTime).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}
              {row.endTime && ` → ${new Date(row.endTime).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}`}</small>
            {canManage && !row.endTime && row.status === 'InProgress' && <div className="cafe-dialog-actions">
              <button className="cafe-button" disabled={busy} onClick={() => { setEditing(row.maintenanceId); setDescription(row.description || ''); }}>Sửa mô tả</button>
              <button className="cafe-button" disabled={busy} onClick={() => {
                void run((signal) => completeMaintenance(computer.ComputerId, row.maintenanceId, signal), 'Đã hoàn tất bảo trì.');
              }}>Hoàn tất bảo trì</button>
            </div>}
          </li>)}
        </ul>)}
    {canManage && (canStart || editing) && <form className="cafe-form" onSubmit={submit}>
      <label>{editing ? 'Sửa mô tả bảo trì' : 'Mô tả bảo trì'}<textarea maxLength={255} disabled={busy}
        value={description} onChange={(event) => setDescription(event.target.value)} /></label>
      <div className="cafe-dialog-actions">
        {editing && <button type="button" className="cafe-button" disabled={busy} onClick={() => { setEditing(null); setDescription(''); }}>Hủy sửa mô tả</button>}
        <button className="cafe-button primary" disabled={busy}>{busy ? 'Đang lưu...' : editing ? 'Lưu mô tả' : 'Bắt đầu bảo trì'}</button>
      </div>
    </form>}
    {error && <p className="cafe-error" role="alert">{error}</p>}
  </section>;
}
