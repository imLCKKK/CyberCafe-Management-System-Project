import React, { useEffect, useState } from 'react';
import { CheckCircle2, ChevronRight, Clock3, Info, Minus, Plus, ShoppingBag, X } from 'lucide-react';
import HomeDialog from '../home/HomeDialog';
import { productArt } from './demoData';
import { dateTime, labels, money, requestItemCancel } from './demoModel';
import { useClientDemo } from './ClientDemoContext';

export function Badge({ status, children }) {
  return <span className={`home-badge ${['Pending', 'Mixed', 'CreditedUnpaid', 'Active'].includes(status) ? 'amber' : ['Cancelled', 'Rejected', 'Ended'].includes(status) ? 'muted' : ''}`}><i/>{children || labels[status] || status}</span>;
}
export function FoodArt({ product, small = false }) {
  return <div className={`home-food-art food-art-${product.category_id} ${small ? 'food-art-small' : ''}`} aria-hidden="true"><span>{productArt[product.id] || '🍽️'}</span><i/><b>TRAM KITCHEN</b></div>;
}
export function PageHeading({ eyebrow, title, description, children }) {
  return <div className="home-heading"><div><div className="home-eyebrow">{eyebrow}</div><h1>{title}</h1><p>{description}</p></div>{children}</div>;
}
export function Empty({ title = 'Chưa có dữ liệu', children, icon: Icon = ShoppingBag }) {
  return <div className="home-empty client-empty-state"><Icon size={30} strokeWidth={1.4}/><h3>{title}</h3><p>{children}</p></div>;
}
export function Stepper({ value, onChange, name, max = 99 }) {
  return <div className="home-stepper"><button type="button" aria-label={`Giảm ${name}`} onClick={() => onChange(value - 1)}><Minus size={13}/></button><span aria-live="polite">{value}</span><button type="button" disabled={value >= max} aria-label={`Tăng ${name}`} onClick={() => onChange(value + 1)}><Plus size={13}/></button></div>;
}
export function Feedback({ notice, error, onClear }) {
  useEffect(() => { if (!notice) return; const timer = setTimeout(onClear, 5000); return () => clearTimeout(timer); }, [notice, onClear]);
  return <>{error && <div className="home-alert" role="alert">{error}</div>}{notice && <div className="home-toast" role="status"><CheckCircle2 size={19}/>{notice}<button type="button" className="home-icon-button" onClick={onClear} aria-label="Đóng thông báo"><X size={16}/></button></div>}</>;
}
export function DemoStrip() {
  const { reset, warning } = useClientDemo();
  const [open, setOpen] = useState(false);
  const [error, setError] = useState('');
  return <><div className="home-demo-strip"><span><Info size={14}/> Bản trải nghiệm · Dữ liệu mẫu lưu trên trình duyệt</span><button onClick={() => setOpen(true)}>Khôi phục bản mẫu</button></div>{warning && <div className="home-alert" role="alert">{warning}</div>}{open && <HomeDialog title="Khôi phục dữ liệu mẫu" onClose={() => setOpen(false)}>{error && <div className="home-alert" role="alert">{error}</div>}<p className="home-dialog-intro">Đưa Trang chủ, Gọi món, Ví, Hóa đơn và Tài khoản về cùng dữ liệu ban đầu. Các thao tác demo bạn vừa thử sẽ được xóa.</p><div className="home-dialog-actions"><button className="home-button" onClick={() => setOpen(false)}>Giữ dữ liệu</button><button className="home-button primary" onClick={() => { try { reset(); setOpen(false); } catch (err) { setError(err.message); } }}>Khôi phục</button></div></HomeDialog>}</>;
}
export function OrderDetail({ order, onClose }) {
  const { db, commit } = useClientDemo();
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  if (!order) return null;
  const items = db.order_items.filter((r) => r.order_id === order.id);
  const cash = order.payment_method === 'Cash';
  const cashPaid = cash && order.payment_status === 'Paid';
  const cashDue = items.filter((item) => item.status !== 'Cancelled').reduce((sum, item) => sum + item.quantity * item.unit_price_snapshot, 0);
  const refund = db.wallet_transactions.filter((t) => t.transaction_type === 'OrderItemRefund' && items.some((r) => r.id === t.order_item_id)).reduce((sum, r) => sum + r.amount, 0);
  return <HomeDialog title={`Chi tiết đơn #GM${order.id}`} onClose={onClose}>
    <p className="home-dialog-intro"><Clock3 size={14}/> {dateTime(order.placed_at)} · Phiên #{order.gaming_session_id}</p>
    {error && <div className="home-alert" role="alert">{error}</div>}{notice && <div className="home-info-box" role="status"><Info size={17}/>{notice}</div>}
    {items.map((item) => {
      const product = db.products.find((r) => r.id === item.product_id);
      const request = db.order_item_cancel_requests.filter((r) => r.order_item_id === item.id).at(-1);
      return <div className="client-order-detail-item" key={item.id}><div className="home-detail-row"><span>{item.quantity} × {product?.name}<small>{money(item.unit_price_snapshot)} / phần</small></span><strong>{money(item.quantity * item.unit_price_snapshot)}</strong></div><div className="client-item-status"><Badge status={item.status}/>{request?.status === 'Pending' ? <span className="client-muted">Đang chờ duyệt hủy</span> : item.status === 'Pending' ? <button className="home-text-button" onClick={() => { try { commit((data) => requestItemCancel(data, item.id)); setError(''); setNotice(cash ? 'Đã gửi yêu cầu hủy. Nhân viên sẽ kiểm tra đơn tiền mặt.' : 'Đã gửi yêu cầu. Ví chỉ được hoàn sau khi nhân viên hủy món.'); } catch (err) { setError(err.message); } }}>Yêu cầu hủy <ChevronRight size={14}/></button> : request?.status === 'Rejected' ? <span className="client-muted">Yêu cầu hủy đã bị từ chối</span> : item.status === 'Cancelled' && <span className="client-muted">{cash ? 'Không thu tiền món đã hủy' : 'Đã hoàn vào ví'}</span>}</div></div>;
    })}
    <div className="home-detail-row"><span>{cash ? (cashPaid ? 'Tiền mặt · Đã thu' : 'Thanh toán tiền mặt · chờ nhân viên thu') : 'Đã trừ ví khi đặt'}</span><strong>{money(cash ? cashDue : order.total_amount)}</strong></div>{refund > 0 && <div className="home-detail-row"><span>Đã hoàn tiền món hủy</span><strong className="client-positive">+{money(refund)}</strong></div>}<div className="home-cart-total"><span>{cash ? (cashPaid ? 'Tiền mặt đã thanh toán' : 'Tiền mặt cần thanh toán') : 'Chi phí thực tế'}</span><strong>{money(cash ? cashDue : order.total_amount - refund)}</strong></div><p className="home-fine-print">{cash ? (cashPaid ? 'Đã thanh toán tiền mặt với nhân viên, không trừ ví.' : 'Đơn này chưa trừ ví. Nhân viên xác nhận món và thu tiền mặt khi phục vụ.') : 'Nhân viên xác nhận từng món. Món đã xác nhận không thể hủy; yêu cầu hủy không tự thay đổi số dư ví.'}</p>
  </HomeDialog>;
}
