import React, { useState } from 'react';
import {
	ArrowDownLeft,
	ArrowDownToLine,
	ArrowUpRight,
	Banknote,
	Check,
	Clock3,
	CreditCard,
	Gamepad2,
	History,
	Plus,
	QrCode,
	RefreshCw,
	Search,
	ShieldCheck,
	Utensils,
	WalletCards,
	X,
} from 'lucide-react';
import { initialBalance, mockData, topUpPromoCodes } from './mockData';
import './wallet.css';

const presets = [50000, 100000, 200000, 500000];
const filters = [
	{ value: 'all', label: 'Tất cả' },
	{ value: 'income', label: 'Tiền vào' },
	{ value: 'spending', label: 'Chi tiêu' },
];

const money = (value) => new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 }).format(value);
const dateTime = (value) => new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value));

function getTransactionKind(transaction) {
	if (transaction.type === 'TopUp') return 'topup';
	if (transaction.type === 'Refund') return 'refund';
	if (transaction.type === 'GamingSession') return 'gaming';
	return 'order';
}

function TransactionIcon({ type }) {
	if (type === 'TopUp') return <ArrowDownLeft size={17} />;
	if (type === 'Refund') return <RefreshCw size={16} />;
	if (type === 'GamingSession') return <Gamepad2 size={17} />;
	return <Utensils size={16} />;
}

function getPromoDiscount(promotion, amount) {
	if (!promotion || !amount) return 0;
	return promotion.discountType === 'Percentage'
		? Math.round(amount * promotion.discountValue / 100)
		: Math.min(amount, promotion.discountValue);
}

function TopUpModal({ onClose, onConfirm }) {
	const [amount, setAmount] = useState(100000);
	const [method, setMethod] = useState('bank');
	const [error, setError] = useState('');
	const [promoCode, setPromoCode] = useState('');
	const [appliedPromo, setAppliedPromo] = useState(null);
	const [promoMessage, setPromoMessage] = useState('');
	const [promoError, setPromoError] = useState('');
	const discount = getPromoDiscount(appliedPromo, Number(amount));
	const payableAmount = Math.max(0, Number(amount) - discount);

	function applyPromo() {
		const promo = topUpPromoCodes.find((item) => item.code === promoCode.trim().toUpperCase());
		const now = new Date();
		if (!promo || !promo.isActive || now < new Date(promo.startDate) || now > new Date(promo.endDate)) {
			setAppliedPromo(null);
			setPromoMessage('');
			setPromoError('Mã giảm giá không hợp lệ hoặc đã hết hạn.');
			return;
		}
		setAppliedPromo(promo);
		setPromoError('');
		setPromoMessage(`Đã áp dụng ${promo.code} · ${promo.name}`);
	}

	function submit(event) {
		event.preventDefault();
		if (amount < 10000) {
			setError('Số tiền nạp tối thiểu là 10.000đ.');
			return;
		}
		onConfirm(Number(amount), method, appliedPromo, discount);
	}

	return (
		<div className="wallet-modal-backdrop" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
			<section className="wallet-modal" role="dialog" aria-modal="true" aria-labelledby="wallet-modal-title">
				<div className="wallet-modal-heading"><div><span className="wallet-kicker">NẠP SỐ DƯ</span><h2 id="wallet-modal-title">Nạp tiền vào ví</h2></div><button className="wallet-icon-button" type="button" aria-label="Đóng" onClick={onClose}><X size={18} /></button></div>
				<form onSubmit={submit}>
					<div className="wallet-presets" aria-label="Chọn số tiền nạp">{presets.map((preset) => <button className={Number(amount) === preset ? 'selected' : ''} key={preset} type="button" onClick={() => { setAmount(preset); setError(''); }}>{money(preset)}đ</button>)}</div>
					<label className="wallet-field"><span>Số tiền khác</span><div className="wallet-amount-input"><input type="number" min="10000" step="1000" value={amount} onChange={(event) => { setAmount(event.target.value); setError(''); }} required /><span>đ</span></div></label>
					<div className="wallet-promo-entry"><label className="wallet-field"><span>Mã giảm giá</span><input className="wallet-promo-input" value={promoCode} onChange={(event) => { setPromoCode(event.target.value.toUpperCase()); setAppliedPromo(null); setPromoMessage(''); setPromoError(''); }} placeholder="Nhập mã giảm giá" aria-label="Mã giảm giá" /></label><button className="wallet-promo-apply" type="button" onClick={applyPromo}>Áp dụng</button></div>
					{promoError && <p className="wallet-form-error" role="alert">{promoError}</p>}
					{promoMessage && <p className="wallet-promo-success" role="status"><Check size={14} />{promoMessage}<button type="button" onClick={() => { setAppliedPromo(null); setPromoMessage(''); setPromoCode(''); }}>Gỡ mã</button></p>}
					<div className="wallet-payment-summary"><div><span>Số tiền nạp vào ví</span><b>{money(Number(amount) || 0)}đ</b></div>{appliedPromo && <div className="wallet-payment-discount"><span>Ưu đãi {appliedPromo.code}</span><b>−{money(discount)}đ</b></div>}<div className="wallet-payment-total"><span>Thanh toán mô phỏng</span><b>{money(payableAmount)}đ</b></div></div>
					<div className="wallet-method-label">Phương thức nạp</div>
					<div className="wallet-methods">
						<button type="button" className={method === 'bank' ? 'selected' : ''} aria-pressed={method === 'bank'} onClick={() => setMethod('bank')}><QrCode size={17} /><span><b>Ngân hàng</b><small>Chuyển khoản mô phỏng</small></span>{method === 'bank' && <Check size={15} />}</button>
						<button type="button" className={method === 'counter' ? 'selected' : ''} aria-pressed={method === 'counter'} onClick={() => setMethod('counter')}><Banknote size={17} /><span><b>Tiền mặt</b><small>Nạp tại quầy</small></span>{method === 'counter' && <Check size={15} />}</button>
					</div>
					{error && <p className="wallet-form-error" role="alert">{error}</p>}
					<p className="wallet-simulation-note"><ShieldCheck size={15} /> Giao dịch minh họa, chưa kết nối cổng thanh toán.</p>
					<div className="wallet-modal-footer"><button className="wallet-button wallet-button-quiet" type="button" onClick={onClose}>Hủy</button><button className="wallet-button wallet-button-primary" type="submit"><Plus size={16} />Thanh toán {money(payableAmount)}đ</button></div>
				</form>
			</section>
		</div>
	);
}

export function WalletPage() {
	const [balance, setBalance] = useState(initialBalance);
	const [transactions, setTransactions] = useState(mockData);
	const [filter, setFilter] = useState('all');
	const [query, setQuery] = useState('');
	const [topUpOpen, setTopUpOpen] = useState(false);
	const [toast, setToast] = useState('');
	const now = new Date();
	const currentMonth = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
	const monthTopUps = transactions.filter((transaction) => transaction.type === 'TopUp' && transaction.createdAt.startsWith(currentMonth)).reduce((sum, transaction) => sum + transaction.amount, 0);
	const todaySpending = transactions.filter((transaction) => transaction.amount < 0 && transaction.createdAt.slice(0, 10) === now.toISOString().slice(0, 10)).reduce((sum, transaction) => sum + Math.abs(transaction.amount), 0);
	const visibleTransactions = transactions.filter((transaction) => {
		const kind = getTransactionKind(transaction);
		return (filter === 'all' || (filter === 'income' ? transaction.amount > 0 : transaction.amount < 0))
			&& transaction.description.toLocaleLowerCase('vi').includes(query.trim().toLocaleLowerCase('vi'));
	});

	function completeTopUp(amount, method, appliedPromo, discount) {
		const nextBalance = balance + amount;
		const methodLabel = method === 'bank' ? 'Chuyển khoản ngân hàng' : 'Nạp tiền tại quầy';
		const promoLabel = appliedPromo ? ` · ${appliedPromo.code} giảm ${money(discount)}đ` : '';
		const transaction = {
			id: Math.max(0, ...transactions.map((item) => item.id)) + 1,
			amount,
			balanceAfter: nextBalance,
			type: 'TopUp',
			description: `Nạp tiền · ${methodLabel}${promoLabel}`,
			createdAt: new Date().toISOString(),
		};
		setBalance(nextBalance);
		setTransactions((current) => [transaction, ...current]);
		setToast(appliedPromo ? `Đã nạp ${money(amount)}đ, áp dụng ${appliedPromo.code} giảm ${money(discount)}đ.` : `Đã nạp ${money(amount)}đ vào ví.`);
		setTopUpOpen(false);
	}

	function exportTransactions() {
		const headers = ['Mã giao dịch', 'Loại giao dịch', 'Mô tả', 'Số tiền', 'Số dư sau giao dịch', 'Thời gian'];
		const rows = visibleTransactions.map((item) => [item.id, item.type, item.description, item.amount, item.balanceAfter, item.createdAt]);
		const csv = [headers, ...rows].map((row) => row.map((value) => `"${String(value).replaceAll('"', '""')}"`).join(',')).join('\n');
		const url = URL.createObjectURL(new Blob(['\ufeff', csv], { type: 'text/csv;charset=utf-8' }));
		const link = document.createElement('a');
		link.href = url;
		link.download = 'lich-su-giao-dich-vi.csv';
		link.click();
		URL.revokeObjectURL(url);
	}

	return (
		<div className="client-page wallet-page">
			<div className="client-page-heading wallet-page-heading"><div><h1>Ví của tôi</h1><p>Quản lý số dư và theo dõi các giao dịch tại TRAM.</p></div><button className="wallet-outline-button" type="button" onClick={exportTransactions}><ArrowDownToLine size={15} />Xuất lịch sử</button></div>
			{toast && <div className="wallet-toast" role="status"><Check size={16} />{toast}<button type="button" aria-label="Đóng thông báo" onClick={() => setToast('')}><X size={15} /></button></div>}

			<div className="wallet-overview">
				<section className="wallet-balance-card">
					<div className="wallet-balance-top"><span><WalletCards size={16} />SỐ DƯ KHẢ DỤNG</span><span className="wallet-card-chip">TRAM WALLET</span></div>
					<div className="wallet-balance-amount">{money(balance)}<small>đ</small></div>
					<div className="wallet-balance-foot"><span><i />Ví sẵn sàng sử dụng</span><button type="button" onClick={() => setTopUpOpen(true)}><Plus size={15} />Nạp tiền</button></div>
				</section>
				<div className="wallet-summary-grid">
					<article className="wallet-summary-card"><span><ArrowDownLeft size={15} />Đã nạp tháng này</span><b>{money(monthTopUps)}<small>đ</small></b><p>Tổng tiền nạp thành công</p></article>
					<article className="wallet-summary-card"><span><ArrowUpRight size={15} />Chi tiêu hôm nay</span><b>{money(todaySpending)}<small>đ</small></b><p>Phiên chơi và gọi món</p></article>
				</div>
			</div>

			<section className="wallet-history-section">
				<div className="wallet-history-heading"><div><h2>Lịch sử giao dịch</h2><p>{visibleTransactions.length} giao dịch</p></div><label className="wallet-search"><Search size={16} /><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Tìm trong mô tả..." aria-label="Tìm giao dịch" />{query && <button type="button" aria-label="Xóa tìm kiếm" onClick={() => setQuery('')}><X size={14} /></button>}</label></div>
				<div className="wallet-history-toolbar"><div className="wallet-filter-tabs" role="tablist" aria-label="Lọc giao dịch">{filters.map((item) => <button key={item.value} type="button" role="tab" aria-selected={filter === item.value} className={filter === item.value ? 'selected' : ''} onClick={() => setFilter(item.value)}>{item.label}</button>)}</div><span className="wallet-ledger-label"><History size={14} />SỔ GIAO DỊCH</span></div>
				<div className="wallet-transaction-list">
					{visibleTransactions.map((transaction) => {
						const kind = getTransactionKind(transaction);
						return <article className="wallet-transaction" key={transaction.id}>
							<div className={`wallet-transaction-icon kind-${kind}`}><TransactionIcon type={transaction.type} /></div>
							<div className="wallet-transaction-copy"><b>{transaction.description}</b><span>Mã GD #{transaction.id} · {dateTime(transaction.createdAt)}</span></div>
							<div className={`wallet-transaction-amount ${transaction.amount > 0 ? 'positive' : 'negative'}`}>{transaction.amount > 0 ? '+' : '−'}{money(Math.abs(transaction.amount))}đ</div>
							<div className="wallet-transaction-balance"><span>Số dư sau GD</span><b>{money(transaction.balanceAfter)}đ</b></div>
						</article>;
					})}
					{!visibleTransactions.length && <div className="wallet-empty-state"><Search size={22} /><b>Không tìm thấy giao dịch</b><span>Thử từ khóa hoặc bộ lọc khác.</span></div>}
				</div>
				<div className="wallet-history-foot"><span><ShieldCheck size={14} />Lịch sử ví được lưu theo từng giao dịch.</span><span>{transactions.length} giao dịch gần đây</span></div>
			</section>

			{topUpOpen && <TopUpModal onClose={() => setTopUpOpen(false)} onConfirm={completeTopUp} />}
		</div>
	);
}
