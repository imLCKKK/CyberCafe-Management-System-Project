# Backend CyberCafe

| Thư mục | Vai trò |
| --- | --- |
| `CyberCafe.Api/` | ASP.NET Core API, entity, DbContext và migration EF Core |
| `database/schema.dbml` | Bản thiết kế dữ liệu ban đầu để đối chiếu khi thống nhất nghiệp vụ |
| `database/InitialCreate.sql` | SQL xuất từ migration để xem trước |
| `integrations/opgg/` | Adapter Node.js lấy thống kê OP.GG và helper Riot ID dùng chung với frontend |

## Trạng thái API .NET

ASP.NET Core 10, EF Core 10 và SQL Server. Bước hiện tại chỉ dựng schema từ
`database/schema.dbml`: 19 bảng, khóa ngoại, chỉ mục, CHECK constraint,
5 loại máy mẫu, migration `InitialCreate` và OpenAPI. Chưa có endpoint nghiệp vụ,
đăng nhập hoặc tích hợp frontend; `/health` là endpoint duy nhất.

Repo chọn SDK .NET 10 ổn định qua `global.json` ở thư mục gốc. `scripts/dotnet-local.cmd` dùng SDK portable trong `.tools/dotnet` nếu có, hoặc SDK đã cài trên máy. SignalR và ZaloPay sandbox chưa được triển khai. Xem [quy trình làm nhóm](../planning/team-workflow.md) và [kế hoạch triển khai](../planning/team-delivery-plan.md).

## Chạy API không cần SQL Server

Từ thư mục gốc:

```powershell
.\scripts\dotnet-local.cmd run --project backend\CyberCafe.Api --no-launch-profile --urls http://127.0.0.1:5088
```

Mở `http://127.0.0.1:5088/health`. Nếu đặt
`ASPNETCORE_ENVIRONMENT=Development`, tài liệu OpenAPI ở `/openapi/v1.json`.
Các lệnh trên chưa kết nối hoặc tạo database.

## Khi đã cài SQL Server

Trên máy hiện tại, nếu thiếu công cụ migration trong `.tools`, cài một lần:

```powershell
.\scripts\dotnet-local.cmd tool install dotnet-ef --version 10.0.13 --tool-path .tools\dotnet-tools
```

Đặt connection string bằng biến môi trường `ConnectionStrings__CyberCafe`
hoặc user secrets của .NET. Không commit mật khẩu. Tên database dự kiến theo
thông tin hiện có là `CyperCafe`.

```powershell
$env:ConnectionStrings__CyberCafe = 'Server=TEN_SERVER;Database=CyperCafe;Trusted_Connection=True;TrustServerCertificate=True'
.\scripts\ef-local.cmd database update --project backend\CyberCafe.Api\CyberCafe.Api.csproj --startup-project backend\CyberCafe.Api\CyberCafe.Api.csproj
```

Lệnh `database update` sẽ tạo database nếu chưa có và áp migration. Chỉ chạy
sau khi kiểm tra đúng server, tên database và quyền tạo database. Nếu dùng SQL
Server Authentication, thay `Trusted_Connection=True` bằng `User Id=...;Password=...`.

SQL để duyệt trước: [`database/InitialCreate.sql`](database/InitialCreate.sql). Migration chưa được chạy trên
SQL Server thật vì máy hiện chưa cài SQL Server.

## Phạm vi của migration

Migration chặn số dư âm, giá/tiền không hợp lệ, phiên Active trùng máy hoặc
khách, yêu cầu hủy món Pending trùng, và một số giao dịch ví ghi trùng. Các
quy tắc liên bảng như ví và sổ giao dịch phải cập nhật cùng transaction,
chống xung đột `wallet_version`, tính tiền theo thời gian, trạng thái món,
và đội đúng 5 người cần được triển khai trong các bước API nghiệp vụ tiếp theo.
