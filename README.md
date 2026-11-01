# CyberCafe-Management-System-Project
Đồ án môn Công Nghệ .NET
### Hệ thống quản lý phòng máy thương mại

## Giới thiệu

Đồ án xây dựng hệ thống quản lý phòng máy thương mại, hỗ trợ quản lý máy tính, khách hàng, nhân viên, dịch vụ và hóa đơn.

## Chức năng

* Quản lý máy tính
* Quản lý khách hàng
* Quản lý nhân viên
* Quản lý dịch vụ
* Quản lý phiên sử dụng máy
* Tính tiền sử dụng máy
* Quản lý hóa đơn
* Thống kê doanh thu
* Đăng nhập và phân quyền

## Công nghệ sử dụng

* C#
* ASP.NET Core Web API
* Entity Framework Core
* SQL Server
* React / Vite / JavaScript / CSS
* SignalR cho realtime và ZaloPay sandbox theo kế hoạch triển khai

## Database

Sử dụng SQL Server.

## Bắt đầu đọc hệ thống

- [Hướng dẫn cho AI trên máy thành viên](codex/PROJECT_GUIDE.md): flow dự án, ranh giới code, cách nhận Task và bàn giao.
- [Hướng dẫn review cho Trưởng nhóm](codex/REVIEW_GUIDE.md): checklist và mẫu báo cáo; Trưởng nhóm là người duyệt cuối.
- [Bản đồ kiến trúc và luồng dữ liệu](docs/architecture.md): app nào đang chạy, dữ liệu nằm ở đâu, phần nào chưa tích hợp.
- [Kế hoạch 4 sprint và phân việc 5 người](planning/team-delivery-plan.md): 31/10–19/11/2026, dự phòng 20–21/11.
- [55 Task để đưa lên Jira](codex/SPRINT_TASKS.md): 53 Task đã gom và 2 Task UI giao Phúc theo yêu cầu mới; mỗi thẻ có tên tiếng Anh nối bằng dấu gạch ngang, Description, Dependencies và Done khi.
- [Quy trình làm nhóm](planning/team-workflow.md): chạy dự án, nhánh, PR, review và migration.
- [Mục lục tài liệu](docs/README.md).

| Thư mục | Vai trò |
| --- | --- |
| [`frontend/`](frontend/README.md) | React/Vite: source, package/config, assets và browser test |
| [`backend/`](backend/README.md) | API .NET, migrations, schema/SQL và adapter OP.GG |
| [`docs/`](docs/README.md) | Kiến trúc, đặc tả và báo cáo |
| [`planning/`](planning/team-delivery-plan.md) | Kế hoạch sprint và quy trình phối hợp |
| [`scripts/`](scripts/README.md) | Script chạy nhanh trên Windows, không phải code nghiệp vụ |
| [`codex/`](codex/PROJECT_GUIDE.md) | Ba tài liệu cho AI: hiểu dự án, review và danh sách Task theo sprint |

Mỗi phần có cấu hình và lệnh chạy tại chỗ. Root chỉ giữ hướng dẫn và cấu hình chung như `.gitignore`, `global.json`; `global.json` chọn .NET SDK cho backend.

## Cách chạy

Giao diện hiện tại dùng React/Vite và dữ liệu mẫu lưu trong trình duyệt. Repository
có dự án ASP.NET Core API, Entity Framework Core và migration trong `backend/`, nhưng
các trang admin chưa kết nối API hay SQL Server. Xem [hướng dẫn admin demo](frontend/ADMIN_DEMO.md)
để biết các luồng mẫu, cách tính báo cáo và giới hạn hiện tại.

Với Node.js 22+ đã cài trên máy, chạy từ thư mục gốc:

```powershell
npm.cmd --prefix frontend ci
npm.cmd --prefix frontend run dev
```

Mở http://127.0.0.1:5173. Dừng server bằng `Ctrl+C`.
Xem [quy trình nhóm](planning/team-workflow.md) để chạy backend, kiểm thử và làm việc với migration.

Nếu muốn dùng script tiện ích Windows (tự cài dependency nếu thiếu):

```powershell
.\scripts\start.cmd
```

## Thành viên

* Thành viên 1: Lâm Chí Khang
* Thành viên 2: Hà Đức Phúc
* Thành viên 3: Nguyễn Quốc Minh
* Thành viên 4: Hoàng Ngọc Đại
* Thành viên 5: Lê Vũ Huy Trường

test
