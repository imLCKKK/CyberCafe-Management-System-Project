import React, { useState } from 'react';
import { Plus, Sparkles } from 'lucide-react';
import HomeDialog from '../shared/Dialog';
import { Stepper } from '../shared/ClientUI';
import { money } from '../shared/demoModel';
import { remainingStock } from './recommendation';

export default function SuggestionResultDialog({ db, productId, cart, onAdd, onClose }) {
  const product = db.products.find(p => p.id === productId && p.is_active);
  const [quantity, setQuantity] = useState(1);
  const stock = product ? remainingStock(db, product) : 0;
  const limit = Math.max(0, Math.min(99 - (cart[productId] || 0), stock - (cart[productId] || 0)));
  const amount = Math.min(quantity, limit);
  return <HomeDialog title="Món ngon dành cho bạn" eyebrow="NEXORA KITCHEN / HÔM NAY ĂN GÌ?" className="today-result-dialog" onClose={onClose}>
    <div className="today-prize-hero"><span className="today-prize-badge"><Sparkles size={12}/> GỢI Ý HÔM NAY</span><h3>{product?.name || 'Món không còn trong thực đơn'}</h3><p>{db.product_categories.find(c => c.id === product?.category_id)?.name} · {money(product?.current_price)} / phần</p></div>
    <div className="today-prize-checkout"><div className="today-prize-quantity"><div><strong>Số lượng thêm</strong><small>Còn {stock} phần · Đã chọn {cart[productId] || 0}</small></div>{limit > 0 && <Stepper name={product?.name || 'món'} value={amount} max={limit} onChange={value => setQuantity(Math.max(1, value))}/>}</div>
      <div className="today-prize-total"><span>Tạm tính</span><strong>{money((product?.current_price || 0) * amount)}</strong></div>
      <button type="button" className="home-button primary" disabled={!product || amount <= 0} onClick={() => { onAdd(productId, amount); onClose(); }}><Plus size={16}/> Thêm vào giỏ</button>
    </div>
  </HomeDialog>;
}
