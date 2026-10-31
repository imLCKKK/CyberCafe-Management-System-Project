# Cập nhật tầng API

> Nghiệp vụ trong controller đã được chuyển xuống Application ở lần refactor tiếp theo. Xem [báo cáo Clean Architecture](clean-architecture-refactor.md) để biết hợp đồng service mới.

## Phạm vi

Đã cập nhật API cho các service Application hiện có, chủ yếu là Order theo từng món,
Invoice/bảng kê phiên và quyền đọc/ghi Product/Category. Giữ nguyên Infrastructure,
SQL, migration và frontend; không tạo commit.

Theo yêu cầu, đợt này **chỉ đọc claims, chưa làm đăng nhập, cấp JWT hoặc cookie**.
Không có tài khoản mẫu, mật khẩu mẫu hoặc header tự khai danh tính trong API thật.

## Endpoint Order

Các URL không phân biệt chữ hoa/thường. Tất cả endpoint yêu cầu tài khoản đã xác thực và đang hoạt động.

| HTTP | URL | Quyền / kết quả |
| --- | --- | --- |
| GET | /api/orders?status=Pending | Staff/Admin; danh sách tất cả hoặc lọc Pending/Mixed/Confirmed/Cancelled |
| GET | /api/orders/mine | Customer; CustomerId lấy từ claims |
| GET | /api/orders/customer/{customerId} | Khách xem chính mình; Staff/Admin được tra cứu khách |
| GET | /api/orders/{id} | Chi tiết đơn và món; service kiểm tra quyền sở hữu |
| GET | /api/orders/{id}/history | Lịch sử xử lý từng món |
| POST | /api/orders | Customer; tạo đơn bằng ví; trả 201 và Location của đơn |
| POST | /api/orders/{id}/items/{itemId}/confirm | Staff/Admin; xác nhận một món, thành công trả 204 |
| POST | /api/orders/{id}/items/{itemId}/cancel | Staff/Admin; hủy món Pending, hoàn ví qua service; trả 204 |
| POST | /api/orders/{id}/items/{itemId}/cancellation-requests | Customer; gửi yêu cầu hủy; trả 201 và Location đọc yêu cầu |
| GET | /api/orders/{id}/items/{itemId}/cancellation-requests/{requestId} | Đọc yêu cầu hủy thuộc đúng đơn/món và có quyền xem đơn |
| POST | /api/orders/{id}/items/{itemId}/cancellation-requests/{requestId}/reject | Staff/Admin; từ chối yêu cầu Pending, trả 204 |

Duyệt yêu cầu hủy bằng endpoint **cancel món** kèm lý do; service hủy món và duyệt yêu cầu đang chờ cùng transaction.
API không tự ghi ví hoặc tồn kho trong controller.

Hai endpoint cũ `POST /api/orders/{id}/confirm` và `POST /api/orders/{id}/cancel`
đã bị bỏ (404). API không còn gọi ConfirmOrderAsync/CancelOrderAsync toàn đơn.

Ví dụ tạo đơn:

```json
{
  "sessionId": 1,
  "items": [
    { "productId": 10, "quantity": 2 }
  ]
}
```

CustomerId có thể bỏ để lấy từ claims. Nếu client gửi CustomerId khác người đăng nhập,
Application từ chối 403. Giá, người xác nhận/hủy và số tiền không lấy từ request của client.

Ví dụ body hủy món:

```json
{ "reason": "Khách yêu cầu hủy" }
```

Lý do bắt buộc, không chỉ chứa khoảng trắng và tối đa 250 ký tự.

## Invoice, bảng kê, Product và Category

- GET /api/invoices: chỉ Staff/Admin.
- GET /api/invoices/{id}: service kiểm tra quyền sở hữu hóa đơn.
- GET /api/invoices/order/{orderId}: tra hóa đơn đã liên kết; không có thì 404.
- GET /api/invoices/session/{sessionId}/statement: bảng kê phiên, không thu thêm tiền hoặc tạo hóa đơn mới.
- Product/Category giữ nguyên các route CRUD. Các hàm đọc yêu cầu tài khoản hoạt động;
  thêm/sửa/ẩn chỉ dành cho Staff/Admin.
- ID trên route phải dương. Product search không truyền keyword sẽ lấy danh sách qua service.

## Claims và phân quyền

ICurrentUser được triển khai bằng HttpCurrentUser, đọc từ HttpContext.User.
Danh tính phải do cơ chế xác thực của server thiết lập. Chỉ gửi Authorization header hoặc
JSON có các trường bên dưới **không tự tạo được người đăng nhập**.

| Claim | Ý nghĩa |
| --- | --- |
| account_id | ID tài khoản dương, kiểu long; nếu không có thì dùng ClaimTypes.NameIdentifier hoặc sub |
| role | Vai trò Customer/Staff/Admin; phải cấu hình RoleClaimType của ClaimsIdentity phù hợp |
| is_active | Boolean true để tài khoản được truy cập API |
| customer_id | ID khách dương cho thao tác của Customer |
| employee_id | ID nhân viên dương cho service xác nhận/hủy món và tra cứu theo quyền nhân viên |
| must_change_password | Đọc nếu có; không dùng để chặn đặt món |

`HttpCurrentUser.Role` đọc theo RoleClaimType của danh tính, không bắt buộc claim thô tên role.
Ví dụ cơ chế xác thực dùng ClaimTypes.Role thì cả API và Application sẽ đọc ClaimTypes.Role.
ID/boolean thiếu hoặc không hợp lệ không được tự gán thành tài khoản có quyền.

AddAuthentication hiện chưa có scheme cấp/kiểm tra token hoặc cookie. Vì vậy API thật
chưa có cách đăng nhập thành công; endpoint được bảo vệ sẽ trả 401 cho request chưa xác thực.
Sau này cần đăng ký scheme thực và cấu hình claims tương ứng. Không dùng header giả trong production.

## Xử lý lỗi

| HTTP | Trường hợp |
| --- | --- |
| 400 | JSON/đầu vào không hợp lệ, ID không dương, validator hoặc ArgumentException |
| 401 | Chưa đăng nhập |
| 403 | Tài khoản khóa, thiếu quyền hoặc truy cập dữ liệu của khách khác |
| 404 | Không tìm thấy đơn/món/yêu cầu/hóa đơn/phiên; EntityNotFoundException |
| 409 | Vi phạm nghiệp vụ hoặc trạng thái đã thay đổi; BusinessRuleException |
| 415 | Nội dung request không có/không đúng Content-Type được hỗ trợ |
| 500 | Lỗi hệ thống, DI hoặc lỗi lập trình; chỉ trả thông báo chung và ghi log nội bộ |

Lỗi validation body dùng ValidationProblemDetails; lỗi do handler và các phản hồi Problem
trả application/problem+json kèm traceId. Route không tồn tại có thể trả 404 không có body.
BusinessRuleException kế thừa InvalidOperationException để giữ tương thích code bắt lỗi cũ,
nhưng API chỉ ánh xạ đúng loại nghiệp vụ sang 409. InvalidOperationException thông thường
vẫn là 500, tránh biến lỗi thiếu dependency thành lỗi do khách hàng.

## File đã sửa/thêm trong đợt API

| File (tính từ Backend/) | Nội dung |
| --- | --- |
| CyberManagement.API/Controller/OrdersController.cs | Route từng món, yêu cầu hủy, danh sách, lịch sử, kiểm tra ID và mã trả về |
| CyberManagement.API/Controller/InvoicesController.cs | Inject IInvoiceService; thêm bảng kê phiên và phân quyền |
| CyberManagement.API/Controller/ProductsController.cs | Phân quyền, ID dương và keyword tùy chọn |
| CyberManagement.API/Controller/CategoriesController.cs | Phân quyền và ID dương |
| CyberManagement.Application/DTOs/CancelOrderItemRequest.cs (đã chuyển) | DTO lý do hủy; quy tắc kiểm tra ở Application/Validators/CancelOrderItemRequestValidator.cs |
| CyberManagement.API/Security/HttpCurrentUser.cs (mới) | Đọc danh tính đã xác thực từ claims |
| CyberManagement.API/Security/ApiPolicies.cs (mới) | Tên policy và danh sách vai trò nhân viên |
| CyberManagement.API/Security/ApiAuthorizationResultHandler.cs (mới) | Trả JSON 401/403, không chuyển hướng đăng nhập |
| CyberManagement.API/ExceptionHandling/ApiExceptionHandler.cs (mới) | Ánh xạ lỗi, che thông tin nội bộ khi 500 |
| CyberManagement.API/ApiDependencyInjection.cs (mới) | Đăng ký HTTP context, claims, authorization và xử lý lỗi |
| CyberManagement.API/Program.cs | Bật Application services, đăng ký API và middleware đúng thứ tự |
| CyberManagement.Application/Exceptions/BusinessRuleException.cs (mới) | Loại lỗi nghiệp vụ cho HTTP 409 |
| CyberManagement.Application/Exceptions/EntityNotFoundException.cs (mới) | Loại lỗi không tìm thấy cho HTTP 404 |
| CyberManagement.Application/Services/OrderService.cs | Dùng hai loại lỗi có tên rõ ràng; giữ nguyên quy tắc xử lý |
| CyberManagement.Application/Services/InvoiceService.cs | Dùng loại lỗi nghiệp vụ có tên rõ ràng |
| tests/CyberManagement.API.Checks/ (mới) | Host kiểm thử HTTP độc lập, dùng mã API thật và service giả |
| docs/api-changes.md (mới) | Báo cáo và hợp đồng endpoint này |

## Kiểm chứng và phần còn chặn

```powershell
dotnet run --project tests/CyberManagement.API.Checks/CyberManagement.API.Checks.csproj --no-launch-profile
dotnet run --project tests/CyberManagement.Application.Checks/CyberManagement.Application.Checks.csproj --no-restore --no-launch-profile
```

- 39 kiểm tra HTTP đạt, gồm role/claims, 201 Location đọc lại được, validation body/ID,
  endpoint cũ đã bỏ, các mã 400/401/403/404/409/415/500 và không lộ chi tiết lỗi nội bộ.
- 14 kiểm thử Application đạt sau khi bổ sung các loại exception.
- Bộ HTTP biên dịch trực tiếp controllers, request, security, exception handler và đăng ký API thật;
  dùng service giả, host cổng loopback tạm thời. Không kiểm tra database/transaction thực.
- Program chính không nằm trong host kiểm thử độc lập. Build API đầy đủ vẫn bị chặn bởi
  Infrastructure/Persistance/Configuration/InvoiceConfiguration.cs: dòng 21 dùng Invoice.PaymentTime,
  dòng 24 dùng Invoice.Order, trong khi entity đã đổi. Không sửa các file đó trong đợt này.
- Infrastructure hiện chỉ đăng ký DbContext, chưa có repository cho các service. Sau khi sửa build,
  cần triển khai/đăng ký repository (đặc biệt IOrderWorkflowRepository và transaction chung) trước
  khi host chính có thể khởi động/phục vụ các luồng nghiệp vụ. Không tắt kiểm tra DI để che lỗi thiếu dependency.
- Chưa làm đăng nhập, JWT/cookie hoặc kết nối frontend với API theo phạm vi đã chốt.
