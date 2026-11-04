# SCRUM-8 — SignalR backend dùng chung

Ngày cập nhật: 04/11/2026. Task tham chiếu `S1-P-2 — Implement-SignalR-LAN-Sync` trong [backlog](../../codex/SPRINT_TASKS.md), phụ thuộc SCRUM-7/`S1-P-1`. Base: commit `c0aae97`, nhánh `SCRUM-8-implement-signal-r-lan-sync`.

Phạm vi đã duyệt: **bước 1 backend, bước 2 frontend, bước 3 kiểm chứng**, cùng phê duyệt riêng thêm `@microsoft/signalr@10.0.11` và dependency bắt buộc. Hub, thông báo máy, hook/reconnect/refetch và proxy WebSocket đã có. Người dùng xác nhận chỉ có một máy: chuẩn bị kịch bản LAN và ghi **chưa nghiệm thu hai máy LAN**. Schema giữ nguyên; login tiếp tục thuộc S1-D-2 theo quyết định trước đó.

## 1. Kết nối và xác thực

- Hub: `/hubs/realtime`. Client chính thức có thể negotiate qua `POST /hubs/realtime/negotiate?negotiateVersion=1`; WebSocket có thể kết nối trực tiếp theo protocol SignalR.
- Dùng cùng JWT/issuer/audience/signing key và thời hạn với [identity](001-monorepo.md). Không có tài khoản hoặc cơ chế cấp token riêng cho realtime.
- Header `Authorization: Bearer ...` áp dụng bình thường. Chỉ WebSocket/SSE ở đúng endpoint `/hubs/realtime` được đọc `access_token` từ query; API HTTP và negotiate không nhận query JWT. Không ghi query token vào log/proxy access log.
- Policy `realtime.connect` yêu cầu JWT hợp lệ và principal được nhận diện: nhân viên thuộc năm role hiện có, `sub` là int dương; hoặc customer có `account_type=Customer`, `role=Customer`, `customer_id` khớp `sub`.
- Thiếu/sai/hết hạn token trả `401`; token hợp lệ nhưng định danh không được hỗ trợ trả `403`. `CloseOnAuthenticationExpiration=true` đóng kết nối khi JWT hết hạn.
- Cơ chế hiện có dùng quyền trong JWT, không truy vấn lại quyền mỗi event. Sửa role/khóa tài khoản không thu hồi JWT đã cấp ngay lập tức; cần token/kết nối mới hoặc hết hạn. Không coi nhóm SignalR là cơ chế thu hồi quyền độc lập.

Tham chiếu cơ chế nền: [Microsoft — SignalR authentication/authorization](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0), [Microsoft — users/groups](https://learn.microsoft.com/en-us/aspnet/core/signalr/groups?view=aspnetcore-10.0).

## 2. Nhóm nhận do server quyết định

| Nhóm | Thành viên |
| --- | --- |
| `account:staff:<EmployeeId>` | Các kết nối của đúng nhân viên |
| `account:customer:<CustomerId>` | Các kết nối của đúng khách |
| `permission:<permission>` | Nhân viên có claim permission tương ứng trong JWT |

`staff:1` và `customer:1` là hai nhóm tài khoản khác nhau. Customer không được thêm vào nhóm quyền nhân viên dù token có claim permission. Hub không có phương thức cho client join nhóm, chọn account hoặc phát event. Khi có kết nối mới, nhóm được tính lại từ principal đã xác thực.

## 3. Sự kiện

| Tên message | Đối tượng nhận | Ý nghĩa |
| --- | --- | --- |
| `RealtimeReady` | Caller, không có arguments | Server đã thêm xong các nhóm; client có thể refetch sau khi đăng ký handler |
| `ComputerChanged` | Nhóm `permission:computer.view` | Trạng thái máy thực sự thay đổi và đã lưu thành công |

Payload `ComputerChanged` dùng cùng envelope [RealtimeEvent](../src/CyberManagement.Application/DTOs/Realtime/RealtimeEvent.cs):

```json
{
  "eventId": "8ad52160-09df-4b1b-9de5-565c33520d00",
  "type": "ComputerChanged",
  "entityId": 101,
  "occurredAtUtc": "2026-11-03T08:00:00+00:00"
}
```

`eventId` do server sinh; `entityId` là `ComputerId`; timestamp UTC là lúc tạo thông báo sau lưu. Không chứa tên/ID khách, số dư, tiền hoặc thông tin phiên. Event chỉ yêu cầu đọc lại API; client không tự cộng/trừ tiền, cập nhật tồn hoặc coi event là nguồn dữ liệu thay SQL. Event có thể đến trước HTTP response của thao tác gây thay đổi.

## 4. Publish sau commit và tích hợp owner

- [ComputerRepository](../src/CyberManagement.Infrastructure/Repositories/ComputerRepository.cs) trả `ComputerStatusUpdateResult` sau SaveChanges/commit/dispose transaction, kèm cờ `Changed`. HTTP API máy vẫn trả đúng `ComputerDto` như SCRUM-7, không lộ cờ nội bộ.
- [ComputerService](../src/CyberManagement.Application/Services/ComputerService.cs) chỉ publish khi `Changed=true`. Validation, conflict, không tìm thấy hoặc SaveChanges thất bại không phát event; retry đã đạt trạng thái đích không phát lại.
- [IRealtimePublisher](../src/CyberManagement.Application/Interfaces/Services/IRealtimePublisher.cs) thuộc Application; implementation SignalR thuộc API. Domain/Application không tham chiếu Hub hoặc HttpContext.
- Owner gọi `PublishToPermissionAsync(permission, message, cancellationToken)` hoặc `PublishToAccountAsync(RealtimeAccountType, accountId, message, cancellationToken)` **sau khi transaction của nghiệp vụ đã commit**. Account ID lấy từ dữ liệu server/claims được kiểm tra, không tin nhóm/ID nhận do client gửi. Interface này không tự commit hoặc chứng minh transaction của caller đã hoàn tất.
- Sau commit, thao tác máy dùng `CancellationToken.None` khi thông báo để việc hủy request không làm bỏ qua thay đổi đã lưu. Publisher có timeout 2 giây, ghi warning nếu gửi thất bại và giữ nguyên kết quả lưu thành công.
- Thông báo là best effort, chưa có outbox/lưu phát lại, không thay schema trong bước này. Client phải refetch khi initial connect/reconnect/`RealtimeReady`; event bị mất không được làm mất trạng thái SQL đã commit.
- Bản này dùng một API process cho demo hai máy LAN. Chưa có backplane cho nhiều API instance; không suy ra các nhóm/thông báo được chia sẻ giữa nhiều host.

## 5. Kiểm chứng và phần còn lại

Lệnh từ repository root, mặc định InMemory:

```powershell
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx --no-restore
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks --no-restore --no-launch-profile
```

Bộ checks dùng TestServer, JWT test và WebSocket thực để trao đổi protocol SignalR; không thêm package client. Các marker gửi theo đúng account được dùng để xác nhận không có event trước đó, thay vì đoán bằng sleep.

Kết quả bước 1: **173 kiểm tra HTTP/persistence/realtime đạt trên InMemory**. Bao phủ auth máy hiện có, negotiate `401/403`, WebSocket header/query token, client không tự join nhóm, nhận event theo permission, tách tài khoản trùng ID, retry không phát lại, conflict/validation/lỗi lưu không phát event, lỗi gửi không làm hỏng kết quả đã lưu, kết nối mới được phân nhóm lại và JWT hết hạn đóng kết nối.

Kiểm chứng bước 3: **182 kiểm tra đạt với SQL Server thật**, gồm migration hiện có (35 bảng nghiệp vụ/4 migrations), tranh chấp cập nhật trạng thái và Hub/post-commit/rollback/quyền. Fixture chỉ tạo, migrate và xóa DB ngẫu nhiên do chính lượt test sở hữu; không dùng DB nghiệp vụ. Bộ checks và cách cấu hình SQL nằm trong [Backend README](../README.md).

Frontend bước 2 đã có hook dùng chung, nhận `ComputerChanged`/`RealtimeReady`, reconnect/refetch, trạng thái mất/kết nối lại, dọn kết nối khi đổi token và proxy WebSocket. Hướng dẫn owner và giới hạn kiểm thử tại [frontend realtime](../../frontend/src/shared/realtime/README.md). 19 kiểm tra Node đạt, gồm SDK thật trên HTTP host cho lỗi auth, WebSocket upgrade và các kiểm tra lifecycle/API; browser realtime/máy đạt với fixtures. Suite admin-controls và 80 lượt routes đã đạt; các kết quả này không phải phép đo realtime LAN.

Kiểm thử tích hợp `node frontend/tests/run-realtime-live.mjs` cũng đã đạt: **hai tab trình duyệt trên một máy**, SDK chính thức → Vite HTTP/WebSocket proxy → Kestrel → SQL Server thật, không mock API/SDK. Lượt chạy bản cuối: 10 lần A thao tác UI và B đọc lại API/cập nhật cả dialog không F5, **44–163 ms** từ bắt đầu click đến quan sát DOM trên B (trung bình 67,9 ms). Cùng đồng hồ `performance.now()` của driver; polling DOM 40 ms nên phép đo bao gồm thời gian quan sát. Đây là số đo loopback, không phải LAN. JSON lưu cả thời gian từ HTTP response đến DOM; event có thể đến trước response nên giá trị này được chặn dưới ở 0.

Restart thực tế dừng rồi tạo lại Kestrel/Hub/service container trên cùng port và DB, xác nhận instance ID đổi. B offline trong lúc restart, A lưu thay đổi bị bỏ lỡ, B online tự reconnect/refetch và tiếp tục nhận thay đổi mới; trạng thái SQL còn nguyên. Waiter xem nhưng PATCH trả 403; customer không nhận `ComputerChanged` trước marker theo đúng account; anonymous negotiate trả 401. Proxy có `ECONNREFUSED` trong khoảng API dừng là lỗi dự kiến của ca restart, sau đó tự phục hồi. Các DB fixture đã được dọn sau cả lượt test thất bại và lượt đạt.

Bằng chứng local bản cuối: `.tools/frontend-retest/2026-11-03T17-40-19-350Z/realtime-live.json`, `realtime-live-admin.png`, `summary.json`; timestamp thư mục là UTC (04/11 theo giờ Việt Nam). Lượt đầu harness dùng sai username seed của Waiter bị 401; đã sửa thành `waiter01` và chạy lại đạt. Lượt tích hợp đạt trước đó có độ trễ 45–141 ms; lượt cuối dùng endpoint test có tiền tố `/api/admin` tương thích cấu hình proxy hiện có. Các artifact chỉ có trên máy chạy; cách tái hiện và kịch bản hai máy ở [hướng dẫn LAN](004-realtime-lan-checks.md).

**Chưa nghiệm thu:** hai máy vật lý cùng LAN, 10 lượt đo theo cùng đồng hồ đạt tối đa 2 giây và reconnect/quyền trong môi trường đó. Phần Hub/hook đã có bằng chứng local để Trưởng nhóm review; checklist LAN vẫn để mở. AI không tự quyết định Done hoặc thay duyệt cuối của Trưởng nhóm.
