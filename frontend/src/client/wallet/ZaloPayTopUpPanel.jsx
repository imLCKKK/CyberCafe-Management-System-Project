import React, { useCallback, useEffect, useRef, useState } from 'react';
import { QrCode, Plus } from 'lucide-react';
import { useAuth } from '../../shared/auth/AuthContext';
import { walletMoney } from '../../shared/customerWalletApi.js';
import { useRealtime } from '../../shared/realtime/useRealtime';
import { beginZaloPayTopUpIntent, clearZaloPayTopUpIntent, createZaloPayTopUp, getOwnZaloPayPayments,
  getZaloPayConfig, paymentIsApplied, paymentNeedsReview, paymentShouldPoll, readZaloPayTopUpIntent, syncZaloPayPayment } from '../../shared/zaloPayPaymentApi.js';
import { ZaloPayPaymentCard } from './ZaloPayPaymentCard';
import './zalopay-payments.css';

const events = ['ZaloPayPaymentChanged'];
const presets = [10000, 20000, 30000, 50000, 100000];
function initialIntent(customerId) {
  try { return { intent: readZaloPayTopUpIntent(customerId), error: '' }; }
  catch (error) { return { intent: null, error: error.message }; }
}
const aborted = () => new DOMException('Yêu cầu đã kết thúc.', 'AbortError');
function keepCommitted(previous, next) {
  if (paymentIsApplied(previous) && !paymentIsApplied(next)) return previous;
  if (previous?.status === 'Succeeded' && next.status !== 'Succeeded') return previous;
  return next;
}
function mergePage(previous, incoming) {
  return { ...incoming, items: incoming.items.map(row => keepCommitted(previous?.items.find(item => item.id === row.id), row)) };
}

export function ZaloPayTopUpPanel({ customerId, onChanged }) {
  const { accessToken } = useAuth();
  const [initial] = useState(() => initialIntent(customerId));
  const [intent, setIntent] = useState(initial.intent);
  const [amountText, setAmountText] = useState(String(initial.intent?.amountVnd ?? 50000));
  const [snapshot, setSnapshot] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [error, setError] = useState(initial.error);
  const [busy, setBusy] = useState(false);
  const [syncing, setSyncing] = useState([]);
  const [syncErrors, setSyncErrors] = useState({});
  const [now, setNow] = useState(Date.now());
  const alive = useRef(false);
  const token = useRef(accessToken);
  token.current = accessToken;
  const controllers = useRef(new Set());
  const listRequest = useRef(null);
  const loadSequence = useRef(0);
  const createGuard = useRef(false);
  const syncGuards = useRef(new Set());
  const pollStarted = useRef(new Map());
  const pollFailures = useRef(new Map());

  const run = useCallback(async action => {
    const controller = new AbortController();
    controllers.current.add(controller);
    try {
      const result = await action(controller.signal, controller);
      if (!alive.current || token.current !== accessToken || controller.signal.aborted) throw aborted();
      return result;
    } finally { controllers.current.delete(controller); }
  }, [accessToken]);

  const reload = useCallback(async () => {
    const sequence = ++loadSequence.current;
    listRequest.current?.abort();
    setLoading(true);
    setLoadError('');
    try {
      const result = await run(async (signal, controller) => {
        listRequest.current = controller;
        const [config, topups, orders] = await Promise.all([getZaloPayConfig(signal),
          getOwnZaloPayPayments({ customerId, purpose: 'TopUp', signal }),
          getOwnZaloPayPayments({ customerId, purpose: 'Order', signal })]);
        return { config, topups, orders };
      });
      if (sequence !== loadSequence.current) return;
      setSnapshot(previous => ({ ...result, topups: mergePage(previous?.topups, result.topups), orders: mergePage(previous?.orders, result.orders) }));
    } catch (failure) {
      if (failure.name !== 'AbortError' && sequence === loadSequence.current) setLoadError(failure.message);
    } finally {
      if (alive.current && token.current === accessToken && sequence === loadSequence.current) setLoading(false);
    }
  }, [customerId, accessToken, run]);

  useEffect(() => {
    alive.current = true;
    reload();
    const clock = setInterval(() => setNow(Date.now()), 1000);
    return () => {
      alive.current = false;
      loadSequence.current++;
      for (const controller of controllers.current) controller.abort();
      clearInterval(clock);
    };
  }, [reload]);

  const connection = useRealtime({ enabled: true, accessToken, events, onEvent: reload, onReady: reload, onAuthFailure: reload });

  const acceptPayment = useCallback(payment => {
    setSnapshot(previous => {
      if (!previous) return previous;
      const key = payment.purpose === 'TopUp' ? 'topups' : 'orders';
      const old = previous[key];
      const existing = old.items.find(row => row.id === payment.id);
      const items = existing ? old.items.map(row => row.id === payment.id ? keepCommitted(row, payment) : row)
        : [payment, ...old.items].slice(0, old.pageSize);
      return { ...previous, [key]: { ...old, items, totalCount: old.totalCount + (existing ? 0 : 1) } };
    });
  }, []);

  const sync = useCallback(async (payment, automatic = false) => {
    if (syncGuards.current.has(payment.id)) return;
    syncGuards.current.add(payment.id);
    setSyncing([...syncGuards.current]);
    if (!automatic) { setSyncErrors(previous => ({ ...previous, [payment.id]: '' })); pollFailures.current.delete(payment.id); pollStarted.current.set(payment.id, Date.now()); }
    try {
      const result = await run(signal => syncZaloPayPayment({ customerId, paymentId: payment.id, signal }));
      acceptPayment(result);
      setSyncErrors(previous => ({ ...previous, [payment.id]: '' }));
      pollFailures.current.delete(payment.id);
      if (result.purpose === 'TopUp' && paymentIsApplied(result) && !paymentIsApplied(payment)) onChanged?.();
    } catch (failure) {
      if (failure.name !== 'AbortError') {
        pollFailures.current.set(payment.id, (pollFailures.current.get(payment.id) ?? 0) + 1);
        setSyncErrors(previous => ({ ...previous, [payment.id]: `${failure.message} Chưa xác nhận kết quả; có thể bấm kiểm tra lại, không cần tạo lượt nạp mới.` }));
      }
    } finally {
      syncGuards.current.delete(payment.id);
      if (alive.current && token.current === accessToken) setSyncing([...syncGuards.current]);
    }
  }, [accessToken, customerId, run, acceptPayment, onChanged]);

  useEffect(() => {
    if (!snapshot) return;
    const payments = [...snapshot.topups.items, ...snapshot.orders.items];
    for (const payment of payments) if (!pollStarted.current.has(payment.id)) pollStarted.current.set(payment.id, Date.now());
    const candidates = payments.filter(payment => (pollFailures.current.get(payment.id) ?? 0) < 3
      && paymentShouldPoll(payment, pollStarted.current.get(payment.id)));
    if (!candidates.length) return;
    const timer = setTimeout(() => { for (const payment of candidates) sync(payment, true); }, 2500);
    return () => clearTimeout(timer);
  }, [snapshot, sync, syncing]);

  const unresolvedTopUp = snapshot?.topups.items.find(payment => payment.status === 'Pending'
    || paymentNeedsReview(payment) || (payment.status === 'Succeeded' && !paymentIsApplied(payment)));
  const cannotCreate = !intent && Boolean(unresolvedTopUp);

  async function submit(event) {
    event.preventDefault();
    if (createGuard.current || !snapshot?.config.enabled || initial.error || loadError || cannotCreate) return;
    const amountVnd = Number(amountText);
    if (!intent && (!/^\d+$/.test(amountText) || !Number.isSafeInteger(amountVnd)
      || amountVnd < snapshot.config.minAmountVnd || amountVnd > snapshot.config.maxAmountVnd)) {
      setError(`Nhập số tiền nguyên từ ${walletMoney(snapshot.config.minAmountVnd)} đến ${walletMoney(snapshot.config.maxAmountVnd)}.`);
      return;
    }
    createGuard.current = true;
    setBusy(true);
    setError('');
    try {
      // Persist before sending, including before any response/timeout; every retry keeps this key and amount.
      const savedIntent = beginZaloPayTopUpIntent(customerId, amountVnd);
      setIntent(savedIntent);
      setAmountText(String(savedIntent.amountVnd));
      const result = await run(signal => createZaloPayTopUp({ customerId, ...savedIntent, signal }));
      acceptPayment(result);
      clearZaloPayTopUpIntent(customerId, savedIntent.idempotencyKey);
      setIntent(null);
      if (paymentIsApplied(result)) onChanged?.();
      reload();
    } catch (failure) {
      if (failure.name !== 'AbortError') setError(`${failure.message} Yêu cầu đã gửi sẽ được giữ để kiểm tra hoặc gửi lại cùng khóa.`);
    } finally {
      createGuard.current = false;
      if (alive.current && token.current === accessToken) setBusy(false);
    }
  }

  return <section className="home-panel zalopay-topup" aria-labelledby="zalopay-topup-heading">
    <div className="client-panel-heading"><h2 id="zalopay-topup-heading"><QrCode size={19}/> Nạp bằng ZaloPay Sandbox</h2>
      <button type="button" className="home-button" disabled={loading || busy} onClick={reload}>Tải lại giao dịch</button></div>
    <div className="zalopay-topup-body">
      <p className="counter-topup-note">Chọn số tiền rồi quét mã bằng app ZaloPay Sandbox. Lượt nạp này cộng đúng số tiền thanh toán, không kèm tiền tặng tại quầy.</p>
      {connection !== 'connected' && <p className="counter-topup-status" role="status">Đồng bộ trực tiếp: {connection === 'connecting' ? 'đang kết nối' : 'chưa kết nối'}. Có thể tải lại hoặc kiểm tra trạng thái.</p>}
      {loading && <p role="status">Đang tải thanh toán ZaloPay…</p>}
      {loadError && <p className="home-alert" role="alert">{loadError}</p>}
      {snapshot && <>
        {!snapshot.config.enabled && <p className="home-info-box" role="status">Nạp ZaloPay Sandbox hiện chưa sẵn sàng. Bạn có thể dùng nạp tiền tại quầy.</p>}
        {intent && <p className="home-info-box" role="status">Đang giữ yêu cầu nạp {walletMoney(intent.amountVnd)}. Nếu lần trước mất kết nối, bấm kiểm tra / gửi lại để lấy cùng giao dịch.</p>}
        {cannotCreate && <p className="home-info-box" role="status">Lượt nạp #{unresolvedTopUp.id} chưa hoàn tất. Hãy dùng mã hoặc kiểm tra trạng thái bên dưới trước khi tạo lượt nạp tiếp.</p>}
        <form onSubmit={submit}>
          <fieldset className="zalopay-topup-amounts" disabled={busy || Boolean(intent) || cannotCreate || !snapshot.config.enabled || Boolean(initial.error) || Boolean(loadError)}>
            <legend>Số tiền muốn nạp</legend><div className="zalopay-topup-presets">{presets.filter(amount => amount >= snapshot.config.minAmountVnd && amount <= snapshot.config.maxAmountVnd).map(amount =>
              <button type="button" className="home-button" key={amount} aria-pressed={Number(amountText) === amount} onClick={() => setAmountText(String(amount))}>{walletMoney(amount)}</button>)}</div>
            <label className="zalopay-topup-custom">Số tiền khác (đồng)<input type="text" inputMode="numeric" pattern="[0-9]+" maxLength={9} value={amountText} onChange={event => setAmountText(event.target.value)}/></label>
          </fieldset>
          <button className="home-button primary" type="submit" disabled={busy || cannotCreate || !snapshot.config.enabled || Boolean(initial.error) || Boolean(loadError)}><Plus size={16}/>{busy ? 'Đang gửi…' : intent ? 'Kiểm tra / gửi lại yêu cầu cũ' : 'Tạo mã nạp ZaloPay Sandbox'}</button>
        </form>
        {error && <p className="home-alert" role="alert">{error}</p>}
        <h3>Lượt nạp ZaloPay gần đây</h3>
        {!snapshot.topups.items.length ? <p className="counter-topup-note">Chưa có lượt nạp ZaloPay.</p>
          : <div className="zalopay-payment-list">{snapshot.topups.items.map(payment => <ZaloPayPaymentCard key={payment.id} payment={payment} enabled={snapshot.config.enabled} now={now} syncing={syncing.includes(payment.id)} syncError={syncErrors[payment.id]} onSync={sync}/>)}</div>}
        {snapshot.topups.totalCount > snapshot.topups.items.length && <p className="counter-topup-note">Hiển thị {snapshot.topups.items.length} lượt nạp mới nhất. Các khoản đã cộng có trong lịch sử ví.</p>}
        {snapshot.orders.items.length > 0 && <><h3>Thanh toán đơn bằng ZaloPay</h3><p className="counter-topup-note">Các giao dịch thanh toán đơn đã được tạo cho tài khoản của bạn.</p>
          <div className="zalopay-payment-list">{snapshot.orders.items.map(payment => <ZaloPayPaymentCard key={payment.id} payment={payment} enabled={snapshot.config.enabled} now={now} syncing={syncing.includes(payment.id)} syncError={syncErrors[payment.id]} onSync={sync}/>)}</div></>}
      </>}
      {!snapshot && error && <p className="home-alert" role="alert">{error}</p>}
    </div>
  </section>;
}
