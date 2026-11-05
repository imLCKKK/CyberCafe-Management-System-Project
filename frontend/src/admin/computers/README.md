# Màn hình máy — SCRUM-7

`ComputersPage` dùng API cho danh sách máy/phòng/loại và đổi trạng thái. Không đọc `computerStore` hoặc ghép khách/phiên/đơn demo với ID SQL. Dashboard/reports/Home giữ nguồn cũ theo phạm vi task; màn hình máy không cam kết đã đồng bộ với các màn hình đó.

## Kết nối API

- Mặc định browser gọi `/api`. Vite dev/preview chuyển `/api/auth/*` và `/api/admin/*` đến `http://127.0.0.1:5088`; đặt `VITE_API_PROXY_TARGET` trong môi trường hoặc `frontend/.env.local` nếu API chạy địa chỉ khác. Biến `API_PROXY_TARGET` cũ vẫn được hỗ trợ khi không có `VITE_API_PROXY_TARGET`.
- OP.GG vẫn dùng middleware `/api/valorant/opgg/stats`, không đi qua proxy .NET.
- Build production cần reverse proxy cho `/api/auth/*`, `/api/admin/*`, hoặc cấu hình `VITE_API_BASE_URL` gồm tiền tố `/api` khi build. API khác origin cần cấu hình CORS phù hợp ở host, không tự thêm trong task này.
- Các endpoint, JSON, permission và giới hạn trạng thái theo [contract backend](../../../../Backend/docs/002-computer-session-contract.md).

## Tích hợp login S1-D-2

Trang máy dùng `useAuth()` từ [AuthContext](../../shared/auth/AuthContext.jsx). Chỉ đọc dữ liệu sau khi provider xác minh phiên nhân viên qua `/auth/me`; permission lấy từ principal đã xác minh, không lấy từ dữ liệu tự khai trong localStorage. Đổi tài khoản/logout hủy request đọc đang chờ và route guard xử lý điều hướng.

Form `/admin/auth` dùng `signIn('employee', credentials)` của S1-D-2, lưu phiên ở `localStorage['cybermanagement.auth.v1']`. Trang máy không đọc/ghi `sessionStorage['nexora.staff.accessToken']`; token cũ ở đó không ảnh hưởng phiên mới.

[apiClient](../../shared/apiClient.js) giữ việc chuyển JSON body, kiểm tra dữ liệu và thông báo lỗi cho module máy, nhưng giao request/token/hết phiên cho [client chung](../../shared/auth/api.js). Không có kho phiên thứ hai. `401` xóa đúng phiên của request và quay về login, không xóa phiên mới khi request cũ trả lỗi muộn. `403` giữ phiên và báo thiếu quyền. Quyền ghi luôn được API kiểm tra; người chỉ có `computer.view` không thấy nút đổi trạng thái.

## Hành vi và kiểm tra

- Search/lọc phòng/loại/trạng thái, dạng lưới/bảng và thông số nullable lấy từ response. Trạng thái không biết vẫn hiển thị, không cho đổi.
- Chỉ người có `computer.status.update` thấy nút thay đổi; phiên/bảo trì chưa đóng chặn thao tác theo contract. PATCH gửi đúng `status` và `expectedStatus`, chờ response hợp lệ mới cập nhật UI; không lưu lạc quan hoặc ghi máy vào localStorage.
- Loading, empty, lỗi kết nối/server và dữ liệu JSON sai có thông báo riêng. `409` đọc lại danh sách; `401` chuyển về login; `403` khi ghi báo lỗi và đọc lại dữ liệu. Reload trang xác minh lại principal và đọc API; không fallback dữ liệu mẫu.
- CRUD tên/phòng/loại và hồ sơ bảo trì chưa thuộc bước này nên form tạo/sửa máy demo được gỡ khỏi trang SQL.

Từ repository root, dùng Node.js 22+:

```powershell
node --test frontend/tests/computer-api.test.mjs frontend/src/shared/auth/api.test.js
```

Khi dependency frontend đã được cài theo lockfile:

```powershell
npm.cmd --prefix frontend run build
node --test frontend/tests/computer-proxy.test.mjs
npm.cmd --prefix frontend run test:frontend:render
npm.cmd --prefix frontend run test:frontend -- computers-api auth-area
```

`computer-api.test.mjs` dùng fetch giả; `computer-proxy.test.mjs` dùng HTTP host thử cho dev/preview và cả hai biến cấu hình proxy. Browser suite `computers-api` thao tác form login thật với HTTP fixtures qua CDP, giữ trạng thái máy ở test process qua reload. Kiểm tra chờ xác minh phiên, token legacy bị bỏ qua, login → đọc → PATCH → reload, chỉ xem, lỗi GET/PATCH, hết phiên và giao diện 390/320px. Đây không phải bằng chứng UI–API–SQL thật. API/SQL có bộ `Backend/tests/CyberManagement.API.Checks`; cần môi trường SQL thử được chỉ định để kiểm chứng đầu-cuối.

Kiểm chứng tích hợp ngày 05/11/2026 trên SCRUM-7 `c0aae97` + dev `e14a362` + bản sửa chưa commit: build production, 21 kiểm tra auth/API client/proxy, ba script render và browser suites `computers-api`, `auth-area` đạt. Browser xác minh cả hết phiên từ GET/PATCH, `/me`, timer và reload; thông báo hết phiên không bị route guard ghi đè. Dùng dependency đã có, không cài thêm package. Backend/schema không đổi; chưa chạy UI–API–SQL thật và không coi 138 checks SQL Server của lần trước là kết quả chạy mới.

Các lượt đầu gặp Chrome bị ngắt trong sandbox, giả định login cũ trong test và request lặp/hủy ở StrictMode; đã xử lý test và chạy lại ngoài sandbox. Test cũng phát hiện mất thông báo hết phiên do cập nhật principal và điều hướng khác lượt render; AuthContext đã sửa và các ca hết phiên chạy lại đạt. Cache Vite cho browser được tách khỏi render/proxy tests. Render Home/Tournaments còn cảnh báo SSR useLayoutEffect đã có từ trước.

## Kiểm chứng UI–API–SQL thật (06/11/2026)

Từ repository root, với .NET 10, Node.js 22+, dependency hiện có và Chrome/Edge:

```powershell
dotnet restore Backend/tests/CyberManagement.API.Checks
# Dùng SQL Server thử mà tài khoản có quyền tạo/xóa DB; không ghi credential vào repo.
$env:AUTH_CHECKS_SQL_CONNECTION = 'Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Encrypt=True'
node frontend/tests/run-computers-live.mjs
```

Runner build test host, tạo DB `CyberManagement_AuthChecks_<guid>` riêng qua fixture hiện có, áp 4 migration/35 bảng nghiệp vụ, seed tài khoản và một máy. API dùng Kestrel, JWT, service và repository thật. Runner đặt proxy Vite trỏ đúng host và tự dọn DB sau khi test thành công hoặc thất bại; không migrate/seed DB được ghi trong connection string. Có thể đặt `DOTNET_EXE`/`CHROME_PATH` nếu công cụ không nằm trong đường dẫn chuẩn. Không đưa suite này vào bộ fixture mặc định; thiếu cấu hình SQL thì dừng, không fallback InMemory.

Hai luồng đã chạy đạt, exit 0:

- Admin đăng nhập bằng form → GET máy/phòng/loại → bấm đổi `Available` sang `Maintenance` → PATCH 200 → đọc SQL độc lập → reload xác minh `/auth/me`/GET mới và UI vẫn bảo trì.
- Waiter đăng nhập bằng form → có `computer.view`, thiếu `computer.status.update`, không có nút sửa → cố tình gửi PATCH hợp lệ qua API client → 403 → toàn bộ cột của các bản ghi Computers trong SQL không đổi → reload vẫn bảo trì.

Không chặn request bằng CDP Fetch hoặc thay `window.fetch`; tài khoản chỉ seed vào DB thử, JWT nhận qua login thật. Endpoint đọc SQL kiểm chứng nằm riêng trong test assembly, yêu cầu Admin, dùng DbContext mới và raw SQL; API production không có endpoint này. Bằng chứng browser gồm log và ảnh `computers-live-admin-reload.png`, `computers-live-readonly-denied.png` trong `.tools/frontend-retest/<lượt chạy>/`; không lưu token/password vào artifacts.

Hồi quy cùng ngày: 138 HTTP/persistence checks SQL Server đạt và DB thử đã dọn. Các lượt đầu sửa import test host và cú pháp selector; lượt cuối đạt cả hai luồng. Build backend có 4 cảnh báo NU1900 do không tải được dữ liệu vulnerability NuGet. Kiểm chứng chỉ bao phủ máy trên một browser/API/SQL local; chưa chứng minh phiên/thu tiền hoặc SignalR hai máy LAN.
