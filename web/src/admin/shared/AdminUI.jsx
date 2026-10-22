import React, { useEffect, useRef, useState } from 'react';
import { ChevronLeft, ChevronRight, Download, Search, X } from 'lucide-react';
import { Modal, PageHeading, StatCard } from '../computers/components/CafeUI';
import { csvText } from './adminModel';
import { RiotIdFields } from '../../shared/RiotIdFields';
import '../computers/cafe.css';
import './admin.css';

export { StatCard };
export function AdminPage({ title, description, actions, children }) {
  return <section className="cafe-page admin-module"><PageHeading eyebrow="NET CAFE / QUẢN TRỊ" title={title} description={description}>{actions || <span className="cafe-system"><i/>Dữ liệu mẫu dùng chung</span>}</PageHeading>{children}<p className="cafe-demo-note">Bản demo · Thay đổi được lưu trên trình duyệt. Chưa kết nối API quản trị.</p></section>;
}
export function Notice({ notice, error, clear }) { return <>{error && <div className="admin-message error" role="alert">{error}{clear && <button onClick={clear} aria-label="Đóng lỗi"><X size={15}/></button>}</div>}{notice && <div className="admin-message" role="status">{notice}{clear && <button onClick={clear} aria-label="Đóng thông báo"><X size={15}/></button>}</div>}</>; }
export function Status({ value, children }) {
  const labels = { Pending: 'Đang chờ', Confirmed: 'Đã xác nhận', Cancelled: 'Đã hủy', Mixed: 'Đã xử lý một phần', Paid: 'Đã thanh toán', PendingCollection: 'Chờ thu tiền', CreditedUnpaid: 'Đã cộng ví · chờ thu', Collected: 'Đã thu tiền', Rejected: 'Từ chối', Active: 'Đang hoạt động', Inactive: 'Đã khóa', Open: 'Mở đăng ký', Closed: 'Đóng đăng ký', Draft: 'Bản nháp', TeamsAssigned: 'Đã chia đội', Finished: 'Kết thúc', Customer: 'Khách hàng', Staff: 'Nhân viên', Admin: 'Quản trị viên', In: 'Nhập kho', Out: 'Xuất kho', Adjust: 'Kiểm kê', Return: 'Hoàn tồn', Registered: 'Đã đăng ký', Assigned: 'Đã xếp đội' };
  const tone = ['Cancelled', 'Rejected', 'Inactive', 'Closed'].includes(value) ? 'muted' : ['Pending', 'PendingCollection', 'CreditedUnpaid', 'Mixed', 'Out'].includes(value) ? 'amber' : ['Admin', 'TeamsAssigned', 'In', 'Adjust'].includes(value) ? 'blue' : 'green';
  return <span className={`admin-status ${tone}`}><i/>{children || labels[value] || value}</span>;
}
export function Panel({ title, description, actions, children, className = '' }) { return <article className={`cafe-panel ${className}`}><div className="cafe-panel-heading"><div><h2>{title}</h2>{description && <p>{description}</p>}</div>{actions}</div>{children}</article>; }
export function Toolbar({ query, onQuery, placeholder = 'Tìm kiếm…', children, onReset }) { return <div className="admin-toolbar"><label className="cafe-search"><Search size={16}/><input aria-label={placeholder} placeholder={placeholder} value={query} onChange={(event) => onQuery(event.target.value)}/></label>{children}{onReset && <button className="cafe-button" onClick={onReset}>Xóa bộ lọc</button>}</div>; }
export function Filter({ label, value, onChange, options }) { return <select aria-label={label} value={value} onChange={(event) => onChange(event.target.value)}>{options.map(([key, text]) => <option key={key} value={key}>{text}</option>)}</select>; }
export function Table({ rows, columns, idKey = 'id', empty = 'Không có dữ liệu phù hợp.', resetKey = '', pageSize = 8 }) {
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState(null);
  useEffect(() => setPage(1), [resetKey]);
  const column = columns.find((entry) => entry.key === sort?.key);
  const sorted = column?.sortValue ? [...rows].sort((a, b) => { const left = column.sortValue(a), right = column.sortValue(b); return (typeof left === 'number' ? left - right : String(left).localeCompare(String(right), 'vi')) * sort.direction; }) : rows;
  const pages = Math.max(1, Math.ceil(rows.length / pageSize));
  const current = Math.min(page, pages);
  const visible = sorted.slice((current - 1) * pageSize, current * pageSize);
  return <><div className="cafe-table-wrap admin-table-wrap"><table className="cafe-table admin-table"><thead><tr>{columns.map((entry) => <th key={entry.key} scope="col" aria-sort={sort?.key === entry.key ? (sort.direction === 1 ? 'ascending' : 'descending') : undefined}>{entry.sortValue ? <button className="admin-sort" onClick={() => { setSort({ key: entry.key, direction: sort?.key === entry.key ? -sort.direction : 1 }); setPage(1); }}>{entry.label} ↕</button> : entry.label}</th>)}</tr></thead><tbody>{visible.map((row) => <tr key={row[idKey]}>{columns.map((entry) => <td key={entry.key}>{entry.render ? entry.render(row) : row[entry.key]}</td>)}</tr>)}</tbody></table></div>{!rows.length && <div className="admin-empty"><Search size={24}/><h3>{empty}</h3><p>Thử từ khóa hoặc bộ lọc khác.</p></div>}<div className="admin-pagination"><span>{rows.length ? `${(current - 1) * pageSize + 1}–${Math.min(current * pageSize, rows.length)}` : '0'} / {rows.length} bản ghi</span><div><button className="cafe-icon-button" disabled={current === 1} aria-label="Trang trước" onClick={() => setPage(current - 1)}><ChevronLeft size={16}/></button><span>{current} / {pages}</span><button className="cafe-icon-button" disabled={current === pages} aria-label="Trang sau" onClick={() => setPage(current + 1)}><ChevronRight size={16}/></button></div></div></>;
}
export function Field({ field, value, onChange }) {
  if (field.type === 'riot-id') return <RiotIdFields value={value} onChange={onChange}/>;
  if (field.type === 'checkbox') return <label className="admin-checkbox"><input type="checkbox" checked={Boolean(value)} onChange={(event) => onChange(event.target.checked)}/>{field.label}</label>;
  const attributes = { name: field.name, required: field.required !== false, value: value ?? '', min: field.min, max: field.max, step: field.step, maxLength: field.maxLength || (field.type === 'password' ? 128 : 250), readOnly: field.readOnly, onChange: (event) => onChange(event.target.value) };
  return <label className={field.full ? 'admin-field-full' : ''}>{field.label}{field.options ? <select {...attributes}>{field.options.map(([key, label]) => <option value={key} key={key}>{label}</option>)}</select> : field.type === 'textarea' ? <textarea {...attributes} rows={3}/> : <input {...attributes} type={field.type || 'text'}/>} {field.help && <small>{field.help}</small>}</label>;
}
export function FormDialog({ title, fields = [], initial = {}, onSave, onClose, submit = 'Lưu thông tin', description, wide = false }) {
  const [form, setForm] = useState(initial);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const guard = useRef(false);
  return <Modal title={title} onClose={onClose} className={wide ? 'admin-wide-dialog' : ''}><form className="cafe-form admin-form" onSubmit={async (event) => { event.preventDefault(); if (guard.current) return; guard.current = true; setBusy(true); try { await onSave(form); onClose(); } catch (err) { setError(err.message); } finally { guard.current = false; setBusy(false); } }}>{description && <p className="admin-form-note">{description}</p>}{error && <div className="cafe-error" role="alert">{error}</div>}{fields.map((field) => <Field key={field.name} field={field} value={form[field.name]} onChange={(value) => setForm((current) => ({ ...current, [field.name]: value }))}/>)}<div className="cafe-dialog-actions"><button type="button" className="cafe-button" disabled={busy} onClick={onClose}>Đóng</button><button type="submit" className="cafe-button primary" disabled={busy}>{busy ? 'Đang lưu…' : submit}</button></div></form></Modal>;
}
export function DetailDialog({ title, onClose, children }) { return <Modal title={title} onClose={onClose} className="admin-wide-dialog">{children}</Modal>; }
export function Row({ label, children }) { return <div className="cafe-detail-row"><span>{label}</span><strong>{children}</strong></div>; }
export function ExportButton({ filename, headers, rows, onError }) { return <button className="cafe-button" onClick={() => { try { const url = URL.createObjectURL(new Blob([csvText(headers, rows)], { type: 'text/csv;charset=utf-8' })); const anchor = document.createElement('a'); anchor.href = url; anchor.download = filename; document.body.appendChild(anchor); anchor.click(); anchor.remove(); setTimeout(() => URL.revokeObjectURL(url), 1000); } catch { onError?.('Không tải được CSV. Hãy kiểm tra quyền trình duyệt.'); } }}><Download size={15}/> Xuất CSV</button>; }
