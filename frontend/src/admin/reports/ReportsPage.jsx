import React, { useState } from 'react';
import { Banknote, CircleDollarSign, Monitor, RotateCcw } from 'lucide-react';
import { useComputers } from '../computers/computerStore';
import { useAdmin } from '../shared/AdminContext';
import { AdminPage, StatCard, Panel, Table, Notice, ExportButton, Row } from '../shared/AdminUI';
import { addDays, dayKey, money, reportData } from '../shared/adminModel';
import { DonutChart, TrendChart } from '../shared/Charts';

export function ReportsPage() {
  const { db, warning } = useAdmin();
  const computers = useComputers();
  const today = dayKey(new Date().toISOString());
  const [range, setRange] = useState({ from: addDays(today, -6), to: today });
  const [downloadError, setDownloadError] = useState('');
  let report, error = '';
  try { report = reportData(db, range, computers); } catch (err) { error = err.message; }
  const series = [{ key: 'gaming', name: 'Tiền máy' }, { key: 'food', name: 'Món ăn / đồ uống' }];
  return <AdminPage title="Báo cáo & thống kê" description="Doanh thu dịch vụ, cơ cấu thu và hiệu quả vận hành theo khoảng ngày." actions={report && <ExportButton filename={`bao-cao-${range.from}-${range.to}.csv`} headers={['Ngày', 'Tiền máy', 'Đơn món sau hoàn', 'Tổng doanh thu']} rows={report.timeline.map((row) => [row.day, row.gaming, row.food, row.total])} onError={setDownloadError}/>}>
    <Notice error={error || warning || downloadError} clear={() => setDownloadError('')}/>
    <div className="admin-date-filter"><label>Từ ngày<input type="date" value={range.from} onChange={(event) => setRange({ ...range, from: event.target.value })}/></label><label>Đến ngày<input type="date" value={range.to} onChange={(event) => setRange({ ...range, to: event.target.value })}/></label><button className="cafe-button" onClick={() => setRange({ from: addDays(today, -6), to: today })}>7 ngày</button><button className="cafe-button" onClick={() => setRange({ from: addDays(today, -29), to: today })}>30 ngày</button></div>
    {report && <>
      <div className="cafe-stats"><StatCard label="Doanh thu dịch vụ" value={money(report.total)} icon={CircleDollarSign}><span>Tiền máy + món đã thanh toán − hoàn tiền</span></StatCard><StatCard label="Tiền mặt đơn món đã thu" value={money(report.cashCollected)} icon={Banknote}/><StatCard label="Phiên bắt đầu trong kỳ" value={report.sessions} icon={Monitor}/><StatCard label="Đã hoàn ví trong kỳ" value={money(report.refunds)} icon={RotateCcw}/></div>
      <div className="admin-grid-two"><Panel title="Xu hướng doanh thu" description="Ghi nhận theo ngày giao dịch ở múi giờ Việt Nam, gồm cả ngày không có doanh thu."><TrendChart data={report.timeline} series={series}/></Panel><Panel title="Cơ cấu doanh thu" description="Tách nguồn thu từ dịch vụ, không bao gồm nạp ví."><DonutChart label="doanh thu" currency data={[{ name: 'Tiền máy', value: Math.max(0, report.gaming) }, { name: 'Đơn món', value: Math.max(0, report.food) }]}/>{(report.gaming < 0 || report.food < 0) && <p className="admin-report-explanation">Một nguồn thu đang âm do hoàn tiền; xem biểu đồ đường và bảng số liệu để đối chiếu.</p>}</Panel></div>
      <div className="admin-grid-two admin-grid-equal"><Panel title="Sản phẩm bán chạy" description="Món đã thanh toán, loại món đã hủy. Giá bán lấy tại lúc đặt."><Table rows={report.topProducts} columns={[{ key: 'name', label: 'Sản phẩm' }, { key: 'quantity', label: 'Số phần', sortValue: (row) => row.quantity }, { key: 'amount', label: 'Giá trị bán', render: (row) => money(row.amount) }]}/></Panel><Panel title="Đối chiếu dòng tiền" description="Các khoản sau không cộng gộp vào doanh thu dịch vụ."><div className="admin-report-explanation"><Row label="Tiền nạp ví đã thu trong kỳ">{money(report.deposits)}</Row><Row label="Tiền nhập kho trong kỳ">{money(report.purchases)}</Row><Row label="Đơn món tiền mặt chờ thu (toàn bộ)">{money(report.cashDue)}</Row><Row label="Công suất máy hiện tại">{report.occupancy}%</Row><p>Nạp ví là tiền ứng trước. Đơn tiền mặt chỉ ghi doanh thu khi xác nhận đã thu, không tính lại tiền đặt món. Hoàn tiền được trừ vào ngày hoàn, kể cả đơn phát sinh trước kỳ báo cáo.</p><p>Giá trị nhập kho không phải giá vốn tiêu thụ; báo cáo không dùng chỉ số này để tính lợi nhuận.</p></div></Panel></div>
      <Panel title="Chi tiết theo ngày" description={`${range.from} → ${range.to} · Bao gồm hai ngày đầu/cuối, tối đa 366 ngày.`}><Table rows={report.timeline.map((row) => ({ ...row, id: row.day }))} pageSize={10} resetKey={range.from + range.to} columns={[{ key: 'day', label: 'Ngày', sortValue: (row) => row.day }, { key: 'gaming', label: 'Tiền máy', render: (row) => money(row.gaming) }, { key: 'food', label: 'Đơn món sau hoàn', render: (row) => money(row.food) }, { key: 'total', label: 'Doanh thu', sortValue: (row) => row.total, render: (row) => <strong>{money(row.total)}</strong> }]}/></Panel>
    </>}
  </AdminPage>;
}
