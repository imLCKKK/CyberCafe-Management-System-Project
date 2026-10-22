# TEAM GUIDE

## Chạy

Trên máy đã cài môi trường trong `.tools`, khởi động bằng:

```powershell
cd D:\CyberCafe-Management-System-Project
.\start.cmd
```

Mở http://127.0.0.1:5173. Dừng server bằng `Ctrl+C`.

Trong VS Code, đóng terminal cũ và mở **Terminal → New Terminal** để nhận PATH
của Node.js và .NET đã cấu hình riêng cho workspace. Khi đó có thể dùng trực tiếp:

```powershell
node --version
npm.cmd --version
dotnet --version
npm.cmd run dev
```

Ngoài VS Code, dùng `.\npm-local.cmd` và `.\dotnet-local.cmd`.
Build frontend bằng `.\npm-local.cmd run build`.
Các công cụ trong `.tools` không được đưa lên Git; khi clone sang máy khác cần cài
Node.js LTS và .NET SDK, rồi chạy `npm.cmd ci` và `npm.cmd run dev`.

## .NET / C#

SDK .NET được cài trong `.tools/dotnet`. Kiểm tra bằng:

```powershell
.\dotnet-local.cmd --version
```

Repository có dự án `.csproj` trong `api/CyberCafe.Api` và migration EF Core.
Giao diện React/Vite vẫn dùng dữ liệu mẫu; trang admin chưa kết nối API hoặc SQL Server.
Xem `docs/admin-demo.md` để biết kho dữ liệu và các giới hạn của bản demo.

## Mỗi người làm ở đâu?
- Đại: admin/auth, admin/staff, client/auth, client/account, client/tournaments
- Minh: admin/customers, admin/tournaments, admin/promotions, client/wallet
- Phúc: admin/dashboard, admin/computers, client/home
- Khang: admin/orders, admin/products, client/ordering
- Trường: admin/inventory, admin/invoices, admin/reports, client/invoices

## Không tự ý sửa
- web/src/app/routes.jsx
- web/src/app/theme.js
- web/src/app/theme.css
- web/src/app/AdminLayout.jsx
- web/src/app/ClientLayout.jsx

Mỗi module có: Page.jsx + components/ + mockData.js.
