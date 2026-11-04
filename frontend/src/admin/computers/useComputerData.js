import { useCallback, useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { ApiError, apiRequest } from '../../shared/apiClient';
import { clearStaffAccessToken, getStaffAccessToken, getStaffSessionReason, subscribeStaffSession } from '../../shared/staffSession';
import { useRealtime } from '../../shared/realtime/useRealtime';
import { getComputerData } from './computerApi';

const empty = { computers: [], rooms: [], types: [], permissions: [] };
const initial = { ...empty, loading: true, refreshing: false, error: null };
const computerEvents = ['ComputerChanged'];
const noServerToken = () => null;

export function useComputerData() {
  const token = useSyncExternalStore(subscribeStaffSession, getStaffAccessToken, noServerToken);
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
    const active = () => !controller.signal.aborted && getStaffAccessToken() === token;
    stateToken.current = token;
    setState(initial);

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
        if (!token) throw new ApiError(getStaffSessionReason() === 'expired'
          ? 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'
          : 'Vui lòng đăng nhập nhân viên để xem danh sách máy.', 401);
        const identity = await apiRequest('/auth/me', { signal: controller.signal });
        if (!active()) return;
        if (!Array.isArray(identity?.permissions)) throw new ApiError('Thông tin quyền truy cập không hợp lệ.', 200);
        if (!identity.permissions.includes('computer.view')) throw new ApiError('Bạn không có quyền xem máy tính.', 403);
        const data = await getComputerData(controller.signal);
        if (!active()) return;
        if (readRevision !== revision.current) { queued = true; return; }
        hasData = true;
        setState({ ...data, permissions: identity.permissions, loading: false, refreshing: false, error: null });
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
  }, [token]);

  const current = stateToken.current === token ? state : initial;
  const realtimeStatus = useRealtime({
    enabled: current.permissions.includes('computer.view'), accessToken: token, events: computerEvents,
    onEvent: reload, onReady: reload,
    onAuthFailure: (status) => {
      if (getStaffAccessToken() !== token) return;
      if (status === 401) clearStaffAccessToken('expired');
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
