import React, { useState } from 'react';
import { ArrowDownLeft, ArrowUpRight, ChevronRight, Clock3, Coins, Plus, ReceiptText, WalletCards } from 'lucide-react';
import HomeDialog from '../shared/Dialog';
import { Empty, PageHeading } from '../shared/ClientUI';
import { dateTime, money } from '../shared/demoModel';
import { walletTypeLabel as label } from '../../services/walletTypes';
import { useWalletData } from './useWalletData';

export function WalletPage() {
  const { status, profile, wallet, error, reload } = useWalletData();
  const [filter, setFilter] = useState('all');
  const [detail, setDetail] = useState(null);

  const heading = (chip) => <PageHeading eyebrow="TRAM WALLET" title={<>Ví của <span>bạn.</span></>} description="Nạp thêm giờ vui. Mọi khoản chi đều rõ ràng trong một nơi.">{chip}</PageHeading>;
  if (status === 'loading') {
    return <div className="cafe-home client-module wallet-page">{heading()}<p className="home-muted" role="status">Đang tải ví...</p></div>;
  }
  if (status === 'error') {
    return <div className="cafe-home client-module wallet-page">{heading()}
      <div className="home-alert" role="alert">{error}</div>
      <button className="home-button primary" onClick={reload}>Thử lại</button>
    </div>;
  }

  // Dữ liệu từ API (SQL Server). Số dư luôn lấy từ server, không cộng lại từ danh sách (danh sách bị giới hạn).
  const transactions = wallet.transactions;
  const rows = transactions.filter((r) => filter === 'all' || (filter === 'in' ? r.amount > 0 : r.amount < 0));
  const deposited = transactions.filter((r) => r.transaction_type === 'TopUp').reduce((s, r) => s + r.amount, 0);
  const spent = transactions.filter((r) => r.amount < 0).reduce((s, r) => s - r.amount, 0);
  const refunded = transactions.filter((r) => ['OrderRefund', 'TournamentRefund'].includes(r.transaction_type)).reduce((s, r) => s + r.amount, 0);

  return <div className="cafe-home client-module wallet-page">
    {heading(<span className="client-heading-chip"><WalletCards size={16}/> Ví #{String(profile.customerId).padStart(6, '0')}</span>)}

    <div className="wallet-top-grid">
      <section className="home-panel wallet-hero">
        <div className="wallet-hero-top"><span><WalletCards size={20}/> TRAM / VÍ NGƯỜI CHƠI</span><span className="client-live-dot"/></div>
        <span className="home-muted">Số dư khả dụng</span>
        <div className="home-balance">{money(wallet.walletBalance)}</div>
        <p><Clock3 size={14}/>{profile.hasActiveSession ? 'Bạn đang có phiên chơi' : 'Sẵn sàng cho phiên chơi tiếp theo'}</p>
        <div className="wallet-hero-bottom"><span>NGƯỜI CHƠI<strong>{profile.fullName}</strong></span><span>ĐƠN VỊ<strong>VND</strong></span></div>
        <WalletCards className="wallet-watermark" size={180} strokeWidth={.6} aria-hidden="true"/>
      </section>

      <section className="home-panel wallet-topup" aria-labelledby="topup-heading">
        <div className="client-panel-heading"><h2 id="topup-heading"><Plus size={18}/> Nạp tiền vào ví</h2><span className="client-muted">Tại quầy</span></div>
        <div className="client-panel-body">
          <p>Nhờ nhân viên nạp trực tiếp tại quầy. Ví được cộng ngay khi nhân viên xác nhận đã thu tiền.</p>
          <p className="home-fine-print">Nạp qua ZaloPay sẽ có sau khi tích hợp cổng thanh toán.</p>
        </div>
      </section>
    </div>

    <div className="client-stat-grid three">
      <div className="client-stat"><span className="client-icon-tile"><ArrowDownLeft size={19}/></span><div><span>Tổng đã nạp</span><strong>{money(deposited)}</strong></div></div>
      <div className="client-stat"><span className="client-icon-tile neutral"><ArrowUpRight size={19}/></span><div><span>Tổng đã sử dụng</span><strong>{money(spent)}</strong></div></div>
      <div className="client-stat"><span className="client-icon-tile neutral"><Coins size={19}/></span><div><span>Đã được hoàn</span><strong>{money(refunded)}</strong></div></div>
    </div>

    <div className="home-section-heading"><div><span className="home-eyebrow">MỌI KHOẢN CHI ĐỀU RÕ RÀNG</span><h2>Lịch sử ví</h2></div></div>
    <section className="home-panel">
      <div className="client-table-toolbar">
        <span className="client-muted">Các khoản tiền đã ghi nhận vào ví ({transactions.length} giao dịch gần nhất)</span>
        <select className="client-select" aria-label="Lọc giao dịch" value={filter} onChange={(e) => setFilter(e.target.value)}>
          <option value="all">Tất cả giao dịch</option><option value="in">Tiền vào</option><option value="out">Tiền ra</option>
        </select>
      </div>
      <div className="home-table-wrap">
        <table className="home-table client-data-table">
          <thead><tr><th>Giao dịch</th><th>Thời gian</th><th>Số tiền</th><th>Số dư sau</th><th><span className="home-sr-only">Chi tiết</span></th></tr></thead>
          <tbody>{rows.map((row) => <tr key={row.id}>
            <td><div className="home-activity-name">
              <span className={`home-activity-icon ${row.amount > 0 ? 'wallet' : 'invoice'}`}>{row.amount > 0 ? <ArrowDownLeft size={18}/> : <ArrowUpRight size={18}/>}</span>
              <div><button onClick={() => setDetail(row)}>{label(row.transaction_type)}</button><small>GD{String(row.id).padStart(5, '0')}</small></div>
            </div></td>
            <td className="home-date">{dateTime(row.recorded_at)}</td>
            <td className={`home-amount ${row.amount > 0 ? 'positive' : ''}`}>{row.amount > 0 ? '+' : '−'}{money(Math.abs(row.amount))}</td>
            <td>{money(row.balance_after)}</td>
            <td><button className="home-icon-button" aria-label={`Xem giao dịch ${row.id}`} onClick={() => setDetail(row)}><ChevronRight size={16}/></button></td>
          </tr>)}</tbody>
        </table>
      </div>
      {!rows.length && <Empty title={transactions.length ? 'Chưa có giao dịch phù hợp' : 'Chưa có giao dịch nào'} icon={WalletCards}>
        {transactions.length ? 'Thử một bộ lọc khác để xem lịch sử ví.' : 'Các khoản nạp và chi tiêu của bạn sẽ hiện ở đây.'}
      </Empty>}
    </section>

    {detail && <HomeDialog title="Chi tiết giao dịch" onClose={() => setDetail(null)}>
      <div className="home-info-box"><ReceiptText size={22}/><span>{label(detail.transaction_type)}<br/>GD{String(detail.id).padStart(5, '0')}</span></div>
      <div className="home-detail-row"><span>Thời gian</span><strong>{dateTime(detail.recorded_at)}</strong></div>
      <div className="home-detail-row"><span>Ghi chú</span><strong>{detail.description || '—'}</strong></div>
      <div className="home-detail-row"><span>Số dư sau giao dịch</span><strong>{money(detail.balance_after)}</strong></div>
      <div className="home-cart-total"><span>{detail.amount > 0 ? 'Tiền vào' : 'Tiền ra'}</span><strong>{detail.amount > 0 ? '+' : '−'}{money(Math.abs(detail.amount))}</strong></div>
    </HomeDialog>}
  </div>;
}
