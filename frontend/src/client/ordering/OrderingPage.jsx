import React, { useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ChevronRight, Dices, History, Plus, Search, ShoppingBag, Trash2, Utensils, RefreshCw } from 'lucide-react';
import TodaySuggestion, { TodaySuggestionTeaser } from './TodaySuggestion';
import { remainingStock } from './recommendation';
import { useCatalog } from '../../shared/catalog/useCatalog';
import { reconcileCart } from '../../shared/catalog/catalogModel';
import { useClientDemo } from '../shared/ClientDemoContext';
import { Badge, Empty, Feedback, FoodArt, OrderDetail, PageHeading, Stepper } from '../shared/ClientUI';
import { dateTime, money, orderStatus } from '../shared/demoModel';

export function OrderingPage() {
  const { db: demoDb, customer } = useClientDemo();
  const { db, loading, error: catalogError, reload } = useCatalog();
  const location = useLocation();
  const navigate = useNavigate();
  const [tab, setTab] = useState('menu');
  const [category, setCategory] = useState('all');
  const [suggestionCategory, setSuggestionCategory] = useState('all');
  const [query, setQuery] = useState('');
  const [cart, setCart] = useState({});
  const [selected, setSelected] = useState(null);
  const [notice, setNotice] = useState('');
  const handoff = useRef(null);

  useEffect(() => {
    if (loading || catalogError) return;
    setCart(current => reconcileCart(db, current));
  }, [db, loading, catalogError]);
  useEffect(() => {
    if (loading || catalogError || !location.state || handoff.current === location.key) return;
    handoff.current = location.key;
    const initial = {};
    for (const item of location.state.initialCartItems || []) {
      if (Number.isInteger(item.Quantity) && item.Quantity > 0) initial[item.ProductId] = item.Quantity;
    }
    setCart(reconcileCart(db, initial));
    navigate(location.pathname, { replace: true, state: null });
  }, [location.key, location.state, location.pathname, db, loading, catalogError, navigate]);

  const validCart = reconcileCart(db, cart);
  const items = Object.entries(validCart).map(([id, quantity]) => ({ product: db.products.find(p => p.id === Number(id)), quantity }));
  const total = items.reduce((sum, r) => sum + r.product.current_price * r.quantity, 0);
  const count = items.reduce((sum, r) => sum + r.quantity, 0);
  const products = db.products.filter(p => p.is_active && (category === 'all' || p.category_id === category) && p.name.toLocaleLowerCase('vi').includes(query.trim().toLocaleLowerCase('vi')));
  const orders = demoDb.orders.filter(r => r.customer_id === customer.id).sort((a, b) => new Date(b.placed_at) - new Date(a.placed_at));
  const selectedOrder = orders.find(order => order.id === selected);
  function quantity(id, value) { setCart(current => reconcileCart(db, { ...current, [id]: value })); }
  function addSuggestion(id, amount = 1) { quantity(id, (validCart[id] || 0) + amount); setNotice(`Đã thêm ${db.products.find(p => p.id === id)?.name} vào giỏ món.`); }

  return <div className="cafe-home client-module ordering-page">
    <PageHeading eyebrow="NEXORA KITCHEN" title={<>Nạp năng lượng, <span>tiếp trận.</span></>} description="Khám phá thực đơn và chọn món yêu thích.">
      <button className="home-button" disabled={loading} onClick={reload}><RefreshCw size={16}/> Tải lại thực đơn</button>
    </PageHeading>
    <Feedback notice={notice} error={catalogError} onClear={() => setNotice('')}/>
    <div className="client-page-tabs" role="group" aria-label="Nội dung gọi món">
      <button className={tab === 'menu' ? 'selected' : ''} aria-pressed={tab === 'menu'} onClick={() => setTab('menu')}><Utensils size={16}/> Thực đơn</button>
      <button className={`today-tab ${tab === 'suggestion' ? 'selected' : ''}`} aria-pressed={tab === 'suggestion'} onClick={() => setTab('suggestion')}><Dices size={16}/> Hôm nay ăn gì?</button>
      <button className={tab === 'orders' ? 'selected' : ''} aria-pressed={tab === 'orders'} onClick={() => setTab('orders')}><History size={16}/> Lịch sử demo <span>{orders.length}</span></button>
    </div>
    {tab !== 'orders' && (loading ? <p role="status">Đang tải thực đơn…</p> : !catalogError && <div className="order-layout"><section className="order-menu-main">
      {tab === 'menu' ? <>
        <TodaySuggestionTeaser db={db} onOpen={() => setTab('suggestion')}/>
        <div className="client-menu-toolbar"><div className="client-category-tabs" role="group" aria-label="Danh mục món">{[{ id: 'all', name: 'Tất cả' }, ...db.product_categories].map(c => <button aria-pressed={category === c.id} className={category === c.id ? 'selected' : ''} key={c.id} onClick={() => setCategory(c.id)}>{c.name}<span>{c.id === 'all' ? db.products.length : db.products.filter(p => p.category_id === c.id).length}</span></button>)}</div><label className="home-search client-menu-search"><Search size={16}/><span className="home-sr-only">Tìm món</span><input placeholder="Tìm món bạn thích…" value={query} onChange={e => setQuery(e.target.value)}/></label></div>
        <div className="ordering-food-grid">{products.map(product => <article className="home-food-card" key={product.id}><FoodArt product={product}/><div className="home-food-body"><span className="home-food-category">{db.product_categories.find(c => c.id === product.category_id)?.name}</span><h3>{product.name}</h3><span className="order-stock-label">{remainingStock(db, product) ? `Còn ${remainingStock(db, product)} phần` : 'Hết hàng'}</span><div className="home-food-bottom"><strong>{money(product.current_price)}</strong>{validCart[product.id] ? <Stepper name={product.name} value={validCart[product.id]} max={Math.min(99, remainingStock(db, product))} onChange={v => quantity(product.id, v)}/> : <button className="home-add" disabled={!remainingStock(db, product)} aria-label={`Thêm ${product.name} vào giỏ`} onClick={() => quantity(product.id, 1)}><Plus size={18}/></button>}</div></div></article>)}</div>
        {!products.length && <Empty title="Chưa tìm thấy món">Thử tên khác hoặc chọn danh mục khác.</Empty>}
      </> : <TodaySuggestion db={db} category={suggestionCategory} onCategoryChange={setSuggestionCategory} cart={validCart} onAdd={addSuggestion} onBack={() => setTab('menu')}/>}
    </section><aside className="order-cart home-panel" aria-label="Giỏ món"><div className="client-panel-heading"><h2><ShoppingBag size={18}/> Giỏ món của bạn <span className="client-count">{count}</span></h2>{count > 0 && <button className="home-icon-button" onClick={() => setCart({})} aria-label="Xóa giỏ hàng"><Trash2 size={16}/></button>}</div><div className="order-cart-body">
      {!items.length ? <Empty title="Giỏ món đang trống">Chọn món từ thực đơn để thêm vào giỏ.</Empty> : <div className="order-cart-items">{items.map(({ product, quantity: qty }) => <div className="order-cart-line" key={product.id}><FoodArt product={product} small/><div><strong>{product.name}</strong><small>{money(product.current_price)}</small><Stepper name={product.name} value={qty} max={Math.min(99, remainingStock(db, product))} onChange={v => quantity(product.id, v)}/></div><button className="home-icon-button" aria-label={`Bỏ ${product.name}`} onClick={() => quantity(product.id, 0)}><Trash2 size={14}/></button></div>)}</div>}
      <div className="order-cart-total"><span>Tạm tính ({count} phần)</span><strong>{money(total)}</strong></div>
      <button className="home-button primary" disabled>Đặt món · Chưa khả dụng</button><p className="order-cart-note">Hiện chưa thể gửi đơn trực tuyến. Vui lòng liên hệ nhân viên để đặt món.</p>
    </div></aside></div>)}
    {tab === 'orders' && <section className="client-orders-list"><p className="client-caption">Lịch sử dưới đây là dữ liệu demo trên trình duyệt.</p>{orders.map(order => <article className="home-panel" key={order.id}><div className="client-panel-heading"><h2><ShoppingBag size={18}/> Đơn #GM{order.id}</h2><Badge status={orderStatus(demoDb, order.id)}/></div><div className="client-order-card-body"><div className="client-order-summary"><span className="client-muted">{dateTime(order.placed_at)}</span><strong>{money(order.total_amount)}</strong></div>{demoDb.order_items.filter(r => r.order_id === order.id).map(item => <div className="client-order-line" key={item.id}><span>{item.quantity} × {demoDb.products.find(p => p.id === item.product_id)?.name}</span><Badge status={item.status}/></div>)}<button className="home-text-button" onClick={() => setSelected(order.id)}>Xem chi tiết <ChevronRight size={15}/></button></div></article>)}{!orders.length && <Empty title="Chưa có đơn demo"/>}</section>}
    {selectedOrder && <OrderDetail order={selectedOrder} onClose={() => setSelected(null)}/>}
  </div>;
}
