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

## SCRUM-25 · Thống kê theo đăng ký

Chạy `node Backend/integrations/opgg/index.js` trên máy API. API gọi loopback, mặc định cổng 8787. Để đổi cổng, cấu hình `STATS_PORT` cho Node và `Opgg__AdapterPort` cho .NET. Vite vẫn phục vụ endpoint tra cứu công khai. Bản lưu SQL dùng migration `20261118094500_RegistrationStatsSnapshots`.

- `GET /api/{client|admin}/tournaments/{id}/registrations` trả `opggSnapshot` của từng đăng ký, hoặc null.
- `POST /api/{client|admin}/tournaments/{id}/registrations/{registrationId}/opgg/refresh` nhận `{}`. Khách chỉ làm mới đăng ký của mình. Nhân viên cần `tournament.manage`. Giải VALORANT còn ở RegistrationOpen hoặc RegistrationClosed và đăng ký còn Registered. Server lấy Riot ID từ SQL.
- Kết quả gồm `stats` đúng schema adapter và `provenance {origin, snapshotId, capturedAtUtc, snapshotSha256}` theo [contract ML](../../../docs/contracts/ml-team-balancing-pipeline.md). Collector standalone kiểm tra toàn bộ schema và tạo JSON canonical RFC 8785. SQL lưu nguyên JSON này để đối chiếu hash SHA-256, truy vết scope, rank, chỉ số và thời gian gốc. Consumer ML áp dụng ngưỡng độ mới và số trận của S1-P-3 khi suy luận.
- API chờ provider trước khi lấy khóa SQL, đọc lại định danh/trạng thái dưới khóa giải. Kết quả mới thêm một bản bất biến, tăng rowversion giải và phát RegistrationChanged sau commit. Kết quả cache hoặc đến muộn trả bản đã lưu. Lỗi provider giữ nguyên HTTP status/message và bản đã lưu trước đó.

Từ gốc repo, chạy `dotnet run --project Backend/tests/CyberManagement.API.Checks -- --opgg-checks` để kiểm tra fixture về lỗi/quyền và vector hash chung. Để kiểm tra nguồn thật, khởi động adapter, đặt `AUTH_CHECKS_SQL_CONNECTION` tới SQL Server có quyền tạo DB thử, rồi chạy cùng lệnh với `--opgg-live`. Bộ kiểm tra tạo và dọn DB riêng, dùng `OPGG_RIOT_ID` hoặc mặc định `Blue#00h`. Suite browser `opgg` dùng fixture HTTP. Suite browser `opgg-live` đọc endpoint Vite với OP.GG thật.
