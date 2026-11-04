# Bản đồ hệ thống

Cập nhật quyết định 02/11/2026: **CyberManagement + database SQL Server mới 35 bảng** là nền chung. Giữ frontend demo, OP.GG và 55 Task; phần sửa hiện dừng trước commit, không mặc định đã có trên `dev`.

## Luồng hiện tại

```text
React/Vite frontend -> routes/layout -> provider/model demo -> localStorage
Vite /api/valorant/opgg/stats -> Backend/integrations/opgg -> OP.GG
Backend/src/CyberManagement.API -> auth/roles/health
  -> Application (identity/seed/validation/interfaces)
  -> Infrastructure (EF repositories/hash) -> SQL Server mới 35 bảng
```

Đăng nhập UI chưa được coi là tích hợp chỉ vì backend auth chạy; Task Build-Admin-Client-Login vẫn cần nối API. SignalR, ZaloPay, các nghiệp vụ ví/đơn/kho/voucher và ML triển khai theo task. OP.GG lỗi phải hiện lỗi, fixture chỉ là kiểm tra nội bộ.

## Vị trí và chiều phụ thuộc

| Nội dung | Đường dẫn |
| --- | --- |
| Solution | `Backend/CyberManagement.slnx` |
| Entity | `Backend/src/CyberManagement.Domain/Entities/` |
| Use case, DTO, interface, validator | `Backend/src/CyberManagement.Application/` |
| DbContext, cấu hình EF, factory | `Backend/src/CyberManagement.Infrastructure/Persistance/` |
| Migration | `Backend/src/CyberManagement.Infrastructure/Migrations/` |
| Controller, JWT, HTTP/DI | `Backend/src/CyberManagement.API/` |
| Auth regression tests | `Backend/tests/CyberManagement.API.Checks/` |
| Adapter OP.GG + helper browser/Node | `Backend/integrations/opgg/` |
| UI admin/client | `frontend/src/admin/`, `frontend/src/client/` |

```text
Application -> Domain
Infrastructure -> Application/Domain
API -> Application/Infrastructure (composition root)
```

Domain độc lập với EF/HTTP; Application không dùng DbContext/HttpContext. Owner giữ module xuyên các tầng; interface theo nghiệp vụ, dùng chung khi thật sự cần. Hub/adapter SignalR nằm tầng ngoài. Giữ đúng chữ hoa `Backend` trong mọi import/lệnh để clone trên Linux hoạt động.

## Database và định danh

Schema được tạo từ bốn migration CyberManagement hiện có: InitialCreate, CustomerPasswordAuthentication, RemoveCustomerLoginLockout, RemoveEmployeeLoginLockout. Đích là **DB mới**, không phải migration nâng cấp tự động từ DB CyberCafe 19 bảng. DB cũ nếu đang có được giữ riêng; lần sửa này không chuyển/xóa dữ liệu cũ.

Staff dùng `Employees.EmployeeId`; khách dùng `Customers.CustomerId`, kiểu int. Không có bảng Account riêng. Mapping account ID trong demo sang principal thực phải được chốt trước khi nối API. [Định danh, role, permission, logout](../Backend/docs/001-monorepo.md) và [lệnh EF/config](../Backend/README.md) là hướng dẫn tích hợp hiện tại.

JWT stateless: logout phía server trả 204, client xóa token; token tự hết hạn. Thu hồi ngay khi khóa/đổi quyền cần contract riêng. Signing key/mật khẩu ở User Secrets/biến môi trường; local override và build output không được theo dõi trong Git.

## Ba kho demo cần đối chiếu khi nối API

| Khóa | Nơi sở hữu |
| --- | --- |
| `tram.client.dbml.demo.v2` | `client/shared/ClientDemoContext.jsx`, `demoModel.js`, `demoData.js` |
| `tram.client.home.demo.v1` | `client/home/useHomeData.js`, `homeModel.js` |
| `net-cafe.admin.computers.v1` | `admin/computers/computerStore.js` |

Admin dùng dữ liệu chung với client; sửa model phải kiểm tra caller hai phía. Storage event chỉ đồng bộ tab cùng origin. Luồng chuyển API: server kiểm tra quyền/dữ liệu, tính tiền/giảm/tồn, lưu transaction, commit rồi phát SignalR và UI refetch. API lỗi không quay về mock rồi coi là thành công.

Xem [backlog](../codex/SPRINT_TASKS.md), [quy trình nhóm](../planning/team-workflow.md) và [kế hoạch](../planning/team-delivery-plan.md). Các báo cáo trong `docs/reports/` là lịch sử ở ngày ghi trong tên; kết quả sửa/review mới được gửi trong chat theo yêu cầu Trưởng nhóm.
