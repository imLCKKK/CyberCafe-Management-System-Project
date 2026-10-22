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
* ASP.NET Core MVC
* Entity Framework Core
* SQL Server
* HTML / CSS / JavaScript
* Bootstrap

## Database

Sử dụng SQL Server.

## Cách chạy

Giao diện hiện tại dùng React/Vite và dữ liệu mẫu lưu trong trình duyệt. Repository
có dự án ASP.NET Core API, Entity Framework Core và migration trong `api/`, nhưng
các trang admin chưa kết nối API hay SQL Server. Xem [hướng dẫn admin demo](docs/admin-demo.md)
để biết các luồng mẫu, cách tính báo cáo và giới hạn hiện tại.

Trên máy đã cài môi trường trong `.tools`, chạy:

```powershell
.\start.cmd
```

Mở http://127.0.0.1:5173. Dừng server bằng `Ctrl+C`.
Xem [TEAM_GUIDE.md](TEAM_GUIDE.md) để dùng Node.js, npm và .NET SDK trong terminal.

Nếu clone sang máy khác, cài Node.js LTS rồi chạy:

```powershell
npm.cmd ci
npm.cmd run dev
```

## Thành viên

* Thành viên 1: Lâm Chí Khang
* Thành viên 2: Hà Đức Phúc
* Thành viên 3: Nguyễn Quốc Minh
* Thành viên 4: Hoàng Ngọc Đại
* Thành viên 5: Lê Vũ Huy Trường
