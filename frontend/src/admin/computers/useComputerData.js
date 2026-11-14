import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../../shared/apiClient';
import { useAuth } from '../../shared/auth/AuthContext';
import { expireSession, getAccessToken } from '../../shared/auth/api';
import { useRealtime } from '../../shared/realtime/useRealtime';
import { getComputerData } from './computerApi';

const empty = { computers: [], rooms: [], types: [], permissions: [] };
const initial = { ...empty, loading: true, refreshing: false, error: null };
const computerEvents = ['ComputerChanged'];

export function useComputerData() {
  const { principal, loading: authLoading, accessToken: token } = useAuth();
  const [state, setState] = useState(initial);
  const stateToken = useRef(token);
  const refresh = useRef(null);
  const markPending = useRef(null);
  const revision = useRef(0);
  const reload = useCallback(() => refresh.current?.(), []);

  useEffect(() => {
    const controller = new AbortController();
    let running = false;
    let queued = false;
    let hasData = false;
    const active = () => !controller.signal.aborted && getAccessToken() === token;
    stateToken.current = token;
    setState(initial);
    if (authLoading) return () => controller.abort();

    // Serialize refetches and keep one trailing read so event bursts cannot starve a request.
    function request() {
      if (!active()) return;
      queued = true;
      if (!running) void load();
    }
    async function load() {
      running = true;
      queued = false;
      const readRevision = revision.current;
      setState((current) => ({ ...current, loading: !hasData, refreshing: hasData, error: null }));
      try {
        if (!token || principal?.type !== 'employee') throw new ApiError('Vui lòng đăng nhập nhân viên để xem danh sách máy.', 401);
        if (!principal.permissions.includes('computer.view')) throw new ApiError('Bạn không có quyền xem máy tính.', 403);
        const data = await getComputerData(controller.signal);
        if (!active()) return;
        if (readRevision !== revision.current) { queued = true; return; }
        hasData = true;
        setState({ ...data, permissions: principal.permissions, loading: false, refreshing: false, error: null });
      } catch (error) {
        if (active()) {
          const denied = [401, 403].includes(error.status);
          if (denied) hasData = false;
          setState((current) => ({ ...(denied ? empty : current), loading: false, refreshing: false, error }));
        }
      } finally {
        running = false;
        if (queued && active()) void load();
      }
    }
    const onMutation = () => { if (running) queued = true; };
    refresh.current = request;
    markPending.current = onMutation;
    request();
    return () => {
      controller.abort();
      if (refresh.current === request) refresh.current = null;
      if (markPending.current === onMutation) markPending.current = null;
    };
  }, [token, principal, authLoading]);

  const current = stateToken.current === token ? state : initial;
  const realtimeStatus = useRealtime({
    enabled: current.permissions.includes('computer.view'), accessToken: token, events: computerEvents,
    onEvent: reload, onReady: reload,
    onAuthFailure: (status) => {
      if (getAccessToken() !== token) return;
      if (status === 401) expireSession({ accessToken: token, accountType: 'employee' });
      else reload();
    },
  });
  const applyComputer = useCallback((computer) => {
    revision.current++;
    markPending.current?.();
    setState((value) => ({ ...value,
      computers: value.computers.map((row) => row.ComputerId === computer.ComputerId ? computer : row),
    }));
  }, []);

  return { ...current, realtimeStatus, reload, applyComputer };
}
