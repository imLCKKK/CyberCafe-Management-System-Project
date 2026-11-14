Ctr + Shift + V để xem dưới dạng md


# 001 – Định danh dùng chung & Ma trận quyền (SCRUM-2)

Cập nhật nền 02/11/2026: Trưởng nhóm chọn DB mới 35 bảng và backend CyberManagement. Các lệnh dưới đây chạy từ `Backend/`; lệnh từ repository root và chế độ kiểm tra SQL nằm trong [Backend README](../README.md). Không dùng InitialCreate mới để nâng cấp DB CyberCafe 19 bảng. Override development được ignore; signing key và mật khẩu ở User Secrets/biến môi trường.

Tài liệu này chốt định danh, vai trò, ma trận quyền và quy tắc khóa. Mọi module (Inventory, Session, Tournament, Invoice...) dùng chung các quy ước dưới đây.

Luồng Identity tuân theo Clean Architecture: API nhận request, phát JWT và ánh xạ HTTP; Application xử lý đăng nhập/khóa tài khoản, xác thực dữ liệu qua request validators, seed vai trò-quyền và định nghĩa DTO/interface; Infrastructure triển khai repository EF Core và password hashing.

## 1. Định danh

| Đối tượng | Bảng | Khóa | Đăng nhập hệ thống? |
|---|---|---|---|
| Staff | `Employees` | `EmployeeId` (int) | **Có** (Admin/Manager/Cashier/Waiter/Technician) |
| Customer | `Customers` | `CustomerId` (int) | **Có** – đăng nhập bằng `Phone` và mật khẩu |

Quyết định:

- Nhân viên và Customer là hai loại principal riêng; không tạo bảng Account riêng. Nhân viên xác thực qua `Employees`, Customer xác thực qua `Customers`.
- Customer tiếp tục là dữ liệu nghiệp vụ được nhân viên quản lý; khách đăng nhập bằng `Phone` duy nhất và `PasswordHash`.
- **Quy ước ID dùng chung**: mọi module chỉ tham chiếu `EmployeeId` và `CustomerId` (int, identity), không tự tạo ID khác. Các cột `EmployeeId`, `CreatedBy`, `ChangedBy` trong Orders, Invoices, Tournaments, StockReceipts, AuditLogs... đều là `Employees.EmployeeId`.
- **Danh tính trong request** (JWT claims): `sub` = `EmployeeId`, `role` = `Roles.RoleName`, kèm danh sách `permission` (từ `RolePermissions`) hoặc tra khi cần.

### Thông tin xác thực

Nhân viên lưu các trường sau trong `Employees`:

| Cột | Kiểu | Mục đích |
|---|---|---|
| `Username` | varchar(50), unique | Tên đăng nhập |
| `PasswordHash` | varchar(255) | Hash mật khẩu (PasswordHasher/BCrypt), không lưu thô |

`IsActive = 0` nghĩa là tài khoản bị vô hiệu hóa, không đăng nhập được.

Customer lưu `PasswordHash` trong `Customers`; không lưu mật khẩu rõ. Customer không có bộ đếm đăng nhập sai/khóa tạm; trạng thái khác `Active` vẫn không được đăng nhập.

## 2. Vai trò (`Roles`)

| Role | Mô tả |
|---|---|
| `Admin` | Toàn quyền, quản lý nhân viên, vai trò, phân quyền |
| `Manager` | Quản lý vận hành: kho, giải đấu, khuyến mãi, báo cáo, xử lý ngoại lệ |
| `Cashier` | Thu ngân/quầy: khách, nạp ví, mở/đóng máy, đơn hàng, hóa đơn, thanh toán |
| `Waiter` | Phục vụ: nhận order đồ ăn/nước, mang đến máy cho khách, cập nhật trạng thái đơn; không đụng đến tiền |
| `Technician` | Kỹ thuật: xem máy, cập nhật trạng thái máy, bảo trì |

## 3. Ma trận quyền (`Permissions` × `Roles`)

Ký hiệu: ✔ được phép · ✘ không · ◐ chỉ dữ liệu của chính mình.

Tên permission theo quy ước `module.action` và lưu vào `Permissions.PermissionName`.

### Nhân viên, phân quyền, ca, nhật ký

| Permission | Admin | Manager | Cashier | Waiter | Technician |
|---|:-:|:-:|:-:|:-:|:-:|
| `employee.view` | ✔ | ✔ | ✘ | ✘ | ✘ |
| `employee.manage` | ✔ | ✘ | ✘ | ✘ | ✘ |
| `role.manage` (Roles, Permissions, RolePermissions) | ✔ | ✘ | ✘ | ✘ | ✘ |
| `shift.view` | ✔ | ✔ | ◐ | ◐ | ◐ |
| `shift.manage` | ✔ | ✔ | ✘ | ✘ | ✘ |
| `audit.view` | ✔ | ✔ | ✘ | ✘ | ✘ |

### Khách hàng và ví

| Permission | Admin | Manager | Cashier | Waiter | Technician |
|---|:-:|:-:|:-:|:-:|:-:|
| `customer.view` | ✔ | ✔ | ✔ | ✔ | ✘ |
| `customer.create` | ✔ | ✔ | ✔ | ✘ | ✘ |
| `customer.update` | ✔ | ✔ | ✔ | ✘ | ✘ |
| `customer.status.change` (khóa/mở khóa) | ✔ | ✔ | ✘ | ✘ | ✘ |
| `wallet.view` | ✔ | ✔ | ✔ | ✘ | ✘ |
| `wallet.topup` | ✔ | ✔ | ✔ | ✘ | ✘ |
| `wallet.adjust` | ✔ | ✔ | ✘ | ✘ | ✘ |
| `wallet.refund` | ✔ | ✔ | ✘ | ✘ | ✘ |

### Máy, phòng máy, bảo trì

| Permission | Admin | Manager | Cashier | Waiter | Technician |
|---|:-:|:-:|:-:|:-:|:-:|
| `computer.view` | ✔ | ✔ | ✔ | ✔ | ✔ |
| `computer.manage` (Rooms, Types, Computers) | ✔ | ✔ | ✘ | ✘ | ✘ |
| `computer.status.update` | ✔ | ✔ | ✘ | ✘ | ✔ |
| `maintenance.view` | ✔ | ✔ | ✘ | ✘ | ✔ |
| `maintenance.manage` | ✔ | ✔ | ✘ | ✘ | ✔ |

### Phiên chơi (GamingSessions)

| Permission | Admin | Manager | Cashier | Waiter | Technician |
|---|:-:|:-:|:-:|:-:|:-:|
| `session.view` | ✔ | ✔ | ✔ | ✔ | ✘ |
| `session.start` | ✔ | ✔ | ✔ | ✘ | ✘ |
| `session.pause` / `session.end` | ✔ | ✔ | ✔ | ✘ | ✘ |
| `session.cancel` | ✔ | ✔ | ✘ | ✘ | ✘ |

### Sản phẩm, kho, nhà cung cấp

| Permission | Admin | Manager | Cashier | Waiter | Technician |
|---|:-:|:-:|:-:|:-:|:-:|
| `product.view` | ✔ | ✔ | ✔ | ✔ | ✘ |
| `product.manage` (Products, Categories) | ✔ | ✔ | ✘ | ✘ | ✘ |
| `supplier.manage` | ✔ | ✔ | ✘ | ✘ | ✘ |
| `stockreceipt.view` | ✔ | ✔ | ✘ | ✘ | ✘ |
| `stockreceipt.create` | ✔ | ✔ | ✘ | ✘ | ✘ |
| `inventory.adjust` (Adjust/Damaged) | ✔ | ✔ | ✘ | ✘ | ✘ |

### Đơn hàng

| Permission | Admin | Manager | Cashier | Waiter | Technician |
|---|:-:|:-:|:-:|:-:|:-:|
| `order.view` | ✔ | ✔ | ✔ | ✔ | ✘ |
| `order.create` | ✔ | ✔ | ✔ | ✔ | ✘ |
| `order.status.update` | ✔ | ✔ | ✔ | ✔ | ✘ |
| `order.cancel` | ✔ | ✔ | ✘ | ✘ | ✘ |

### Hóa đơn, thanh toán, khuyến mãi

| Permission | Admin | Manager | Cashier | Waiter | Technician |
|---|:-:|:-:|:-:|:-:|:-:|
| `invoice.view` | ✔ | ✔ | ✔ | ✘ | ✘ |
| `invoice.create` | ✔ | ✔ | ✔ | ✘ | ✘ |
| `invoice.void` | ✔ | ✔ | ✘ | ✘ | ✘ |
| `payment.create` | ✔ | ✔ | ✔ | ✘ | ✘ |
| `payment.refund` | ✔ | ✔ | ✘ | ✘ | ✘ |
| `promotion.view` | ✔ | ✔ | ✔ | ✘ | ✘ |
| `promotion.manage` | ✔ | ✔ | ✘ | ✘ | ✘ |

### Giải đấu

| Permission | Admin | Manager | Cashier | Waiter | Technician |
|---|:-:|:-:|:-:|:-:|:-:|
| `game.manage` (Games, GameRanks) | ✔ | ✔ | ✘ | ✘ | ✘ |
| `tournament.view` | ✔ | ✔ | ✔ | ✘ | ✘ |
| `tournament.manage` (tạo/mở/đóng) | ✔ | ✔ | ✘ | ✘ | ✘ |
| `tournament.register` (đăng ký thay khách) | ✔ | ✔ | ✔ | ✘ | ✘ |
| `team.assign` (chạy ML chia đội) | ✔ | ✔ | ✘ | ✘ | ✘ |

### Báo cáo

| Permission | Admin | Manager | Cashier | Waiter | Technician |
|---|:-:|:-:|:-:|:-:|:-:|
| `report.view` | ✔ | ✔ | ✘ | ✘ | ✘ |

## 4. Quy tắc trạng thái tài khoản

**Nhân viên (đăng nhập)**

- Sai mật khẩu trả `401`; không khóa tài khoản theo số lần đăng nhập sai.
- Admin vô hiệu hóa thủ công bằng `IsActive = 0`; mọi thay đổi ghi `AuditLogs` (`EntityType = 'Employee'`).

**Khách hàng**

- Trạng thái nằm ở `Customers.Status` (`Active` / `Locked`).
- Khóa/mở khóa chỉ Admin/Manager (`customer.status.change`), **bắt buộc có lý do**, ghi vào `CustomerStatusHistory` (`OldStatus`, `NewStatus`, `Reason`, `ChangedBy`).
- Khách `Locked` không được mở phiên chơi, tạo đơn hay nạp/trừ ví (kiểm tra tại API, không chỉ ở UI).

## 5. Cơ chế xác thực & mã lỗi

- Xác thực dùng chung: **JWT Bearer**, access token mặc định sống 20 phút (cấu hình được từ 1–30 phút).
- `POST /api/auth/login` cấp token; `GET /api/auth/me` trả danh tính hiện tại.
- `POST /api/auth/logout` yêu cầu token hợp lệ và trả `204`; client phải xóa token. JWT access token không bị thu hồi phía server và tự hết hạn; muốn thu hồi ngay cần bổ sung refresh-token/revocation lưu DB.
- Customer dùng `POST /api/auth/customer/login` với `phone` và `password`; `GET /api/auth/customer/me` xem danh tính và `POST /api/auth/customer/logout` đăng xuất.
- Quyền được kiểm tra **tại API** bằng `[Authorize(Policy = "permission:<module.action>")]`; token chỉ chứa quyền lấy từ `RolePermissions`.
- Tài khoản nhân viên không hoạt động hoặc customer có trạng thái khác `Active` nhận `403`.

| Tình huống | HTTP |
|---|---|
| Sai tài khoản hoặc mật khẩu (thông báo chung, không lộ sai cái nào) | `401` |
| Chưa đăng nhập / token hết hạn hoặc không hợp lệ | `401` |
| Tài khoản bị khóa tạm hoặc `IsActive = 0` | `403` |
| Đã đăng nhập nhưng thiếu permission | `403` |

## 6. Tài khoản seed

| Username | Role | Dùng để test |
|---|---|---|
| `admin` | Admin | Toàn quyền |
| `manager01` | Manager | Kho, giải đấu, khuyến mãi |
| `cashier01` | Cashier | Quầy: khách, phiên, đơn, hóa đơn |
| `waiter01` | Waiter | Nhận order, giao đồ tại máy |
| `tech01` | Technician | Bảo trì máy |

Mật khẩu seed chỉ dùng môi trường dev, lưu dạng hash, đọc từ cấu hình chứ không hard-code trong repo.

Trong Development, cấu hình signing key và mật khẩu qua .NET User Secrets (không đưa giá trị thật vào `appsettings*.json`):

```powershell
dotnet user-secrets set "Authentication:Jwt:SigningKey" "<random-secret-at-least-32-bytes>" --project .\src\CyberManagement.API
dotnet user-secrets set "SeedAccounts:Admin:Password" "<unique-password-at-least-12-characters>" --project .\src\CyberManagement.API
dotnet user-secrets set "SeedAccounts:Manager:Password" "<unique-password-at-least-12-characters>" --project .\src\CyberManagement.API
dotnet user-secrets set "SeedAccounts:Cashier:Password" "<unique-password-at-least-12-characters>" --project .\src\CyberManagement.API
dotnet user-secrets set "SeedAccounts:Waiter:Password" "<unique-password-at-least-12-characters>" --project .\src\CyberManagement.API
dotnet user-secrets set "SeedAccounts:Technician:Password" "<unique-password-at-least-12-characters>" --project .\src\CyberManagement.API
```

Khi khởi động, API áp dụng EF migrations và seed vai trò/quyền. Các tài khoản mẫu chỉ được tạo nếu cấu hình đủ cả năm mật khẩu; mật khẩu được lưu bằng `PasswordHasher`. Tài khoản đã tồn tại không bị thay đổi mật khẩu hoặc vai trò khi seed lại. Chỉ tài khoản seed trong Development; production phải được provision bằng quy trình riêng.
Production cũng phải cấp `Authentication__Jwt__SigningKey` qua secret manager/environment variable (tối thiểu 32 byte); không lưu signing key trong cấu hình được commit.

Chạy kiểm tra HTTP đăng nhập sai, khóa tài khoản và truy cập sai quyền:

```powershell
dotnet run --project .\tests\CyberManagement.API.Checks
```
