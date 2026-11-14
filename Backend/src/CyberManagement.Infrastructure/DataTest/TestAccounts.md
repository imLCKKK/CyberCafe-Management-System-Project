# Tài khoản kiểm thử SCRUM-13

Các tài khoản dưới đây được tạo tự động bởi CyberManagement.Inventory.Checks.
Mật khẩu chung chỉ dùng cho fixture test: `Inventory-check-only-2026!`.

| Vai trò | Tên đăng nhập / số điện thoại | Trang đăng nhập | Quyền đọc kho |
| --- | --- | --- | --- |
| Admin | `admin` | `/admin/auth` | Có |
| Manager | `manager01` | `/admin/auth` | Có |
| Cashier | `cashier01` | `/admin/auth` | Không |
| Waiter | `waiter01` | `/admin/auth` | Không |
| Technician | `tech01` | `/admin/auth` | Không |
| Customer | `0900000013` | `/client/auth` | Không |

Khách có tên Inventory Check Customer, đăng nhập bằng số điện thoại, không dùng username.
Nhân viên gọi POST `/api/auth/login` với `username` và `password`;
khách gọi POST `/api/auth/customer/login` với `phone` và `password`.

## Khởi tạo tài khoản để test giao diện

Từ thư mục gốc repository, bật MSSQLLocalDB rồi chạy:

```powershell
$env:INVENTORY_CHECKS_SQL_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.Inventory.Checks -- --serve
```

Chờ `INVENTORY_HOST_READY`. Host kiểm thử chạy ở `http://127.0.0.1:5093`.
Khi chạy frontend, đặt `VITE_API_PROXY_TARGET=http://127.0.0.1:5093` để gọi đúng host này.

Tài khoản nằm trong database test `CyberManagement_SCRUM13_Checks_<guid>` riêng của lượt chạy.
Host dừng bình thường bằng Ctrl+C sẽ dọn database đó. Chạy lại host sẽ tạo DB/tài khoản mới.
Đây là danh sách account fixture, không phải script seed vào DB của API chạy bằng F5.
Các mật khẩu này không tự áp dụng cho tài khoản trong database khác hoặc môi trường triển khai.

## Tài khoản bổ sung chỉ để kiểm tra API

Cùng mật khẩu fixture ở trên:

| Username | Permission của role test | GET `/api/admin/inventory` |
| --- | --- | --- |
| `inventory-reader` | `stockreceipt.view` | 200 |
| `inventory-adjuster` | `inventory.adjust` | 403 |

Hai role test không thuộc năm role nhân viên auth/me hỗ trợ; dùng token từ API login để kiểm tra
policy trực tiếp, không dùng chúng để đăng nhập giao diện.

Nguồn: Program.cs của CyberManagement.Inventory.Checks và IdentitySeeder.cs của Application.
Không lưu mật khẩu tài khoản thật của nhóm vào file này.
