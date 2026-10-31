import React from 'react';
import { Banknote, Clock3, History, Monitor, ReceiptText, Utensils, WalletCards } from 'lucide-react';
import { Badge } from '../shared/ClientUI';
import { dateTime, minutesLabel, money, transactionLabels } from '../shared/demoModel';
import { orderPaymentText, paymentLabels } from './invoiceModel';

export function PaymentBadge({ receipt }) {
  return <span className={`invoice-payment-badge ${receipt.cashDue > 0 ? 'due' : ''}`}><i/>{paymentLabels[receipt.paymentState]}</span>;
}

export default function ReceiptDetail({ receipt, db, account, now, print = false }) {
  const session = receipt.session;
  return <div className="invoice-receipt-content">
    <div className="receipt-brand invoice-receipt-brand"><span className="home-eyebrow">TRAM / KHÔNG GIAN CHƠI</span><h3>Bảng kê #PC{receipt.id}</h3><p>{account.display_name} · {account.phone}</p><div className="invoice-receipt-badges"><Badge status={session.status}/><PaymentBadge receipt={receipt}/></div></div>
    <div className="invoice-session-meta">
      <div><span>Máy sử dụng</span><strong>{receipt.computer?.code || '—'}</strong></div>
      <div><span>Thời gian chơi</span><strong>{minutesLabel(session.started_at, session.ended_at || now)}</strong></div>
      <div><span>Bắt đầu</span><strong>{dateTime(session.started_at)}</strong></div>
      <div><span>Kết thúc</span><strong>{session.ended_at ? dateTime(session.ended_at) : 'Phiên đang hoạt động'}</strong></div>
    </div>
    <div className="receipt-section-title"><Monitor size={16}/> TIỀN MÁY</div>
    <div className="home-detail-row"><span>Đơn giá khi bắt đầu phiên</span><strong>{money(session.hourly_rate_snapshot)} / giờ</strong></div>
    <div className="home-detail-row"><span>Tiền máy đã ghi nhận<small>Tính tới {dateTime(session.last_charged_at)} theo dữ liệu phiên</small></span><strong>{money(session.charged_amount)}</strong></div>
    <div className="receipt-section-title"><Utensils size={16}/> CHI TIẾT ĐƠN MÓN <span>({receipt.orders.length} đơn)</span></div>
    {receipt.orderDetails.map((order) => <section className="invoice-order" key={order.id} aria-label={`Đơn GM${order.id}`}>
      <div className="invoice-order-heading"><div><strong>#GM{order.id}</strong><small>{dateTime(order.placed_at)}</small></div><span className={`invoice-order-method ${order.cashDue > 0 ? 'due' : ''}`}>{order.cash ? <Banknote size={13}/> : <WalletCards size={13}/>} {orderPaymentText(order)}</span></div>
      {order.items.map((item) => {
        const amount = item.quantity * item.unit_price_snapshot;
        const refund = receipt.transactions.filter((row) => row.transaction_type === 'OrderItemRefund' && row.order_item_id === item.id).reduce((sum, row) => sum + row.amount, 0);
        const cancelled = item.status === 'Cancelled';
        return <div className={`invoice-order-line ${cancelled ? 'cancelled' : ''}`} key={item.id}>
          <div><strong>{item.quantity} × {db.products.find((product) => product.id === item.product_id)?.name || `Món #${item.product_id}`}</strong><small>{money(item.unit_price_snapshot)} / phần{cancelled ? ` · ${order.cash ? 'Không thu tiền' : refund >= amount ? 'Đã hoàn vào ví' : 'Chờ hoàn tiền'}` : ''}</small></div>
          <div><span className="invoice-line-amount">{money(amount)}</span><Badge status={item.status}/></div>
        </div>;
      })}
      <div className="invoice-order-totals">
        {order.refunds > 0 && <span>Đã hoàn vào ví <b>−{money(order.refunds)}</b></span>}
        {order.cash && order.cancelled > 0 && <span>Món đã hủy, không thu <b>−{money(order.cancelled)}</b></span>}
        {order.refundPending > 0 && <span className="invoice-warning-text">Tiền món hủy chờ hoàn <b>{money(order.refundPending)}</b></span>}
        <span>Còn ghi nhận <strong>{money(order.net)}</strong></span>
      </div>
    </section>)}
    {!receipt.orders.length && <p className="client-muted invoice-empty-orders">Không có đơn món trong phiên chơi này.</p>}
    <div className="receipt-section-title"><ReceiptText size={16}/> TỔNG HỢP CHI PHÍ</div>
    <div className="home-detail-row"><span>Tiền máy</span><strong>{money(session.charged_amount)}</strong></div>
    <div className="home-detail-row"><span>Tiền món ban đầu</span><strong>{money(receipt.foodGross)}</strong></div>
    {receipt.refunds > 0 && <div className="home-detail-row"><span>Tiền món đã hoàn vào ví</span><strong className="client-positive">−{money(receipt.refunds)}</strong></div>}
    {receipt.cashCancelled > 0 && <div className="home-detail-row"><span>Món tiền mặt đã hủy, không thu</span><strong className="client-positive">−{money(receipt.cashCancelled)}</strong></div>}
    <div className="home-cart-total invoice-grand-total"><span>Tổng ghi nhận</span><strong>{money(receipt.total)}</strong></div>
    <div className="invoice-payment-breakdown">
      <div><span><WalletCards size={14}/> Đã trả bằng ví<small>Sau khi trừ các khoản hoàn tiền</small></span><strong>{money(receipt.walletPaid)}</strong></div>
      <div><span><Banknote size={14}/> Đã thu tiền mặt</span><strong>{money(receipt.cashPaid)}</strong></div>
      <div><span>Tổng đã thanh toán</span><strong>{money(receipt.paidTotal)}</strong></div>
      <div className={receipt.cashDue > 0 ? 'invoice-cash-due' : ''}><span>Tiền mặt chờ thu</span><strong>{money(receipt.cashDue)}</strong></div>
    </div>
    {receipt.cashDue > 0 && <p className="invoice-payment-note">Nhân viên sẽ thu khoản tiền mặt chờ thanh toán. Khoản này chưa trừ ví; không thanh toán lại phần đã trả bằng ví.</p>}
    {receipt.refundPending > 0 && <p className="invoice-payment-note invoice-warning-text">Có {money(receipt.refundPending)} tiền món đã hủy đang chờ hoàn. Số tiền này chưa được trừ khỏi tổng ghi nhận.</p>}
    <details className="invoice-wallet-history" open={print || undefined}>
      <summary><History size={15}/> Giao dịch ví trong phiên <span>{receipt.transactions.length}</span></summary>
      {receipt.transactions.map((row) => <div className="invoice-wallet-row" key={row.id}><div><strong>{transactionLabels[row.transaction_type] || row.transaction_type}</strong><small>#GD{row.id} · {dateTime(row.recorded_at)}</small><small>Số dư sau giao dịch: {money(row.balance_after)}</small></div><span className={row.amount > 0 ? 'client-positive' : ''}>{row.amount > 0 ? '+' : '−'}{money(Math.abs(row.amount))}</span></div>)}
      {!receipt.transactions.length && <p className="client-muted">Chưa có giao dịch ví trong phiên này.</p>}
    </details>
    <p className="invoice-receipt-footnote"><Clock3 size={13}/> Cập nhật tới {dateTime(receipt.updatedAt)}. {session.status === 'Active' ? 'Bảng kê tạm tính; phiên đang chơi sẽ tiếp tục cập nhật.' : 'Phiên chơi đã kết thúc.'}</p>
    <p className="invoice-receipt-footnote">Bảng kê sử dụng dịch vụ từ dữ liệu demo, không phải hóa đơn thuế.</p>
  </div>;
}
