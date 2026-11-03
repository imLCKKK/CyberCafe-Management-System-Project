import { clearStaffAccessToken, getStaffAccessToken } from './staffSession.js';

const apiBase = (import.meta.env?.VITE_API_BASE_URL?.trim() || '/api').replace(/\/$/, '');

export class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

export async function apiRequest(path, { method = 'GET', body, signal, accessToken = getStaffAccessToken() } = {}) {
  let response;
  try {
    response = await fetch(`${apiBase}${path}`, {
      method,
      signal,
      headers: {
        Accept: 'application/json',
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
        ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      },
      ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
    });
  } catch (error) {
    if (error.name === 'AbortError') throw error;
    throw new ApiError('Không thể kết nối máy chủ. Vui lòng thử lại.', 0);
  }

  if (response.status === 401 && accessToken && getStaffAccessToken() === accessToken) clearStaffAccessToken();
  let result;
  try {
    result = response.status === 204 ? null : await response.json();
  } catch {
    if (response.ok) throw new ApiError('Dữ liệu máy chủ trả về không hợp lệ.', response.status);
  }
  if (!response.ok) {
    const message = response.status === 401 ? (accessToken ? 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'
      : result?.message || 'Vui lòng đăng nhập nhân viên để tiếp tục.')
      : response.status === 403 ? 'Bạn không có quyền thực hiện thao tác này.'
        : response.status >= 500 ? 'Máy chủ đang gặp lỗi. Vui lòng thử lại sau.'
          : result?.detail || result?.message || result?.title || 'Không thể thực hiện thao tác. Vui lòng tải lại dữ liệu.';
    throw new ApiError(message, response.status);
  }
  return result;
}
