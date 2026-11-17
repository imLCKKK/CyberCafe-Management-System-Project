import React, { useEffect, useRef, useState } from 'react';
import { getSessions, openSession, closeSession } from '../../shared/sessionApi';
import { getCustomers } from '../../shared/customerWalletApi';
import { getAccessToken } from '../../shared/auth/api';

export function SessionOperations({ computer, permissions, onSaved }) {
  const [session, setSession] = useState(null);
  const [customers, setCustomers] = useState([]);
  const [customerId, setCustomerId] = useState('');
  const [search, setSearch] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const pending = useRef(null);
  const key = useRef('');
  useEffect(() => { key.current = `session-${Date.now()}-${Math.random().toString(36).slice(2)}`; }, [computer.ComputerId, customerId]);
  useEffect(() => () => pending.current?.abort(), []);
  useEffect(() => {
    const controller = new AbortController();
    const token = getAccessToken();
    setLoading(true); setSession(null); setError('');
    if (!permissions.includes('session.view')) { setLoading(false); return () => controller.abort(); }
    getSessions(controller.signal).then(rows => {
      if (!controller.signal.aborted && getAccessToken() === token)
        setSession(rows.find(row => row.computerId === computer.ComputerId && row.status === 'Active') || null);
    }).catch(failure => { if (!controller.signal.aborted) setError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [computer, permissions]);
  useEffect(() => {
    if (!permissions.includes('session.start') || !permissions.includes('customer.view')) return undefined;
    const controller = new AbortController();
    const token = getAccessToken();
    const timer = setTimeout(() => getCustomers({ search, pageSize: 20, signal: controller.signal })
      .then(data => { if (!controller.signal.aborted && getAccessToken() === token) setCustomers(data.items); })
      .catch(failure => { if (!controller.signal.aborted) setError(failure.message); }), 200);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [search, permissions]);
  async function run(work) {
    if (pending.current) return;
    const controller = new AbortController();
    const token = getAccessToken();
    pending.current = controller; setBusy(true); setError('');
    try {
      await work(controller.signal);
      if (!controller.signal.aborted && getAccessToken() === token) onSaved('Đã cập nhật phiên.');
    } catch (failure) { if (!controller.signal.aborted && getAccessToken() === token) setError(failure.message); }
    finally { pending.current = null; if (!controller.signal.aborted) setBusy(false); }
  }
  if (!permissions.includes('session.view') && !permissions.includes('session.start')) return null;
  const idle = computer.Status === 'Available' && !computer.HasOpenSession && !computer.HasOpenMaintenance;
  return <section className="computer-management maintenance-panel" aria-label="Phiên chơi">
    <h3>Phiên chơi</h3>
    {loading && <p role="status">Đang tải phiên…</p>}
    {error && <p className="cafe-error" role="alert">{error}</p>}
    {idle && permissions.includes('session.start') && <form onSubmit={event => {
      event.preventDefault(); void run(signal => openSession(computer.ComputerId, Number(customerId), key.current, signal));
    }}>
      <label>Tìm khách<input value={search} onChange={event => setSearch(event.target.value)} placeholder="Tên hoặc số điện thoại"/></label>
      <label>Khách hàng<select required value={customerId} onChange={event => setCustomerId(event.target.value)}>
        <option value="">Chọn khách</option>{customers.filter(row => row.status === 'Active').map(row =>
          <option key={row.customerId} value={row.customerId}>{row.fullName} · {row.phone}</option>)}</select></label>
      <button className="cafe-button primary" disabled={loading || busy || !customerId}>Mở phiên</button>
    </form>}
    {session && <p>Phiên #{session.gamingSessionId} · Khách #{session.customerId} · Đã thu {session.totalCost.toLocaleString('vi-VN')} đ</p>}
    {session && permissions.includes('session.end') && permissions.includes('invoice.create') &&
      <button className="cafe-button primary" disabled={busy}
        onClick={() => void run(signal => closeSession(session.gamingSessionId, key.current, signal))}>Đóng phiên và xuất hóa đơn</button>}
  </section>;
}
