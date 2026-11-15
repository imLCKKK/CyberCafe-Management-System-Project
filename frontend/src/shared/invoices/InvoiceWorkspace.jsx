import React, { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Printer, RefreshCw, Search, Plus, ReceiptText } from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import Dialog from '../../client/shared/Dialog';
import { getInvoice, listInvoices, issueInvoice, invoiceMoney as money, invoiceDate as date, invoiceStatus as statusLabel } from './invoiceApi';
import './invoices.css';

function Snapshot({ invoice }) {
  return <article className="invoice-snapshot" data-testid="invoice-snapshot">
    <header><strong>NEXORA CYBER</strong><h2>Hóa đơn #{invoice.id}</h2><p>{invoice.customerName} · KH{invoice.customerId}</p><p>{date(invoice.issuedAt)} · {invoice.issuedBy}</p><p>{statusLabel(invoice.status)} · {invoice.orderIds.length ? `Đơn ${invoice.orderIds.join(', ')}` : `Phiên ${invoice.gamingSessionId}`}</p></header>
    <div className="sql-invoice-table-wrap"><table><caption className="home-sr-only">Chi tiết giá và giảm tại thời điểm phát hành</caption><thead><tr><th>Nội dung</th><th>SL</th><th>Đơn giá</th><th>Giảm</th><th>Sau giảm</th><th>Đã hủy</th></tr></thead><tbody>{invoice.lines.map(line => <tr key={line.id}><td>{line.description}{line.voucherCode && <small>Voucher {line.voucherCode}</small>}</td><td>{line.quantity}</td><td>{money(line.unitPrice)}</td><td>{money(line.discountAmount)}</td><td>{money(line.amount)}</td><td>{money(line.cancelledAmount)}</td></tr>)}</tbody></table></div>
    <dl className="sql-invoice-totals">{[
      ['Trước giảm', invoice.subtotal], ['Giảm giá', invoice.discount], ['Giá trị lúc phát hành', invoice.grossAmount],
      ['Giá trị đã hủy', invoice.cancelledAmount], ['Đã hoàn thành công', invoice.refundAmount],
      ['Đã thu ròng', invoice.paidAmount], ['Còn phải thu', invoice.amountDue], ['Tiền dư / cần đối soát', invoice.overpaidAmount],
    ].map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{money(value)}</dd></div>)}</dl>
    {invoice.payments.length > 0 && <section className="invoice-money-history"><h3>Giao dịch thu / hoàn</h3><ul>{invoice.payments.map(payment => <li key={payment.id}>#{payment.id} · {payment.type === 'Refund' ? 'Hoàn' : 'Thu'} {payment.method} · {money(payment.amount)} · {payment.status === 'Succeeded' ? 'Đã xác nhận' : payment.status === 'Pending' ? 'Đang chờ xác nhận' : payment.status === 'Failed' ? 'Thất bại' : 'Hết hạn'}</li>)}</ul></section>}
    <p className="sql-invoice-note">Chứng từ thanh toán nội bộ. Khoản hoàn đang chờ chưa được tính là đã hoàn. Giá và giảm được giữ theo thời điểm phát hành.</p>
  </article>;
}

function IssueForm({ onSaved, onClose }) {
  const [kind, setKind] = useState('Orders');
  const [source, setSource] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const key = useRef(crypto.randomUUID());
  const sending = useRef(false);
  async function submit(event) {
    event.preventDefault();
    if (sending.current) return;
    const values = source.trim().split(/[,\s]+/).map(Number);
    if (!source.trim() || !values.every(value => Number.isSafeInteger(value) && value > 0) || (kind === 'Session' && values.length !== 1)) {
      setError('Nhập ID đơn hợp lệ, hoặc đúng một ID phiên đã kết thúc.'); return;
    }
    sending.current = true; setBusy(true); setError('');
    try {
      const body = { kind, orderIds: kind === 'Orders' ? [...new Set(values)].sort((a, b) => a - b) : [], gamingSessionId: kind === 'Session' ? values[0] : null };
      const saved = await issueInvoice(body, key.current);
      onSaved(saved);
    } catch (failure) {
      const unknown = failure.status === 0 || failure.status >= 500;
      setUncertain(unknown);
      setError(unknown ? 'Chưa xác nhận được kết quả. Bấm Thử lại để tra cùng yêu cầu, tránh phát hành hai lần.' : failure.message);
    } finally { sending.current = false; setBusy(false); }
  }
  return <section className="sql-invoice-issue" aria-label="Phát hành hóa đơn"><h2>Phát hành từ nguồn đã ghi nhận</h2><p>Chỉ liên kết khoản đã thu và lưu chứng từ. Thao tác này không thu thêm tiền.</p><form onSubmit={submit}>
    <label>Loại hóa đơn<select value={kind} disabled={busy || uncertain} onChange={event => { setKind(event.target.value); setSource(''); }}><option value="Orders">Đơn món ăn</option><option value="Session">Tiền giờ của phiên đã kết thúc</option></select></label>
    <label>{kind === 'Orders' ? 'ID đơn món (ngăn cách bằng dấu phẩy)' : 'ID phiên'}<input value={source} disabled={busy || uncertain} onChange={event => setSource(event.target.value)} required placeholder={kind === 'Orders' ? 'Ví dụ: 12, 18' : 'Ví dụ: 7'}/></label>
    {error && <p className="cafe-error" role="alert">{error}</p>}
    <div className="sql-invoice-actions"><button type="button" className="cafe-button" disabled={busy} onClick={onClose}>Đóng</button><button className="cafe-button primary" disabled={busy}>{busy ? 'Đang phát hành…' : uncertain ? 'Thử lại' : 'Phát hành hóa đơn'}</button></div>
  </form></section>;
}

export default function InvoiceWorkspace({ admin = false }) {
  const { principal } = useAuth();
  const identity = `${principal?.type}:${principal?.employeeId ?? principal?.customerId ?? ''}`;
  const canIssue = admin && principal?.permissions?.includes('invoice.create');
  const [list, setList] = useState({ loading: true, rows: [], error: '' });
  const [selected, setSelected] = useState(null);
  const [detail, setDetail] = useState({ loading: false, invoice: null, error: '' });
  const [refresh, setRefresh] = useState(0);
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('all');
  const [page, setPage] = useState(1);
  const [issuing, setIssuing] = useState(false);
  const [notice, setNotice] = useState('');
  useEffect(() => {
    const controller = new AbortController(); let current = true;
    setList({ loading: true, rows: [], error: '' });
    listInvoices(admin, controller.signal).then(rows => { if (current) setList({ loading: false, rows, error: '' }); })
      .catch(error => { if (current && error.name !== 'AbortError') setList({ loading: false, rows: [], error: error.message }); });
    return () => { current = false; controller.abort(); };
  }, [admin, identity, refresh]);
  useEffect(() => { setSelected(null); setIssuing(false); setNotice(''); }, [identity]);
  useEffect(() => {
    if (!selected) { setDetail({ loading: false, invoice: null, error: '' }); return undefined; }
    const controller = new AbortController(); let current = true;
    setDetail({ loading: true, invoice: null, error: '' });
    getInvoice(admin, selected, controller.signal).then(invoice => { if (current) setDetail({ loading: false, invoice, error: '' }); })
      .catch(error => { if (current && error.name !== 'AbortError') setDetail({ loading: false, invoice: null, error: error.message }); });
    return () => { current = false; controller.abort(); };
  }, [selected, admin, identity, refresh]);
  const rows = list.rows.filter(row => (status === 'all' || row.status === status)
    && `${row.id} ${row.customerName} ${row.orderIds.join(' ')} ${row.gamingSessionId ?? ''}`.toLocaleLowerCase('vi').includes(query.trim().toLocaleLowerCase('vi')));
  const pages = Math.max(1, Math.ceil(rows.length / 10)); const activePage = Math.min(page, pages);
  const visible = rows.slice((activePage - 1) * 10, activePage * 10);
  function reload() { setRefresh(value => value + 1); }
  return <div className="invoice-workspace">
    <div className="sql-invoice-tools"><label className="sql-invoice-search"><Search size={17}/><span className="home-sr-only">Tìm hóa đơn</span><input placeholder="Tìm mã hóa đơn, khách hàng, đơn hoặc phiên…" value={query} onChange={event => { setQuery(event.target.value); setPage(1); }}/></label>
      <label><span className="home-sr-only">Trạng thái hóa đơn</span><select aria-label="Trạng thái hóa đơn" value={status} onChange={event => { setStatus(event.target.value); setPage(1); }}><option value="all">Tất cả trạng thái</option><option value="Pending">Còn phải thu</option><option value="Paid">Đã thanh toán</option><option value="Cancelled">Đã hủy</option></select></label>
      <button className="cafe-button" onClick={reload} disabled={list.loading}><RefreshCw size={16}/>Tải lại</button>
      {canIssue && <button className="cafe-button primary" onClick={() => setIssuing(value => !value)}><Plus size={16}/>Phát hành</button>}
    </div>
    {notice && <p className="sql-invoice-notice" role="status">{notice}</p>}
    {issuing && canIssue && <IssueForm onClose={() => setIssuing(false)} onSaved={invoice => { setIssuing(false); setNotice(`Đã phát hành hóa đơn #${invoice.id}.`); reload(); setSelected(invoice.id); }}/ >}
    {list.loading ? <p role="status" className="sql-invoice-empty">Đang tải hóa đơn…</p> : list.error ? <div className="sql-invoice-empty"><p className="cafe-error" role="alert">{list.error}</p><button className="cafe-button" onClick={reload}>Thử lại</button></div>
      : rows.length === 0 ? <div className="sql-invoice-empty"><ReceiptText size={28}/><h2>{list.rows.length ? 'Không có hóa đơn phù hợp' : 'Chưa có hóa đơn được phát hành'}</h2><p>{list.rows.length ? 'Thay đổi từ khóa hoặc trạng thái để tìm lại.' : 'Hóa đơn sẽ xuất hiện sau khi nhân viên phát hành chứng từ.'}</p></div>
        : <><p className="sql-invoice-count" role="status">{rows.length} hóa đơn{list.rows.length === (admin ? 500 : 200) ? ' gần nhất' : ''}</p>
          <div className="sql-invoice-table-wrap" role="region" aria-label="Danh sách hóa đơn" tabIndex={0}><table className="sql-invoice-table"><thead><tr><th>Hóa đơn / Nguồn</th>{admin && <th>Khách hàng</th>}<th>Sau giảm</th><th>Đã thu ròng / Hoàn</th><th>Còn phải thu</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>{visible.map(row => <tr key={row.id}><td><strong>#{row.id}</strong><small>{date(row.issuedAt)}</small><small>{row.orderIds.length ? `Đơn ${row.orderIds.join(', ')}` : `Phiên ${row.gamingSessionId}`}</small></td>{admin && <td>{row.customerName}<small>KH{row.customerId}</small></td>}<td>{money(row.grossAmount)}{row.cancelledAmount > 0 && <small>Hủy: {money(row.cancelledAmount)}</small>}</td><td>{money(row.paidAmount)}<small>Đã hoàn: {money(row.refundAmount)}</small></td><td>{money(row.amountDue)}{row.overpaidAmount > 0 && <small className="sql-invoice-warning">Cần đối soát: {money(row.overpaidAmount)}</small>}</td><td><span className={`sql-invoice-status ${row.status}`}>{statusLabel(row.status)}</span></td><td><button className="cafe-button" onClick={() => setSelected(row.id)} aria-label={`Xem và in hóa đơn ${row.id}`}>Chi tiết / In</button></td></tr>)}</tbody></table></div>
          <nav className="sql-invoice-pagination" aria-label="Trang hóa đơn"><button className="cafe-button" disabled={activePage === 1} onClick={() => setPage(activePage - 1)}>Trang trước</button><span>{activePage} / {pages}</span><button className="cafe-button" disabled={activePage === pages} onClick={() => setPage(activePage + 1)}>Trang sau</button></nav></>}
    {selected && <Dialog title={`Hóa đơn #${selected}`} wide className="sql-invoice-dialog" onClose={() => setSelected(null)}>{detail.loading ? <p role="status">Đang tải chi tiết…</p> : detail.error ? <div><p role="alert" className="cafe-error">{detail.error}</p><button className="cafe-button" onClick={reload}>Thử lại</button></div> : detail.invoice && <><Snapshot invoice={detail.invoice}/><div className="sql-invoice-actions"><button className="cafe-button primary" onClick={() => window.print()}><Printer size={16}/>In hóa đơn</button></div></>}</Dialog>}
    {detail.invoice && selected && createPortal(<div className="invoice-print-root"><Snapshot invoice={detail.invoice}/></div>, document.body)}
  </div>;
}
