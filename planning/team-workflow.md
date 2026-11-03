# Quy trình làm nhóm

Nền được chọn ngày 02/11/2026: React/Vite + backend CyberManagement Clean Architecture, .NET 10/EF Core/SQL Server mới 35 bảng. Đọc [PROJECT_GUIDE](../codex/PROJECT_GUIDE.md), [backlog 55 task](../codex/SPRINT_TASKS.md), [kế hoạch](team-delivery-plan.md) và [hướng dẫn backend](../Backend/README.md).

## Chạy từ repository root

```powershell
.\scripts\npm-local.cmd --prefix frontend ci
.\scripts\npm-local.cmd --prefix frontend run dev
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx
```

Trước khi chạy API, cấu hình DB mới, signing key và seed bằng User Secrets/biến môi trường theo hướng dẫn backend:

```powershell
.\scripts\dotnet-local.cmd run --project Backend\src\CyberManagement.API --no-launch-profile --urls http://127.0.0.1:5088
```

`scripts/start.cmd` chỉ chạy frontend và middleware OP.GG, không tạo SQL/API/LAN. Tên đường dẫn chuẩn là `Backend` (B hoa); giữ đúng cả trên Windows và Linux. Frontend vẫn demo, nối login là Task riêng của Đại.

## Backend và migration chung

Application tham chiếu Domain; Infrastructure tham chiếu Application/Domain; API ghép DI. Mỗi người phụ trách module xuyên tầng và UI. Entity ở Domain; DbContext/config ở `Infrastructure/Persistance`, migrations ở `Infrastructure/Migrations`. Interface do Application định nghĩa khi có nhu cầu; không tạo abstraction/thư mục rỗng.

```powershell
.\scripts\ef-local.cmd migrations list --no-connect --project Backend\src\CyberManagement.Infrastructure --startup-project Backend\src\CyberManagement.API
.\scripts\ef-local.cmd migrations has-pending-model-changes --project Backend\src\CyberManagement.Infrastructure --startup-project Backend\src\CyberManagement.API
```

EF design-time đọc `ConnectionStrings__DefaultConnection`; xem backend README trước lệnh cần kết nối DB. Dùng DB mới 35 bảng; không tự áp migration mới lên DB 19 bảng hoặc reset DB chung. Trường điều phối thứ tự, owner viết entity/config/migration của module. Không sửa lịch sử đã áp; DBML/SQL mô tả cập nhật theo migration.

## Nhận task và đưa thay đổi vào dev

1. Giữ Jira key, tên, owner, sprint và checklist của 55 Task hiện hữu. Ghi link contract/commit thực tế; các bước nhỏ vẫn là checklist, không tự thêm Task/Sub-task.
2. Kiểm tra branch, status, diff; giữ thay đổi chưa commit. Sau khi Trưởng nhóm tích hợp nền mới lên `dev`, cập nhật `dev` rồi tạo nhánh từ Jira theo flow của nhóm. Nhánh đang làm cần tích hợp nền có kiểm soát.
3. Chốt endpoint/DTO/ID, trạng thái, lỗi, event và schema với owner liên quan trước khi khác nhau. Staff dùng EmployeeId; khách dùng CustomerId, kiểu int.
4. Mỗi PR migration/router/auth/API client chung được tích hợp theo thứ tự. Chủ module phối hợp Đại/Trường; Trưởng nhóm là người duyệt cuối.
5. Gửi PR nhỏ và kết quả kiểm tra trong chat, nêu giới hạn. Tác giả chờ review trước khi merge `dev`; AI không tự gửi nhận xét, đổi Jira, commit/push/merge.
6. Task đủ checklist và được Trưởng nhóm duyệt mới Done; owner kiểm tra chéo không thay quyền duyệt. Khi re-review, tập trung diff mới và finding cũ.

## Kiểm tra theo phần sửa

```powershell
.\scripts\npm-local.cmd --prefix frontend run test:frontend:models
.\scripts\npm-local.cmd --prefix frontend run test:frontend:render
.\scripts\npm-local.cmd --prefix frontend run build
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks
```

Auth checks mặc định dùng InMemory; chế độ SQL tạo/xóa DB thử riêng theo backend README. Browser suite, SQL nghiệp vụ, SignalR hai máy, ZaloPay và ML cần bằng chứng phù hợp từng Task; build/demo/fixture không thay kiểm thử hệ thống thật. Không đưa bin/obj, logs hoặc secret vào Git.
