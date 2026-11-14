# CyberCafe Management System

**SCRUM-21 / S2-M-1** bổ sung shared credit/debit/refund và nạp tiền mặt tại quầy; xem
[hợp đồng ví](Backend/docs/003-customer-wallet-contract.md) để biết API, tiền tặng, migration và cách kiểm tra.

Đồ án .NET quản lý phòng máy: máy/phiên, khách hàng, nhân viên, ví, món/đơn, kho, hóa đơn/báo cáo, giải đấu và khuyến mãi. Nhận diện NEXORA CYBER; React/Vite + ASP.NET Core .NET 10 + EF Core + SQL Server.

## Nền được chọn

Ngày 02/11/2026, Trưởng nhóm chọn **backend CyberManagement, database SQL Server mới 35 bảng**. Solution [Backend/CyberManagement.slnx](Backend/CyberManagement.slnx) gồm Domain, Application, Infrastructure, API và project kiểm tra auth. Chủ module làm xuyên tầng; API là host chung.

Backend đã có auth nhân viên/khách, JWT/permission, seed và health. Frontend hiện vẫn chủ yếu demo/localStorage; nối UI và các nghiệp vụ còn triển khai theo [55 task hiện hữu](codex/SPRINT_TASKS.md). Giữ tên/owner/sprint; chưa tự nghiệm thu Task hoặc đưa thay đổi lên `dev`.

DB mới tạo từ migrations CyberManagement; không áp InitialCreate mới lên DB 19 bảng cũ. OP.GG vẫn dùng adapter Node tại `Backend/integrations/opgg/`. SignalR hai máy, ZaloPay sandbox và ML theo kế hoạch; chưa được coi hoàn thành từ việc có schema.

## Thử nạp tại quầy — SCRUM-21

Từ root của checkout này, có SQL Server Express cục bộ:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-counter-topup.ps1
```

Frontend `http://127.0.0.1:5176`, API cổng 5094; script dùng DB demo riêng và giữ dữ liệu để reload.
Khách thử: `0900000021` / `CounterCustomer-2026!` tại `/client/auth`; nhân viên:
`admin` / `CounterAdmin-2026!` tại `/admin/auth`. Đây là tài khoản fixture cục bộ.
Khách gửi nạp 50.000 ở Ví, admin mở `/admin/topups` xác nhận đã thu 50.000 mới cộng 55.000 vào ví.
UI hiển thị riêng tiền mặt/tiền tặng/tổng; gửi hoặc duyệt lại không cộng thêm.
Luồng ZaloPay nằm ở task riêng; worker phiên và checkout dùng shared writer qua giao điểm trong hợp đồng.

Kiểm tra SQL tập trung (runner luôn tạo/dọn DB thử riêng, không dùng DB demo đang chạy):

```powershell
$env:AUTH_CHECKS_SQL_CONNECTION = 'Server=127.0.0.1\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks -- --wallet-only
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks -- --wallet-migration-checks
Remove-Item Env:AUTH_CHECKS_SQL_CONNECTION
.\scripts\npm-local.cmd --prefix frontend run test:counter-topups
.\scripts\npm-local.cmd --prefix frontend run test:counter-topups:render
```

Các lệnh trên là hướng dẫn tái hiện, không phải xác nhận đã pass hoặc Trưởng nhóm đã duyệt.

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
