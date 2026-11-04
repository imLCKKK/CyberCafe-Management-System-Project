# SignalR frontend — SCRUM-8

Phần dùng chung gồm [useRealtime](useRealtime.js), [registry kết nối](sharedRealtime.js) và [lifecycle client](realtimeClient.js). Client chính thức `@microsoft/signalr` được khóa ở `10.0.11`; manifest/lockfile chỉ bổ sung package này và dependency bắt buộc, giữ phiên bản các package đã có.

## Dùng trong module

```jsx
const connectionStatus = useRealtime({
  enabled: Boolean(accessToken && canReadModule),
  accessToken,
  events: ['ComputerChanged'],
  onEvent: () => refetchFromApi(),
  onReady: () => refetchFromApi(),
  onAuthFailure: (status) => handleAuthenticationFailure(status),
});
```

- `enabled` theo trạng thái xác thực/quyền, không theo cờ loading của mỗi lượt đọc API. Đổi callback hoặc truyền mảng event có nội dung như cũ không tạo lại kết nối.
- Mỗi token dùng một kết nối dùng chung trong tab. Consumer cuối unmount/disable thì timer và handler được dọn, kết nối dừng. Token khác dùng client khác; callback cũ không được chạy trên session mới.
- Đăng ký handler trước khi start. `RealtimeReady` do server gửi sau khi phân nhóm xong; cả tín hiệu này và callback reconnect đều yêu cầu refetch. Client không chọn nhóm/account và không gọi JoinGroup.
- Event là tín hiệu đọc lại API, không phải lệnh sửa ví/tồn hoặc thay dữ liệu cục bộ bằng payload. Owner xử lý loading/lỗi của request và nên gộp các lượt refetch nếu module nhận nhiều event.
- Trạng thái trả về: `idle`, `connecting`, `connected`, `reconnecting`, `disconnected`, `unauthorized`, `forbidden`. Owner hiển thị nhãn phù hợp cho người dùng; không đưa lỗi thô hoặc URL mang token lên giao diện/log.

## Reconnect và xác thực

SDK tự reconnect theo các mốc 0/2/10/30 giây. Initial start thất bại hoặc SDK hết lượt reconnect thì lifecycle thử lại sau 2/5/10/30 giây, tối đa 30 giây giữa các lượt; unmount sẽ hủy retry. `401/403` dừng retry và báo về consumer.

Adapter HTTP đọc mã lỗi trước khi SDK bọc lỗi negotiate và làm mất `statusCode`. `401` của trang máy chỉ xóa token nếu vẫn là token hiện tại; `403` giữ token và đọc lại quyền API. Phiên hết hạn giữ thông báo yêu cầu đăng nhập lại. Logic này có kiểm thử bằng SDK chính thức trên HTTP host cục bộ.

Login `S1-D-2` dùng `setStaffAccessToken`/`clearStaffAccessToken` trong [staffSession](../staffSession.js), không ghi sessionStorage trực tiếp rồi mong React tự cập nhật. Helper thông báo login/logout cho consumer; quyền vẫn do `/api/auth/me` và backend quyết định. Hook dùng chung nhận token qua tham số để module khách sau này có thể dùng cùng cơ chế với nguồn token của mình.

## Màn hình máy

[useComputerData](../../admin/computers/useComputerData.js) giữ kết nối khi refetch, không xóa permission mỗi lượt đọc. Các lượt đọc chạy tuần tự, gộp burst thành tối đa một lượt tiếp theo; không abort liên tục khiến request không bao giờ xong. Kết quả đọc bắt đầu trước một lần lưu tại máy hiện tại sẽ không ghi đè kết quả lưu mới hơn, mà được thay bằng lượt đọc tiếp theo.

Event, reconnect và `RealtimeReady` đều đọc lại API. Bộ lọc và dialog đang mở được giữ khi cập nhật nền. Lỗi API hiển thị rõ, không fallback dữ liệu demo. Khi đổi session, dữ liệu/quyền được nạp lại; phản hồi PATCH của token cũ không cập nhật UI hoặc báo thành công cho session mới.

## URL và proxy

- Mặc định `/hubs/realtime` cùng origin với frontend; cấu hình `VITE_REALTIME_URL` khi build nếu dùng URL khác.
- Vite dev/preview chuyển `/hubs/realtime`, negotiate, SSE và WebSocket đến `API_PROXY_TARGET` (mặc định `http://127.0.0.1:5088`). Regex proxy nhận cả dấu `?` của query token/connection ID; `ws: true` giữ HTTP Upgrade.
- OP.GG vẫn ở middleware riêng. Build production cần reverse proxy hỗ trợ `/hubs/realtime` và WebSocket, hoặc host có CORS phù hợp khi khác origin; chưa thay cấu hình CORS/firewall trong bước này.
- SDK được tách thành chunk build riêng. Contract event/quyền và giới hạn một API process nằm trong [backend contract](../../../../Backend/docs/003-realtime-contract.md).

## Kiểm tra

Từ repository root:

```powershell
node --test frontend/tests/realtime-client.test.mjs frontend/tests/realtime-sdk.test.mjs frontend/tests/computer-api.test.mjs frontend/tests/computer-proxy.test.mjs
npm.cmd --prefix frontend run build
npm.cmd --prefix frontend run test:frontend:render
npm.cmd --prefix frontend run test:frontend -- realtime computers-api admin-controls routes
```

19 kiểm tra Node gồm lifecycle/session/API, negotiate 401/403 với SDK thật, HTTP proxy và WebSocket upgrade. Browser suite `realtime` dùng SDK và HTTP fixtures để kiểm tra event/refetch không F5, burst 20 event, giữ filter/dialog, reconnect, client dùng chung và đổi token. Suite máy dùng HTTP fixtures với client chính thức nhận 503 ở negotiate để cô lập dữ liệu test. Lỗi CDP do request đã được Network xác nhận hủy không coi là lỗi ứng dụng; các lỗi interception khác vẫn fail test.

Build và render đạt; build còn hai cảnh báo annotation `PURE` từ mã SDK, render Home/Tournaments còn cảnh báo SSR `useLayoutEffect` có sẵn. Không sửa thư viện hoặc các trang ngoài phạm vi để che cảnh báo.

Kiểm thử bước 3 `node frontend/tests/run-realtime-live.mjs` cần `AUTH_CHECKS_SQL_CONNECTION`: tự build host thử, tạo/migrate DB riêng, chạy hai tab Chrome với SDK/Hub/Kestrel/SQL thật rồi dừng và xóa DB do fixture sở hữu. Không nằm trong suite mặc định để tránh tự tạo DB. Host và các endpoint `/api/admin/__checks/*` chỉ có trong project test, không có trong API production. Không thêm form login; test gọi API identity và helper session đã có.

Đã đạt trên một máy: 10 lượt A đổi trạng thái, B refetch/cập nhật dialog không F5 (44–163 ms từ click đến DOM ở lượt chạy cuối, đồng hồ driver); API restart giữ SQL, B offline bỏ lỡ event rồi reconnect/refetch và nhận event tiếp theo; Waiter không sửa được máy, customer không nhận event máy. Bộ checks backend cũng đạt 182 kiểm tra SQL thật. Chi tiết và cách tái hiện trong [contract](../../../../Backend/docs/003-realtime-contract.md).

**Chưa nghiệm thu hai máy LAN:** người dùng hiện chỉ có một máy; số đo loopback không chứng minh mục tiêu LAN 2 giây. Dùng [kịch bản LAN](../../../../Backend/docs/004-realtime-lan-checks.md) khi có máy B. Hướng dẫn reconnect/start dựa trên [Microsoft SignalR JavaScript client](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-10.0).
