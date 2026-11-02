import React, { useState } from 'react';
import { ArrowDownLeft, ArrowUpRight, ChevronRight, Clock3, Coins, ReceiptText, WalletCards } from 'lucide-react';
import HomeDialog from '../shared/Dialog';
import { Badge, Empty, Feedback, PageHeading } from '../shared/ClientUI';
import { dateTime, money, transactionLabels } from '../shared/demoModel';
import { requestClientTopUp } from '../../services/walletApi';
import { useWalletData } from './useWalletData';
import { TopUpPanel } from './TopUpPanel';

export function WalletPage() {
  const { status, profile, wallet, error: loadError, reload } = useWalletData();
  const [amount, setAmount] = useState('100000');
  const [tab, setTab] = useState('transactions');
  const [filter, setFilter] = useState('all');
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [detail, setDetail] = useState(null);

  if (status === 'loading') return <div className="cafe-home client-module wallet-page"><p className="home-muted" role="status">Đang tải ví...</p></div>;
  if (status === 'error') return <div className="cafe-home client-module wallet-page"><div className="home-alert" role="alert">{loadError}</div><button className="home-button primary" onClick={reload}>Thử lại</button></div>;

  // Dữ liệu từ API (SQL Server), đưa về dạng các thành phần giao diện hiện có đang dùng.
  const customer = { id: profile.customerId, wallet_balance: wallet.walletBalance };
  const account = { display_name: profile.displayName, is_active: profile.isActive, must_change_password: profile.mustChangePassword };
  const session = profile.hasActiveSession ? { hourly_rate_snapshot: profile.activeHourlyRate } : null;
  const transactions = wallet.transactions;
  const topUps = wallet.topUps;
  const waiting = topUps.find((r) => ['Pending', 'CreditedUnpaid'].includes(r.status));
  const rows = transactions.filter((r) => filter === 'all' || (filter === 'in' ? r.amount > 0 : r.amount < 0));
  const deposited = transactions.filter((r) => r.transaction_type === 'TopUpCredit').reduce((s, r) => s + r.amount, 0);
  const spent = transactions.filter((r) => r.amount < 0).reduce((s, r) => s - r.amount, 0);
  const refunded = transactions.filter((r) => ['OrderItemRefund', 'TournamentRefund'].includes(r.transaction_type)).reduce((s, r) => s + r.amount, 0);
  const rate = Number(session?.hourly_rate_snapshot) || 0;
  async function requestCashTopUp() {
    if (submitting) return;
    setSubmitting(true);
    try {
      await requestClientTopUp(Number(amount));
      setError('');
      setNotice('Đã gửi yêu cầu nạp tiền mặt. Nhân viên sẽ xử lý tại quầy.');
      await reload();
    } catch (err) { setError(err.message); }
    finally { setSubmitting(false); }
  }
  return <div className="cafe-home client-module wallet-page"><PageHeading eyebrow="TRAM WALLET" title={<>Ví của <span>bạn.</span></>} description="Nạp thêm giờ vui. Mọi khoản chi đều rõ ràng trong một nơi."><span className="client-heading-chip"><WalletCards size={16}/> Ví #{String(customer.id).padStart(6, '0')}</span></PageHeading><Feedback notice={notice} error={error} onClear={() => setNotice('')}/>
    <div className="wallet-top-grid"><section className="home-panel wallet-hero"><div className="wallet-hero-top"><span><WalletCards size={20}/> TRAM / VÍ NGƯỜI CHƠI</span><span className="client-live-dot"/></div><span className="home-muted">Số dư khả dụng</span><div className="home-balance">{money(customer.wallet_balance)}</div><p><Clock3 size={14}/>{rate > 0 ? `Tương đương khoảng ${Math.floor(customer.wallet_balance / rate)} giờ ${Math.floor(customer.wallet_balance / rate * 60) % 60} phút chơi` : 'Sẵn sàng cho phiên chơi tiếp theo'}</p><div className="wallet-hero-bottom"><span>NGƯỜI CHƠI<strong>{account.display_name}</strong></span><span>ĐƠN VỊ<strong>VND</strong></span></div><WalletCards className="wallet-watermark" size={180} strokeWidth={.6} aria-hidden="true"/></section>
    <TopUpPanel amount={amount} setAmount={setAmount} waiting={waiting} session={session} customer={customer} account={account} warning="" onCashRequest={requestCashTopUp}/></div>
    <div className="client-stat-grid three"><div className="client-stat"><span className="client-icon-tile"><ArrowDownLeft size={19}/></span><div><span>Tổng đã nạp</span><strong>{money(deposited)}</strong></div></div><div className="client-stat"><span className="client-icon-tile neutral"><ArrowUpRight size={19}/></span><div><span>Tổng đã sử dụng</span><strong>{money(spent)}</strong></div></div><div className="client-stat"><span className="client-icon-tile neutral"><Coins size={19}/></span><div><span>Đã được hoàn</span><strong>{money(refunded)}</strong></div></div></div>
    <div className="home-section-heading"><div><span className="home-eyebrow">MỌI KHOẢN CHI ĐỀU RÕ RÀNG</span><h2>Lịch sử ví</h2></div></div>
    <section className="home-panel"><div className="home-tabs" role="group" aria-label="Lịch sử ví"><button className={tab === 'transactions' ? 'selected' : ''} onClick={() => setTab('transactions')}>Giao dịch ví <span>{transactions.length}</span></button><button className={tab === 'requests' ? 'selected' : ''} onClick={() => setTab('requests')}>Yêu cầu nạp <span>{topUps.length}</span></button></div>
    {tab === 'transactions' ? <><div className="client-table-toolbar"><span className="client-muted">Các khoản tiền đã ghi nhận vào ví</span><select className="client-select" aria-label="Lọc giao dịch" value={filter} onChange={(e) => setFilter(e.target.value)}><option value="all">Tất cả giao dịch</option><option value="in">Tiền vào</option><option value="out">Tiền ra</option></select></div><div className="home-table-wrap"><table className="home-table client-data-table"><thead><tr><th>Giao dịch</th><th>Thời gian</th><th>Số tiền</th><th>Số dư sau</th><th><span className="home-sr-only">Chi tiết</span></th></tr></thead><tbody>{rows.map((row) => <tr key={row.id}><td><div className="home-activity-name"><span className={`home-activity-icon ${row.amount > 0 ? 'wallet' : 'invoice'}`}>{row.amount > 0 ? <ArrowDownLeft size={18}/> : <ArrowUpRight size={18}/>}</span><div><button onClick={() => setDetail(row)}>{transactionLabels[row.transaction_type]}</button><small>GD{String(row.id).padStart(5, '0')}{row.order_id ? ` · Đơn #GM${row.order_id}` : row.gaming_session_id ? ` · Phiên #${row.gaming_session_id}` : ''}</small></div></div></td><td className="home-date">{dateTime(row.recorded_at)}</td><td className={`home-amount ${row.amount > 0 ? 'positive' : ''}`}>{row.amount > 0 ? '+' : '−'}{money(Math.abs(row.amount))}</td><td>{money(row.balance_after)}</td><td><button className="home-icon-button" aria-label={`Xem giao dịch ${row.id}`} onClick={() => setDetail(row)}><ChevronRight size={16}/></button></td></tr>)}</tbody></table></div>{!rows.length && <Empty title="Chưa có giao dịch phù hợp" icon={WalletCards}>Thử một bộ lọc khác để xem lịch sử ví.</Empty>}</> : <div className="home-table-wrap"><table className="home-table client-data-table"><thead><tr><th>Yêu cầu</th><th>Thời gian gửi</th><th>Số tiền</th><th>Trạng thái</th><th>Thu tiền</th></tr></thead><tbody>{topUps.map((row) => <tr key={row.id}><td><strong>#NT{String(row.id).padStart(4, '0')}</strong><small className="client-table-sub">{row.requested_by_customer ? 'Yêu cầu tại máy' : 'Nạp tại quầy'}</small></td><td className="home-date">{dateTime(row.requested_at)}</td><td className="home-amount">{money(row.amount)}</td><td><Badge status={row.status}/></td><td>{row.collection_method === 'Cash' ? 'Tiền mặt' : row.collection_method === 'BankTransfer' ? 'Chuyển khoản' : '—'}</td></tr>)}</tbody></table></div>}</section>
    {detail && <HomeDialog title="Chi tiết giao dịch" onClose={() => setDetail(null)}><div className="home-info-box"><ReceiptText size={22}/><span>{transactionLabels[detail.transaction_type]}<br/>GD{String(detail.id).padStart(5, '0')}</span></div><div className="home-detail-row"><span>Thời gian</span><strong>{dateTime(detail.recorded_at)}</strong></div><div className="home-detail-row"><span>Nguồn giao dịch</span><strong>{detail.top_up_id ? `Nạp tiền #NT${detail.top_up_id}` : detail.order_id ? `Đơn #GM${detail.order_id}` : detail.order_item_id ? `Hoàn món #${detail.order_item_id}` : detail.gaming_session_id ? `Phiên #${detail.gaming_session_id}` : `Đăng ký giải #${detail.tournament_registration_id}`}</strong></div><div className="home-detail-row"><span>Số dư sau giao dịch</span><strong>{money(detail.balance_after)}</strong></div><div className="home-cart-total"><span>{detail.amount > 0 ? 'Tiền vào' : 'Tiền ra'}</span><strong>{detail.amount > 0 ? '+' : '−'}{money(Math.abs(detail.amount))}</strong></div></HomeDialog>}
  </div>;
}
