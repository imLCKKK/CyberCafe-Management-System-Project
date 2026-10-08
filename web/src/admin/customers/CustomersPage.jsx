import React, { useState } from 'react';
import {
	ArrowDownToLine,
	ArrowUpDown,
	ChevronLeft,
	ChevronRight,
	Crown,
	Eye,
	Mail,
	MoreHorizontal,
	Pencil,
	Phone,
	Plus,
	Search,
	SlidersHorizontal,
	Sparkles,
	Users,
	Wallet,
	X,
} from 'lucide-react';
import { mockData } from './mockData';
import './customers.css';

const pageSize = 8;
const tierOptions = ['Diamond', 'Gold', 'Silver', 'Bronze'];
const emptyCustomer = {
	name: '',
	email: '',
	phone: '',
	tier: 'Bronze',
	status: 'active',
};

const money = (amount) => new Intl.NumberFormat('vi-VN', {
	style: 'currency',
	currency: 'VND',
	maximumFractionDigits: 0,
}).format(amount);

const shortDate = (date) => new Intl.DateTimeFormat('vi-VN', {
	day: '2-digit',
	month: '2-digit',
	year: 'numeric',
}).format(new Date(`${date}T00:00:00`));

const normalize = (value) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();

function getInitials(name) {
	return name.split(' ').slice(-2).map((part) => part[0]).join('').toUpperCase();
}

function CustomerModal({ mode, customer, onClose, onSave }) {
	const [form, setForm] = useState(customer || emptyCustomer);
	const isForm = mode === 'create' || mode === 'edit';
	const title = mode === 'create' ? 'Thêm khách hàng' : mode === 'edit' ? 'Chỉnh sửa khách hàng' : 'Thông tin khách hàng';

	function handleSubmit(event) {
		event.preventDefault();
		onSave(form);
	}

	function updateField(event) {
		const { name, value } = event.target;
		setForm((current) => ({ ...current, [name]: value }));
	}

	return (
		<div className="customer-modal-backdrop" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
			<section className="customer-modal" role="dialog" aria-modal="true" aria-labelledby="customer-modal-title">
				<div className="customer-modal-heading">
					<div>
						<span className="customer-kicker">HỒ SƠ KHÁCH HÀNG</span>
						<h2 id="customer-modal-title">{title}</h2>
					</div>
					<button className="customer-icon-button" type="button" aria-label="Đóng" onClick={onClose}><X size={19} /></button>
				</div>

				{isForm ? (
					<form onSubmit={handleSubmit}>
						<div className="customer-form-grid">
							<label className="customer-field customer-field-wide">
								<span>Họ và tên</span>
								<input name="name" value={form.name} onChange={updateField} placeholder="Nguyễn Văn An" required autoFocus />
							</label>
							<label className="customer-field">
								<span>Email</span>
								<input name="email" type="email" value={form.email} onChange={updateField} placeholder="email@example.com" required />
							</label>
							<label className="customer-field">
								<span>Số điện thoại</span>
								<input name="phone" type="tel" value={form.phone} onChange={updateField} placeholder="090 000 0000" required />
							</label>
							<label className="customer-field">
								<span>Hạng thành viên</span>
								<select name="tier" value={form.tier} onChange={updateField}>
									{tierOptions.map((tier) => <option key={tier} value={tier}>{tier}</option>)}
								</select>
							</label>
							<label className="customer-field">
								<span>Trạng thái</span>
								<select name="status" value={form.status} onChange={updateField}>
									<option value="active">Đang hoạt động</option>
									<option value="inactive">Ngừng hoạt động</option>
								</select>
							</label>
						</div>
						<div className="customer-modal-footer">
							<button className="customer-button customer-button-quiet" type="button" onClick={onClose}>Hủy</button>
							<button className="customer-button customer-button-primary" type="submit">{mode === 'create' ? 'Tạo khách hàng' : 'Lưu thay đổi'}</button>
						</div>
					</form>
				) : (
					<>
						<div className="customer-profile">
							<div className="customer-avatar customer-avatar-large">{getInitials(customer.name)}</div>
							<div>
								<h3>{customer.name}</h3>
								<span className={`customer-status ${customer.status}`}><i />{customer.status === 'active' ? 'Đang hoạt động' : 'Ngừng hoạt động'}</span>
							</div>
							<span className={`customer-tier tier-${customer.tier.toLowerCase()}`}><Crown size={13} />{customer.tier}</span>
						</div>
						<div className="customer-detail-list">
							<div><Mail size={16} /><span>Email</span><b>{customer.email}</b></div>
							<div><Phone size={16} /><span>Điện thoại</span><b>{customer.phone}</b></div>
							<div><Users size={16} /><span>Mã khách hàng</span><b>{customer.id}</b></div>
							<div><Sparkles size={16} /><span>Ngày tham gia</span><b>{shortDate(customer.joinedAt)}</b></div>
							<div><Wallet size={16} /><span>Số dư ví</span><b>{money(customer.walletBalance ?? 0)}</b></div>
							<div><Wallet size={16} /><span>Tổng chi tiêu</span><b>{money(customer.spent)}</b></div>
						</div>
						<div className="customer-modal-footer">
							<button className="customer-button customer-button-quiet" type="button" onClick={onClose}>Đóng</button>
							<button className="customer-button customer-button-primary" type="button" onClick={() => onSave(customer, 'edit')}><Pencil size={15} />Chỉnh sửa</button>
						</div>
					</>
				)}
			</section>
		</div>
	);
}

export function CustomersPage() {
	const [customers, setCustomers] = useState(mockData);
	const [query, setQuery] = useState('');
	const [statusFilter, setStatusFilter] = useState('all');
	const [tierFilter, setTierFilter] = useState('all');
	const [page, setPage] = useState(1);
	const [modal, setModal] = useState(null);
	const [menuFor, setMenuFor] = useState(null);

	const filteredCustomers = customers.filter((customer) => {
		const searchable = normalize(`${customer.name} ${customer.email} ${customer.phone} ${customer.id}`);
		return searchable.includes(normalize(query))
			&& (statusFilter === 'all' || customer.status === statusFilter)
			&& (tierFilter === 'all' || customer.tier === tierFilter);
	});
	const activeCount = customers.filter((customer) => customer.status === 'active').length;
	const totalSpent = customers.reduce((total, customer) => total + customer.spent, 0);
	const averageVisits = customers.length ? Math.round(customers.reduce((total, customer) => total + customer.visits, 0) / customers.length) : 0;
	const pageCount = Math.max(1, Math.ceil(filteredCustomers.length / pageSize));
	const visibleCustomers = filteredCustomers.slice((page - 1) * pageSize, page * pageSize);
	const firstResult = filteredCustomers.length ? (page - 1) * pageSize + 1 : 0;
	const lastResult = Math.min(page * pageSize, filteredCustomers.length);

	function updateFilter(setter, value) {
		setter(value);
		setPage(1);
	}

	function saveCustomer(customer, nextMode) {
		if (nextMode === 'edit') {
			setModal({ mode: 'edit', customer });
			return;
		}

		if (modal.mode === 'create') {
			const nextId = `KH-${Math.max(0, ...customers.map((item) => Number(item.id.replace('KH-', '')))) + 1}`;
					setCustomers((current) => [{ ...customer, id: nextId, visits: 0, spent: 0, walletBalance: 0, joinedAt: new Date().toISOString().slice(0, 10), lastVisit: new Date().toISOString().slice(0, 10) }, ...current]);
		} else {
			setCustomers((current) => current.map((item) => item.id === customer.id ? { ...item, ...customer } : item));
		}
		setModal(null);
	}

	function exportCustomers() {
		const headers = ['Mã khách hàng', 'Họ và tên', 'Email', 'Số điện thoại', 'Hạng', 'Trạng thái', 'Lượt ghé', 'Số dư ví', 'Tổng chi tiêu'];
		const rows = filteredCustomers.map((customer) => [customer.id, customer.name, customer.email, customer.phone, customer.tier, customer.status, customer.visits, customer.walletBalance ?? 0, customer.spent]);
		const csv = [headers, ...rows].map((row) => row.map((value) => `"${String(value).replaceAll('"', '""')}"`).join(',')).join('\n');
		const url = URL.createObjectURL(new Blob(['\ufeff', csv], { type: 'text/csv;charset=utf-8' }));
		const link = document.createElement('a');
		link.href = url;
		link.download = 'danh-sach-khach-hang.csv';
		link.click();
		URL.revokeObjectURL(url);
	}

	function removeCustomer(customer) {
		if (window.confirm(`Xóa khách hàng ${customer.name}?`)) {
			setCustomers((current) => current.filter((item) => item.id !== customer.id));
			setMenuFor(null);
		}
	}

	return (
		<section className="page customers-page">
			<div className="customers-content">
				<div className="customers-heading">
					<div>
						<div className="customers-eyebrow">QUẢN LÝ THÀNH VIÊN</div>
						<h2>Khách hàng</h2>
						<p>Theo dõi hồ sơ, hoạt động và giá trị thành viên tại quán.</p>
					</div>
					<div className="customers-heading-actions">
						<button className="customer-button customer-button-outline" type="button" onClick={exportCustomers}><ArrowDownToLine size={16} />Xuất danh sách</button>
						<button className="customer-button customer-button-primary" type="button" onClick={() => setModal({ mode: 'create', customer: emptyCustomer })}><Plus size={17} />Thêm khách hàng</button>
					</div>
				</div>

				<div className="customer-stats">
					<article className="customer-stat">
						<div className="customer-stat-icon stat-blue"><Users size={19} /></div>
						<span>Tổng khách hàng</span>
						<strong>{customers.length.toLocaleString('vi-VN')}</strong>
						<small>Hồ sơ thành viên</small>
					</article>
					<article className="customer-stat">
						<div className="customer-stat-icon stat-green"><Sparkles size={18} /></div>
						<span>Đang hoạt động</span>
						<strong>{activeCount.toLocaleString('vi-VN')}</strong>
						<small>{customers.length ? Math.round((activeCount / customers.length) * 100) : 0}% tổng thành viên</small>
					</article>
					<article className="customer-stat">
						<div className="customer-stat-icon stat-coral"><Wallet size={18} /></div>
						<span>Tổng chi tiêu</span>
						<strong>{money(totalSpent)}</strong>
						<small>Giá trị vòng đời thành viên</small>
					</article>
					<article className="customer-stat">
						<div className="customer-stat-icon stat-yellow"><Crown size={18} /></div>
						<span>Lượt ghé trung bình</span>
						<strong>{averageVisits}<small className="customer-stat-unit">lượt</small></strong>
						<small>Trên mỗi khách hàng</small>
					</article>
				</div>

				<section className="customer-list-panel">
					<div className="customer-list-header">
						<div>
							<h3>Danh sách thành viên</h3>
							<p>{filteredCustomers.length} khách hàng phù hợp</p>
						</div>
						<label className="customer-search">
							<Search size={17} />
							<input value={query} onChange={(event) => updateFilter(setQuery, event.target.value)} placeholder="Tìm tên, email, số điện thoại..." aria-label="Tìm khách hàng" />
							{query && <button type="button" aria-label="Xóa tìm kiếm" onClick={() => updateFilter(setQuery, '')}><X size={15} /></button>}
						</label>
					</div>

					<div className="customer-toolbar">
						<div className="customer-tabs" role="tablist" aria-label="Lọc trạng thái khách hàng">
							{[['all', 'Tất cả'], ['active', 'Đang hoạt động'], ['inactive', 'Ngừng hoạt động']].map(([value, label]) => (
								<button key={value} type="button" role="tab" aria-selected={statusFilter === value} className={statusFilter === value ? 'selected' : ''} onClick={() => updateFilter(setStatusFilter, value)}>{label}</button>
							))}
						</div>
						<label className="customer-tier-filter"><SlidersHorizontal size={15} /><span>Hạng</span>
							<select value={tierFilter} onChange={(event) => updateFilter(setTierFilter, event.target.value)} aria-label="Lọc theo hạng thành viên">
								<option value="all">Tất cả hạng</option>
								{tierOptions.map((tier) => <option key={tier} value={tier}>{tier}</option>)}
							</select>
						</label>
					</div>

					<div className="customer-table-wrap">
						<table className="customer-table">
							<thead><tr>
								<th><span>Khách hàng</span><ArrowUpDown size={13} /></th>
								<th>Hạng thành viên</th>
								<th>Trạng thái</th>
								<th>Lượt ghé</th>
								<th>Số dư ví</th>
								<th>Tổng chi tiêu</th>
								<th>Lần ghé gần nhất</th>
								<th><span className="sr-only">Thao tác</span></th>
							</tr></thead>
							<tbody>
								{visibleCustomers.map((customer) => (
									<tr key={customer.id}>
										<td>
											<button className="customer-identity" type="button" onClick={() => setModal({ mode: 'view', customer })}>
												<span className={`customer-avatar avatar-${customer.tier.toLowerCase()}`}>{getInitials(customer.name)}</span>
												<span><b>{customer.name}</b><small>{customer.email}</small></span>
											</button>
										</td>
										<td><span className={`customer-tier tier-${customer.tier.toLowerCase()}`}><Crown size={13} />{customer.tier}</span></td>
										<td><span className={`customer-status ${customer.status}`}><i />{customer.status === 'active' ? 'Hoạt động' : 'Ngừng hoạt động'}</span></td>
										<td className="customer-visits">{customer.visits}<small> lượt</small></td>
										<td className="customer-wallet">{money(customer.walletBalance ?? 0)}</td>
										<td className="customer-spent">{money(customer.spent)}</td>
										<td className="customer-date">{shortDate(customer.lastVisit)}</td>
										<td className="customer-row-actions">
											<button className="customer-icon-button" type="button" aria-label={`Xem ${customer.name}`} title="Xem hồ sơ" onClick={() => setModal({ mode: 'view', customer })}><Eye size={16} /></button>
											<div className="customer-menu-wrap">
												<button className="customer-icon-button" type="button" aria-label={`Thao tác với ${customer.name}`} title="Thao tác" onClick={() => setMenuFor(menuFor === customer.id ? null : customer.id)}><MoreHorizontal size={18} /></button>
												{menuFor === customer.id && <div className="customer-row-menu">
													<button type="button" onClick={() => { setModal({ mode: 'edit', customer }); setMenuFor(null); }}><Pencil size={14} />Chỉnh sửa</button>
													<button className="customer-delete-action" type="button" onClick={() => removeCustomer(customer)}><X size={14} />Xóa khách hàng</button>
												</div>}
											</div>
										</td>
									</tr>
								))}
								{!visibleCustomers.length && <tr><td className="customer-empty" colSpan="8"><Search size={22} /><b>Không tìm thấy khách hàng</b><span>Thử thay đổi từ khóa hoặc bộ lọc.</span></td></tr>}
							</tbody>
						</table>
					</div>

					<footer className="customer-pagination">
						<span>Hiển thị <b>{firstResult}–{lastResult}</b> trong <b>{filteredCustomers.length}</b> khách hàng</span>
						<div>
							<button type="button" aria-label="Trang trước" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}><ChevronLeft size={17} /></button>
							<span>Trang <b>{page}</b> / {pageCount}</span>
							<button type="button" aria-label="Trang sau" disabled={page >= pageCount} onClick={() => setPage((current) => current + 1)}><ChevronRight size={17} /></button>
						</div>
					</footer>
				</section>
			</div>
			{modal && <CustomerModal mode={modal.mode} customer={modal.customer} onClose={() => setModal(null)} onSave={saveCustomer} />}
		</section>
	);
}
