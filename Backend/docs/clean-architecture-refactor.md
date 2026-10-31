# Chuyển nghiệp vụ từ API xuống Application

## Kết quả

Controller giữ trách nhiệm HTTP: nhận route/query/body, gọi service và trả response.
Application quyết định khách hiện tại, lọc dữ liệu, quyền sở hữu, tìm món/yêu cầu hủy,
kiểm tra trạng thái và thực hiện thay đổi trong transaction.
Không sửa Domain hoặc Infrastructure trong đợt refactor này. Giữ nguyên các URL API.

## Những phần đã chuyển

| Trước đây ở API | Hiện tại ở Application |
| --- | --- |
| Controller chọn GetAllAsync hoặc GetByStatusAsync dựa trên query | OrderService.GetAllAsync(status) quyết định và kiểm tra trạng thái lọc |
| Controller đọc CustomerId từ ICurrentUser cho /mine | OrderService.GetMineAsync() lấy danh tính qua abstraction ICurrentUser và kiểm tra quyền |
| Controller bổ sung request.CustomerId khi tạo đơn | CreateOrderAsync tự lấy CustomerId hiện tại nếu request không gửi; không sửa object request của bên gọi |
| Controller gọi GetById trước khi lấy lịch sử | GetStatusHistoryAsync tự tìm đơn và kiểm tra quyền, không cần truy vấn trước ở controller |
| Controller duyệt Items/CancellationRequests để tìm yêu cầu | GetCancellationRequestAsync tìm trong đúng đơn/món và kiểm tra quyền tại Application |
| Controller kiểm tra đơn/món tồn tại trước khi xác nhận/hủy | ProcessItemAsync kiểm tra tồn tại và trạng thái ngay trong transaction |
| Controller kiểm tra yêu cầu tồn tại trước khi từ chối | RejectItemCancellationAsync kiểm tra đúng đơn/món/yêu cầu và trạng thái Pending trong transaction |
| Required/StringLength của lý do hủy nằm ở model API | DTO thuần trong Application và CancelOrderItemRequestValidator, được gọi trong service |
| Controller đổi keyword null thành chuỗi rỗng | ProductService.SearchAsync nhận string? và dùng quy tắc xử lý từ khóa rỗng sẵn có |

Đã bỏ hai hàm trợ giúp ProcessItemAsync và CheckItemExistsAsync khỏi OrdersController.
OrdersController cũng không còn inject ICurrentUser hoặc duyệt các collection của DTO.

## Phân biệt trách nhiệm theo Clean Architecture

- **API:** routing, model binding, kiểm tra hình thức ID trên URL bằng Range, Authorize ở mức HTTP,
  chuyển kết quả thành 200/201/204/404 và chuyển exception thành ProblemDetails qua handler chung.
- **Application:** ca sử dụng, validator đầu vào, quyền sở hữu dữ liệu, điều kiện trạng thái,
  lựa chọn khách hiện tại, tồn kho/ví và điều phối transaction qua các interface.
- **ICurrentUser:** interface nằm ở Application. API triển khai việc đọc HttpContext/claims,
  service chỉ nhìn thấy interface, không tham chiếu ASP.NET hoặc controller.
- **Infrastructure:** triển khai repository/transaction; giữ nguyên theo phạm vi yêu cầu.

Không chuyển IActionResult, StatusCodes, HttpContext, route hoặc handler HTTP vào Application.
Application vẫn chỉ tham chiếu Domain và dependency injection abstractions hiện có.

## Thay đổi hợp đồng service

```csharp
Task<IEnumerable<OrderDTOs>> GetAllAsync(string? status = null);
Task<IEnumerable<OrderDTOs>> GetMineAsync();
Task<OrderItemCancelRequestDTO?> GetCancellationRequestAsync(int orderId, int orderDetailId, int requestId);
Task ConfirmItemAsync(int orderId, int orderDetailId);
Task CancelItemAsync(int orderId, int orderDetailId, string reason);
Task RejectItemCancellationAsync(int orderId, int orderDetailId, int requestId);
Task<IEnumerable<ProductDTOs>> SearchAsync(string? keyword);
```

Ba lệnh xác nhận/hủy/từ chối không còn trả bool gây nhập nhằng giữa thiếu dữ liệu và sai trạng thái.
Thành công thì hoàn tất Task; không tìm thấy ném EntityNotFoundException;
trạng thái không cho phép ném BusinessRuleException. API ánh xạ tương ứng 404/409 ở handler chung.
Các bên gọi IOrderService phải dùng await, không còn kiểm tra if (!result) cho ba hàm này.
GetCancellationRequestAsync trả null khi không tìm thấy, còn lỗi quyền vẫn được ném từ service.
GetByStatusAsync được giữ cho bên gọi hiện có và dùng chung luồng lọc trong GetAllAsync.

## File thay đổi trong đợt này

| File (tính từ Backend/) | Thay đổi |
| --- | --- |
| CyberManagement.API/Controller/OrdersController.cs | Chỉ gọi service và tạo HTTP response; bỏ đọc danh tính, truy vấn trước và tìm dữ liệu trong DTO |
| CyberManagement.API/Controller/ProductsController.cs | Truyền keyword trực tiếp vào Application |
| CyberManagement.Application/Services/OrderService.cs | Nhận các nghiệp vụ chuyển xuống; phân biệt thiếu dữ liệu/sai trạng thái trong transaction |
| CyberManagement.Application/Interfaces/Services/IOrderService.cs | Bổ sung GetMine/GetCancellationRequest, query trạng thái tùy chọn; cập nhật lệnh trả Task |
| CyberManagement.Application/DTOs/CancelOrderItemRequest.cs | Di chuyển từ API/Requests; DTO thuần, bỏ DataAnnotations |
| CyberManagement.API/Requests/CancelOrderItemRequest.cs | Không còn ở API, đã chuyển sang Application/DTOs |
| CyberManagement.Application/Validators/CancelOrderItemRequestValidator.cs | Validator lý do hủy, áp dụng cho cả bên gọi service trực tiếp |
| CyberManagement.Application/Services/ProductService.cs | Nhận keyword nullable, giữ quy tắc từ khóa rỗng trả toàn bộ |
| CyberManagement.Application/Interfaces/Services/IProductService.cs | Đồng bộ kiểu nullable của keyword |
| tests/CyberManagement.Application.Checks/Program.cs | Cập nhật hợp đồng và thêm 5 kiểm thử gọi Application trực tiếp |
| tests/CyberManagement.API.Checks/Program.cs | Đồng bộ service giả theo hợp đồng mới; giữ kiểm thử các route/mã HTTP |
| tests/CyberManagement.API.Checks/CyberManagement.API.Checks.csproj | Bỏ liên kết model API/Requests đã chuyển đi |
| docs/clean-architecture-refactor.md | Báo cáo này |
| docs/api-changes.md | Cập nhật đường dẫn model và thông tin refactor mới nhất |
| docs/application-order-changes.md | Thêm liên kết đến báo cáo refactor |

## Kiểm chứng

- **19 kiểm thử Application đạt:** có kiểm tra gọi thẳng service để lấy khách hiện tại,
  tìm yêu cầu hủy đúng chủ sở hữu, phân biệt thiếu dữ liệu với xung đột trạng thái,
  kiểm tra lý do hủy và lọc trạng thái; không cần controller thực hiện trước.
- **39 kiểm tra HTTP đạt:** giữ các URL và mã phản hồi 400/401/403/404/409/415/500,
  quyền truy cập và Location cho các tài nguyên tạo mới.
- Kiểm tra HTTP dùng mã API thật cùng service giả; không thay thế kiểm thử SQL/transaction thực.
- Build toàn API vẫn bị chặn bởi hai tham chiếu Invoice.PaymentTime/Invoice.Order cũ trong
  Infrastructure/Persistance/Configuration/InvoiceConfiguration.cs đã ghi nhận từ trước.
  Infrastructure chưa có repository/transaction thực và vẫn chưa được chỉnh sửa.
- Chưa làm đăng nhập hoặc cấp JWT/cookie; giữ cách đọc claims đã thống nhất.
