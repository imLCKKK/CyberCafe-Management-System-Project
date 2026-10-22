import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowRight, CircleDollarSign, ChartNoAxesCombined, Monitor, RefreshCw, ShoppingBag } from 'lucide-react';
import { useComputers } from '../computers/computerStore';
import { remainingStock } from '../../client/ordering/recommendation';
import { orderStatus } from '../../client/shared/demoModel';
import { useAdmin } from '../shared/AdminContext';
import { AdminPage, StatCard, Panel, Status } from '../shared/AdminUI';
import { addDays, dayKey, foodComputerIds, money, reportData } from '../shared/adminModel';
import { DonutChart, TrendChart } from '../shared/Charts';

export function DashboardPage() {
  const { db, warning } = useAdmin();
  const computers = useComputers();
  const [updatedAt, setUpdatedAt] = useState(null);
  const today = dayKey(new Date().toISOString()), yesterday = addDays(today, -1);
  const current = reportData(db, { from: today, to: today }, computers);
  const previous = reportData(db, { from: yesterday, to: yesterday }, computers);
  const trend = reportData(db, { from: addDays(today, -6), to: today }, computers);
  const occupied = computers.filter((pc) => pc.Status === 'Occupied').length, available = computers.filter((pc) => pc.Status === 'Available').length, maintenance = computers.filter((pc) => pc.Status === 'Maintenance').length;
  const alerts = foodComputerIds(db);
  const pending = db.orders.filter((order) => db.order_items.some((item) => item.order_id === order.id && item.status === 'Pending')).length;
  const recent = [...db.orders].sort((a, b) => new Date(b.placed_at) - new Date(a.placed_at)).slice(0, 8);
  return <AdminPage title="Xin chào, Admin" description="Cùng xem hoạt động tại quán của bạn hôm nay." actions={<span className="cafe-system"><i/>Dữ liệu mẫu dùng chung</span>}>
    {warning && <div className="cafe-error" role="alert">{warning}</div>}
    <div className="cafe-stats"><StatCard label="Doanh thu hôm nay" value={money(current.total)} icon={CircleDollarSign}><span className={current.total >= previous.total ? 'cafe-positive' : 'cafe-muted'}>{previous.total > 0 ? `${((current.total / previous.total - 1) * 100).toFixed(1)}% so với hôm qua` : 'Hôm qua chưa phát sinh doanh thu'}</span></StatCard><StatCard label="Máy đang hoạt động" value={occupied} suffix={`/${computers.length}`} icon={Monitor} progress={current.occupancy}/><StatCard label="Tỷ lệ sử dụng" value={`${current.occupancy}%`} icon={ChartNoAxesCombined} progress={current.occupancy}/><StatCard label="Đơn món đang chờ" value={pending} icon={ShoppingBag}><Link className="cafe-attention" to="/admin/orders">Cần xử lý</Link></StatCard></div>
    <div className="cafe-dashboard-grid">
      <article className="cafe-panel cafe-map-panel"><div className="cafe-panel-heading"><div><h2>Sơ đồ máy tính</h2><p>Trạng thái hiện tại · {computers.length} máy · <span aria-live="polite">{updatedAt || 'Vừa cập nhật'}</span></p></div><button className="cafe-button" onClick={() => setUpdatedAt(new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', second: '2-digit' }).format(new Date()))}><RefreshCw size={14}/>Làm mới</button></div><div className="cafe-station-map">{computers.map((pc) => <Link key={pc.ComputerId} to={`/admin/computers?computer=${pc.ComputerId}`} className={`cafe-map-link ${pc.Status.toLowerCase()} ${alerts.has(pc.ComputerId) ? 'has-food' : ''}`} aria-label={`Xem ${pc.ComputerName}`}><span>{pc.ComputerName}</span><span className={`cafe-badge ${alerts.has(pc.ComputerId) ? 'food' : pc.Status.toLowerCase()}`}>{alerts.has(pc.ComputerId) ? 'Gọi món' : pc.Status === 'Occupied' ? 'Đang dùng' : pc.Status === 'Available' ? 'Trống' : 'Bảo trì'}</span></Link>)}</div><div className="cafe-legend"><span><i className="occupied"/>Đang dùng <b>{occupied}</b></span><span><i className="available"/>Còn trống <b>{available}</b></span><span><i className="food"/>Gọi món <b>{alerts.size}</b></span><span><i className="maintenance"/>Bảo trì <b>{maintenance}</b></span></div><div className="cafe-map-footer">Chọn một máy để xem thông tin chi tiết.<Link to="/admin/computers">Quản lý máy <ArrowRight size={14}/></Link></div></article>
      <article className="cafe-panel cafe-orders-panel"><div className="cafe-panel-heading"><div><h2>Đơn món gần đây</h2><p>{pending} đơn đang chờ xử lý</p></div><span className="cafe-attention">{pending} đang chờ</span></div><div className="cafe-order-list">{recent.map((order) => { const items = db.order_items.filter((row) => row.order_id === order.id); const pc = db.gaming_sessions.find((row) => row.id === order.gaming_session_id)?.computer_id; return <div className="cafe-order" key={order.id}><Link to={`/admin/orders?order=${order.id}`} className="cafe-order-pc">GM{order.id}</Link><div className="cafe-order-info"><strong>{items.map((item) => db.products.find((product) => product.id === item.product_id)?.name).join(', ')}</strong><small>Máy {pc || '—'} · {money(order.total_amount)}</small></div><Status value={orderStatus(db, order.id)}/></div>; })}</div><Link className="cafe-text-button cafe-orders-more" to="/admin/orders">Quản lý đơn món <ArrowRight size={15}/></Link></article>
    </div>
    <div className="admin-grid-two" style={{ marginTop: 24 }}><Panel title="Doanh thu 7 ngày gần nhất" description="Tổng từ giao dịch thực tế trong dữ liệu mẫu, đã trừ hoàn tiền." actions={<Link className="cafe-button" to="/admin/reports">Xem báo cáo</Link>}><TrendChart data={trend.timeline} series={[{ key: 'gaming', name: 'Tiền máy' }, { key: 'food', name: 'Đơn món' }]}/></Panel><Panel title="Công suất máy" description="Ảnh chụp trạng thái hiện tại."><DonutChart label="máy tính" data={[{ name: 'Đang dùng', value: occupied }, { name: 'Còn trống', value: available }, { name: 'Bảo trì', value: maintenance }]}/></Panel></div>
    <div className="admin-note-box">Có <strong>{db.products.filter((product) => product.is_active !== false && remainingStock(db, product) <= product.min_stock).length}</strong> mặt hàng cần kiểm tra tồn kho và <strong>{money(current.cashDue)}</strong> tiền mặt đơn món chờ thu (tất cả phiên). <Link className="admin-link" to="/admin/inventory">Mở kho</Link> · <Link className="admin-link" to="/admin/invoices">Đối chiếu thanh toán</Link></div>
  </AdminPage>;
}
