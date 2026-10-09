# Sản phẩm, đơn hàng và gọi món

## Chạy giao diện

```powershell
npm.cmd ci
npm.cmd run dev
```

- `/admin/products`: tìm kiếm không dấu, lọc danh mục/trạng thái, sắp xếp, phân trang; thêm/sửa sản phẩm, bật/tắt kinh doanh.
- `/admin/orders`: thống kê, lọc thời gian/trạng thái, tìm mã đơn/khách/máy, xem chi tiết và lịch sử, nhận đơn, chuẩn bị, giao và hủy đơn.
- `/client/ordering`: thực đơn, tìm/lọc/sắp xếp, giỏ hàng, điều chỉnh số lượng, đặt món tại máy và theo dõi đơn của khách hiện tại.

### Mở hòm “Ăn gì cũm được”

Nút vàng ánh kim nổi ở góc dưới bên trái phần nội dung, sau sidebar, cách đáy 16px (mobile: 12px, có chừa vùng an toàn thiết bị). Bấm nút chỉ mở bảng cấu hình, chưa quay. Khách chọn ngân sách tối đa (20k / 35k / 50k / 75k / không giới hạn) và loại Tất cả / Mì / Cơm / Nước / Combo, xem danh sách có thể trúng, rồi bấm **Mở hòm**. Các lựa chọn được khóa trong lúc quay.

Mì/cơm được nhận diện từ tên sản phẩm; nước từ danh mục đồ uống. Combo ghép 1 món mì/cơm với 1 nước đang bán, giá bằng tổng hai món. Combo chỉ tồn tại trong tính năng chọn món; khi trúng sẽ thêm hai dòng sản phẩm thật vào giỏ, không tạo sản phẩm giả trong DB. Ngân sách áp dụng cho tổng giá combo. Mỗi lựa chọn hợp lệ có xác suất bằng nhau; bộ lọc tìm kiếm thực đơn không ảnh hưởng tới hòm.

Bốn hạng hiển thị theo giá món hoặc combo: **Thường** xanh lá (≤20k), **Vjp** tím (>20k–30k), **Đỉnh cao** đỏ (>30k–45k), **Thượng hạng** vàng (>45k). Đây là quy ước hiển thị frontend, không phải cột dữ liệu DB hay tỷ lệ trúng khác nhau.

Dải món giảm tốc trong khoảng 5,8 giây rồi dừng tại vạch giữa. Khi kết thúc sẽ kiểm tra lại tồn kho, số lượng trong giỏ, trạng thái kinh doanh và giá; nếu món/combo thay đổi sẽ không thêm. Mỗi lượt thêm một phần của từng món trong kết quả, không tự gửi đơn. Đóng giữa lượt hủy quay; **Chọn lại hòm** trở về cấu hình mà không tự quay. Chế độ giảm chuyển động bỏ qua cuộn dài. Logic ở `client/ordering/foodCase.js`, giao diện ở `components/FoodCase.jsx` và `food-case.css`.

## Dữ liệu và quy tắc

Đây là frontend demo, chưa kết nối API/SQL Server hoặc xác thực/phân quyền. `shared/commerce/store.js` lưu dữ liệu tại khóa localStorage `cybercafe.commerce.v1`, thông báo thay đổi giữa các trang và các tab cùng origin. Dữ liệu không chia sẻ giữa các trình duyệt/máy và không thay thế giao dịch đồng thời phía server. Xóa khóa này rồi tải lại để khởi tạo dữ liệu mẫu.

`mockData.js` nằm trong từng module; logic nghiệp vụ thuần nằm tại `shared/commerce/model.js`. Trường dữ liệu bám schema PascalCase:

- `Categories`, `Products`: giá theo VNĐ nguyên; tồn kho không âm. Ngừng kinh doanh giữ lại tham chiếu sản phẩm trong đơn cũ. Emoji/mô tả món là metadata giao diện riêng, không thêm cột vào DB.
- `Orders`, `OrderDetails`: liên kết khách/phiên chơi, chụp `UnitPrice`, tự tính `TotalAmount`, kiểm tra món đang bán và tồn kho trước khi đặt. Tồn kho mẫu là số lượng còn có thể bán sau các đơn có sẵn.
- `GamingSessions` → `Computers`: xác định máy nhận món. Khách mẫu là CustomerId=1, SessionId=1 (Máy 12), khai báo ở `client/ordering/mockData.js`.
- `OrderStatusHistory`: lưu từng lần chuyển trạng thái. Quy ước frontend: `Pending → Preparing → Ready → Delivered`; cho hủy từ `Pending` hoặc `Preparing`.
- `InventoryTransactions`: `Sale` với số lượng âm khi đặt, `Adjust` với số lượng dương khi hoàn do hủy; sửa tồn kho ghi phần chênh lệch.
- Đơn mới chưa có `InvoiceId`. Giao món không tự đánh dấu đã thanh toán và không trừ ví. Tích hợp hóa đơn/thanh toán thuộc module khác.

Nhân viên demo có ID=1. Khi tích hợp API, thay ID cố định bằng phiên đăng nhập, thống nhất enum trạng thái với backend, lấy thời gian từ server, kiểm tra quyền và thực hiện đặt/hủy đơn cùng tồn kho trong một transaction. Dữ liệu localStorage không đáng tin để thực hiện giao dịch thật.

## Kiểm tra

```powershell
node --test web/src/shared/commerce/model.test.js
npm.cmd run build
```

Kiểm thử luồng giao diện: thêm sản phẩm ở admin → tìm món ở client → đặt đơn → mở admin/orders → chuyển trạng thái → kiểm tra Đơn của tôi. Thử thêm đơn rồi hủy để kiểm tra hoàn tồn kho; thay giá sản phẩm để kiểm tra đơn cũ vẫn giữ đơn giá đã đặt.

Chỉ nối ba trang vào `app/routes.jsx`; không thay theme hoặc layout chung. CSS mới dùng tiền tố `cc-`; điều chỉnh sidebar mobile chỉ áp dụng khi trang gọi món đang mở.
