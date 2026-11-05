// Lớp gọi API dùng chung cho các trang đã nối backend CyberManagement.
// Quy ước: API lỗi thì ném ApiError để trang hiện lỗi; KHÔNG quay về dữ liệu localStorage/demo.
import { getAccessToken } from './accessToken';

export class ApiError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}

export async function request(path, { kind = 'client', method = 'GET', body } = {}) {
  const token = getAccessToken(kind);
  const headers = { ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}), ...(token ? { Authorization: `Bearer ${token}` } : {}) };
  let response;
  try {
    response = await fetch(path, { method, headers, body: body !== undefined ? JSON.stringify(body) : undefined });
  } catch {
    throw new ApiError(0, 'Không kết nối được máy chủ API. Hãy kiểm tra API đang chạy.');
  }
  if (response.ok) return response.status === 204 ? null : response.json();

  let message = `Máy chủ trả về lỗi ${response.status}.`;
  if (response.status === 401) message = 'Bạn chưa đăng nhập hoặc phiên đã hết hạn.';
  else if (response.status === 403) message = 'Tài khoản này không có quyền thực hiện thao tác này.';
  else {
    try {
      const problem = await response.json();
      if (problem.errors) message = Object.values(problem.errors).flat().join(' ');
      else if (problem.detail || problem.title || problem.message) message = problem.detail || problem.title || problem.message;
    } catch { /* body không phải JSON: giữ thông báo mặc định */ }
  }
  throw new ApiError(response.status, message);
}
