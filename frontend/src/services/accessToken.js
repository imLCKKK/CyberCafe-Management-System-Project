// Nơi giữ access token JWT. Hai loại tài khoản, hai khóa riêng:
//   'client' = khách (POST /api/auth/customer/login), 'staff' = nhân viên (POST /api/auth/login).
// TẠM THỜI cho tới khi API client/đăng nhập chung của Đại (S1-D-2/S1-D-3) bàn giao: khi đó chỉ cần
// đổi 3 hàm này sang kho token của Đại. Dùng sessionStorage nên đóng tab là mất; không ghi token vào log.
const KEYS = { client: 'cybercafe.client.accessToken', staff: 'cybercafe.staff.accessToken' };
const keyOf = (kind) => KEYS[kind] || KEYS.client;

export function getAccessToken(kind = 'client') {
  try { return sessionStorage.getItem(keyOf(kind)) || ''; } catch { return ''; }
}
export function setAccessToken(token, kind = 'client') {
  try { sessionStorage.setItem(keyOf(kind), token); } catch { /* trình duyệt chặn storage: coi như chưa đăng nhập */ }
}
export function clearAccessToken(kind = 'client') {
  try { sessionStorage.removeItem(keyOf(kind)); } catch { /* bỏ qua */ }
}
