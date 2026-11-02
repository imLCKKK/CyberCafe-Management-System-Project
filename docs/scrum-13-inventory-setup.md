# SCRUM-13 - Dựng DB thử và chuẩn bị luồng đọc kho

## Phạm vi hiện tại

Branch: SCRUM-13-Integrate-Migrations-Seed-Data.
Backend: backend/CyberCafe.Api, DbContext: CyberCafeDbContext.

Đã chuẩn bị schema Product, migration, seed test đọc, DTO, InventoryQueries,
InventoryEndpoints và đăng ký DI cho truy vấn. Endpoint CHƯA được map trong Program.cs
vì backend chung chưa có auth/policy hoạt động từ Đại.
Đây là bàn giao một phần SCRUM-13, chưa phải task hoàn thành hoặc đã được Trưởng nhóm duyệt.
Chưa tích hợp migration của các owner khác, chưa nối màn hình hoặc kiểm tra reload qua API.

Contract tham chiếu ở branch SCRUM-12-Define-Inventory-Invoice-Voucher-Contracts:
docs/contracts/inventory-invoice-voucher.md (không merge nguyên code Truong).
Phần Product bổ sung stock_quantity/is_active phải đối chiếu Khang trước khi tích hợp;
không sinh thêm bộ trường hoặc migration trùng.

## File liên quan

- [Entity](../backend/CyberCafe.Api/Data/Entities.cs).
- [DbContext](../backend/CyberCafe.Api/Data/CyberCafeDbContext.cs).
- [Migrations](../backend/CyberCafe.Api/Data/Migrations/).
- [Seed đọc kho](../backend/CyberCafe.Api/DataTest/InventoryReadSeed.sql).
- [DTO](../backend/CyberCafe.Api/Features/Inventory/InventoryItemResponse.cs).
- [Truy vấn](../backend/CyberCafe.Api/Features/Inventory/InventoryQueries.cs).
- [Endpoint chưa bật](../backend/CyberCafe.Api/Features/Inventory/InventoryEndpoints.cs).
- [DBML](../backend/database/schema.dbml).
- [SQL xuất đến migration kho](../backend/database/SCRUM13_InventorySchema.sql).

## 1. Công cụ và database

Cần .NET 10 SDK, EF CLI tương thích project, SQL Server LocalDB và SSMS.
Wrapper trong scripts ưu tiên công cụ portable nếu có, rồi dùng công cụ đã cài.

Người thực hiện tự bật LocalDB nếu đang dừng.
DB kiểm thử là CyberCafe_SCRUM13_Test trên (localdb)\MSSQLLocalDB.
Không dùng DB CyberManagement đã test trên Truong, DB dùng chung hoặc database đang có dữ liệu thật.
Nếu DB thử đã tồn tại, database update chỉ kiểm tra/nâng cấp, không chứng minh lượt dựng mới từ đầu.
Không drop/reset tự động; muốn chứng minh fresh DB phải kiểm tra đích chưa tồn tại trước lượt chạy.

Trong PowerShell tại root repository, cùng một cửa sổ terminal:

~~~powershell
cd D:\CyberManagementBackEnd
$env:ConnectionStrings__CyberCafe = 'YOUR_DefaultConnection'
.\scripts\dotnet-local.cmd build backend\CyberCafe.Api\CyberCafe.Api.csproj
.\scripts\ef-local.cmd database update --project backend\CyberCafe.Api\CyberCafe.Api.csproj --startup-project backend\CyberCafe.Api\CyberCafe.Api.csproj --context CyberCafeDbContext
~~~

Biến môi trường chỉ có hiệu lực trong terminal đó và process con.
Mở API từ Visual Studio/terminal khác cần cấu hình cùng key CyberCafe tương ứng ở môi trường đó.
Không nhầm với key DefaultConnection của CyberManagement.
Factory design-time có fallback không dành để kết nối local thực; luôn đặt biến trước database update.
Không đưa mật khẩu/secret vào Git.

Thứ tự migration:
1. 20261020192706_InitialCreate.
2. 20261102061018_AddInventoryProductFields.

Migration mới chỉ thêm is_active (bit, mặc định true), stock_quantity (int, mặc định 0)
và CK_products_stock >= 0. Không sửa InitialCreate đã có.
SQL xuất bao gồm cả hai migration, dùng để review; KHÔNG chạy lại trên DB đã áp migration.
Nguồn chính để dựng DB là EF migrations.

## 2. Chạy seed và kiểm tra SQL

Mở SSMS kết nối đúng LocalDB, mở và chạy toàn bộ InventoryReadSeed.sql.
Script có USE CyberCafe_SCRUM13_Test, kiểm tra migration, transaction và chặn seed trùng.
Không xóa/ghi đè dữ liệu cũ; không dùng ID cố định hoặc IDENTITY_INSERT.

Seed tạo một danh mục SCRUM13_TEST_INVENTORY và ba sản phẩm:
- TEST_Nước suối: tồn 20, đang bán, giá 10000.
- TEST_Mì ly hết hàng: tồn 0, đang bán, giá 20000.
- TEST_Coca ngừng bán: tồn 5, ngừng bán, giá 15000.

Nếu chạy lần hai: mong đợi báo seed đã tồn tại, không thêm bản sao.
Tồn seed là fixture trực tiếp cho API đọc, không phải chứng cứ nhập/trừ/hoàn kho.
Không có ledger, đơn hoặc lịch sử biến động trong seed này.

Truy vấn đọc để kiểm tra:

~~~sql
USE [CyberCafe_SCRUM13_Test];

SELECT MigrationId
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;

SELECT p.id, p.name, p.stock_quantity, p.is_active,
       p.current_price, p.category_id
FROM dbo.products p
JOIN dbo.product_categories c ON c.id = p.category_id
WHERE c.name = N'SCRUM13_TEST_INVENTORY'
ORDER BY p.id;
~~~

## 3. Chạy backend ở trạng thái hiện tại

Trong cùng terminal đã đặt connection string:

~~~powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
.\scripts\dotnet-local.cmd run --project backend\CyberCafe.Api\CyberCafe.Api.csproj --no-launch-profile --urls http://127.0.0.1:5088
~~~

GET /health trả trạng thái process; không chứng minh truy vấn SQL thành công.
Development có /openapi/v1.json; chưa thêm Swagger UI.

GET /api/admin/inventory HIỆN CHƯA ĐƯỢC ĐĂNG KÝ, nên có thể trả 404 và chưa có trong OpenAPI.
Không ghi 404 này là kiểm thử quyền đạt. Không bỏ RequireAuthorization hoặc thêm danh tính giả
để biến route thành đã tích hợp auth thật.

## 4. Nối endpoint khi nhận auth của Đại

Đầu vào: scheme xác thực, đăng nhập, seed tài khoản, cách đọc role/account/active và policy chung.
Đại cung cấp auth; Trường nối endpoint của mình, không viết lại hệ thống đăng nhập.
RequireAuthenticatedUser + RequireRole trong InventoryEndpoints mới biểu diễn yêu cầu đăng nhập/role,
chưa thay thế kiểm tra khóa tài khoản hoặc bắt buộc đổi mật khẩu/CSRF theo policy chung.

Sau khi cấu hình authentication/authorization và middleware đúng theo contract của Đại,
đăng ký InventoryQueries như hiện tại và gọi app.MapInventoryEndpoints() trước app.Run().
Bổ sung policy tài khoản hoạt động theo auth chung trước nghiệm thu.
Không chỉ thêm MapInventoryEndpoints() rồi coi đã đủ xác thực.

Test API phải có:
- Chưa đăng nhập: 401; Customer: 403; Admin/Staff hoạt động: 200.
- Tài khoản khóa bị từ chối theo policy chung.
- Đủ ba sản phẩm seed, kể cả tồn 0 và ngừng bán.
- JSON: id, name, stockQuantity, isActive, currentPrice, categoryId.
- Thứ tự id tăng dần; số tồn/giá/trạng thái khớp SQL.
- API/DB lỗi không trả fixture hoặc localStorage thay thế.
- Gọi lại và restart API vẫn đọc đúng dữ liệu đã lưu.

Sau backend, nối một màn hình đọc kho vào API và kiểm tra reload/loading/error/empty/quyền.
Đây là tiêu chí còn lại của SCRUM-13, không tự loại khỏi task khi làm backend trước.

## 5. Bằng chứng và việc còn lại

| Mục | Bằng chứng hiện có / giới hạn |
| --- | --- |
| Build | Trường báo build thành công; lượt viết hướng dẫn không chạy lại build |
| Migration | Trường báo database update Done trên DB thử được chỉ định; không trực tiếp kiểm tra SQL ở lượt này |
| Seed | Ảnh SSMS do Trường gửi có đúng 3 dòng và các giá trị tồn/giá/trạng thái |
| DBML/SQL | Đã đọc DBML products và SQL xuất, khớp hai cột và check constraint mới |
| HTTP/auth | Chưa test: endpoint chưa map, auth chung chưa tích hợp |
| UI/reload | Chưa nối theo thứ tự làm backend trước của Trường |
| Migration owner khác | Chưa tích hợp; cần nhận commit/PR và đối chiếu schema chung |
| SCRUM-12 | Có contract trên branch riêng; phản hồi giao điểm và duyệt được theo dõi riêng |
| Duyệt task | Chưa có quyết định của Trưởng nhóm, không ghi Done |

Trước PR: đối chiếu Product với Khang, auth với Đại, schema/seed các owner,
kiểm tra lại migration/seed trên DB thử mới, chạy HTTP/UI theo tiêu chí,
cập nhật DBML/SQL nếu tích hợp thêm và ghi bằng chứng thật.
Chưa nhận phụ thuộc thì có thể bàn giao phần độc lập, ghi rõ các mục chưa đạt.
