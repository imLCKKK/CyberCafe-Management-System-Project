import React, { useCallback, useRef, useState } from 'react';
import { Banknote, Plus } from 'lucide-react';
import { useAuth } from '../../shared/auth/AuthContext';
import { CounterTopUpAmounts } from '../../shared/CounterTopUpAmounts';
import { beginCounterTopUpIntent, clearCounterTopUpIntent, createCounterTopUp, getCounterTopUpOptions, getOwnCounterTopUps, readCounterTopUpIntent } from '../../shared/counterTopUpApi.js';
import { customerDate, walletMoney } from '../../shared/customerWalletApi.js';
import { useCustomerQuery } from '../../shared/useCustomerQuery.js';
import { useRealtime } from '../../shared/realtime/useRealtime';

const events = ['CounterTopUpChanged'];
function initialIntent(customerId) {
  try { return { intent: readCounterTopUpIntent(customerId), error: '' }; }
  catch (error) { return { intent: null, error: error.message }; }
}

export function CounterTopUpPanel({ customerId, onChanged }) {
  const { accessToken } = useAuth();
  const [initial] = useState(() => initialIntent(customerId));
  const [intent, setIntent] = useState(initial.intent);
  const [amount, setAmount] = useState(initial.intent?.amount || 50000);
  const [error, setError] = useState(initial.error);
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);
  const guard = useRef(false);
  const load = useCallback(async signal => {
    const [options, requests] = await Promise.all([getCounterTopUpOptions(signal), getOwnCounterTopUps({ customerId, signal })]);
    return { options, requests };
  }, [customerId]);
  const query = useCustomerQuery(`counter-topups:${customerId}`, load);
  const connection = useRealtime({ enabled: true, accessToken, events, onEvent: query.reload, onReady: query.reload, onAuthFailure: query.reload });
  const pending = query.data?.requests.items.find(row => row.status === 'Pending');
  const selected = query.data?.options.find(row => row.amount === amount);

  async function submit(event) {
    event.preventDefault();
    if (guard.current || !query.data || (!intent && pending) || initial.error) return;
    guard.current = true;
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const savedIntent = beginCounterTopUpIntent(customerId, amount);
      setIntent(savedIntent);
      setAmount(savedIntent.amount);
      const saved = await createCounterTopUp({ customerId, ...savedIntent });
      clearCounterTopUpIntent(customerId, savedIntent.idempotencyKey);
      setIntent(null);
      setNotice(saved.status === 'Approved'
        ? `Yêu cầu #${saved.requestId} đã được duyệt. Số dư được đọc lại từ máy chủ.`
        : `Đã gửi yêu cầu #${saved.requestId}. Mang đúng ${walletMoney(saved.amount)} đến quầy; ví chỉ được cộng sau khi nhân viên đã thu tiền.`);
      query.reload();
      onChanged?.();
    } catch (failure) {
      setError(failure.message);
      if ([400, 409].includes(failure.status)) query.reload();
    } finally {
      guard.current = false;
      setBusy(false);
    }
  }
  return <section className="home-panel wallet-topup" aria-labelledby="counter-topup-heading">
    <div className="client-panel-heading"><h2 id="counter-topup-heading"><Banknote size={19}/> Nạp tiền tại quầy</h2>
      <button className="home-button" disabled={query.loading || busy} onClick={query.reload}>Tải lại yêu cầu</button></div>
    <div className="counter-topup-body">
      <p className="counter-topup-note">Chọn mức nạp rồi gửi yêu cầu. Nhân viên thu đủ tiền mặt trước khi xác nhận cộng ví và tiền tặng. Mỗi khách chỉ có một yêu cầu đang chờ.</p>
      {connection !== 'connected' && <p className="counter-topup-status" role="status">Đồng bộ trực tiếp: {connection === 'connecting' ? 'đang kết nối' : 'chưa kết nối'}. Có thể bấm tải lại yêu cầu.</p>}
      {query.loading ? <p role="status">Đang tải mức nạp và yêu cầu…</p>
        : query.error ? <><p className="home-alert" role="alert">{query.error.message}</p><button className="home-button" onClick={query.reload}>Thử tải lại</button></>
          : query.data && <>
            {pending && <div className="home-info-box" role="status"><div><strong>Yêu cầu #{pending.requestId} đang chờ thu tiền mặt</strong><CounterTopUpAmounts request={pending}/><p>Chưa cộng vào số dư ví.</p></div></div>}
            {intent && <p className="home-info-box">Đang giữ yêu cầu {walletMoney(intent.amount)} để gửi lại cùng khóa. Nếu lần trước chưa nhận được phản hồi, bấm kiểm tra/gửi lại để lấy đúng kết quả cũ.</p>}
            <form onSubmit={submit}>
              <fieldset disabled={busy || Boolean(intent) || Boolean(pending) || Boolean(initial.error)} className="wallet-methods">
                <legend>Số tiền mặt muốn nạp</legend>
                <div className="counter-topup-options">{query.data.options.map(option => <button key={option.amount} className="home-button" type="button" aria-pressed={amount === option.amount} onClick={() => setAmount(option.amount)}>
                  <strong>{walletMoney(option.amount)}</strong><small>Tặng {walletMoney(option.bonusAmount)}</small>
                </button>)}</div>
              </fieldset>
              {selected && <CounterTopUpAmounts request={selected}/>}
              <button className="home-button primary" type="submit" disabled={busy || (!intent && Boolean(pending)) || Boolean(initial.error)}><Plus size={16}/>{busy ? 'Đang gửi…' : intent ? 'Kiểm tra / gửi lại yêu cầu cũ' : 'Gửi yêu cầu nạp tiền'}</button>
            </form>
            {notice && <p className="home-info-box" role="status">{notice}</p>}
            <h3>Yêu cầu nạp gần đây</h3>
            {!query.data.requests.items.length ? <p className="counter-topup-note">Chưa có yêu cầu nạp tại quầy.</p>
              : <ul className="counter-topup-list">{query.data.requests.items.map(row => <li key={row.requestId}>
                <div className="counter-topup-list-heading"><strong>#{row.requestId} · {row.status === 'Approved' ? 'Đã thu tiền và cộng ví' : 'Chờ thu tiền tại quầy'}</strong><span>{customerDate(row.createdAtUtc)}</span></div>
                <CounterTopUpAmounts request={row}/>
                {row.status === 'Approved' && <p className="counter-topup-note">Giao dịch ví #{row.transactionId} · Duyệt lúc {customerDate(row.approvedAtUtc)} · Số dư sau {walletMoney(row.balanceAfter)}</p>}
              </li>)}</ul>}
            {query.data.requests.totalCount > query.data.requests.items.length && <p className="counter-topup-note">Hiển thị {query.data.requests.items.length} yêu cầu mới nhất. Các khoản đã duyệt có trong lịch sử ví bên dưới.</p>}
          </>}
      {error && <p className="home-alert" role="alert">{error}</p>}
    </div>
  </section>;
}
