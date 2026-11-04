# Màn hình máy — SCRUM-7

`ComputersPage` dùng API cho danh sách máy/phòng/loại và đổi trạng thái. Không đọc `computerStore` hoặc ghép khách/phiên/đơn demo với ID SQL. Dashboard/reports/Home giữ nguồn cũ theo phạm vi task; màn hình máy không cam kết đã đồng bộ với các màn hình đó.

## Kết nối API

- Mặc định browser gọi `/api`. Vite dev/preview chuyển `/api/auth/*` và `/api/admin/*` đến `http://127.0.0.1:5088`; đặt `API_PROXY_TARGET` trong môi trường hoặc `frontend/.env.local` nếu API chạy địa chỉ khác.
- SignalR cập nhật máy qua `/hubs/realtime`, nhận event/reconnect rồi đọc lại API; Vite proxy hỗ trợ WebSocket. Cấu hình và hook dùng chung theo [hướng dẫn realtime](../../shared/realtime/README.md).
- OP.GG vẫn dùng middleware `/api/valorant/opgg/stats`, không đi qua proxy .NET.
- Build production cần reverse proxy cho `/api/auth/*`, `/api/admin/*`, hoặc cấu hình `VITE_API_BASE_URL` gồm tiền tố `/api` khi build. API khác origin cần cấu hình CORS phù hợp ở host, không tự thêm trong task này.
- Các endpoint, JSON, permission và giới hạn trạng thái theo [contract backend](../../../../Backend/docs/002-computer-session-contract.md).

## Bàn giao login cho S1-D-2

Người dùng yêu cầu giữ login cho `S1-D-2`; `/admin/auth` vẫn là placeholder. SCRUM-7 cung cấp [apiClient](../../shared/apiClient.js) và [staffSession](../../shared/staffSession.js) để phần login tích hợp, chưa có luồng đăng nhập người dùng hoàn chỉnh.

Phần login gọi `apiRequest('/auth/login', { method: 'POST', body: { username, password }, accessToken: null })`, sau thành công gọi `setStaffAccessToken(result.accessToken)` rồi điều hướng về `/admin/computers`. Token lưu trong sessionStorage khóa `nexora.staff.accessToken`; không lưu mật khẩu hoặc dùng dữ liệu localStorage để tự cấp quyền. Logout phải xóa token bằng `clearStaffAccessToken()` sau khi xử lý theo contract auth.

Trang máy gọi `/auth/me` để lấy permission, sau đó mới đọc danh sách. Helper token thông báo thay đổi session cho React; không ghi sessionStorage trực tiếp từ phần login. Thiếu token/hết hạn yêu cầu đăng nhập; `401` xóa đúng token của request, không xóa token mới được cấp trong lúc request cũ đang chờ. `403` hiển thị không đủ quyền. Không dùng form nhập token hoặc token hard-code trong sản phẩm.

## Hành vi và kiểm tra

- Search/lọc phòng/loại/trạng thái, dạng lưới/bảng và thông số nullable lấy từ response. Trạng thái không biết vẫn hiển thị, không cho đổi.
- Chỉ người có `computer.status.update` thấy nút thay đổi; phiên/bảo trì chưa đóng chặn thao tác theo contract. PATCH gửi đúng `status` và `expectedStatus`, chờ response hợp lệ mới cập nhật UI; không lưu lạc quan hoặc ghi máy vào localStorage.
- Loading, empty, lỗi kết nối/server và dữ liệu JSON sai có thông báo riêng. `409` đọc lại danh sách; `401/403` khi ghi đọc lại danh tính/quyền. Reload đọc API; không fallback dữ liệu mẫu.
- CRUD tên/phòng/loại và hồ sơ bảo trì chưa thuộc bước này nên form tạo/sửa máy demo được gỡ khỏi trang SQL.

Từ repository root, dùng Node.js 22+:

```powershell
node --test frontend/tests/computer-api.test.mjs
```

Khi dependency frontend đã được cài theo lockfile:

```powershell
npm.cmd --prefix frontend run build
node --test frontend/tests/computer-proxy.test.mjs
npm.cmd --prefix frontend run test:frontend:render
npm.cmd --prefix frontend run test:frontend -- computers-api admin-controls
```

`computer-api.test.mjs` dùng fetch giả; `computer-proxy.test.mjs` dùng HTTP host thử; browser suite `computers-api` dùng HTTP fixtures qua CDP, giữ trạng thái ở test process qua reload và chèn token giả chỉ trong môi trường test. Các ca này không thay thế chứng minh UI–API–SQL thật hoặc login thật. API/SQL có bộ `Backend/tests/CyberManagement.API.Checks`; nghiệm thu luồng người dùng còn phụ thuộc login `S1-D-2` và cấu hình API/SQL chạy thực tế.

Kiểm chứng ngày 03/11/2026: build, 10 kiểm tra API client/proxy, ba script render, các suite browser `computers-api`, `admin-controls` và 80 lượt route/viewport đều đạt. Package manifest và lockfile không đổi sau npm ci. Chrome cần chạy ngoài sandbox trong môi trường kiểm tra này. Render Home/Tournaments có cảnh báo SSR useLayoutEffect; tại 320px, body có min-width 320px trong [theme hiện có](../../app/theme.css) nên ảnh có scrollbar ngang khi xuất hiện scrollbar dọc. Các phần nền này chưa sửa trong SCRUM-7.
