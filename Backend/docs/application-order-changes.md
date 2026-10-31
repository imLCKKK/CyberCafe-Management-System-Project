# Cập nhật Order trong Domain và Application

> Cập nhật mới nhất: [chuyển nghiệp vụ từ API xuống Application theo Clean Architecture](clean-architecture-refactor.md).

> Báo cáo dưới đây ghi nhận giai đoạn Domain/Application. API đã được cập nhật ở giai đoạn tiếp theo; xem [báo cáo API](api-changes.md). Infrastructure vẫn giữ nguyên.

## Phạm vi và kết quả

Đã cập nhật Domain/Application của thư mục chính theo luồng gọi món của frontend trong
`D:/TestCyberFrontEnd/CyberCafe-Management-System-Project`.
Không chỉnh sửa Infrastructure, API, SQL, migration hoặc frontend; không tạo commit.
Giữ nguyên các thay đổi đang có của người dùng: soft delete Category/Product, IsActive,
EmployeeId nullable, InvoiceId nullable và các trường CustomerId/SessionId/TotalAmount của Order.
`Order.cs` và `OrderDTOs.cs` đã có thay đổi trước phiên này; không sửa thêm hai file đó.

Đây là phần nghiệp vụ và hợp đồng tích hợp, **chưa phải luồng API/SQL chạy hoàn chỉnh**.
Infrastructure chưa có triển khai cho các hợp đồng mới. Không thể xác nhận đồng bộ SQL thực tế
hoặc khả năng chạy API khi chưa cập nhật mapping, DI và endpoint.

## Luồng đã viết

- Khách đã đăng nhập và tài khoản hoạt động được đặt món; không bắt buộc đổi mật khẩu ban đầu.
  CustomerId phải khớp người đăng nhập; SessionId phải thuộc khách và còn Active.
- Gộp dòng trùng ProductId; kiểm tra tổng số lượng 1–99, sản phẩm hoạt động, giá và tồn kho.
  Kiểm tra toàn bộ trước khi ghi. Giá lưu vào OrderDetail.UnitPrice.
- Đặt món: tạo Order/OrderDetail Pending, giảm tồn kho, ghi xuất kho,
  giảm WalletBalance, tăng WalletVersion và ghi OrderDebit âm trong cùng transaction.
  Chưa tạo Invoice/Payment, chưa thu tiền lần nữa.
- Nhân viên/Admin xác nhận từng món Pending: chuyển Confirmed, lưu tài khoản/thời gian,
  từ chối yêu cầu hủy đang chờ. Không trừ kho hay ví lần hai.
- Khách chỉ gửi yêu cầu hủy món Pending của chính mình; không tự hoàn tiền.
  Mỗi món chỉ có một yêu cầu Pending. Không bắt buộc phiên còn Active khi gửi yêu cầu.
- Nhân viên/Admin hủy toàn bộ dòng Pending: phải có OrderDebit đúng chủ sở hữu và đúng số tiền,
  chưa có OrderItemRefund. Hoàn quantity × UnitPrice đã lưu; khôi phục tồn kho,
  ghi Return và OrderItemRefund dương; duyệt yêu cầu hủy Pending.
  Món Confirmed/Cancelled không thể hủy lại. Cho phép xử lý đơn sau khi phiên đã kết thúc.
  Lý do hủy 1–250 ký tự theo luồng admin hiện có.
- Từ chối yêu cầu hủy riêng không làm thay đổi món, tồn kho hoặc tiền.
- Trạng thái toàn Order suy ra từ món: Pending, Mixed, Confirmed, Cancelled.
  Confirmed gồm trường hợp Confirmed + Cancelled nhưng không còn Pending, giống frontend.
  TotalAmount giữ tổng lúc đặt; số hoàn và số thực chi lấy từ ledger.
- Mỗi thao tác xác nhận/hủy món có OrderStatusHistory với OrderDetailId và EmployeeId hợp lệ;
  không gán người xử lý bằng 0.
- Bảng kê phiên lấy GamingCharge/OrderDebit/OrderItemRefund thực tế và các đơn thuộc phiên;
  không tự kết thúc phiên, không tạo hóa đơn thanh toán, không thu tiền thêm.
  Phiên Active là bảng kê tạm tính; Ended là dữ liệu đã ghi nhận sau khi kết thúc.
- Sửa InvoiceService đang dùng các thuộc tính không còn tồn tại: map InvoiceDate,
  SubTotal, Discount, Customer/Session/Employee/Promotion, Status và InvoiceDetails.
  Tra hóa đơn của đơn qua Order.InvoiceId thay vì gọi hàm repository chưa tồn tại.
- Category/Product trả đúng IsActive trên các hàm đọc, giữ soft delete của người dùng.

## Danh sách file sửa/thêm

Đường dẫn dưới đây tính từ `Backend/`.

| File | Thay đổi |
| --- | --- |
| CyberManagement.Application/Services/OrderService.cs | Viết luồng từng món, ví, tồn kho, yêu cầu hủy, lịch sử; thêm đọc danh sách/lọc trạng thái/phân quyền |
| CyberManagement.Application/Services/InvoiceService.cs | Sửa mapping và tra cứu invoice; thêm bảng kê phiên, kiểm tra quyền |
| CyberManagement.Application/Services/CategoryService.cs | Bổ sung IsActive ở GetAll/GetById |
| CyberManagement.Application/Services/ProductService.cs | Bổ sung IsActive ở GetAll/GetById/Search |
| CyberManagement.Application/Interfaces/Services/IOrderService.cs | Các hàm từng món, yêu cầu hủy, danh sách và lịch sử |
| CyberManagement.Application/Interfaces/Services/IInvoiceService.cs | GetSessionStatementAsync |
| CyberManagement.Application/Interfaces/ICurrentUser.cs (mới) | Danh tính lấy từ server đã xác thực, bao gồm AccountId/CustomerId/EmployeeId/Role |
| CyberManagement.Application/Interfaces/Repositories/IOrderWorkflowRepository.cs (mới) | Transaction, đọc có khóa, khách/phiên, ledger và yêu cầu hủy |
| CyberManagement.Application/Interfaces/Repositories/IOrderRepository.cs | Ghi rõ Include cần thiết và yêu cầu trả ID đã sinh; giữ nguyên chữ ký hàm |
| CyberManagement.Application/Interfaces/Repositories/IInvoiceRepository.cs | Ghi rõ Include Payments/InvoiceDetails; giữ nguyên chữ ký hàm |
| CyberManagement.Application/DTOs/OrderDetailsDTOs.cs | ID món, trạng thái, người/thời gian xác nhận/hủy, lý do và yêu cầu hủy |
| CyberManagement.Application/DTOs/OrderItemCancelRequestDTO.cs (mới) | Dữ liệu yêu cầu hủy |
| CyberManagement.Application/DTOs/OrderStatusHistoryDTOs.cs | Thêm OrderDetailId |
| CyberManagement.Application/DTOs/SessionStatementDTO.cs (mới) | Đơn, tiền chơi thực thu, tiền món trừ/hoàn, tổng ghi nhận |
| CyberManagement.Application/DTOs/InvoiceDTOs.cs | EmployeeId/InvoiceDate nullable theo entity |
| CyberManagement.Domain/Entities/OrderDetail.cs | Pending/Confirmed/Cancelled, người/thời gian xử lý, lý do, collection yêu cầu hủy |
| CyberManagement.Domain/Entities/OrderItemCancelRequest.cs (mới) | Yêu cầu hủy và người/thời gian xử lý |
| CyberManagement.Domain/Entities/OrderStatusHistory.cs | Thêm OrderDetailId, khởi tạo chuỗi trạng thái |
| CyberManagement.Domain/Entities/Customers.cs | WalletVersion |
| CyberManagement.Domain/Entities/WalletTransactions.cs | Nguồn OrderId/OrderDetailId/SessionId và RecordedByAccountId |
| CyberManagement.Domain/Entities/InventoryTransaction.cs | EmployeeId nullable cho thao tác khách, tài khoản thực hiện, Before/After |
| CyberManagement.Domain/Entities/Category.cs | Bỏ dấu ; thừa sau property IsActive để build |
| CyberManagement.Domain/Entities/Product.cs | Bỏ dấu ; thừa sau property IsActive để build |
| CyberManagement.Domain/Entities/Payment.cs | Bỏ dấu ; thừa sau property WalletTransaction để build |
| tests/CyberManagement.Application.Checks/CyberManagement.Application.Checks.csproj (mới) | Chương trình kiểm thử không thêm package test bên ngoài |
| tests/CyberManagement.Application.Checks/Program.cs (mới) | Kiểm thử các luồng thành công, phân quyền, hủy/hoàn lặp, rollback và bảng kê |
| docs/application-order-changes.md (mới) | Báo cáo này |

## Tích hợp cần làm ở đợt sau (chưa sửa)

1. Triển khai ICurrentUser từ phiên đăng nhập/claims đã xác thực. Không bind Role,
   AccountId, EmployeeId từ JSON do client gửi. Role hợp lệ: Customer/Staff/Admin.
   EmployeeId dùng cho bảng lịch sử nhân viên cũ; AccountId dùng cho trường xử lý theo database mới.
   Cần ánh xạ nhân viên từ account vì Domain hiện tại chưa có bảng Account thống nhất.
2. Triển khai và đăng ký IOrderWorkflowRepository cùng các repository đang được inject.
   OrderService cần IOrderRepository, IProductRepository, IOrderStatusHistoryRepository,
   IInventoryTransactionRepository, IOrderWorkflowRepository, ICurrentUser.
   InvoiceService cần IInvoiceRepository, IOrderRepository, IOrderWorkflowRepository,
   IOrderService, ICurrentUser. Các đăng ký service hiện có vẫn giữ nguyên.
3. ExecuteAsync phải dùng một transaction/DbContext cho tất cả repository, rollback cả khi
   lỗi ledger, kho, lịch sử hoặc commit. AddAsync phải trả OrderId/OrderDetailIds thực trước khi
   ghi ledger. Không dùng implementation giả không có transaction trong môi trường thực.
4. Khóa nhất quán customer -> session/order -> products (ProductId tăng dần), hoặc concurrency
   token có retry toàn bộ transaction. Luồng hủy và xác nhận cùng khóa customer/order.
   Các luồng nạp tiền/tính tiền phiên cũng phải dùng cùng cơ chế tránh mất cập nhật ví.
   Query locate/GetSessionAsync chỉ đọc, không giữ lock gây đảo thứ tự khóa.
5. Ràng buộc database: số dư không âm, lượng món dương, giá không âm; unique có điều kiện
   cho OrderDebit theo OrderId, OrderItemRefund theo OrderDetailId và một yêu cầu hủy Pending/món.
   Nếu repository dùng optimistic concurrency, WalletVersion phải được cấu hình concurrency token.
6. Include OrderDetails.Product và CancellationRequests khi đọc Order; Include Payments và
   InvoiceDetails khi đọc Invoice. Wallet ledger của phiên phải lấy GamingCharge qua SessionId,
   OrderDebit qua Order.SessionId, refund qua OrderDetail.Order.SessionId.
7. API cũ vẫn gọi ConfirmOrderAsync/CancelOrderAsync toàn đơn. Hai hàm giữ chữ ký để mã gọi
   còn compile, đánh dấu Obsolete và ném NotSupportedException; cần thay endpoint để gọi
   ConfirmItemAsync/CancelItemAsync với orderId + orderDetailId, cùng endpoint gửi/từ chối yêu cầu hủy.
   Endpoint mới cần ánh xạ lỗi xác thực/nghiệp vụ thành HTTP phù hợp. Không gọi hàm cũ trong production.
8. Schema phụ dùng orders/order_items/order_item_cancel_requests và ID bigint, còn Domain chính
   giữ tên Order/OrderDetail và ID int để tránh phá toàn bộ model hiện có. Cần mapping rõ ràng,
   hoặc migration đổi kiểu ID khi triển khai SQL; hiện chưa đồng bộ vật lý hai schema.
   OrderDetailId tương ứng order_item_id; RequestId tương ứng id; UnitPrice tương ứng unit_price_snapshot;
   OrderTime tương ứng placed_at; SessionId tương ứng gaming_session_id.
   SQL gốc chưa có các mở rộng kho/soft delete đang tồn tại trong frontend demo; cần migration tương ứng.
   InventoryTransaction.Quantity dùng số có dấu: Out âm, Return dương; ReferenceId là OrderId.
9. Dữ liệu đơn cũ đã xác nhận theo luồng thu tiền cũ không được tự backfill trạng thái món Pending
   rồi chạy luồng mới. Cần chuyển trạng thái và đối chiếu ledger thực tế trước; không tạo refund
   khi chưa có OrderDebit. Chưa có migration/backfill trong đợt này.
10. Đợt này triển khai đặt bằng Wallet theo lựa chọn đã xác nhận. Nhánh Cash trong admin demo
    chưa được đưa vào Application; không coi Cash là đã trả bằng ví.

## Kiểm tra

```powershell
dotnet build CyberManagement.Application/CyberManagement.Application.csproj --no-restore
dotnet run --project tests/CyberManagement.Application.Checks/CyberManagement.Application.Checks.csproj --no-launch-profile
```

Kiểm thử dùng transaction model trong bộ nhớ, bao gồm hoàn tiền lặp và lỗi ghi dữ liệu có rollback.
Không chứng minh được transaction/concurrency thực tế trên SQL Server hoặc tích hợp HTTP/frontend.
Cảnh báo cũ còn lại: Promotions.DiscountType chưa khởi tạo. Môi trường không truy cập được
NuGet vulnerability feed (NU1900); không thêm dependency mới để thực hiện các kiểm thử này.

Kết quả cuối: build Domain/Application thành công; 14/14 kiểm thử nghiệp vụ đạt; git diff --check không có lỗi whitespace. Không có diff tại Infrastructure/API.
