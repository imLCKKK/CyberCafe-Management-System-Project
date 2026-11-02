# Hướng dẫn AI làm việc với dự án CyberCafe

Cập nhật 31/10/2026. Đọc file này trước khi sửa code, sau đó đọc đúng task trong [SPRINT_TASKS.md](SPRINT_TASKS.md). File này đủ để nắm hướng đi; các liên kết giúp kiểm tra chi tiết. Luôn đối chiếu trạng thái code/nhánh hiện tại, không coi ngày ghi trong tài liệu là bằng chứng chức năng đã hoàn thành.

## 1. Dự án làm gì và đã chốt điều gì?

Hệ thống quản lý phòng máy: admin/nhân viên quản lý máy, phiên, khách, ví, món/đơn, kho, nhân viên, hóa đơn, báo cáo, giải đấu và khuyến mãi; khách dùng web để xem phiên, gọi món, dùng ví và đăng ký giải.

| Nội dung | Quyết định của nhóm |
| --- | --- |
| Stack | React/Vite + ASP.NET Core API .NET 10 + EF Core + SQL Server |
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

Baseline kiểm tra ngày 31/10/2026, trước các task triển khai:

- React đang chạy bản demo với dữ liệu nghiệp vụ chủ yếu trong localStorage. Có giao diện/hành vi hữu ích để giữ lại; auth admin/client và khuyến mãi còn placeholder.
- `backend/CyberCafe.Api/Program.cs` mới có `/health` và OpenAPI development; EF có schema/migration ban đầu 19 bảng. Chưa có API nghiệp vụ cấp dữ liệu cho các trang.
- OP.GG là adapter Node riêng, được Vite gắn middleware; đây không phải backend quản lý phòng máy.
- Chưa triển khai SignalR hai máy, ZaloPay sandbox và ML chia đội trong nhánh nền đã khảo sát. Kiểm kê lại code Phúc có khi nhận task ML; không tự kết luận các nhánh khác không có gì.
- Việc dọn cấu trúc nằm trên nhánh `refactor/project-foundation` tại lần lập tài liệu. Không mặc định đã commit/merge/push hoặc có trên máy của thành viên khác. Kiểm tra Git thật trước khi bắt đầu.
- `cybernet-html/` đã được chủ dự án chủ động xóa. Không dựng lại prototype này. Backend trên các nhánh cũ có khác schema/provider; chọn lọc code phù hợp, không merge nguyên migration MySQL vào SQL Server.

Nếu code hiện tại đã tiến xa hơn baseline, báo chênh lệch và cập nhật tài liệu dựa trên bằng chứng; không kéo code về trạng thái cũ để khớp mô tả.

## 3. Bản đồ thư mục

```text
frontend/
  package.json, package-lock.json, vite.config.js, index.html
  public/
  src/
    main.jsx
    app/                  router, layout, theme
    admin/<feature>/      màn hình quản trị theo nghiệp vụ
    client/<feature>/     màn hình khách theo nghiệp vụ
    admin/shared/         UI/CSS và logic demo phía admin
    client/shared/        UI, provider và model demo dùng nhiều nơi
    shared/               UI dùng chung cả hai phía
  tests/                  kiểm tra luồng trình duyệt
backend/
  CyberCafe.Api/
    Program.cs
    Data/                 entity, DbContext, migrations
  integrations/opgg/      adapter Node và helper dùng cả browser/Node
  database/               SQL xuất và DBML mô tả
docs/                     kiến trúc/đặc tả và báo cáo
planning/                 kế hoạch sprint và quy trình nhóm
codex/
  PROJECT_GUIDE.md        đọc trước khi làm việc
  REVIEW_GUIDE.md         cách review và báo cáo cho Trưởng nhóm
  SPRINT_TASKS.md         nguồn chính của 55 Task và checklist bên trong
scripts/                  tiện ích Windows để gọi npm/.NET/EF/chạy frontend
```

Thêm file vào module đang sở hữu nghiệp vụ. UI chỉ dùng một màn hình đặt gần màn hình đó; dùng chung một phía vào `admin/shared` hoặc `client/shared`; dùng cả hai vào `src/shared`. Khi nối API, thêm client/service theo nghiệp vụ trong `frontend/src/services/`; endpoint/DTO/nghiệp vụ vào `backend/CyberCafe.Api/Features/<feature>/` khi cần. Hai đường dẫn này là hướng tổ chức, chưa khẳng định đã có code.

Không tạo backend riêng theo người, tách backend admin/client, tạo thư mục rỗng hay `mockData.js` rỗng để đánh dấu nhiệm vụ. Chỗ đặt model/runtime ML chốt sau khi kiểm kê phần Phúc đã làm, không tự thêm một service/ngôn ngữ vào stack.

## 4. Luồng dữ liệu cần theo

### Luồng hiện tại của bản demo

```text
frontend/index.html → src/main.jsx → app/routes.jsx
  → AdminLayout / ClientLayout → trang theo nghiệp vụ
  → provider/model demo → localStorage

Vite /api/valorant/opgg/stats → backend/integrations/opgg → OP.GG
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
6. Sửa trong phạm vi task, giữ hành vi hiện có ngoài phạm vi. EF migrations là lịch sử schema chính; DBML/SQL xuất là tài liệu đối chiếu. Không sửa migration đã chạy ở DB chung; xác nhận đúng DB thử trước khi áp migration.
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
.\scripts\dotnet-local.cmd run --project backend\CyberCafe.Api --no-launch-profile --urls http://127.0.0.1:5088
```

Ở baseline, `/health` chạy được không chứng minh kết nối SQL. Cấu hình DB, EF CLI và migration theo [backend/README.md](../backend/README.md); dùng secret/biến môi trường server, không commit credential. LAN/SignalR/ZaloPay cần các task triển khai và cấu hình riêng, không suy ra đã có từ lệnh dev.

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
.\scripts\dotnet-local.cmd build backend\CyberCafe.Api\CyberCafe.Api.csproj
```

`test:frontend` không truyền tên suite sẽ chạy bộ browser đầy đủ. Cần Chrome/Edge, có thể đặt `CHROME_PATH`. Log/ảnh trong `.tools/frontend-retest/` là cục bộ, không đi theo clone. Bộ mặc định dùng fixture OP.GG; suite `opgg-live` mới gọi nguồn thật. Chọn kiểm tra theo thay đổi; không cần chạy cả bộ app khi chỉ sửa tài liệu.

## 8. Bàn giao cuối phiên

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
