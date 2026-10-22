import React, { useId, useRef, useState } from 'react';
import { ArrowRight, Banknote, Check, CheckCircle2, Clock3, MapPin, Plus, QrCode, Sparkles } from 'lucide-react';
import HomeDialog from '../home/HomeDialog';
import { Stepper } from '../shared/ClientUI';
import { useClientDemo } from '../shared/ClientDemoContext';
import { productArt } from '../shared/demoData';
import { money, placeCashOrder } from '../shared/demoModel';
import { remainingStock } from './recommendation';

export default function SuggestionResultDialog({ productId, cart, onAdd, onClose, onViewOrders }) {
  const { db, account, session, computer, warning, commit } = useClientDemo();
  const product = db.products.find((item) => item.id === productId);
  const [method, setMethod] = useState('cash');
  const [quantity, setQuantity] = useState(1);
  const [error, setError] = useState('');
  const [placed, setPlaced] = useState(null);
  const submitting = useRef(false);
  const methodName = useId();
  const stock = product ? remainingStock(db, product) : 0;
  const total = (product?.current_price || 0) * quantity;
  const canCheckout = Boolean(account.is_active && !account.must_change_password && session && !warning && product?.is_active !== false && stock >= quantity);

  function requestCashOrder(event) {
    event.preventDefault();
    if (method !== 'cash' || !canCheckout || submitting.current) return;
    submitting.current = true;
    try {
      const result = commit((data) => placeCashOrder(data, { [productId]: quantity }));
      setPlaced({ ...result.orders.at(-1), quantity });
      setError('');
    } catch (err) {
      setError(err.message);
      submitting.current = false;
    }
  }

  return <HomeDialog title={placed ? 'Đã nhận yêu cầu đặt món' : 'Món ngon dành cho bạn'} eyebrow="TRAM KITCHEN / HÔM NAY ĂN GÌ?" className="today-result-dialog" onClose={onClose}>
    {placed ? <div className="today-checkout-success" role="status">
      <span className="today-success-icon"><CheckCircle2 size={42} strokeWidth={1.4}/></span>
      <span className="today-eyebrow">ĐƠN #GM{placed.id}</span>
      <h3>Món ngon sắp đến!</h3>
      <p>Yêu cầu đã được lưu. Nhân viên sẽ xác nhận món và thu tiền mặt tại {computer?.code || 'máy của bạn'}.</p>
      <div className="today-success-receipt"><span>{placed.quantity} × {product?.name}</span><strong>{money(placed.total_amount)}</strong><span><Clock3 size={13}/> Chờ thu tiền mặt</span></div>
      <p className="home-fine-print">Số dư ví của bạn không thay đổi.</p>
      <button type="button" className="home-button primary" onClick={onViewOrders}>Xem đơn của tôi <ArrowRight size={16}/></button>
      <button type="button" className="home-text-button today-success-close" onClick={onClose}>Tiếp tục khám phá</button>
    </div> : <>
      <div className="today-prize-hero">
        <span className="today-prize-badge"><Sparkles size={12}/> VỪA TÌM THẤY MÓN NGON</span>
        <h3>{product?.name || 'Món không còn trong thực đơn'}</h3>
        <p>{db.product_categories.find((item) => item.id === product?.category_id)?.name} <span>·</span> {money(product?.current_price)} / phần</p>
        <div className="today-prize-art" aria-hidden="true"><i/><span>{productArt[productId] || '🍽️'}</span><Sparkles className="today-prize-sparkle one" size={21}/><Sparkles className="today-prize-sparkle two" size={14}/></div>
        <span className="today-prize-delivery"><MapPin size={12}/> Phục vụ tận {computer?.code || 'máy của bạn'}</span>
      </div>
      <form className="today-prize-checkout" onSubmit={requestCashOrder}>
        <div className="today-prize-quantity"><div><strong>Số lượng</strong><small>{stock > 0 ? `Còn ${stock} phần sẵn sàng phục vụ` : 'Món này đã hết hàng'}</small></div><Stepper name={product?.name || 'món'} value={quantity} max={Math.min(stock, 99)} onChange={(value) => { setQuantity(Math.max(1, Math.min(99, value))); setError(''); }}/></div>
        <fieldset className="wallet-methods today-result-methods">
          <legend>Chọn phương thức thanh toán</legend>
          <label className={`wallet-method ${method === 'transfer' ? 'selected' : ''}`}><input type="radio" name={methodName} value="transfer" checked={method === 'transfer'} onChange={() => { setMethod('transfer'); setError(''); }}/><QrCode size={22}/><span><strong>Chuyển khoản</strong><small>Quét QR qua ZaloPay</small></span><span className="today-method-check">{method === 'transfer' && <Check size={11}/>}</span></label>
          <label className={`wallet-method ${method === 'cash' ? 'selected' : ''}`}><input type="radio" name={methodName} value="cash" checked={method === 'cash'} onChange={() => { setMethod('cash'); setError(''); }}/><Banknote size={22}/><span><strong>Tiền mặt</strong><small>Thanh toán với nhân viên</small></span><span className="today-method-check">{method === 'cash' && <Check size={11}/>}</span></label>
        </fieldset>
        {method === 'cash' ? <div className="today-cash-detail"><Clock3 size={16}/><p>Nhân viên sẽ xác nhận đơn và thu <strong>{money(total)}</strong> tiền mặt khi phục vụ món tại máy.</p></div> : <div className="wallet-qr-panel today-transfer-detail"><span className="today-transfer-icon"><QrCode size={44} strokeWidth={1.25}/></span><div><strong>{money(total)}</strong><p>QR thanh toán sẽ hiển thị khi ZaloPay được kết nối. Hiện chưa thể chuyển khoản.</p><span>Chưa kết nối ZaloPay</span></div></div>}
        {(error || warning) && <div className="home-alert" role="alert">{error || warning}</div>}
        {!stock && <p className="home-fine-print">Hãy đóng cửa sổ và thử món khác nhé.</p>}
        {!session && <p className="home-fine-print">Bạn cần phiên chơi đang hoạt động để đặt món.</p>}
        <div className="today-prize-total"><span>Tổng thanh toán<small>{quantity} phần · {method === 'cash' ? 'Tiền mặt' : 'Chuyển khoản'}</small></span><strong>{money(total)}</strong></div>
        <button className="home-button primary today-checkout-submit" type="submit" disabled={method !== 'cash' || !canCheckout}>{method === 'cash' ? <><Banknote size={18}/> Đặt món · Thanh toán tiền mặt <ArrowRight size={16}/></> : <><QrCode size={18}/> Tạo QR · chưa tích hợp</>}</button>
        <button type="button" className="home-text-button today-add-instead" disabled={!canCheckout || quantity + (cart[productId] || 0) > stock || quantity + (cart[productId] || 0) > 99} onClick={() => { onAdd(productId, quantity); onClose(); }}><Plus size={14}/> Thêm vào giỏ để chọn thêm món</button>
      </form>
    </>}
  </HomeDialog>;
}
