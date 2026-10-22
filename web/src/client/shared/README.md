# Client demo theo DBML

Trang chủ, Gọi món, Ví, Hóa đơn và Tài khoản dùng chung `ClientDemoProvider`.
Mock data ở `demoData.js` dùng tên bảng/cột theo `docs/client-demo-schema.dbml`.
Các ảnh minh họa món được tách khỏi dữ liệu bảng trong `productArt`.

- Dữ liệu lưu trong localStorage `tram.client.dbml.demo.v2`; khôi phục bản mẫu từ thanh demo.
- Gọi món từ giỏ cần phiên Active và ví đủ tiền. Chụp giá món vào order_items, ghi OrderDebit và cập nhật ví cùng một lần lưu. `stock_quantity` trong dữ liệu demo giảm cùng lần lưu và chặn đặt vượt tồn kho.
- Vòng quay “Hôm nay ăn gì?” chọn theo số phần còn lại, rồi mở hộp kết quả và thanh toán. Tiền mặt tạo đơn có `payment_method: Cash`, `payment_status: PendingCollection`, giữ nguyên ví; cần phiên Active nhưng không đòi số dư ví. Hai trường này là phần mở rộng của demo trình duyệt, chưa có trong schema/API. Đơn cũ không có trường thanh toán được hiểu là đã trả bằng ví.
- Chuyển khoản trong hộp kết quả dùng giao diện ZaloPay giống trang Ví. Chưa tích hợp tạo QR/nhận thông báo thanh toán, nên nút tạo QR bị vô hiệu và không tạo đơn hoặc giao dịch giả.
- Khách chỉ gửi yêu cầu hủy dòng Pending. Yêu cầu chưa được xử lý không đổi trạng thái món hoặc hoàn ví. Món Confirmed không thể yêu cầu hủy.
- Yêu cầu nạp không tự cộng ví. Hộp “Mô phỏng tại quầy” cho thử Pending → CreditedUnpaid → Collected, hoặc từ chối; cộng ví đúng một lần. Thu tiền không cộng ví thêm lần nữa.
- Hóa đơn là bảng kê từ gaming_sessions, orders, order_items và giao dịch ví. Tách đã trả bằng ví, đã thu tiền mặt, tiền mặt chờ thu và hoàn tiền; có bộ lọc ngày/thanh toán, tìm món/mã đơn, phân trang, TXT và in/lưu PDF. Xem `../invoices/README.md` để biết cách đối chiếu tổng và xử lý món hủy. DBML không có bảng invoices.
- Hồ sơ chỉ sửa display_name. Phone là tên đăng nhập cố định. Đổi mật khẩu demo kiểm tra mật khẩu hiện tại rồi lưu PBKDF2 với salt mới; không lưu mật khẩu gốc. Mật khẩu mẫu: `Tram@2026`.
- Đây là mô phỏng trình duyệt, chưa nối SQL Server hay API tài khoản. Tiền máy là số đã ghi nhận trong mock, không có tiến trình tự trừ tiền theo thời gian thực.
- Giải đấu/OP.GG của Phuc vẫn dùng module và khóa localStorage cũ riêng; chưa chuyển nghiệp vụ giải đấu sang DBML mới.

Chạy từ thư mục gốc: `.\start.cmd`.

Kiểm tra: `.\npm-local.cmd run test:client`, `.\npm-local.cmd run build`.
