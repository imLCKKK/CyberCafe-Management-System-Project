# SCRUM-13 - Tích hợp migration, seed và luồng đọc kho

Cập nhật 04/11/2026. Nhánh `SCRUM-13-Integrate-Migrations-Seed-Data`.
Nền backend: `dev` tại `a975f8d`, CyberManagement 35 bảng, SQL Server, ID int.
Phần đăng nhập frontend tích hợp từ SCRUM-3 của Đại tại `491eed0`.
[Contract SCRUM-12](contracts/inventory-invoice-voucher.md) là đầu vào thiết kế;
các giao điểm còn chờ tại mục 9 của contract vẫn phải được owner xác nhận.

## 1. Phạm vi đã tích hợp

- Dùng host duy nhất `Backend/src/CyberManagement.API`, `ApplicationDbContext` và migrations Infrastructure.
- Giữ bốn migration đã có; bỏ chuỗi 19 bảng khỏi nhánh triển khai. Không sửa lịch sử migration của nền 35 bảng.
- Products đã có Stock/IsActive: đọc trực tiếp, không tạo StockQuantity/is_active trùng cột.
- GET `/api/admin/inventory` dùng policy `permission:stockreceipt.view` của auth chung.
- DTO: `{id,name,stockQuantity,isActive,currentPrice,categoryId}`; thứ tự ProductId tăng dần,
  gồm cả tồn 0 và sản phẩm ngừng bán, danh sách rỗng trả `[]`.
- Màn hình `/admin/inventory` dùng API/JWT chung, tìm/lọc và tải lại; không lấy tồn từ demo/localStorage.
  Khi lỗi hoặc chưa có quyền, không hiển thị lại sản phẩm demo.
- Seed đọc kho ở Infrastructure/DataTest; SQL/DBML xuất từ model/migration đã tích hợp.

Đợt này không thêm nghiệp vụ nhập/điều chỉnh/lịch sử kho, voucher hoặc phát hành hóa đơn.
Các thay đổi snapshot, precision, PromotionUsage và nguồn thu trước hóa đơn trong SCRUM-12
chưa phải schema đã triển khai. Không thay entity/config thuộc owner khác khi chưa chốt giao điểm.
Giá hiện tại vẫn theo precision của migration nền; không ghi đã nâng toàn hệ sang decimal(18,0).

## 2. Thư mục và thứ tự migration

| Nội dung | Đường dẫn |
| --- | --- |
| Host/auth | [API](../Backend/src/CyberManagement.API/) |
| DTO và interface đọc | [InventoryItemDto](../Backend/src/CyberManagement.Application/DTOs/Inventory/InventoryItemDto.cs), [IInventoryQueries](../Backend/src/CyberManagement.Application/Interfaces/Repositories/IInventoryQueries.cs) |
| Truy vấn EF | [InventoryQueries](../Backend/src/CyberManagement.Infrastructure/Repositories/InventoryQueries.cs) |
| Controller | [InventoryController](../Backend/src/CyberManagement.API/Controller/InventoryController.cs) |
| Migration chính | [Infrastructure/Migrations](../Backend/src/CyberManagement.Infrastructure/Migrations/) |
| Seed test | [InventoryReadSeed.sql](../Backend/src/CyberManagement.Infrastructure/DataTest/InventoryReadSeed.sql) |
| SQL/DBML | [CyberManagement.sql](../Backend/database/CyberManagement.sql), [schema.dbml](../Backend/database/schema.dbml) |
| Kiểm tra API/SQL và xuất schema | [Inventory.Checks](../Backend/tests/CyberManagement.Inventory.Checks/) |
| Màn hình | [InventoryPage.jsx](../frontend/src/admin/inventory/InventoryPage.jsx) |

Thứ tự áp lên DB mới:

1. `20261102065247_InitialCreate` — nền CyberManagement, không phải InitialCreate 19 bảng.
2. `20261102073816_CustomerPasswordAuthentication`.
3. `20261102083200_RemoveCustomerLoginLockout`.
4. `20261102084459_RemoveEmployeeLoginLockout`.

Owner phải bàn giao commit/PR, migration và dữ liệu seed tương ứng trước khi nối thêm.
Trường điều phối thứ tự; từng owner chịu trách nhiệm entity/config của mình.
Chưa tích hợp migration mới cho ví/đơn/phiên/voucher khi chưa nhận và đối chiếu đầu vào.

## 3. Dựng database thử bằng Visual Studio / EF

Cần .NET 10, EF CLI tương thích package (10.0.14), SQL Server LocalDB và Node.js 22+.
Người thực hiện tự bật `(localdb)\MSSQLLocalDB`; các lệnh dưới không tự bật SQL/Docker.

Dùng **DB mới** `CyberManagement_SCRUM13_Test` hoặc tên bắt đầu `CyberManagement_SCRUM13_`.
Giữ nguyên DB cũ/DB chung. Không áp migration 35 bảng lên `CyberCafe_SCRUM13_Test` 19 bảng.
Không drop/reset DB đang tồn tại để tạo bằng chứng fresh DB.

Trong PowerShell tại repository root, đặt chuỗi kết nối thật thay placeholder:

```powershell
$env:ConnectionStrings__DefaultConnection = 'YOUR_DefaultConnection'
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx
.\scripts\ef-local.cmd database update --project Backend\src\CyberManagement.Infrastructure --startup-project Backend\src\CyberManagement.API --context ApplicationDbContext
```

`YOUR_DefaultConnection` phải trỏ đúng LocalDB và database mới ở trên, Windows authentication.
Factory đọc key `ConnectionStrings__DefaultConnection`; không dùng key CyberCafe của bản cũ.
Nếu database đã tồn tại thì lệnh nâng cấp không chứng minh một lượt dựng mới từ đầu.

Trong NuGet Package Manager Console, cùng connection string đã cấu hình:

```powershell
$env:ConnectionStrings__DefaultConnection = 'YOUR_DefaultConnection'
Update-Database -Project CyberManagement.Infrastructure -StartupProject CyberManagement.API -Context ApplicationDbContext
```

Mở SSMS/SQL Server Object Explorer vào **đúng DB thử**, chạy toàn bộ
[InventoryReadSeed.sql](../Backend/src/CyberManagement.Infrastructure/DataTest/InventoryReadSeed.sql).
Script chặn database ngoài tiền tố SCRUM13, kiểm tra migration, dùng transaction và kiểm tra fixture cũ.
Chạy lại không thêm bản sao, không tự ghi đè tồn/giá/trạng thái đã thay đổi; dữ liệu khác phải đối soát.

Seed tạo một Category đang hoạt động và ba Product:

| Tên | Stock | IsActive | Price |
| --- | ---: | --- | ---: |
| TEST_Nước suối | 20 | true | 10000 |
| TEST_Mì ly hết hàng | 0 | true | 20000 |
| TEST_Coca ngừng bán | 5 | false | 15000 |

Đây là fixture đọc kho, không chứng minh nghiệp vụ nhập/trừ/hoàn. Không có hóa đơn hoặc ledger giả.

```sql
SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT p.ProductId, p.ProductName, p.Stock, p.IsActive, p.Price, p.CategoryId
FROM dbo.Products p
JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
WHERE c.CategoryName = N'SCRUM13_TEST_INVENTORY'
ORDER BY p.ProductId;
```

## 4. Chạy API và màn hình

Cấu hình JWT signing key và năm mật khẩu seed Development theo
[Backend README](../Backend/README.md) và [auth chung](../Backend/docs/001-monorepo.md).
Không đặt secret vào Git hoặc frontend. Mật khẩu seed tối thiểu 12 ký tự và cấp đủ năm role
hoặc không tạo account mẫu. Seed roles/permissions dùng lại implementation của Đại.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__DefaultConnection = 'YOUR_DefaultConnection'
.\scripts\dotnet-local.cmd run --project Backend\src\CyberManagement.API --no-launch-profile --urls http://127.0.0.1:5088
```

API mặc định migrate/seed lúc khởi động; kiểm tra đúng DB thử trước khi chạy.
Development có `/swagger`. GET `/health` chỉ là liveness, không thay test truy vấn SQL.

Ở terminal khác:

```powershell
.\scripts\npm-local.cmd --prefix frontend ci
.\scripts\npm-local.cmd --prefix frontend run dev
```

Vite proxy `/api` mặc định tới `http://127.0.0.1:5088`; cấu hình cổng khác qua
`VITE_API_PROXY_TARGET` trong môi trường chạy Vite hoặc `.env.local` của frontend.
Đăng nhập `/admin/auth` bằng account thật được seed, vào `/admin/inventory`.
Màn hình chỉ đọc dữ liệu: thao tác ghi thuộc task sau, không giữ nút ghi vào demo trên màn hình SQL.

| Danh tính/quyền | Kết quả GET kho |
| --- | --- |
| Chưa có JWT | 401 |
| Admin / Manager có stockreceipt.view | 200 |
| Cashier / Waiter / Technician theo catalog hiện tại | 403 |
| Customer | 403 |
| Employee chỉ có inventory.adjust, không có stockreceipt.view | Không được xem tồn theo contract |

Menu/router dùng đúng stockreceipt.view; quyết định cuối vẫn ở API.
Token hiện có không bị thu hồi ngay khi khóa tài khoản; giữ giới hạn auth chung và chờ Đại chốt,
không tuyên bố module này đã triển khai kiểm tra khóa tức thời.

## 5. Kiểm tra có thể chạy lại

Không cần SQL:

```powershell
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.Inventory.Checks
.\scripts\npm-local.cmd --prefix frontend run test:auth
.\scripts\npm-local.cmd --prefix frontend run build
```

Chứng minh migration/seed và API bằng SQL thật, sau khi người dùng bật LocalDB:

```powershell
$env:INVENTORY_CHECKS_SQL_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.Inventory.Checks
```

Bộ kiểm tra chỉ lấy server/auth từ chuỗi đầu vào, luôn tạo tên
`CyberManagement_SCRUM13_Checks_<guid>` mới, chặn trùng tên, áp bốn migration,
kiểm tra 35 bảng, seed hai lần, đăng nhập thật và thử quyền, dữ liệu/ID/giá,
restart host và query failure. Cuối lượt dọn đúng DB do lượt đó tạo; không reset DB người dùng.
SQL test dùng mật khẩu/key riêng của fixture, không lấy mật khẩu thật của nhóm.

Kiểm tra browser với SQL thật dùng host fixture riêng ở cổng 5093. Terminal thứ nhất:

```powershell
$env:INVENTORY_CHECKS_SQL_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.Inventory.Checks -- --serve
```

Chờ `INVENTORY_HOST_READY`. Terminal thứ hai:

```powershell
$env:VITE_API_PROXY_TARGET = 'http://127.0.0.1:5093'
.\scripts\npm-local.cmd --prefix frontend run test:inventory:sql
```

Bộ chạy mở Chrome/Edge headless. Phần đăng nhập, quyền, dữ liệu và reload dùng API/SQL thật;
loading/empty/error dùng phản hồi chèn riêng để kiểm tra trạng thái UI, không coi là bằng chứng SQL.
Ảnh/log ở `.tools/frontend-retest/`, không đưa vào Git.
Kết thúc bằng Ctrl+C ở host fixture; host dọn DB riêng của nó. Host kiểm thử cũng có
POST `/__checks/stop` yêu cầu token Admin/role.manage; endpoint này chỉ trong project checks,
không có trong API sản phẩm.

## 6. SQL/DBML và phần còn chờ

Xuất artifact hoặc kiểm tra chúng còn khớp EF, không mở kết nối SQL:

```powershell
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.Inventory.Checks -- --export-schema
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.Inventory.Checks -- --check-schema
```

Exporter chặn model chưa có migration. SQL là script idempotent của đúng lịch sử bốn migration;
DBML ghi tên bảng/cột/kiểu/nullability/PK/FK/index từ relational model, không dùng schema 19 bảng.
SQL/migrations là nguồn đầy đủ cho CHECK/default/filtered index; DBML là sơ đồ đối chiếu.
Khi owner bổ sung migration mới, đối chiếu thứ tự và seed rồi xuất lại cả hai artifact.

Đã chạy trong lượt cập nhật 04/11/2026: build backend/frontend; 52 auth checks InMemory;
22 inventory checks trên DB SQL mới; 9 auth-client checks; render smoke và browser kho dùng SQL thật.
Browser kiểm tra đăng nhập Admin/Manager, từ chối Cashier/Waiter/Technician/Customer,
reload, tìm kiếm, không fallback demo, trạng thái loading/empty/error.
Lượt SQL đầu phát hiện seed thiếu Categories.IsActive; đã sửa và chạy lại trên DB mới đạt.

Tiêu chí luồng đọc kho/API/UI/SQL đã có kiểm chứng. Tiêu chí tích hợp đầy đủ migration của các owner
vẫn chờ commit/PR và các giao điểm SCRUM-12: nguồn ví/payment, đơn/phiên, snapshot/precision và giữ/hoàn lượt.
Chưa có quyết định duyệt của Trưởng nhóm; không ghi toàn bộ SCRUM-13 Done chỉ từ luồng đọc này.
