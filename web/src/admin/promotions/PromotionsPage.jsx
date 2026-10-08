import React, { useState } from 'react';
import {
	ArrowDownToLine,
	BadgePercent,
	CalendarDays,
	ChevronLeft,
	ChevronRight,
	CircleDollarSign,
	Clock3,
	Pencil,
	Plus,
	Search,
	TicketPercent,
	X,
} from 'lucide-react';
import { mockData } from './mockData';
import './promotions.css';

const pageSize = 6;
const discountTypes = [
	{ value: 'Percentage', label: 'Phần trăm' },
	{ value: 'FixedAmount', label: 'Số tiền cố định' },
];
const statusInfo = {
	active: { label: 'Đang áp dụng', className: 'active' },
	scheduled: { label: 'Sắp diễn ra', className: 'scheduled' },
	expired: { label: 'Đã kết thúc', className: 'expired' },
	paused: { label: 'Tạm dừng', className: 'paused' },
};
const emptyPromotion = { name: '', discountType: 'Percentage', discountValue: '10', startDate: '', endDate: '', isActive: true };

const money = (amount) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(amount || 0);
const dateTime = (value) => new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value));

function getPromotionStatus(promotion, now = new Date()) {
	if (!promotion.isActive) return 'paused';
	if (now < new Date(promotion.startDate)) return 'scheduled';
	if (now > new Date(promotion.endDate)) return 'expired';
	return 'active';
}

function displayDiscount(promotion) {
	return promotion.discountType === 'Percentage' ? `${promotion.discountValue}%` : money(promotion.discountValue);
}

function getDefaultDates() {
	const now = new Date();
	const end = new Date(now);
	end.setDate(now.getDate() + 30);
	const toInput = (date) => new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
	return { startDate: toInput(now), endDate: toInput(end) };
}

function PromotionModal({ mode, promotion, onClose, onSave }) {
	const [form, setForm] = useState(mode === 'create' ? { ...emptyPromotion, ...getDefaultDates() } : { ...promotion, discountValue: String(promotion.discountValue) });
	const isCreate = mode === 'create';

	function updateField(event) {
		const { name, value, checked, type } = event.target;
		setForm((current) => ({ ...current, [name]: type === 'checkbox' ? checked : value }));
	}

	function submit(event) {
		event.preventDefault();
		onSave({ ...form, discountValue: Number(form.discountValue) });
	}

	return (
		<div className="promotion-backdrop" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
			<section className="promotion-modal" role="dialog" aria-modal="true" aria-labelledby="promotion-modal-title">
				<div className="promotion-modal-heading">
					<div><span className="promotion-kicker">ƯU ĐÃI THÀNH VIÊN</span><h2 id="promotion-modal-title">{isCreate ? 'Tạo khuyến mãi' : 'Chỉnh sửa khuyến mãi'}</h2></div>
					<button className="promotion-icon-button" type="button" aria-label="Đóng" onClick={onClose}><X size={19} /></button>
				</div>
				<form onSubmit={submit}>
					<div className="promotion-form-grid">
						<label className="promotion-field promotion-field-wide"><span>Tên chương trình</span><input name="name" value={form.name} onChange={updateField} placeholder="Ví dụ: Giờ vàng ngày thường" required autoFocus /></label>
						<label className="promotion-field"><span>Loại ưu đãi</span><select name="discountType" value={form.discountType} onChange={updateField}>{discountTypes.map((type) => <option key={type.value} value={type.value}>{type.label}</option>)}</select></label>
						<label className="promotion-field"><span>{form.discountType === 'Percentage' ? 'Mức giảm (%)' : 'Số tiền giảm (VNĐ)'}</span><input name="discountValue" type="number" min={form.discountType === 'Percentage' ? '1' : '1000'} max={form.discountType === 'Percentage' ? '100' : undefined} step={form.discountType === 'Percentage' ? '0.1' : '1000'} value={form.discountValue} onChange={updateField} required /></label>
						<label className="promotion-field"><span>Bắt đầu lúc</span><input name="startDate" type="datetime-local" value={form.startDate} onChange={updateField} required /></label>
						<label className="promotion-field"><span>Kết thúc lúc</span><input name="endDate" type="datetime-local" min={form.startDate} value={form.endDate} onChange={updateField} required /></label>
					</div>
					<label className="promotion-active-toggle"><input name="isActive" type="checkbox" checked={form.isActive} onChange={updateField} /><span className="promotion-toggle-track"><i /></span><span><b>Kích hoạt chương trình</b><small>Khuyến mãi có hiệu lực theo khung thời gian đã chọn.</small></span></label>
					<div className="promotion-modal-footer"><button className="promotion-button promotion-button-quiet" type="button" onClick={onClose}>Hủy</button><button className="promotion-button promotion-button-primary" type="submit">{isCreate ? 'Tạo khuyến mãi' : 'Lưu thay đổi'}</button></div>
				</form>
			</section>
		</div>
	);
}

export function PromotionsPage() {
	const [promotions, setPromotions] = useState(mockData);
	const [query, setQuery] = useState('');
	const [statusFilter, setStatusFilter] = useState('all');
	const [typeFilter, setTypeFilter] = useState('all');
	const [page, setPage] = useState(1);
	const [modal, setModal] = useState(null);
	const now = new Date();

	const rows = promotions.map((promotion) => ({ ...promotion, computedStatus: getPromotionStatus(promotion, now) }));
	const filtered = rows.filter((promotion) => promotion.name.toLocaleLowerCase('vi').includes(query.trim().toLocaleLowerCase('vi'))
		&& (statusFilter === 'all' || promotion.computedStatus === statusFilter)
		&& (typeFilter === 'all' || promotion.discountType === typeFilter));
	const activeCount = rows.filter((item) => item.computedStatus === 'active').length;
	const scheduledCount = rows.filter((item) => item.computedStatus === 'scheduled').length;
	const expiredCount = rows.filter((item) => item.computedStatus === 'expired').length;
	const pageCount = Math.max(1, Math.ceil(filtered.length / pageSize));
	const visible = filtered.slice((page - 1) * pageSize, page * pageSize);
	const first = filtered.length ? (page - 1) * pageSize + 1 : 0;
	const last = Math.min(page * pageSize, filtered.length);

	function updateFilter(setter, value) {
		setter(value);
		setPage(1);
	}

	function savePromotion(promotion) {
		if (modal.mode === 'create') {
			const nextId = Math.max(0, ...promotions.map((item) => item.id)) + 1;
			setPromotions((current) => [{ ...promotion, id: nextId }, ...current]);
		} else {
			setPromotions((current) => current.map((item) => item.id === promotion.id ? { ...item, ...promotion } : item));
		}
		setModal(null);
	}

	function togglePromotion(promotion) {
		setPromotions((current) => current.map((item) => item.id === promotion.id ? { ...item, isActive: !item.isActive } : item));
	}

	function exportPromotions() {
		const headers = ['Mã khuyến mãi', 'Tên chương trình', 'Loại giảm', 'Giá trị', 'Bắt đầu', 'Kết thúc', 'Đang bật', 'Trạng thái'];
		const csvRows = filtered.map((item) => [item.id, item.name, item.discountType, item.discountValue, item.startDate, item.endDate, item.isActive ? 'Có' : 'Không', statusInfo[item.computedStatus].label]);
		const csv = [headers, ...csvRows].map((row) => row.map((value) => `"${String(value).replaceAll('"', '""')}"`).join(',')).join('\n');
		const url = URL.createObjectURL(new Blob(['\ufeff', csv], { type: 'text/csv;charset=utf-8' }));
		const link = document.createElement('a');
		link.href = url;
		link.download = 'danh-sach-khuyen-mai.csv';
		link.click();
		URL.revokeObjectURL(url);
	}

	return (
		<section className="page promotions-page">
			<div className="promotions-content">
				<div className="promotions-heading">
					<div><div className="promotions-eyebrow">CHĂM SÓC KHÁCH HÀNG</div><h2>Khuyến mãi</h2><p>Quản lý ưu đãi theo thời hạn và mức giảm.</p></div>
					<div className="promotions-heading-actions"><button className="promotion-button promotion-button-outline" type="button" onClick={exportPromotions}><ArrowDownToLine size={16} />Xuất danh sách</button><button className="promotion-button promotion-button-primary" type="button" onClick={() => setModal({ mode: 'create' })}><Plus size={17} />Tạo khuyến mãi</button></div>
				</div>
				<div className="promotion-stats">
					<article className="promotion-stat"><div className="promotion-stat-icon stat-lime"><BadgePercent size={19} /></div><span>Đang áp dụng</span><strong>{activeCount}</strong><small>Khuyến mãi trong thời hạn</small></article>
					<article className="promotion-stat"><div className="promotion-stat-icon stat-blue"><CalendarDays size={19} /></div><span>Sắp diễn ra</span><strong>{scheduledCount}</strong><small>Đã bật, chưa đến ngày bắt đầu</small></article>
					<article className="promotion-stat"><div className="promotion-stat-icon stat-amber"><Clock3 size={19} /></div><span>Đã kết thúc</span><strong>{expiredCount}</strong><small>Hết thời hạn khuyến mãi</small></article>
					<article className="promotion-stat"><div className="promotion-stat-icon stat-teal"><TicketPercent size={19} /></div><span>Tổng chương trình</span><strong>{promotions.length}</strong><small>Trong danh sách quản lý</small></article>
				</div>
				<section className="promotion-list-panel">
					<div className="promotion-list-heading"><div><h3>Danh sách chương trình</h3><p>{filtered.length} khuyến mãi phù hợp</p></div><label className="promotion-search"><Search size={17} /><input value={query} onChange={(event) => updateFilter(setQuery, event.target.value)} placeholder="Tìm tên chương trình..." aria-label="Tìm khuyến mãi" />{query && <button type="button" aria-label="Xóa tìm kiếm" onClick={() => updateFilter(setQuery, '')}><X size={15} /></button>}</label></div>
					<div className="promotion-toolbar">
						<div className="promotion-tabs" role="tablist" aria-label="Lọc trạng thái khuyến mãi">{[['all', 'Tất cả'], ['active', 'Đang áp dụng'], ['scheduled', 'Sắp diễn ra'], ['expired', 'Đã kết thúc'], ['paused', 'Tạm dừng']].map(([value, label]) => <button key={value} type="button" role="tab" aria-selected={statusFilter === value} className={statusFilter === value ? 'selected' : ''} onClick={() => updateFilter(setStatusFilter, value)}>{label}</button>)}</div>
						<label className="promotion-type-filter"><CircleDollarSign size={15} /><span>Loại giảm</span><select value={typeFilter} onChange={(event) => updateFilter(setTypeFilter, event.target.value)} aria-label="Lọc loại giảm"><option value="all">Tất cả</option>{discountTypes.map((type) => <option key={type.value} value={type.value}>{type.label}</option>)}</select></label>
					</div>
					<div className="promotion-table-wrap"><table className="promotion-table"><thead><tr><th>Chương trình</th><th>Mức ưu đãi</th><th>Thời hạn</th><th>Trạng thái</th><th>Đang bật</th><th><span className="sr-only">Thao tác</span></th></tr></thead><tbody>
						{visible.map((promotion) => <tr key={promotion.id}>
							<td><div className="promotion-identity"><span className="promotion-mark"><TicketPercent size={18} /></span><span><b>{promotion.name}</b><small>Mã KM-{String(promotion.id).padStart(4, '0')}</small></span></div></td>
							<td><span className="promotion-value">{displayDiscount(promotion)}</span><small className="promotion-cell-subtext">{promotion.discountType === 'Percentage' ? 'Phần trăm' : 'Số tiền cố định'}</small></td>
							<td><span className="promotion-date-primary">{dateTime(promotion.startDate).split(' ')[0]} – {dateTime(promotion.endDate).split(' ')[0]}</span><small className="promotion-cell-subtext">{dateTime(promotion.startDate).split(' ').slice(1).join(' ')} – {dateTime(promotion.endDate).split(' ').slice(1).join(' ')}</small></td>
							<td><span className={`promotion-status status-${statusInfo[promotion.computedStatus].className}`}><i />{statusInfo[promotion.computedStatus].label}</span></td>
							<td><button className={`promotion-switch ${promotion.isActive ? 'on' : ''}`} type="button" role="switch" aria-checked={promotion.isActive} aria-label={`${promotion.isActive ? 'Tắt' : 'Bật'} ${promotion.name}`} onClick={() => togglePromotion(promotion)}><span /></button></td>
							<td className="promotion-actions"><button className="promotion-icon-button" type="button" title="Chỉnh sửa" aria-label={`Chỉnh sửa ${promotion.name}`} onClick={() => setModal({ mode: 'edit', promotion })}><Pencil size={16} /></button></td>
						</tr>)}
						{!visible.length && <tr><td className="promotion-empty" colSpan="6"><Search size={22} /><b>Không tìm thấy khuyến mãi</b><span>Thử đổi từ khóa hoặc bộ lọc.</span></td></tr>}
					</tbody></table></div>
					<footer className="promotion-pagination"><span>Hiển thị <b>{first}–{last}</b> trong <b>{filtered.length}</b> chương trình</span><div><button type="button" aria-label="Trang trước" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}><ChevronLeft size={17} /></button><span>Trang <b>{page}</b> / {pageCount}</span><button type="button" aria-label="Trang sau" disabled={page >= pageCount} onClick={() => setPage((current) => current + 1)}><ChevronRight size={17} /></button></div></footer>
				</section>
			</div>
			{modal && <PromotionModal mode={modal.mode} promotion={modal.promotion} onClose={() => setModal(null)} onSave={savePromotion} />}
		</section>
	);
}
