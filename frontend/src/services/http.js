// Lớp gọi API cho các trang đã nối backend CyberManagement (ví, hồ sơ, khách hàng).
// Dùng CHUNG client đăng nhập của Đại (shared/auth/api.js): token đọc từ phiên chung
// (localStorage 'cybermanagement.auth.v1'), tự gắn Authorization, và khi gặp 401 phiên chung bị xóa
// rồi AuthProvider đưa về trang đăng nhập. Không giữ kho token riêng nên logout/đổi tài khoản không để lại token cũ.
// Quy ước: API lỗi thì ném ApiError để trang hiện lỗi; KHÔNG quay về dữ liệu localStorage/demo.
import { apiRequest } from '../shared/auth/api';

export class ApiError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}

function toApiError(error) {
  const status = error?.status;
  if (!status) {
    // Không có status: lỗi mạng (fetch ném TypeError) hoặc phiên đang được xác minh (AUTH_SESSION_UNVERIFIED).
    return new ApiError(0, error?.code === 'AUTH_SESSION_UNVERIFIED'
      ? error.message
      : 'Không kết nối được máy chủ API. Hãy kiểm tra API đang chạy.');
  }
  let message = `Máy chủ trả về lỗi ${status}.`;
  if (status === 401) message = 'Bạn chưa đăng nhập hoặc phiên đã hết hạn.';
  else if (status === 403) message = 'Tài khoản này không có quyền thực hiện thao tác này.';
  else {
    const problem = error.body;
    if (problem && typeof problem === 'object') {
      // ProblemDetails: ưu tiên chi tiết lỗi nghiệp vụ (detail) hơn tiêu đề chung (title).
      if (problem.errors) message = Object.values(problem.errors).flat().join(' ');
      else if (problem.detail || problem.title || problem.message) message = problem.detail || problem.title || problem.message;
    }
  }
  return new ApiError(status, message);
}

// path viết đầy đủ như trước ('/api/admin/customers'); client chung đã tự thêm tiền tố '/api'.
export async function request(path, { method = 'GET', body, headers } = {}) {
  const apiPath = path.startsWith('/api/') ? path.slice('/api'.length) : path;
  try {
    return await apiRequest(apiPath, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch (error) {
    throw toApiError(error);
  }
}

// Khóa chống xử lý lặp cho thao tác ghi tiền: một khóa cho MỘT ý định của người dùng, gửi lại cùng khóa khi retry.
export function newIdempotencyKey() {
  if (globalThis.crypto?.randomUUID) return globalThis.crypto.randomUUID();
  const bytes = globalThis.crypto.getRandomValues(new Uint8Array(16));
  return [...bytes].map((b) => b.toString(16).padStart(2, '0')).join('');
}
