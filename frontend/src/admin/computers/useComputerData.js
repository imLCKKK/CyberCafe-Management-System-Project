import { useCallback, useEffect, useState } from 'react';
import { ApiError } from '../../shared/apiClient';
import { useAuth } from '../../shared/auth/AuthContext';
import { getComputerData } from './computerApi';

const empty = { computers: [], rooms: [], types: [], permissions: [] };

export function useComputerData() {
  const { principal, loading: authLoading } = useAuth();
  const [version, setVersion] = useState(0);
  const [state, setState] = useState({ ...empty, loading: true, error: null });
  const reload = useCallback(() => setVersion((value) => value + 1), []);

  useEffect(() => {
    const controller = new AbortController();
    setState({ ...empty, loading: true, error: null });
    if (authLoading) return () => controller.abort();
    async function load() {
      try {
        if (principal?.type !== 'employee') throw new ApiError('Vui lòng đăng nhập nhân viên để xem danh sách máy.', 401);
        if (!principal.permissions.includes('computer.view')) throw new ApiError('Bạn không có quyền xem máy tính.', 403);
        const data = await getComputerData(controller.signal);
        if (!controller.signal.aborted) setState({ ...data, permissions: principal.permissions, loading: false, error: null });
      } catch (error) {
        if (!controller.signal.aborted) setState({ ...empty, loading: false, error });
      }
    }
    load();
    return () => controller.abort();
  }, [version, principal, authLoading]);

  const applyComputer = useCallback((computer) => setState((current) => ({
    ...current,
    computers: current.computers.map((row) => row.ComputerId === computer.ComputerId ? computer : row),
  })), []);

  return { ...state, reload, applyComputer };
}
