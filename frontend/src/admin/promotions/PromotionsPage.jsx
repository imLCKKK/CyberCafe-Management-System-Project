import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Plus, RefreshCw } from 'lucide-react';
import { useAuth } from '../../shared/auth/AuthContext';
import { expireSession, getAccessToken } from '../../shared/auth/api';
import { useRealtime } from '../../shared/realtime/useRealtime';
import { AdminPage, Filter, FormDialog, Notice, Panel, Status, Table, Toolbar } from '../shared/AdminUI';
import { matches, money } from '../shared/adminModel';
import { voucherApi, voucherInputDate, voucherPayload } from './voucherApi';

const events = ['PromotionChanged'];
const date = (value) => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
const fields = [
  { name: 'code', label: 'Mã voucher', maxLength: 50, help: 'Chữ không dấu, số, - và _. Không đổi mã đã có lịch sử sử dụng.' },
  { name: 'name', label: 'Tên voucher', maxLength: 150 },
  { name: 'discountType', label: 'Loại giảm', options: [['Percent', 'Phần trăm (%)'], ['Fixed', 'Số tiền cố định (VND)']] },
  { name: 'discountValue', label: 'Giá trị giảm (% hoặc VND)', type: 'number', min: 0.01, step: 0.01, help: '%: lớn hơn 0 đến 100, tối đa 2 số thập phân. Cố định: nguyên đồng.' },
  { name: 'minOrderAmount', label: 'Ngưỡng đơn tối thiểu (VND)', type: 'number', min: 0, max: Number.MAX_SAFE_INTEGER, step: 1 },
  { name: 'maxDiscountAmount', label: 'Trần giảm (VND)', type: 'number', required: false, min: 1, max: Number.MAX_SAFE_INTEGER, step: 1, help: 'Để trống: không đặt trần.' },
  { name: 'startDate', label: 'Bắt đầu (giờ Việt Nam)', type: 'datetime-local', step: 0.001 },
  { name: 'endDate', label: 'Kết thúc (giờ Việt Nam)', type: 'datetime-local', step: 0.001 },
  { name: 'totalUsageLimit', label: 'Hạn mức toàn hệ thống', type: 'number', required: false, min: 1, max: 2147483647, step: 1, help: 'Để trống: không giới hạn.' },
  { name: 'perCustomerUsageLimit', label: 'Hạn mức mỗi khách', type: 'number', required: false, min: 1, max: 2147483647, step: 1, help: 'Để trống: không giới hạn.' },
  { name: 'isActive', label: 'Bật voucher', type: 'checkbox' },
];

export function PromotionsPage() {
  const { principal, accessToken } = useAuth();
  const canView = principal?.type === 'employee' && principal.permissions.includes('promotion.view');
  const canManage = canView && principal.permissions.includes('promotion.manage');
  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('all');
  const [editing, setEditing] = useState(undefined);
  const [pending, setPending] = useState(null);
  const mutation = useRef(false);
  const [version, setVersion] = useState(0);
  const reload = useCallback(() => setVersion((value) => value + 1), []);
  const connection = useRealtime({
    enabled: canView, accessToken, events, onEvent: reload, onReady: reload,
    onAuthFailure: (code) => {
      if (getAccessToken() !== accessToken) return;
      if (code === 401) expireSession({ accessToken, accountType: 'employee' });
      else { setRows([]); setEditing(undefined); setError('Bạn không có quyền kết nối realtime. Vui lòng đăng nhập lại.'); }
    },
  });

  useEffect(() => {
    const controller = new AbortController();
    setError('');
    if (!canView) { setRows([]); setLoading(false); return () => controller.abort(); }
    setLoading(true);
    voucherApi.list(controller.signal).then((data) => {
      if (!controller.signal.aborted) setRows(data);
    }).catch((cause) => {
      if (!controller.signal.aborted) { setRows([]); setError(cause.message); }
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [accessToken, canView, version]);

  async function save(form) {
    if (!canManage) throw new Error('Bạn không có quyền quản lý voucher.');
    const payload = voucherPayload(form);
    if (editing) await voucherApi.update(editing.id, payload);
    else await voucherApi.create(payload);
    setNotice(editing ? 'Đã lưu thay đổi voucher.' : 'Đã tạo voucher.');
    reload();
  }

  async function toggle(row) {
    if (!canManage || mutation.current) return;
    mutation.current = true;
    setPending(row.id);
    setError('');
    setNotice('');
    try {
      await voucherApi.setStatus(row.id, !row.isActive);
      setNotice(row.isActive ? `Đã tắt ${row.code}.` : `Đã bật ${row.code}.`);
      reload();
    } catch (cause) { setError(cause.message); }
    finally { mutation.current = false; setPending(null); }
  }

  const filtered = rows.filter((row) => matches(query, row.code, row.name)
    && (status === 'all' || row.isActive === (status === 'active')));
  const initial = editing ? {
    ...editing, startDate: voucherInputDate(editing.startDate), endDate: voucherInputDate(editing.endDate),
  } : {
    code: '', name: '', discountType: 'Percent', discountValue: '', minOrderAmount: '0',
    maxDiscountAmount: '', startDate: voucherInputDate(new Date().toISOString()),
    endDate: voucherInputDate(new Date(Date.now() + 86400000).toISOString()),
    totalUsageLimit: '', perCustomerUsageLimit: '', isActive: true,
  };

  return <AdminPage live title="Quản lý voucher" description="Voucher chỉ áp dụng tiền món ăn. Quy tắc và hạn mức được máy chủ kiểm tra."
    actions={canView && <><button className="cafe-button" disabled={loading} onClick={reload}><RefreshCw size={16}/>Tải lại</button>
      {canManage && <button className="cafe-button primary" disabled={pending !== null} onClick={() => { setNotice(''); setEditing(null); }}><Plus size={16}/>Thêm voucher</button>}</>}>
    {!canView ? <p className="cafe-error" role="alert">Bạn không có quyền xem voucher.</p> : <>
      <Notice notice={notice} error={error}/>
      <p role="status">{connection === 'connected' ? 'Đã kết nối cập nhật trực tiếp.' : 'Chưa có kết nối cập nhật trực tiếp; có thể tải lại danh sách.'}</p>
      <Panel title="Danh sách voucher" description="Thời gian hiển thị theo giờ Việt Nam (UTC+07:00). Tắt thay vì xóa để giữ lịch sử.">
        <Toolbar query={query} onQuery={setQuery} placeholder="Tìm mã hoặc tên voucher…" onReset={() => { setQuery(''); setStatus('all'); }}>
          <Filter label="Trạng thái voucher" value={status} onChange={setStatus} options={[['all', 'Tất cả'], ['active', 'Đang bật'], ['inactive', 'Đang tắt']]}/>
        </Toolbar>
        {loading && rows.length === 0 ? <p role="status">Đang tải voucher…</p> : error && rows.length === 0 ? <button className="cafe-button" onClick={reload}>Thử lại</button>
          : <Table rows={filtered} resetKey={query + status} empty={rows.length ? 'Không có voucher phù hợp.' : 'Chưa có voucher.'} columns={[
            { key: 'code', label: 'Voucher', sortValue: (row) => row.code, render: (row) => <><strong>{row.code}</strong><small>{row.name}</small></> },
            { key: 'discountValue', label: 'Mức giảm', render: (row) => <>{row.discountType === 'Percent' ? `${row.discountValue}%` : money(row.discountValue)}<small>Trần: {row.maxDiscountAmount === null ? 'Không đặt' : money(row.maxDiscountAmount)}</small></> },
            { key: 'minOrderAmount', label: 'Đơn tối thiểu', render: (row) => money(row.minOrderAmount) },
            { key: 'startDate', label: 'Hiệu lực (VN)', render: (row) => <>{date(row.startDate)}<small>Đến trước {date(row.endDate)}</small></> },
            { key: 'totalUsageLimit', label: 'Hạn mức lượt', render: (row) => <>Toàn hệ thống: {row.totalUsageLimit ?? 'Không giới hạn'}<small>Mỗi khách: {row.perCustomerUsageLimit ?? 'Không giới hạn'}</small></> },
            { key: 'isActive', label: 'Trạng thái', render: (row) => <Status value={row.isActive ? 'Active' : 'Inactive'}>{row.isActive ? 'Đang bật' : 'Đang tắt'}</Status> },
            ...(canManage ? [{ key: 'actions', label: 'Thao tác', render: (row) => <div className="cafe-row-actions">
              <button className="cafe-button" disabled={pending !== null} aria-label={`Sửa ${row.code}`} onClick={() => setEditing(row)}>Sửa</button>
              <button className="cafe-button" disabled={pending !== null} aria-label={`${row.isActive ? 'Tắt' : 'Bật'} ${row.code}`} onClick={() => void toggle(row)}>{pending === row.id ? 'Đang lưu…' : row.isActive ? 'Tắt' : 'Bật'}</button>
            </div> }] : []),
          ]}/>}
      </Panel>
    </>}
    {canManage && editing !== undefined && <FormDialog wide title={editing ? `Sửa voucher ${editing.code}` : 'Thêm voucher'}
      fields={fields} initial={initial} onSave={save} onClose={() => setEditing(undefined)}
      description="Để trống trần giảm/hạn mức nếu không giới hạn. Bật không đồng nghĩa đang trong thời hạn. Lưu sẽ gửi toàn bộ quy tắc; thay đổi chỉ áp dụng cho đơn mới."/>}
  </AdminPage>;
}
