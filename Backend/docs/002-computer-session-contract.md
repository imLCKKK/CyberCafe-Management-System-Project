# SCRUM-7 — Contract máy, phiên và tiền giờ

Ngày soạn: 03/11/2026. Task tham chiếu: `S1-P-1 — Implement-Computer-Queries-Session-Rules` trong [backlog](../../codex/SPRINT_TASKS.md). Nền đối chiếu: nhánh `dev`, commit `a975f8d`, CyberManagement/SQL Server 35 bảng.

**Trạng thái:** người dùng đã cho phép bước 1 (soạn contract), bước 2 (backend), `APPROVE SQL CHECK`, bước 3 (frontend) và cài dependency bằng npm ci theo lockfile hiện có. API máy đã được kiểm chứng trên SQL Server thử riêng; frontend đã qua build, render, proxy và browser test với HTTP fixtures. Người dùng chọn giữ login cho `S1-D-2`, nên chưa nghiệm thu luồng người dùng UI–API–SQL thật. Các quy tắc phiên/tiền vẫn chờ Minh/Trường xác nhận nguồn tiền/chứng từ. Không thay schema hoặc triển khai thu tiền; kiểm thử SQL chỉ áp migration hiện có vào DB thử rồi dọn đúng DB đó.

## 1. Phạm vi và nền hiện tại

- SCRUM-7 chốt quy tắc, cung cấp danh sách máy/loại máy/phòng và lưu trạng thái máy qua API; nối riêng màn hình admin máy.
- CRUD máy/loại máy, hồ sơ bảo trì, mở/đóng phiên và thu tiền thực tế thuộc `S2-P-1`; SignalR thuộc `S1-P-2`; hoàn thiện hết tiền/restart thuộc `S3-P-1`. Quy tắc các phần này được mô tả trước để các owner dùng cùng contract.
- Entity hiện có: [Computer](../src/CyberManagement.Domain/Entities/Computer.cs), [ComputerType](../src/CyberManagement.Domain/Entities/ComputerType.cs), [GamingSession](../src/CyberManagement.Domain/Entities/GamingSession.cs), [SessionCharge](../src/CyberManagement.Domain/Entities/SessionCharge.cs), [WalletTransaction](../src/CyberManagement.Domain/Entities/WalletTransaction.cs), [Invoice](../src/CyberManagement.Domain/Entities/Invoice.cs), [Payment](../src/CyberManagement.Domain/Entities/Payment.cs).
- Tại baseline trên chưa có API nghiệp vụ máy/phiên hoặc thao tác ví dùng chung. Bước 2 bổ sung API máy; bước 3 chuyển riêng admin máy từ localStorage sang API. Phiên/ví chưa triển khai; login frontend còn placeholder theo quyết định giữ `S1-D-2`. Không lấy số dư, khách hay phiên mẫu để ghép với ID máy SQL.
- Dùng `ComputerId`, `ComputerTypeId`, `RoomId`, `GamingSessionId`, `CustomerId`, `EmployeeId` kiểu `int` từ SQL. Quyền/JWT theo [contract identity](001-monorepo.md), không tạo principal hoặc quyền riêng.

## 2. Trạng thái và điều kiện chuyển

### Máy

| Từ | Sang | Điều kiện / chủ thể |
| --- | --- | --- |
| `Available` | `Maintenance` | Nhân viên có `computer.status.update`; không còn phiên chưa đóng |
| `Maintenance` | `Available` | Cùng quyền; không còn phiên chưa đóng hoặc hồ sơ bảo trì chưa kết thúc |
| `Available` | `Occupied` | Chỉ nghiệp vụ mở phiên; lưu máy và phiên trong cùng transaction |
| `Occupied` | `Available` | Chỉ nghiệp vụ đóng/hủy phiên hợp lệ, sau quyết toán; lưu cùng transaction |

`Occupied` không được đặt hoặc gỡ bằng API sửa trạng thái. Muốn bảo trì máy đang dùng phải đóng phiên trước. SCRUM-7 chỉ lưu trạng thái vận hành; không tự tạo hoặc hoàn tất bản ghi `ComputerMaintenance` thay cho nghiệp vụ bảo trì của S2.

Để chặn dữ liệu mâu thuẫn, coi một phiên là chưa đóng nếu `Status == Active` **hoặc** `EndTime == null`; cả phiên `Ended` thiếu `EndTime` cũng chặn sửa máy. Hồ sơ bảo trì có `EndTime == null` chặn chuyển máy về `Available` và mở phiên. Trạng thái máy không thuộc ba giá trị trên được trả nguyên ở truy vấn để nhận biết dữ liệu cũ, nhưng thao tác chuyển trả `409`, không tự sửa dữ liệu.

### Phiên (contract cho S2/S3)

- `Active`: `EndTime == null`, máy `Occupied`. Mỗi máy và mỗi khách tối đa một phiên chưa đóng; kiểm tra và bảo vệ tranh chấp tại SQL, không chỉ kiểm tra trước ở UI.
- Mở phiên yêu cầu quyền `session.start`, khách `Active`, máy `Available`, không có bảo trì chưa kết thúc, giá giờ dương và ví đủ trả ít nhất một giây theo mục 3. Lưu giờ bắt đầu UTC, snapshot giá và người mở; không thu tiền trước thời gian đã dùng.
- `Ended`: chốt `EndTime`, thu phần còn thiếu, ghi chứng từ và trả máy về `Available` trong cùng transaction. Kết thúc do nhân viên có `session.end` hoặc tác vụ hết tiền của server; hết tiền được lưu lý do riêng, không tự thêm trạng thái máy.
- `Cancelled`: chỉ nhân viên có `session.cancel`, có lý do, chưa phát sinh thời gian tính phí, khoản thu hoặc chứng từ tiền; lưu `EndTime` và trả máy về `Available`. Phiên đã phát sinh phí dùng luồng kết thúc/điều chỉnh được duyệt, không xóa khoản thu.
- Đóng/hủy lại cùng yêu cầu trả kết quả đã lưu; không đổi `EndTime` hoặc tạo thêm khoản thu. Yêu cầu khác loại với kết quả đã chốt trả `409`.
- Quyền `session.pause` đã có trong catalog nhưng model chưa lưu khoảng tạm dừng. Contract này chưa cung cấp pause/resume; không nhận trạng thái `Paused` hoặc tự trừ thời gian nghỉ.
- Theo identity hiện tại, khách `Locked` không được trừ ví. Nếu khóa khách đang chơi, cần quyết toán trước khi khóa trong transaction chung; trường hợp đã bị khóa nhưng còn phiên phải báo đối soát, không tự vượt kiểm tra khóa. Minh cần xác nhận luồng này trước S2.

## 3. Công thức và thời điểm thu

Dùng số thập phân chính xác phía server; timestamp lưu UTC và trả ISO 8601 có múi giờ. UI hiển thị giờ Việt Nam, không gửi thời lượng hoặc tiền do trình duyệt tự tính để ghi sổ.

Với `P` là giá VND/giờ chụp lúc mở phiên, `t` là thời điểm quyết toán:

```text
s(t) = max(0, floor((t - StartTime).TotalSeconds))
C(t) = ceil(P * s(t) / 3600)                 // Tổng phải thu, đơn vị đồng
D(t) = C(t) - tổng SessionCharge đã commit   // Khoản thu thêm
```

- Tính theo giây hoàn tất, không làm tròn lên phút; dưới một giây là 0. Làm tròn tiền lên một đồng trên tổng tích lũy, không làm tròn độc lập từng khoảng rồi cộng. Không có phí tối thiểu khác; `P > 0`, đủ trong giới hạn decimal của SQL. Giá sửa sau đó chỉ áp dụng phiên mới.
- Đề xuất tác vụ thu mỗi 60 giây tính từ `StartTime`; thu bổ sung khi đóng, trước một thao tác debit ví khác và khi phục hồi. Tác vụ trễ dùng thời gian thực tế, không giả định mỗi lần chạy đúng 60 giây.
- `D > 0`: thu đúng chênh lệch trong transaction mục 5. `D == 0`: không thêm ledger 0 đồng; có thể tiến mốc đã xử lý khi trạng thái còn nhất quán. `D < 0`: dừng và báo lệch sổ, không tự hoàn tiền.
- `TotalCost` là tổng khoản thu đã commit, bằng tổng `SessionCharge.Amount`; `LastChargedAt` là mốc thời gian đã quyết toán. Giá ước tính trên UI không ghi đè hai trường này.
- Phí phiên mới là số nguyên VND. Schema ví hiện cho phép hai chữ số thập phân: không tự làm tròn/xóa số dư cũ; chỉ dùng `floor(WalletBalance)` làm ngân sách phí nguyên đồng. Quy ước tiền lẻ này cần Minh xác nhận.

Ví dụ `P = 10.000`: tổng phí tại 0/60/120/180 giây là 0/167/334/500 đồng; ba khoản thu là 167/167/166 đồng. Một giờ luôn 10.000 đồng dù tác vụ chạy trễ hoặc được gọi lại.

## 4. Hết tiền và restart

- Gọi `A` là tổng tiền phiên đã thu, `B = floor(WalletBalance)` là số dư khả dụng hiện tại sau khi khóa dữ liệu ví. Số giây tối đa còn được tài trợ kể từ đầu phiên là `sMax = floor((A + B) * 3600 / P)`.
- Nếu số giây cần quyết toán vượt `sMax`, chốt `EndTime = StartTime + sMax giây`, thu `C(EndTime) - A` rồi kết thúc. Không tính phí vượt ngân sách hoặc để ví âm. Nếu các mốc hiện có không phù hợp công thức, rollback và báo đối soát.
- Hết tiền là không đủ cho giây tính phí tiếp theo; số dư có thể còn tiền nhỏ hơn phí giây tiếp theo. Không bắt buộc trừ ví về đúng 0. Nạp sau khi phiên đã kết thúc không tự mở lại phiên.
- Mọi debit dùng chung ví (đơn, phí giải...) phải quyết toán phiên đang mở đến thời điểm server trước khi xét số dư cho khoản chi mới. Tất cả theo cùng khóa/thứ tự và transaction; thiếu tiền cho khoản chi mới thì rollback toàn bộ request, tác vụ phiên sẽ quyết toán lại độc lập. Minh/Khang cần nhận contract này; chỉ chạy tác vụ 60 giây không đủ ngăn hai luồng tiêu cùng tiền.
- Restart đọc lại phiên, snapshot giá, khoản thu và mốc từ SQL. Đề xuất thời gian phiên vẫn chạy trong lúc API dừng; quyết toán bù đến hiện tại nhưng luôn giới hạn tại `sMax`. Không lấy giờ khởi động làm giờ bắt đầu và không thu lại các khoảng đã commit.
- Trước khi nhận thao tác ảnh hưởng máy/ví, phục hồi phiên liên quan và hoàn tất quyết toán theo cùng kiểm soát đồng thời. Lỗi SQL thì báo lỗi/giữ trạng thái chờ phục hồi, không tự giải phóng máy hoặc dùng dữ liệu demo.
- Đóng tab/mất kết nối không kết thúc phiên. Giờ đóng do server nhận và lưu được quyết định; request chưa commit không được coi là đã đóng. Lệnh retry phải dùng lại khóa nghiệp vụ; lệnh đã commit trả lại kết quả cũ.
- Đây là vòng đời phiên trên web, không phải bằng chứng đã khóa máy Windows. Thông báo realtime chỉ phát sau commit ở task SignalR.

## 5. Nguồn tiền, transaction và chứng từ cần đối chiếu

| Dữ liệu / thao tác | Contract đề xuất |
| --- | --- |
| Nguồn số dư | `Customers.WalletBalance` và `WalletTransactions` trong SQL; không lấy ví từ browser |
| Gọi debit | Nhận `CustomerId`, số tiền server tính, nguồn `GamingSession`/ID khoản thu, khóa nghiệp vụ và thời điểm UTC; dùng chung transaction của caller, không tự commit bên trong |
| Một khoản thu | Ghi `SessionCharge`, ledger, cập nhật ví/`TotalCost`/`LastChargedAt` nguyên tử; lỗi giữa chừng rollback tất cả |
| Chống lặp | Khóa nghiệp vụ bền vững theo phiên và mốc kết thúc khoản thu; cùng khóa/cùng nội dung trả kết quả cũ, khác nội dung trả xung đột; cần ràng buộc unique tại SQL |
| Cạnh tranh | Mở/đóng phiên, tác vụ thu, debit khác và sửa trạng thái phải khóa dữ liệu chung theo thứ tự thống nhất; chạy nhiều API instance vẫn chỉ ghi một lần |
| Quy ước ledger | Đề xuất `Type = GamingCharge`, `Amount = -D`, `BalanceAfter` bằng số dư sau thu; cần Minh xác nhận dấu và tên loại trước khi code |
| Chứng từ mỗi lần thu | `SessionCharge` là nguồn phí gắn trực tiếp với ledger; không dựa vào nội dung `Description` để truy vết/chống lặp |
| Hóa đơn kết thúc | Đề xuất một hóa đơn tiền giờ cho phiên có tổng phí dương, dòng chi tiết theo snapshot; `Payment` liên kết các ledger đã thu. Lập hóa đơn không debit lại ví; phiên 0 đồng không sinh ledger/payment tiền |
| Đối soát | Tổng phí phiên = tổng khoản thu = tổng debit tiền giờ quy về số dương = tổng payment tiền giờ = tổng hóa đơn tiền giờ; món/hoàn tiền đối chiếu theo nguồn riêng, không cộng lại vào phí giờ |
| Doanh thu | Nạp ví không là doanh thu tiền giờ. Trường xác nhận thời điểm ghi nhận và cách thể hiện khoản đã thu khi phiên còn mở để tránh đếm cả ledger và hóa đơn |

Khi đóng có thu bổ sung, khoản thu cuối, kết thúc phiên, trạng thái máy và hóa đơn/payment phải cùng transaction. Nếu tất cả phí đã thu trước đó, transaction đóng chỉ lập chứng từ/thay trạng thái, không thu lại. Hóa đơn tự đóng cần actor hợp lệ vì `Invoice.EmployeeId` bắt buộc: đề xuất lưu người mở để làm actor khi hết tiền; Trường phải xác nhận trước triển khai, không tự dùng tài khoản admin/ID giả.

Các thiếu hụt schema hiện đã xác minh: `GamingSession` chưa có snapshot giá/người mở/lý do đóng; `SessionCharge` chưa liên kết ledger hoặc có khóa chống lặp; `WalletTransaction` chưa có định danh nguồn/khóa nghiệp vụ; chưa có ràng buộc duy nhất phiên chưa đóng theo máy/khách hay hóa đơn tiền giờ theo phiên. **Chỉ ghi yêu cầu dữ liệu tại đây; tên cột, cơ chế khóa và migration cần đề xuất/duyệt riêng ở S2 cùng Minh/Trường.** SCRUM-7 không thay schema hoặc coi các bảo đảm tiền này đã được thực thi.

## 6. Contract API cho phần máy của SCRUM-7

Các endpoint sau đã có backend ở bước 2, được kiểm chứng trên SQL Server thử riêng và có caller frontend ở bước 3. Luồng người dùng hoàn chỉnh vẫn phụ thuộc login. JSON dùng camelCase, danh sách trả mảng (rỗng là `[]`). Request chỉ sử dụng trạng thái đích/dự kiến; các trường giá, tiền, role, danh tính và thuộc tính máy khác không được dùng để ghi dữ liệu.

| Endpoint | Permission | Response `200` |
| --- | --- | --- |
| `GET /api/admin/computers` | `computer.view` | `{ computerId, computerName, roomId, computerTypeId, status, hasOpenSession, hasOpenMaintenance }[]` |
| `GET /api/admin/computer-types` | `computer.view` | `{ computerTypeId, typeName, cpu, ram, gpu, pricePerHour, description }[]` |
| `GET /api/admin/computer-rooms` | `computer.view` | `{ roomId, roomName, floor, description }[]` |
| `PATCH /api/admin/computers/{computerId}/status` | `computer.status.update` | Bản ghi máy cùng cấu trúc GET, sau commit |

`cpu`, `ram`, `gpu`, `description` cho phép null theo entity. Danh sách máy sắp theo `roomId`, `computerName`, `computerId`; loại/phòng sắp theo ID tăng dần. Search/lọc phòng/loại/trạng thái ở UI trên bộ dữ liệu này; chưa thêm phân trang hoặc query filter. Các cờ phiên/bảo trì chỉ phản ánh tồn tại dữ liệu, không lộ ID khách, tên khách, số dư, thời điểm/tiền phiên cho người chỉ có `computer.view`.

Request đổi trạng thái:

```json
{ "status": "Maintenance", "expectedStatus": "Available" }
```

- ID dương; cả hai trường bắt buộc, nhận chính xác `Available` hoặc `Maintenance`. Giá trị `Occupied`/trạng thái khác trả `400`. Không cho phép sửa tên/phòng/loại qua endpoint này.
- Đọc máy và kiểm tra phiên/bảo trì theo mục 2 trong cùng transaction được bảo vệ với nghiệp vụ mở/đóng tương lai. Nếu trạng thái hiện tại khác `expectedStatus`, trả `409`; UI đọc lại, không tự ghi đè thay đổi của người khác.
- Retry mà trạng thái hiện tại đã bằng trạng thái đích và các điều kiện an toàn vẫn đúng trả `200`, không ghi thêm thay đổi. So sánh trạng thái này không phát hiện chuỗi đổi đi rồi đổi lại; nếu sau này cần phát hiện mọi phiên bản thì đề xuất concurrency token riêng, không tự thêm cột trong SCRUM-7.
- Implementation SQL Server dùng transaction `Serializable`, đọc máy với `UPDLOCK, HOLDLOCK` trước khi kiểm tra trạng thái và các phiên/bảo trì liên quan. Nghiệp vụ mở/đóng phiên và bảo trì ở S2 phải cùng khóa máy trước khi sửa dữ liệu; không coi bảo đảm này đã áp dụng cho writer chưa triển khai. Nhánh InMemory chỉ phục vụ kiểm tra chức năng, không xác minh khóa/rollback SQL.
- Nhân viên được xét theo permission của JWT; theo seed hiện tại, năm role đều xem được, Admin/Manager/Technician đổi trạng thái được. Customer không có quyền các endpoint này.

| Tình huống | HTTP / xử lý |
| --- | --- |
| Thiếu token, token sai/hết hạn | `401`; yêu cầu đăng nhập lại, không retry ghi tự động |
| Có token nhưng thiếu permission | `403`; thông báo không có quyền |
| Request sai kiểu/thiếu trường/ID không dương/trạng thái không cho phép | `400` |
| ID máy hợp lệ nhưng không tồn tại | `404` |
| Phiên/bảo trì chưa kết thúc, trạng thái mâu thuẫn, dữ liệu đã thay đổi | `409`; giữ nguyên dữ liệu và đọc lại |
| SQL hoặc lỗi nội bộ | `500` với ProblemDetails chung, không lộ SQL/connection string |

Lỗi nghiệp vụ dùng ProblemDetails theo [ApiExceptionHandler](../src/CyberManagement.API/ExceptionHandling/ApiExceptionHandler.cs). `401/403` do middleware có thể không có body; API client vẫn phải xử lý được. Không trả thành công trước commit.

## 7. Yêu cầu UI và kiểm chứng ở các bước tiếp theo

- Danh sách, phòng, loại, giá và trạng thái trong trang máy đều đọc API. Loading/lỗi/empty có phân biệt; GET lỗi không fallback localStorage; PATCH đang chờ khóa thao tác trùng, chỉ báo lưu thành công sau response.
- Bỏ hiển thị khách/phiên mẫu và cảnh báo đơn dựa trên ID demo trong trang máy SQL. Chi tiết phiên thật chỉ bổ sung khi có API `session.view`. Không thay nguồn dashboard/reports/Home trong SCRUM-7; giữ store demo cũ cho các màn hình chưa chuyển và không quảng bá chúng là đã đồng bộ SQL.
- Tạo/sửa tên/phòng/loại máy chưa có API trong task này; UI phải thể hiện chưa hỗ trợ, không tiếp tục lưu localStorage rồi báo thành công trên trang SQL.
- Login/API client dùng contract của `S1-D-2`. Frontend hiện chưa có luồng này; cần bàn giao từ task phụ thuộc hoặc duyệt mở rộng riêng, không dùng token hard-code/hộp nhập token làm luồng người dùng.
- Bước backend: kiểm tra quyền từng endpoint; dữ liệu đọc đúng SQL; trạng thái sai/không tìm thấy/phiên mâu thuẫn/bảo trì; hai request cạnh tranh; lưu rồi đọc bằng DbContext mới. Kiểm tra SQL trên DB thử được duyệt; InMemory không chứng minh transaction SQL.
- Bước frontend: loading/empty/GET lỗi/PATCH lỗi; người chỉ xem không sửa được; lưu rồi reload lấy trạng thái SQL; không xuất hiện dữ liệu mẫu khi lỗi. Build frontend/backend và tự rà theo [REVIEW_GUIDE](../../codex/REVIEW_GUIDE.md).
- S2/S3 cần kiểm tra công thức tại 0/dưới 1/1/60/120/180/3600 giây, snapshot giá, số dư sát ngưỡng, hết tiền, rollback, double click/retry, nhiều instance, debit khác tranh ví, crash trước/sau commit, restart qua thời điểm hết tiền và đóng lặp không nhân đôi chứng từ.

## 8. Xác nhận và trạng thái nghiệm thu

| Nội dung | Trạng thái / người cần xác nhận |
| --- | --- |
| Soạn contract bước 1 | Đã được người dùng cho phép ngày 03/11/2026 |
| Backend bước 2 | Đã được phép và triển khai API máy/loại/phòng, lưu trạng thái, DI/policy và kiểm thử; schema giữ nguyên |
| Công thức, chu kỳ thu, giới hạn hết tiền, tính giờ lúc API dừng | Phương án ở mục 3–4; chờ Phúc/Minh/Trường đối chiếu và Trưởng nhóm duyệt nghiệp vụ |
| Số dư khả dụng, quy ước ledger, transaction chung và khóa khách đang có phiên | Chờ Minh; các luồng chi ví khác cần cùng contract |
| Chứng từ, actor tự đóng, doanh thu và yêu cầu migration | Chờ Trường phối hợp Minh/Phúc, Trưởng nhóm duyệt |
| Kiểm tra backend | Build solution đạt, 0 warning/error; 129 kiểm tra HTTP/persistence đạt trên InMemory và 138 đạt trên SQL Server (gồm auth và máy); 8 request đổi trạng thái đồng thời đều trả 200, không deadlock và trạng thái cuối đúng |
| Fixture SQL đã được duyệt | Đã tạo DB riêng `CyberManagement_AuthChecks_a128bf9faa18483d932d62edc5bc4bf9`, áp 4 migration hiện có, xác nhận 35 bảng nghiệp vụ, chạy checks và xóa DB thử thành công; exit code 0 |
| Giới hạn lần chạy SQL | Lần đầu lỗi hỗ trợ mã hóa trong sandbox trước khi tạo DB; chạy lại ngoài sandbox với cùng cấu hình kết nối đã pass. Kết quả cạnh tranh ở đây chỉ bao phủ đổi trạng thái, chưa kiểm chứng nghiệp vụ phiên/tiền hoặc fault injection giữa transaction |
| Frontend bước 3 | Đã nối danh sách/phòng/loại và lưu trạng thái qua API, xử lý quyền/loading/lỗi/reload, bỏ ghép dữ liệu demo trong trang máy; build production đạt, 10 kiểm tra API client/proxy đạt |
| Kiểm chứng giao diện | Ba script render đạt; browser suite computers-api và admin-controls đạt; routes đạt 80 lượt trên 5 kích thước, 4 redirect và 3 placeholder đã biết. Đã xem ảnh dialog máy ở 390/320px |
| Dependency/môi trường frontend | npm ci cài 71 package theo lockfile, package.json/package-lock.json không đổi. Lần đầu npm bị EACCES và Chrome crash trong sandbox; chạy ngoài sandbox thành công. Lỗi selector/đồng bộ đóng dialog trong script test đã sửa, suite máy lần cuối đạt |
| Giới hạn frontend | Browser dùng HTTP fixtures, chưa chạy luồng UI–API–SQL thật hoặc login thật; login giữ ở S1-D-2. Render còn cảnh báo SSR useLayoutEffect ở Home/Tournaments; ảnh 320px có scrollbar ngang do body min-width 320px hiện có. Không sửa các phần nền ngoài phạm vi |
| Hướng dẫn tích hợp | [Frontend máy và bàn giao login](../../frontend/src/admin/computers/README.md); lệnh checks/cấu hình SQL theo [Backend README](../README.md) |
| Hai tiêu chí Done của SCRUM-7 | Chưa đánh dấu đạt; còn xác nhận liên module và bằng chứng tích hợp |

Kết quả bước 1 là tài liệu contract, không phải báo cáo xác nhận owner đã thống nhất hoặc task đã Done. Các xác nhận sau này phải ghi người, ngày và quyết định thực tế; AI không tự thay mặt Minh/Trường hoặc Trưởng nhóm.
