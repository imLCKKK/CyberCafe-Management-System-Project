# Tích hợp OP.GG

Đây là adapter Node.js đọc thống kê VALORANT từ OP.GG. API nghiệp vụ quản lý phòng máy nằm ở [`CyberManagement.API`](../../src/CyberManagement.API/).

- `opgg.js`: truy vấn, kiểm tra và chuẩn hóa dữ liệu nguồn.
- `statsMiddleware.js`: endpoint `GET /api/valorant/opgg/stats?riotId=Name%23TAG`.
- `index.js`: chạy riêng với cổng `STATS_PORT`, mặc định `8787`.
- `shared.js`: xử lý Riot ID/URL dùng chung giữa adapter Node và frontend; không import API của Node trong file này.
- `opgg.test.js`: kiểm thử adapter bằng dữ liệu kiểm thử cô lập.
- `package.json`: đánh dấu thư mục dùng ES module; adapter không cần dependency npm riêng.

Từ thư mục gốc repository, `npm.cmd --prefix frontend run dev` và `npm.cmd --prefix frontend run preview` tự gắn middleware vào Vite. Chạy adapter riêng bằng `node Backend/integrations/opgg/index.js`; chạy kiểm tra bằng `npm.cmd --prefix frontend run test:opgg`.

Khi phục vụ frontend bằng server khác, cần đưa đường dẫn `/api/valorant/opgg/stats` tới adapter này. Chỉ đưa thư mục `frontend/dist/` lên static hosting sẽ không tạo được endpoint.

Hợp đồng response, cache, giới hạn và hướng dẫn kiểm tra với nguồn thật: [tài liệu giải đấu](../../../frontend/src/client/tournaments/README.md). Endpoint bên ngoài và dữ liệu OP.GG có thể thay đổi; lỗi nguồn phải được hiển thị rõ, không thay bằng thống kê giả.

Adapter giữ nguyên endpoint công khai; chỉ thay đổi nơi đặt mã nguồn.
