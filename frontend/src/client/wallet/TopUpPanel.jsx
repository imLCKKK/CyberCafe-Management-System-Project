import React, { useState } from 'react';
import { Banknote, Clock3, Plus, QrCode } from 'lucide-react';
import { money } from '../shared/demoModel';

const PRESETS = [50000, 100000, 200000, 500000];

export function TopUpPanel({ amount, setAmount, waiting, session, customer, account, warning, onCashRequest }) {
  const [paymentMethod, setPaymentMethod] = useState('cash');
  const canRequest = !waiting && Boolean(session) && customer.wallet_balance > 0
    && account.is_active && !account.must_change_password && !warning;

  return <section className="home-panel wallet-topup" aria-labelledby="topup-heading">
    <div className="client-panel-heading">
      <h2 id="topup-heading"><Plus size={18}/> Nạp tiền vào ví</h2>
      <span className="client-muted">Tiền mặt / ZaloPay</span>
    </div>
    <form className="client-panel-body" onSubmit={(event) => { event.preventDefault(); if (paymentMethod === 'cash') onCashRequest(); }}>
      <label className="home-field">1. Số tiền muốn nạp (VNĐ)
        <input type="number" min="10000" max="2000000" step="1000" required value={amount} onChange={(event) => setAmount(event.target.value)}/>
      </label>
      <div className="home-preset-grid">
        {PRESETS.map((value) => <button key={value} type="button" className={Number(amount) === value ? 'selected' : ''} aria-pressed={Number(amount) === value} onClick={() => setAmount(String(value))}>{money(value)}</button>)}
      </div>

      <fieldset className="wallet-methods">
        <legend>2. Phương thức thanh toán</legend>
        <label className={`wallet-method ${paymentMethod === 'cash' ? 'selected' : ''}`}>
          <input type="radio" name="topup-method" value="cash" checked={paymentMethod === 'cash'} onChange={() => setPaymentMethod('cash')}/>
          <Banknote size={19}/><span><strong>Tiền mặt</strong><small>Gửi yêu cầu, sau đó chờ nhân viên duyệt và thu tiền mặt</small></span>
        </label>
        <label className={`wallet-method ${paymentMethod === 'zalopay' ? 'selected' : ''}`}>
          <input type="radio" name="topup-method" value="zalopay" checked={paymentMethod === 'zalopay'} onChange={() => setPaymentMethod('zalopay')}/>
          <QrCode size={19}/><span><strong>Chuyển khoản qua ZaloPay</strong><small>QR riêng cho từng lần nạp · tích hợp sau</small></span>
        </label>
      </fieldset>

      {paymentMethod === 'cash' ? <div className="wallet-method-detail">
        <p className="home-fine-print">3. Gửi yêu cầu, sau đó chờ nhân viên duyệt và thu tiền mặt.</p>
        {waiting && <div className="home-info-box"><Clock3 size={17}/><span>Yêu cầu #{waiting.id}: <b>{money(waiting.amount)}</b><br/>{waiting.status === 'Pending' ? 'Đang chờ nhân viên xử lý.' : 'Bản demo hiện tại đã cộng ví và đang chờ thu tiền tại quầy.'}</span></div>}
        <button className="home-button primary" disabled={!canRequest} type="submit"><Plus size={16}/> Gửi yêu cầu nạp tiền mặt</button>
      </div> : <div className="wallet-qr-panel" aria-label="Vị trí hiển thị QR ZaloPay trong tương lai">
        <div className="wallet-qr-placeholder"><QrCode size={68} strokeWidth={1.1}/><span>QR ZALOPAY</span></div>
        <strong>{money(Number(amount) || 0)}</strong>
        <p>QR riêng sẽ hiển thị ở đây sau khi kết nối ZaloPay. Hiện chưa thể chuyển khoản hoặc tự động cộng ví.</p>
        <button className="home-button primary" type="button" disabled>Tạo QR · chưa tích hợp</button>
      </div>}
      {customer.wallet_balance <= 0 && <p className="home-fine-print">Ví đã hết tiền. Vui lòng nhờ nhân viên nạp trực tiếp tại quầy.</p>}
    </form>
  </section>;
}
