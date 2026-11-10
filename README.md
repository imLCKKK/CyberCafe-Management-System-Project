# CyberCafe Management System

Gói bàn giao **SCRUM-6**: xem [README-SCRUM-6.md](README-SCRUM-6.md) để biết phạm vi, kiểm tra và cách commit/push.

Đồ án .NET quản lý phòng máy: máy/phiên, khách hàng, nhân viên, ví, món/đơn, kho, hóa đơn/báo cáo, giải đấu và khuyến mãi. Nhận diện NEXORA CYBER; React/Vite + ASP.NET Core .NET 10 + EF Core + SQL Server.

## Nền được chọn

Ngày 02/11/2026, Trưởng nhóm chọn **backend CyberManagement, database SQL Server mới 35 bảng**. Solution [Backend/CyberManagement.slnx](Backend/CyberManagement.slnx) gồm Domain, Application, Infrastructure, API và project kiểm tra auth. Chủ module làm xuyên tầng; API là host chung.

Backend đã có auth nhân viên/khách, JWT/permission, seed và health. Frontend hiện vẫn chủ yếu demo/localStorage; nối UI và các nghiệp vụ còn triển khai theo [55 task hiện hữu](codex/SPRINT_TASKS.md). Giữ tên/owner/sprint; chưa tự nghiệm thu Task hoặc đưa thay đổi lên `dev`.

DB mới tạo từ migrations CyberManagement; không áp InitialCreate mới lên DB 19 bảng cũ. OP.GG vẫn dùng adapter Node tại `Backend/integrations/opgg/`. SignalR hai máy, ZaloPay sandbox và ML theo kế hoạch; chưa được coi hoàn thành từ việc có schema.

## Chạy

Từ repository root với Node.js 22+ và .NET 10 SDK:

```powershell
.\scripts\npm-local.cmd --prefix frontend ci
.\scripts\npm-local.cmd --prefix frontend run dev
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx
```

Frontend ở http://127.0.0.1:5173. Cấu hình DB mới/signing key/seed và chạy API theo [Backend README](Backend/README.md). Giữ đúng chữ B hoa trong `Backend` để đường dẫn/import hoạt động cả Linux.

## Tài liệu cho nhóm

- [Kiến trúc và luồng dữ liệu](docs/architecture.md).
- [Định danh và ma trận quyền](Backend/docs/001-monorepo.md).
- [Hướng dẫn AI](codex/PROJECT_GUIDE.md), [hướng dẫn review](codex/REVIEW_GUIDE.md).
- [Kế hoạch 4 sprint](planning/team-delivery-plan.md), [55 Task](codex/SPRINT_TASKS.md), [quy trình nhóm](planning/team-workflow.md).
- [Frontend demo](frontend/ADMIN_DEMO.md), [adapter OP.GG](Backend/integrations/opgg/README.md), [scripts](scripts/README.md), [mục lục tài liệu](docs/README.md).

Kết quả sửa/review gửi trực tiếp trong chat; chỉ tạo file báo cáo khi được yêu cầu. Trưởng nhóm duyệt trước khi merge `dev`.

## Thành viên

* Thành viên 1: Lâm Chí Khang
* Thành viên 2: Hà Đức Phúc
* Thành viên 3: Nguyễn Quốc Minh
* Thành viên 4: Hoàng Ngọc Đại
* Thành viên 5: Lê Vũ Huy Trường
