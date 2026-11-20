import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowUpRight, ChevronRight, Clock3, Cpu, Gamepad2, History, Info, Monitor, Plus, ReceiptText, Trophy, Users, Utensils, WalletCards, Zap } from 'lucide-react';
import { useClientDemo } from '../shared/ClientDemoContext';
import { Badge, DemoStrip, Empty, FoodArt, OrderDetail } from '../shared/ClientUI';
import { dateTime, money, orderStatus } from '../shared/demoModel';
import { useCatalog } from '../../shared/catalog/useCatalog';
import { useAuth } from '../../shared/auth/AuthContext';
import { getTournaments, getTournamentRegistrations } from '../../shared/tournamentApi';
import { useRealtime } from '../../shared/realtime/useRealtime';
import { useSessionHome } from './useSessionHome';
import { HomeSessionPanels } from './HomeSessionPanels';
import './home.css';

export default function HomePage() {
  const navigate = useNavigate();
  const catalog = useCatalog();
  const { db, account, customer } = useClientDemo();
  const home = useSessionHome();
  const { principal, accessToken } = useAuth();
  const [tournament, setTournament] = useState(null);
  const [registered, setRegistered] = useState(false);
  const [tournamentError, setTournamentError] = useState('');
  const [tournamentRefresh, setTournamentRefresh] = useState(0);
  const [now, setNow] = useState(() => new Date());
  const [filter, setFilter] = useState('all');
  const [showAll, setShowAll] = useState(false);
  const [selectedOrder, setSelectedOrder] = useState(null);
  useEffect(() => { const timer = setInterval(() => setNow(new Date()), 30000); return () => clearInterval(timer); }, []);
  const firstName = account.display_name.split(' ').slice(-2).join(' ');
  const reloadTournament = useCallback(() => setTournamentRefresh((version) => version + 1), []);
  useRealtime({
    enabled: principal?.type === 'customer',
    accessToken,
    events: ['TournamentChanged', 'RegistrationChanged'],
    onEvent: reloadTournament,
    onReady: reloadTournament,
  });
  useEffect(() => {
    const controller = new AbortController();
    getTournaments('client', controller.signal).then(async (rows) => {
      const current = rows.find((row) => row.status === 'RegistrationOpen') || rows[0] || null;
      if (!current) {
        setTournament(null);
        setRegistered(false);
        return;
      }
      const participants = await getTournamentRegistrations('client', current.id, controller.signal);
      if (controller.signal.aborted) return;
      setTournament(current);
      setRegistered(participants.some((row) => row.isCurrentCustomer));
      setTournamentError('');
    }).catch((error) => {
      if (!controller.signal.aborted) setTournamentError(error.message);
    });
    return () => controller.abort();
  }, [accessToken, tournamentRefresh]);
  const activities = [
    ...db.orders.filter((r) => r.customer_id === customer.id).map((r) => ({ key: `order-${r.id}`, id: r.id, kind: 'order', title: `Đơn món #GM${r.id}`, subtitle: `${db.order_items.filter((i) => i.order_id === r.id).reduce((s, i) => s + i.quantity, 0)} phần · Phục vụ tại máy`, time: r.placed_at, amount: r.total_amount, status: orderStatus(db, r.id), Icon: Utensils })),
    ...home.transactions.map((r) => ({ key: `wallet-${r.transactionId}`, id: r.transactionId, kind: 'wallet', title: r.type === 'GamingCharge' ? 'Tiền giờ' : r.type, subtitle: `Giao dịch #${r.transactionId}`, time: r.createdAtUtc, amount: r.amount, status: 'Paid', Icon: WalletCards })),
    ...home.invoices.map((r) => ({ key: `invoice-${r.id}`, id: r.id, kind: 'invoice', title: `Hóa đơn #${r.id}`, subtitle: r.gamingSessionId ? `Phiên #${r.gamingSessionId}` : 'Đơn món', time: r.issuedAt, amount: r.grossAmount, status: r.status, Icon: ReceiptText })),
  ].sort((a, b) => new Date(b.time) - new Date(a.time));
  const filtered = activities.filter((r) => filter === 'all' || filter === r.kind);
  function goToOrdering(product) { navigate('/client/ordering', { state: { openCart: Boolean(product), initialCartItems: product ? [{ ProductId: product.id, Quantity: 1 }] : [] } }); }
  function openActivity(row) { if (row.kind === 'order') setSelectedOrder(row.id); else navigate(row.kind === 'wallet' ? '/client/wallet' : '/client/invoices'); }
  return <div className="cafe-home"><DemoStrip/>
    <div className="home-heading"><div><div className="home-eyebrow">PLAY. CONNECT. REPEAT.</div><h1>Chào {firstName}, sẵn sàng vào trận<span>?</span></h1><p>Mọi thứ bạn cần cho buổi chơi hôm nay, ngay tại đây.</p></div><button className="home-button" onClick={() => goToOrdering()}><Utensils size={16}/> Gọi món tại máy</button></div>
    <div className="home-dashboard-grid">
      <HomeSessionPanels home={home} now={now} navigate={navigate}/>
      <section className="home-food-section" aria-labelledby="food-title"><div className="home-section-heading"><div><span className="home-eyebrow">NEXORA KITCHEN</span><h2 id="food-title">Nạp năng lượng, tiếp trận</h2></div><button className="home-text-button" onClick={() => goToOrdering()}>Xem thực đơn <ArrowUpRight size={15}/></button></div><div className="home-food-grid">{catalog.loading ? <p role="status">Đang tải thực đơn…</p> : catalog.error ? <div className="home-alert" role="alert">{catalog.error}<button className="home-button" onClick={catalog.reload}>Thử lại thực đơn</button></div> : catalog.db.products.filter(product => product.is_active && product.stock_quantity > 0).slice(0, 3).map((product) => <article className="home-food-card" key={product.id}><FoodArt product={product}/><div className="home-food-body"><span className="home-food-category">{catalog.db.product_categories.find((r) => r.id === product.category_id)?.name}</span><h3>{product.name}</h3><div className="home-food-bottom"><strong>{money(product.current_price)}</strong><button className="home-add" aria-label={`Thêm ${product.name} vào giỏ`} onClick={() => goToOrdering(product)}><Plus size={18}/></button></div></div></article>)}</div></section>
      <section className="home-panel home-tournament" aria-labelledby="tournament-title"><Trophy className="home-trophy-art" size={145} strokeWidth={.7} aria-hidden="true"/><div className="home-tournament-content"><span className="home-badge"><i/>{registered ? 'BẠN ĐÃ ĐĂNG KÝ' : 'GIẢI ĐẤU SẮP TỚI'}</span><h2 id="tournament-title">{tournament?.name || 'Hẹn bạn ở giải đấu tới'}</h2>{tournament ? <><p><Gamepad2 size={15}/> {tournament.gameName} <span>·</span><Users size={15}/> Đội {tournament.teamSize} người</p><p><Clock3 size={15}/>{dateTime(tournament.startTimeUtc)}</p><div className="home-tournament-fee">{tournament.entryFee === 0 ? 'Miễn phí tham gia' : money(tournament.entryFee)}<span> / người chơi</span></div><button className="home-button tournament" onClick={() => navigate(`/client/tournaments?tournament=${tournament.id}`)}>{registered ? 'Xem đăng ký của bạn' : 'Chi tiết & đăng ký'}<ArrowUpRight size={16}/></button></> : <p role={tournamentError ? 'alert' : undefined}>{tournamentError || 'Giải đấu mới sẽ xuất hiện khi lịch được công bố.'}</p>}</div></section>
    </div>
    <section className="home-activity-section" aria-labelledby="activity-title"><div className="home-section-heading"><div><span className="home-eyebrow">MỌI THỨ TRONG TẦM TAY</span><h2 id="activity-title">Hoạt động gần đây</h2></div><button className="home-text-button" onClick={() => setShowAll(!showAll)}>{showAll ? 'Thu gọn' : 'Xem tất cả'}<History size={15}/></button></div><div className="home-panel"><div className="home-tabs" role="group" aria-label="Lọc hoạt động">{[['all', 'Tất cả'], ['order', 'Đơn món'], ['wallet', 'Giao dịch ví'], ['invoice', 'Hóa đơn']].map(([value, label]) => <button key={value} aria-pressed={filter === value} className={filter === value ? 'selected' : ''} onClick={() => setFilter(value)}>{label}{value === 'all' && <span>{activities.length}</span>}</button>)}</div><div className="home-table-wrap"><table className="home-table"><thead><tr><th>Giao dịch / đơn hàng</th><th>Thời gian</th><th>Số tiền</th><th>Trạng thái</th><th><span className="home-sr-only">Chi tiết</span></th></tr></thead><tbody>{(showAll ? filtered : filtered.slice(0, 5)).map((row) => <tr key={row.key}><td><div className="home-activity-name"><span className={`home-activity-icon ${row.kind}`}><row.Icon size={18}/></span><div><button onClick={() => openActivity(row)}>{row.title}</button><small>{row.subtitle}</small></div></div></td><td className="home-date">{dateTime(row.time)}</td><td className={`home-amount ${row.kind === 'wallet' && row.amount > 0 ? 'positive' : ''}`}>{row.kind === 'wallet' && row.amount > 0 ? '+' : row.amount < 0 ? '−' : ''}{money(Math.abs(row.amount))}</td><td><Badge status={row.status}/></td><td><button className="home-icon-button" aria-label={`Xem ${row.title}`} onClick={() => openActivity(row)}><ChevronRight size={17}/></button></td></tr>)}</tbody></table></div>{!filtered.length && <Empty title="Chưa có hoạt động trong mục này"/>}</div></section>
    <footer className="home-footer"><span><Zap size={13}/> NEXORA CYBER · Chơi hết mình, kết nối hết cỡ.</span><time dateTime={now.toISOString()}>{dateTime(now)}</time></footer>{selectedOrder && <OrderDetail order={db.orders.find((r) => r.id === selectedOrder)} onClose={() => setSelectedOrder(null)}/>}
  </div>;
}
