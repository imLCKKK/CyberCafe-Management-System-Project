# Client Home

Trang chủ tại `/client/home`, thiết kế theo giao diện tối TRAM và DBML được cung cấp. Luồng gọi món chuyển sang `/client/ordering` và được nối tới `OrderingPage` trong router.

## Chạy và kiểm tra

```powershell
npm.cmd run dev
npm.cmd run build
node --test --test-isolation=none web/src/client/home/homeModel.test.js
node web/src/client/home/render-smoke.mjs
```

Mở `/client/home`. Bấm dấu `+` trên thẻ món để chuyển sang Gọi món với món đã chọn. “Gọi món tại máy” và “Xem thực đơn” cũng chuyển sang trang này. Có thể xem chi tiết hoạt động, nạp ví mô phỏng; nút giải đấu chuyển sang `/client/tournaments?tournament=<id>` để đăng ký và xem bảng thống kê. Nút “Khôi phục bản mẫu” xóa dữ liệu demo dùng chung với trang giải đấu sau xác nhận trong giao diện.

## Truyền món sang Gọi món

Home dùng React Router navigation state: `{ openCart: true, initialCartItems: [{ ProductId, ProductName, Quantity: 1 }] }`. `OrderingPage` nhận state bằng `useLocation()` và hiện thông báo món đã chọn. Chưa triển khai giao diện giỏ hàng theo yêu cầu; khi làm giỏ hàng, dùng `openCart` để mở ngay và nhận `initialCartItems` một lần. Không cộng lại các món này mỗi lần render hoặc tải lại. `ProductName` chỉ dùng hiển thị; giá và tồn kho phải lấy theo `ProductId`. Bấm `+` chưa tạo đơn, trừ ví hoặc giảm tồn kho.

## Phạm vi dữ liệu

Module Home chưa có backend nghiệp vụ hoặc cấu hình SQL Server. Backend OP.GG mới chỉ phục vụ tra cứu thống kê ở trang giải đấu. Home là bản mẫu có tương tác, **không kết nối database thật và không xử lý thanh toán thật**. Dữ liệu được lưu tại khóa `tram.client.home.demo.v1` của localStorage. Trang cập nhật khi tab khác thay đổi dữ liệu, kiểm tra dữ liệu hỏng và báo lỗi nếu trình duyệt không cho lưu. Kiểm tra tab khác là bảo vệ cơ bản của bản mẫu, không thay thế transaction/locking của database.

- Khách hàng: `Customers`, giới hạn truy vấn theo `CURRENT_CUSTOMER_ID` trong bản mẫu.
- Máy/phiên chơi: `GamingSessions` → `Computers` → `ComputerRooms`, `ComputerTypes`; `SessionCharges` cho các khoảng đã ghi nhận.
- Thực đơn: `Products` → `Categories`, chỉ hiện sản phẩm và danh mục đang hoạt động.
- Logic đặt món mẫu trong `homeModel.js` vẫn được giữ để tham khảo: tạo `Orders`, `OrderDetails`, `OrderStatusHistory`, giảm `Products.Stock` và ghi `InventoryTransactions`. Hiện chưa gọi logic này từ giao diện vì luồng đã chuyển sang trang Gọi món đang chờ triển khai.
- Ví: `Customers.WalletBalance` và `WalletTransactions.BalanceAfter` được cập nhật cùng nhau. Nạp demo không tạo `Payments` vì bảng đó liên kết hóa đơn.
- Lịch sử: tổng hợp `Orders`, `WalletTransactions`, `Invoices`, sắp xếp theo thời gian. Chi tiết hóa đơn lấy `InvoiceDetails` và `Payments`.
- Giải đấu: `Tournaments` → `Games`; hạng từ `GameRanks` cùng game. Đăng ký vào `TournamentRegistrations`; không tự tạo `SkillScore`, đội hoặc thành viên. Nếu đã xếp đội, đọc `Teams` và `TeamMembers` để hiển thị. Giải có phí được hướng dẫn liên hệ quầy vì schema chưa có quan hệ thanh toán lệ phí giải.

## Quy ước của bản mẫu

Đăng ký giải nhận `RiotId` (Tên#TAG) thay cho tự nhập tỉ lệ thắng và giờ chơi. `RiotId` là trường mở rộng của bản mẫu, chưa có trong DBML ban đầu; khi làm backend cần thêm trường này vào `TournamentRegistrations` hoặc bảng hồ sơ game của khách hàng. `WinRate`, `HoursPlayed` và `SkillScore` trong bản ghi đăng ký giữ `null`, không giả lập kết quả. Trang giải đấu hiện đồng bộ thống kê qua backend đọc hồ sơ OP.GG công khai; snapshot hiển thị riêng, không ghi vào bản ghi demo. Xem `../tournaments/README.md` để biết cách chạy và giới hạn nguồn.

DBML chưa quy định tất cả giá trị trạng thái; module dùng `Active` cho khách hàng/phiên, `Occupied` cho máy, `Pending / Preparing / Ready / Delivered / Completed / Cancelled` cho đơn, `Paid / Unpaid` cho hóa đơn. Trạng thái giải và đăng ký theo ghi chú DBML. Các enum này phải được thống nhất với backend.

`GamingSessions.TotalCost` là tiền máy đã ghi nhận tới `LastChargedAt`. Tiền máy tạm tính cộng thêm thời gian chưa ghi nhận × đơn giá hiện tại. Đồng hồ chỉ hiển thị, không ghi phí và không trừ ví. Bản mẫu dùng cách thanh toán cuối phiên; ước tính thời gian còn lại trừ toàn bộ tiền máy tạm tính và món chưa thanh toán khỏi số dư. Khi backend áp dụng trừ ví định kỳ, phải dùng công nợ còn lại từ server để tránh trừ hai lần. Không cộng `SessionCharges` lần nữa vào `TotalCost`.

Các bản ghi do khách hàng tạo để `EmployeeId`/`ChangedBy` là null, không giả mạo nhân viên. Việc cho phép null cần được thống nhất khi tạo SQL schema. Nếu bắt buộc NOT NULL, backend phải cung cấp cơ chế xác nhận tại quầy thích hợp.

## Khi kết nối backend

Thay nguồn dữ liệu và mutation trong `useHomeData.js` bằng API. Lấy CustomerId từ phiên đăng nhập trên server, không tin ID hoặc số dư gửi từ client. Server phải kiểm tra quyền, cửa sổ đăng ký, game/rank, trạng thái khách hàng, tồn kho và giá; ghi đơn/tồn kho và ví/ledger trong transaction; bảo vệ yêu cầu trùng. Đọc đúng quan hệ hóa đơn/thanh toán khi tính công nợ. Không đưa PasswordHash vào payload.

Luồng home và dữ liệu demo độc lập với dữ liệu mẫu admin. Sidebar, hỗ trợ và chuông thông báo vẫn thuộc `ClientLayout` dùng chung. Ví và chi tiết hoạt động mở hộp thoại ngay trong trang. Giải đấu chuyển sang `client/tournaments`; Gọi món chuyển sang `client/ordering`.

## Tournament navigation update

Home tournament actions now navigate to `/client/tournaments?tournament=<id>`. Registration is handled on that page using the same localStorage data; the old Home registration dialog was removed. See `../tournaments/README.md` for the stats API contract and current integration limits.
