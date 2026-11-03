import { useCallback, useEffect, useState } from 'react';
import { ApiError, apiRequest } from '../../shared/apiClient';
import { getStaffAccessToken } from '../../shared/staffSession';
import { getComputerData } from './computerApi';

const empty = { computers: [], rooms: [], types: [], permissions: [] };

export function useComputerData() {
  const [version, setVersion] = useState(0);
  const [state, setState] = useState({ ...empty, loading: true, error: null });
  const reload = useCallback(() => setVersion((value) => value + 1), []);

  useEffect(() => {
    const controller = new AbortController();
    setState({ ...empty, loading: true, error: null });
    async function load() {
      try {
        if (!getStaffAccessToken()) throw new ApiError('Vui lòng đăng nhập nhân viên để xem danh sách máy.', 401);
        const identity = await apiRequest('/auth/me', { signal: controller.signal });
        if (!Array.isArray(identity?.permissions)) throw new ApiError('Thông tin quyền truy cập không hợp lệ.', 200);
        if (!identity.permissions.includes('computer.view')) throw new ApiError('Bạn không có quyền xem máy tính.', 403);
        const data = await getComputerData(controller.signal);
        if (!controller.signal.aborted) setState({ ...data, permissions: identity.permissions, loading: false, error: null });
      } catch (error) {
        if (!controller.signal.aborted) setState({ ...empty, loading: false, error });
      }
    }
    load();
    return () => controller.abort();
  }, [version]);

  const applyComputer = useCallback((computer) => setState((current) => ({
    ...current,
    computers: current.computers.map((row) => row.ComputerId === computer.ComputerId ? computer : row),
  })), []);

  return { ...state, reload, applyComputer };
}
