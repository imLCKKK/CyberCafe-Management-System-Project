# Backlog Jira — 55 Task, chủ yếu 2–3 Task/người/sprint

Cập nhật nền backend/DB ngày 02/11/2026; giữ lịch và phân công ngày 31/10/2026. Đã gom 100 việc nhỏ thành **53 Task Jira độc lập**, giữ tiêu chí trong checklist, cộng **2 Task UI giao Phúc theo yêu cầu mới**, tổng cộng **55 Task**. Mỗi thẻ có tên tiếng Anh nối bằng dấu gạch ngang (ví dụ `Build-Admin-Client-Login`), Description tiếng Việt, Dependencies và Done khi để copy lên Jira. Vòng quay vẫn thuộc Task của Khang, không tạo thẻ trùng. Checklist không phải Sub-task và không tạo thêm issue. Phạm vi theo [kế hoạch bàn giao](../planning/team-delivery-plan.md). VNPay trong ví dụ chỉ là mẫu trình bày; cổng của dự án vẫn là **ZaloPay sandbox chính thức**.

**Reviewer duy nhất: Trưởng nhóm**, người yêu cầu lập kế hoạch và trực tiếp duyệt thay đổi. Chưa có tên/account reviewer để ánh xạ Jira; không tự gán cho thành viên khác. Các tên trong “Phối hợp” hỗ trợ nghiệp vụ/tích hợp. Trước khi làm đọc [PROJECT_GUIDE.md](PROJECT_GUIDE.md); khi review dùng [REVIEW_GUIDE.md](REVIEW_GUIDE.md).

| Sprint | Thời gian năm 2026 | Đại | Minh | Phúc | Khang | Trường | Tổng Task |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| S1 | 31/10–05/11 | 3 | 2 | 4 | 2 | 2 | 13 |
| S2 | 06–10/11 | 4 | 3 | 4 | 3 | 4 | 18 |
| S3 | 10–15/11 | 3 | 3 | 3 | 2 | 3 | 14 |
| S4 | 15–19/11 | 2 | 2 | 2 | 2 | 2 | 10 |
| Tổng | Dự phòng 20–21/11 | 12 | 10 | 13 | 9 | 11 | 55 |

Ngày **10/11** và **15/11** được nhóm dùng làm ngày bàn giao chung của hai sprint liền kề: Task hoàn tất của sprint trước được review/bàn giao để sprint sau tích hợp; một Task vẫn chỉ thuộc một sprint.

Hai Task UI **S1-U-1** và **S2-U-1** chuyển từ Trưởng nhóm sang **Phúc**; giữ mã tham chiếu để không đứt liên kết đã dùng. Chữ U là mã lịch sử của phần UI, không còn chỉ người thực hiện. Trưởng nhóm review cuối; các owner kiểm tra chéo luồng bị ảnh hưởng. Không thêm thành viên hoặc Task mới.

### Đối chiếu các yêu cầu frontend mới

| Phản hồi của Trưởng nhóm | Xử lý trong backlog |
| --- | --- |
| Logo không thống nhất, đổi xanh lá sang xanh–tím–đen | **S1-U-1**, Phúc làm; NEXORA CYBER, theme dùng chung và logo theo concept nhóm chốt |
| Món ăn chưa có ảnh cụ thể | **S2-U-1**, Phúc làm ảnh/component theo theme mới; Khang phối hợp dữ liệu sản phẩm |
| Vòng quay random chọn món chưa tốt | Đã thuộc phần gợi ý món của Khang; bổ sung tiêu chí trải nghiệm vào **S2-K-3**, hồi quy trong **S3-K-2**, giữ số thẻ của Khang |

Qua đọc code: admin dùng “NET CAFE”, client dùng “TRAM”; `FoodArt` và vòng quay còn lấy emoji từ `productArt`. Đây là căn cứ cho hai Task bổ sung. Các nhận xét “khó nhìn” và “vòng quay chưa tốt” cần ghi ví dụ/ảnh trước–sau khi thực hiện, không tự coi mọi vấn đề thị giác là lỗi đã tái hiện. Auth/API/giá/tồn/thuật toán gợi ý vẫn thuộc owner hiện tại.

Các ngoại lệ 4 Task/người/sprint có lý do bàn giao cụ thể:

- **Đại có 4 Task:** nhân viên; giải/đăng ký; UI voucher; UI đề xuất đội ML. Danh sách đăng ký phải sẵn sàng cho Phúc trước khi ghép UI ML; voucher dùng API Trường, nên không gộp tất cả thành một thẻ chờ nhiều phía.
- **Trường có 4 Task:** kho; chứng từ; rule voucher; báo cáo đối chiếu. API tồn/chứng từ/voucher phải bàn giao trước khi Khang/Phúc tích hợp; báo cáo đối chiếu chờ giao dịch thật từ các module đó. Gộp các bước này thành một thẻ sẽ làm mờ phụ thuộc và tăng kích thước lượt review.
- **Phúc có 4 Task ở S1 và S2:** thêm nhận diện và ảnh món theo phân công mới. Giữ UI riêng với ML/realtime để review đúng phạm vi. S1 bàn giao contract máy/ML và hook realtime sớm, triển khai token/UI trong S1-U-1, chốt asset logo sau quyết định concept. S2 bàn giao component ảnh S2-U-1 sớm cho Khang; không đợi hoàn tất model mới bắt đầu UI, không nhận thêm logic vòng quay/checkout.

Số thẻ không phải ước lượng thời gian. Gom thẻ giảm số lần nghiệm thu riêng, không tự giảm khối lượng code. Phúc vẫn phụ trách ML chia đội; đăng nhập vẫn thuộc Đại ở S1; Trưởng nhóm duyệt cuối.

## Quyết định nền 35 bảng — 02/11/2026

Trưởng nhóm chọn **CyberManagement, database SQL Server mới 35 bảng**. Dùng `Backend/CyberManagement.slnx`; API tại `Backend/src/CyberManagement.API`, entity tại Domain, use case/interface tại Application, DbContext/config tại `Infrastructure/Persistance`, migrations tại `Infrastructure/Migrations`. Giữ đúng chữ hoa `Backend` trong import/lệnh.

Định danh đích: `Employees.EmployeeId` cho staff và `Customers.CustomerId` cho khách, kiểu int; không có bảng Account riêng. Mapping account/customer trong demo phải được chủ module đối chiếu với [hợp đồng auth](../Backend/docs/001-monorepo.md). Giữ transaction và rule nghiệp vụ trong các checklist bên dưới; có 35 entity không chứng minh API/voucher/ví/kho/ML đã hoàn thành.

Giữ đủ 55 Task, tên, owner, sprint và checklist nghiệp vụ. Mỗi owner giữ trọn module xuyên các tầng. Không tạo Task mới cho từng bước sửa review; không tự đổi trạng thái Jira/Done. Những task đã tạo giữ key/lịch sử; cập nhật kỹ thuật cần thiết theo [hướng dẫn backend](../Backend/README.md).

Đích là DB mới: bốn migration hiện có không phải đường nâng cấp tự động từ DB CyberCafe 19 bảng. Không sửa migration đã áp ở DB chung; không chạy reset/xóa DB cũ. Phần chuyển dữ liệu cũ nếu phát sinh cần phạm vi riêng. SQL/DBML mô tả tiếp theo phải theo schema 35 bảng này.

Các task backend dùng nền mới sau khi Trưởng nhóm review/tích hợp; điền link commit/PR thực tế khi có. UI theme/asset và thiết kế contract có thể tiếp tục độc lập. Auth/seed phải kiểm tra SQL, ma trận quyền, role ngoài catalog và seed lặp; frontend giữ OP.GG. EF dùng `--project Backend\src\CyberManagement.Infrastructure --startup-project Backend\src\CyberManagement.API`.

## Cách nhận việc và review

1. Mỗi tiêu đề có mã dạng **S1-D-1** là một thẻ Jira độc lập. Copy nguyên tên tiếng Anh nối bằng dấu gạch ngang sau dấu “—” vào **Summary**, không đưa mã kế hoạch vào tên Jira. Người phụ trách và reviewer ghi ở đầu nhóm, sprint ghi ở đầu phần; chọn các trường này trên Jira, không lặp lại trong Description. Jira key thực tế chỉ ghi sau khi tạo issue.
2. Copy **Description**, **Dependencies** và **Done khi** vào nội dung Jira; giữ cả căn cứ/ranh giới nếu thẻ có. Description là phạm vi cần làm, Done khi là kết quả cần chứng minh. Chỉ tick khi có bằng chứng; không tạo Sub-task hoặc lượt review riêng cho từng dòng.
3. Có thể chia nhiều commit/PR nhỏ cùng tham chiếu một Task khi cần bàn giao code dùng chung sớm. Ghi mốc/PR đã cung cấp để người phụ thuộc tích hợp; Task chỉ Done khi đủ checklist và được Trưởng nhóm duyệt, không đợi cuối sprint mới đẩy toàn bộ code.
4. Gửi **kết quả review tổng hợp theo Task trực tiếp trong chat**, theo mẫu tin nhắn ở mục 6 của [REVIEW_GUIDE.md](REVIEW_GUIDE.md), để Trưởng nhóm copy gửi người làm: lỗi/thiếu sót, hướng sửa và kết quả cần kiểm tra; nêu ngắn diff/PR và bằng chứng liên quan. Chỉ tạo file báo cáo khi người dùng yêu cầu rõ. Lượt sau tập trung vào phần đã sửa; không buộc Trưởng nhóm đọc lại toàn bộ phần không đổi.
5. Các phụ thuộc là điều kiện để hoàn tất tích hợp. Có thể phát triển theo contract đã chốt trước khi chúng Done, nhưng phải có bằng chứng tích hợp thật để nghiệm thu. Khi đã tạo issue, ánh xạ mã kế hoạch sang Jira key và liên kết “is blocked by” phù hợp; không dùng Parent.
6. Workflow: To Do → In Progress → Review → Done, ánh xạ vào board hiện có. Mỗi người tập trung một luồng chính; chỉ tách thêm thẻ khi có kết quả độc lập hoặc blocker riêng cần theo dõi, không tách vì mỗi bước mất một ngày.
7. Tên Task dùng dạng `Build-Admin-Client-Login` để dùng trực tiếp khi tạo branch từ Jira theo flow của nhóm. Không cần trường Branch riêng trong backlog hoặc Description. Giữ mã kế hoạch để tra phụ thuộc trong tài liệu; ánh xạ với key thật sau khi tạo issue.

**Tình trạng:** người dùng đã tạo sprint task; repo chưa có bảng ánh xạ đầy đủ Jira key/trạng thái từng Task. Lần sửa này chỉ cập nhật repo, không thao tác Jira. Chưa có xác nhận trạng thái thực hiện từng Task. Nếu nhóm đã tự tạo issue theo bản cũ, dùng dòng “Gom từ mã cũ” để đối chiếu, chuyển checklist/link về thẻ giữ lại rồi xử lý thẻ trùng theo quyết định Trưởng nhóm; không tự xóa issue hoặc lịch sử.

### Mẫu Description

```text
Summary: Implement-ZaloPay-Gateway-Callbacks
Mã tham chiếu: S2-M-2
Description: [copy các việc cần làm của Task]
Dependencies: [copy mã phụ thuộc và bổ sung link contract/PR thực tế]
Done khi: [copy nguyên checklist nghiệm thu của Task]
PR/commit: [các thay đổi thuộc cùng Task]
Kiểm tra và bằng chứng: [lệnh, kết quả, log/ảnh đã che secret]
Chưa đạt / blocker:
Quyết định của Trưởng nhóm: [chỉ ghi khi đã có]
```

Task triển khai phải chạy thật trong phạm vi của nó; Task thiết kế phải có tài liệu được duyệt. Test/fixture không thay thế bằng chứng SQL, LAN, sandbox hoặc ML được yêu cầu. Checklist chưa đạt thì Task còn mở, kể cả đã có một phần code được tích hợp.

**Điều kiện Done chung, áp dụng cho cả 55 thẻ:** đủ các tiêu chí riêng bên dưới; có PR/diff hoặc tài liệu bàn giao, lệnh/kết quả kiểm tra phù hợp và bằng chứng để tái hiện; phần chưa kiểm tra/blocker được ghi rõ; Trưởng nhóm đã duyệt. Khi copy lên Jira, giữ điều kiện này cùng checklist riêng. AI/người làm chỉ báo sẵn sàng review, không tự coi test pass là đã được duyệt. Dependencies dùng các mã đang tồn tại trong backlog và contract do chúng bàn giao; không chép mã P2/BR hay “API Contract v4.0” từ ví dụ khi dự án chưa có tài liệu đó.

### Nhận diện NEXORA CYBER cho các Task UI

- Nhóm xác nhận concept cụ thể **chiều 31/10/2026**, hiện chưa chọn Concept 1/2/3. Phúc ghi concept và nguồn asset đã duyệt vào S1-U-1 trước khi hoàn tất phần logo; có thể làm token/layout trước.
- Theo bảng màu trong ảnh người dùng gửi: nền đen `#0B0F14`, tím `#A855F7`, cyan `#22D3EE`, chữ sáng `#E5F0FF`. Dùng token chung, chữ nội dung rõ; không phủ hiệu ứng neon làm khó đọc bảng/form.
- Thay xanh lá ở **màu thương hiệu**, không thay hàng loạt màu success/warning/error hoặc dữ liệu biểu đồ. Màu trạng thái phải có nhãn/icon để không chỉ dựa vào màu.
- Bản logo trên header/sidebar, favicon và màn hình auth phải cùng nhận diện. Hình đính kèm có ba concept tham khảo, không phải bằng chứng rằng đã có asset logo cuối trong repo. Không tự cắt cả bảng concept làm logo sản phẩm.

## Sprint 1 — 31/10–05/11: nền tảng và hợp đồng

Có login admin/client nối API, dữ liệu SQL và một thay đổi máy chạy qua LAN; chốt contract, dữ liệu ML và điều kiện ZaloPay. Ưu tiên bàn giao hợp đồng sớm cho người cùng làm.

### Đại — Auth, quyền và hợp đồng giải đấu

Assignee: **Đại**. Reviewer: **Trưởng nhóm**. Phối hợp: Minh; Phúc đối chiếu hợp đồng giải/ML.

#### S1-D-1 — Implement-Auth-API-Roles

**Description:**

- Chốt định danh account/customer/staff, ma trận quyền và cơ chế xác thực dùng chung.
- Triển khai login/logout, seed tài khoản theo vai trò và kiểm tra quyền tại API; kiểm thử đăng nhập sai và truy cập trái quyền.

**Dependencies:** Không có thẻ chặn được xác định.

**Done khi:**

- [ ] **Chốt tài khoản, vai trò và định danh dùng chung.** Có mapping account/customer/staff, ma trận quyền và quy tắc khóa; Minh/Trường đối chiếu hợp đồng để các module dùng cùng ID.
- [ ] **Xây API đăng nhập, đăng xuất và xác thực.** Tài khoản seed đăng nhập được; mật khẩu sai và request không đủ quyền bị từ chối tại API; logout xử lý theo cơ chế đã chọn.
- [ ] **Kiểm tra seed và auth trên nền 35 bảng.** Seed lặp không tạo bản ghi thừa, giữ quyền role ngoài catalog; login/quyền chạy trên SQL Server mới. Signing key/mật khẩu cấu hình ngoài Git; build và hướng dẫn chạy được bàn giao.

**Gom từ mã cũ:** `S1-D-01`, `S1-D-02`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S1-D-2 — Build-Admin-Client-Login

**Description:**

- Thay placeholder đăng nhập admin/client bằng form gọi API và API client dùng chung.
- Xử lý loading, lỗi, hết phiên và điều hướng theo quyền; bàn giao cấu hình và ca kiểm tra để các module nối auth.

**Dependencies:** `S1-D-1`, `S1-D-3`

**Done khi:**

- [ ] **Nối login admin/client và API client chung.** Hai màn hình login gọi API thật; có loading/lỗi/phiên hết hạn; không tự cấp quyền từ localStorage; hướng dẫn cấu hình API cho nhóm.
- [ ] **Kiểm tra quyền và bàn giao auth cho các module.** Có kết quả login hợp lệ/sai, gọi trực tiếp endpoint khác quyền, cấu hình seed và cách dùng API client; reviewer tái hiện được.

**Gom từ mã cũ:** `S1-D-03`, `S1-D-05`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S1-D-3 — Define-Staff-Tournament-Contracts

**Description:**

- Đặc tả endpoint, DTO, trạng thái và lỗi cho nhân viên, đổi mật khẩu, giải đấu, đăng ký và đội.
- Chốt policy phí giải, mapping UI/DBML và dữ liệu đăng ký đầu vào cho Phúc; ghi quyết định để các owner cùng triển khai.

**Dependencies:** `S1-D-1`

**Done khi:**

- [ ] **Chốt API nhân viên, đổi mật khẩu và giải đấu.** Có endpoint/DTO/lỗi/trạng thái cho nhân viên, giải, đăng ký và đội; chốt phí giải UI/DBML với nhóm, cung cấp danh sách đăng ký cho Phúc.

**Gom từ mã cũ:** `S1-D-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Minh — Khách hàng, ví và điều kiện ZaloPay

Assignee: **Minh**. Reviewer: **Trưởng nhóm**. Phối hợp: Đại; Trường đối chiếu ledger/giao dịch tiền.

#### S1-M-1 — Implement-Customer-Wallet-Queries

**Description:**

- Chốt contract credit/debit/refund, ledger, chống xử lý lặp và thời điểm thu tiền với Phúc/Khang/Trường.
- Nối tạo/đọc hồ sơ khách, số dư và lịch sử ví qua API/SQL; kiểm tra quyền sở hữu, reload và lỗi API.

**Dependencies:** `S1-D-1`

**Done khi:**

- [ ] **Chốt ví, ledger và thời điểm thu tiền.** Có contract credit/debit/refund, chống xử lý lặp, số dư không âm và chính sách nạp tại quầy; Phúc/Khang biết cách gọi trong transaction chung.
- [ ] **Nối hồ sơ khách hàng với API/SQL.** Admin tạo/đọc khách và client đọc hồ sơ đúng tài khoản; reload vẫn còn dữ liệu; request xem hồ sơ người khác bị từ chối.
- [ ] **Đọc số dư và lịch sử ví từ SQL.** API và màn hình ví hiển thị cùng số dư/ledger; có trạng thái rỗng/lỗi; không lấy số dư demo khi API lỗi.

**Gom từ mã cũ:** `S1-M-01`, `S1-M-02`, `S1-M-03`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S1-M-2 — Configure-ZaloPay-Sandbox-Contracts

**Description:**

- Chuẩn bị biến môi trường/secret server và callback HTTPS cho ZaloPay sandbox chính thức.
- Định nghĩa giao dịch TopUp/Order, ID, số tiền, trạng thái và callback/query; kiểm tra chữ ký bằng fixture có ghi rõ phạm vi.

**Dependencies:** `S1-K-2`, `S1-M-1`, Tài khoản và thông tin ứng dụng sandbox

**Done khi:**

- [ ] **Chuẩn bị credentials và callback ZaloPay sandbox.** Có cấu hình secret phía server và URL callback HTTPS được kiểm tra truy cập; ghi bằng chứng hoặc blocker cụ thể; không đưa khóa vào Git/log.
- [ ] **Chốt giao dịch gateway cho nạp ví và đơn món.** Có ID, mục đích TopUp/Order, trạng thái, số tiền dự kiến và contract callback/query; kiểm tra chữ ký bằng fixture ghi rõ là test nội bộ.

**Gom từ mã cũ:** `S1-M-04`, `S1-M-05`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Phúc — Máy, realtime và thiết kế ML chia đội

Assignee: **Phúc**. Reviewer: **Trưởng nhóm**. Phối hợp: Đại; Minh đối chiếu tiền giờ.

#### S1-P-1 — Implement-Computer-Queries-Session-Rules

**Description:**

- Chốt vòng đời máy/phiên, công thức tiền giờ, làm tròn, hết tiền và phục hồi sau restart.
- Triển khai API danh sách máy/loại máy, lưu thay đổi trạng thái và nối màn hình admin; kiểm tra quyền và dữ liệu SQL.

**Dependencies:** `S1-D-1`, `S1-M-1`

**Done khi:**

- [ ] **Chốt trạng thái máy/phiên và tiền giờ.** Có công thức làm tròn, mở/đóng phiên, hết tiền/restart và thời điểm trừ ví; Minh/Trường thống nhất nguồn tiền/chứng từ.
- [ ] **Nối danh sách máy và loại máy với API.** Admin đọc máy từ SQL, thay đổi trạng thái được lưu; kiểm tra quyền, loading/lỗi; reload không mất dữ liệu.

**Gom từ mã cũ:** `S1-P-01`, `S1-P-02`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S1-P-2 — Implement-SignalR-LAN-Sync

**Description:**

- Xây Hub/hook SignalR dùng chung, nhóm nhận event theo tài khoản/quyền và phát event sau commit.
- Nối thay đổi máy, reconnect/refetch và hướng dẫn cho các owner; chạy demo hai máy LAN, đo độ trễ và kiểm tra quyền nhận event.

**Dependencies:** `S1-P-1`

**Done khi:**

- [ ] **Xây SignalR Hub và hook dùng chung.** Nhóm nhận event do server xác thực theo tài khoản/quyền; phát thay đổi máy sau commit; hook reconnect đọc lại API; có hướng dẫn cho owner khác.
- [ ] **Kiểm tra realtime trên hai máy LAN.** Máy A đổi trạng thái, B cập nhật không F5; có số đo theo mục tiêu 2 giây, kiểm tra reconnect và không nhận event sai quyền.

**Gom từ mã cũ:** `S1-P-03`, `S1-P-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S1-P-3 — Define-ML-Team-Balancing-Pipeline

**Description:**

- Kiểm kê code/model/dataset đã có và dữ liệu OP.GG để tận dụng phần Phúc đã làm.
- Chốt input/output, ràng buộc đội, baseline, cách chia train/evaluation, chỉ số/ngưỡng và môi trường suy luận; ghi blocker dữ liệu cụ thể.

**Dependencies:** `S1-D-3`

**Done khi:**

- [ ] **Kiểm kê code ML, dataset và đầu vào OP.GG.** Ghi phần đã có/còn thiếu, nguồn và đặc trưng dữ liệu; kiểm tra adapter OP.GG giữ contract; không coi fixture là dữ liệu provider thật.
- [ ] **Chốt bài toán ML và contract đề xuất đội.** Có input/output, ràng buộc đội, baseline, chỉ số/ngưỡng đánh giá, cách tách dữ liệu và môi trường chạy; Đại đối chiếu contract, Trưởng nhóm duyệt; thiếu dataset được ghi thành blocker.

**Gom từ mã cũ:** `S1-P-05`, `S1-P-06`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Khang — Danh mục, menu và hợp đồng đơn

Assignee: **Khang**. Reviewer: **Trưởng nhóm**. Phối hợp: Trường; Minh đối chiếu phương thức thanh toán.

#### S1-K-1 — Implement-Catalog-API-Menu

**Description:**

- Port danh mục/sản phẩm phù hợp vào API SQL Server chung, giữ kiểm tra quyền và dữ liệu giá.
- Nối quản lý sản phẩm, menu và gợi ý món vào cùng dữ liệu API/tồn; kiểm tra CRUD, tìm/lọc, reload và lỗi, không fallback mock.

**Dependencies:** `S1-D-1`, `S1-K-2`

**Done khi:**

- [ ] **Port API danh mục/sản phẩm vào backend chung.** Chọn lọc code phù hợp, DI/repository chạy được; CRUD lưu SQL và kiểm tra quyền, giá, danh mục; không nhập migration MySQL.
- [ ] **Nối quản lý sản phẩm và menu client.** Món admin tạo hiển thị ở menu từ API; tìm/lọc cơ bản và lỗi được xử lý; dữ liệu còn sau reload.
- [ ] **Nối dữ liệu gợi ý món với menu và tồn.** Gợi ý/giỏ dùng cùng danh sách món API, loại món không còn bán/hết tồn theo policy; không phát sinh bộ sản phẩm giả riêng.
- [ ] **Kiểm tra quyền CRUD và hợp đồng dữ liệu giá.** API danh mục/sản phẩm từ chối quyền sai và giá không hợp lệ; lỗi không quay về mock. Contract đơn quy định server tính tổng; ca sửa tổng tiền khi checkout thực thi ở S2-K-3.

**Gom từ mã cũ:** `S1-K-01`, `S1-K-03`, `S1-K-04`, `S1-K-05`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S1-K-2 — Define-Order-Checkout-Contracts

**Tài liệu bàn giao:** [Hợp đồng đơn món và checkout](../Backend/docs/004-order-checkout-contract.md),
phiên bản 0.4 ngày 08/11/2026; sau review 66f1a29, người dùng ủy quyền AI chốt các lựa chọn còn mở.
Mục 11.2 ghi D01–D11 cùng căn cứ/lý do; việc chốt thiết kế không còn chờ phản hồi riêng của owner.
Mục 11.3 tách phụ thuộc code/migration/kiểm chứng và duyệt cuối; chưa ghi các owner đã đồng ý,
chưa đánh dấu Done. Trưởng nhóm review bản 0.4 trước khi duyệt cuối.

**Description:**

- Chốt DTO đặt/xử lý/hủy đơn, trạng thái, snapshot giá/giảm và cách tính tiền tại server.
- Chốt ví/tiền mặt/ZaloPay, giữ tồn/lượt voucher, hết hạn và hoàn tiền theo quyết định 08/11/2026;
  bàn giao hợp đồng cho Minh/Trường và Phúc ở giao điểm phí phiên để tích hợp cùng một phương án.

**Dependencies:** `S1-M-1`, `S1-T-1`

**Done khi:**

- [ ] **Chốt vòng đời đơn và phương thức thu.** Có DTO đặt/xử lý/hủy, snapshot giá/giảm giá, ví/tiền mặt/gateway, giữ tồn/hết hạn và policy hoàn; ghi căn cứ chốt theo ủy quyền 08/11/2026, đầu vào bàn giao liên module và được Trưởng nhóm duyệt theo điều kiện Done chung.

**Gom từ mã cũ:** `S1-K-02`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Trường — Schema kho, chứng từ, khuyến mãi

Assignee: **Trường**. Reviewer: **Trưởng nhóm**. Phối hợp: Khang; Minh đối chiếu tiền, Đại đối chiếu contract voucher.

#### S1-T-1 — Define-Inventory-Invoice-Voucher-Contracts

**Trạng thái đối chiếu 05/11/2026:** PR #8 đã merge vào `dev` tại `229d7af`, chưa phải nghiệm thu của
Trưởng nhóm. [Contract kho/chứng từ/voucher](../docs/contracts/inventory-invoice-voucher.md) có bổ sung
đề xuất về giờ xác nhận hoàn ZaloPay (mục 6.1.1) và khóa phát hành hóa đơn lưu bền vững (mục 6.3.1).
Các phần bổ sung chờ review; phản hồi liên module và P1 tại mục 6.5/9 vẫn còn mở. Giữ các tiêu chí Done
chưa đánh dấu cho đến khi đủ bằng chứng và Trưởng nhóm duyệt.

**Description:**

- Đối chiếu schema cũ, entity/quan hệ/ràng buộc và chia phần migration cho từng owner.
- Chốt rule voucher món ăn, thời điểm phát hành chứng từ, snapshot và nguồn doanh thu; cung cấp contract chung cho Đại/Minh/Phúc/Khang.

**Dependencies:** `S1-D-1`, `S1-M-1`

**Done khi:**

- [ ] **Chốt schema kho, chứng từ và voucher.** Có entity/quan hệ/ràng buộc, nguồn doanh thu và mapping schema cũ; từng owner biết phần phải bổ sung; giải quyết khác biệt trước khi viết migration.
- [ ] **Chốt quy tắc và API voucher.** Có %/cố định, ngày hiệu lực, ngưỡng đơn, hạn mức, làm tròn và hoàn lượt; Đại/Khang dùng chung contract, voucher chỉ áp món ăn.
- [ ] **Chốt hợp đồng ghi chứng từ phiên và đơn.** Có thời điểm phát hành, snapshot tiền/giảm giá và hoàn tiền; phân biệt nạp ví với doanh thu; Minh/Phúc/Khang đối chiếu hợp đồng.

**Gom từ mã cũ:** `S1-T-01`, `S1-T-02`, `S1-T-03`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S1-T-2 — Integrate-Migrations-Seed-Data

**Description:**

- Tích hợp migrations theo thứ tự, seed dữ liệu và thử trên SQL Server mới riêng cho kiểm tra.
- Nối luồng đọc kho/chứng từ vào API, cập nhật DBML/SQL mô tả và hướng dẫn chạy; kiểm tra quyền và dữ liệu sau reload.

**Dependencies:** `S1-D-1`, `S1-T-1`

**Done khi:**

- [ ] **Tích hợp migration/seed trên DB thử mới.** Migration của các owner có thứ tự, áp dụng được vào SQL Server thử mới; seed nhất quán; không sửa migration đã chạy ở DB chung.
- [ ] **Nối một luồng đọc kho/chứng từ qua API.** Màn hình đọc được dữ liệu SQL, có kiểm tra quyền/lỗi và hướng dẫn chạy; DBML/SQL mô tả khớp migration đã tích hợp.

**Gom từ mã cũ:** `S1-T-04`, `S1-T-05`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Phúc — Nhận diện NEXORA và giao diện dùng chung

Assignee: **Phúc**. Reviewer: **Trưởng nhóm**. Phối hợp: Đại, Minh, Khang, Trường kiểm tra chéo màn hình của mình.

#### S1-U-1 — Apply-Nexora-Branding-Theme

**Description:**

- Chuẩn hóa nhận diện NEXORA CYBER trên admin/client, header/sidebar, favicon và màn hình đăng nhập khi được Đại bàn giao; concept logo chờ nhóm chốt chiều 31/10.
- Thay màu thương hiệu xanh lá bằng token nền đen #0B0F14, tím #A855F7, cyan #22D3EE, chữ sáng #E5F0FF; giữ màu trạng thái nghiệp vụ có ý nghĩa.
- Sửa font, tương phản, spacing, nút, bảng và dialog dùng chung; kiểm tra desktop/mobile, bàn phím và bàn giao ảnh trước–sau cho Trưởng nhóm.

**Dependencies:** Theme/layout làm trên frontend hiện có; phần logo cần concept/asset nhóm chốt chiều 31/10/2026. Phối hợp `S1-D-2` để áp cùng nhận diện khi trang login được bàn giao; không chờ API để làm token/component, không nhận thay logic auth của Đại.

**Căn cứ và nơi bắt đầu:** `frontend/src/app/AdminLayout.jsx` dùng NET CAFE, `ClientLayout.jsx` dùng TRAM; các tiêu đề/nhãn thương hiệu và favicon cần được rà cùng nhau. Xem `app/theme.css`, `app/layout.css`, `app/client.css`, UI/CSS trong `admin/shared/` và `client/shared/`.

**Done khi:**

- [ ] **Thống nhất nhận diện.** NEXORA CYBER dùng cùng asset/component theo concept nhóm đã chốt ở sidebar, header, favicon và nhãn thương hiệu; trang login của Đại dùng cùng bộ nhận diện khi tích hợp. Có ghi concept/nguồn asset đã duyệt, không còn NET CAFE/TRAM ở nhãn thương hiệu trong phạm vi rà; vai trò admin/client được phân biệt bằng nhãn khu vực. Không đổi khóa dữ liệu demo chỉ để đổi tên thương hiệu.
- [ ] **Áp dụng theme cyan–tím–đen.** Các token nền/chính/nhấn/chữ theo bảng màu đã chốt, các điểm dùng xanh lá làm thương hiệu được chuyển sang token mới; màu trạng thái nghiệp vụ vẫn có nghĩa và nhãn/icon rõ. Bàn giao danh sách vị trí đổi và ảnh trước–sau; không còn trộn theme cũ ở các màn hình đã rà.
- [ ] **Chỉnh các phần khó nhìn dùng chung.** Rà font/cỡ chữ, tương phản, khoảng cách, nút, bảng và dialog trên dashboard, máy, menu, ví, hóa đơn, giải đấu. Ghi vấn đề cụ thể và ảnh trước–sau; sửa tại component/style chung khi phù hợp, không làm lại logic nghiệp vụ hoặc toàn bộ từng trang.
- [ ] **Kiểm tra layout và thao tác sau khi sửa.** Thử desktop 1440/1024px và mobile 390/320px: chữ/nút chính đọc được, nội dung không bị che, vùng bảng cuộn được, menu/dialog/Tab/Escape hoạt động đúng. Build và browser suite bị ảnh hưởng đạt; owner liên quan xác nhận luồng của mình không bị hỏng.

**Bàn giao:** một PR hoặc nhóm commit cùng Task, bộ nhận diện/component chung và ảnh so sánh. Lỗi riêng của module được chuyển vào checklist Task owner đang có, không mặc định nhận sửa toàn bộ frontend.

## Sprint 2 — 06–10/11: các luồng tích hợp đầu tiên

Có đường chạy UI–API–SQL cho mọi họ chức năng, gồm đề xuất đội ML, hai mục đích ZaloPay và voucher. Đại và Trường giữ 4 thẻ để các phần cung cấp/gọi contract được bàn giao và review riêng.

### Đại — Nhân viên, giải đấu và UI khuyến mãi

Assignee: **Đại**. Reviewer: **Trưởng nhóm**. Phối hợp: Minh; Phúc đối chiếu giải/ML, Trường đối chiếu voucher.

#### S2-D-1 — Implement-Staff-Password-Management

**Description:**

- Triển khai CRUD/khóa nhân viên, quyền quản trị và đổi mật khẩu theo contract S1.
- Nối form vào API; kiểm tra truy cập trực tiếp, tài khoản quản trị cuối và dữ liệu sau reload.

**Dependencies:** `S1-D-2`

**Done khi:**

- [ ] **Hoàn thiện nhân viên, quyền và đổi mật khẩu.** CRUD/khóa theo policy, guard quản trị cuối và đổi mật khẩu chạy API; kiểm tra quyền trực tiếp và dữ liệu sau reload.

**Gom từ mã cũ:** `S2-D-01`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S2-D-2 — Implement-Tournament-Registration

**Description:**

- Triển khai CRUD/mở giải, danh sách giải và đăng ký bằng dữ liệu SQL theo policy trạng thái/phí.
- Chặn sai tài khoản, đăng ký trùng hoặc đã đóng; phát event sau commit và bàn giao danh sách người tham gia cho Phúc.

**Dependencies:** `S1-D-3`, `S1-P-2`, `S1-T-2`

**Done khi:**

- [ ] **Xây CRUD giải đấu và danh sách giải.** Admin tạo/sửa/mở giải, client xem dữ liệu SQL; trạng thái/đăng ký/phí đúng policy; không dùng kho giải demo.
- [ ] **Nối đăng ký giải và danh sách người tham gia.** Đăng ký đúng tài khoản, chặn trùng/đóng đăng ký; admin/client cùng dữ liệu, cập nhật qua event sau commit.

**Gom từ mã cũ:** `S2-D-02`, `S2-D-03`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S2-D-3 — Build-Voucher-Admin-UI

**Description:**

- Xây form/list quản trị voucher dùng API và rule của Trường, gồm %/cố định, ngày, ngưỡng và hạn mức.
- Hiển thị lỗi server, lưu thay đổi và nối event đúng quyền; kiểm tra thao tác admin qua UI/API.

**Dependencies:** `S1-P-2`, `S2-T-3`

**Done khi:**

- [ ] **Xây màn hình CRUD voucher.** Form %/cố định, thời hạn/ngưỡng/hạn mức gọi API Trường; hiển thị lỗi server; thay đổi được lưu và gửi event đúng quyền.

**Gom từ mã cũ:** `S2-D-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S2-D-4 — Integrate-ML-Team-Preview

**Description:**

- Nối danh sách đăng ký hợp lệ vào contract suy luận của Phúc và hiển thị các đội được đề xuất.
- Hiển thị model version, thiếu dữ liệu và lỗi; kiểm tra rằng xem/đề xuất lại chưa ghi đè đội đã chốt.

**Dependencies:** `S2-D-2`, `S2-P-3`

**Done khi:**

- [ ] **Tích hợp bản đầu của đề xuất đội ML.** Gửi danh sách đăng ký hợp lệ cho model Phúc; UI hiển thị đội đề xuất và phiên bản; lỗi thiếu dữ liệu rõ ràng, chưa tự ghi đè đội đã chốt.

**Gom từ mã cũ:** `S2-D-05`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Minh — Ví và ZaloPay sandbox

Assignee: **Minh**. Reviewer: **Trưởng nhóm**. Phối hợp: Trường; Đại đối chiếu quyền/callback, Khang đối chiếu checkout.

#### S2-M-1 — Implement-Wallet-Ledger-Counter-Topups

**Description:**

- Triển khai credit/debit/refund dùng chung với ledger trong cùng transaction và khóa chống xử lý lặp.
- Nối nạp tại quầy, lịch sử và realtime số dư; kiểm tra số dư không âm, xử lý lặp và điểm tích hợp phiên/đơn.

**Dependencies:** `S1-M-1`, `S1-P-2`, `S1-T-2`

**Done khi:**

- [ ] **Xây credit/debit/refund và ledger dùng chung.** Số dư/ledger cập nhật cùng transaction, khóa chống xử lý lặp và không âm ví; có điểm tích hợp cho phiên/đơn.
- [ ] **Nối nạp tại quầy và lịch sử ví.** Thu/cộng ví đúng policy, xử lý lặp không cộng hai lần; UI hồ sơ/lịch sử đọc API và nhận số dư qua realtime.

**Gom từ mã cũ:** `S2-M-01`, `S2-M-02`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S2-M-2 — Implement-ZaloPay-Gateway-Callbacks

**Description:**

- Xây adapter tạo giao dịch ZaloPay sandbox cho TopUp/Order; số tiền và trạng thái dự kiến do server quản lý, secret qua cấu hình server.
- Xử lý callback/query, xác minh chữ ký, ID và số tiền; viết unit test tham số/chữ ký và kiểm tra chống ghi nhận trùng, trạng thái chờ/thất bại; không coi redirect là xác nhận thanh toán.

**Dependencies:** `S1-M-2`

**Done khi:**

- [ ] **Tạo giao dịch ZaloPay sandbox từ server.** Adapter dùng chung TopUp/Order, số tiền do server tính, lưu ID/trạng thái dự kiến; gọi sandbox chính thức và giữ secret ở server.
- [ ] **Xử lý callback và truy vấn trạng thái.** Xác minh chữ ký/giao dịch/số tiền; chống callback/query xử lý trùng; redirect không tự ghi nhận đã thanh toán; có luồng chờ/thất bại.
- [ ] **Kiểm tra adapter.** Unit test tạo tham số/ký request và xác minh callback đạt với fixture đúng/sai; có kết quả kiểm tra callback/query lặp không ghi nhận trùng. Ghi rõ fixture nội bộ và bằng chứng gọi sandbox thật; API/provider lỗi không tự fallback mock.

**Gom từ mã cũ:** `S2-M-03`, `S2-M-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S2-M-3 — Integrate-ZaloPay-Topups-Checkout

**Description:**

- Nối giao dịch ZaloPay thành công vào ví/ledger và điểm xác nhận Order cho Khang, phát cập nhật sau commit.
- Chạy sandbox chính thức cho TopUp và Order với thành công/chờ/thất bại/gửi lặp; thu log đã che secret và đối chiếu số tiền ghi nhận.

**Dependencies:** `S1-P-2`, `S2-K-2`, `S2-M-1`, `S2-M-2`

**Done khi:**

- [ ] **Hoàn thiện nạp ví qua ZaloPay sandbox.** Một lượt sandbox thật ghi có ví/ledger đúng một lần, UI nhận số dư sau commit; bằng chứng đã che secret.
- [ ] **Tích hợp gateway với checkout món và kiểm tra lặp.** Cung cấp điểm xác nhận Order cho Khang; chạy thành công/chờ/thất bại/callback lặp trên cả TopUp và Order; có log sandbox chính thức.

**Gom từ mã cũ:** `S2-M-05`, `S2-M-06`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Phúc — Phiên, OP.GG và model đầu tiên

Assignee: **Phúc**. Reviewer: **Trưởng nhóm**. Phối hợp: Đại; Minh/Trường đối chiếu giao dịch tiền phiên.

#### S2-P-1 — Implement-Computer-Sessions-Billing

**Description:**

- Hoàn thiện CRUD máy/loại máy/bảo trì và Home dùng API.
- Triển khai mở/đóng phiên, tính tiền server và tích hợp ví/chứng từ cùng transaction; kiểm tra phiên trùng, đóng lặp và đồng bộ hai máy.

**Dependencies:** `S1-P-1`, `S1-P-2`, `S2-M-1`, `S2-T-2`

**Done khi:**

- [ ] **Hoàn thiện CRUD máy và trạng thái bảo trì.** Tạo/sửa máy/loại máy, bảo trì và hiển thị Home lấy API; chặn trạng thái không hợp lệ, phát event sau lưu.
- [ ] **Xây mở/đóng phiên và thu tiền giờ.** Server tính tiền, gọi ví và chứng từ cùng transaction; chặn phiên trùng/đóng lặp; hai máy thấy trạng thái đã lưu.

**Gom từ mã cũ:** `S2-P-01`, `S2-P-02`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S2-P-2 — Integrate-OPGG-Player-Stats

**Description:**

- Nối lookup/refresh OP.GG với người đăng ký giải, lưu nguồn và thời điểm dữ liệu.
- Xử lý không có dữ liệu/provider lỗi đúng contract, cung cấp đầu vào cho giải/ML; kiểm tra bằng nguồn thật và phân biệt fixture.

**Dependencies:** `S1-P-3`, `S2-D-2`

**Done khi:**

- [ ] **Nối OP.GG với người tham gia giải.** Lookup/refresh lưu nguồn và thời điểm; thiếu/lỗi hiển thị thật, không sinh stats giả; contract dùng được cho luồng giải và ML đã chốt.

**Gom từ mã cũ:** `S2-P-03`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S2-P-3 — Train-Serve-Team-Balancing-Model

**Description:**

- Chuẩn bị dataset có nguồn, đặc trưng, xử lý thiếu dữ liệu, tách train/evaluation và chạy baseline.
- Huấn luyện, đánh giá model đầu tiên; lưu version/cách chạy và báo cáo giới hạn.
- Cung cấp suy luận/đề xuất đội theo contract để Đại tích hợp, kiểm tra đủ người, không trùng/bỏ sót và lỗi đầu vào.

**Dependencies:** `S1-P-3`, `S2-D-2`

**Done khi:**

- [ ] **Chuẩn bị dữ liệu và baseline cho ML.** Dataset có nguồn, đặc trưng, quy tắc thiếu dữ liệu và tách train/evaluation; baseline được chạy, không rò dữ liệu đánh giá vào huấn luyện.
- [ ] **Huấn luyện và đánh giá model chia đội bản đầu.** Có model/version, cách chạy, kết quả trên dữ liệu đánh giá so với baseline và giới hạn; không gọi quy tắc ghép đội viết tay là model học máy.
- [ ] **Cung cấp suy luận và đề xuất đội qua contract.** Danh sách người thật đi qua suy luận/logic chia đội; đúng số người, không trùng/bỏ sót, có lỗi rõ; Đại gọi được từ luồng giải.

**Gom từ mã cũ:** `S2-P-04`, `S2-P-05`, `S2-P-06`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Khang — Đặt món, voucher và thanh toán

Assignee: **Khang**. Reviewer: **Trưởng nhóm**. Phối hợp: Trường; Minh đối chiếu tiền/gateway.

#### S2-K-1 — Implement-Food-Orders-Voucher-Checkout

**Description:**

- Triển khai tạo/đọc/xử lý đơn với giá/tổng do server tính và snapshot dữ liệu món/giảm giá.
- Nối ví/tiền mặt, tồn, chứng từ và rule voucher trong giao dịch nghiệp vụ; kiểm tra quyền, thiếu tiền/tồn và voucher không hợp lệ.

**Dependencies:** `S1-K-1`, `S1-K-2`, `S1-T-2`, `S2-M-1`, `S2-T-1`, `S2-T-2`, `S2-T-3`

**Done khi:**

- [ ] **Xây đặt món với snapshot giá và trạng thái.** API tạo/đọc đơn và admin xử lý theo contract; server xác định món/giá/tổng; lưu snapshot và dữ liệu khách đúng quyền.
- [ ] **Nối thanh toán ví và tiền mặt với tồn/chứng từ.** Đơn, ledger, tồn và chứng từ cập nhật nhất quán theo policy; thiếu tiền/tồn bị từ chối; thu tiền mặt có trạng thái rõ.
- [ ] **Áp voucher vào đơn món ăn.** Gọi rule chung, server tính giảm và lưu snapshot/lượt dùng trong giao dịch đơn; mã sai/hết hạn/hết lượt bị từ chối.

**Gom từ mã cũ:** `S2-K-01`, `S2-K-02`, `S2-K-03`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S2-K-2 — Integrate-ZaloPay-Checkout-Reservations

**Description:**

- Tạo payment Order qua adapter Minh, trả dữ liệu thanh toán theo contract và giữ tồn/lượt voucher có hạn.
- Chỉ xác nhận trả tiền khi có kết quả được server kiểm chứng; kiểm tra trạng thái chờ và tránh giữ SQL transaction trong lúc chờ provider.

**Dependencies:** `S2-K-1`, `S2-M-2`

**Done khi:**

- [ ] **Nối checkout ZaloPay và giữ chỗ khi chờ.** Tạo payment Order qua adapter Minh; giữ tồn/lượt voucher có hạn; callback hợp lệ mới xác nhận trả tiền, không mở SQL transaction suốt lúc chờ.

**Gom từ mã cũ:** `S2-K-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S2-K-3 — Integrate-Ordering-UI-Food-Wheel

**Description:**

- Nối menu, gợi ý, giỏ và xử lý đơn admin/client vào API, voucher, các phương thức thanh toán và event sau commit.
- Sửa trải nghiệm vòng quay, trạng thái/chuyển động và kết quả trên desktop/mobile; tích hợp component ảnh/theme do Phúc bàn giao, giữ nguyên quy tắc chọn món.
- Kiểm tra xuyên UI–API–SQL và suggestion-checkout: thiếu tiền/tồn, sửa giá, voucher sai, gửi lặp, 0/1 món và tồn thay đổi.

**Dependencies:** `S1-K-1`, `S1-P-2`, `S2-K-2`, `S2-M-3`, `S2-U-1`

**Done khi:**

- [ ] **Nối menu, gợi ý, giỏ và thao tác xử lý đơn.** Các điểm gọi món dùng cùng API, phương thức thu và voucher; admin/client thấy cùng đơn, nhận event sau commit; loading/lỗi không tạo bản ghi giả.
- [ ] **Kiểm tra đặt món xuyên UI–API–SQL.** Có ca ví/tiền mặt/ZaloPay, thiếu tiền/tồn, sửa giá client, voucher sai và gửi lặp; đối chiếu đơn/tiền/tồn từ SQL.
- [ ] **Cải thiện trải nghiệm vòng quay đã có.** Cùng Phúc và Trưởng nhóm chỉ rõ điểm chưa tốt ở `TodaySuggestion.jsx`/`SuggestionResultDialog.jsx`; chỉnh bố cục, chuyển động, nút quay và kết quả dễ đọc trên desktop/mobile theo token NEXORA do Phúc bàn giao. Trạng thái sẵn sàng/đang quay/kết quả/hết món rõ; không kích hoạt lượt quay chồng nhau, không kẹt khi đổi màn hình/đóng dialog, món dừng ở vùng chọn khớp kết quả; xử lý 0/1 món và tồn thay đổi. Giữ quy tắc chọn món đã chốt, không sửa xác suất chỉ để làm hiệu ứng. Dùng component ảnh từ S2-U-1; kiểm tra `suggestion-checkout` và model liên quan.

**Gom từ mã cũ:** `S2-K-05`, `S2-K-06`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Trường — Kho, chứng từ và quy tắc voucher

Assignee: **Trường**. Reviewer: **Trưởng nhóm**. Phối hợp: Khang; Minh đối chiếu tiền, Đại đối chiếu API quản trị voucher.

#### S2-T-1 — Implement-Inventory-Transactions

**Description:**

- Triển khai nhập/điều chỉnh/trừ/hoàn tồn với lịch sử biến động và kiểm soát đồng thời.
- Cung cấp điểm gọi trong transaction đơn và policy giữ chỗ; kiểm tra tồn không âm, rollback và truy vết biến động.

**Dependencies:** `S1-K-2`, `S1-T-2`

**Done khi:**

- [ ] **Xây nhập/điều chỉnh/trừ hoàn tồn dùng chung.** Biến động có lịch sử; kiểm soát đồng thời, không âm tồn; cung cấp điểm gọi trong transaction đơn và chính sách giữ chỗ đã chốt.

**Gom từ mã cũ:** `S2-T-01`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S2-T-2 — Implement-Invoice-Issuing-Printing

**Description:**

- Xây phát hành chứng từ phiên/đơn dùng chung transaction, lưu snapshot và chống phát hành trùng.
- Nối đọc/chi tiết/in admin/client theo quyền; đối chiếu giá/giảm/thu/hoàn và kiểm tra khi dữ liệu món thay đổi.

**Dependencies:** `S1-D-1`, `S1-T-1`, `S1-T-2`

**Done khi:**

- [ ] **Xây nghiệp vụ phát hành chứng từ phiên/đơn.** Ghi đúng snapshot tiền/giảm giá theo contract, xử lý lặp không tạo chứng từ trùng; gọi được cùng transaction nghiệp vụ.
- [ ] **Nối đọc/in chứng từ admin và client.** Đọc chứng từ API theo quyền; chi tiết thể hiện snapshot giá/giảm/thu/hoàn; bản in không thay tiền khi giá món đổi.

**Gom từ mã cũ:** `S2-T-02`, `S2-T-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S2-T-3 — Implement-Voucher-API-Validation

**Description:**

- Triển khai CRUD và rule voucher chung cho admin của Đại và checkout của Khang.
- Kiểm tra server với %/cố định, ngày, ngưỡng, hạn mức, làm tròn và xử lý đồng thời; cung cấp ca biên để tích hợp.

**Dependencies:** `S1-T-1`, `S1-T-2`

**Done khi:**

- [ ] **Xây CRUD và rule voucher dùng chung.** Đủ %/cố định, ngày/ngưỡng/hạn mức, làm tròn; kiểm tra phía server và hạn mức đồng thời; Đại/Khang gọi cùng rule.

**Gom từ mã cũ:** `S2-T-03`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S2-T-4 — Implement-Revenue-Queries-Reconciliation

**Description:**

- Xây truy vấn doanh thu từ giao dịch thực, phân biệt nguồn thu và nạp ví.
- Đối chiếu dữ liệu mẫu phát sinh từ đơn/phiên với tồn, lượt voucher và chứng từ; bàn giao bảng nguồn số liệu.

**Dependencies:** `S2-K-1`, `S2-P-1`, `S2-T-1`, `S2-T-3`

**Done khi:**

- [ ] **Xây truy vấn doanh thu và đối chiếu dữ liệu mẫu.** Có số liệu theo nguồn giao dịch, tách nạp ví khỏi doanh thu; đối chiếu tồn/lượt voucher/chứng từ khi đơn hoặc phiên tạo dữ liệu thật.

**Gom từ mã cũ:** `S2-T-05`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Phúc — Ảnh món ăn và trình bày menu

Assignee: **Phúc**. Reviewer: **Trưởng nhóm**. Phối hợp: Khang cung cấp contract sản phẩm và kiểm tra chéo luồng chọn món/giỏ/checkout.

#### S2-U-1 — Add-Food-Images-Menu-Cards

**Description:**

- Chuẩn bị ảnh đúng món, mapping theo ID/trường ảnh đã thống nhất với Khang; không tạo nguồn sản phẩm riêng.
- Chuẩn hóa component ảnh/thẻ món theo theme NEXORA cyan–tím–đen của S1-U-1, gồm crop, tải ảnh, fallback và nội dung dễ đọc.
- Áp dụng Home/menu/giỏ/chi tiết kết quả, bàn giao component cho Khang tích hợp vòng quay; kiểm tra ảnh hỏng, món mới và mobile.

**Dependencies:** `S1-U-1`, `S1-K-1`. Chốt với Khang trường ảnh và cách cung cấp URL/asset; không chờ hoàn tất thanh toán để làm phần trình bày.

**Căn cứ và nơi bắt đầu:** `frontend/src/client/shared/ClientUI.jsx` đang vẽ `FoodArt` bằng emoji `productArt` trong `demoData.js`; teaser/vòng quay cũng dùng emoji. Xem các nơi gọi `FoodArt`, không tạo bộ ảnh/tên món riêng lệch với sản phẩm.

**Done khi:**

- [ ] **Gắn ảnh đúng sản phẩm.** Chuẩn bị ảnh khớp từng món trong bộ demo, có mapping theo ID/trường ảnh rõ ràng; thống nhất đường dẫn/URL với Khang. Khang sở hữu entity/API/form dữ liệu sản phẩm cần bổ sung; Phúc sở hữu ảnh và component hiển thị, không tạo API ảnh riêng trùng việc.
- [ ] **Chuẩn hóa thẻ ảnh món.** Thẻ dùng token NEXORA cyan–tím–đen từ S1-U-1. Ảnh giữ tỉ lệ, kích thước/crop nhất quán và tên/giá dễ đọc; có fallback khi thiếu/tải lỗi, không gây vòng lặp lỗi; tải ảnh phù hợp kích thước, không làm layout nhảy. Có mô tả ảnh phù hợp; giữ nút, tồn, giá và hành vi giỏ hiện có.
- [ ] **Áp dụng cùng bộ ảnh vào các điểm hiển thị.** Home/menu/giỏ/chi tiết kết quả dùng chung component hoặc quy ước; Phúc bàn giao component và asset, Khang nối vào teaser/vòng quay/kết quả trong S2-K-3. Kiểm tra ảnh đúng món, URL hỏng, danh sách dài, mobile và món mới chưa có ảnh; build và các luồng gọi món bị ảnh hưởng đạt, có ảnh trước–sau.

**Ranh giới:** Task này nghiệm thu ảnh/component/menu và bàn giao giao diện dùng chung. Việc hoàn tất vòng quay/checkout là phần nghiệm thu của Khang ở S2-K-3, tránh hai Task chờ nhau hoặc cùng sửa state/animation.

## Sprint 3 — 10–15/11: hoàn thành chức năng

Cuối ngày 15/11 hoàn thành chức năng bắt buộc, có đánh giá ML và demo hai máy để bàn giao sang hồi quy S4. Việc chưa đạt giữ trạng thái thật, không đổi thành tính năng tùy chọn.

### Đại — Hoàn thiện quyền, giải đấu và UI voucher

Assignee: **Đại**. Reviewer: **Trưởng nhóm**. Phối hợp: Minh; Phúc đối chiếu ML/đội, Trường đối chiếu voucher.

#### S3-D-1 — Complete-Tournament-Lifecycle-Team-Confirmation

**Description:**

- Hoàn thiện trạng thái giải, đăng ký/hủy và các điều kiện hợp lệ tại API.
- Kiểm tra và lưu đội từ đề xuất ML cùng model version/input snapshot; bảo vệ đội đã chốt và đồng bộ kết quả hai máy.

**Dependencies:** `S2-D-2`, `S2-D-4`, `S3-P-2`

**Done khi:**

- [ ] **Hoàn thiện vòng đời giải và đăng ký/hủy.** Admin/client dùng API cho các trạng thái/đăng ký/hủy đúng policy; xử lý trùng, hết hạn, giải đóng và điều kiện đủ người/đội.
- [ ] **Xác nhận và lưu đội từ đề xuất ML.** Kiểm tra danh sách/quyền ở server, lưu đội cùng model version/input snapshot; gọi lại đề xuất không ghi đè đội chốt; hai máy cùng kết quả sau commit.

**Gom từ mã cũ:** `S3-D-01`, `S3-D-02`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S3-D-2 — Complete-Account-Staff-Voucher-Forms

**Description:**

- Hoàn thiện form tài khoản/nhân viên, bỏ placeholder auth và nối toàn bộ thao tác bắt buộc vào API.
- Hoàn thiện UI voucher với validation/lỗi rule, hết hạn và thay đổi đồng thời; kiểm tra quyền/khóa/đổi mật khẩu ở server.

**Dependencies:** `S2-D-1`, `S2-D-3`, `S3-T-1`

**Done khi:**

- [ ] **Hoàn thiện form tài khoản và nhân viên.** Toàn bộ thao tác bắt buộc gọi API, validation/lỗi rõ; bỏ placeholder auth; guard quyền/khóa/đổi mật khẩu hoạt động tại server.
- [ ] **Hoàn thiện UI voucher và trạng thái lỗi.** Tạo/sửa/hiệu lực/hạn mức phản ánh API; form báo lỗi rule, voucher hết hạn và dữ liệu đổi đồng thời theo policy.

**Gom từ mã cũ:** `S3-D-03`, `S3-D-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S3-D-3 — Verify-Access-Control-Tournament-Sync

**Description:**

- Kiểm tra truy cập endpoint trái quyền/khác tài khoản và các trạng thái đăng ký trùng/đã đóng.
- Chạy đề xuất/xác nhận đội và đồng bộ giải/voucher trên hai máy; ghi bằng chứng, sửa lỗi trong phạm vi và kiểm tra lại.

**Dependencies:** `S3-D-1`, `S3-D-2`

**Done khi:**

- [ ] **Kiểm tra quyền chéo tài khoản và realtime giải.** Có kết quả truy cập trực tiếp endpoint trái quyền, đăng ký trùng/đóng, đề xuất/xác nhận đội và đồng bộ giải/voucher hai máy.

**Gom từ mã cũ:** `S3-D-05`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Minh — Hoàn thiện ví, gateway và đối soát

Assignee: **Minh**. Reviewer: **Trưởng nhóm**. Phối hợp: Trường; Đại đối chiếu quyền, Khang đối chiếu hoàn tiền đơn.

#### S3-M-1 — Complete-Wallet-Debits-Refunds

**Description:**

- Hoàn thiện hồ sơ, tìm/lọc và lịch sử ví dùng API với validation/quyền sở hữu.
- Nối debit/refund từ phiên/đơn, đối chiếu ledger từng nguồn và bàn giao xử lý hoàn gateway theo policy cho Khang.

**Dependencies:** `S2-M-1`, `S2-M-3`

**Done khi:**

- [ ] **Hoàn thiện hồ sơ, bộ lọc và lịch sử ví.** Các màn hình khách/ví dùng API, tìm/lọc và lịch sử đầy đủ; sửa hồ sơ có validation và quyền đúng tài khoản.
- [ ] **Hoàn thiện trừ/hoàn tiền từ phiên và đơn.** Ledger đối chiếu từng nguồn; không âm ví, không debit/refund hai lần; cung cấp policy và xử lý hoàn gateway đã chốt cho Khang.

**Gom từ mã cũ:** `S3-M-01`, `S3-M-02`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S3-M-2 — Harden-ZaloPay-Callback-Recovery

**Description:**

- Xử lý callback trễ/lặp, sai chữ ký/ID/số tiền và query đồng thời cho TopUp/Order.
- Phục hồi từ trạng thái đã lưu khi restart; kiểm tra callback/query cạnh tranh và bảo đảm không ghi nhận tiền hai lần.

**Dependencies:** `S2-M-3`

**Done khi:**

- [ ] **Xử lý callback trễ/lặp và truy vấn đồng thời.** TopUp/Order xử lý sai chữ ký/số tiền, callback trễ/lặp, query đồng thời và restart từ trạng thái lưu; không ghi nhận tiền hai lần.

**Gom từ mã cũ:** `S3-M-03`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S3-M-3 — Reconcile-Expired-Cancelled-Payments

**Description:**

- Cùng Khang chạy hết hạn giữ chỗ, hủy/hoàn và callback thành công muộn theo policy đã chốt.
- Đối soát ví–ledger–gateway–đơn, quyền và realtime; thu bằng chứng nạp ví/checkout sandbox chính thức đã che secret.

**Dependencies:** `S3-K-1`, `S3-M-1`, `S3-M-2`

**Done khi:**

- [ ] **Kiểm tra gateway với đơn hết hạn/hủy.** Phối hợp Khang tái hiện hết hạn giữ chỗ/callback thành công muộn/hủy/hoàn; tiền được đối soát đúng policy, không tự giao món hết tồn.
- [ ] **Đối soát ví và thu bằng chứng sandbox.** Số dư–ledger–gateway–đơn khớp; quyền dữ liệu/realtime đúng; có bằng chứng nạp ví và checkout sandbox thật đã che secret.

**Gom từ mã cũ:** `S3-M-04`, `S3-M-05`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Phúc — Hoàn thiện ML, phiên và realtime

Assignee: **Phúc**. Reviewer: **Trưởng nhóm**. Phối hợp: Đại; Minh đối chiếu tiền giờ.

#### S3-P-1 — Complete-Dashboard-Session-Recovery

**Description:**

- Nối Home/dashboard với dữ liệu máy, phiên, ví, đơn và giải từ API, xử lý loading/lỗi/empty.
- Hoàn thiện tiền giờ, hết tiền, đóng phiên, bảo trì và restart; kiểm tra không mất phiên, không thu lặp/nhân đôi chứng từ.

**Dependencies:** `S2-D-2`, `S2-K-3`, `S2-P-1`

**Done khi:**

- [ ] **Hoàn thiện Home/dashboard từ các API.** Máy, phiên, ví/đơn/giải liên quan hiển thị từ nguồn thật; loading/lỗi/empty rõ, không nối nửa SQL nửa localStorage.
- [ ] **Hoàn thiện phiên khi hết tiền và restart.** Tiền giờ/đóng phiên/bảo trì theo policy, restart không mất phiên đã lưu; thu lặp không phát sinh ledger/chứng từ mới.

**Gom từ mã cũ:** `S3-P-01`, `S3-P-02`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S3-P-2 — Improve-OPGG-Data-ML-Quality

**Description:**

- Hoàn thiện refresh/cache/retry giới hạn và nguồn/thời điểm stats OP.GG.
- Đánh giá model theo tiêu chí S1, so baseline và sửa ràng buộc số người/vai trò; lưu version/input và báo lỗi thiếu dữ liệu đúng thực tế.

**Dependencies:** `S2-P-2`, `S2-P-3`

**Done khi:**

- [ ] **Hoàn thiện OP.GG refresh, cache và lỗi.** Stats có nguồn/thời điểm; thiếu/lỗi/retry có giới hạn theo contract; không biến dữ liệu vắng thành chỉ số giả.
- [ ] **Hoàn thiện chất lượng và ràng buộc chia đội ML.** Đánh giá theo chỉ số/ngưỡng S1, báo cáo so baseline; đội đúng số lượng/vai trò, không trùng/bỏ sót; lưu được version/input và lỗi thiếu dữ liệu rõ.

**Gom từ mã cũ:** `S3-P-03`, `S3-P-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S3-P-3 — Verify-ML-Flow-Realtime-Sync

**Description:**

- Tích hợp cùng Đại luồng đề xuất–xác nhận–lưu–xem lại đội; kiểm tra model lỗi, dự phòng có nhãn và đội đã chốt.
- Đo và sửa realtime máy/phiên, ví, đơn/tồn, giải/đội, voucher trên hai máy; kiểm tra reconnect/refetch và quyền nhận event.

**Dependencies:** `S3-D-1`, `S3-K-2`, `S3-M-3`, `S3-P-1`, `S3-P-2`, `S3-T-1`

**Done khi:**

- [ ] **Tích hợp ML chia đội với Đại và kiểm tra hai máy.** Đề xuất–xác nhận–lưu–xem lại hoạt động; model lỗi và chia dự phòng có nhãn; gọi lại không đổi đội đã chốt; có bằng chứng demo.
- [ ] **Đo và sửa realtime toàn hệ thống.** Máy/phiên, ví, đơn/tồn, giải/đội, voucher có số đo theo mục tiêu 2 giây; reconnect refetch đúng, không lộ event khác quyền.

**Gom từ mã cũ:** `S3-P-05`, `S3-P-06`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Khang — Hoàn thiện đơn, voucher và gợi ý món

Assignee: **Khang**. Reviewer: **Trưởng nhóm**. Phối hợp: Trường; Minh đối chiếu tiền/gateway.

#### S3-K-1 — Handle-Order-Cancellation-Concurrency

**Description:**

- Hoàn thiện hủy món/đơn, thu tiền mặt, hoàn ví/tồn/lượt và chứng từ theo trạng thái/policy.
- Xử lý hết hạn giữ chỗ, hủy gateway và callback muộn gắn đúng payment.
- Kiểm tra hai request tranh tồn/lượt cuối, lỗi giữa transaction và retry; đối chiếu tiền/tồn/lượt không ghi nhận hai lần.

**Dependencies:** `S2-K-2`, `S2-K-3`, `S3-M-1`, `S3-M-2`, `S3-T-1`

**Done khi:**

- [ ] **Hoàn thiện hủy món/đơn và hoàn ví/tồn/lượt.** Hủy theo trạng thái/policy, thu tiền mặt rõ; ví/tồn/lượt voucher/chứng từ cập nhật nhất quán; khách không tự phê duyệt hoàn tiền.
- [ ] **Hoàn thiện hết hạn và hủy checkout gateway.** Giải phóng giữ tồn/lượt voucher đúng hạn, xử lý callback muộn theo policy; hủy/hoàn gắn đúng payment và không thu/hoàn hai lần.
- [ ] **Kiểm tra tranh tồn và lượt voucher cuối.** Hai request tranh cùng tồn/lượt, lỗi giữa transaction và retry không gây âm tồn/vượt hạn mức/đơn trùng; có kết quả API/DB.

**Gom từ mã cũ:** `S3-K-01`, `S3-K-03`, `S3-K-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S3-K-2 — Complete-Menu-Cart-Invoice-Consistency

**Description:**

- Hoàn thiện tìm/lọc, gợi ý, giỏ và chặn thao tác sai trạng thái/gửi lặp trên desktop/mobile.
- Đối chiếu đơn–hóa đơn–event admin/client, chạy đủ gợi ý → checkout → hủy/hoàn.
- Hồi quy ảnh món/component của Phúc và vòng quay sau tích hợp, kiểm tra món được chọn, kết quả và món vào giỏ khớp nhau.

**Dependencies:** `S2-K-3`, `S3-K-1`, `S3-T-2`

**Done khi:**

- [ ] **Hoàn thiện menu, tìm/lọc, gợi ý và giỏ hàng.** Đủ hành vi hiện có trên desktop/mobile; snapshot giá/giảm được giữ; chặn thao tác sai trạng thái và gửi lặp.
- [ ] **Đối chiếu đơn, hóa đơn và event admin/client.** Tổng thu/hoàn khớp snapshot; hai vai trò xem cùng trạng thái sau commit; luồng gợi ý → checkout → hủy/hoàn được chạy đủ.
- [ ] **Kiểm tra lại ảnh món và vòng quay sau tích hợp.** Dùng ảnh/component đã bàn giao ở S2-U-1, chạy các trạng thái quay/chọn/thử lại/đóng kết quả trên desktop/mobile; món ở vùng chọn khớp kết quả và món được thêm vào giỏ. Ghi lỗi còn lại vào cùng Task, không tạo thẻ frontend trùng chỉ để rà lại.

**Gom từ mã cũ:** `S3-K-02`, `S3-K-05`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Trường — Hoàn thiện kho, hóa đơn, báo cáo, voucher

Assignee: **Trường**. Reviewer: **Trưởng nhóm**. Phối hợp: Khang; Minh đối chiếu số liệu tiền.

#### S3-T-1 — Complete-Inventory-Voucher-Lifecycle

**Description:**

- Hoàn thiện nhập/điều chỉnh/lịch sử/lọc kho và truy vết biến động từ đơn.
- Hoàn thiện CRUD/áp/giữ/hoàn lượt voucher, kiểm tra ngày, làm tròn, ngưỡng và hạn mức đồng thời; bàn giao test biên cho Đại/Khang.

**Dependencies:** `S2-T-1`, `S2-T-3`

**Done khi:**

- [ ] **Hoàn thiện quản lý và lịch sử kho.** Đủ nhập/điều chỉnh/lịch sử/lọc như demo, nguồn SQL và quyền server; biến động liên quan đơn truy ngược được.
- [ ] **Hoàn thiện voucher và hoàn lượt khi hủy.** Rule CRUD/áp/giữ/hoàn lượt xử lý đồng thời; ngày, làm tròn, min order, tổng/theo khách đúng policy; có test biên cho Đại/Khang tích hợp.

**Gom từ mã cũ:** `S3-T-01`, `S3-T-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S3-T-2 — Complete-Invoice-Filters-Print-Export

**Description:**

- Hoàn thiện danh sách/chi tiết/tìm/lọc/in/xuất hóa đơn admin/client theo hành vi hiện có.
- Kiểm tra quyền dữ liệu và snapshot tiền/giảm sau khi sản phẩm hoặc voucher bị sửa; đối chiếu màn hình, bản in và bản xuất.

**Dependencies:** `S2-T-2`

**Done khi:**

- [ ] **Hoàn thiện hóa đơn admin/client, in và xuất.** Chi tiết/tìm/lọc/in/xuất đủ hành vi hiện có, đúng quyền; giá/giảm không đổi theo sản phẩm/voucher sửa sau đó.

**Gom từ mã cũ:** `S3-T-02`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S3-T-3 — Complete-Revenue-Reports-Schema-Docs

**Description:**

- Hoàn thiện lọc ngày, bảng và biểu đồ doanh thu từ cùng số liệu API, phân biệt nguồn thu/giảm/hoàn/nạp ví.
- Đối chiếu giao dịch đơn/phiên/hoàn, cập nhật DBML/SQL theo migrations và bàn giao bảng giải thích số liệu.

**Dependencies:** `S2-T-4`, `S3-K-2`, `S3-P-1`, `S3-T-1`, `S3-T-2`

**Done khi:**

- [ ] **Hoàn thiện báo cáo và biểu đồ doanh thu.** Bộ lọc/khoảng ngày/bảng/biểu đồ dùng cùng số liệu API; nguồn thu, giảm/hoàn và nạp ví tách đúng policy.
- [ ] **Đối chiếu báo cáo và cập nhật schema mô tả.** Số liệu khớp giao dịch đơn/phiên/hoàn; DBML/SQL cập nhật theo migration; có bảng đối chiếu để nhóm giải thích khi demo.

**Gom từ mã cũ:** `S3-T-03`, `S3-T-05`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

## Sprint 4 — 15–19/11: hồi quy và bàn giao

Hồi quy, sửa lỗi, kiểm tra DB mới/LAN/provider và đóng gói. Lỗi cần theo dõi riêng có thể tạo Bug liên kết với Task; không tạo thêm thẻ chỉ để tách các bước kiểm tra nhỏ.

### Đại — Quyền, giải đấu và bản nộp

Assignee: **Đại**. Reviewer: **Trưởng nhóm**. Phối hợp: Minh; Phúc đối chiếu phần ML/giải.

#### S4-D-1 — Regress-Auth-Tournaments-Admin-UI

**Description:**

- Chạy ma trận admin/staff/client cho auth, nhân viên, khóa/đổi mật khẩu và truy cập chéo.
- Hồi quy đăng ký/hủy/đóng giải, đề xuất/xác nhận đội và UI voucher; đối chiếu SQL/hai máy, sửa và kiểm tra lại lỗi chặn.

**Dependencies:** `S3-D-2`, `S3-D-3`, `S3-P-3`

**Done khi:**

- [ ] **Hồi quy auth, nhân viên và truy cập trái quyền.** Ma trận quyền qua API đủ admin/staff/client, khóa/đổi mật khẩu và truy cập chéo; lỗi chặn được sửa, kiểm tra lại.
- [ ] **Hồi quy giải đấu, chia đội và UI voucher.** Chạy đủ đăng ký/hủy/đóng giải, đề xuất/xác nhận đội, form voucher; kết quả khớp SQL và hai máy; lỗi đã sửa có bằng chứng.

**Gom từ mã cũ:** `S4-D-01`, `S4-D-02`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S4-D-2 — Prepare-Setup-Guide-Final-Demo

**Description:**

- Tổng hợp cấu hình, cách cài/chạy frontend/API/SQL/model và giới hạn thật, rà secret trước bàn giao.
- Cùng các owner diễn tập toàn hệ thống trên bản nộp, lưu bằng chứng và danh sách đạt/chưa đạt; nhờ người khác làm theo hướng dẫn.

**Dependencies:** `S4-D-1`, `S4-K-2`, `S4-M-2`, `S4-P-2`, `S4-T-2`

**Done khi:**

- [ ] **Hoàn thiện cấu hình và hướng dẫn khởi chạy.** Rà cấu hình/secret, cách cài/chạy frontend/API/SQL/model; người khác làm theo được, giới hạn được ghi đúng.
- [ ] **Diễn tập bản nộp và chốt danh sách lỗi.** Có kịch bản toàn hệ thống, bằng chứng và danh sách đạt/chưa đạt; các owner cùng demo, không ghi provider giả lập thành tích hợp thật.

**Gom từ mã cũ:** `S4-D-03`, `S4-D-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Minh — Hồi quy ví/ZaloPay và bằng chứng

Assignee: **Minh**. Reviewer: **Trưởng nhóm**. Phối hợp: Trường; Đại đối chiếu quyền/cấu hình.

#### S4-M-1 — Regress-Wallet-ZaloPay-Reconciliation

**Description:**

- Hồi quy nạp tại quầy, debit/refund, thiếu tiền, retry và ledger/số dư.
- Chạy ZaloPay sandbox TopUp/Order, callback sai/lặp/trễ, query, restart và provider lỗi.
- Đối soát ví–ledger–gateway–đơn/chứng từ cho thành công/hủy/hoàn/hết hạn; sửa và kiểm tra lại khoản lệch/lặp.

**Dependencies:** `S3-M-3`, `S4-K-1`

**Done khi:**

- [ ] **Hồi quy nạp tại quầy, ví và hoàn tiền.** Thành công/thất bại/lặp/thiếu tiền đều có kết quả; ledger/số dư đúng và lỗi chặn được kiểm tra lại.
- [ ] **Hồi quy ZaloPay cho nạp ví và checkout.** Có sandbox chính thức cho cả hai mục đích; kiểm tra callback sai/lặp/trễ, query, restart và provider lỗi; redirect không tự ghi có.
- [ ] **Đối soát bộ dữ liệu nghiệm thu.** Đối chiếu ví–ledger–gateway–đơn/chứng từ, kể cả hoàn/hết hạn; không có khoản trùng hoặc chênh chưa giải thích.

**Gom từ mã cũ:** `S4-M-01`, `S4-M-02`, `S4-M-03`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S4-M-2 — Document-ZaloPay-Sandbox-Handoff

**Description:**

- Bàn giao cấu hình sandbox, cách chạy nạp ví/checkout và cách xử lý chờ/lỗi.
- Chuẩn bị log đã che secret và kịch bản để thành viên khác chạy lại; xác nhận cấu hình chỉ trỏ sandbox.

**Dependencies:** `S4-M-1`

**Done khi:**

- [ ] **Bàn giao cấu hình và kịch bản sandbox.** Hướng dẫn tái hiện, trạng thái lỗi/chờ và log che secret; cấu hình chỉ sandbox; người khác chạy lại được.

**Gom từ mã cũ:** `S4-M-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Phúc — Nghiệm thu ML, LAN và phục hồi

Assignee: **Phúc**. Reviewer: **Trưởng nhóm**. Phối hợp: Đại; từng owner xác nhận event module mình.

#### S4-P-1 — Regress-Sessions-LAN-Realtime

**Description:**

- Đo 10 lượt mỗi luồng realtime trên hai máy LAN, thử mất kết nối/reconnect/restart và quyền nhận event.
- Hồi quy máy/phiên/Home/OP.GG, API/provider lỗi, hết tiền và responsive; đối chiếu tiền phiên, sửa rồi đo/kiểm tra lại lỗi.

**Dependencies:** `S3-P-1`, `S3-P-2`, `S3-P-3`

**Done khi:**

- [ ] **Nghiệm thu tốc độ và quyền realtime trên LAN.** Ghi 10 lượt mỗi luồng, mục tiêu tối đa 2 giây; thử mất/kết nối/restart, không F5 hoặc lộ event khác quyền; sửa và đo lại phần lỗi.
- [ ] **Hồi quy máy, phiên, Home và OP.GG.** Đủ trạng thái API/provider lỗi, hết tiền/restart và responsive; không mất dữ liệu đã lưu; tiền phiên đối chiếu được.

**Gom từ mã cũ:** `S4-P-01`, `S4-P-02`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S4-P-2 — Package-ML-Model-LAN-Demo

**Description:**

- Bàn giao IP/cổng/CORS, hướng dẫn chạy hai máy và xử lý lỗi kết nối để người khác tái hiện.
- Chạy đánh giá ML cuối, đóng gói model/version/train/infer/giới hạn; cùng Đại demo chia đội và các ca thiếu người/dữ liệu, lệch role, model lỗi.

**Dependencies:** `S3-P-3`, `S4-P-1`

**Done khi:**

- [ ] **Bàn giao cấu hình và hướng dẫn LAN.** Có IP/cổng/CORS/cách chạy API/frontend và kết nối hai máy; hướng dẫn reconnect và sửa lỗi được kiểm tra bởi người khác.
- [ ] **Đánh giá cuối và đóng gói ML chia đội.** Chạy bộ đánh giá và ca thiếu người/dữ liệu, lệch role, model lỗi; bàn giao version/model/cách train/infer/giới hạn; demo cùng Đại hai máy và ghi kết quả thật.

**Gom từ mã cũ:** `S4-P-03`, `S4-P-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Khang — Hồi quy đơn/menu/voucher

Assignee: **Khang**. Reviewer: **Trưởng nhóm**. Phối hợp: Trường; Minh đối chiếu thanh toán/hoàn tiền.

#### S4-K-1 — Regress-Orders-Payments-Vouchers

**Description:**

- Hồi quy đặt/hủy/thu/hoàn với ví, tiền mặt, ZaloPay và voucher xuyên UI–API–SQL.
- Kiểm tra tranh tồn/lượt cuối, double click, retry, sửa giá và lỗi giữa transaction; sửa và kiểm tra lại lỗi chặn.
- Lập bảng đối chiếu đơn–ví–tồn–chứng từ, gồm thành công/hủy/hoàn/hết hạn để Minh/Trường dùng chung.

**Dependencies:** `S3-K-1`, `S3-K-2`, `S3-M-3`

**Done khi:**

- [ ] **Hồi quy đặt/hủy/thu/hoàn các phương thức.** Ví/tiền mặt/ZaloPay cùng voucher hoạt động từ UI tới SQL; hủy từng món/đơn đúng policy và lỗi chặn đã sửa.
- [ ] **Hồi quy tranh tồn, voucher và gửi lặp.** Hai request tranh lượt/tồn cuối, double click, retry, sửa giá và lỗi giữa transaction không làm sai tiền/tồn/lượt; có kết quả kiểm tra lại.
- [ ] **Đối chiếu đơn với ví/tồn/chứng từ.** Bảng đối chiếu gồm thành công/hủy/hoàn/hết hạn, giữ snapshot tiền/giảm; dữ liệu dùng được cho Minh/Trường đối soát.

**Gom từ mã cũ:** `S4-K-01`, `S4-K-02`, `S4-K-03`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S4-K-2 — Prepare-Menu-Data-Ordering-Demo

**Description:**

- Chuẩn bị bộ menu demo có chủ đích và kịch bản khách–admin dựa trên API thật.
- Chạy gợi ý/giỏ/tìm/lọc/mobile và ghi bằng chứng admin nhận đơn, khách nhận trạng thái sau cập nhật.

**Dependencies:** `S3-K-2`, `S4-K-1`

**Done khi:**

- [ ] **Chuẩn bị menu và kịch bản khách–admin.** Dữ liệu demo có chủ đích, gợi ý/giỏ/tìm/lọc/mobile chạy được; có bằng chứng admin nhận đơn và khách nhận trạng thái; không thay nguồn API bằng mock.

**Gom từ mã cũ:** `S4-K-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

### Trường — DB, báo cáo và gói dữ liệu

Assignee: **Trường**. Reviewer: **Trưởng nhóm**. Phối hợp: Khang; Minh đối chiếu số liệu tiền.

#### S4-T-1 — Regress-Inventory-Invoices-Reports

**Description:**

- Hồi quy kho/hóa đơn/báo cáo/voucher với lọc/in/xuất, ngày, giảm/hoàn/hạn mức và quyền.
- Đối chiếu doanh thu–tiền–tồn với đơn/phiên/hoàn/ledger, không đếm nạp ví là doanh thu; sửa và kiểm tra lại lỗi, bàn giao bảng nguồn số liệu.

**Dependencies:** `S3-T-3`, `S4-K-1`, `S4-M-1`

**Done khi:**

- [ ] **Hồi quy kho, hóa đơn, báo cáo và voucher.** Có ca lọc/in/xuất, ngày/giảm/hoàn/hạn mức và quyền; số liệu tính đúng policy, lỗi chặn được sửa và kiểm tra lại.
- [ ] **Đối chiếu doanh thu, tiền và tồn bản nộp.** Báo cáo khớp đơn/phiên/hoàn và ledger, không tính nạp ví thành doanh thu; có bảng nguồn số liệu để giải thích.

**Gom từ mã cũ:** `S4-T-01`, `S4-T-03`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

#### S4-T-2 — Verify-Fresh-Database-Seed-Handoff

**Description:**

- Chạy migrations/seed trên SQL Server thử mới và kiểm tra dữ liệu demo/luồng đọc.
- Bàn giao ERD/DBML/SQL khớp migrations, seed, hướng dẫn cấu hình/reset DB thử và bảng đối chiếu được các owner xác nhận.

**Dependencies:** `S3-T-3`, `S4-T-1`

**Done khi:**

- [ ] **Kiểm tra cài SQL Server từ migrations/seed.** Tạo DB thử mới thành công, chạy dữ liệu demo và luồng đọc; hướng dẫn cấu hình/reset chỉ rõ DB thử, không ghi đè dữ liệu chung.
- [ ] **Bàn giao ERD, seed và dữ liệu demo.** Schema mô tả khớp migration; gói seed/hướng dẫn và bảng đối chiếu dùng lại được, các owner đối chiếu phần dữ liệu của mình.

**Gom từ mã cũ:** `S4-T-02`, `S4-T-04`. Đây là mã tra cứu, không tạo lại các thẻ cũ.

## Dự phòng 20–21/11 và thứ tự bàn giao

Giữ hai ngày cuối để sửa lỗi chặn, kiểm tra lại phần bị ảnh hưởng, đóng gói và diễn tập; không xếp thêm tính năng mới.

- Bắt đầu sớm S1-D-1 (tài khoản/API auth), S1-M-2 (sandbox) và S1-P-3 (dữ liệu/ML); cung cấp hợp đồng trong các Task trước để nhóm cùng triển khai.
- S2 ưu tiên nghiệp vụ ví S2-M-1, adapter gateway S2-M-2, tồn S2-T-1, chứng từ S2-T-2 và voucher S2-T-3. Khang/Phúc dùng các điểm tích hợp này; mỗi owner tự nối event/UI phần mình.
- Đại cung cấp giải/đăng ký ở S2-D-2 cho Phúc làm OP.GG/model; sau đó S2-D-4 ghép UI đề xuất ML. Phúc giữ owner ML, Đại giữ luồng xác nhận/lưu đội.
- Các tiêu chí từ 100 việc cũ đã được giữ nguyên hoặc đổi mã tham chiếu trong checklist. Không dùng việc gom thẻ để giảm phạm vi auth, ZaloPay TopUp/Order, ML, khuyến mãi, kho/hóa đơn/báo cáo hay demo hai máy.
