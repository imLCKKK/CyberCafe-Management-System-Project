# Kiểm thử lại frontend admin và client — 22/10/2026

Phạm vi: frontend React/Vite hiện có, dữ liệu demo trong trình duyệt. Kiểm thử bằng Chrome headless trên Windows, với profile riêng, không sử dụng dữ liệu trong profile trình duyệt của người dùng.

## Kết quả

| Nhóm | Phạm vi |
| --- | --- |
| Build production | Vite build thành công |
| Kiểm thử logic | 58/58 bài đạt, gồm model client, Home, admin, hóa đơn, gợi ý món, giải đấu và adapter thống kê |
| Render | 3 script kiểm tra admin, Home và giải đấu đạt |
| Browser workflow | 8/8 nhóm đạt trong lượt chạy tổng hợp cuối, không ghi nhận runtime exception hoặc React console error trong các nhóm này |
| Responsive và điều hướng | 16 trang × 5 độ rộng = 80 lượt; 320, 390, 768, 1024, 1440 px; chiều cao 900 px |
| Nội dung trang | Có tiêu đề, đúng một liên kết menu đang hoạt động, không NaN, không ảnh lỗi hoặc nút thiếu tên truy cập |
| Dữ liệu bất thường | JSON hỏng, sai cấu trúc, ngày hỏng, khôi phục mẫu, lỗi hết dung lượng lưu trữ |
| Trạng thái giới hạn | 25 lượt render kết hợp 5 trang client với: hết phiên, ví bằng 0, tài khoản khóa, bắt buộc đổi mật khẩu, danh sách rỗng |
| Hai tab thật | Admin sửa hồ sơ → client cập nhật; client sửa → admin cập nhật; không tải lại trang |

Các trang được duyệt ở cả 5 độ rộng:

| Khu vực | Trang |
| --- | --- |
| Admin | Tổng quan, Máy, Khách hàng, Order, Sản phẩm, Kho, Hóa đơn & thanh toán, Giải đấu, Nhân viên, Báo cáo |
| Client | Trang chủ, Gọi món, Ví, Hóa đơn, Tài khoản, Giải đấu |

Các bảng và dải tab có vùng cuộn ngang riêng khi cần; kiểm tra responsive yêu cầu toàn trang không tràn ngang. Có lưu ảnh của cả 80 lượt duyệt; một số ảnh desktop/mobile và hộp thoại được xem trực tiếp để đối chiếu giao diện.

## Các luồng đã chạy trên trình duyệt

Log của lượt tổng hợp cuối: [summary.json](../.tools/frontend-retest/2026-10-22T08-49-44-056Z/summary.json). Các phép đo responsive: [routes.json](../.tools/frontend-retest/2026-10-22T08-49-44-056Z/routes.json). Đây là artifact cục bộ, không đưa vào Git.

- Admin: tạo khách hàng và mật khẩu băm; yêu cầu nạp, duyệt cộng ví một lần, xác nhận thu tiền không cộng lại; tạo sản phẩm; nhập kho; chặn xuất quá tồn; chặn khóa admin cuối cùng; tạo nhân viên.
- Admin: xác nhận món tiền mặt và thu tiền; duyệt hủy món trả ví, hoàn đúng tiền và tồn kho; kiểm tra trạng thái đồng bộ sang client; tạo/mở giải, thêm người tham gia và hiển thị phía client.
- Máy tính: thêm máy, tìm kiếm, chuyển bảo trì, tải lại để kiểm tra lưu trạng thái, chuyển dạng bảng, hoàn tất bảo trì, chặn trùng tên không phân biệt hoa/thường, đặt lại bộ lọc rỗng.
- Bảng admin: tìm kiếm không có kết quả, xóa bộ lọc, đổi chiều sắp xếp, chuyển trang ở các bảng có đủ bản ghi. Báo cáo: khoảng ngày sai và khôi phục, bật/tắt chuỗi biểu đồ, đọc điểm bằng bàn phím, tải CSV và kiểm tra nội dung.
- Client: chuyển món từ Home sang giỏ, thanh toán ví, tạo yêu cầu hủy không hoàn sớm, đối chiếu số dư Home; nhập số tiền nạp dưới mức tối thiểu hoặc sai bước; ZaloPay bị vô hiệu hóa; thông báo yêu cầu nạp; mô phỏng duyệt/thu; lọc tiền vào/ra.
- Tài khoản: chặn tên chỉ có khoảng trắng, lưu tên và sidebar, mật khẩu cũ sai/đúng, không lưu mật khẩu mới dạng rõ, giữ thay đổi sau tải lại.
- Giải đấu client: Riot ID sai/đúng, đăng ký, tìm kiếm không có kết quả và đặt lại, đồng bộ trạng thái về Home; provider thống kê không sẵn sàng vẫn giữ chỉ số thiếu thay vì tạo số liệu giả.
- Gợi ý món: hoạt ảnh chạy và chậm dần, kết quả khớp món; đổi số lượng; chuyển khoản không tạo đơn; Escape, mở lại, đặt tiền mặt không trừ ví; nhấp hai lần không tạo hai đơn; hiển thị trạng thái đơn và hóa đơn; mobile, Tab giữ focus trong dialog, reduced motion, đặt hết tồn vẫn giữ màn hình thành công.
- Hóa đơn: đối chiếu ví/tiền mặt đã thu/chờ thu/món hủy; tìm mã đơn và tên món không dấu; kết hợp bộ lọc, khoảng ngày Việt Nam, trạng thái rỗng; chi tiết nhóm đơn; tải TXT và kiểm tra số tiền; thao tác in, CSS in và tạo file PDF hợp lệ; phân trang và đặt lại trang khi lọc; hộp thoại mobile vừa màn hình.
- Khả năng phục hồi: xóa đơn đang mở từ thay đổi storage, không làm trắng trang; lỗi lưu hồ sơ hiển thị thông báo và giữ nguyên dữ liệu đã lưu.

## Lỗi đã sửa

1. `/client` thiếu route mặc định, chỉ hiển thị khung trang. Đã thêm chuyển hướng đến `/client/home`.
2. Dữ liệu lưu có ngày sai vẫn vượt qua kiểm tra và làm `Intl.DateTimeFormat` ném lỗi, trắng trang Tài khoản. Đã kiểm tra các trường `*_at` trước render, đồng thời chặn ID trùng và khách tham chiếu tài khoản không tồn tại. Giao diện cảnh báo và cho khôi phục mẫu.
3. Đơn đang xem bị xóa khi dữ liệu đổi có thể truyền `undefined` vào chi tiết đơn. Đã kiểm tra đơn còn tồn tại và bảo vệ component chi tiết dùng chung.
4. Home gọi tổng tiền món là “đã trừ ví” dù có đơn tiền mặt. Đã đổi nhãn thành “ghi nhận”; bài kiểm tra có đơn tiền mặt xác nhận nhãn đúng.
5. Script render giải đấu thiếu `ClientDemoProvider` khi render Home, và mock storage không phân biệt hai kho. Đã bổ sung provider và đọc đúng storage key.

## Chạy lại

```powershell
.\npm-local.cmd run build
.\npm-local.cmd run test:frontend:models
.\npm-local.cmd run test:frontend:render
.\npm-local.cmd run test:frontend
```

Bộ browser test tự khởi động Vite và Chrome, chọn cổng riêng, dùng profile tạm, đóng server/browser khi kết thúc. Nếu Chrome nằm ngoài các vị trí mặc định, đặt `CHROME_PATH` đến executable. Trong môi trường chạy kiểm thử hạn chế process sandbox như phiên này, chạy:

```powershell
$env:CHROME_NO_SANDBOX='1'
.\npm-local.cmd run test:frontend
```

Có thể chạy riêng một nhóm, ví dụ `npm-local.cmd run test:frontend -- cross-tab resilience`. Nguồn kiểm thử nằm trong `tests/frontend/`. Kết quả từng nhóm, ảnh, CSV/TXT và PDF được lưu ở `.tools/frontend-retest/<timestamp>/`, với `summary.json` và `routes.json`.

## Giới hạn còn lại

- `/admin/auth`, `/client/auth`, `/admin/promotions` vẫn là trang giữ chỗ. Đã ghi nhận đúng tình trạng; không coi là chức năng hoàn thành.
- Nạp ZaloPay/chuyển khoản trong các màn hình chờ tích hợp vẫn bị khóa đúng thiết kế.
- Chưa kiểm thử đăng nhập/phân quyền thực, SQL Server, API nghiệp vụ, thanh toán thật. Frontend hiện dùng dữ liệu demo.
- Kho máy tính và kho giải đấu có cấu trúc riêng; kết quả đồng bộ trong báo cáo áp dụng cho các luồng đã liệt kê, không có nghĩa mọi bảng đều dùng một kho dữ liệu.
- Thống kê OP.GG kiểm tra bằng fixture và mô phỏng dịch vụ lỗi; chưa xác minh độ sẵn sàng của provider thật trong lần chạy này.
- Kiểm thử tương tác chạy trên Chrome. Chưa xác minh Safari/Firefox, thiết bị di động thật hoặc thực hiện kiểm toán accessibility toàn diện.
- Hai script render client có cảnh báo `useLayoutEffect` của React khi render phía server; ứng dụng chạy CSR và các bài browser không ghi nhận lỗi React tương ứng.
