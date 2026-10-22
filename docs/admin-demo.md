# Admin demo

Mở `/admin/dashboard` sau khi chạy `npm run dev` (hoặc `npm-local.cmd run dev` trên máy đã cấu hình). Mười mục trong menu admin dùng dữ liệu mẫu và lưu thay đổi trong `localStorage` của trình duyệt. Không cần đăng nhập để xem bản demo.

## Các kho dữ liệu

| Khóa `localStorage` | Nội dung | Nơi dùng |
| --- | --- | --- |
| `tram.client.dbml.demo.v2` | Tài khoản, khách, ví, sản phẩm, kho, đơn món, phiên chơi và giao dịch; admin mở rộng thêm nhân viên, lịch sử kho và nhật ký thao tác | Admin và client |
| `tram.client.home.demo.v1` | Trò chơi, giải đấu, đăng ký, đội và hạng | Admin giải đấu và client |
| `net-cafe.admin.computers.v1` | Sơ đồ, loại và trạng thái 50 máy mẫu | Admin tổng quan và máy |

Các kho được giữ riêng để tương thích dữ liệu demo đã lưu trước đó. Trang chi tiết máy ưu tiên phiên đang hoạt động trong kho dùng chung; những máy không có phiên tương ứng hiển thị phiên minh họa của sơ đồ máy. Cập nhật trong một tab được thông báo cho các tab khác qua sự kiện `storage`.

Muốn bắt đầu lại, xóa ba khóa trên trong DevTools → Application → Local Storage rồi tải lại trang. Việc này xóa mọi thao tác demo đã lưu trong trình duyệt hiện tại.

## Luồng có thể thử

1. Tạo khách ở **Khách hàng**, lập yêu cầu nạp ví, cộng ví và xác nhận thu tiền. Kiểm tra số dư ở trang client.
2. Tạo sản phẩm ở **Sản phẩm**, nhập tồn ở **Kho**, đặt món ở client, rồi xác nhận hoặc xử lý yêu cầu hủy ở **Order**.
3. Xem khoản tiền mặt chờ thu ở **Hóa đơn & thanh toán**, xác nhận thu và đối chiếu ngày thu trong **Báo cáo**.
4. Tạo giải ở **Giải đấu**, mở đăng ký, thêm người chơi, đóng đăng ký và chia đội khi đủ người.
5. Tạo nhân viên ở **Nhân viên**, sửa ca làm hoặc khóa tài khoản; không thể khóa quản trị viên đang hoạt động cuối cùng.

## Quy tắc số liệu

- Doanh thu dịch vụ = tiền máy đã ghi nhận + món đã thanh toán − hoàn món. Nạp ví là tiền ứng trước, được báo riêng.
- Đơn tiền mặt được tính vào ngày xác nhận thu; hoàn tiền được trừ vào ngày hoàn. Giá món trong hóa đơn lấy theo giá lúc đặt.
- Chi phí nhập kho chỉ là số tiền nhập hàng mẫu, chưa phải giá vốn hàng đã bán. Công suất máy lấy trạng thái hiện tại của sơ đồ.
- Bộ lọc ngày dùng ngày Việt Nam và bao gồm cả hai ngày đầu, cuối. Dữ liệu xuất CSV dùng cùng phép tính với bảng báo cáo.

## Giới hạn

Đây là bản demo frontend. Vai trò, mật khẩu và nhật ký thao tác nằm trong dữ liệu trình duyệt; chưa có xác thực, phân quyền hay ghi nhận người thao tác từ server. Các route `/admin/auth` và `/admin/promotions` hiện không thuộc mười mục quản trị đã triển khai và vẫn là khung chờ. API ASP.NET Core trong repository là phần riêng, chưa cấp dữ liệu cho các trang admin. Không dùng dữ liệu demo để vận hành quán thật.

## Kiểm tra

```powershell
.\npm-local.cmd run test:admin
.\npm-local.cmd run test:client
.\npm-local.cmd run test:admin:render
.\npm-local.cmd run build
```
