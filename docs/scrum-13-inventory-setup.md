# SCRUM-13 - Tích hợp migration, seed và luồng đọc kho

Cập nhật 07/11/2026. Nhánh `SCRUM-13-Integrate-Migrations-Seed-Data`.
Nền tích hợp là `dev` tại `95f33fc`, sau khi gộp SCRUM-12 và bản bổ sung `af64179`.
Người dùng xác nhận SCRUM-12 đã được duyệt; [contract](contracts/inventory-invoice-voucher.md)
trên nhánh này giữ nguyên bản đã có trên dev. Không cần lấy thêm nhánh owner để chạy phạm vi dưới đây.

## 1. Phạm vi tích hợp

- Dùng `Backend/CyberManagement.slnx`, API/Application/Domain/Infrastructure hiện có, SQL Server và ID int.
- Giữ nguyên bốn migration nền; thêm migration `20261107071954_IntegrateInventoryInvoiceVoucherContracts`.
- Schema sau tích hợp có **38 bảng**: giữ 35 bảng nguồn, thêm PromotionUsages, RefundAttempts,
  InvoiceIssueRequests. Số 35 trong tài liệu baseline không phải giới hạn cấm bổ sung bảng của contract.
- GET `/api/admin/inventory` đọc Products với JWT và `permission:stockreceipt.view`.
- Màn hình `/admin/inventory` dùng API/auth chung, tìm tên/ID, lọc hết hàng/ngừng bán, tải lại/reload;
  không lấy tồn demo từ localStorage và không giữ nút ghi vào demo trên màn hình SQL.
- Seed đọc kho đặt ở Infrastructure/DataTest; SQL và DBML xuất từ model/chuỗi migration cuối.

Đây là task tích hợp schema, seed và một luồng đọc kho. Chưa triển khai API nhập/điều chỉnh,
CRUD voucher, đặt/hủy đơn, gọi ZaloPay hay service phát hành hóa đơn. CHECK/FK/index không thay
thế service kiểm tra cùng khách, số hoàn còn lại, tổng giảm phân bổ, xác minh provider hoặc thứ tự khóa.
Các chức năng đó tiếp tục theo task triển khai tương ứng, dùng schema đã chuẩn bị ở đây.

## 2. Schema và migration

| Phần | Kết quả trong migration mới |
| --- | --- |
| Products | Giữ Stock/IsActive, CHECK tồn/giá không âm; không thêm StockQuantity trùng Stock |
| InventoryTransactions | EmployeeId nullable, Before/After, Reason, OrderDetailId và thời gian offset; CHECK dấu/delta/nguồn, unique OrderDetailId + Type có lọc; FK không cascade |
| Promotions / PromotionUsages | Code unique, Percent/Fixed, ngưỡng/trần/hạn mức nullable, thời gian offset, mỗi đơn một usage, thời điểm hoàn lượt và index đếm lượt đang giữ |
| Orders / OrderDetails | Snapshot code/loại/giá trị voucher, DiscountAmount; ProductNameSnapshot trên dòng, giữ UnitPrice; CHECK nội bảng |
| Payments | InvoiceId nullable, XOR OrderId/SessionChargeId, Type thu/hoàn, RefundOfPaymentId, OperationKey unique, liên kết ledger unique, mã gateway và thời gian xác nhận/basis |
| RefundAttempts | Metadata mã hoàn theo app, trạng thái và mã API/query; không có Amount/ledger thứ hai |
| InvoiceOrders / InvoiceDetails | Unique OrderId; FK và unique có lọc OrderDetailId/SessionChargeId để truy nguồn callback; không cascade lịch sử |
| InvoiceIssueRequests | PK OperationKey phân biệt hoa/thường ở SQL và EF, RequestHash char(64) lowercase hex, InvoiceId unique/FK NoAction |
| Tiền VND | Các cột giá món/giờ, tồn nhập, ví/ledger, đơn, phí phiên, hóa đơn và Payment chuyển decimal(18,0); DiscountValue và snapshot voucher giữ decimal(18,2) cho tỷ lệ. Các cột điểm/tỷ lệ giải đấu ngoài phạm vi giữ nguyên |

Giữ Invoice.Subtotal/Discount/TotalAmount/InvoiceDate/Status; TotalAmount là gross sau giảm lúc phát hành,
không phải tiền thực thu. PaidAmount/refund và doanh thu phải đối soát từ nguồn thu/hoàn theo contract,
không thêm cột tổng tiền trùng chức năng hoặc suy đã thu từ trạng thái hóa đơn.
PromotionUsage không tạo thêm bộ đếm trên Promotions. RefundAttempts và InvoiceIssueRequests không
được tính như một khoản thu/hoàn. Các thời điểm mới dùng DateTimeOffset; AuditLogs giữ schema UTC của auth.

Thứ tự áp trên DB thử mới:

1. `20261102065247_InitialCreate` — nền 35 bảng CyberManagement, không phải bản 19 bảng.
2. `20261102073816_CustomerPasswordAuthentication`.
3. `20261102083200_RemoveCustomerLoginLockout`.
4. `20261102084459_RemoveEmployeeLoginLockout`.
5. `20261107071954_IntegrateInventoryInvoiceVoucherContracts` — schema contract đã duyệt.

Migration mới có kiểm tra trước mọi thay đổi:

- Dừng nếu các cột tiền đang đổi có phần lẻ VND; không tự làm tròn.
- Dừng nếu InventoryTransactions, Promotions, Orders, OrderDetails, Invoices, InvoiceDetails hoặc
  Payments đã có dữ liệu cũ cần xác minh snapshot/nguồn tiền/khóa/múi giờ. Không tự tạo mã voucher,
  khóa lịch sử, tên món snapshot hay ConfirmedAt giả. Cần một phương án backfill có bằng chứng
  trước khi áp lên database như vậy; phạm vi chạy chuẩn của SCRUM-13 là DB mới riêng.
- Down dừng khi còn dữ liệu contract hoặc giá trị vượt precision cũ; không xóa lịch sử để hạ schema.
- Giữ nguyên bốn migration đã áp trước đó. Không drop/reset database người dùng để chạy qua guard.

## 3. Các file chính

| Nội dung | Đường dẫn |
| --- | --- |
| Entity | [Domain/Entities](../Backend/src/CyberManagement.Domain/Entities/) |
| Cấu hình schema | [Persistance/Configuration](../Backend/src/CyberManagement.Infrastructure/Persistance/Configuration/) |
| Migration | [Infrastructure/Migrations](../Backend/src/CyberManagement.Infrastructure/Migrations/) |
| DTO / interface đọc | [InventoryItemDto](../Backend/src/CyberManagement.Application/DTOs/Inventory/InventoryItemDto.cs), [IInventoryQueries](../Backend/src/CyberManagement.Application/Interfaces/Repositories/IInventoryQueries.cs) |
| Query EF / controller | [InventoryQueries](../Backend/src/CyberManagement.Infrastructure/Repositories/InventoryQueries.cs), [InventoryController](../Backend/src/CyberManagement.API/Controller/InventoryController.cs) |
| Seed test | [InventoryReadSeed.sql](../Backend/src/CyberManagement.Infrastructure/DataTest/InventoryReadSeed.sql) |
| SQL / DBML | [CyberManagement.sql](../Backend/database/CyberManagement.sql), [schema.dbml](../Backend/database/schema.dbml) |
| Bộ kiểm tra | [Inventory.Checks](../Backend/tests/CyberManagement.Inventory.Checks/) |
| Màn hình kho | [InventoryPage.jsx](../frontend/src/admin/inventory/InventoryPage.jsx) |

## 4. Dựng database thử

Cần .NET 10, EF CLI 10.0.x tương thích package 10.0.14, Node.js 22+ và SQL Server LocalDB.
Người thực hiện tự bật SQL. Không áp chuỗi này lên database CyberCafe 19 bảng hoặc database chung.
Dùng DB mới tên `CyberManagement_SCRUM13_Test` hoặc bắt đầu `CyberManagement_SCRUM13_`.
Nếu tên đã có và có dữ liệu thì chọn tên mới, không xóa dữ liệu để dựng một lượt fresh DB.

Trong PowerShell từ repository root, thay placeholder bằng connection string thật:

```powershell
$env:ConnectionStrings__DefaultConnection = 'YOUR_DefaultConnection'
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx
.\scripts\ef-local.cmd database update --project Backend\src\CyberManagement.Infrastructure --startup-project Backend\src\CyberManagement.API --context ApplicationDbContext
```

Hoặc dùng NuGet Package Manager Console trong cùng cấu hình:

```powershell
$env:ConnectionStrings__DefaultConnection = 'YOUR_DefaultConnection'
Update-Database -Project CyberManagement.Infrastructure -StartupProject CyberManagement.API -Context ApplicationDbContext
```

`YOUR_DefaultConnection` phải trỏ đúng server/DB thử. Factory đọc key DefaultConnection,
không dùng key CyberCafe của bản cũ. Nếu LocalDB chưa chạy, tự mở terminal và chạy:

```powershell
sqllocaldb start MSSQLLocalDB
sqllocaldb info MSSQLLocalDB
```

Nếu terminal thấy Running nhưng API không phân giải được instance, lấy dòng `Instance pipe name`
và dùng đúng địa chỉ `np:...` làm Server. Pipe thay đổi sau khi restart; không ghi pipe cụ thể vào Git.

Vào SSMS/SQL Server Object Explorer, chọn **đúng DB thử** rồi chạy toàn bộ
[InventoryReadSeed.sql](../Backend/src/CyberManagement.Infrastructure/DataTest/InventoryReadSeed.sql).
Script chặn database ngoài tiền tố SCRUM13, yêu cầu migration số 5, dùng transaction và kiểm tra fixture cũ.
Chạy lại không thêm bản sao hoặc ghi đè tồn/giá/trạng thái đã thay đổi; khác fixture phải được đối soát.

| Sản phẩm test | Stock | IsActive | Price |
| --- | ---: | --- | ---: |
| TEST_Nước suối | 20 | true | 10000 |
| TEST_Mì ly hết hàng | 0 | true | 20000 |
| TEST_Coca ngừng bán | 5 | false | 15000 |

Đây là fixture đọc kho, không tạo hóa đơn/ledger hoặc chứng minh nghiệp vụ nhập/trừ/hoàn.

```sql
SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT p.ProductId, p.ProductName, p.Stock, p.IsActive, p.Price, p.CategoryId
FROM dbo.Products p
JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
WHERE c.CategoryName = N'SCRUM13_TEST_INVENTORY'
ORDER BY p.ProductId;
```

## 5. Chạy API và màn hình

Đặt JWT signing key và năm mật khẩu seed Development theo [Backend README](../Backend/README.md)
và [auth chung](../Backend/docs/001-monorepo.md). Không commit secret hoặc đưa vào frontend.
Mật khẩu seed tối thiểu 12 ký tự; cấp đủ năm role hoặc không tạo tài khoản mẫu.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__DefaultConnection = 'YOUR_DefaultConnection'
.\scripts\dotnet-local.cmd run --project Backend\src\CyberManagement.API --no-launch-profile --urls http://127.0.0.1:5088
```

API mặc định migrate/seed lúc khởi động; luôn kiểm tra đúng DB thử trước khi chạy.
Development có `/swagger`; `/health` chỉ là liveness, không chứng minh truy vấn SQL.
Ở terminal khác:

```powershell
.\scripts\npm-local.cmd --prefix frontend ci
.\scripts\npm-local.cmd --prefix frontend run dev
```

Vite proxy `/api` mặc định tới `http://127.0.0.1:5088`; đổi qua `VITE_API_PROXY_TARGET` nếu dùng cổng khác.
Đăng nhập thật tại `/admin/auth`, vào `/admin/inventory`.

| Danh tính / quyền | GET kho và màn hình |
| --- | --- |
| Chưa có JWT | API 401; màn hình chuyển đăng nhập |
| Admin / Manager có stockreceipt.view | API 200; xem cả hàng hết tồn/ngừng bán |
| Cashier / Waiter / Technician theo catalog hiện tại | API 403; menu/route không cấp truy cập kho |
| Customer | API 403 |
| Employee chỉ có inventory.adjust | API 403; không suy quyền điều chỉnh thành quyền đọc |

Quyết định cuối nằm ở API. Hai role riêng trong bộ test chỉ chứng minh policy API theo permission;
không mở rộng danh sách năm role được auth/me chấp nhận hoặc tự sửa auth chung.
Khóa tài khoản không tự thu hồi JWT đã cấp theo auth hiện có; không tuyên bố SCRUM-13 đã thay policy này.

## 6. Kiểm tra và xuất schema

Không cần SQL:

```powershell
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.Inventory.Checks
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.Inventory.Checks -- --check-schema
.\scripts\npm-local.cmd --prefix frontend run test:auth
.\scripts\npm-local.cmd --prefix frontend run build
```

Kiểm tra SQL thật, sau khi đã bật LocalDB:

```powershell
$env:INVENTORY_CHECKS_SQL_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.Inventory.Checks
```

Bộ kiểm tra chỉ lấy server/auth từ đầu vào, luôn tạo tên `CyberManagement_SCRUM13_Checks_<guid>`
mới, chặn trùng tên, áp năm migration, kiểm tra 38 bảng và seed hai lần. Chạy HTTP TestServer thật
với EF SQL Server: quyền, dữ liệu/ID/giá, restart host, lỗi query, các CHECK/FK/unique của contract,
nguồn Payment trước hóa đơn, thời điểm xác nhận, usage, refund attempt và khóa phát hành.
Một DB thử GUID thứ hai kiểm tra nâng nền 4 migration: phần lẻ VND/nguồn lịch sử bị chặn và giữ dữ liệu;
đường nâng hợp lệ giữ Product đã có. Down có dữ liệu bị chặn. Cuối lượt chỉ dọn DB do lượt đó tạo.
Dữ liệu thu/hoàn trong SchemaContractChecks là fixture kiểm tra schema, không là giao dịch provider thật.

Browser với SQL thật, terminal thứ nhất:

```powershell
$env:INVENTORY_CHECKS_SQL_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.Inventory.Checks -- --serve
```

`--serve` bắt buộc có cấu hình SQL; thiếu sẽ dừng để không báo nhầm InMemory là bằng chứng SQL.
Chờ `INVENTORY_HOST_READY`. Danh sách nhân viên/customer và mật khẩu fixture ở
[TestAccounts.md](../Backend/src/CyberManagement.Infrastructure/DataTest/TestAccounts.md).
Terminal thứ hai:

```powershell
$env:VITE_API_PROXY_TARGET = 'http://127.0.0.1:5093'
.\scripts\npm-local.cmd --prefix frontend run test:inventory:sql
```

Đăng nhập, quyền, dữ liệu, bộ lọc và reload dùng API/SQL thật. Loading/empty/error được chèn phản hồi
riêng để kiểm tra UI, không coi đó là dữ liệu từ SQL. Ảnh/log nằm trong `.tools/frontend-retest/`, không commit.
Kết thúc host bằng Ctrl+C; fixture dọn DB riêng. POST `/__checks/stop` yêu cầu token Admin/role.manage
chỉ có trong project checks để dọn host tự động; không có trong API sản phẩm.

Xuất lại khi model/migration thay đổi:

```powershell
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.Inventory.Checks -- --export-schema
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.Inventory.Checks -- --check-schema
```

Exporter chặn model chưa có migration. SQL là script idempotent của năm migration; DBML mô tả
38 bảng/cột/kiểu/nullability/PK/FK/index từ EF. SQL/migration là nguồn đầy đủ cho CHECK, collation,
default và filtered index; không hiểu unique nullable trong DBML là unique không có điều kiện.
Bản SQL được sinh từ EF; kiểm thử migration dùng EF Migrate, chưa chạy riêng file SQL xuất trên SSMS.

## 7. Kết quả xác minh

Ngày 07/11/2026: backend/frontend build đạt; 52 auth checks InMemory và 52 auth checks SQL đạt;
9 auth-client checks, 62 inventory/schema/upgrade checks SQL đạt. SQL/DBML đã kiểm tra khớp model:
38 bảng, 5 migration. Render smoke đạt, có cảnh báo SSR useLayoutEffect sẵn có ở các màn client/router.

Lượt đầu dùng tên LocalDB/pipe cũ không kết nối được; đã dùng pipe Running người dùng cung cấp.
Kiểm tra SQL phát hiện whitelist ký tự voucher và cách EF so sánh khóa phát hành chưa đúng;
đã sửa, rồi chạy lại trên database mới đạt. Các database kiểm thử tự động đã được dọn.
Browser SQL đã đạt: đăng nhập Admin/Manager, chặn Cashier/Waiter/Technician/Customer,
tìm kiếm/lọc hết hàng/ngừng bán, reload giữ dữ liệu SQL và không fallback demo.
Loading/empty/error dùng phản hồi chèn riêng. Ca UI dùng năm role auth chung;
role chỉ có permission riêng được kiểm tra trực tiếp tại API, không coi auth chung đã hỗ trợ role UI mới.

Không dùng kết quả schema để khẳng định API voucher, thu/hoàn ZaloPay hoặc phát hành chứng từ đã hoàn thành.
Phạm vi SCRUM-13 đã chuẩn bị để Trưởng nhóm review; duyệt SCRUM-13 và merge PR là bước riêng.
