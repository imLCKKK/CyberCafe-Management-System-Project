# Frontend React / Vite

Frontend có mã nguồn, package/lockfile, cấu hình Vite, tài nguyên public và
kiểm tra trình duyệt trong cùng thư mục này. Xem [bản đồ src](src/README.md)
và [kiến trúc repository](../docs/architecture.md). Hành vi bản demo admin được ghi trong [ADMIN_DEMO.md](ADMIN_DEMO.md).

## Chạy

Cần Node.js 22 trở lên. Từ thư mục gốc repository:

```powershell
cd frontend
npm.cmd ci
npm.cmd run dev
```

Mở URL Vite in ra. Các lệnh sau cũng chạy trong `frontend/`:

```powershell
npm.cmd run build
npm.cmd run test:frontend:models
npm.cmd run test:frontend:render
npm.cmd run test:frontend
```

Kiểm tra trình duyệt cần Chrome hoặc Edge; có thể chỉ định executable bằng
`CHROME_PATH`. Kết quả/ảnh kiểm tra nằm ở `.tools/frontend-retest/` tại gốc
repository. `npm.cmd run test:frontend -- opgg-live` kiểm tra nguồn OP.GG thật
và cần mạng; các suite mặc định dùng fixture cho OP.GG.

## Trạng thái tích hợp

Các màn hình hiện dùng dữ liệu demo trong localStorage, chưa kết nối API
nghiệp vụ ASP.NET Core hoặc SQL Server và chưa đồng bộ giữa hai máy. Vite chỉ
gắn middleware OP.GG từ `../backend/integrations/opgg/`; integration này phục
vụ tra cứu thống kê game. Không coi việc chạy được frontend là đã hoàn tất
backend quản lý phòng máy.
