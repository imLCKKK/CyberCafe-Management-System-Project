import React, { useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ArrowRight, Check, ChevronRight, Dices, History, Plus, Search, ShoppingBag, Trash2, Utensils, WalletCards } from 'lucide-react';
import TodaySuggestion, { TodaySuggestionTeaser } from './TodaySuggestion';
import { remainingStock } from './recommendation';
import HomeDialog from '../home/HomeDialog';
import { useClientDemo } from '../shared/ClientDemoContext';
import { Badge, DemoStrip, Empty, Feedback, FoodArt, OrderDetail, PageHeading, Stepper } from '../shared/ClientUI';
import { dateTime, money, nextId, orderStatus, placeClientOrder } from '../shared/demoModel';

export function OrderingPage() {
  const { db, customer, session, account, computer, commit, warning } = useClientDemo();
  const location = useLocation();
  const navigate = useNavigate();
  const [tab, setTab] = useState('menu');
  const [category, setCategory] = useState('all');
  const [suggestionCategory, setSuggestionCategory] = useState('all');
  const [query, setQuery] = useState('');
  const [cart, setCart] = useState({});
  const [confirm, setConfirm] = useState(false);
  const [selected, setSelected] = useState(null);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const handoff = useRef(null);
  useEffect(() => {
    if (!location.state || handoff.current === location.key) return;
    handoff.current = location.key;
    const initial = {};
    for (const item of location.state.initialCartItems || []) {
      const product = db.products.find((row) => row.id === item.ProductId);
      if (product && Number.isInteger(item.Quantity) && item.Quantity > 0) {
        const amount = Math.min(item.Quantity, 99, remainingStock(db, product));
        if (amount > 0) initial[item.ProductId] = amount;
      }
    }
    setCart(initial);
    navigate(location.pathname, { replace: true, state: null });
  }, [location.key, location.state, location.pathname, db.products, navigate]);
  const items = Object.entries(cart).map(([id, quantity]) => ({ product: db.products.find((r) => r.id === Number(id)), quantity })).filter((r) => r.product);
  const total = items.reduce((sum, r) => sum + r.product.current_price * r.quantity, 0);
  const count = items.reduce((sum, r) => sum + r.quantity, 0);
  const canOrder = account.is_active && !account.must_change_password && session && customer.wallet_balance > 0 && !warning;
  const products = db.products.filter((r) => r.is_active !== false && (category === 'all' || r.category_id === category) && r.name.toLocaleLowerCase('vi').includes(query.trim().toLocaleLowerCase('vi')));
  const orders = db.orders.filter((r) => r.customer_id === customer.id).sort((a, b) => new Date(b.placed_at) - new Date(a.placed_at));
  const selectedOrder = orders.find((order) => order.id === selected);
  function quantity(id, value) { setCart((current) => { const result = { ...current }; const product = db.products.find((row) => row.id === id); const amount = product ? Math.min(99, remainingStock(db, product), value) : 0; if (amount <= 0) delete result[id]; else result[id] = amount; return result; }); }
  function addSuggestion(id, amount = 1) { quantity(id, (cart[id] || 0) + amount); setNotice(`Đã thêm ${db.products.find((product) => product.id === id)?.name} vào giỏ món.`); }
  function submit() {
    try {
      const id = nextId(db.orders);
      commit((data) => placeClientOrder(data, cart));
      setCart({}); setConfirm(false); setError(''); setNotice(`Đã đặt đơn #GM${id}. ${money(total)} đã được trừ từ ví.`); setTab('orders');
    } catch (err) { setError(err.message); }
  }
  return <div className="cafe-home client-module ordering-page"><DemoStrip/>
    <PageHeading eyebrow="TRAM KITCHEN" title={<>Nạp năng lượng, <span>tiếp trận.</span></>} description="Món ngon đến tận máy. Chọn món yêu thích, phần còn lại để TRAM lo."><div className="client-heading-chip"><span className="client-live-dot"/>{computer?.code || 'Chưa có phiên'}<span className="client-muted">· Phục vụ tại máy</span></div></PageHeading>
    <Feedback notice={notice} error={!confirm ? error : ''} onClear={() => setNotice('')}/>
    <div className="client-page-tabs" role="group" aria-label="Nội dung gọi món"><button className={tab === 'menu' ? 'selected' : ''} aria-pressed={tab === 'menu'} onClick={() => setTab('menu')}><Utensils size={16}/> Thực đơn</button><button className={`today-tab ${tab === 'suggestion' ? 'selected' : ''}`} aria-pressed={tab === 'suggestion'} onClick={() => setTab('suggestion')}><Dices size={16}/> Hôm nay ăn gì?</button><button className={tab === 'orders' ? 'selected' : ''} aria-pressed={tab === 'orders'} onClick={() => setTab('orders')}><History size={16}/> Đơn của tôi <span>{orders.length}</span></button></div>
    {tab !== 'orders' ? <div className="order-layout"><section className="order-menu-main">
      {tab === 'menu' ? <>
      <TodaySuggestionTeaser db={db} onOpen={() => setTab('suggestion')}/>
      <div className="client-menu-toolbar"><div className="client-category-tabs" role="group" aria-label="Danh mục món">{[{ id: 'all', name: 'Tất cả' }, ...db.product_categories].map((r) => <button aria-pressed={category === r.id} className={category === r.id ? 'selected' : ''} key={r.id} onClick={() => setCategory(r.id)}>{r.name}<span>{r.id === 'all' ? db.products.length : db.products.filter((p) => p.category_id === r.id).length}</span></button>)}</div><label className="home-search client-menu-search"><Search size={16}/><span className="home-sr-only">Tìm món</span><input placeholder="Tìm món bạn thích…" value={query} onChange={(e) => setQuery(e.target.value)}/></label></div>
      <div className="ordering-food-grid">{products.map((product) => <article className="home-food-card" key={product.id}><FoodArt product={product}/><div className="home-food-body"><span className="home-food-category">{db.product_categories.find((r) => r.id === product.category_id)?.name}</span><h3>{product.name}</h3><span className="order-stock-label">{remainingStock(db, product) ? `Còn ${remainingStock(db, product)} phần` : 'Hết hàng'}</span><div className="home-food-bottom"><strong>{money(product.current_price)}</strong>{cart[product.id] ? <Stepper name={product.name} value={cart[product.id]} max={Math.min(99, remainingStock(db, product))} onChange={(v) => quantity(product.id, v)}/> : <button className="home-add" disabled={!canOrder || !remainingStock(db, product)} aria-label={`Thêm ${product.name} vào giỏ`} onClick={() => quantity(product.id, 1)}><Plus size={18}/></button>}</div></div></article>)}</div>
      {!products.length && <Empty title="Chưa tìm thấy món">Thử tên khác hoặc chọn một danh mục khác.</Empty>}<p className="client-caption">Nhân viên kiểm tra và xác nhận từng món sau khi nhận đơn.</p>
      </> : <TodaySuggestion db={db} category={suggestionCategory} onCategoryChange={setSuggestionCategory} cart={cart} onAdd={addSuggestion} onBack={() => setTab('menu')} onViewOrders={() => setTab('orders')}/>}
    </section><aside className="order-cart home-panel" aria-label="Giỏ món"><div className="client-panel-heading"><h2><ShoppingBag size={18}/> Giỏ món của bạn <span className="client-count">{count}</span></h2>{count > 0 && <button className="home-icon-button" onClick={() => setCart({})} aria-label="Xóa giỏ hàng"><Trash2 size={16}/></button>}</div><div className="order-cart-body">
      <div className="order-delivery"><span className="client-icon-tile"><Utensils size={17}/></span><div>Phục vụ tại {computer?.code || 'máy của bạn'}<small>Phiên chơi #{session?.id || '—'}</small></div><Check size={15}/></div>
      {!items.length ? <Empty title="Giỏ món đang trống">Thêm một món ngon để tiếp sức cho trận đấu nhé.</Empty> : <div className="order-cart-items">{items.map(({ product, quantity: qty }) => <div className="order-cart-line" key={product.id}><FoodArt product={product} small/><div><strong>{product.name}</strong><small>{money(product.current_price)}</small><Stepper name={product.name} value={qty} max={Math.min(99, remainingStock(db, product))} onChange={(v) => quantity(product.id, v)}/></div><button className="home-icon-button" aria-label={`Bỏ ${product.name}`} onClick={() => quantity(product.id, 0)}><Trash2 size={14}/></button></div>)}</div>}
      <div className="home-detail-row"><span>Tạm tính ({count} phần)</span><strong>{money(total)}</strong></div><div className="order-cart-total"><span>Tổng thanh toán</span><strong>{money(total)}</strong></div><div className="order-wallet"><WalletCards size={16}/><span>Ví TRAM</span><strong>{money(customer.wallet_balance)}</strong></div>
      {total > customer.wallet_balance && <div className="home-alert" role="alert">Ví còn thiếu {money(total - customer.wallet_balance)}. <button className="client-inline-link" onClick={() => navigate('/client/wallet')}>Nạp thêm tiền</button></div>}
      {!canOrder && <p className="home-fine-print">Bạn cần phiên chơi đang hoạt động và ví còn tiền để đặt món.</p>}
      <button className="home-button primary" disabled={!count || !canOrder || total > customer.wallet_balance} onClick={() => { setError(''); setConfirm(true); }}>Đặt món · {money(total)} <ArrowRight size={16}/></button><p className="order-cart-note">Trừ ví ngay khi xác nhận đặt món.<br/>Món được nhân viên hủy sẽ hoàn tiền vào ví.</p>
    </div></aside></div> : tab === 'orders' ? <section className="client-orders-list">{orders.map((order) => {
      const rows = db.order_items.filter((r) => r.order_id === order.id);
      return <article className="home-panel" key={order.id}><div className="client-panel-heading"><h2><ShoppingBag size={18}/> Đơn #GM{order.id}</h2><Badge status={orderStatus(db, order.id)}/></div><div className="client-order-card-body"><div className="client-order-summary"><span className="client-muted">{dateTime(order.placed_at)} · Phiên #{order.gaming_session_id}</span><strong>{money(order.total_amount)} <small>{order.payment_method === 'Cash' ? (orderStatus(db, order.id) === 'Cancelled' ? 'tiền mặt · đã hủy' : (order.payment_status === 'Paid' ? 'tiền mặt · đã thu' : 'tiền mặt · chờ thu')) : 'đã trừ ví'}</small></strong></div>{rows.map((item) => <div className="client-order-line" key={item.id}><span>{item.quantity} × {db.products.find((p) => p.id === item.product_id)?.name}</span><Badge status={item.status}/></div>)}<button className="home-text-button" onClick={() => setSelected(order.id)}>Xem chi tiết & yêu cầu hủy <ChevronRight size={15}/></button></div></article>;
    })}{!orders.length && <Empty title="Bạn chưa có đơn món nào">Chọn món trong thực đơn để bắt đầu.</Empty>}</section> : null}
    {confirm && <HomeDialog title="Xác nhận đặt món" onClose={() => setConfirm(false)}>{error && <div className="home-alert" role="alert">{error}</div>}<p className="home-dialog-intro">Phục vụ tại {computer?.code}. Kiểm tra lại giỏ món trước khi xác nhận.</p>{items.map(({ product, quantity: qty }) => <div className="home-detail-row" key={product.id}><span>{qty} × {product.name}</span><strong>{money(product.current_price * qty)}</strong></div>)}<div className="home-cart-total"><span>Số tiền trừ ví</span><strong>{money(total)}</strong></div><div className="home-detail-row"><span>Số dư sau khi đặt</span><strong>{money(customer.wallet_balance - total)}</strong></div><p className="home-fine-print">Món đang chờ có thể gửi yêu cầu hủy. Món đã được nhân viên xác nhận không thể hủy.</p><button className="home-button primary" onClick={submit}>Xác nhận & thanh toán <ArrowRight size={16}/></button></HomeDialog>}
    {selectedOrder && <OrderDetail order={selectedOrder} onClose={() => setSelected(null)}/>}
  </div>;
}
