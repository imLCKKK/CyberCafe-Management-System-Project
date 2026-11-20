import { useCallback, useEffect, useRef, useState } from 'react';
import { useAuth } from '../../shared/auth/AuthContext';
import { getAccessToken } from '../../shared/auth/api';
import { getSessionHome } from '../../shared/sessionApi';
import { getCustomerWallet } from '../../shared/customerWalletApi';
import { listInvoices } from '../../shared/invoices/invoiceApi';
import { useRealtime } from '../../shared/realtime/useRealtime';

const empty = { session: null, walletBalance: null, transactions: [], invoices: [], loading: true, error: '' };
const events = ['SessionChanged', 'WalletChanged', 'CounterTopUpChanged', 'InvoiceChanged'];

export function useSessionHome() {
  const { accessToken, principal, loading: authLoading } = useAuth();
  const [state, setState] = useState(empty);
  const refresh = useRef(null);
  const stateToken = useRef(accessToken);
  const reload = useCallback(() => refresh.current?.(), []);
  useEffect(() => {
    const controller = new AbortController();
    const active = () => !controller.signal.aborted && getAccessToken() === accessToken;
    let running = false;
    let queued = false;
    stateToken.current = accessToken;
    setState(empty);
    async function load() {
      if (!active()) return;
      if (running) { queued = true; return; }
      running = true;
      try {
        if (principal?.type !== 'customer') throw new Error('Vui lòng đăng nhập khách hàng để xem phiên.');
        const [home, wallet, invoices] = await Promise.all([
          getSessionHome(controller.signal), getCustomerWallet({ pageSize: 20, signal: controller.signal }),
          listInvoices(false, controller.signal),
        ]);
        if (active()) setState({ ...home, transactions: wallet.transactions, invoices, loading: false, error: '' });
      } catch (error) {
        if (active()) setState({ ...empty, loading: false, error: error.message });
      } finally {
        running = false;
        if (queued && active()) { queued = false; void load(); }
      }
    }
    refresh.current = load;
    if (!authLoading) void load();
    const timer = setInterval(load, 30000);
    return () => { controller.abort(); clearInterval(timer); refresh.current = null; };
  }, [accessToken, principal, authLoading]);
  useRealtime({ enabled: principal?.type === 'customer', accessToken, events, onEvent: reload, onReady: reload });
  return { ...(stateToken.current === accessToken ? state : empty), reload };
}
