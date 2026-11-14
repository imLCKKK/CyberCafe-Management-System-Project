# SCRUM-8 — Kịch bản nghiệm thu hai máy LAN

**Ghi nhận ngày 14/11/2026 theo xác nhận Trưởng nhóm trong phiên review Sprint 1.** Trưởng nhóm cho biết đã thử hai máy LAN và chạy ổn. Hướng dẫn và bằng chứng local bên dưới được giữ theo thời điểm kiểm tra, các số đo local chỉ mô tả hai browser context trên một máy.

Mốc 08/11/2026 ghi nhận người dùng chưa có máy B, local integration đã chạy với hai browser context tách biệt để mỗi tài khoản có localStorage riêng.

## 1. Tái hiện kiểm thử tự động trên một máy

Từ root, sau khi dependency theo lockfile đã cài, SQL Server và Chrome/Edge sẵn sàng:

```powershell
$env:AUTH_CHECKS_SQL_CONNECTION = 'Server=localhost;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15'
node frontend/tests/run-realtime-live.mjs
Remove-Item Env:AUTH_CHECKS_SQL_CONNECTION
```

Nếu Node chưa có trong PATH, dùng đường dẫn Node đã cài. `DOTNET_EXE` và `CHROME_PATH` hỗ trợ vị trí executable khác mặc định. Không cần thêm package. Runner tự build test project, bind loopback, tạo DB ngẫu nhiên, chạy 10 lượt UI cùng kiểm tra quyền/restart/missed event rồi gửi `stop` để dọn DB, kể cả khi browser suite fail. Thành công phải có dòng `Removed isolated auth-check database: CyberManagement_AuthChecks_...` và exit code 0. Không dùng kill cưỡng bức; nếu tiến trình bị ngắt bất thường, ghi lại tên DB fixture để owner DB xử lý đúng DB đó.

Artifact nằm trong `.tools/frontend-retest/<UTC>/`, gồm `realtime-live.json` (số đo thô, `lanAccepted: false`), ảnh và summary. Thư mục này là output local, không tự có trên máy reviewer.

## 2. Chuẩn bị máy A và B

- A chạy SQL/API/Vite; B cùng LAN mở browser. Cả hai dùng cùng origin `http://<IP-LAN-A>:5173` và một API process. Xác nhận IP bằng `ipconfig`; ghi IP/OS/browser/commit/thời điểm chạy vào biên bản. Không điền IP giả vào kết quả.
- Dùng **test host** dưới đây để có DB riêng và tài khoản thử, không seed lên DB nghiệp vụ. API chỉ nghe loopback, Vite chuyển tiếp API/Hub cho B; không cần mở cổng SQL hay API cho B.
- Máy A cần cho máy B truy cập cổng Vite 5173 trên mạng LAN tin cậy. Nếu firewall chặn thì owner cấu hình quyền truy cập phù hợp; hướng dẫn này không tự thay firewall hoặc triển khai production. Không mở debugger/CDP ra LAN.

Terminal 1 trên A, từ root:

```powershell
.\scripts\dotnet-local.cmd build Backend\tests\CyberManagement.API.Checks --no-restore
$env:AUTH_CHECKS_SQL_CONNECTION = 'Server=localhost;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15'
$env:REALTIME_CHECKS_URL = 'http://127.0.0.1:5089'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks --no-build --no-restore --no-launch-profile -- --realtime-host
```

Đợi dòng JSON `"type":"ready"`. Terminal này giữ stdin mở, nhận `restart` hoặc `stop`; Ctrl+C cũng dừng/dọn DB. Không dùng tài khoản/khóa test cố định này ngoài fixture và LAN thử.

Terminal 2 trên A:

```powershell
$env:API_PROXY_TARGET = 'http://127.0.0.1:5089'
npm.cmd --prefix frontend run dev -- --host 0.0.0.0 --port 5173 --strictPort
```

Mở `http://<IP-LAN-A>:5173/admin/auth` trên cả hai máy và đăng nhập bằng form UI: A dùng `admin` / `Admin-check-password-2026!`; B dùng `waiter01` / `Waiter-check-password-2026!`. Chọn trang **Máy** sau khi đăng nhập. Auth, request máy và Hub cùng dùng phiên `cybermanagement.auth.v1`; không gán token qua helper hoặc sessionStorage để bỏ qua bước tích hợp.

Không in/copy token vào log hoặc biên bản. Hai máy phải thấy `LAN-PC-01` và nhãn đã kết nối trực tiếp; B chỉ xem, không có nút đổi trạng thái. Ghi `globalThis.__lanMarker = 'no-f5'` vào Console B để kiểm tra trang không reload.

## 3. Đo 10 lượt bằng cùng đồng hồ

Dùng **một video/camera có cả màn hình A và B**, biết FPS, thấy rõ thời điểm click ở A và trạng thái mới ở B. Từ số frame tính `độ trễ = (frame B đổi - frame A click) / FPS`. Đây là phép đo từ click đến hiển thị, bao gồm request/commit/event/refetch/render, chặt hơn chỉ đo sau commit. Nếu không quay được hai màn hình trong cùng video, cần công cụ đo có cùng đồng hồ trước khi ghi số; không trừ `Date.now()` của hai máy chưa chứng minh đồng bộ.

Trên B giữ dialog `LAN-PC-01` mở. A luân phiên **Chuyển bảo trì / Đưa máy vào sử dụng** 10 lần, đợi B xong mỗi lượt, không F5 và không bấm Tải lại trên B. Ghi từng lượt với frame bắt đầu/kết thúc/FPS/độ trễ, trạng thái đích và pass/fail. Có thể dùng bảng:

| Lượt | Đích | Frame A | Frame B | FPS | Giây | ≤ 2 giây | Không F5 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1–10 (mỗi lượt một dòng khi chạy) | | | | | | | |

Chỉ đạt khi cả 10 lượt tối đa 2 giây và dialog/list B đúng SQL. Giữ số liệu thất bại nếu có, không chỉ báo trung bình hoặc bỏ lượt chậm. Đối chiếu Network: A PATCH 200, B tự GET `/api/admin/computers`; không xuất HAR chứa Authorization/query token.

## 4. Reconnect, restart và quyền

1. B bật DevTools Network Offline. Tại terminal test host A gõ `restart`; chờ `"type":"restarted"`. Kiểm tra trạng thái máy vẫn còn sau restart. A đổi trạng thái khi B còn offline. B Online lại, phải tự chuyển từ mất/kết nối lại sang đã kết nối, đọc API và thấy trạng thái bị bỏ lỡ; A đổi tiếp thì B vẫn cập nhật. Không dùng F5. Kiểm tra `globalThis.__lanMarker` còn `no-f5`. Ghi thời gian phục hồi riêng, không trộn với độ trễ 10 lượt khi mạng ổn định.
2. B dùng token Waiter hiện tại thử PATCH trạng thái (body có `expectedStatus` đúng) bằng `fetch`; phải 403 và SQL không đổi. Xác minh A có quyền sửa, B chỉ quyền xem. Anonymous POST `/hubs/realtime/negotiate?negotiateVersion=1` phải 401.
3. Mở tab riêng trên B cho customer test (`/api/auth/customer/login`, phone `0900000010`, password `Customer-check-password-2026!`). Không dùng token staff cho tab này. Đăng ký SDK thật trước khi connect qua helper:

```javascript
const r = await fetch('/api/auth/customer/login', {
  method:'POST', headers:{'Content-Type':'application/json'},
  body:JSON.stringify({phone:'0900000010',password:'Customer-check-password-2026!'})
});
if (!r.ok) throw Error(`Customer login HTTP ${r.status}`);
const {accessToken} = await r.json();
globalThis.__received = [];
globalThis.__customerReady = false;
globalThis.__customerClient = (await import('/src/shared/realtime/sharedRealtime.js')).createSignalRClient(accessToken);
__customerClient.subscribe({events:['ComputerChanged','CheckBarrier'],
  onReady:()=>{globalThis.__customerReady=true},
  onEvent:(name,message)=>__received.push({name,eventId:message.eventId})});
```

Đợi `__customerReady === true`, A đổi máy vài lượt. Trên A dùng token admin trong session helper POST `/api/admin/__checks/customer-barrier`; kiểm tra HTTP 200, chỉ ghi `eventId` trả về. B phải nhận `CheckBarrier` có đúng `eventId` nhưng không có `ComputerChanged` trước marker (`__received`). Marker theo cùng kết nối xác nhận không nhận event trước đó, không suy đoán bằng sleep. Customer GET danh sách máy phải 403. Kết thúc gọi `__customerClient.dispose()`.

Console A để gửi marker qua đúng Vite proxy:

```javascript
const {apiRequest} = await import('/src/shared/auth/api.js');
const marker = await apiRequest('/admin/__checks/customer-barrier', {method:'POST'});
console.log(marker.eventId); // Chỉ event ID, không in token.
```

Các endpoint `/api/admin/__checks/*` chỉ nằm trong test project và yêu cầu Admin; không có trong production API. Kiểm tra tách account cùng ID, client không tự join nhóm và JWT hết hạn đã có trong bộ backend checks; kết quả LAN bổ sung bằng chứng môi trường thật.

4. Đăng xuất trên B và xác nhận socket Hub cũ đóng trong Network. Đăng nhập tài khoản khác bằng UI, mở trang Máy và xác nhận kết nối mới cùng đúng quyền của tài khoản mới. Không có event từ kết nối cũ. Lỗi 401/403 trong reconnect được kiểm tra riêng bằng SDK thật trong `frontend/tests/realtime-sdk.test.mjs`; retry phải dừng, chỉ phiên mới mới mở kết nối mới.

## 5. Kết thúc và bàn giao

Gõ `stop` tại terminal host A; xác nhận log dọn đúng DB ngẫu nhiên. Dừng Vite, xóa biến môi trường test và đóng các tab/tài khoản thử. Bàn giao commit/nhánh, hai IP, thời điểm, browser, video/FPS, 10 dòng số đo, min/max, kết quả reconnect/restart/quyền và lỗi quan sát. Che token/connection string nếu ảnh có Console/Network.

Trạng thái được ghi nhận ngày 14/11/2026 gồm local integration đạt và Trưởng nhóm xác nhận đã thử hai máy LAN chạy ổn trong phiên review Sprint 1. Bộ số đo thô, IP và thời điểm thực hiện LAN được ghi theo bằng chứng của lần thử thực tế khi bàn giao, các artifact local giữ rõ phạm vi một máy.
