# SCRUM-12 - Contract kho, hóa đơn và voucher

- Task: Define-Inventory-Invoice-Voucher-Contracts; mã kế hoạch S1-T-1.
- Owner quyết định contract kho/chứng từ/voucher: Lê Vũ Huy Trường.
- Trạng thái: contract do owner chốt để bàn giao triển khai; chưa có quyết định duyệt của Trưởng nhóm.
- Nguồn code: branch Truong.
- Backend tích hợp: backend/CyberCafe.Api; không merge nguyên backend/migration CyberManagement vào đây.
- Phụ thuộc: S1-D-1 (định danh/quyền) và S1-M-1 (ví/ledger/thời điểm thu tiền).
  Chưa xác minh hai task này đã hoàn thành. Tài liệu này không tạo/cập nhật issue Jira.

## 1. Phạm vi và nguồn đối chiếu

Tài liệu chốt schema logic, quy tắc, HTTP contract, điểm gọi nội bộ và owner migration.
Không thay entity/configuration/migration, không triển khai service hoặc đổi dữ liệu trong SCRUM-12.

Nguồn trong branch SCRUM-12:

- [Backlog và tiêu chí](../../codex/SPRINT_TASKS.md).
- [Kế hoạch và owner](../../planning/team-delivery-plan.md).
- [Quy trình nhóm](../../planning/team-workflow.md).
- [DBML ban đầu](../../backend/database/schema.dbml).
- [Backend chung](../../backend/README.md).

Nguồn trên Truong (đường dẫn tương đối root tại commit ef318bd):

- Backend/CyberManagement.Domain/Entities: InventoryTransaction, Invoice, InvoiceDetail,
  Promotions, PromotionUsage, Order, OrderDetail.
- Backend/CyberManagement.Infrastructure/Persistance/Configuration: cấu hình tương ứng.
- Backend/CyberManagement.API/Controller: InventoryController, InvoicesController.
- Backend/CyberManagement.Application/DTOs: InventoryDTOs, InvoiceResponse, ReportDTOs.
- Migration tham khảo: 20261029132438_AlignIdsAndInventorySchema và
  20261102025517_AddVoucherAndInvoiceContracts.

Đọc code nguồn mà không checkout hoặc merge:

```powershell
git show Truong:Backend/CyberManagement.Domain/Entities/Invoice.cs
git show Truong:Backend/CyberManagement.Domain/Entities/Promotions.cs
```

| Phần    | Bằng chứng trên Truong                                      | Còn cần triển khai/bàn giao                                      |
| ------- | ----------------------------------------------------------- | ---------------------------------------------------------------- |
| Kho     | Entity/config, repository, transaction, service, 4 API      | Port chọn lọc, điểm gọi Sale/Return/giữ chỗ, kiểm tra tích hợp   |
| Hóa đơn | Entity/config, repository/service đọc, 2 API danh sách      | Phát hành/cập nhật/chi tiết, snapshot đầy đủ, chống lặp, in/xuất |
| Voucher | Promotions, PromotionUsage, snapshot đơn/món, configuration | DTO/API/rule/CRUD, giữ/hoàn lượt, khóa đồng thời                 |
| Báo cáo | DTO                                                         | Truy vấn, nguồn tiền thật và đối soát                            |

Test Swagger/SQL trên backend riêng do Trường báo đạt không chứng minh backend chung đã tích hợp.
Migrations là lịch sử schema; DBML/SQL xuất là tài liệu đối chiếu, không cập nhật độc lập.

## 2. Quy ước chung

- ID là long/bigint. AccountId khác CustomerId; không dùng CustomerId thay AccountId.
- Vai trò: Admin, Staff, Customer. A/S nghĩa là Admin hoặc Staff.
- Danh tính lấy từ xác thực server; không tin role, người thao tác, giá, tổng tiền từ client.
- JSON camelCase, enum chuỗi, tiền VND dạng số. Tiền lưu decimal(18,0);
  tỷ lệ voucher lưu decimal(18,2). Không dùng float/double để tính tiền.
- Thời điểm mới dùng DateTimeOffset/datetimeoffset. Giờ nghiệp vụ Việt Nam +07:00;
  gửi ISO 8601 có offset. Không tự coi datetime cũ là UTC nếu chưa đối chiếu nguồn.
- Auth/cookie/CSRF theo cơ chế chung của Đại; DevAuth chỉ phục vụ test local, không là auth bàn giao.
- Lệnh ghi dùng transaction/DbContext chung. Module con không tự commit hoặc mở transaction lồng.
  Thất bại hoàn tác ví/ledger, đơn, tồn, lượt voucher và chứng từ.
- SignalR sau commit, nhóm nhận theo quyền; reconnect đọc lại API. Event không tự cộng/trừ tiền.

## 3. Schema logic và mapping

Các tên PascalCase dưới đây là tên có thật trên Truong. Cột/tên entity đích phải theo
quy ước backend chung khi port; đây không phải yêu cầu giữ hai bộ bảng song song.

| Nguồn Truong                                 | Backend chung/DBML ban đầu                                   | Quyết định tích hợp và owner                                                                     |
| -------------------------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------ |
| Customers.CustomerId                         | customers.id / Customer.Id                                   | Dùng Customer.Id của DB đích; Minh/Đại cung cấp mapping dữ liệu cũ, không ghép ID theo số trùng  |
| Employees.EmployeeId, RecordedByAccountId    | accounts.id, role Staff/Admin                                | Dùng Account.Id của DB đích làm actor; Đại cung cấp danh tính, không thêm Employees để giữ FK cũ |
| Category.CategoryId, CategoryName            | product_categories.id, name                                  | Khang; giữ ID danh mục của DB đích                                                               |
| Product.ProductId, Price, Stock, IsActive    | products.id, current_price; thiếu tồn/trạng thái bán         | Khang phụ trách entity sản phẩm; Trường chốt invariant tồn/trạng thái                            |
| Order.OrderId, SessionId, OrderTime          | orders.id, gaming_session_id, placed_at                      | Khang; bổ sung phương thức/trạng thái thu, thời điểm/số tiền thu                                 |
| OrderDetail.OrderDetailId, UnitPrice         | order_items.id, unit_price_snapshot                          | Khang; thêm giảm phân bổ, snapshot tên món phục vụ chứng từ                                      |
| InventoryTransaction                         | Chưa có bảng tương ứng                                       | Trường bổ sung biến động kho, FK products/order_items/accounts                                   |
| Invoice, InvoiceDetail                       | Chưa có bảng tương ứng                                       | Trường bổ sung chứng từ/chi tiết snapshot                                                        |
| Promotions, PromotionUsage                   | Chưa có bảng tương ứng                                       | Trường bổ sung voucher/lượt dùng; không tạo thêm bảng Voucher trùng Promotions                   |
| WalletTransactions.CreatedAt, SessionId      | wallet_transactions.recorded_at, gaming_session_id           | Minh xác nhận type, dấu tiền, nguồn và chống xử lý lặp                                           |
| GamingSessions.TotalCost, StartTime, EndTime | charged_amount, started_at, ended_at                         | Phúc xác nhận giá giờ snapshot và tổng đã ghi nhận                                               |
| Top-up                                       | top_ups đã có ở backend chung, chưa có trong CyberManagement | Minh sở hữu; báo cáo đọc nguồn này, không tạo bảng nạp tiền riêng                                |

DBML cũ ghi không quản lý tồn, chỉ trả ví, chưa có voucher/hóa đơn.
Đây là khác biệt cần cập nhật theo kế hoạch mới, không giữ ghi chú cũ rồi bỏ yêu cầu tồn/voucher/cash.
Nguồn danh mục/tên/giá hiện tại không được thay thế snapshot của đơn cũ.

### 3.1. Kho

- Sản phẩm: tồn int >= 0, trạng thái bán bool. Đổi trạng thái bán không đổi tồn/giá.
- Biến động: Id, ProductId, Type, QuantityDelta, StockBefore, StockAfter,
  Reason (trim 1..200), OrderItemId nullable, RecordedByAccountId, RecordedAt.
- Mapping: InvTransId -> Id; Quantity -> QuantityDelta; Before/After -> StockBefore/StockAfter;
  OrderDetailId -> OrderItemId; CreatedAt -> RecordedAt.
- StockAfter = StockBefore + QuantityDelta; trước/sau không âm.
- Receive: dương, không có OrderItemId. Adjust: khác 0, không có OrderItemId.
- Sale: âm, có OrderItemId. Return: dương, có OrderItemId.
- Unique có điều kiện (OrderItemId, Type) khi OrderItemId khác null:
  mô hình hiện tại đặt/hủy cả dòng một lần, không chia nhiều lần giao/hoàn cùng dòng.
- FK nguồn tồn tại; không cascade xóa lịch sử. RecordedByAccountId phải dùng account thực khi port,
  không mang FK EmployeeId cũ sang account chung bằng cách giả định hai ID bằng nhau.

### 3.2. Voucher và lượt dùng

Promotions:

- PromotionId bigint PK; Code bắt buộc, trim, 1..50, không phân biệt hoa/thường, unique;
  PromotionName 1..150.
- DiscountType Percent hoặc Fixed; DiscountValue decimal(18,2).
  Percent > 0 và <= 100; Fixed > 0 và nguyên đồng.
- MinOrderAmount decimal(18,0) >= 0.
- MaxDiscountAmount decimal(18,0)?; null không đặt trần; có giá trị phải > 0.
- StartDate/EndDate datetimeoffset; StartDate < EndDate.
- TotalUsageLimit/PerCustomerUsageLimit int?; null không giới hạn, có giá trị > 0.
- IsActive bool. Không hard-delete voucher đã có lịch sử.

PromotionUsage:

- PromotionUsageId PK; PromotionId, OrderId, CustomerId FK; UsedAt; ReleasedAt nullable.
- Unique OrderId; một đơn có tối đa một bản ghi dù đã hoàn lượt.
- ReleasedAt null nghĩa là đang chiếm một lượt; nếu có phải >= UsedAt.
- Đếm theo PromotionId và ReleasedAt null; theo khách thêm CustomerId.
  Index (PromotionId, CustomerId) lọc ReleasedAt null. Không lưu bộ đếm trùng trong Promotions.
- Cùng một OrderId không được đổi voucher bằng cách tạo thêm usage sau khi đã hủy.
- Schema hiện có chưa biểu diễn thời hạn giữ chỗ gateway. Chốt invariant: lượt giữ chỗ phải chiếm hạn mức,
  giải phóng đúng một lần khi hết hạn/hủy; thời hạn và trạng thái nguồn do contract checkout/gateway cung cấp (mục 9).

Snapshot trên Order:
VoucherCodeSnapshot, VoucherDiscountTypeSnapshot, VoucherDiscountValueSnapshot nullable;
DiscountAmount nguyên đồng >= 0. Không dùng voucher thì snapshot null và giảm bằng 0;
có voucher thì đủ ba trường, loại/giá trị hợp lệ.

Snapshot trên OrderItem:
UnitPriceSnapshot, tên món snapshot và DiscountAmount cho toàn dòng (không phải mỗi đơn vị).
0 <= DiscountAmount <= Quantity \* UnitPriceSnapshot.
Tổng giảm các dòng bằng giảm đơn; kiểm tra trong nghiệp vụ, không dùng CHECK liên bảng.
Tên món snapshot còn phải bổ sung trong code nguồn khi triển khai; không đọc tên hiện tại để in đơn cũ.

### 3.3. Hóa đơn

Invoice:

- Id, CustomerId; OrderId nullable hoặc GamingSessionId nullable: phải có đúng một nguồn.
- Unique có điều kiện trên mỗi nguồn khác null; không tạo hai hóa đơn cho cùng đơn/phiên.
- SubTotal, Discount, GrossAmount, RefundAmount, PaidAmount decimal(18,0).
- Status Pending/Paid/Cancelled; IssuedAt datetimeoffset.
- RecordedByAccountId nullable theo nguồn phát hành; nguồn khách lấy danh tính thực.
  PromotionId nullable chỉ là tham chiếu, không thay cho snapshot voucher/giá.
- SubTotal >= 0; 0 <= Discount <= SubTotal; GrossAmount = SubTotal - Discount;
  0 <= RefundAmount <= GrossAmount; 0 <= PaidAmount <= GrossAmount - RefundAmount.
- GrossAmount là giá trị SAU voucher khi phát hành, TRƯỚC hủy; không sửa khi hủy.
- RefundAmount là giá trị hủy sau giảm đã chốt trên hóa đơn; với khoản đã thu, chỉ chốt cùng nguồn hoàn
  đã được xác minh. Cash chưa thu chỉ giảm phải thu, không tạo dòng tiền hoàn.
  PaidAmount là tiền thực thanh toán ròng; chỉ giảm theo hoàn tiền thực đã ghi nhận.
- Ràng buộc PaidAmount yêu cầu cập nhật chứng từ và nguồn ghi nhận hoàn cùng transaction SQL.
  Gateway đang chờ hoàn được theo dõi ở nghiệp vụ đơn/payment; hóa đơn giữ số tiền đã chốt trước đó.
  Khi nguồn hoàn thành công được xác minh, cập nhật RefundAmount/PaidAmount đúng một lần.
  Không thêm trạng thái PendingRefund vào Invoice hoặc giữ transaction SQL trong lúc chờ provider.
- Đơn đã hủy toàn bộ: Cancelled; không kết luận chỉ dựa vào GrossAmount - RefundAmount = 0
  vì đơn giảm 100% có thể vẫn hợp lệ. Đơn hợp lệ có số phải trả 0 có thể Paid, PaidAmount = 0.

InvoiceDetail: Id, InvoiceId FK, ItemType (Food/Gaming), Description snapshot,
Quantity > 0, UnitPrice >= 0, Amount và giảm phân bổ.
Truong hiện có Description/Quantity/UnitPrice/Amount, chưa có cột giảm phân bổ riêng.
Khi triển khai phải bổ sung hoặc xác định nguồn snapshot bất biến; không tự đọc giá/giảm từ voucher hiện tại.
Phiên cần snapshot đơn giá giờ và khoảng thời gian đã tính. Không tính cả món lần nữa vào hóa đơn phiên.

Bỏ mô hình Orders.InvoiceId -> Invoices nhiều đơn/một hóa đơn khi chuyển nguồn;
dùng Invoices.OrderId là liên kết chính. Nếu có dữ liệu cũ phải đối chiếu/chuyển trước khi drop.
InvoiceDate -> IssuedAt; TotalAmount -> GrossAmount chỉ khi đã xác minh cùng định nghĩa;
PaidAmount không được suy ra từ TotalAmount hoặc chữ Paid mà không đối chiếu nguồn thu/hoàn.

## 4. Quy tắc voucher

1. Chỉ tiền món ăn, một voucher/đơn; không giảm tiền chơi hoặc nạp ví.
2. Hợp lệ khi IsActive và StartDate <= now < EndDate; backend dùng một mốc now cho thao tác.
3. Kiểm tra ngưỡng trên tổng tiền món trước giảm.
4. Giảm thô: Percent = subtotal \* value / 100; Fixed = value.
   Áp trần nếu MaxDiscountAmount khác null, chặn không vượt subtotal,
   làm tròn nguyên đồng (0,5 lên; MidpointRounding.AwayFromZero), kiểm tra lại 0..subtotal.
5. Ghi lượt khi tạo đơn thành công kể cả cash chưa thu; thao tác lỗi không chiếm lượt.
   Kiểm tra trước/preview không ghi lượt hoặc đảm bảo còn lượt lúc đặt.
6. Hủy một phần không hoàn lượt, không tính lại ngưỡng hay voucher theo dữ liệu hiện tại.
   Hủy toàn bộ hoàn lượt đúng một lần; vẫn giữ lịch sử. Hoàn lượt không gia hạn voucher.
7. Chia giảm theo tỷ lệ giá trị dòng: lấy phần nguyên từng phần chia,
   phần đồng còn dư cấp theo phần lẻ giảm dần; hòa thì thứ tự dòng ổn định lúc tạo.
   Lưu kết quả nguyên đồng, không tính lại phân bổ sau khi hủy.
8. Hủy cả dòng: giá trị sau giảm = quantity \* snapshot price - allocated discount.
   Hoàn theo nguồn đã trả; cash chưa thu chỉ giảm phải thu. Không mặc định hoàn ZaloPay vào ví.
9. Tắt/sửa voucher chỉ ảnh hưởng đơn mới; không sửa snapshot cũ.
   Mã voucher đã có usage giữ bất biến để truy vết; sửa tên/rule không sửa lịch sử.
10. Kiểm tra và ghi usage dưới khóa voucher trong transaction đơn; khóa chỉ giữ đến commit.
    Hai request tranh lượt cuối chỉ một request được nhận; unique OrderId và chống lặp của đơn
    ngăn cùng request dùng lượt hai lần. Mọi caller/sửa hạn mức phải dùng cùng chiến lược khóa.

Ví dụ: subtotal 200000, Percent 20, max 30000 => giảm 30000.
Max null => giảm 40000. Giảm cố định 300000 trên đơn 200000 => giảm 200000.
Đơn 2 dòng 100000/50000, giảm 30000 => phân bổ 20000/10000; hủy dòng đầu trị giá 80000.

## 5. HTTP contract

### 5.1. Kho/hóa đơn có API tương ứng trên Truong

| Method + route                      | Quyền    | Request                                               | Thành công                      |
| ----------------------------------- | -------- | ----------------------------------------------------- | ------------------------------- |
| GET /api/admin/inventory            | A/S      | Không body                                            | 200: item[]                     |
| GET /api/admin/inventory/movements  | A/S      | Không body                                            | 200: tối đa 500, mới nhất trước |
| POST /api/admin/inventory/movements | A/S      | productId, type Receive/Adjust, quantityDelta, reason | 201: movement                   |
| PUT /api/admin/products/{id}/status | A/S      | isActive bắt buộc bool                                | 200: item                       |
| GET /api/admin/invoices             | A/S      | Không body                                            | 200: tối đa 500, mới nhất trước |
| GET /api/client/invoices            | Customer | CustomerId lấy từ danh tính                           | 200: tối đa 200 của mình        |

item = {id,name,stockQuantity,isActive,currentPrice,categoryId}.
movement = {id,productId,type,quantityDelta,stockAfter,reason,orderItemId,recordedByAccountId,recordedAt}.
invoice = {id,customerId,orderId,gamingSessionId,grossAmount,refundAmount,paidAmount,status,issuedAt}.
Danh sách rỗng trả []; delta khác 0 và trong [-1000000,1000000], tồn 0..int.MaxValue.
Thứ tự hóa đơn IssuedAt giảm dần rồi Id giảm dần; Admin không tự có quyền API chỉ dành Customer.

### 5.2. API voucher do Trường chốt (chưa có implementation)

Đại dùng contract này cho UI quản trị; Khang dùng cùng rule cho checkout.
Hai bên đối chiếu để phát hiện xung đột tích hợp.

| Method + route                      | Quyền    | Kết quả                                 |
| ----------------------------------- | -------- | --------------------------------------- |
| GET /api/admin/vouchers             | Admin    | 200: voucher[]                          |
| GET /api/admin/vouchers/{id}        | Admin    | 200: voucher                            |
| POST /api/admin/vouchers            | Admin    | 201: voucher, Location tới GET chi tiết |
| PUT /api/admin/vouchers/{id}        | Admin    | 200: voucher                            |
| PUT /api/admin/vouchers/{id}/status | Admin    | 200: voucher; body {isActive}           |
| POST /api/client/vouchers/preview   | Customer | 200: kết quả giảm cho giỏ của mình      |

Không thêm hard-delete; tắt voucher thay xóa lịch sử.
Body tạo/sửa:

```json
{
  "code": "FOOD20",
  "name": "Giảm tiền món",
  "discountType": "Percent",
  "discountValue": 20,
  "minOrderAmount": 100000,
  "maxDiscountAmount": null,
  "startDate": "2026-11-02T00:00:00+07:00",
  "endDate": "2026-11-10T00:00:00+07:00",
  "totalUsageLimit": 100,
  "perCustomerUsageLimit": 2,
  "isActive": true
}
```

Response voucher = body trên + id; name map PromotionName; không công khai danh sách khách đã dùng.
PUT gửi đủ các field, không lấy thiếu field/null làm cập nhật ngầm.

Preview nhận {code,items:[{productId,quantity}]}; quantity là số nguyên dương.
Server nạp giá/sản phẩm, tính subtotal và lấy CustomerId từ auth; không nhận tổng/giảm do client tính.
Response {code,subtotal,discountAmount,totalAmount}; subtotal - discountAmount = totalAmount.
Preview không tạo Order/Usage/Invoice và không giữ lượt; checkout phải kiểm tra lại trong transaction.
Checkout của Khang gọi rule nội bộ, không gọi HTTP preview giữa transaction để tin kết quả cũ.
Tạo voucher normalize trim + uppercase invariant; code ASCII chữ/số/gạch ngang/gạch dưới,
tối đa 50 để thống nhất chuẩn hóa. Đây là contract do Trường chốt; nếu caller có xung đột
thì ghi rõ chênh lệch và cập nhật contract trước khi triển khai khác nhau.

Lỗi chung dùng ProblemDetails: status/title/detail/traceId; validation có errors theo field.

- 400: JSON/ID/kiểu tiền/ngày hoặc DTO sai.
- 401: chưa đăng nhập. 403: sai quyền/khóa/không sở hữu.
- 404: không có ID voucher/sản phẩm/hóa đơn.
- 409: trùng mã, voucher tắt/hết hạn/chưa hiệu lực/thiếu ngưỡng/hết lượt,
  trạng thái đơn không cho phép, hoặc thay đổi đồng thời.
  Với preview, mã voucher không tồn tại trả 404.
- 500/503: lỗi hệ thống/DB theo handler chung, không giả lập thành công.

Chi tiết/tìm/lọc/in/xuất hóa đơn thuộc S2-T-2/S3-T-2, cần contract bổ sung dựa trên UI.
Hai API danh sách hiện tại chưa đáp ứng các tiêu chí đó; không tự coi là đã triển khai đủ.

## 6. Điểm gọi nội bộ và hợp đồng phát hành chứng từ

Tên bên dưới là điểm gọi logic do Trường chốt để triển khai, chưa phải interface đã có trên Truong.
Trường cung cấp nghiệp vụ kho/voucher/chứng từ; Khang/Phúc gọi từ luồng của họ, không tự viết bản rule khác.

| Điểm gọi                          | Input tin cậy từ server                                         | Output/hành vi                                                                  |
| --------------------------------- | --------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| Inventory.ApplySale / ApplyReturn | orderItemId, productId, quantity, actor, operationKey           | Biến động + tồn; Sale âm, Return dương; nguồn đơn hợp lệ                        |
| Voucher.ValidateAndUse            | order đã tính giá, code, customerId từ auth, actor              | Snapshot giảm, phân bổ từng dòng, Usage cùng transaction                        |
| Voucher.ReleaseForCancelledOrder  | orderId, actor                                                  | Chỉ khi toàn bộ đơn hủy; trả lại usage hiện có nếu đã hoàn, không tạo usage mới |
| Invoice.IssueForOrder             | orderId và snapshot đã lưu, nguồn payment/ledger                | Tạo/đọc lại cùng invoice; không tin Amount do browser truyền                    |
| Invoice.IssueForSession           | sessionId đã kết thúc, phí/giá giờ snapshot, nguồn ledger       | Tạo/đọc lại cùng invoice, không gộp món đã xuất hóa đơn đơn                     |
| Invoice.ApplyFinancialChange      | sourceId, operationKey duy nhất, khoản thu/hủy/hoàn đã xác minh | Cập nhật invoice/chi tiết nhất quán; gửi lặp không cộng RefundAmount hai lần    |

- Caller Khang/Phúc mở transaction chung rồi gọi ví, kho, voucher, hóa đơn bằng cùng DbContext.
  Các service con chỉ stage/save theo cơ chế chung, không commit; UoW kho hiện tại cấm transaction lồng,
  nên không gọi nguyên API CreateMovementAsync từ transaction đơn.
- Thứ tự khóa trong các module của Trường: đơn/nguồn, voucher, sản phẩm theo ID tăng dần.
  Nếu thao tác có ví/phiên, caller lấy khóa customer/ví và phiên theo contract tiền chung trước các khóa này.
  Minh/Khang/Phúc cung cấp thứ tự khóa nguồn đang dùng để kiểm tra không có vòng khóa ngược.
- FK/unique source chống phát hành trùng. Khi gọi lại phải trả chứng từ cũ nếu cùng nguồn/snapshot;
  snapshot khác phải báo xung đột, không âm thầm ghi đè.
- Unique source chưa đủ chống hoàn/thu lặp: operationKey phải gắn nguồn ledger/payment/hủy.
  Yêu cầu do Trường chốt: một khóa nguồn ổn định cho một thao tác, được giữ nguyên khi retry.
  Minh/Khang cung cấp tên trường/index trong ledger/payment/hủy để adapter nối vào; không thêm ledger độc lập.
- Không tự retry riêng đoạn phát hành hoặc hoàn tiền sau khi transaction chung đã thất bại.

### Trạng thái theo nguồn

| Nguồn        | Thời điểm phát hành                    | Tiền/trạng thái                                                                             |
| ------------ | -------------------------------------- | ------------------------------------------------------------------------------------------- |
| Đơn Wallet   | Đặt đơn thành công cùng lần debit      | Paid; PaidAmount bằng tiền thực trừ                                                         |
| Đơn Cash     | Đặt đơn thành công                     | Pending, PaidAmount = 0; thu thành công chuyển Paid                                         |
| Đơn ZaloPay  | Đơn chờ thanh toán được tạo thành công | Pending, PaidAmount = 0; Paid chỉ sau callback/query đã xác minh; IssuedAt giữ nguyên       |
| Phiên        | Kết thúc thành công                    | Paid khi tiền chơi đã ghi nhận theo ledger; không thu lần hai                               |
| Hủy một phần | Cập nhật invoice đã có                 | Giữ Gross; chốt Refund/Paid cùng kết quả hủy/hoàn; gateway đang chờ hoàn chưa chốt tiền mới |
| Hủy toàn bộ  | Khi hủy/hoàn được chốt                 | Cancelled; hoàn lượt đúng một lần; gateway đang chờ hoàn giữ trạng thái tiền trước đó       |

IssuedAt là lúc phát hành, không đổi thành collectedAt hoặc thời gian chỉnh sửa.
Đơn ví/cash không có phí chơi trong hóa đơn đơn; hóa đơn phiên chỉ ghi tiền chơi.

## 7. Nguồn báo cáo và đối soát

Báo cáo đọc giao dịch đã ghi nhận, không tính doanh thu bằng cách cộng tổng hóa đơn.

| Chỉ tiêu             | Nguồn                                                              | Thời gian                                                              |
| -------------------- | ------------------------------------------------------------------ | ---------------------------------------------------------------------- |
| Tiền chơi ví         | WalletTransaction GamingCharge âm, đổi dấu khi hiển thị            | recorded_at                                                            |
| Tiền món ví          | OrderDebit âm, đổi dấu                                             | recorded_at                                                            |
| Phí giải             | TournamentFee âm nếu policy phí được duyệt                         | recorded_at                                                            |
| Hoàn ví              | OrderItemRefund/TournamentRefund dương, khấu trừ doanh thu         | recorded_at                                                            |
| Tiền mặt món đã thu  | Nguồn thu đơn đã xác nhận, không dùng TotalAmount của đơn chưa thu | collected_at                                                           |
| Nạp ví               | TopUpCredit/top_ups; báo riêng, KHÔNG là doanh thu dịch vụ         | Theo nghiệp vụ nạp/thu                                                 |
| Checkout ZaloPay món | Giao dịch thu thành công cho Payment Order đã được server xác minh | Thời điểm server ghi nhận thu thành công, không dùng redirect/IssuedAt |
| Hóa đơn              | Đối soát snapshot/phải thu/đã thu/hủy                              | issued_at; không đại diện ngày dòng tiền                               |

Quy tắc do Trường chốt: mỗi khoản thu/hoàn có đúng một nguồn được đếm.
Wallet dùng ledger; Cash dùng khoản thu/hoàn đã xác nhận; ZaloPay món dùng giao dịch Order đã xác minh.
Nếu cùng khoản gateway có ledger đại diện thì loại bản sao khỏi tổng debit/hoàn ví khi cộng gateway.
Minh cung cấp ID liên kết nguồn để thực hiện khử trùng; không đoán từ mô tả hoặc số tiền bằng nhau.
Nạp ZaloPay vào ví vẫn là nạp ví, không phải doanh thu gateway món.

netRevenue = debit dịch vụ Wallet đổi dấu - hoàn dịch vụ Wallet

- Cash thực thu - Cash thực hoàn + ZaloPay món thực thu - ZaloPay món thực hoàn.
  Các tập trên phải không trùng nhau; không cộng thêm tổng Invoice.
  Khoản hoàn tính ngày ghi nhận hoàn, không sửa lại doanh thu ngày đặt đơn.
  Nếu luồng chưa hỗ trợ hoàn Cash thì không tạo khoản hoàn giả; gateway chưa có nguồn xác minh
  thì báo thiếu nguồn tích hợp, không trả số 0 như thể đã đối soát đầy đủ.

Ngày from/to dạng YYYY-MM-DD; từ đầu ngày +07:00 đến trước đầu ngày sau to.
Đơn đếm theo placed_at; khách/máy/phiên Active/top-up Pending là bộ đếm hiện tại.
Tiền mặt phải có số tiền thực thu snapshot và collectedAt; không sửa ngày thu khi đọc báo cáo.
Không suy doanh thu từ các hóa đơn seed chỉ phục vụ test đọc (không có ledger/payment).

## 8. Owner, phần bổ sung và thứ tự migration

| Owner  | Phần chịu trách nhiệm                                 | Bàn giao cần đối chiếu                                                 |
| ------ | ----------------------------------------------------- | ---------------------------------------------------------------------- |
| Đại    | Account/quyền, cơ chế auth, UI voucher                | Mapping account/customer/actor, quyền API voucher và contract form     |
| Minh   | Customer/ví/top-up/ledger/payment gateway             | Dấu/type tiền, operationKey, thu/hoàn, callback/query và nguồn báo cáo |
| Phúc   | Máy/phiên, giá giờ snapshot và phí đã ghi nhận        | Đầu vào phát hành phiên, đóng lặp, giao dịch đã trừ; SignalR chung     |
| Khang  | Product/Order/OrderItem, thu cash, snapshot món/giảm  | Tồn/trạng thái bán, điểm gọi kho/voucher/chứng từ; cash/gateway và hủy |
| Trường | Biến động kho, invoice/detail, voucher/usage, báo cáo | Invariant/API/rule, nghiệp vụ dùng chung và thứ tự migration           |

Thứ tự phụ thuộc migration do Trường chốt (tên file/PR do owner cung cấp, không sao chép lịch sử Truong):

1. Đại/Minh: account/customer và khóa nguồn giao dịch chung được nhóm xác nhận.
2. Khang/Phúc: thay đổi nguồn product/order/item/session, snapshot và nguồn thu.
3. Trường phối hợp Khang: stock/status, inventory movement; voucher và order snapshot;
   invoice/detail; usage sau khi FK order/customer/voucher đã có.
4. Index/constraint và backfill sau khi đối chiếu dữ liệu cũ; kiểm tra DB thử mới ở S1-T-2.

Mỗi owner viết entity/config phần mình, Trường điều phối không viết thay toàn bộ schema.
Giải quyết chênh lệch trước khi owner sinh migration tích hợp; chỉ một PR migration xử lý tại một thời điểm.
Không sửa/xóa migration đã chạy trên DB chung. Dữ liệu cũ không đủ nguồn thì lập danh sách đối soát,
không gán ngầm đã Paid, snapshot giá hiện tại, datetime mặc định hoặc mã voucher rỗng.
Migration thử thành công ở CyberManagement riêng không chứng minh migration hợp lệ ở CyberCafe chung.
DBML/SQL xuất cập nhật theo migration đã tích hợp ở S1-T-2, không thay đổi chúng trong tài liệu này.

## 9. Quyết định của owner và phụ thuộc cần bàn giao

### 9.1. Phần Trường đã chốt

| Nội dung                                    | Quyết định / nơi tra                                                             | Người nhận để triển khai             |
| ------------------------------------------- | -------------------------------------------------------------------------------- | ------------------------------------ |
| Schema kho/chứng từ/voucher, mapping đích   | Mục 3; dùng account/customer của DB đích, không tạo identity riêng               | Đại/Minh/Khang/Phúc                  |
| API/DTO, quyền và lỗi voucher               | Mục 5.2 là contract của owner, chưa phải API đã triển khai                       | Đại làm UI; Khang dùng rule checkout |
| Ngày/ngưỡng/trần/làm tròn/hạn mức/hoàn lượt | Mục 4; không thay đổi snapshot của đơn cũ                                        | Khang/Đại                            |
| Hóa đơn Wallet/Cash/ZaloPay/phiên           | Mục 6; đơn phát hành khi tạo, phiên khi kết thúc; chống trùng theo nguồn         | Khang/Phúc/Minh                      |
| Hóa đơn khi gateway chờ hoàn                | Giữ tiền đã chốt; hoàn xác minh thành công mới cập nhật, không thêm enum hóa đơn | Minh/Khang                           |
| Snapshot và trường nguồn phải bổ sung       | Mục 3.2/3.3; tên/giá/giảm và phí giờ bất biến                                    | Khang/Phúc                           |
| Doanh thu và ngày ghi nhận                  | Mục 7; không đếm nạp ví, Invoice hoặc nguồn thanh toán hai lần                   | Minh/Khang/Phúc                      |
| Phân chia/thứ tự migration                  | Mục 8; từng owner viết phần mình                                                 | Tất cả owner                         |

Các thành viên nhận contract và đối chiếu caller/dữ liệu của họ. Khi phát hiện xung đột phải nêu
trường/luồng cụ thể để Trường điều chỉnh có ghi nhận; không tự triển khai một rule khác.
Bàn giao contract không đồng nghĩa các owner đã nhận/xác nhận hoặc Trưởng nhóm đã duyệt.

### 9.2. Phụ thuộc từ module khác, không phải yêu cầu Trường làm thay

| Đầu vào cần nhận                                                                               | Owner cung cấp                                   | Ảnh hưởng khi chưa có                                                                 |
| ---------------------------------------------------------------------------------------------- | ------------------------------------------------ | ------------------------------------------------------------------------------------- |
| Mapping dữ liệu account/customer cũ, claims và cơ chế auth/CSRF thực                           | Đại + Minh, S1-D-1/S1-M-1                        | Chưa hoàn tất mapping backfill và test auth tích hợp                                  |
| Ledger/payment: ID nguồn thu/hoàn, trạng thái thành công, operationKey và thứ tự khóa ví/phiên | Minh; Khang/Phúc nối nguồn                       | Chưa chạy được phát hành/cập nhật/đối soát xuyên module; không tự giả định tên trường |
| Checkout gateway: thời hạn giữ chỗ/hết hạn, callback muộn và cách hoàn theo phương thức gốc    | Khang + Minh; Trường cung cấp rule giữ/hoàn lượt | Chưa chốt được giá trị thời hạn và mapping reservation vào schema gateway             |
| File/PR migration và dữ liệu cũ thực tế                                                        | Từng owner                                       | Chưa áp lịch tích hợp hoặc xác nhận backfill thành công                               |

Trường đã chốt invariant giữ chỗ: chiếm hạn mức trong lúc chờ, giải phóng đúng một lần khi hết hạn/hủy,
không tự giao món khi callback đến muộn sau khi đã giải phóng; phải đối soát nguồn thanh toán.
Thời hạn cụ thể và xử lý payment muộn thuộc contract checkout/gateway liên module, không tự đặt
một con số khi chưa có dữ liệu từ Minh/Khang. Trường chịu phần schema/rule voucher tương ứng.

### 9.3. Đối chiếu và duyệt

- Trường bàn giao tài liệu này; ghi lại phản hồi kỹ thuật cụ thể và thay đổi contract nếu có.
- Đại/Minh/Phúc/Khang đối chiếu phần họ gọi/cung cấp; không giao họ viết lại phần thiết kế của Trường.
- Trưởng nhóm duyệt cuối. Chưa có bằng chứng bàn giao/phản hồi hoặc duyệt thì ghi đúng trạng thái,
  không tick Done chỉ vì file đã viết xong.

## 10. Checklist bàn giao SCRUM-12

| Tiêu chí Jira                                        | Bằng chứng trong tài liệu   | Trạng thái                                                                       |
| ---------------------------------------------------- | --------------------------- | -------------------------------------------------------------------------------- |
| Schema/quan hệ/ràng buộc, mapping và owner migration | Mục 3, 8                    | Owner đã chốt thiết kế; mapping dữ liệu và lịch PR thực theo phụ thuộc 9.2       |
| Rule và API voucher chung                            | Mục 4, 5                    | Owner đã chốt để Đại/Khang sử dụng; chưa có bằng chứng bàn giao/đối chiếu        |
| Hợp đồng chứng từ và nguồn doanh thu                 | Mục 6, 7                    | Owner đã chốt; các field/nguồn ví và policy checkout liên module cần đầu vào 9.2 |
| Bàn giao/duyệt                                       | Tài liệu này và PR của task | Chưa gửi PR, chưa có quyết định duyệt                                            |

Ca kiểm thử phải bàn giao cho các task triển khai:

- Receive/Adjust biên, tồn âm, rollback; hai request tranh tồn; Sale/Return không ghi trùng.
- Voucher Percent/Fixed, có/không trần, min order, đúng đầu/cuối hiệu lực, 100%, làm tròn/phân bổ.
- Tranh lượt cuối toàn hệ thống/theo khách, retry cùng đơn, rollback, hủy một phần/toàn bộ/lặp.
- Invoice nguồn XOR, unique, caller cùng transaction, thu/hoàn lặp, zero-total,
  giá/tên/voucher đổi không đổi snapshot cũ.
- Quyền A/S/Customer, không xem chứng từ khách khác, preview không tin giá/tổng client.
- Báo cáo đúng nguồn/ngày, không cộng nạp ví hoặc cộng invoice lần hai.
- Gateway sandbox/giữ chỗ/hết hạn/callback muộn theo policy đã được xác nhận, không coi fixture là tích hợp thật.

Kiểm tra ở lượt soạn: đọc code từ git show Truong, đối chiếu DBML/backend chung,
rà nội dung/liên kết và whitespace; không chạy API/SQL/build cho thay đổi tài liệu này.
Chưa ghi nhận task Done. Thiết kế thuộc owner đã chốt; còn đối chiếu phụ thuộc thực, bàn giao và quyết định duyệt của Trưởng nhóm.
