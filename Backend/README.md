# Backend CyberManagement — 35 bảng

Quyết định của Trưởng nhóm ngày 02/11/2026: dùng **database SQL Server mới 35 bảng** và backend CyberManagement làm nền chung. Mở [CyberManagement.slnx](CyberManagement.slnx). `src/CyberManagement.API` là HTTP host duy nhất; adapter [OP.GG](integrations/opgg/README.md) vẫn chạy Node/Vite riêng.

| Project/thư mục | Trách nhiệm |
| --- | --- |
| `src/CyberManagement.Domain/Entities/` | 35 entity nghiệp vụ, C# thuần |
| `src/CyberManagement.Application/` | Use case, DTO, interface, validator; tham chiếu Domain |
| `src/CyberManagement.Infrastructure/Persistance/` | ApplicationDbContext, cấu hình EF và factory design-time |
| `src/CyberManagement.Infrastructure/Migrations/` | Bốn migration hiện có của schema 35 bảng |
| `src/CyberManagement.API/` | Controller, JWT/authorization, DI, `/health` |
| `tests/CyberManagement.API.Checks/` | Auth/seed regression, chế độ InMemory hoặc SQL thử riêng |

Owner triển khai module xuyên các tầng và UI. Application/Domain không dùng DbContext/HttpContext; interface được implementation ở tầng ngoài. [Hợp đồng identity](docs/001-monorepo.md) quy định EmployeeId/CustomerId kiểu int, năm role nhân viên và customer principal. Frontend còn demo; nối login và nghiệp vụ khác theo [55 task](../codex/SPRINT_TASKS.md).



## Build từ repository root

```powershell
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx
```

Cần .NET 10 SDK; `global.json` chọn bản ổn định. Wrapper tìm SDK portable ở `.tools/` nếu có rồi dùng bản đã cài. Tên thư mục chuẩn là `Backend` (B hoa). `bin/obj`, `.tools/` và override development là dữ liệu cục bộ, không đưa lên Git.

## Cấu hình DB mới và secret

`ConnectionStrings:DefaultConnection` trong cấu hình chung được để trống. API yêu cầu cấp connection string và JWT signing key bằng User Secrets hoặc biến môi trường. `appsettings.Development.json` nếu dùng chỉ là file local được ignore; không đặt signing key dùng chung trong repo.

Ví dụ với SQL Server cục bộ, dùng **tên DB mới của bạn**, từ repository root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__DefaultConnection = 'Server=localhost;Database=CyberManagement_Dev;Trusted_Connection=True;TrustServerCertificate=True'
```

Đặt key riêng đủ ít nhất 32 byte bằng .NET User Secrets:

```powershell
.\scripts\dotnet-local.cmd user-secrets set 'Authentication:Jwt:SigningKey' '<your-random-secret-at-least-32-bytes>' --project Backend\src\CyberManagement.API
```

Cũng có thể cấp `Authentication__Jwt__SigningKey` từ môi trường/secret manager. Không dùng lại key từng được commit. Seed tài khoản Development yêu cầu cấu hình đủ cả năm `SeedAccounts:<Role>:Password` (ít nhất 12 ký tự mỗi mật khẩu) hoặc không cấu hình account seed nào; xem [các lệnh seed](docs/001-monorepo.md). Role/permission vẫn được đồng bộ khi seed mà không tạo account mẫu.

## Chạy API

```powershell
.\scripts\dotnet-local.cmd run --project Backend\src\CyberManagement.API --no-launch-profile --urls http://127.0.0.1:5088
```

Mặc định `Database:InitializeOnStartup=true`: API áp migration và seed vào DB đã cấu hình khi khởi động. Kiểm tra đúng server/tên DB mới trước khi chạy. `/health` là liveness, không chứng minh kết nối SQL; Development có Swagger tại `/swagger` và OpenAPI `/openapi/v1.json`.

Để kiểm tra host mà không migrate/seed, đặt `Database__InitializeOnStartup=false`; vẫn cần connection string hợp lệ về cú pháp và signing key. Trạng thái flag này không tự chứng minh DB đã sẵn sàng.

## EF từ repository root

Cài CLI đúng phiên bản package của backend nếu máy chưa có:

```powershell
.\scripts\dotnet-local.cmd tool install dotnet-ef --version 10.0.14 --tool-path .tools\dotnet-tools
.\scripts\ef-local.cmd migrations list --no-connect --project Backend\src\CyberManagement.Infrastructure --startup-project Backend\src\CyberManagement.API
.\scripts\ef-local.cmd migrations has-pending-model-changes --project Backend\src\CyberManagement.Infrastructure --startup-project Backend\src\CyberManagement.API
```

Factory design-time đọc **`ConnectionStrings__DefaultConnection`**. Khi không có biến này, nó dùng fallback cục bộ cổng 1 để xây model/sinh SQL offline; không đọc key JWT hay phụ thuộc thư mục đang chạy. Lệnh cần truy cập SQL phải cấp biến môi trường connection string đúng DB mới, kể cả khi API dùng User Secrets.

```powershell
.\scripts\ef-local.cmd database update --project Backend\src\CyberManagement.Infrastructure --startup-project Backend\src\CyberManagement.API
```

Đây là chuỗi migration cho **DB mới 35 bảng**, không phải đường nâng cấp DB CyberCafe 19 bảng. Giữ DB cũ riêng nếu có; không reset/xóa/chuyển dữ liệu cũ trong đợt sửa này. Khi Task sau thay schema, owner phối hợp Trường rồi thêm migration mới vào Infrastructure; không sửa lịch sử đã áp. Bản SQL/DBML bổ sung phải được xuất/đối chiếu từ schema này.

## Kiểm tra auth và seeder

Mặc định không cần SQL Server:

```powershell
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks
```

Chế độ SQL dùng connection string để lấy server/auth; chương trình luôn thay database bằng tên `CyberManagement_AuthChecks_<guid>` mới, từ chối dùng DB trùng, áp migrations, chạy cùng bộ kiểm tra và dọn đúng DB thử cả khi test thất bại. Tài khoản SQL cần quyền tạo/xóa DB thử:

```powershell
$env:AUTH_CHECKS_SQL_CONNECTION = 'Server=localhost;Integrated Security=True;TrustServerCertificate=True'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks
Remove-Item Env:AUTH_CHECKS_SQL_CONNECTION
```

Test gồm login đúng/sai/khóa, quyền nhân viên/khách, JWT sai chữ ký/issuer/audience/hết hạn, seed lặp và giữ permission của role ngoài catalog. Key/password trong project checks chỉ phục vụ TestServer; không phải cấu hình để chạy API. Logout stateless giữ token hợp lệ tới lúc hết hạn theo contract đã có.

Kết quả review/bàn giao mới gửi trong chat; chỉ tạo file báo cáo khi được yêu cầu. [Quy trình nhóm](../planning/team-workflow.md) quy định Trưởng nhóm review rồi mới merge `dev`.
