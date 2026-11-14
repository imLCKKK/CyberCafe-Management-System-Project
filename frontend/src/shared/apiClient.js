import { apiRequest as authenticatedRequest } from './auth/api.js';

export class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

// Preserve the computer module's JSON/error contract; S1-D-2 owns the session and HTTP request.
export async function apiRequest(path, { method = 'GET', body, signal } = {}) {
  try {
    const result = await authenticatedRequest(path, {
      method, signal,
      ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
    });
    if (typeof result === 'string') throw new ApiError('Dữ liệu máy chủ trả về không hợp lệ.', 200);
    return result;
  } catch (error) {
    if (error.name === 'AbortError' || error instanceof ApiError || error.code === 'AUTH_SESSION_UNVERIFIED') throw error;
    let details = error.body;
    // ASP.NET ProblemDetails uses application/problem+json, returned as text by the auth client.
    if (typeof details === 'string') {
      try { details = JSON.parse(details); } catch { details = null; }
    }
    const status = error.status ?? (error instanceof SyntaxError ? 200 : 0);
    const message = status === 401 ? 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'
      : status === 403 ? 'Bạn không có quyền thực hiện thao tác này.'
        : status >= 500 ? 'Máy chủ đang gặp lỗi. Vui lòng thử lại sau.'
          : status === 0 ? 'Không thể kết nối máy chủ. Vui lòng thử lại.'
            : status === 200 ? 'Dữ liệu máy chủ trả về không hợp lệ.'
              : details?.detail || details?.message || details?.title || 'Không thể thực hiện thao tác. Vui lòng tải lại dữ liệu.';
    throw new ApiError(message, status);
  }
}
