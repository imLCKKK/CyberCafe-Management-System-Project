import React, { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { Banknote, CheckCircle2, ChevronLeft, ChevronRight, Download, Info, Printer, ReceiptText, RotateCcw, Search } from 'lucide-react';
import HomeDialog from '../home/HomeDialog';
import { useClientDemo } from '../shared/ClientDemoContext';
import { Badge, DemoStrip, Empty, Feedback, PageHeading } from '../shared/ClientUI';
import { dateTime, getReceipts, minutesLabel, money } from '../shared/demoModel';
import ReceiptDetail, { PaymentBadge } from './ReceiptDetail';
import { filterReceipts, receiptText } from './invoiceModel';
import './invoices.css';

const DEFAULT_FILTERS = { session: 'all', payment: 'all', from: '', to: '', query: '' };
const PAGE_SIZE = 10;

export function InvoicesPage() {
  const { db, account } = useClientDemo();
  const [filters, setFilters] = useState(DEFAULT_FILTERS);
  const [selected, setSelected] = useState(null);
  const [page, setPage] = useState(1);
  const [now, setNow] = useState(() => new Date());
  const [error, setError] = useState('');
  useEffect(() => { const timer = setInterval(() => setNow(new Date()), 60000); return () => clearInterval(timer); }, []);
  const receipts = getReceipts(db);
  const rows = filterReceipts(receipts, db, filters);
  const receipt = receipts.find((row) => row.id === selected);
  const invalidRange = Boolean(filters.from && filters.to && filters.from > filters.to);
  const filtered = Object.keys(DEFAULT_FILTERS).some((key) => filters[key] !== DEFAULT_FILTERS[key]);
  const pages = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const currentPage = Math.min(page, pages);
  const visible = rows.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);
  const sum = (key) => rows.reduce((total, row) => total + row[key], 0);
  function changeFilter(key, value) { setFilters((current) => ({ ...current, [key]: value })); setPage(1); }
  function resetFilters() { setFilters(DEFAULT_FILTERS); setPage(1); }
  function download(row) {
    let url;
    try {
      url = URL.createObjectURL(new Blob(['\uFEFF' + receiptText(row, db, account, now)], { type: 'text/plain;charset=utf-8' }));
      const anchor = document.createElement('a');
      anchor.href = url; anchor.download = `TRAM-PC${row.id}.txt`;
      document.body.appendChild(anchor); anchor.click(); anchor.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1000); setError('');
    } catch { if (url) URL.revokeObjectURL(url); setError('Không thể tải bảng kê. Hãy thử lại hoặc dùng In / lưu PDF.'); }
  }
  return <div className="cafe-home client-module invoices-page">
    <DemoStrip/>
    <PageHeading eyebrow="YOUR PLAY, AT A GLANCE" title={<>Hóa đơn & <span>lịch sử chơi.</span></>} description="Theo dõi tiền máy, đơn món và các khoản thanh toán trong từng phiên."><span className="client-heading-chip"><ReceiptText size={16}/> {receipts.length} bảng kê phiên chơi</span></PageHeading>
    <Feedback error={error}/>
    <div className="invoice-stats" aria-label={`Tổng hợp ${rows.length} phiên theo bộ lọc`}>
      {[{ label: 'Tổng ghi nhận', value: sum('total'), caption: `${rows.length} phiên theo bộ lọc`, Icon: ReceiptText },
        { label: 'Đã thanh toán', value: sum('paidTotal'), caption: `Ví ${money(sum('walletPaid'))} · Tiền mặt ${money(sum('cashPaid'))}`, Icon: CheckCircle2 },
        { label: 'Tiền mặt chờ thu', value: sum('cashDue'), caption: 'Chưa trừ ví · Thanh toán với nhân viên', Icon: Banknote, due: true },
        { label: 'Đã hoàn vào ví', value: sum('refunds'), caption: sum('refundPending') > 0 ? `Còn ${money(sum('refundPending'))} chờ hoàn` : 'Đã trừ khỏi tổng ghi nhận', Icon: RotateCcw },
      ].map(({ label, value, caption, Icon, due }) => <div className={`home-panel invoice-stat ${due && value > 0 ? 'has-due' : ''}`} key={label}><span><Icon size={16}/>{label}</span><strong>{money(value)}</strong><small>{caption}</small></div>)}
    </div>
    <div className="home-info-box invoice-info"><Info size={18}/><span>Tiền máy và món trả bằng ví đã được ghi nhận trong quá trình sử dụng. Chỉ khoản <b>tiền mặt chờ thu</b> cần thanh toán với nhân viên. Phiên đang chơi hiển thị bảng kê tạm tính.</span></div>
    <section className="home-panel invoice-list-panel" aria-label="Danh sách bảng kê">
      <div className="home-tabs" role="group" aria-label="Lọc phiên chơi">{[['all', 'Tất cả phiên'], ['Active', 'Đang chơi'], ['Ended', 'Đã kết thúc']].map(([value, name]) => <button key={value} aria-pressed={filters.session === value} className={filters.session === value ? 'selected' : ''} onClick={() => changeFilter('session', value)}>{name}<span>{receipts.filter((row) => value === 'all' || row.session.status === value).length}</span></button>)}</div>
      <div className="invoice-filters">
        <label className="home-search invoice-search"><Search size={16}/><span className="home-sr-only">Tìm bảng kê, máy, mã đơn hoặc món</span><input value={filters.query} onChange={(event) => changeFilter('query', event.target.value)} placeholder="Tìm mã phiên, máy, đơn hoặc món…"/></label>
        <label className="invoice-filter-field">Thanh toán<select value={filters.payment} onChange={(event) => changeFilter('payment', event.target.value)}><option value="all">Tất cả thanh toán</option><option value="due">Còn tiền mặt chờ thu</option><option value="paid">Đã thanh toán</option><option value="refund">Có hoàn / chờ hoàn tiền</option></select></label>
        <label className="invoice-filter-field">Bắt đầu từ ngày<input type="date" aria-invalid={invalidRange || undefined} value={filters.from} onChange={(event) => changeFilter('from', event.target.value)}/></label>
        <label className="invoice-filter-field">Đến hết ngày<input type="date" aria-invalid={invalidRange || undefined} value={filters.to} onChange={(event) => changeFilter('to', event.target.value)}/></label>
      </div>
      {invalidRange && <p className="invoice-filter-error" role="alert">Ngày bắt đầu phải bằng hoặc trước ngày kết thúc.</p>}
      <div className="invoice-list-meta"><span aria-live="polite">{rows.length} / {receipts.length} bảng kê{filtered ? ' phù hợp bộ lọc' : ''} · Ngày tính theo lúc bắt đầu phiên</span>{filtered && <button type="button" className="home-text-button" onClick={resetFilters}><RotateCcw size={13}/> Xóa bộ lọc</button>}</div>
      <div className="home-table-wrap invoice-table-wrap"><table className="home-table invoice-table"><caption className="home-sr-only">Bảng kê phiên chơi và trạng thái thanh toán</caption><thead><tr><th scope="col">Bảng kê / Phiên chơi</th><th scope="col">Tiền máy / Món</th><th scope="col">Tổng ghi nhận</th><th scope="col">Thanh toán</th><th scope="col"><span className="home-sr-only">Thao tác</span></th></tr></thead><tbody>{visible.map((row) => <tr key={row.id}>
        <td><div className="invoice-row-title"><span className="client-icon-tile neutral"><ReceiptText size={18}/></span><div><button className="invoice-open-link" onClick={() => setSelected(row.id)}>Bảng kê #PC{row.id}</button><small>{row.computer?.code || '—'} · {row.orders.length} đơn món</small></div></div><span className="invoice-row-date">{dateTime(row.session.started_at)}</span><span className="invoice-row-session"><Badge status={row.session.status}/><small>{minutesLabel(row.session.started_at, row.session.ended_at || now)}</small></span></td>
        <td><div className="invoice-cost-lines"><span>Tiền máy <b>{money(row.session.charged_amount)}</b></span><span>Tiền món <b>{money(row.food)}</b></span>{row.refunds > 0 && <small>Đã hoàn ví: {money(row.refunds)}</small>}{row.cashCancelled > 0 && <small>Không thu món hủy: {money(row.cashCancelled)}</small>}</div></td>
        <td><strong className="invoice-row-total">{money(row.total)}</strong><small className="invoice-total-caption">{row.session.status === 'Active' ? 'Tạm tính · Phiên đang chơi' : 'Phiên đã kết thúc'}</small></td>
        <td><PaymentBadge receipt={row}/><div className="invoice-row-payment"><span>Ví: {money(row.walletPaid)}</span>{row.cashPaid > 0 && <span>Tiền mặt đã thu: {money(row.cashPaid)}</span>}{row.cashDue > 0 && <strong>Chờ thu: {money(row.cashDue)}</strong>}{row.refundPending > 0 && <span className="invoice-warning-text">Chờ hoàn: {money(row.refundPending)}</span>}</div></td>
        <td><div className="invoice-row-actions"><button className="home-icon-button" aria-label={`Tải bảng kê PC${row.id}`} title="Tải bảng kê TXT" onClick={() => download(row)}><Download size={16}/></button><button className="home-icon-button" aria-label={`Xem bảng kê PC${row.id}`} title="Xem chi tiết" onClick={() => setSelected(row.id)}><ChevronRight size={17}/></button></div></td>
      </tr>)}</tbody></table></div>
      {!rows.length && !invalidRange && <Empty title="Chưa có bảng kê phù hợp" icon={ReceiptText}>Thử mã phiên, mã đơn, tên món hoặc chọn khoảng ngày khác.{filtered && <button className="home-text-button invoice-reset-empty" onClick={resetFilters}>Xóa bộ lọc</button>}</Empty>}
      {rows.length > PAGE_SIZE && <nav className="invoice-pagination" aria-label="Phân trang bảng kê"><span>{(currentPage - 1) * PAGE_SIZE + 1}–{Math.min(currentPage * PAGE_SIZE, rows.length)} / {rows.length} bảng kê</span><div><button type="button" className="home-icon-button" disabled={currentPage === 1} aria-label="Trang trước" onClick={() => setPage(currentPage - 1)}><ChevronLeft size={16}/></button><span>Trang {currentPage} / {pages}</span><button type="button" className="home-icon-button" disabled={currentPage === pages} aria-label="Trang sau" onClick={() => setPage(currentPage + 1)}><ChevronRight size={16}/></button></div></nav>}
    </section>
    <p className="client-caption">Bảng kê tổng hợp từ phiên chơi, đơn món và giao dịch ví; không phải hóa đơn thuế.</p>
    {receipt && <>
      <HomeDialog title={`Bảng kê #PC${receipt.id}`} className="invoice-detail-dialog" onClose={() => setSelected(null)}>
        <Feedback error={error}/>
        <ReceiptDetail receipt={receipt} db={db} account={account} now={now}/>
        <div className="invoice-detail-actions"><button type="button" className="home-button" onClick={() => window.print()}><Printer size={16}/> In / lưu PDF</button><button type="button" className="home-button primary" onClick={() => download(receipt)}><Download size={16}/> Tải bảng kê TXT</button></div>
      </HomeDialog>
      {createPortal(<div className="invoice-print-copy" aria-hidden="true"><ReceiptDetail receipt={receipt} db={db} account={account} now={now} print/></div>, document.body)}
    </>}
  </div>;
}
