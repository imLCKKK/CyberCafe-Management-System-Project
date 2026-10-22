# CyberNet — Bộ giao diện HTML cho đồ án Công nghệ .NET

Giao diện nền tối, điểm nhấn cyan, theo bản thiết kế đã trao đổi. HTML/CSS/JavaScript thuần, không cần npm, thư viện ngoài hoặc kết nối Internet để hiển thị.

## Chạy

1. Giải nén toàn bộ thư mục `cybernet-html`.
2. Mở `index.html` bằng Chrome hoặc Edge.
3. Để sửa: VS Code → File → Open Folder → chọn thư mục vừa giải nén.
4. Sửa HTML, CSS hoặc JS, lưu rồi tải lại trang. Có thể dùng Live Server nếu đã cài.

## Cấu trúc

- `index.html`: khung ứng dụng, thanh điều hướng và hộp thoại mua combo.
- `css/styles.css`: màu, bố cục, nút, bảng, thẻ máy và responsive.
- `js/app.js`: dữ liệu mẫu, chuyển màn hình, lọc máy và xử lý tương tác demo.

## Các màn hình

| Mục | Đã có trong bản UI |
| --- | --- |
| Phòng máy | 12 máy mẫu, thống kê trạng thái, tìm mã máy, lọc khu vực và trạng thái, chọn máy xem chi tiết |
| Phiên sử dụng | Danh sách phiên, thời lượng mẫu và tạm tính tiền giờ |
| Gói giờ / combo | Lọc gói theo khu vực, mở hộp thoại, xác nhận điều kiện, ghi nhận hóa đơn mua thử |
| Hội viên & ví | Bảng thông tin hội viên mẫu, số dư và hạng |
| Dịch vụ | Bảng sản phẩm, đơn giá và tồn kho mẫu |
| Hóa đơn | Hóa đơn phiên và combo đã thao tác trong demo |
| Báo cáo | Tổng tiền hóa đơn demo, số hóa đơn, tỷ lệ sử dụng và bảng khu vực |

## Thử thao tác

- Phòng máy → chọn PC-01 → thêm trà đào → thanh toán phiên → mở Hóa đơn.
- Chọn máy sẵn sàng → Mở phiên khách lẻ.
- Chọn PC-07 đang bảo trì → Hoàn tất bảo trì.
- Gói giờ / combo → Mua gói → tích xác nhận → Xác nhận mua thử.
- Tải lại trang: trạng thái máy và hóa đơn được giữ bằng localStorage khi trình duyệt cho phép.
- Xóa dữ liệu demo: mở Console và chạy `localStorage.removeItem('cybernet-ui-demo-v1')`, sau đó tải lại trang.

## Phạm vi và giới hạn

Đây là bộ frontend khởi đầu, chưa phải phần mềm quản lý hoàn chỉnh.

- Không có đăng nhập hoặc phân quyền thật. Nhãn Quản trị viên là minh họa.
- Hội viên và dịch vụ hiện là bảng xem dữ liệu, chưa có CRUD hoặc nạp ví.
- Thời lượng phiên là dữ liệu mẫu cố định; chưa chạy đồng hồ hoặc đồng bộ máy trạm.
- Thanh toán, mở phiên và mua combo chỉ sửa dữ liệu demo trong trình duyệt. Mua combo chưa gán vào hội viên hoặc máy thật.
- localStorage không thay thế SQL Server; không dùng để lưu mật khẩu, số dư hoặc giao dịch thật.
- Chưa có API, SignalR, ML.NET hay điều khiển máy trạm.
- Một số trình duyệt hạn chế localStorage khi mở file trực tiếp; khi đó dữ liệu chỉ tồn tại đến khi tải lại.

## Điểm nối backend .NET

Khi làm backend, thay dữ liệu mẫu và các thao tác trong `app.js` bằng lời gọi ASP.NET Core API theo hợp đồng nhóm thống nhất:

| Phần giao diện | Backend cần đảm nhiệm |
| --- | --- |
| Đăng nhập sẽ bổ sung | Xác thực và kiểm tra quyền ở server |
| `machines`, `grid()`, `detail()` | Đọc trạng thái máy; mở/đóng phiên; nhận cập nhật trạng thái |
| `cost()`, xử lý thanh toán | Server tính thời gian, đơn giá và tổng tiền; lưu hóa đơn theo giao dịch |
| `packs`, `purchase()` | Kiểm tra điều kiện gói, gán người mua/máy và kích hoạt gói |
| Hội viên & ví | Đọc/sửa hội viên, nạp ví và lịch sử giao dịch |
| Dịch vụ | Đọc danh mục, tạo đơn, kiểm tra và trừ tồn kho |
| Hóa đơn, báo cáo | Đọc dữ liệu đã xác nhận từ SQL Server |

Giá và tổng tiền trong JS chỉ phục vụ hiển thị demo. Server phải tính lại và xác nhận khi tích hợp. Bộ HTML này là hướng giao diện web; không đặt trực tiếp vào XAML của project WPF.

## Kiểm tra

Đã kiểm tra cú pháp JavaScript bằng Node.js. Môi trường tạo mã chưa có trình duyệt Chromium để chạy kiểm thử hiển thị và tương tác tự động; hãy thử các thao tác ở trên bằng trình duyệt của bạn.
