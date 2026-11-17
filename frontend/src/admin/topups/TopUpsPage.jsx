import React, { useCallback, useRef, useState } from 'react';
import { Banknote } from 'lucide-react';
import { PageHeading } from '../computers/components/CafeUI';
import { DetailDialog, Notice, Panel, Status } from '../shared/AdminUI';
import { CounterTopUpAmounts } from '../../shared/CounterTopUpAmounts';
import { useAuth } from '../../shared/auth/AuthContext';
import { approveCounterTopUp, getAdminCounterTopUps } from '../../shared/counterTopUpApi.js';
import { customerDate, walletMoney } from '../../shared/customerWalletApi.js';
import { useCustomerQuery } from '../../shared/useCustomerQuery.js';
import { CustomerPagination } from '../../shared/WalletHistory';
import { useRealtime } from '../../shared/realtime/useRealtime';

const events = ['CounterTopUpChanged'];
export function ApproveDialog({ request, onClose, onApproved }) {
  const [confirmed, setConfirmed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const guard = useRef(false);
  return <DetailDialog title={`Thu tiền và duyệt yêu cầu #${request.requestId}`} onClose={busy ? () => {} : onClose}>
    <p><strong>{request.customerName}</strong> · {request.customerPhone} · KH{request.customerId}</p>
    <CounterTopUpAmounts request={request}/>
    <p className="counter-topup-note">Chỉ thu số tiền mặt đã ghi. Tiền tặng do hệ thống cộng thêm; khoản nạp này không được tính là doanh thu dịch vụ.</p>
    <form onSubmit={async event => {
      event.preventDefault();
      if (!confirmed || guard.current) return;
      guard.current = true;
      setBusy(true);
      setError('');
      try { const saved = await approveCounterTopUp(request, confirmed); onApproved(saved); }
      catch (failure) { setError(`${failure.message} Nếu đã nhận tiền, giữ nguyên yêu cầu #${request.requestId} và kiểm tra/gửi lại; không thu khách lần hai.`); }
      finally { guard.current = false; setBusy(false); }
    }}>
      <label className="counter-topup-confirm"><input type="checkbox" checked={confirmed} disabled={busy} onChange={event => setConfirmed(event.target.checked)}/><span>Tôi xác nhận đã nhận đủ <strong>{walletMoney(request.amount)} tiền mặt</strong> từ khách để cộng tổng <strong>{walletMoney(request.totalAmount)}</strong> vào ví.</span></label>
      {error && <p className="cafe-error" role="alert">{error}</p>}
      <div className="cafe-dialog-actions"><button className="cafe-button" type="button" disabled={busy} onClick={onClose}>Đóng</button><button className="cafe-button primary" type="submit" disabled={!confirmed || busy}>{busy ? 'Đang xác nhận…' : 'Đã thu đủ tiền · cộng ví'}</button></div>
    </form>
  </DetailDialog>;
}

export function TopUpsPage() {
  const { principal, accessToken } = useAuth();
  const canApprove = principal?.type === 'employee' && principal.permissions.includes('wallet.topup');
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('Pending');
  const [selected, setSelected] = useState(null);
  const [notice, setNotice] = useState('');
  const load = useCallback(signal => getAdminCounterTopUps({ page, status, signal }), [page, status]);
  const query = useCustomerQuery(`admin-topups:${principal?.employeeId}:${page}:${status}`, load, canApprove);
  const selectedRef = useRef(null);
  selectedRef.current = selected;
  const refresh = useCallback(() => {
    if (selectedRef.current) {
      setSelected(null);
      setNotice('Đã đóng phiếu thu đang mở để kiểm tra lại trạng thái. Chỉ thu tiền sau khi chọn lại yêu cầu còn chờ.');
    }
    query.reload();
  }, [query.reload]);
  const onTopUpChanged = useCallback((_eventName, message) => {
    if (selectedRef.current?.requestId === message.entityId) {
      setSelected(null);
      setNotice(`Yêu cầu #${message.entityId} vừa được cập nhật. Kiểm tra trạng thái trước khi thu tiền; không thu khách lần hai.`);
    }
    query.reload();
  }, [query.reload]);
  const connection = useRealtime({ enabled: canApprove, accessToken, events, onEvent: onTopUpChanged, onReady: refresh, onAuthFailure: refresh });
  return <section className="cafe-page admin-module">
    <PageHeading eyebrow="QUẢN TRỊ / NẠP TẠI QUẦY" title="Yêu cầu nạp tiền" description="Thu đủ tiền mặt, xác nhận và cộng cả tiền nạp lẫn tiền tặng vào ví khách.">
      <button className="cafe-button" disabled={query.loading} onClick={refresh}>Tải lại danh sách</button>
    </PageHeading>
    <Notice notice={notice} clear={() => setNotice('')}/>
    {!canApprove ? <p className="cafe-error" role="alert">Bạn không có quyền duyệt nạp tiền tại quầy.</p> : <Panel title="Yêu cầu nạp tại quầy" description="Số tiền do server tính và lưu cùng yêu cầu; duyệt lại không cộng tiền thêm.">
      <div className="admin-toolbar"><label>Trạng thái <select aria-label="Lọc yêu cầu nạp" value={status} onChange={event => { setStatus(event.target.value); setPage(1); }}><option value="Pending">Chờ thu tiền</option><option value="Approved">Đã thu và cộng ví</option><option value="All">Tất cả</option></select></label>
        <span className="counter-topup-status" role="status">{connection === 'connected' ? 'Đang đồng bộ trực tiếp' : 'Chưa kết nối trực tiếp · có thể tải lại'}</span></div>
      {query.loading ? <p className="cafe-empty" role="status">Đang tải yêu cầu nạp…</p>
        : query.error ? <div className="cafe-empty"><p className="cafe-error" role="alert">{query.error.message}</p><button className="cafe-button" onClick={refresh}>Thử lại</button></div>
          : query.data && <>
            <div className="cafe-table-wrap"><table className="cafe-table admin-table"><thead><tr><th>Yêu cầu / khách hàng</th><th>Tiền mặt cần thu</th><th>Tiền tặng</th><th>Tổng cộng ví</th><th>Trạng thái</th><th>Xử lý</th></tr></thead><tbody>{query.data.items.map(row => <tr key={row.requestId}>
              <td><strong>#{row.requestId} · {row.customerName}</strong><small>KH{row.customerId} · {row.customerPhone}</small><small>{customerDate(row.createdAtUtc)}</small></td>
              <td>{walletMoney(row.amount)}</td><td>{walletMoney(row.bonusAmount)}</td><td><strong>{walletMoney(row.totalAmount)}</strong></td>
              <td><Status value={row.status}>{row.status === 'Pending' ? 'Chờ thu tiền' : 'Đã cộng ví'}</Status>{row.status === 'Approved' && <small>GD{row.transactionId} · {customerDate(row.approvedAtUtc)}</small>}</td>
              <td>{row.status === 'Pending' ? <button className="cafe-button primary" onClick={() => setSelected(row)}><Banknote size={16}/>Thu tiền / duyệt</button> : <span>Sau nạp: {walletMoney(row.balanceAfter)}</span>}</td>
            </tr>)}</tbody></table></div>
            {!query.data.items.length && <div className="cafe-empty"><h3>Không có yêu cầu phù hợp</h3><p>Yêu cầu khách gửi sẽ xuất hiện ở đây.</p></div>}
            <CustomerPagination data={query.data} onPage={setPage} label="Trang yêu cầu"/>
          </>}
    </Panel>}
    {selected && canApprove && <ApproveDialog key={selected.requestId} request={selected} onClose={() => setSelected(null)} onApproved={saved => { setSelected(null); setNotice(`Yêu cầu #${saved.requestId} đã ghi nhận thu ${walletMoney(saved.amount)}, tặng ${walletMoney(saved.bonusAmount)}, cộng ví ${walletMoney(saved.totalAmount)} · GD${saved.transactionId}.`); query.reload(); }}/ >}
  </section>;
}
