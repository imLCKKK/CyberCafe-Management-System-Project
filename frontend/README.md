# Frontend React / Vite

Frontend có mã nguồn, package/lockfile, cấu hình Vite, tài nguyên public và
kiểm tra trình duyệt trong cùng thư mục này. Xem [bản đồ src](src/README.md)
và [kiến trúc repository](../docs/architecture.md). Hành vi bản demo admin được ghi trong [ADMIN_DEMO.md](ADMIN_DEMO.md).

## Chạy

Cần Node.js 22 trở lên. Từ thư mục gốc repository:

```powershell
cd frontend
npm.cmd ci
npm.cmd run dev
```

Mở URL Vite in ra. Các lệnh sau cũng chạy trong `frontend/`:

```powershell
npm.cmd run build
npm.cmd run test:frontend:models
npm.cmd run test:frontend:render
npm.cmd run test:frontend
```

Kiểm tra trình duyệt cần Chrome hoặc Edge; có thể chỉ định executable bằng
`CHROME_PATH`. Kết quả/ảnh kiểm tra nằm ở `.tools/frontend-retest/` tại gốc
repository. `npm.cmd run test:frontend -- opgg-live` kiểm tra nguồn OP.GG thật
và cần mạng; các suite mặc định dùng fixture cho OP.GG.

## Auth frontend

Đăng nhập nhân viên và khách dùng chung API client tại `src/shared/auth/api.js`.
Nhân viên gọi `POST /api/auth/login` bằng `username/password`; khách gọi
`POST /api/auth/customer/login` bằng `phone/password`. Sau đăng nhập, frontend
đọc `/api/auth/me` hoặc `/api/auth/customer/me` để xác nhận phiên và lấy danh
tính/quyền từ server. JWT được giữ trong `localStorage`; role và permissions
không được khôi phục từ storage. API là nơi thực thi quyền cuối cùng.

Khi chạy dev, khởi động API tại `http://127.0.0.1:5088` rồi chạy Vite. Mặc định
Vite proxy `/api` tới API này. Có thể đặt trong `frontend/.env.local`:

```dotenv
VITE_API_PROXY_TARGET=http://127.0.0.1:5088
VITE_API_BASE_URL=/api
```

`VITE_API_PROXY_TARGET` chỉ dùng cho proxy dev. Khi deploy frontend và API khác
origin, đặt `VITE_API_BASE_URL` thành base URL API, ví dụ
`https://api.example.test/api`, và cấu hình CORS phía API cho origin frontend.
Không đặt signing key, mật khẩu seed hoặc credential trong biến `VITE_*`.

Chạy test API client bằng `npm.cmd run test:auth`. Kiểm tra tích hợp thủ công
cần SQL Server/API đã cấu hình theo [Backend auth guide](../backend/docs/001-monorepo.md):

Từ thư mục gốc repo, chạy HTTP/persistence checks backend bằng:

```powershell
.\scripts\dotnet-local.cmd run --project backend\tests\CyberManagement.API.Checks
```

Mặc định harness dùng InMemory và tự seed tài khoản kiểm thử. Để chạy cùng bộ
checks qua SQL Server, cung cấp `AUTH_CHECKS_SQL_CONNECTION` trỏ tới SQL Server
test mà tài khoản có quyền tạo/xóa database. Harness tạo database tên ngẫu nhiên,
chạy migration/seed rồi xóa database đó; không trỏ tới database dùng chung hoặc
production. Không commit connection string. Bộ checks bao gồm login hợp lệ/sai,
seed lặp và gọi `/api/admin/roles` bằng role thiếu quyền để xác nhận `403`.

- Đăng nhập đúng nhân viên và khách; xác nhận `/me` trả đúng role/permissions và
	điều hướng vào khu vực tương ứng.
- Đăng nhập sai mật khẩu nhận `401` và hiện lỗi; nhân viên vô hiệu hóa hoặc khách
	bị khóa nhận `403`.
- Đăng nhập với từng role seed; sidebar chỉ hiện module có permission. Mở trực
	tiếp route thiếu quyền phải quay về trang tổng quan; gọi trực tiếp API nghiệp
	vụ thiếu quyền vẫn phải nhận `403`.
- Làm token hết hạn (đặt lifetime dev tối thiểu 1 phút), gọi API hoặc chờ hết
	hạn; token bị xóa và UI chuyển về đúng màn đăng nhập với thông báo hết phiên.
- Đăng xuất gửi endpoint đúng loại tài khoản và xóa token cả khi API logout lỗi;
	đăng xuất ở tab khác cũng đóng phiên UI.

Module nghiệp vụ dùng `apiRequest(path, options)` từ
`src/shared/auth/api.js`. Hàm tự gắn bearer token, trả JSON/text đã parse, và
ném `Error` có `status`/`body`; response `401` xóa phiên và phát event
`cybermanagement:auth-expired`. Module vẫn phải để API xác minh permission;
không dùng role phía UI để thay thế `[Authorize]`.

## Trạng thái tích hợp

Các màn hình nghiệp vụ hiện vẫn dùng dữ liệu demo trong localStorage, chưa kết
nối API ASP.NET Core/SQL Server và chưa đồng bộ giữa hai máy. Auth cung cấp
contract phiên dùng chung; mỗi module cần nối endpoint nghiệp vụ và quyền tương
ứng. Vite tiếp tục gắn middleware OP.GG từ `../Backend/integrations/opgg/`.
