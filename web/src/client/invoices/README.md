# Hóa đơn và bảng kê phiên chơi

Trang client tổng hợp dữ liệu từ `ClientDemoProvider`; không tạo thêm hóa đơn thanh toán hoặc giao dịch thu tiền.

- `getReceipts` giữ giá món tại lúc đặt, tách số đã trả bằng ví, tiền mặt đã thu, tiền mặt chờ thu và tiền hoàn. Tổng ghi nhận = tổng đã thanh toán + tiền mặt chờ thu. Món tiền mặt đã hủy bị loại khỏi số cần thu; món ví hủy chỉ trừ khỏi tổng sau khi có giao dịch hoàn tiền thực tế.
- Đơn cũ không có `payment_method` được hiểu là đã trả bằng ví. Đơn Cash có `payment_status: Paid` được hiển thị là đã thu tiền mặt. Trang này không tự đổi trạng thái thu tiền.
- Bộ lọc ngày tính theo ngày bắt đầu phiên tại `Asia/Ho_Chi_Minh`, gồm cả ngày đầu và ngày cuối. Tìm kiếm hỗ trợ mã PC/GM, máy, tên món và tiếng Việt không dấu. Bộ lọc kết hợp với trạng thái phiên/thanh toán; thống kê tính trên toàn bộ kết quả lọc, không chỉ trang đang xem. Mỗi trang 10 bảng kê.
- Chi tiết nhóm món theo đơn, hiển thị trạng thái món, khoản hủy/hoàn và giao dịch ví thuộc phiên. Số dư trong lịch sử là số dư ngay sau giao dịch đó, không phải số dư hiện tại.
- TXT có dấu tiếng Việt (UTF-8 BOM). `In / lưu PDF` dùng hộp in của trình duyệt; nội dung in riêng bảng kê được chọn, mở đầy đủ lịch sử ví và không in menu, bộ lọc hoặc nút bấm.
- Dữ liệu hiện lưu trong trình duyệt, chưa nối API/SQL Server; bảng kê không phải hóa đơn thuế. Phiên Active là tạm tính và chỉ dùng số tiền đã ghi nhận, không ước tính thêm chi phí theo đồng hồ.

Kiểm tra từ thư mục gốc: `.\npm-local.cmd run test:client` và `.\npm-local.cmd run build`.
