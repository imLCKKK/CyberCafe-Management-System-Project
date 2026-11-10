# Hướng dẫn AI làm việc với dự án CyberCafe

Cập nhật 02/11/2026 theo quyết định dùng backend 35 bảng; giữ lịch/phân công baseline 31/10/2026. Đọc file này trước khi sửa code, sau đó đọc đúng task trong [SPRINT_TASKS.md](SPRINT_TASKS.md). File này đủ để nắm hướng đi; các liên kết giúp kiểm tra chi tiết. Luôn đối chiếu trạng thái code/nhánh hiện tại, không coi ngày ghi trong tài liệu là bằng chứng chức năng đã hoàn thành.

## 1. Dự án làm gì và đã chốt điều gì?

Hệ thống quản lý phòng máy: admin/nhân viên quản lý máy, phiên, khách, ví, món/đơn, kho, nhân viên, hóa đơn, báo cáo, giải đấu và khuyến mãi; khách dùng web để xem phiên, gọi món, dùng ví và đăng ký giải.

| Nội dung | Quyết định của nhóm |
| --- | --- |
| Stack | React/Vite + ASP.NET Core API .NET 10 + EF Core + SQL Server |
| Backend/DB | `Backend/CyberManagement.slnx`, Clean Architecture, SQL Server mới 35 bảng; EmployeeId/CustomerId kiểu int |
| Demo | Hai máy mở web trên cùng LAN, dùng chung API/SQL; SignalR báo thay đổi, mục tiêu cập nhật trong 2 giây được đo khi nghiệm thu |
| Máy trạm | Không xây phần mềm khóa/điều khiển Windows |
| Thanh toán | ZaloPay sandbox chính thức cho nạp ví và thanh toán đơn món; giữ ví/tiền mặt/nạp tại quầy; không dùng tiền thật |
| Khuyến mãi | Voucher món ăn giảm % hoặc số tiền cố định, ngày hiệu lực, đơn tối thiểu, hạn mức; server tính và lưu snapshot trên chứng từ |
| Giải đấu | Quản lý giải/đăng ký/đội, thống kê OP.GG; bắt buộc có ML hỗ trợ chia đội, Phúc phụ trách |
| Nhận diện | NEXORA CYBER; nền đen `#0B0F14`, tím `#A855F7`, cyan `#22D3EE`, chữ `#E5F0FF`. Concept logo nhóm sẽ chốt chiều 31/10/2026; chưa tự chọn một trong ba mẫu. Phúc triển khai token/layout trước, đối chiếu asset đã duyệt trước khi hoàn tất logo |
| Hạn nộp | 21/11/2026; S1 31/10–05/11, S2 06–10/11, S3 10–15/11, S4 15–19/11; ngày 10/11 và 15/11 là mốc bàn giao giữa hai sprint; dự phòng 20–21/11 |
| Cách giao việc | 55 Task Jira độc lập: 53 Task đã gom, chủ yếu 2–3 Task/người/sprint, và 2 Task UI giao Phúc theo yêu cầu mới; các bước nhỏ là checklist, không tạo Task cha/Sub-task |
| Reviewer | **Trưởng nhóm**, chính là người yêu cầu lập bộ tài liệu này và trực tiếp duyệt thay đổi |

Chưa có tên/account Jira hoặc GitHub của reviewer trong tài liệu. Không tự suy ra đó là Đại, Minh hay tài khoản đang chạy AI trên máy khác. Các thành viên hỗ trợ đối chiếu nghiệp vụ; quyền duyệt cuối thuộc Trưởng nhóm. AI hỗ trợ thực hiện hoặc review theo yêu cầu, không tự nhận là người đã duyệt.

## 2. Hiện trạng cần hiểu trước khi code

Ngày 02/11/2026, Trưởng nhóm chọn backend **CyberManagement và database mới 35 bảng** của phần auth làm nền chung. Solution là `Backend/CyberManagement.slnx`; giữ bốn tầng Domain/Application/Infrastructure/API và project kiểm tra auth. Bản CyberCafe 19 bảng thuộc lịch sử Git, không còn là backend/schema đích.

- API có login/logout nhân viên và khách, JWT, phân quyền, danh sách role và `/health`. Tài liệu định danh/quyền: [001-monorepo.md](../Backend/docs/001-monorepo.md). Đây chưa phải quyết định nghiệm thu Task của Trưởng nhóm.
- Database mới được tạo từ bốn migration hiện có trong `Backend/src/CyberManagement.Infrastructure/Migrations/`. Không áp chuỗi InitialCreate này lên DB 19 bảng cũ. Yêu cầu hiện tại không chuyển/xóa dữ liệu cũ; giữ DB cũ riêng nếu đang có.
- Staff dùng `Employees.EmployeeId`, customer dùng `Customers.CustomerId` (int). Không có bảng Account riêng. Chủ module đối chiếu mapping UI demo/account ID và hợp đồng auth trước khi nối API.
- React vẫn chủ yếu dùng localStorage. Nối form login và các luồng UI thuộc Task hiện hữu; không coi có backend auth là UI đã tích hợp.
- OP.GG vẫn là adapter Node ở `Backend/integrations/opgg/`, được Vite gắn middleware. Giữ adapter khi thay backend .NET.
- SignalR hai máy, ZaloPay, ML và các API nghiệp vụ còn thực hiện theo backlog; kiểm tra branch/code thực tế trước khi kết luận đã có hoặc chưa có.
- Đợt sửa theo yêu cầu đang dừng trước commit trên checkout riêng; không mặc định đã push/merge vào `dev`. Giữ nguyên bản foundation local khác và lấy đúng commit được Trưởng nhóm tích hợp làm base cho nhóm.

Không phục hồi prototype `cybernet-html/` đã xóa. Không nhập nguyên migration/provider MySQL từ nhánh cũ. Tài liệu có ngày baseline phải được đối chiếu với source mới.

## 3. Bản đồ thư mục

```text
frontend/
  src/app/                       router, layout, theme
  src/admin/<feature>/           màn hình admin
  src/client/<feature>/          màn hình khách
  src/shared/                    UI dùng cả hai phía
  tests/                         browser tests
Backend/
  CyberManagement.slnx
  src/CyberManagement.Domain/             entity, không EF/HTTP
  src/CyberManagement.Application/        DTO, use case, interface, validation
  src/CyberManagement.Infrastructure/
    Persistance/                         ApplicationDbContext, cấu hình EF, factory
    Migrations/                          lịch sử schema 35 bảng
    Repositories/, Security/             implementation lưu trữ/hash
  src/CyberManagement.API/                Controller, JWT/HTTP, cấu hình DI
  tests/CyberManagement.API.Checks/       auth + seed regression; InMemory/SQL
  integrations/opgg/                     adapter Node và helper browser/Node
docs/                            kiến trúc và báo cáo lịch sử
planning/                        lịch sprint, phân công, quy trình
codex/                           hướng dẫn và 55 Task
scripts/                         wrapper Windows dùng chung
```

Tên thư mục chuẩn là **`Backend`** (chữ B hoa), kể cả import JavaScript và lệnh chạy trên Linux. Application chỉ tham chiếu project Domain; Infrastructure tham chiếu Application/Domain; API ghép DI và gọi Application. Application/Domain không dùng DbContext/HttpContext. Hub SignalR và adapter HTTP thuộc tầng ngoài.

Owner triển khai module xuyên tầng và UI. Endpoint theo cấu trúc `Controller/` hiện có; use case/DTO/interface đặt gần nghiệp vụ; interface dùng chung có thể ở `Interfaces/`. Không thêm thư mục rỗng, generic repository, framework hoặc backend riêng theo người. UI một màn hình đặt cạnh màn hình; dùng chung admin/client vào shared phù hợp. API client frontend được thêm theo Task khi có code thật.

## 4. Luồng dữ liệu cần theo

### Luồng hiện tại của bản demo

```text
frontend/index.html → src/main.jsx → app/routes.jsx
  → AdminLayout / ClientLayout → trang theo nghiệp vụ
  → provider/model demo → localStorage

Vite /api/valorant/opgg/stats → Backend/integrations/opgg → OP.GG
```

Ba kho demo cần đọc trước khi thay nghiệp vụ:

| Khóa | Nơi sở hữu | Lưu ý |
| --- | --- | --- |
| `tram.client.dbml.demo.v2` | `client/shared/ClientDemoContext.jsx`, `demoModel.js`, `demoData.js` | Nhiều màn hình admin/client dùng cùng dữ liệu |
| `tram.client.home.demo.v1` | `client/home/useHomeData.js`, `homeModel.js` | Dữ liệu giải đấu cũ và các màn hình liên quan |
| `net-cafe.admin.computers.v1` | `admin/computers/computerStore.js` | Máy/dashboard/báo cáo |

`AdminContext` đang dùng `ClientDemoProvider` và `useHomeData`. Sửa model client có thể ảnh hưởng admin. `storage` event chỉ đồng bộ các tab cùng trình duyệt/origin; không đáp ứng yêu cầu hai máy.

### Luồng phải đạt khi chuyển sang hệ thống thật

```text
Người dùng thao tác React
  → API .NET xác thực, kiểm tra quyền và dữ liệu
  → nghiệp vụ tính giá/tiền/giảm giá, kiểm tra trạng thái
  → transaction EF/SQL lưu dữ liệu liên quan
  → commit thành công
  → SignalR thông báo tới đúng tài khoản/vai trò
  → màn hình liên quan đọc lại API để hiển thị trạng thái đã lưu
```

- Server quyết định giá, quyền, tài khoản, trạng thái, số dư và tồn. Không tin các giá trị tiền/quyền do browser tự gửi.
- Ví/ledger, đơn/tồn, lượt voucher/chứng từ thuộc cùng một thao tác phải nhất quán. Có chống xử lý lặp và kiểm soát đồng thời; snapshot giá/giảm không đổi khi sản phẩm/voucher sửa sau đó.
- Chuyển từng luồng UI–API–SQL theo task. API lỗi hiển thị lỗi; không tự trả localStorage giả rồi coi là thành công. Giữ các luồng demo chưa chuyển để tránh phá giao diện ngoài phạm vi.
- SignalR gửi sau commit, giới hạn nhóm theo server; reconnect đọc lại API. Event không phải lệnh để client tự cộng ví hay trừ tồn.
- ZaloPay: server tạo giao dịch TopUp/Order; callback xác minh chữ ký/giao dịch/số tiền hoặc truy vấn provider mới ghi nhận. Redirect của browser không chứng minh thanh toán; callback/query lặp không ghi tiền hai lần. Không giữ SQL transaction trong thời gian chờ provider.
- Đơn chờ ZaloPay cần policy giữ tồn/lượt voucher, hết hạn, callback muộn và hoàn tiền. Quy tắc cụ thể chốt ở S1, không tự đổi phương thức hoàn tiền.
- ML: đăng ký hợp lệ → Phúc cung cấp dữ liệu/model/suy luận và đề xuất đội → Đại tích hợp UI/xác nhận → API lưu đội cùng version/snapshot → SignalR. Có dữ liệu đánh giá và baseline; phân biệt mô hình học từ dữ liệu với thuật toán ghép đội viết tay.
- Quyết định ngày 10/11/2026: baseline/fallback SCRUM-4 và SCRUM-9 dùng chung `rank-snake-v1`: sắp `OpggSnapshot.rank.tier` giảm dần, hòa theo `RegistrationId` tăng dần rồi phân snake; không xếp theo SkillScore tổng hợp. Thiếu rank OP.GG hợp lệ báo lỗi; dự phòng có nhãn/phương pháp riêng. Yêu cầu ML và đánh giá so baseline giữ nguyên.
- OP.GG lỗi/không có dữ liệu phải hiện đúng; fixture chỉ dùng kiểm thử, không ghi thành dữ liệu provider thật.

## 5. Người sở hữu từng phần

| Thành viên | Phần thực hiện và phối hợp |
| --- | --- |
| Đại | Auth/quyền/nhân viên, luồng giải đấu/đăng ký/UI xác nhận/lưu đội; UI CRUD voucher; điều phối tích hợp |
| Minh | Khách/hồ sơ, ví/ledger, nạp tại quầy, ZaloPay; thao tác tiền dùng chung |
| Phúc | Máy/phiên/tiền giờ/Home, SignalR, OP.GG, **ML chia đội**, nhận diện NEXORA/theme chung và ảnh món |
| Khang | Danh mục/sản phẩm/menu/gợi ý/đơn/checkout/hủy; tích hợp voucher và gateway vào đơn |
| Trường | Kho/chứng từ/hóa đơn/báo cáo; API/rule voucher; điều phối thứ tự migration |

Owner không đồng nghĩa reviewer. Tất cả Task gửi **Trưởng nhóm** duyệt. Trường điều phối migration nhưng từng owner viết entity/config của mình; Phúc cung cấp realtime chung nhưng từng owner tự nối event/UI module mình.

Phúc nhận thêm **S1-U-1** (NEXORA CYBER/logo/theme cyan–tím–đen) và **S2-U-1** (ảnh món/component thẻ món) theo phân công mới. Giữ mã U để tra cứu; Assignee là Phúc. Phúc có 4 Task ở S1/S2, ưu tiên bàn giao contract/hook và component ảnh sớm cho người tích hợp; không gộp UI vào ML để che tải công việc. Khang giữ logic vòng quay/checkout trong **S2-K-3**, hồi quy ở **S3-K-2**; Phúc cung cấp token/component/asset, không tạo Task trùng. Trưởng nhóm review cuối, các owner kiểm tra chéo module của mình.

## 6. Quy trình khi AI nhận một Task

1. Đọc yêu cầu hiện tại, xác định người thực hiện, mã tham chiếu trong `SPRINT_TASKS.md` và Jira key nếu đã có. Chỉ hỏi phần thiếu ảnh hưởng triển khai; không hỏi lại các quyết định đã chốt ở mục 1.
2. Kiểm tra `git status --short`, nhánh, diff và chỉ dẫn áp dụng. Giữ thay đổi có sẵn của người dùng; không reset/clean/khôi phục file bị xóa ngoài phạm vi. Dùng đường dẫn tương đối repo, không giả định máy nào cũng có ổ `D:` hoặc thư mục `.tools`.
3. Đọc **Description**, **Dependencies** và từng mục **Done khi** của Task; đây lần lượt là phạm vi, phụ thuộc và kết quả phải chứng minh. Đọc code thực tế của module, các caller/provider và test liên quan. Phân biệt phụ thuộc chỉ cần contract với phụ thuộc phải chạy tích hợp thật.
4. Xác định branch/base đang chứa cấu trúc mới. Summary dùng nguyên tên Task dạng `Build-Admin-Client-Login`; người thực hiện tạo branch từ Jira theo flow của nhóm, không cần viết sẵn trường Branch trong backlog. Dùng key thật của issue khi liên kết, không coi mã minh họa là issue đang tồn tại. Tránh làm mất diff chưa commit khi đổi nhánh.
5. Với giao điểm module, thống nhất DTO/endpoint/trạng thái/lỗi/event/schema trước khi triển khai khác nhau. Các quyết định còn mở ở [kế hoạch bàn giao](../planning/team-delivery-plan.md), mục 11, cần được ghi lại khi chốt.
6. Sửa trong phạm vi task, giữ hành vi hiện có ngoài phạm vi. EF migrations trong CyberManagement.Infrastructure là lịch sử schema chính của DB 35 bảng; DBML/SQL xuất là tài liệu đối chiếu. Không sửa migration đã chạy ở DB chung; xác nhận đúng DB thử trước khi áp migration.
7. Chạy kiểm tra phù hợp ở mục 7; ghi lệnh, kết quả và giới hạn. Không ghi test đã pass nếu chưa chạy hoặc chỉ dùng fixture thay hệ thống thật.
8. Tự rà bằng `REVIEW_GUIDE.md`, sửa lỗi tìm thấy trong phạm vi task, rồi bàn giao một báo cáo tổng hợp theo Task để Trưởng nhóm review. Giữ các bước nhỏ trong checklist; có thể chia commit/PR để bàn giao sớm nhưng cùng tham chiếu một Task. Chỉ tách thêm thẻ nếu có kết quả/blocker độc lập cần theo dõi. AI báo “sẵn sàng review”, không tự xác nhận Trưởng nhóm đã duyệt hoặc Task đã Done.

## 7. Chạy và kiểm tra

Từ thư mục gốc repo trên Windows, cần Node.js 22+ và .NET 10 SDK. Wrapper ưu tiên công cụ portable nếu có, rồi dùng bản đã cài. `global.json` chọn .NET 10 ổn định. Cài dependency khi clone mới hoặc lockfile thay đổi:

```powershell
.\scripts\npm-local.cmd --prefix frontend ci
.\scripts\npm-local.cmd --prefix frontend run dev
```

Hoặc dùng `npm.cmd --prefix frontend ...` trực tiếp. `scripts/start.cmd` cài dependency nếu thiếu và chạy Vite tại `127.0.0.1:5173`; chưa chạy API/SQL hay thiết lập LAN.

```powershell
.\scripts\dotnet-local.cmd run --project Backend\src\CyberManagement.API --no-launch-profile --urls http://127.0.0.1:5088
```

`/health` là liveness, không chứng minh kết nối SQL. API cần cấu hình connection string và signing key theo hướng dẫn backend trước khi chạy. Cấu hình DB, EF CLI và migration theo [Backend/README.md](../Backend/README.md); dùng secret/biến môi trường server, không commit credential. LAN/SignalR/ZaloPay cần các task triển khai và cấu hình riêng, không suy ra đã có từ lệnh dev.

| Phạm vi sửa | Kiểm tra phù hợp đang có |
| --- | --- |
| Tài liệu | Link/đường dẫn, owner/reviewer, tính nhất quán task; `git diff --check` |
| UI/import/config | Build frontend; render hoặc browser suite liên quan |
| Model demo dùng chung | Model test và các luồng browser bị ảnh hưởng |
| C# / EF | Build API; khi có nghiệp vụ, thêm/chạy test API/SQL có ý nghĩa cho phần sửa |
| Ví/đơn/tồn/promo/gateway | Thành công, lỗi, gửi lặp, cạnh tranh và tính nhất quán SQL; UI demo test không thay thế các ca này |
| SignalR / ML | Hai máy và reconnect/quyền; hoặc dữ liệu đánh giá/model/luồng chia đội theo task |

Các lệnh hiện có để chọn dùng:

```powershell
.\scripts\npm-local.cmd --prefix frontend run build
.\scripts\npm-local.cmd --prefix frontend run test:frontend:models
.\scripts\npm-local.cmd --prefix frontend run test:frontend:render
.\scripts\npm-local.cmd --prefix frontend run test:frontend -- routes
.\scripts\npm-local.cmd --prefix frontend run test:frontend -- client-workflows cross-tab
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx
```

`test:frontend` không truyền tên suite sẽ chạy bộ browser đầy đủ. Cần Chrome/Edge, có thể đặt `CHROME_PATH`. Log/ảnh trong `.tools/frontend-retest/` là cục bộ, không đi theo clone. Bộ mặc định dùng fixture OP.GG; suite `opgg-live` mới gọi nguồn thật. Chọn kiểm tra theo thay đổi; không cần chạy cả bộ app khi chỉ sửa tài liệu.

## 8. Bàn giao cuối phiên

Gửi lỗi, cách sửa và kết quả kiểm tra trực tiếp trong chat. Chỉ tạo file báo cáo khi người dùng yêu cầu. Không tự đăng nhận xét GitHub/Jira, commit, merge hoặc push.

```text
Task / Jira key / người thực hiện:
Nhánh / base hoặc commit / thay đổi chưa commit:
Đã làm và hành vi thay đổi:
File chính và module bị ảnh hưởng:
Thay đổi contract/schema/event/cấu hình:
Kiểm tra đã chạy: lệnh → kết quả → bằng chứng
Chưa kiểm tra / blocker / giới hạn:
Tiêu chí task: đạt / chưa đạt, lý do
Đề nghị: sẵn sàng để Trưởng nhóm review / cần làm tiếp
```

Không ghi secret vào bàn giao. Không coi việc soạn tài liệu/backlog là đã tạo issue hoặc giao việc trên Jira. Cập nhật task/PR theo đúng hành động được yêu cầu và kết quả thực tế.

## Prompt cho thành viên copy sang AI của mình

```text
Đọc codex/PROJECT_GUIDE.md và task <mã tham chiếu> trong codex/SPRINT_TASKS.md.
Tôi là <tên thành viên>, làm Jira <key thật nếu có>.
Kiểm tra nhánh/diff và code hiện tại trước khi sửa. Thực hiện task theo flow
đã chốt, giữ thay đổi ngoài phạm vi, chạy kiểm tra phù hợp và bàn giao bằng chứng.
Reviewer cuối là Trưởng nhóm; tự kiểm tra bằng codex/REVIEW_GUIDE.md trước khi bàn giao.
```
