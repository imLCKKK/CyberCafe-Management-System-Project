# TEAM GUIDE

## Chạy
```powershell
cd D:\CyberCafe-Management-System-Project
npm.cmd install
npm.cmd run dev
```

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
