# Tiến độ admin — hoàn thiện bản demo 10 mục trong menu

Ngày cập nhật: 21/10/2026. Toàn bộ thay đổi đã được ghi vào các file trong workspace; chưa tạo Git commit.

## Đã thực hiện

- Nối đủ 10 mục trong menu admin: Tổng quan, Máy, Khách hàng, Order, Sản phẩm, Kho, Hóa đơn & thanh toán, Giải đấu, Nhân viên và Báo cáo.
- Giao diện dựa trên Tổng quan: dùng chung CafeUI, nền sáng, thẻ thống kê, bảng, bộ lọc, phân trang và hộp thao tác.
- Thêm dữ liệu admin dùng chung với client: khách/tài khoản, sản phẩm, tồn kho, đơn món, ví, thu tiền và nhật ký thao tác. Giữ dữ liệu client đã có; bổ sung lịch sử mẫu cho báo cáo.
- Khách hàng: tạo/sửa/khóa, xem ví, tạo yêu cầu nạp, duyệt nạp, xác nhận thu một lần.
- Order: xác nhận món, xử lý hủy, hoàn ví theo giá lúc đặt, hoàn tồn, thu tiền mặt; cập nhật phía khách hàng.
- Sản phẩm/danh mục: tạo/sửa và ngừng bán; món ngừng bán bị loại khỏi menu/vòng quay và không thể đặt thêm.
- Kho: nhập, xuất hao hụt, kiểm kê; lịch sử trước/sau; chặn tồn âm.
- Hóa đơn: xem bảng kê của mọi khách, tách ví/tiền mặt/chờ thu, tải TXT/CSV, xác nhận thu tiền mặt.
- Giải đấu: dùng cùng kho dữ liệu với trang giải đấu client hiện có; tạo/sửa, chuyển trạng thái, đăng ký khách tại quầy, chia đội theo rank và xem đội.
- Nhân viên: tạo/sửa, ca làm, vai trò, khóa/mở; bảo vệ quản trị viên hoạt động cuối cùng; mật khẩu mới lưu dạng hash.
- Tổng quan/Báo cáo: doanh thu từ giao dịch, biểu đồ đường/tròn, lọc ngày, dữ liệu biểu đồ, tương tác chú giải/điểm, CSV. Nạp ví không bị cộng vào doanh thu; tiền mặt ghi nhận theo ngày thu; hoàn tiền theo ngày hoàn.
- Dữ liệu hiện vẫn lưu trong trình duyệt. Backend quản trị/xác thực/phân quyền thực tế chưa tích hợp.

## Kiểm tra đã chạy

- Build frontend đã thành công trong quá trình triển khai.
- 16 test client đã đạt.
- Đã chạy lại toàn bộ 16 test admin và 16 test client: đều đạt.
- `test:admin:render` đã đạt sau lần chỉnh cuối: render đủ 10 mục, không có placeholder, NaN hoặc cảnh báo React ngoài cảnh báo SSR đã biết của router.
- Trình duyệt: đủ 10 trang trên desktop/mobile không tràn trang hoặc lỗi JavaScript.
- Trình duyệt: tạo khách/nhân viên, nạp ví và thu tiền một lần, nhập kho/chặn xuất quá tồn, bảo vệ admin cuối cùng, xác nhận/thu tiền món, hủy hoàn tiền và tồn, đồng bộ client, tạo/mở/đăng ký giải và tải báo cáo CSV đều đã qua kiểm tra.

## Chốt bản demo

- Build và render smoke đã chạy lại sau lần chỉnh cuối và đều đạt.
- Chi tiết máy ưu tiên phiên đang hoạt động từ dữ liệu dùng chung; máy không có phiên tương ứng vẫn dùng phiên minh họa của sơ đồ.
- Tài liệu kho dữ liệu, cách tính báo cáo, luồng thử và giới hạn backend ở [admin-demo.md](admin-demo.md).
- Hai route ngoài menu `/admin/auth` và `/admin/promotions` vẫn là khung chờ; xác thực và khuyến mãi chưa nằm trong bản demo 10 mục này.

Các script kiểm tra trình duyệt và ảnh chụp nằm trong `.tools/` (được Git bỏ qua). Không ghi đè các thay đổi sẵn có của người dùng khi tiếp tục.
