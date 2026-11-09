# SCRUM-12 - Hợp đồng kho, hóa đơn và voucher

| Thông tin           | Nội dung                                                                 |
| ------------------- | ------------------------------------------------------------------------ |
| Công việc           | Define-Inventory-Invoice-Voucher-Contracts                               |
| Mã Jira / kế hoạch  | SCRUM-12 / S1-T-1                                                        |
| Người phụ trách     | Lê Vũ Huy Trường                                                         |
| Phiên bản           | 1.7 - bổ sung sau review 05/11/2026, chờ duyệt                            |
| Backend đích        | CyberManagement, cấu trúc Backend/src, ApplicationDbContext, nền 35 bảng |
| Phụ thuộc           | SCRUM-2 / S1-D-1 và SCRUM-5 / S1-M-1                                     |
| Trạng thái bàn giao | Đề nghị review; các đầu vào và xác nhận còn chờ được liệt kê tại mục 9   |

## 1. Mục đích và phạm vi

Tài liệu thống nhất cấu trúc dữ liệu, quy tắc nghiệp vụ, API, phân quyền và trách nhiệm
tích hợp cho kho, hóa đơn và voucher. Đây là cơ sở để các thành viên triển khai cùng một
contract và chuẩn bị migration theo thứ tự phụ thuộc.

Nền áp dụng là CyberManagement 35 bảng, sử dụng EmployeeId/CustomerId kiểu int.
Các phần có sẵn được giữ và mở rộng theo bảng mapping tại mục 3.

Phạm vi bàn giao gồm:

- Mapping schema và phân loại giữ nguyên, thay đổi, thêm mới.
- Quy tắc voucher, API/DTO và permission cho từng endpoint.
- Phát hành hóa đơn gộp, snapshot và cập nhật thu/hủy/hoàn.
- Nguồn doanh thu, nguyên tắc chống tính trùng và phân chia migration.
- Kết quả đối chiếu nguồn, đầu vào còn chờ và tiêu chí nghiệm thu.

Việc triển khai chức năng, áp migration và kiểm thử tích hợp được thực hiện trong các task liên quan.
Phần bổ sung phiên bản 1.7 tại mục 6.1.1/6.3.1 và các mapping tương ứng là phương án sau review,
chưa phải quyết định đã được Trường, các owner tích hợp hoặc Trưởng nhóm chấp thuận; xem mục 9.4.

## 2. Định danh, xác thực và quy ước dữ liệu

### 2.1. Định danh và quyền

- Backend dùng cấu trúc Backend/src/CyberManagement.API, Application, Domain và Infrastructure.
- PK/FK dùng int theo nền đích. Dữ liệu long từ nguồn cũ (baseline CyberCafe.Api 19 bảng) phải được kiểm tra phạm vi
  và mapping trước khi chuyển; giá trị vượt int.MaxValue hoặc thiếu đối ứng phải được báo để xử lý.
- Nhân viên tham chiếu Employees.EmployeeId; khách tham chiếu Customers.CustomerId.
  Hai miền ID độc lập; không sử dụng CustomerId làm EmployeeId và không tạo thêm Account.
- Các vai trò nhân viên: Admin, Manager, Cashier, Waiter, Technician.
  Endpoint nghiệp vụ kiểm tra permission cụ thể tại mục 5; Customer kiểm tra thêm quyền sở hữu.
- Danh tính và người thao tác lấy từ principal đã xác thực. Giá, số tiền, trạng thái và quyền
  không lấy từ các giá trị do trình duyệt tự khai báo.

### 2.2. Xác thực JWT

| Nội dung              | Contract SCRUM-2                                                      |
| --------------------- | --------------------------------------------------------------------- |
| Nhân viên đăng nhập   | POST /api/auth/login, body username/password                          |
| Khách đăng nhập       | POST /api/auth/customer/login, body phone/password                    |
| Response              | accessToken, expiresAtUtc, employee hoặc customer                     |
| Request sau đăng nhập | `Authorization: Bearer <token>`                                       |
| Claims nhân viên      | sub=EmployeeId, role và các claim permission                          |
| Claims khách          | sub=CustomerId, `customer_id`, role=Customer, `account_type`=Customer |
| Ánh xạ claims         | MapInboundClaims=false, RoleClaimType=role, NameClaimType=name        |

Access token mặc định có hiệu lực 20 phút, cấu hình trong khoảng 1–30 phút.
Logout trả 204 và client xóa token; cơ chế hiện có không thu hồi ngay token đã cấp.
Login từ chối Employee.IsActive=false hoặc Customer.Status khác Active.
Yêu cầu khóa tài khoản có hiệu lực ngay sau khi cấp token cần được đối chiếu với Đại
trước nghiệm thu; không suy diễn từ kết quả kiểm tra trạng thái tại login.

### 2.3. Tiền, thời gian và transaction

- JSON dùng camelCase; enum truyền dưới dạng chuỗi; tiền VND là số nguyên.
- Chuẩn tiền đích: C# decimal, SQL decimal(18,0) cho các giá trị tiền VND: giá món/giờ,
  tiền đơn, phí phiên, số dư, thu/hoàn, tổng/giảm hóa đơn, Payment.Amount và ngưỡng/trần voucher.
- Promotion.DiscountValue dùng decimal(18,2) vì cột này chứa cả tỷ lệ và số tiền:
  Percent cho phép hai chữ số thập phân; Fixed phải là số nguyên đồng và được validation/CHECK bảo vệ.
  Không đổi cột DiscountValue thành decimal(18,0) làm mất tỷ lệ như 12,5%.
- Precision trên là thiết kế đích của contract. Các cột hiện còn decimal(12,2) được phân loại
  là thay đổi schema; owner bảng thực hiện migration theo mục 8. Trường điều phối kiểu đích,
  Minh đối chiếu cột tiền ví/payment. Kiểm tra dữ liệu trước khi đổi: dòng có phần lẻ VND phải
  được đối soát/xử lý rõ, migration dừng nếu chưa giải quyết; không âm thầm làm tròn dữ liệu cũ.
- Các thời điểm mới dùng DateTimeOffset/datetimeoffset; nghiệp vụ theo múi giờ +07:00.
  Dữ liệu DateTime cũ phải được đối chiếu nguồn múi giờ trước khi chuyển.
- Ví/ledger, đơn, tồn, lượt voucher và chứng từ trong cùng thao tác phải cùng thành công
  hoặc cùng hoàn tác. Các module dùng chung transaction/DbContext; module con không tự commit.
- SignalR phát sau commit và theo nhóm nhận được cấp quyền. Khi reconnect, client đọc lại API.

## 3. Schema hiện tại và mapping giữ / thay đổi / thêm mới

Nền đối chiếu: dev tại a975f8d6f329266d954090521469975c5e8dcdb6, đã có local;
nguồn được cố định theo commit để tái hiện việc đối chiếu. ApplicationDbContext có 35 DbSet, dùng CyberManagement,
EmployeeId/CustomerId kiểu int; không có Account hoặc TopUp/TopUps.

35 bảng trong nền: Roles, Permissions, RolePermissions, Employees, Shifts, AuditLogs,
Customers, CustomerStatusHistories, WalletTransactions, ComputerRooms, ComputerTypes,
Computers, ComputerMaintenances, GamingSessions, SessionCharges, Categories, Products,
Suppliers, StockReceipts, StockReceiptDetails, InventoryTransactions, Promotions, Orders,
OrderDetails, OrderStatusHistories, Invoices, InvoiceOrders, InvoiceDetails, Payments,
Games, GameRanks, Tournaments, TournamentRegistrations, Teams, TeamMembers.

Tên entity singular khác tên bảng plural: Promotion -> Promotions, InvoiceOrder -> InvoiceOrders.
Các cột thêm dưới đây là thiết kế cần triển khai bằng migration mới, không khẳng định đã có.
Toàn bộ PK/FK mới dùng int, đồng bộ với schema CyberManagement hiện tại.

| Bảng/nguồn hiện có                                        | Giữ                                                                                                                                                                                                 | Thay đổi / bổ sung trên bảng sẵn có                                                                                                                                                                                           | Thêm mới riêng                                                                                                | Owner                                                                                      |
| --------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| Employees, Customers, Roles, Permissions, RolePermissions | Identity int, JWT sub/role/permission; hai miền ID nhân viên/khách riêng                                                                                                                            | Chỉ mapping dữ liệu cũ và nối principal; không tạo Account                                                                                                                                                                    | Không                                                                                                         | Đại; Minh đối chiếu Customer                                                               |
| AuditLogs                                                 | LogId int IDENTITY PK; EmployeeId int NULL, FK Employees.EmployeeId, Restrict; EntityType nvarchar(50) NOT NULL; EntityId int NOT NULL; Action nvarchar(255) NOT NULL; CreatedAt datetime2 NOT NULL | Dùng lại để ghi điều chỉnh thu muộn theo mục 6.2.2; không thêm Details/JSON. UTC do ứng dụng ghi, không có SQL default/constraint bảo đảm UTC                                                                                 | Không; nguồn entity/config/migration được ghim tại mục 10.1, 11                                               | Đại schema audit; Minh ghi audit tiền; Trường đối chiếu chứng từ                           |
| Categories                                                | CategoryId, CategoryName và FK sản phẩm                                                                                                                                                             | Đối chiếu dữ liệu trước migration                                                                                                                                                                                             | Không                                                                                                         | Khang                                                                                      |
| Products                                                  | ProductId, CategoryId, ProductName, Price, Stock, IsActive đã có                                                                                                                                    | Constraint Stock >= 0, concurrency/khóa khi cập nhật tồn nếu nền chưa có; không thêm StockQuantity trùng Stock                                                                                                                | Không                                                                                                         | Khang cấu hình Product; Trường rule kho                                                    |
| InventoryTransactions                                     | InvTransId, ProductId, EmployeeId, Type, Quantity, ReferenceType, ReceiptId, OrderId                                                                                                                | Thêm Before/After, Reason, OrderDetailId; chuẩn hóa Type/dấu Quantity sau đối soát; CreatedAt sang thời điểm có offset nếu triển khai; EmployeeId nullable cho nguồn khách/hệ thống theo mục 3.1                              | Không tạo lại bảng                                                                                            | Trường; Khang đối chiếu nguồn đơn                                                          |
| Suppliers, StockReceipts, StockReceiptDetails             | Các bảng/FK có sẵn, ReceiptId, SupplierId, EmployeeId, chi tiết nhập                                                                                                                                | Nối nguồn Receive và chống ghi tồn hai lần; không tạo bộ phiếu nhập khác                                                                                                                                                      | Không                                                                                                         | Trường đối chiếu nguồn nhập                                                                |
| Orders                                                    | OrderId, CustomerId, GamingSessionId, OrderTime, quan hệ nguồn                                                                                                                                      | Thêm snapshot voucher/code/type/value và DiscountAmount; nguồn payment/thu-hủy do Khang/Minh xác định                                                                                                                         | Không                                                                                                         | Khang; Trường contract snapshot                                                            |
| OrderDetails, OrderStatusHistories                        | OrderDetailId, OrderId, ProductId, Quantity, UnitPrice và lịch sử sẵn có                                                                                                                            | Thêm tên món snapshot, giảm phân bổ và trạng thái/hủy theo contract đơn; đây là các phần bổ sung vào schema hiện tại                                                                                                          | Không trong task này                                                                                          | Khang                                                                                      |
| Invoices                                                  | InvoiceId, CustomerId, GamingSessionId, EmployeeId, PromotionId, InvoiceDate, Subtotal, Discount, TotalAmount, Status                                                                               | Snapshot tổng/giảm/thu/hoàn và mapper từ nguồn thực theo mục 3.3; chuyển cột tiền sang decimal(18,0) theo mục 2.3 sau đối soát                                                                                                | Không tạo lại bảng                                                                                            | Trường                                                                                     |
| InvoiceOrders                                             | Khóa kép (InvoiceId,OrderId) và hai FK hiện có                                                                                                                                                      | Thêm unique index OrderId để một đơn chỉ thuộc một chứng từ; giữ index phục vụ truy nguồn theo OrderId, không chuyển sang Invoice.OrderId XOR                                                                                 | Không                                                                                                         | Trường + Khang                                                                             |
| Chưa có InvoiceIssueRequests                              | Không có nơi lưu khóa phát hành hóa đơn trong nền hiện tại | Lưu khóa, hash yêu cầu và InvoiceId cùng transaction phát hành theo mục 6.3.1; độc lập với khóa thu/hoàn trong Payments | InvoiceIssueRequests, không phải bảng ghi tiền | Trường; Khang/Phúc đối chiếu caller |
| InvoiceDetails                                            | InvoiceDetailId, InvoiceId, ItemType, Description, Quantity, UnitPrice, Amount                                                                                                                      | Thêm OrderDetailId/SessionChargeId nullable với XOR theo dòng món/phí; FK NoAction, unique có lọc theo từng nguồn; callback phí tìm InvoiceId bằng SessionChargeId; giữ snapshot bất biến                                     | Không                                                                                                         | Trường; Khang/Phúc cung cấp nguồn                                                          |
| Promotions (entity Promotion)                             | PromotionId, PromoName, DiscountType, DiscountValue, StartDate, EndDate, IsActive                                                                                                                   | Thêm Code unique, MinOrderAmount, MaxDiscountAmount nullable, TotalUsageLimit/PerCustomerUsageLimit nullable; chuẩn hóa loại/offset; tiền decimal(18,0), DiscountValue decimal(18,2) theo mục 2.3                             | Không tạo bảng Voucher khác                                                                                   | Trường                                                                                     |
| Chưa có PromotionUsage                                    | Không có bảng để tái sử dụng                                                                                                                                                                        | FK int đến Promotions/Orders/Customers; xem rule usage                                                                                                                                                                        | PromotionUsages: từng lần dùng và hoàn lượt, unique OrderId                                                   | Trường                                                                                     |
| Payments                                                  | PaymentId, Amount, Method, Status và quan hệ InvoiceId/WalletTransactionId đã có; nền vẫn bắt buộc InvoiceId                                                                                        | Thiết kế mục 6.1–6.4: InvoiceId nullable; OrderId/SessionChargeId XOR; Type Payment/Refund và RefundOfPaymentId tự tham chiếu; khóa thao tác, mã gateway/hoàn, thời gian có offset và unique index; backfill trước constraint | Không tạo Payments thứ hai, PaymentRefunds hoặc bảng phân bổ trong phạm vi hiện tại; schema đích chưa migrate | Minh xử lý tiền/gateway và migration; Trường contract chứng từ; Khang/Phúc đối chiếu nguồn |
| Chưa có RefundAttempts                                    | Không có bảng để tái sử dụng                                                                                                                                                                        | FK đến dòng Payments.Type=Refund; lịch sử mã gửi/query provider theo mục 6.4.1, không ghi thêm khoản tiền hoặc tính doanh thu                                                                                                 | RefundAttempts, metadata lần gọi hoàn, không phải bảng PaymentRefunds                                         | Minh gateway/migration; Trường đối soát                                                    |
| WalletTransactions                                        | TransactionId, CustomerId, Amount, BalanceAfter, Type, Description, CreatedAt                                                                                                                       | Đầu vào SCRUM-5: dấu/type, source IDs, idempotency, ngày thu/hoàn; chưa khẳng định có RecordedAt/OrderId/OrderDetailId                                                                                                        | Không tạo lại ví                                                                                              | Minh                                                                                       |
| GamingSessions, SessionCharges                            | Hai bảng phí phiên đã có, FK máy/khách và phí được ghi nhận                                                                                                                                         | Phúc cung cấp snapshot giá giờ/khoảng phí và tổng đã trừ, chống đóng/thu lặp                                                                                                                                                  | Không                                                                                                         | Phúc                                                                                       |
| Top-up                                                    | Chưa có TopUp trong 35 DbSet đã kiểm tra                                                                                                                                                            | Nhận schema/contract SCRUM-5 trước khi quyết định nguồn nạp/thu; không khẳng định `top_ups` đã có                                                                                                                             | Theo phần Minh, chưa tự thiết kế bảng mới ở đây                                                               | Minh                                                                                       |
| Các bảng giải/team/khác                                   | Giữ nguyên schema ngoài phạm vi                                                                                                                                                                     | Chỉ đối chiếu nguồn thu/hoàn nếu báo cáo dùng                                                                                                                                                                                 | Không                                                                                                         | Owner tương ứng                                                                            |

Mapping giữa schema hiện tại và contract:

- Product.Stock, Product.Price, Product.ProductId lần lượt map sang stockQuantity, currentPrice, id trong DTO; không tạo cột trùng chức năng.
- Promotion.PromoName map sang name trong DTO; PromotionUsage là entity thêm mới, lưu trong bảng PromotionUsages.
- Order.GamingSessionId tham chiếu phiên chơi; OrderDetail.UnitPrice giữ đơn giá lúc đặt.
- EmployeeId tham chiếu nhân viên thực hiện; không tạo thêm cột người thực hiện trùng chức năng với EmployeeId, không dùng CustomerId thay EmployeeId.
- InvoiceOrder liên kết hóa đơn với các đơn được gộp.
  TotalAmount/Payment.Amount không tự map thành PaidAmount nếu chưa đối chiếu nguồn thu/hoàn.

Baseline CyberCafe.Api 19 bảng không phải schema triển khai đích. Không dùng InitialCreate hoặc
DBML cũ làm căn cứ tạo lại các bảng đã có. Giữ lịch sử 35 bảng và thêm migration tăng dần,
backfill có kiểm tra, không cast long sang int nếu vượt phạm vi hoặc không có mapping.

### 3.1. Kho

- Giữ Products.Stock int và IsActive; Stock >= 0. Đổi trạng thái bán không đổi tồn/giá.
- Giữ InventoryTransactions.InvTransId/ProductId/EmployeeId/Type/Quantity/ReferenceType/
  ReceiptId/OrderId. Bổ sung Before, After, Reason (trim 1..200), OrderDetailId theo thiết kế.
- EmployeeId FK Employees khi nhân viên thực hiện. Giao dịch bắt nguồn khách/hệ thống:
  cần migration cho EmployeeId nullable nếu không có nhân viên thật; truy nguồn Customer qua Order.
  Không giả mạo nhân viên, không lưu CustomerId vào EmployeeId, không tạo Account.
- After = Before + Quantity; trước/sau không âm. DTO quantityDelta map Quantity,
  stockAfter map After, orderItemId map OrderDetailId, employeeId map EmployeeId,
  recordedAt map CreatedAt sau khi đã xác định múi giờ.
- Receive dương, OrderDetailId null, có thể truy ReceiptId; Adjust khác 0, OrderDetailId null.
  Sale âm/Return dương có OrderDetailId và nguồn OrderId nhất quán.
- Chuẩn hóa Type và dấu Quantity phải dựa trên ý nghĩa và dữ liệu thực tế của từng nguồn nghiệp vụ;
  không đổi tên/đổi dấu hàng loạt khi chưa đối soát.
- Unique có điều kiện (OrderDetailId,Type) khi OrderDetailId khác null theo policy đặt/hủy cả dòng
  một lần; không dùng constraint này để tuyên bố đã hỗ trợ giao/hoàn nhiều lần cùng dòng.
- Không cascade xóa lịch sử. Receive từ phiếu nhập và API nhập phải dùng cùng nguồn,
  không để một phiếu được cộng tồn hai lần.

### 3.2. Voucher và lượt dùng

Promotions (schema logic; tên entity nguồn của Đại là Promotion):

- PromotionId int PK theo schema CyberManagement hiện tại.
- Code bắt buộc, trim, 1..50, chỉ gồm ASCII chữ/số/gạch ngang/gạch dưới, không phân biệt hoa/thường, unique;
  lưu dạng uppercase invariant.
- PromoName là tên cột đích (DTO: name); độ dài cuối theo cấu hình nguồn và validation được thống nhất,
  không tự tạo PromotionName trùng cột.
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

Snapshot trên OrderDetail:
Giữ OrderDetail.UnitPrice làm giá tại lúc đặt; không thêm cột giá snapshot trùng chức năng.
Chỉ bổ sung tên món snapshot và DiscountAmount cho toàn dòng (không phải mỗi đơn vị).
0 <= DiscountAmount <= Quantity \* UnitPrice.
Tổng giảm các dòng bằng giảm đơn; kiểm tra trong nghiệp vụ, không dùng CHECK liên bảng.
Tên món snapshot còn phải bổ sung trong code nguồn khi triển khai; không đọc tên hiện tại để in đơn cũ.

### 3.3. Hóa đơn

Giữ mô hình Invoice, InvoiceOrder và InvoiceDetail của nền đích. Một hóa đơn có thể chứa một hoặc nhiều đơn;
GamingSessionId vẫn nullable như nguồn. Nguồn đơn được liên kết qua InvoiceOrders.

- InvoiceId, CustomerId, EmployeeId, GamingSessionId, PromotionId dùng int/int? theo entity nguồn.
- EmployeeId là nhân viên chốt/phát hành; không tạo AccountId. Không phát hành chứng từ nhân viên
  bằng cách giả CustomerId là EmployeeId. Khách xem dữ liệu của mình, không tự phê duyệt phát hành.
- InvoiceOrder lưu cặp (InvoiceId, OrderId), khóa chính kép và hai FK như configuration Đại.
  Tất cả đơn gộp phải cùng CustomerId với Invoice; nếu có GamingSessionId thì đơn phải thuộc phiên đó.
- Hóa đơn có ít nhất một nguồn: đơn được nối hoặc phí phiên; có thể chứa cả phí phiên và món.
  Không tính một dòng món/phí phiên lần hai khi tính tổng.
- Khóa chính kép nguồn chỉ chặn trùng cùng cặp; chưa ngăn một OrderId nằm ở hai hóa đơn khác nhau.
  Yêu cầu nghiệp vụ: không ghi nhận cùng đơn/phí phiên vào hai hóa đơn phát hành.
  Khi triển khai phải kiểm soát nguồn dưới khóa transaction và bổ sung unique nguồn phù hợp;
  constraint này thuộc phần bổ sung trong migration triển khai.
- Giữ các field nguồn: Subtotal, Discount, TotalAmount, InvoiceDate, Status và navigation InvoiceOrders.
  Các thay đổi field hoặc kiểu dữ liệu được thực hiện bằng migration có đối soát.
- Các giá trị đọc grossAmount/refundAmount/paidAmount là định nghĩa nghiệp vụ, cần mapper/nguồn
  thu-hủy-hoàn tin cậy hoặc field bổ sung khi triển khai. Gross = subtotal - discount tại phát hành,
  refund là giá trị hủy sau giảm đã chốt, paid là số thực thu ròng; không suy paid từ TotalAmount.
- Các cột tiền Invoice hiện cấu hình decimal(12,2); kiểu đích là decimal(18,0) theo mục 2.3.
  Trường bổ sung migration sau khi đối soát dữ liệu. Quy tắc tương tự áp dụng các cột tiền khác;
  riêng Promotion.DiscountValue giữ decimal(18,2) và kiểm tra Fixed nguyên đồng.
- Trạng thái lưu nguồn là Unpaid/Paid/Cancelled theo entity Đại. Hợp đồng API dùng Pending
  thì mapper chuyển Unpaid -> Pending rõ ràng; không coi schema hiện tại đã lưu Pending.
- Giá trị hủy/hoàn và trạng thái theo toàn hóa đơn gộp: hủy một đơn không làm Cancelled
  nếu còn nguồn hợp lệ khác; hóa đơn 0 đồng do voucher 100% vẫn có thể hợp lệ.
- Gateway đang chờ hoàn được theo dõi ở đơn/payment. Chỉ cập nhật số tiền đã chốt khi kết quả
  hoàn được xác minh; không giữ SQL transaction khi chờ provider.

InvoiceDetail giữ Description/Quantity/UnitPrice/Amount làm snapshot; thêm OrderDetailId và
SessionChargeId kiểu int? với FK NoAction. Dòng món có OrderDetailId và SessionChargeId null;
dòng phí phiên có SessionChargeId và OrderDetailId null. Với các loại dòng thuộc contract này,
CHECK bảo vệ XOR nguồn; nghiệp vụ kiểm tra loại dòng, cùng khách và cùng phiên tương ứng hóa đơn.
Thêm unique filtered index trên SessionChargeId IS NOT NULL và OrderDetailId IS NOT NULL để
một khoản phí/dòng món không xuất hiện lặp trong chứng từ. InvoiceOrders có unique index OrderId,
vừa chặn một đơn vào hai hóa đơn vừa làm điểm truy nguồn nhanh. Dữ liệu cũ phải backfill và xử lý
trùng trước index; chưa khẳng định các cột/index này đã có trong nền.

Callback theo OrderId tra InvoiceOrders.OrderId; callback theo SessionChargeId tra
InvoiceDetails.SessionChargeId, từ đó lấy InvoiceId duy nhất. Không dùng Description hoặc chọn
một hóa đơn bất kỳ cùng khách/phiên. Giảm phân bổ đọc từ snapshot, không đọc tên/giá/voucher mới
để in chứng từ cũ. Trường bổ sung cột/FK/index chứng từ trong task triển khai sau khi đối chiếu Khang/Phúc.
Voucher vẫn áp theo từng đơn. Một hóa đơn gộp có thể gồm các đơn dùng voucher khác nhau;
Invoice.PromotionId không được dùng để áp lại một voucher cho toàn hóa đơn hoặc tiền chơi.
Tổng giảm hóa đơn bằng tổng giảm đã lưu trên các đơn/dòng; tiền chơi không được giảm bởi voucher món.

Khi bổ sung snapshot và ràng buộc cho hóa đơn, phải đối soát dữ liệu hiện có trước khi áp migration.
InvoiceDate được dùng làm thời điểm phát hành (issuedAt qua mapper), không đổi thành ngày thu.

## 4. Quy tắc voucher

1. Chỉ tiền món ăn, một voucher/đơn; không giảm tiền chơi hoặc nạp ví.
2. Hợp lệ khi IsActive và StartDate <= now < EndDate; backend dùng một mốc now cho thao tác.
3. Kiểm tra ngưỡng trên tổng tiền món trước giảm.
4. Giảm thô: Percent = subtotal \* value / 100; Fixed = value.
   Áp trần nếu MaxDiscountAmount khác null, chặn không vượt subtotal,
   làm tròn nguyên đồng (0,5 lên; MidpointRounding.AwayFromZero), kiểm tra lại 0..subtotal.
5. Ghi lượt khi tạo đơn thành công kể cả cash chưa thu; thao tác lỗi không chiếm lượt.
   Kiểm tra trước/preview không ghi lượt và không giữ lượt; hạn mức được kiểm tra lại lúc đặt trong transaction.
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

### 5.1. API kho, hóa đơn và báo cáo theo permission đích

Bảng dưới quy định endpoint đích, dữ liệu và quyền bắt buộc. Trạng thái triển khai API
được nghiệm thu trong task chức năng tương ứng.

| Method + route                                    | Policy bắt buộc                                | Request                             | Thành công                      |
| ------------------------------------------------- | ---------------------------------------------- | ----------------------------------- | ------------------------------- |
| GET /api/admin/inventory                          | permission:stockreceipt.view                   | Không body                          | 200: item[]                     |
| GET /api/admin/inventory/movements                | permission:stockreceipt.view                   | Không body                          | 200: tối đa 500, mới nhất trước |
| POST /api/admin/inventory/movements, type Receive | permission:stockreceipt.create                 | productId,type,quantityDelta,reason | 201: movement                   |
| POST /api/admin/inventory/movements, type Adjust  | permission:inventory.adjust                    | productId,type,quantityDelta,reason | 201: movement                   |
| PUT /api/admin/products/{id}/status               | permission:product.manage                      | isActive bắt buộc bool              | 200: item                       |
| GET /api/admin/invoices                           | permission:invoice.view                        | Không body                          | 200: tối đa 500, mới nhất trước |
| GET /api/client/invoices                          | role Customer, `customer_id` hợp lệ, ownership | ID khách từ token                   | 200: tối đa 200 của mình        |
| GET /api/admin/reports                            | permission:report.view                         | from/to tùy chọn YYYY-MM-DD         | 200: báo cáo theo nguồn thực    |

POST movements là một route; server xác thực và kiểm tra quyền theo type đã validate:
Receive cần stockreceipt.create, Adjust cần inventory.adjust. Không kiểm tra chỉ một quyền rồi cho
client đổi type để thực hiện nhánh khác. Sale/Return không nhận từ endpoint nhập/điều chỉnh.

Chốt cho API quản trị kho: stockreceipt.view cho danh sách tồn và sổ biến động;
product.view chỉ cho chức năng xem sản phẩm, không tự cấp quyền xem sổ kho.
Sử dụng các permission hiện có trong catalog SCRUM-2.

item = {id,name,stockQuantity,isActive,currentPrice,categoryId}.
movement = {id,productId,type,quantityDelta,stockAfter,reason,orderItemId,employeeId,recordedAt}.

invoice = {id,customerId,orderIds,gamingSessionId,grossAmount,refundAmount,paidAmount,status,issuedAt}.
orderIds lấy từ InvoiceOrders; có thể rỗng khi chỉ có phí phiên, không chọn tùy ý một đơn để trả.
InvoiceId/InvoiceDate map id/issuedAt; các số tiền thu-hủy-hoàn cần nguồn thực mục 3.3/7.
Unpaid của entity map Pending ở DTO, không tự đổi dữ liệu lưu sang Pending.

Danh sách rỗng trả []; delta khác 0 trong [-1000000,1000000], tồn 0..int.MaxValue.
Danh sách hóa đơn xếp InvoiceDate giảm dần rồi InvoiceId giảm dần.
Customer chỉ xem hóa đơn theo CustomerId token, không tự truyền ID khách khác.

### 5.2. API voucher

| Method + route                      | Quyền            | Kết quả                                 |
| ----------------------------------- | ---------------- | --------------------------------------- |
| GET /api/admin/vouchers             | promotion.view   | 200: voucher[]                          |
| GET /api/admin/vouchers/{id}        | promotion.view   | 200: voucher                            |
| POST /api/admin/vouchers            | promotion.manage | 201: voucher, Location tới GET chi tiết |
| PUT /api/admin/vouchers/{id}        | promotion.manage | 200: voucher                            |
| PUT /api/admin/vouchers/{id}/status | promotion.manage | 200: voucher; body {isActive}           |
| POST /api/client/vouchers/preview   | Customer         | 200: kết quả giảm cho giỏ của mình      |

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

Response voucher = body trên + id; name map Promotion.PromoName; không công khai danh sách khách đã dùng.
PUT gửi đủ các field, không lấy thiếu field/null làm cập nhật ngầm.

Preview nhận {code,items:[{productId,quantity}]}; quantity là số nguyên dương.
Server nạp giá/sản phẩm, tính subtotal và lấy CustomerId từ auth; không nhận tổng/giảm do client tính.
Response {code,subtotal,discountAmount,totalAmount}; subtotal - discountAmount = totalAmount.
Preview không tạo Order/Usage/Invoice và không giữ lượt; checkout phải kiểm tra lại trong transaction.
Checkout của Khang gọi rule nội bộ, không gọi HTTP preview giữa transaction để tin kết quả cũ.
Tạo voucher chuẩn hóa Code (trim, uppercase invariant) theo quy tắc tại mục 3.2. Mọi thay đổi ảnh hưởng caller phải được cập nhật thống nhất trong contract trước khi triển khai.

Lỗi chung dùng ProblemDetails: status/title/detail/traceId; validation có errors theo field.

- 400: JSON/ID/kiểu tiền/ngày hoặc DTO sai.
- 401: chưa đăng nhập. 403: thiếu quyền hoặc không sở hữu tài nguyên.
- 404: không có ID voucher/sản phẩm/hóa đơn; riêng preview, mã voucher không tồn tại cũng trả 404.
- 409: trùng mã, voucher tắt/hết hạn/chưa hiệu lực/thiếu ngưỡng/hết lượt,
  trạng thái đơn không cho phép, hoặc thay đổi đồng thời.
- 500/503: lỗi hệ thống/DB theo handler chung, không giả lập thành công.

Chi tiết/tìm/lọc/in/xuất hóa đơn thuộc S2-T-2/S3-T-2, cần contract bổ sung dựa trên UI.
Hai API danh sách hiện tại chưa đáp ứng các tiêu chí đó; không tự coi là đã triển khai đủ.

### 5.3. Ma trận quyền cần nghiệm thu khi triển khai

Mỗi policy có prefix permission: khi đăng ký ASP.NET. Tên claim permission và giá trị
phải lấy từ catalog/role-permission server; không chỉ kiểm tra tên role hoặc ẩn nút UI.

| Vai trò seed | API kho quản trị                                                    | Hóa đơn quản trị                           | Voucher                                   | Thanh toán quản trị                                                                             |
| ------------ | ------------------------------------------------------------------- | ------------------------------------------ | ----------------------------------------- | ----------------------------------------------------------------------------------------------- |
| Admin        | Theo permission mục 5.1                                             | invoice.view/create/void                   | promotion.view/manage                     | payment.create và payment.refund: xác nhận thu, xác nhận hoàn cash                              |
| Manager      | Theo permission mục 5.1                                             | invoice.view/create/void                   | promotion.view/manage                     | payment.create và payment.refund: xác nhận thu, xác nhận hoàn cash                              |
| Cashier      | Không có stockreceipt.view/create, inventory.adjust, product.manage | invoice.view/create; không có invoice.void | promotion.view; không có promotion.manage | payment.create: được xác nhận thu; không có payment.refund, không được xác nhận hoàn cash       |
| Waiter       | Không xem sổ kho; product.view chỉ xem sản phẩm                     | Không có invoice.view/create/void          | Không có promotion.view/manage            | Không có payment.create hoặc payment.refund                                                     |
| Technician   | Từ chối endpoint kho                                                | Từ chối hóa đơn quản trị                   | Từ chối voucher quản trị                  | Không có payment.create hoặc payment.refund                                                     |
| Customer     | Từ chối endpoint quản trị                                           | Chỉ API hóa đơn của mình                   | Preview giỏ của mình                      | Không có hai permission nhân viên; checkout/ví của mình dùng quyền sở hữu và nghiệp vụ được cấp |

Đã đối chiếu RolePermissionCatalog của nền a975f8d: payment.create/payment.refund có trong
catalog và PermissionDescriptions, được IdentitySeeder dùng để đồng bộ. Không yêu cầu Đại seed
permission mới trong nền đã kiểm tra. Admin/Manager xác nhận hoàn cash theo payment.refund;
Cashier có thể chuyển yêu cầu cho người có quyền, không được tự xác nhận hoặc mượn danh tính.
Callback đã xác minh là xử lý server của giao dịch đã được khởi tạo hợp lệ, không yêu cầu token
nhân viên từ provider và không dùng callback để bỏ qua quyền khởi tạo/duyệt hoàn.

Đây là quyền của role seed tại catalog đã kiểm tra, không hứa role luôn có mọi quyền trên sau khi cấu hình thay đổi.
Quyết định cuối khi gọi API là principal hợp lệ và permission thực được cấp.
401 khi chưa xác thực; 403 khi thiếu quyền/ownership. Manager được quản lý voucher là trường hợp
bắt buộc kiểm tra. Cashier/Waiter/Technician gửi trực tiếp request POST/PUT không được vượt quyền.

Ma trận trên là tiêu chí kiểm thử module khi triển khai. Phạm vi kiểm chứng hiện có được ghi tại mục 10.

## 6. Điểm gọi nội bộ và phát hành hóa đơn gộp

Contract logic dưới đây để triển khai sau, chưa phải interface có sẵn trên nguồn Đại.
Trường cung cấp rule kho/voucher/chứng từ; Khang/Phúc gọi trong nghiệp vụ của họ.

| Điểm gọi                          | Input server tin cậy                                                                | Output/hành vi                                                                 |
| --------------------------------- | ----------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| Inventory.ApplySale / ApplyReturn | orderDetailId, productId, quantity, EmployeeId nếu là nhân viên, operationKey       | Tồn và lịch sử cùng transaction                                                |
| Voucher.ValidateAndUse            | đơn đã tính giá, code, CustomerId từ auth                                           | Snapshot giảm, phân bổ từng dòng, một usage/đơn                                |
| Voucher.ReleaseForCancelledOrder  | orderId, nguồn hủy đã xác minh                                                      | Hủy toàn đơn mới hoàn lượt đúng một lần                                        |
| Invoice.IssueForOrders            | danh sách orderIds, CustomerId, EmployeeId từ principal nhân viên, operationKey     | Một hóa đơn và các InvoiceOrder; trả lại kết quả cũ khi retry cùng thao tác    |
| Invoice.IssueForSession           | sessionId đã kết thúc, các đơn chưa lên hóa đơn được chọn, EmployeeId, operationKey | Phí phiên và đơn được chọn lên cùng chứng từ; không đưa lại nguồn đã phát hành |
| Invoice.ApplyFinancialChange      | nguồn thu/hủy/hoàn đã xác minh, operationKey                                        | Cập nhật phần tiền liên quan trong hóa đơn, không cộng/trừ hai lần             |

- Nhân viên chốt/phát hành hóa đơn khi nghiệp vụ yêu cầu, theo tập đơn/phiên được chọn.
  Không tự tạo một hóa đơn cho từng lần đặt rồi lại gộp chúng; phát hành theo lần chốt tập nguồn đã chọn.
  Nguồn lưu khoản thu trước phát hành đã được owner Trường chốt về thiết kế tại mục 6.1;
  quy tắc liên kết hóa đơn và chống thu/hoàn lặp được chốt thiết kế tại mục 6.2–6.4.
  Schema nền chưa có các thay đổi này; xác nhận liên module còn chờ mục 6.5.
- Đơn trả ví trước khi chốt vẫn giữ debit và snapshot; phát hành không trừ ví thêm lần nữa.
  Cash chưa thu là Unpaid (API Pending); thu được xác minh mới Paid.
  Gateway chưa xác minh không Paid. Phiên dùng phí đã tính, không tự thu phí lần hai.
- Hóa đơn gộp chỉ Paid khi tổng phải trả còn lại đã thanh toán đủ theo nguồn thực.
  Khi đã phát hành không tùy ý thêm/bớt đơn để đổi snapshot; hủy/hoàn có truy vết riêng.
- Nhân viên phải có permission invoice.create; khách xem hóa đơn theo CustomerId của token.
  Tác vụ tự động không giả nhân viên: chỉ thực hiện khi có cơ chế actor hệ thống được nhóm định nghĩa.
- Caller mở transaction/DbContext chung; module con không tự commit hoặc mở transaction lồng.
  Kiểm tra cùng khách/cùng phiên trước khi nối; khóa các đơn theo ID tăng dần và kiểm tra nguồn
  đã được lập chứng từ trước khi tạo InvoiceOrder. Mọi caller theo thứ tự khóa mục 6.2.1.
- Retry phát hành dùng operationKey ổn định và kiểm tra danh sách nguồn; không tạo chứng từ mới
  từ cùng lần chốt. Số tiền/EmployeeId/CustomerId không nhận tùy ý từ browser.
- Tên trường và trạng thái Payment đích theo mục 6.1. Minh đối chiếu ledger/gateway và mapping dữ liệu cũ;
  các bên phải đối chiếu quy tắc tại mục 6.2–6.4, thống nhất thứ tự khóa liên module và giao nhận trước triển khai.
- Hủy toàn bộ một đơn chỉ hoàn lượt voucher của đơn đó, không hoàn lượt các đơn khác cùng hóa đơn.
- IssuedAt map InvoiceDate, không thay bằng ngày thu; SignalR chỉ phát sau commit.

### 6.1. Payments neo theo nguồn, hóa đơn liên kết sau

**Owner Trường chốt thiết kế ngày 05/11/2026:** mở rộng Payments để lưu khoản thu trước hóa đơn;
liên kết hóa đơn sau mà không thu lại; khoản hoàn là dòng riêng trong cùng bảng, tham chiếu khoản thu gốc.
Không tạo PaymentRefunds hoặc PaymentAllocations trong phạm vi phương án này. Cash thu gộp được
phân thành từng dòng theo nguồn. Không tách một giao dịch ZaloPay đã thu thành nhiều Payment dùng cùng mã gateway.

**Hiện trạng:** Payments.InvoiceId int NOT NULL, PaymentTime là DateTime bắt buộc; các field
nguồn/khóa/hoàn bên dưới chưa có. Đây là thiết kế đích cần migration mới, không phải schema đã áp dụng.
Quyết định owner không thay cho phản hồi xác nhận của Minh/Khang/Phúc hoặc duyệt của Trưởng nhóm.

| Trường                          | Thiết kế đích                                    | Ý nghĩa / ràng buộc                                                                                                        |
| ------------------------------- | ------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------- |
| PaymentId                       | int, PK hiện có                                  | Định danh dòng thu hoặc hoàn                                                                                               |
| InvoiceId                       | int?, FK Invoices                                | Nullable trước phát hành; liên kết sau với hóa đơn chứa đúng nguồn                                                         |
| OrderId                         | int?, FK Orders                                  | Nguồn đơn món; đơn có phiên vẫn dùng OrderId, không gắn thêm nguồn phí phiên                                               |
| SessionChargeId                 | int?, FK SessionCharges.ChargeId                 | Nguồn một lần tính phí phiên; truy GamingSessionId qua SessionCharge, không dùng ID phiên thay ID khoản phí                |
| WalletTransactionId             | int?, FK WalletTransactions                      | Liên kết duy nhất ledger debit/credit tương ứng khi Method=Wallet                                                          |
| Type                            | Payment / Refund                                 | Phân biệt thu và hoàn, độc lập với Status                                                                                  |
| RefundOfPaymentId               | int?, FK Payments.PaymentId                      | Null khi Type=Payment; bắt buộc khi Type=Refund, tham chiếu khoản thu gốc                                                  |
| OrderDetailId                   | int?, FK OrderDetails                            | Chỉ dùng cho Refund do hủy dòng món; phải thuộc OrderId của khoản thu gốc                                                  |
| Amount                          | decimal(18,0), > 0                               | Số tiền nguyên đồng, luôn dương; Type quyết định chiều tiền, không ghi đè Amount của khoản thu gốc                         |
| Method                          | Wallet / Cash / ZaloPay                          | Phương thức thu/hoàn; hoàn theo phương thức khoản thu gốc                                                                  |
| Status                          | Pending / Succeeded / Failed / Expired           | Trạng thái xử lý; Expired dành cho yêu cầu thu hết hạn, không dùng để tự kết luận gateway chưa thu                         |
| OperationKey                    | Đích nvarchar(100) NOT NULL, unique sau backfill | Migration tạm nullable + unique filtered khi còn dữ liệu cũ; mọi ghi mới bắt buộc có khóa; retry nghiệp vụ giữ nguyên khóa |
| MerchantTransactionId           | nvarchar(40)?                                    | `app_trans_id`: tài liệu Create Order xác nhận string(40); Refund lấy mã thu qua RefundOfPaymentId                         |
| GatewayTransactionId            | nvarchar(50)? theo lựa chọn lưu trữ nội bộ       | `zp_trans_id` được callback mô tả long/int64; 50 không phải giới hạn do ZaloPay công bố; không ép về int hoặc cắt mã       |
| Mã yêu cầu/kết quả hoàn gateway | Lưu trong RefundAttempts ở mục 6.4.1             | Một Refund nghiệp vụ có thể có nhiều mã thử gửi qua ngày; không ghi đè mất mã cũ trong Payments                            |
| Reason                          | nvarchar(250)?                                   | Bắt buộc có lý do đã trim khi Type=Refund                                                                                  |
| CreatedAt                       | datetimeoffset, bắt buộc                         | Thời điểm tạo yêu cầu thu/hoàn nội bộ                                                                                      |
| PaymentTime                     | datetimeoffset?                                  | Mốc ghi nhận dùng cho báo cáo theo mục 6.1.1; chỉ có khi Succeeded, đọc cùng PaymentTimeBasis                               |
| ConfirmedAt                     | datetimeoffset?                                  | Thời điểm hệ thống ghi nhận Succeeded trong transaction thành công đầu tiên, không thay khi retry                          |
| ProviderOccurredAt              | datetimeoffset?                                  | Thời điểm giao dịch do provider cung cấp và được xác minh; nullable khi response không có trường này                       |
| PaymentTimeBasis                | nvarchar(20)?, Source / Confirmation              | Source: có thời điểm nguồn; Confirmation: dùng ConfirmedAt khi thiếu thời điểm provider; không gắn nhãn là giờ thực hoàn   |

Ràng buộc được phân theo nơi thực thi, không gộp kiểm tra chéo bảng vào CHECK:

**CHECK nội bảng Payments:**

- Amount > 0; Type/Method/Status thuộc tập cho phép; OperationKey không null/rỗng ở schema đích.
- Đúng một nguồn OrderId hoặc SessionChargeId khác null; InvoiceId không thay thế nguồn nghiệp vụ.
- Type=Payment: RefundOfPaymentId và OrderDetailId null. Type=Refund: RefundOfPaymentId không null,
  khác PaymentId, Reason không null và sau trim còn nội dung; không dùng trạng thái Expired.
- Succeeded có PaymentTime, ConfirmedAt và PaymentTimeBasis hợp lệ; trạng thái khác để cả ba null.
  Basis=Confirmation yêu cầu PaymentTime=ConfirmedAt. Expired chỉ dành cho Type=Payment.
- Method=Wallet và Succeeded phải có WalletTransactionId. Thu ZaloPay phải có MerchantTransactionId,
  và khi Succeeded có GatewayTransactionId. Các điều kiện này chỉ xét cột cùng dòng, chưa chứng minh
  nội dung ledger hoặc dữ liệu provider hợp lệ.

**FK và index:**

- FK đến Orders, SessionCharges, WalletTransactions, Invoices và Payment gốc bảo vệ bản ghi tham chiếu
  tồn tại; dùng NoAction để giữ lịch sử. Unique index theo mục 6.2 bảo vệ khóa/mã không trùng.
- FK không xác nhận cùng khách, cùng phương thức, đúng Type của Payment gốc hoặc nguồn thuộc hóa đơn.

**Service trong cùng transaction và theo thứ tự khóa mục 6.2.1:**

- Kiểm tra hóa đơn cùng khách và chứa đúng nguồn; OrderDetailId thuộc đúng đơn nếu hoàn theo dòng.
- Refund tham chiếu Payment gốc Type=Payment đã Succeeded, cùng nguồn/khách/Method; không hoàn
  từ một dòng Refund hoặc khoản chưa thu thành công. Kiểm tra tổng đã hoàn và Pending dưới khóa gốc.
- Đối chiếu ledger đúng nguồn, chiều/số tiền và phương thức. Refund ZaloPay phải có RefundAttempt
  đã lưu trước gửi; chỉ ghi Succeeded khi query xác minh attempt của đúng Refund/app/giao dịch/số tiền.
  Đây là kiểm tra chéo bảng/nguồn ngoài, không dùng CHECK để gọi sang RefundAttempts hoặc gateway.
- Khi nhận kết quả đã xác minh, ghi trạng thái, liên kết hóa đơn/ledger và audit cần thiết nguyên tử;
  lỗi lưu ở bất kỳ phần nào phải rollback transaction, không trả thành công giả.

Không tạo dòng 0 đồng: đơn voucher 100% có thể phát hành hóa đơn hợp lệ mà không có khoản thu.
Không hard-delete giao dịch. Dữ liệu cũ phải backfill nguồn, Type, OperationKey, mốc thu có bằng chứng
và mã provider trước constraint; thiếu đối ứng phải đối soát, không giả nguồn hoặc bỏ FK để chạy qua.

Riêng ví, chọn một phương án triển khai thống nhất: mỗi debit/credit dịch vụ có một dòng
Payment/Refund đối soát, liên kết bằng WalletTransactionId unique và ghi trong cùng transaction.
WalletTransactions vẫn là sổ tiền có thẩm quyền của ví. Minh cung cấp OperationKey bền vững và
nguồn đơn/khoản phí cho ledger dịch vụ; có thể truy ngược FK qua dòng Payment liên kết, không dựa
Description. Các nguồn nạp ví/giải ngoài phạm vi không bị ép thành đơn món hoặc SessionCharge.
Không chuyển tiền trực tiếp trên Customers mà bỏ ledger hoặc chỉ tạo dòng đối soát khi đã phát hành hóa đơn.

#### 6.1.1. Thời điểm nguồn và thời điểm xác nhận khi query hoàn

**Bổ sung thiết kế sau review, chờ Trưởng nhóm duyệt và Minh đối chiếu adapter.**
Response được công bố của [Query Refund](https://docs.zalopay.vn/docs/specs/order-query-refund/)
chỉ có mã/thông báo trạng thái; không có thời điểm thực hoàn. Không lấy timestamp của request,
CreatedAt của Refund hoặc tiền tố ngày trong mã hoàn làm thời điểm thực hoàn. Query thành công
vẫn phải được ghi nhận dù thiếu thời điểm này; không giữ Refund Pending chỉ để chờ một trường
provider không trả về. Quy tắc đích:

Basis trong các mô tả dưới đây là viết tắt của cột PaymentTimeBasis, không phải một cột khác.

| Nguồn | PaymentTime và PaymentTimeBasis khi lần đầu ghi Succeeded |
| --- | --- |
| Wallet | Mốc của ledger debit/credit đã đối soát, Basis=Source; ConfirmedAt là lúc hệ thống ghi nhận cùng transaction |
| Cash | Mốc nhân viên đủ quyền xác nhận thực thu/hoàn tại server, Basis=Source; không nhận ngày tùy ý từ browser |
| ZaloPay thu có thời điểm giao dịch hợp lệ | ProviderOccurredAt từ server_time đã xác minh của Callback/Query Order, PaymentTime bằng thời điểm này, Basis=Source |
| ZaloPay hoàn, hoặc kết quả đã xác minh thiếu thời điểm giao dịch | ProviderOccurredAt null, PaymentTime=ConfirmedAt, Basis=Confirmation; đây là ngày hệ thống xác nhận, không phải khẳng định ngày thực hoàn |

ConfirmedAt do server gán UTC khi chuyển Succeeded trong transaction; chỉ có giá trị sau khi
transaction commit. Nếu rollback, lần xử lý thành công sau gán lại; retry sau commit giữ nguyên
ConfirmedAt/PaymentTime/Basis. FK/ledger và kết quả provider vẫn phải được xác minh theo mục 6.1.
Service kiểm tra Basis=Source có nguồn thời gian đúng loại: ledger/cash hợp lệ hoặc timestamp
provider hợp lệ; không dùng CHECK liên bảng để thay việc đối chiếu này.

Báo cáo và DTO tiền phải cung cấp paymentTime, confirmedAt, providerOccurredAt, paymentTimeBasis.
Lọc ngày theo PaymentTime ở +07:00; hiển thị rõ các khoản Basis=Confirmation và số lượng/tổng tiền
của chúng khi đối soát, để người đọc biết thiếu ngày giao dịch từ provider. Số đã thu/hoàn và
PaidAmount vẫn tính đúng một lần theo Status/Type, không phụ thuộc việc có thời điểm provider.
Ví dụ Refund được tạo 05/11 lúc 23:58, query xác nhận thành công 06/11 lúc 00:03 (+07:00), không có
giờ thực hoàn: PaymentTime=ConfirmedAt tại 06/11 00:03, Basis=Confirmation; ghi giảm theo ngày
06/11 và không ghi đây là bằng chứng tiền đã hoàn ngày 05/11 hay đúng 00:03 bên provider.

Nếu có bằng chứng thời điểm provider đến sau, chỉ bổ sung ProviderOccurredAt có audit và nguồn
chứng minh; không âm thầm đổi PaymentTime/Basis/ConfirmedAt hoặc tổng báo cáo đã ghi nhận.
Điều chỉnh kỳ báo cáo cần quy trình đối soát do người có quyền phê duyệt; ghi nhận thời gian mới
không tạo thêm Refund/ledger hoặc hoàn lại tiền. Dữ liệu cũ chỉ backfill từ bằng chứng có thật;
không dùng thời điểm chạy migration làm ConfirmedAt của giao dịch lịch sử.

### 6.2. Ghi thu, khóa chống lặp và cash thu gộp

- OperationKey unique toàn bảng và bắt buộc cho ghi mới. Filter IS NOT NULL chỉ là bước chuyển
  đổi cho dữ liệu cũ chưa backfill; đích cuối không cho caller bỏ khóa bằng null/rỗng.
- Cùng OperationKey/cùng nội dung trả lại kết quả cũ; khác nguồn, Method hoặc Amount trả 409.
  Khóa được suy từ thao tác nghiệp vụ ổn định, không lấy timestamp mới cho mỗi lần nhấn/retry.
  Đổi OperationKey không được vượt kiểm tra nghĩa vụ còn phải thu của cùng đơn/khoản phí.
- Unique index có lọc cho WalletTransactionId khác null; các dòng thu ZaloPay có unique theo
  (Method, MerchantTransactionId) và (Method, GatewayTransactionId) khi mã tương ứng khác null.
  Mã hoàn nằm ở RefundAttempts: unique MerchantRefundId theo ứng dụng merchant và unique GatewayRefundId
  khác null. Mỗi attempt chỉ thuộc một Refund; không dùng các index thu để ghi lại mã thu trên dòng hoàn.
  Không thêm unique (RefundOfPaymentId, OperationKey) vì OperationKey đã unique toàn bảng.
- Các mã gateway gắn đúng ứng dụng merchant đã cấu hình; nhận callback phải kiểm tra `app_id`,
  MAC/chữ ký, mã tham chiếu, số tiền và kết quả. Không coi unique index là thay thế xác minh callback.
- Tất cả đường ghi thu, phát hành, callback/query và hoàn tuân theo thứ tự khóa tại mục 6.2.1.
  Module con không tự mở transaction lồng hoặc commit. Kiểm tra/ghi trong transaction ngắn;
  không giữ khóa SQL khi gọi hoặc chờ gateway.
- Wallet: debit/ledger/Payment Succeeded ghi cùng transaction; phí phiên còn có SessionCharge và
  các thay đổi phiên liên quan. Khi không đủ tiền hoặc lưu lỗi thì rollback toàn bộ thao tác.
- Cash: chỉ ghi Succeeded sau khi nhân viên có payment.create xác nhận thực thu. Thu gộp nhiều đơn
  thì server phân số tiền còn phải thu thành các dòng, tổng bằng tiền đã nhận được xác nhận và
  lưu tất cả trong một transaction. Mỗi dòng có khóa con như cash:{batchId}:order:{orderId};
  retry giữ nguyên batchId, tập nguồn và số tiền. Không dùng cùng OperationKey cho nhiều dòng.
- ZaloPay: commit dòng Payment Pending và mã merchant gắn OrderId trước khi gọi provider;
  sau đó gọi ngoài SQL transaction. Callback/query cập nhật chính dòng đã có dưới khóa,
  không insert thêm khi nhận lại cùng giao dịch; Succeeded không ghi thu lại hoặc quay về Pending/Failed.
- Nếu process dừng sau khi lưu Pending nhưng trước/sau lời gọi gateway, phục hồi/đối soát bằng
  dòng cũ và mã merchant đã lưu. Không tự tạo mã mới hoặc ghi thành công chỉ từ việc đã gửi request.
- Timeout/hết hạn chờ không đủ để xác định chưa thu. Không mở yêu cầu thu mới cho cùng nghĩa vụ
  khi lần trước còn chưa đối soát. Nếu provider xác nhận thành công muộn, ghi nhận khoản thực thu
  và xử lý theo trạng thái nguồn tại mục 6.3–6.5; không bỏ qua tiền đã thu hoặc tự khôi phục đơn đã hủy.

#### 6.2.1. Thứ tự khóa dùng chung

Thứ tự thiết kế cho mọi thao tác tiền/chứng từ, chỉ lấy các nhóm cần dùng nhưng không đảo thứ tự:

```text
Customer (ví và chủ nguồn)
→ Computer → GamingSession → SessionCharge → Order (nguồn liên quan)
→ Product (tồn kho, ProductId tăng dần) → Promotion (nếu dùng voucher)
→ Payment gốc → Payment Refund → RefundAttempt
→ Invoice → InvoiceIssueRequest (chỉ khi phát hành) → AuditLogs (ghi audit nếu có)
```

Hủy đơn/món phải lấy toàn bộ Product liên quan theo ProductId tăng dần sau khi đã khóa nguồn Order,
trước Promotion/Payment. ApplySale/ApplyReturn và các writer tồn khác phải theo cùng thứ tự Product;
không giữ Product rồi quay ngược lấy Order/Customer. Nhập/điều chỉnh chỉ đụng tồn có thể bỏ các nhóm
không dùng, nhưng không được sau đó gọi module lấy khóa ở nhóm đứng trước. Nếu cần hoàn trong cùng
thao tác hủy, cập nhật trạng thái hủy, trả tồn và tạo Refund Pending hoặc hoàn ví cùng transaction SQL;
chỉ lời gọi gateway diễn ra sau commit, không giữ khóa tồn trong lúc chờ provider.

Trong từng nhóm khóa theo ID tăng dần; nhiều khách khóa theo CustomerId tăng dần. Đọc ID để
xác định tập khóa có thể làm trước, nhưng phải đọc lại/kiểm tra sau khi đã giữ khóa. Nếu tập nguồn
thay đổi thì rollback và retry lại toàn thao tác, không quay ngược để lấy thêm khóa phía trước.
Mọi đường cùng nguồn đều giữ Customer trước nguồn, kể cả callback và phát hành chỉ đọc ledger;
không chỉ áp dụng quy tắc này cho riêng đường debit ví. Khóa nguồn/Payment khi ghi dùng
UPDLOCK/HOLDLOCK hoặc cơ chế SQL tương đương đã được kiểm chứng; giữ đến commit/rollback.
Caller chưa có PaymentId/InvoiceId tạo mới chúng tại bước tương ứng sau khi khóa các nguồn hiện có.
Khoản mới sinh không phải lý do đảo thứ tự của các dòng cũ.

Worker không giữ SQL transaction trong thời gian chờ provider: ghi nhận/claim attempt rồi commit,
gọi gateway, sau đó mở transaction ngắn và lấy lại toàn bộ khóa theo thứ tự trên để ghi kết quả.
Minh/Phúc/Khang phải phản hồi nếu writer của họ dùng thứ tự khác; đây là thứ tự owner đề xuất
để tích hợp, chưa phải bằng chứng các writer hiện tại đã tuân thủ hoặc đã qua test cạnh tranh.

#### 6.2.2. Bảng chuyển trạng thái thu và hoàn

PaymentTime/ConfirmedAt/Basis chỉ được đặt khi ghi nhận Succeeded có căn cứ thu/hoàn.
Thời điểm query chỉ dùng làm mốc xác nhận theo mục 6.1.1 khi thiếu thời điểm provider;
phải ghi Basis=Confirmation, không coi đó là giờ giao dịch thực tế.
Các chuyển trạng thái không nằm trong bảng bị từ chối hoặc đưa vào đối soát, không cập nhật ngầm.

| Type    | Từ → sang                       | Điều kiện và tác động                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| ------- | ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Payment | Pending → Succeeded             | Wallet/cash đã ghi nhận hợp lệ hoặc ZaloPay được xác minh; ghi tiền, liên kết hóa đơn theo nguồn và đặt PaymentTime một lần                                                                                                                                                                                                                                                                                                                                                                                        |
| Payment | Pending → Failed                | Có kết quả thất bại có thẩm quyền; lỗi mạng/timeout không đủ để chuyển Failed                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| Payment | Pending → Expired               | Hết thời hạn chờ theo policy; chưa coi đây là bằng chứng provider không thu, không khôi phục đơn/giải phóng tiền đã nhận                                                                                                                                                                                                                                                                                                                                                                                           |
| Payment | Expired → Succeeded             | Provider xác nhận thu thành công muộn, đúng app/mã/số tiền; ghi nhận khoản thực thu, đối soát hóa đơn/đơn đã hủy theo mục 6.3                                                                                                                                                                                                                                                                                                                                                                                      |
| Payment | Failed → Succeeded, chỉ ZaloPay | Ngoại lệ sửa kết quả thu: không bắt buộc có callback. Nguồn xác nhận là kết quả Query Order do server gọi độc lập (kích hoạt bởi callback hợp lệ hoặc worker đối soát khi callback không đến lại), phải khớp `app_id`, `app_trans_id`/MerchantTransactionId, `zp_trans_id` và số tiền của Payment; callback chỉ là tín hiệu, không là bằng chứng duy nhất. Ghi AuditLogs theo quy tắc dưới bảng, chống lặp bằng khóa/unique mục 6.2, không cấp dịch vụ/thu lần hai. Wallet/Cash không tự đổi Failed sang Succeeded |
| Payment | Succeeded → Succeeded           | Retry/callback trùng là no-op về tiền; giữ mốc thực thu cũ. Succeeded không quay về Pending/Failed/Expired; hoàn là dòng Refund riêng                                                                                                                                                                                                                                                                                                                                                                              |
| Refund  | Pending → Succeeded             | Wallet/cash thực hoàn hợp lệ hoặc query gateway xác nhận hoàn thành; chuyển phần giữ chỗ thành đã hoàn, đặt PaymentTime và cập nhật hóa đơn một lần                                                                                                                                                                                                                                                                                                                                                                |
| Refund  | Pending → Failed                | Xác minh mọi attempt liên quan đã thất bại/không thực hiện và không còn attempt đang chờ hoặc chưa rõ; giải phóng phần giữ chỗ trong cùng transaction                                                                                                                                                                                                                                                                                                                                                              |
| Refund  | Pending → Pending               | Timeout/đang xử lý, query lỗi hoặc retry qua ngày còn chờ đối soát: giữ hạn mức, không ghi hoàn thành và không gửi mã mới tùy tiện                                                                                                                                                                                                                                                                                                                                                                                 |
| Refund  | Succeeded → Succeeded           | Retry/query trùng không hoàn thêm hoặc sửa PaymentTime; đây là trạng thái cuối                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| Refund  | Failed → Failed                 | Trạng thái cuối của yêu cầu nghiệp vụ đã thất bại; retry trả kết quả cũ, không tự tái giữ chỗ hoặc tự gửi hoàn                                                                                                                                                                                                                                                                                                                                                                                                     |

Payment Failed chỉ có ngoại lệ thu muộn ZaloPay đã được server xác minh ở trên; không tự đổi
Failed ↔ Expired hoặc quay về Pending. Refund không có Expired và không tự Failed → Succeeded.
Nếu query sau đó chứng minh đã thực hoàn trái với kết luận Failed, phải khóa khoản thu gốc,
giữ bằng chứng attempt và đưa vào đối soát có duyệt trước khi điều chỉnh tiền, không bỏ qua kết quả.

Ngoại lệ Payment Failed → Succeeded phải thêm AuditLogs trong cùng transaction với cập nhật tiền:
EntityType=Payment, EntityId=PaymentId, Action ghi ngắn gọn Failed→Succeeded, lý do LateVerifiedZaloPay,
mã giao dịch và nguồn query; ứng dụng gán CreatedAt bằng DateTime.UtcNow. Mapping AuditLogs đã đối chiếu
tại mục 3 và bằng chứng mục 10.1: CreatedAt là datetime2 NOT NULL, không có SQL default/constraint
bảo đảm UTC; writer chịu trách nhiệm ghi UTC. Dùng Action tối đa 255 ký tự,
không giả định có cột Details/JSON. Callback/worker hệ thống để EmployeeId null; thao tác nhân viên lấy
EmployeeId từ principal. Không ghi key/secret hoặc raw payload vào Action. Retry không đổi trạng thái
không tạo lại audit của cùng chuyển trạng thái; lỗi ghi audit làm rollback các cập nhật SQL của lần
điều chỉnh này. Rollback SQL không hoàn tác tiền đã chuyển ở provider: lần xử lý sau phải query lại
giao dịch cũ và ghi nhận kết quả thực, không gửi lại lệnh thu/hoàn. Để lần xử lý sau thật sự xảy ra cần
hai cơ chế: callback handler không xác nhận thành công khi transaction rollback (bảng phản hồi dưới đây),
và một worker đối soát định kỳ query các khoản thu Pending/Expired/Failed chưa chốt cùng Refund Pending
quá hạn. Chu kỳ, giới hạn số lần và nơi vận hành worker do Minh đề xuất; đây là yêu cầu triển khai, chưa
phải thành phần đã có. Toàn bộ cần kiểm thử transaction/retry, không phải hành vi đã được chứng minh
chỉ bằng entity/config/migration.

Phản hồi callback ZaloPay của merchant, chỉ xác nhận thành công sau commit hoặc khi đã xử lý trước đó:

| Tình huống                                                                                        | Phản hồi cho provider                                                                                           | Xử lý                                                                                                                     |
| ------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| Chữ ký `mac` sai hoặc dữ liệu không đến từ ZaloPay                                                | `return_code=2` (Invalid theo trang callback-api; mã mẫu ở knowledge-base dùng `-1`)                            | Không dùng dữ liệu làm căn cứ, không ghi Payment/audit tiền; log bảo mật không chứa key/secret; không coi là lỗi tạm thời |
| Chữ ký đúng, đã xử lý thành công và commit                                                        | `return_code=1`                                                                                                 | Chỉ trả sau commit                                                                                                        |
| Chữ ký đúng, khoản này đã xử lý trước đó (Succeeded, cùng `app_trans_id`, `zp_trans_id`, số tiền) | `return_code=1`, chỉ sau khi đối chiếu đúng giao dịch; không có mã riêng cho callback trùng                     | No-op về tiền, không ghi audit lần hai                                                                                    |
| Chữ ký đúng nhưng lỗi tạm thời (khóa, DB, timeout, rollback, lỗi ghi audit)                       | `return_code=0` (chỉ có trong mã mẫu knowledge-base, kèm chú thích gửi lại tối đa 3 lần; không có trong đặc tả) | Rollback, không báo thành công; worker đối soát chốt khi callback không đến lại                                           |
| Chữ ký đúng nhưng app/mã/số tiền không khớp Payment                                               | `return_code=2` (Invalid), không dùng mã gửi lại                                                                | Không ghi thu; lưu bằng chứng, đưa đối soát                                                                               |

Căn cứ ZaloPay (đọc bản EN ngày 05/11/2026): trang callback-api chỉ mô tả `return_code` 1 (Success) và
2 (Invalid), không có mã riêng cho callback trùng và không nêu cơ chế gửi lại. Trang knowledge-base ghi
1 (Success) và 2 (Failed); mã mẫu ở đó dùng `-1` cho `mac` sai và `0` kèm chú thích gửi lại tối đa 3 lần,
nhưng đó là chú thích trong mã ví dụ, không phải đặc tả. Trang này cũng khuyến nghị: nếu sau 15 phút kể từ
lúc tạo đơn vẫn chưa có callback thì chủ động gọi Query Order, và xử lý callback trùng theo hướng idempotent.
Vì vậy không dựa vào việc provider gửi lại: worker đối soát qua Query Order là cơ chế chính, lần query đầu
cho khoản chưa có callback sau 15 phút, còn mã `0` chỉ là tối ưu. Minh xác nhận bằng adapter/sandbox
trước khi coi bảng này là contract.

Trong retry gateway qua ngày, trạng thái thất bại của một RefundAttempt không tự làm Refund
nghiệp vụ Failed: nếu vẫn tiếp tục cùng yêu cầu hoàn thì Refund giữ Pending và hạn mức giữ nguyên.
Muốn khởi tạo yêu cầu hoàn nghiệp vụ mới sau Refund Failed phải có quyết định được cấp quyền,
khóa nghiệp vụ mới và kiểm tra lại quyền/hạn mức/nguồn hủy; không dùng retry kỹ thuật để hoàn lại.

### 6.3. Phát hành hóa đơn chỉ liên kết và đối soát

1. Caller xác minh quyền invoice.create, cùng khách/cùng phiên và tập nguồn chưa lên chứng từ khác.
   Khóa nguồn theo thứ tự chung, tạo Invoice/InvoiceOrders và các dòng snapshot trong cùng transaction.
2. Gắn InvoiceId cho các dòng thu/hoàn Succeeded có InvoiceId null thuộc tập nguồn đã chọn,
   sau khi đối chiếu ledger ví. Không đổi InvoiceId đã khác null sang hóa đơn khác.
3. Tính PaidAmount theo quy tắc một nguồn tại mục 7, không debit ví, nhận cash hoặc gọi tạo giao dịch
   gateway trong thao tác phát hành. Hóa đơn còn phải thu vẫn Unpaid (API Pending).
4. Retry cùng khóa phát hành và cùng tập nguồn trả hóa đơn cũ; khác nội dung trả 409.
   Khóa phát hành được lưu bền vững tại InvoiceIssueRequests theo mục 6.3.1, không chỉ giữ trong RAM
   hoặc suy từ Payments.OperationKey. Khóa nguồn và ràng buộc chống trùng phải chặn hai request
   tạo hóa đơn cho cùng đơn/phí phiên.

Không chỉ xử lý liên kết ở lúc phát hành: khi Payment Pending/Expired hoặc Failed theo ngoại lệ ở bảng trạng thái được provider xác nhận thu
thành công sau đó, callback/query phải khóa theo mục 6.2.1. Với OrderId tra InvoiceOrders qua
unique index OrderId; với SessionChargeId tra InvoiceDetails qua unique filtered index SessionChargeId,
lấy InvoiceId của nguồn đó, khóa Invoice cuối cùng rồi liên kết/tính lại số đã thu một lần. Dòng Refund mới thành công cũng theo đúng hóa đơn của khoản thu gốc;
nguồn chưa phát hành thì InvoiceId tiếp tục null. Dòng chờ chưa được tính vào PaidAmount.
Nếu phát hành và callback/hoàn chạy đồng thời, cùng chiến lược khóa nguồn phải bảo đảm hai bên
thấy kết quả đã commit của nhau; không để khoản đã thành công bị bỏ lại vô liên kết sau khi cả hai commit.

Trường hợp nguồn đã hủy, hóa đơn không còn nhận thu hoặc phát hiện một giao dịch khác đã trả đủ:
phải giữ bằng chứng khoản thu thực tế theo mã gateway, đưa vào đối soát/hoàn theo nguồn gốc;
không gắn vào hóa đơn khác, tự cộng ví, đánh dấu đã hoàn hoặc lặng lẽ cắt số tiền dư.
Policy thời hạn và tự động hay duyệt hoàn thuộc giao điểm gateway tại mục 6.5.

#### 6.3.1. Khóa phát hành hóa đơn lưu trong InvoiceIssueRequests

**Bổ sung thiết kế sau review, chờ duyệt.** Trường bổ sung bảng InvoiceIssueRequests để một lần
phát hành vẫn được nhận diện sau restart, kể cả hóa đơn 0 đồng không có dòng Payments.
Đây là metadata của thao tác phát hành; không chứa Amount/ledger và không được cộng vào doanh thu.

| Trường | Kiểu và ràng buộc đích | Ý nghĩa |
| --- | --- | --- |
| OperationKey | nvarchar(100), PK, bắt buộc, format [A-Za-z0-9:_-]{1,100}, so sánh phân biệt hoa/thường bằng binary collation | Khóa ổn định cho một yêu cầu phát hành; không tạo khóa mới khi retry |
| RequestHash | char(64), NOT NULL, SHA-256 hex lowercase | Hash nội dung yêu cầu đã chuẩn hóa, gồm version của định dạng |
| InvoiceId | int NOT NULL, FK Invoices.InvoiceId, NoAction, unique | Một yêu cầu thành công gắn đúng một hóa đơn; một hóa đơn mới chỉ có một bản ghi phát hành |

OperationKey của bảng này độc lập với Payments.OperationKey. PK bảo vệ khóa không trùng;
validation/CHECK bảo vệ định dạng không rỗng và hash 64 ký tự hex. Không hard-delete khóa sau
khi hủy/hoàn hóa đơn; không cho InvoiceId hoặc RequestHash thay đổi sau commit.

Chuẩn hóa yêu cầu trước khi tra khóa: kind=Orders hoặc Session theo điểm gọi, customerId lấy từ
nguồn đã xác minh, employeeId từ principal nhân viên, gamingSessionId (null nếu không yêu cầu phiên),
orderIds là mảng ID int dương đã loại trùng và sắp tăng dần. JSON UTF-8 compact có thứ tự field
như ví dụ dưới, không BOM, không khoảng trắng; SHA-256 của bytes đó là RequestHash. Kind=Session
bắt buộc có gamingSessionId. Giá/giảm/tổng, thời gian gọi và trạng thái thanh toán hiện tại không
nằm trong hash; chúng là kết quả server đọc, không phải nội dung caller được phép đổi.

```json
{"version":1,"kind":"Orders","customerId":7,"employeeId":3,"gamingSessionId":null,"orderIds":[12,18]}
```

Invoice.IssueForSession phải nhận tập orderIds cụ thể, có thể rỗng; không hiểu retry là chọn lại
mọi đơn mới phát sinh. Các SessionCharge được chọn dưới khóa nguồn trong lần đầu và giữ bằng
InvoiceDetails bất biến; retry tra khóa cũ trước khi chọn lại phí. Không tự thêm phí/đơn mới vào
hóa đơn cũ. Một yêu cầu phát hành bổ sung cần khóa mới và chỉ được chọn các nguồn chưa có chứng từ.

Luồng nguyên tử và xử lý tranh chấp:

1. Xác thực, kiểm tra invoice.create và quyền với khách/nguồn trước khi trả kết quả theo khóa.
   Tra InvoiceIssueRequests; cùng hash trả cùng InvoiceId, khác hash trả 409, không ghi thay đổi.
   Không dùng cùng khóa để xem hóa đơn vượt quyền; đổi employeeId cũng là đổi nội dung yêu cầu.
2. Nếu chưa có khóa, mở transaction, khóa nguồn theo mục 6.2.1; đọc lại khóa sau khi chờ nguồn,
   trước khi kết luận nguồn đã có chứng từ. Tạo Invoice, InvoiceOrders, InvoiceDetails, liên kết
   khoản thu/hoàn và insert InvoiceIssueRequests cùng transaction. Không lưu bản ghi khóa mồ côi
   trước khi có hóa đơn; lỗi bất kỳ phần nào rollback toàn bộ.
3. Hai request cùng khóa nhưng khác nguồn có thể đụng PK sau khi đã tạo hóa đơn tạm trong transaction.
   Khi lỗi unique, rollback toàn bộ request thua, rồi đọc khóa đã commit và áp quy tắc hash ở bước 1;
   không giữ hóa đơn/chi tiết tạm và không retry riêng đoạn insert khóa. Khóa khác nhưng trùng nguồn
   phải bị unique OrderId/SessionChargeId từ chối, trả 409, không tạo hóa đơn thứ hai.
4. Mất response hoặc restart sau commit: tra cùng khóa để trả InvoiceId cũ; không gọi thu/hoàn,
   không thêm Invoice/InvoiceDetail/Payment/ledger, kể cả nguồn đã bị hủy hoặc thanh toán thay đổi.
   Quyền truy cập hiện tại vẫn được kiểm tra; không tự khôi phục trạng thái hóa đơn từ response cũ.

Không tạo khóa giả cho hóa đơn lịch sử chưa có yêu cầu gốc; giữ các hóa đơn đó và xử lý trùng nguồn
trước unique index. Khóa mới không được gắn vào hóa đơn cũ để tuyên bố đã xử lý một request chưa
có bằng chứng. Bảng mới được triển khai trước khi bật đường phát hành mới; mọi caller phát hành mới
bắt buộc ghi metadata này. Trường sở hữu entity/config/migration của bảng, Khang/Phúc nối điểm gọi.

### 6.4. Khoản hoàn là dòng riêng trong Payments

- Dùng Type=Refund và RefundOfPaymentId để tham chiếu khoản thu gốc; không thêm PaymentRefunds.
  Amount dương; khoản thu gốc giữ nguyên Amount, PaymentTime và trạng thái Succeeded.
  Không chuyển khoản thu sang Failed để biểu diễn đã hoàn vì sẽ làm sai tổng thu trừ hoàn.
- Server tính số được hoàn từ nguồn hủy đã xác minh và snapshot sau giảm. Refund do hủy dòng
  có OrderDetailId đúng đơn; hủy toàn đơn chỉ hoàn phần còn lại sau những lần đã hoàn/đang giữ chỗ.
  Phí phiên đã phát sinh chỉ hoàn theo quyết định điều chỉnh được duyệt, không tự hoàn khi đóng phiên.
- Tạo yêu cầu hoàn phải có khóa nghiệp vụ ổn định gắn khoản thu và thao tác hủy/điều chỉnh.
  Giữ cùng dòng Refund và OperationKey khi retry; khác số tiền/nội dung cùng khóa trả 409.
  Mã provider theo từng attempt có thể đổi khi đủ điều kiện ở mục 6.4.1, không đổi khóa nghiệp vụ.
- Khóa khoản thu gốc, kiểm tra điều kiện dưới đây rồi thêm dòng Refund Pending giữ chỗ cùng transaction:

```text
tổng Refund Succeeded + tổng Refund Pending + số yêu cầu mới <= Amount khoản thu gốc
```

Ngoài hạn mức tiền còn phải kiểm tra quyền hoàn và giá trị đủ điều kiện từ snapshot hủy;
việc gửi khóa mới không cho phép hoàn lại cùng phần món/phiên đã được xử lý.

- Cash chưa thu chỉ giảm phải thu, không có Refund. Wallet hoàn về đúng ví chủ nguồn;
  ghi credit/ledger/dòng Refund Succeeded cùng transaction, mỗi ledger chỉ có một dòng đối soát.
  Cash đã thu cần Admin/Manager có payment.refund theo catalog hiện tại xác nhận tiền thực hoàn;
  Cashier không được xác nhận. Phải có dấu vết người thao tác
  theo nguồn nghiệp vụ/audit chung; không ghi hoàn chỉ vì đã duyệt hủy món.
- ZaloPay: lưu Refund Pending và RefundAttempt trước khi gọi API ngoài transaction; lấy mã thu gốc
  qua RefundOfPaymentId. Không dùng mã thu gốc làm mã hoàn. Query kết quả cuối mới ghi Succeeded;
  timeout giữ Pending, xử lý retry/cross-day theo mục 6.4.1. Không tự nhả giữ chỗ chỉ vì lỗi mạng.
  Không gửi `refund_fee_amount` (tương đương phí hoàn 0); số khách nhận lại phải bằng Amount của Refund.
  Minh xác minh khả năng hoàn từng phần của đúng kênh và chương trình trước triển khai.
  [FAQ ZaloPay, mục 2](https://docs.zalopay.vn/vi/docs/faq/) (đối chiếu 05/11/2026) yêu cầu hoàn
  toàn bộ đối với giao dịch có khuyến mãi từ chương trình ZaloPay/đối tác triển khai. Khuyến mãi
  provider khác với voucher món ăn nội bộ của quán; không suy rằng mọi đơn có voucher quán đều
  bị cấm hoàn một phần, cũng không coi một kênh hỗ trợ hoàn một phần là đủ cho mọi chương trình.
  Nếu thông tin đã xác minh của giao dịch xác định chỉ được hoàn toàn bộ, không gửi lệnh hoàn
  một phần khi hủy dòng món; chuyển yêu cầu sang xử lý thủ công, ghi rõ tiền chưa hoàn. Nếu đã
  tạo Refund Pending nhưng chưa gửi, chỉ chuyển Failed/nhả giữ chỗ khi xác nhận không có attempt
  đang gửi/xử lý. Mặc định khi chưa biết giao dịch có thuộc khuyến mãi provider hay không: gửi lệnh hoàn
  một phần đúng một lần theo OperationKey/attempt và lấy phản hồi provider làm căn cứ, không chặn trước
  chỉ vì nghi ngờ để hủy món hợp lệ không bị chặn nhầm; nếu bị từ chối thì xử lý theo bảng mục 6.4.1.
  Khi Minh xác nhận điều kiện của một chương trình cụ thể, mặc định này có thể đổi thành chặn trước cho
  chương trình đó; không tự coi một giao dịch là đủ điều kiện hoàn một phần khi chưa có căn cứ.
  Nếu provider từ chối hoàn một phần, áp bảng mục 6.4.1; không đổi mã để thử vượt hạn chế, không
  tự tăng Amount thành hoàn toàn bộ hoặc đổi sang cash/ví. Phương án khác cần người có quyền
  payment.refund phê duyệt, Minh/Khang đối chiếu phần hủy và chống hoàn trùng trước thực hiện.
  Refund Failed chỉ là chưa hoàn được; không xóa nghĩa vụ xử lý phần hủy hoặc ghi đã trả tiền.
- Hoàn thành trước hóa đơn vẫn lưu được vì Refund giữ cùng OrderId/SessionChargeId với khoản thu gốc.
  Khi hóa đơn có sau, liên kết cả thu và hoàn thành công theo mục 6.3. Hóa đơn chỉ tính khoản thực hoàn
  thành công; Refund Pending giữ hạn mức nhưng chưa làm giảm PaidAmount/doanh thu thực.
- Không giữ SQL transaction hoặc khóa khoản thu trong thời gian chờ provider.

#### 6.4.1. Mã hoàn ZaloPay, retry qua ngày và lịch sử attempt

OperationKey nhận diện yêu cầu hoàn nghiệp vụ, giữ nguyên kể cả qua ngày. MerchantRefundId
(`m_refund_id`) nhận diện một lần thử phía provider, không đồng nhất với OperationKey. Mã mới
được sinh theo `yymmdd_appid_<định danh>` với ngày tạo yêu cầu gửi theo +07:00; query mã cũ vẫn dùng
mã cũ. Không biến tiền tố ngày cũ thành ngày mới bằng cách ghi đè rồi mất khả năng đối soát.

Bổ sung bảng metadata RefundAttempts do Minh quản lý; mỗi dòng thuộc một Payments.Type=Refund:

| Trường                             | Thiết kế đích                                                    | Mục đích                                                                                    |
| ---------------------------------- | ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| RefundAttemptId                    | int PK                                                           | Định danh lần thử, không phải khoản hoàn tiền thứ hai                                       |
| PaymentId                          | int NOT NULL, FK Payments, NoAction                              | Trỏ đúng dòng Refund nghiệp vụ                                                              |
| ProviderAppId                      | int NOT NULL                                                     | Ghim `app_id` đã dùng cho attempt, không phụ thuộc cấu hình mới khi query lại               |
| MerchantRefundId                   | nvarchar(45) NOT NULL                                            | Mã yêu cầu ZaloPay; unique (ProviderAppId, MerchantRefundId), giữ nguyên lịch sử            |
| GatewayRefundId                    | nvarchar(50)?                                                    | Mã `refund_id`; unique có lọc theo app nếu khác null; đây là cỡ lưu trữ nội bộ cho mã int64 |
| Status                             | Created / Submitted / Processing / NotFound / Failed / Succeeded | Kết quả của attempt, không phải trạng thái tiền của dòng Refund                             |
| CreatedAt / LastQueriedAt          | datetimeoffset / datetimeoffset?                                 | Thời điểm tạo và đối soát attempt                                                           |
| LastApi                            | Refund / QueryRefund, nullable trước khi gọi                     | Ghi rõ endpoint trả mã lỗi, không giải mã chỉ dựa trên con số                               |
| LastReturnCode / LastSubReturnCode | int? / int?                                                      | Lưu mã cùng LastApi/LastQueriedAt; query lỗi không tự là hoàn thất bại                      |

Submitted nghĩa là worker đã claim/bắt đầu lệnh gửi; chưa chứng minh ZaloPay đã nhận yêu cầu
hoặc đã hoàn tiền. Processing chỉ dùng khi query xác nhận provider còn xử lý. Khi lỗi mạng hoặc crash,
Submitted tiếp tục cần đối soát; không chuyển Failed hoặc tạo attempt mới chỉ vì tên trạng thái này.

Một worker chỉ được gửi khi claim nguyên tử Created → Submitted thành công trên đúng attempt;
worker khác thấy Submitted/Processing chỉ được query, không gửi thêm. Claim/kết quả phải lưu bền vững,
không chỉ giữ cờ trong RAM. Không thay attempt khi worker cũ còn quyền gửi hoặc còn lời gọi đang chạy.
Sau crash phải đối soát trạng thái attempt; không tự coi hết thời gian chờ là đã hủy lệnh bên provider.

Các field trên chưa có trong schema nền. RefundAttempts không có Amount/ledger riêng và không
được cộng vào PaidAmount/doanh thu. Dòng Refund ở Payments vẫn là khoản hoàn nghiệp vụ duy nhất;
FK PaymentId cùng service validation bảo vệ attempt đúng loại và đúng app/nguồn.

Quy trình retry:

1. Timeout hoặc không rõ kết quả: giữ Refund Pending và hạn mức, query đúng mã cũ/app cũ trước.
   Nếu đang xử lý hoặc thành công thì không sinh mã mới; cập nhật cùng yêu cầu nghiệp vụ.
2. Chỉ chấp nhận kết luận không tồn tại từ chính API Query Refund /v2/query_refund, với đúng `app_id`
   và `m_refund_id` cũ, phản hồi hợp lệ `return_code`=2, `sub_return_code`=-101 (`M_REFUND_ID_NOT_FOUND`).
   Phải loại trừ lệnh cũ còn gửi/xử lý rồi mới cấp attempt có mã ngày hiện tại. `sub_return_code`=-101
   từ API Refund /v2/refund là `ORDER_NOT_FOUND` của `zp_trans_id` thu gốc, không cho phép đổi mã hoàn.
   Lỗi MAC/tham số/rate-limit/hệ thống/mạng hoặc thiếu dữ liệu không là xác nhận không tồn tại.
   Chưa đủ căn cứ thì giữ Pending và đối soát; lưu LastApi cùng các mã để không dùng nhầm ngữ cảnh.
3. Ghi attempt cũ NotFound và tạo attempt mới dưới khóa khoản thu gốc/Refund theo mục 6.2.1;
   giữ nguyên PaymentId, OperationKey, nguồn, Amount và phần giữ chỗ. Không chạy hai lệnh gửi
   provider đồng thời cho cùng yêu cầu; worker phải có claim bền vững theo attempt trước lời gọi.
4. Cùng ngày, retry kỹ thuật vẫn query mã cũ trước; không tự thêm attempt chỉ vì người dùng nhấn lại.
   Bản ghi Created chưa gửi cũng phải được đối soát/claim để worker cũ không gửi sau khi mã đã được thay.
5. Attempt mới không xóa attempt cũ. Lưu kết quả query cho mọi mã đã cấp; nếu phát hiện kết quả
   mâu thuẫn hoặc hai attempt cùng được provider chấp nhận, dừng gửi thêm và báo đối soát khoản thu gốc,
   không che khoản thực hoàn hoặc tự coi logical refund đã được xử lý an toàn chỉ vì OperationKey unique.

Mã lỗi phải xét theo API và cả `return_code`/sub_return_code, không suy từ HTTP 200 hoặc con số riêng:

| API trả kết quả                                      | Mã chi tiết                                                                                                                                  | Hành động contract                                                                                                                                                                                                                                                                                                            |
| ---------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Query Refund /v2/query_refund                        | -101 `M_REFUND_ID_NOT_FOUND`                                                                                                                 | Chỉ trường hợp này được xét cấp mã mới sau các điều kiện ở bước 2; chưa tự giải phóng giữ chỗ                                                                                                                                                                                                                                 |
| Refund /v2/refund                                    | -101 `ORDER_NOT_FOUND`                                                                                                                       | Sai/không thấy `zp_trans_id` thu gốc: dừng gửi, đối soát khoản thu; không đổi `m_refund_id` để che lỗi                                                                                                                                                                                                                        |
| Query Refund /v2/query_refund                        | -13 `REFUND_EXPIRE_TIME` hoặc -2 `REFUND_TYPE_INVALID`                                                                                       | Lỗi cuối cần xử lý thủ công/liên hệ provider, không tự retry hoặc cấp attempt mới. Khi mọi attempt đều xác minh không hoàn và không còn lệnh chờ, Refund → Failed và nhả giữ chỗ trong cùng transaction                                                                                                                       |
| Refund /v2/refund                                    | -13 `ORDER_REFUND_EXPIRED`                                                                                                                   | Tài liệu mô tả lỗi thời điểm request; không đồng nhất với hết hạn hoàn của Query Refund. Dừng retry tự động, kiểm tra timestamp/đối soát trước khi xử lý thủ công                                                                                                                                                             |
| Refund /v2/refund hoặc Query Refund /v2/query_refund | `return_code`=2, `sub_return_code`=-32 `NOT_SUPPORT_PARTIAL_REFUND`                                                                          | Từ chối hoàn một phần: lưu đúng LastApi/mã vào attempt và chuyển xử lý thủ công; không retry tự động hoặc cấp mã khác để né hạn chế. Khi mọi attempt đều được xác minh không hoàn và không còn lệnh chờ, Refund → Failed, nhả giữ chỗ một lần trong cùng transaction; còn kết quả chưa rõ thì giữ Pending/giữ chỗ để đối soát |
| Query Refund /v2/query_refund                        | -1 `REFUND_PENDING` hoặc -16 `INSERT_REFUND_LOG_AR_FAIL`                                                                                     | Còn xử lý/chờ xác nhận: giữ Pending/giữ chỗ, không gửi thêm hoặc ghi đã hoàn                                                                                                                                                                                                                                                  |
| Bất kỳ API nào                                       | Không nhận/đọc được kết quả hợp lệ hoặc lỗi gọi API do MAC/parameter/rate-limit/system; không phải các kết quả nghiệp vụ đã phân loại ở trên | Không suy rằng tiền đã/không được hoàn; đối soát, không tự nhả giữ chỗ                                                                                                                                                                                                                                                        |

Trang [status codes ZaloPay](https://docs.zalopay.vn/docs/developer-tools/knowledge-base/status-codes/)
ghi -32 cho Refund và Query Refund, chú thích ví điện tử khác ZaloPay. Đây là tình huống từ chối
hoàn một phần cần xử lý, không khẳng định mọi giao dịch có khuyến mãi đều trả đúng mã -32.
Hạn chế chương trình khuyến mãi ở FAQ là điều kiện riêng; giữ nguyên phản hồi thực và đối soát
nếu provider trả mã khác, không suy thành hoàn thành công hoặc lỗi mạng.

**Nguồn độ dài và ngày ghi nhận nội dung: 05/11/2026 (Asia/Saigon).** Đây là ghi nhận đọc trang,
không phải ảnh chụp đính kèm hoặc kết quả sandbox. Các bản ngôn ngữ hiện có khác biệt:

| Nguồn cụ thể                                                                     | Nhãn quan sát được / kết luận                                                                                             |
| -------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| [Create Order bản VI](https://docs.zalopay.vn/vi/docs/specs/order-create/)       | `app_trans_id` string(40)                                                                                                 |
| [Refund bản VI](https://docs.zalopay.vn/vi/docs/specs/order-refund/)             | `m_refund_id` string(45), description string(100)                                                                         |
| [Refund bản EN](https://docs.zalopay.vn/docs/specs/order-refund/)                | `m_refund_id` và description chỉ ghi string required, không ghi độ dài                                                    |
| [Callback bản VI](https://docs.zalopay.vn/vi/docs/specs/callback-api/) và Refund | `zp_trans_id` được mô tả long, `refund_id` int64; nvarchar(50) là cỡ lưu trữ nội bộ, không phải giới hạn provider công bố |

Cỡ 40/45 của schema đích lấy theo nhãn quan sát ở bản VI, không khẳng định hai bản API đã thống nhất
hoặc đã được sandbox xác nhận. Minh phải xác nhận đúng sản phẩm/phiên bản, các ca biên mã hoàn
45/46 ký tự và description 100/101 trong sandbox trước khi nghiệm thu adapter/migration; nếu bản VI
không truy cập được hoặc kết quả khác phải cập nhật contract/cột/index/validator trước triển khai,
không cắt mã giao dịch. Reason nội bộ giữ đầy đủ tối đa 250; description gửi Refund là nội dung
riêng đáp ứng giới hạn đã xác minh. Không đưa giới hạn 100 thành CHECK trên Reason nội bộ.

### 6.5. Đầu vào và xác nhận liên module còn chờ

Ba phần nơi lưu, liên kết hóa đơn và chống thu/hoàn lặp đã được owner chọn ở nền thiết kế 1.6 tại mục 6.1–6.4.
Phần bổ sung 1.7 về thời gian và khóa phát hành là đề xuất chờ review theo mục 9.4.
Chưa coi đây là code đã chạy, migration đã áp hoặc phản hồi chấp thuận của các owner khác.

| Giao điểm           | Đầu vào / xác nhận cần bổ sung                                                                                                                                                              |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Minh: ví và gateway | Mapping ledger/OperationKey, dấu và Type ví, app merchant/callback/query/hoàn ZaloPay, trạng thái cuối và backfill; xác nhận Payments thu/hoàn, RefundAttempts, các trường thời gian/Basis mục 6.1.1 và migration thuộc phần Minh |
| Khang: đơn và cash  | Nguồn OrderId/OrderDetailId, khóa của thao tác đặt/hủy/thu gộp, tiền còn phải thu và điểm gọi cùng transaction; đối chiếu payload/khóa phát hành tại mục 6.3.1 |
| Phúc: phiên         | ChargeId, nguồn ledger/snapshot phí, đóng phiên/actor, payload phát hành phí tại mục 6.3.1 và đối chiếu thứ tự khóa đề xuất mục 6.2.1 với writer ví/chứng từ |
| Gateway             | Thời hạn giữ tồn/lượt, callback muộn, duyệt hoàn/thu dư; Minh xác nhận sandbox cho độ dài/format theo nguồn ngôn ngữ và bảng mã lỗi đúng API mục 6.4.1                                      |
| Giao nhận và duyệt  | Commit/PR hoặc phản hồi cụ thể của từng owner; Trưởng nhóm xem xét contract chung trước triển khai                                                                                          |

**Trạng thái P1:** đã có phương án thiết kế của owner cho cả ba phần; chưa khép tiêu chí hợp đồng
chứng từ chung khi thiếu xác nhận/đầu vào bắt buộc tại bảng này. Không ghi các bên đã đồng ý khi chưa có bằng chứng.

## 7. Nguồn báo cáo và đối soát

Không cộng tổng Invoice thành doanh thu. Mỗi phương thức chọn đúng một nguồn tiền, áp dụng
nhất quán cho tổng đã thu của hóa đơn và báo cáo; Payment đối soát ví không được cộng lần hai.

| Phương thức / chỉ tiêu               | Nguồn được tính                                                                                                                                            | Ngày ghi nhận                                                                                 |
| ------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| Wallet thu/hoàn dịch vụ              | Chỉ WalletTransactions debit/credit đã commit, đúng nguồn đơn/phí phiên qua FK hoặc Payment liên kết; loại trừ Payment/Refund Method=Wallet khỏi phép cộng | CreatedAt của ledger sau đối chiếu múi giờ; không lấy InvoiceDate                             |
| Cash                                 | Payments Method=Cash, Status=Succeeded; Type=Payment cộng, Type=Refund trừ                                                                                 | PaymentTime là lúc thực thu/hoàn, có xác nhận người đủ quyền                                  |
| ZaloPay món                          | Payments Method=ZaloPay, Status=Succeeded, kết quả được server xác minh; Type quyết định thu/hoàn                                                          | PaymentTime cùng Basis theo mục 6.1.1; thiếu giờ provider thì dùng ngày xác nhận đầu tiên và ghi rõ Confirmation |
| Phí phiên                            | Nguồn SessionCharge và ledger/Payment tương ứng theo phương thức, không cộng thêm SessionCharge.Amount vào khoản đã tính                                   | Theo khoản thực thu được ghi nhận, không coi phí ước tính là doanh thu                        |
| Nạp ví                               | Nguồn nạp do Minh bàn giao                                                                                                                                 | Báo riêng, không là doanh thu sử dụng dịch vụ                                                 |
| Invoice/InvoiceOrders/InvoiceDetails | Đối soát nguồn, snapshot và trạng thái phải thu/đã thu/hủy                                                                                                 | InvoiceDate là ngày phát hành, không mặc định ngày thu/hoàn                                   |

Công thức cho nguồn dịch vụ đã chuẩn hóa, không trùng:

```text
thuRongVi = debit ví dịch vụ quy về số dương - credit hoàn ví dịch vụ
thuRongNgoaiVi = tổng Payment Succeeded (Cash/ZaloPay)
               - tổng Refund Succeeded (Cash/ZaloPay)
netRevenue = thuRongVi + thuRongNgoaiVi
```

PaidAmount của một hóa đơn dùng cùng quy tắc trên nhưng chỉ lấy nguồn thuộc hóa đơn đó và
các liên kết đã đối soát; không cộng toàn bộ Payments rồi cộng thêm ví. Refund Pending giữ hạn mức
hoàn nhưng chưa trừ số đã thu ròng. Giá trị hủy/phải thu theo snapshot không thay thế khoản thực hoàn.
Các phép cộng phải đối chiếu được từng ledger hoặc mã giao dịch, không dựa trên Description.

Ví dụ đối chiếu bắt buộc:

- Đơn ví 100000 có debit ledger và Payment đối soát 100000: PaidAmount là 100000, không phải 200000.
- Thu cash gộp hai đơn 100000/50000: tạo hai dòng có khóa con riêng; tổng thực thu 150000,
  retry batch không thêm dòng. Phát hành hóa đơn gộp chỉ liên kết hai khoản đã thu.
- Khoản đã thu 100000 có Refund Pending 60000: yêu cầu hoàn mới 50000 bị từ chối;
  PaidAmount vẫn 100000 trước khi khoản 60000 hoàn thành công, sau đó là 40000.

Mapping Type/dấu ledger và dữ liệu cũ do Minh xác nhận; không áp enum của DBML cũ vào schema mới.
Nạp ZaloPay vào ví vẫn là nạp ví, không tính như thu ZaloPay món. Thu trước hóa đơn vẫn được tính
theo ngày ghi nhận của nguồn, không bỏ qua chỉ vì InvoiceId còn null. Khoản hoàn tính theo
PaymentTime và Basis ở mục 6.1.1; thiếu giờ provider thì dùng ngày xác nhận và thể hiện rõ giới hạn,
không sửa lại doanh thu ngày đặt. Pending/Failed không được tính như đã thu/hoàn.
Thiếu liên kết hoặc khoản thu dư chưa đối soát phải báo rõ, không tự trả số 0 hoặc che tiền dư.

Ngày from/to dạng YYYY-MM-DD; từ đầu ngày +07:00 đến trước đầu ngày sau to. Đơn đếm theo
OrderTime nguồn, các bộ đếm khác theo định nghĩa đã thống nhất. Chưa triển khai pending top-up khi
chưa có nguồn Minh bàn giao. Hóa đơn seed không có ledger/payment không chứng minh doanh thu thực.

## 8. Owner, phần bổ sung và thứ tự migration

| Owner  | Giữ và chịu trách nhiệm nguồn                                          | Phần bổ sung / contract giao nhận                                                               |
| ------ | ---------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| Đại    | Identity Employee/Customer, role/permission, auth JWT; UI voucher      | Claims/quyền theo SCRUM-2; không thay code auth trong SCRUM-12                                  |
| Minh   | Customers/WalletTransactions/Payments và nguồn nạp/gateway             | Đối chiếu dấu/type ledger, nguồn/khóa thu-hoàn trong Payments, gateway và migration mục 6.1–6.5 |
| Phúc   | Computers/GamingSessions/SessionCharges                                | Snapshot phí/giá giờ, tổng đã ghi nhận, kết thúc phiên và actor                                 |
| Khang  | Categories/Products/Orders/OrderDetails                                | Giữ Stock/IsActive; thêm snapshot món/giảm, trạng thái hủy và điểm gọi rule                     |
| Trường | InventoryTransactions/Promotions/Invoices/InvoiceOrders/InvoiceDetails | Mở rộng các bảng sẵn có; thêm PromotionUsages, InvoiceIssueRequests, query/DTO và rule; điều phối thứ tự migration |

Thứ tự tích hợp trên nền CyberManagement 35 bảng:

1. Giữ lịch sử migration nguồn, xác nhận commit nền và đúng database thử.
   Không tạo lại Employees/Customers/Products/Invoices hoặc áp InitialCreate của hệ 19 bảng vào đây.
2. Minh đối chiếu và mở rộng Payments thu/hoàn theo mục 6.1–6.4; Khang/Phúc bổ sung nguồn đơn/phiên
   và snapshot phần mình. Bổ sung RefundAttempts phục vụ query/retry mã hoàn; ConfirmedAt,
   ProviderOccurredAt, PaymentTimeBasis và mapping báo cáo theo mục 6.1.1; không thêm sổ tiền song song.
   Với OperationKey: thêm cột tạm nullable và unique filtered, bắt buộc khóa ở mọi ghi mới, backfill
   dữ liệu cũ có đối ứng rồi chuyển NOT NULL trước nghiệm thu; chặn thiếu/trùng, không bỏ qua dữ liệu lỗi.
   Trường đối chiếu chỉ những field/constraint Product thực sự thiếu; không thêm StockQuantity cạnh Stock.
3. Trường mở rộng Promotions đã có; backfill Code/giá trị trước unique/check, kiểm tra ngày/precision.
   Cùng Khang thêm snapshot voucher trên Orders/OrderDetails; tạo PromotionUsages sau khi FK nguồn đã có.
4. Trường mở rộng InventoryTransactions, InvoiceDetails và phần truy vết/tiền chứng từ cần thiết.
   Giữ InvoiceOrders, thêm unique OrderId; thêm InvoiceDetails.OrderDetailId/SessionChargeId,
   FK NoAction và unique filtered theo nguồn sau backfill. Thêm InvoiceIssueRequests với PK khóa,
   hash và unique FK InvoiceId; bật ghi nguyên tử cho mọi phát hành mới theo mục 6.3.1.
   Đối soát trùng trước khi áp constraint; không backfill khóa giả cho hóa đơn cũ.
5. Rà backfill/type/precision/offset, thêm constraint/index sau dữ liệu hợp lệ, chạy trên DB thử mới
   với seed nhất quán; cập nhật schema/SQL mô tả theo migration đã tích hợp ở S1-T-2.

Mỗi owner viết entity/config phần mình. Một PR migration xử lý tại một thời điểm.
Không sửa/xóa migration đã áp vào DB chung; migration bổ sung phải nối tiếp lịch sử CyberManagement hiện tại.
ID long cần mapping/range check sang int; không tạo ID nhân viên giả, không đổi tiền/ngày cũ không đối soát.
Code/seed ở `CyberCafe_SCRUM13_Test` trước đây chỉ chứng minh baseline cũ, không là bằng chứng nền 35 bảng đã tích hợp.
Phần migration được thực hiện và kiểm thử trong task tích hợp theo thứ tự trên.

## 9. Kết quả đối chiếu và đầu vào còn chờ

### 9.1. Quyết định thiết kế

| Nội dung                   | Quyết định                                                                                                                                                               |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Backend và schema          | CyberManagement 35 bảng, cấu trúc Backend/src và ApplicationDbContext                                                                                                    |
| Định danh                  | EmployeeId/CustomerId kiểu int; không bổ sung Account                                                                                                                    |
| Kho                        | Mở rộng InventoryTransactions hiện có; giữ Products.Stock/IsActive                                                                                                       |
| Voucher                    | Mở rộng Promotions, thêm PromotionUsages; một voucher trên mỗi đơn                                                                                                       |
| Hóa đơn                    | Giữ InvoiceOrders, cho phép gộp nhiều đơn cùng khách; phát hành khi nhân viên chốt hóa đơn                                                                               |
| Khoản thu/hoàn và chứng từ | Owner Trường chốt ngày 05/11/2026: Payments neo OrderId/SessionChargeId; InvoiceId nullable; Refund tự tham chiếu; phát hành không thu lại và chống lặp theo mục 6.1–6.4 |
| Quyền                      | Theo permission từng endpoint tại mục 5, khớp catalog SCRUM-2                                                                                                            |
| Doanh thu                  | Wallet tính ledger; Cash/ZaloPay tính Payments theo Type/Status; không cộng Payment đối soát ví; tách nạp ví theo mục 7                                                  |

### 9.2. Kết quả đối chiếu theo thành viên

Bảng dưới phân biệt kết quả rà soát mã nguồn với xác nhận trực tiếp của owner.
Các quyết định thiết kế được dùng làm cơ sở bàn giao; phản hồi của từng bên được cập nhật khi nhận.

| Thành viên | Kết quả đối chiếu                                                                                                                                                                                                                                                                                                | Đầu vào / xác nhận còn chờ                                                                                                |
| ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| Đại        | Đã rà SCRUM-2 tại 15d537d và schema/catalog trên dev local tại a975f8d. Contract sử dụng JWT/permission, int, Employee/Customer; giữ nền 35 bảng, Payments và InvoiceOrders. Bộ auth có 35 HTTP checks đạt trên TestServer/InMemory                                                                              | Đối chiếu trực tiếp contract endpoint; policy khóa tài khoản sau cấp token nếu yêu cầu có hiệu lực ngay                   |
| Minh       | Đã rà SCRUM-5 local tại 92ffe80: WalletLedger dùng CyberCafeDbContext và ID long, có nguồn nghiệp vụ nhưng chưa phải contract/model CyberManagement int. Đã xác định SCRUM-5 tương ứng S1-M-1. Mapping giữ WalletTransactions/Payments; chưa xác nhận schema nạp ví hoặc quy ước dấu/type của contract mới       | Contract credit/debit/refund, source IDs, operationKey, trạng thái/ngày thu-hoàn, nạp tại quầy/gateway và mapping dữ liệu |
| Phúc       | Đã đọc contract SCRUM-7 local tại c0aae97: snapshot giá lúc mở phiên, phí tích lũy làm tròn lên đồng và quyết toán khi đóng; nguồn tiền/chứng từ vẫn ghi chờ Minh/Trường. Đã đối chiếu nền GamingSessions/SessionCharges và phần phân công. Hóa đơn lấy phí đã ghi nhận, không thu lại hoặc cộng trùng tiền chơi | Snapshot giá giờ/khoảng phí, thời điểm kết thúc, chống thu/đóng lặp, actor và điểm gọi phát hành                          |
| Khang      | Đã đối chiếu Products/Orders/OrderDetails và phần phân công. Giữ Stock/IsActive, thêm snapshot món/giảm theo contract; voucher áp từng đơn                                                                                                                                                                       | Contract đặt/hủy/cash, field snapshot cần bổ sung, điểm gọi transaction; cùng Minh xác nhận policy checkout/gateway       |

Chưa có bằng chứng xác nhận trực tiếp của cả bốn owner hoặc quyết định duyệt cuối.
Việc rà code không thay cho xác nhận giao điểm cần bàn giao.

Bổ sung SCRUM-5 ngày 08/11/2026: [contract hồ sơ/ví CyberManagement int](../../Backend/docs/003-customer-wallet-contract.md)
ghi credit/debit/refund, dấu/type và khóa nguồn, transaction chung, liên kết Payment dịch vụ và nạp quầy.
Người dùng chọn thu tiền trước khi cộng ví, không nạp nợ; nạp ví không phải doanh thu.
Query/tạo hồ sơ đã có code; writer/nguồn nạp/OperationKey và migration vẫn thuộc S2.
Ghi chú này bổ sung đầu vào cho Minh/Trường/Khang/Phúc, không thay lịch sử rà soát hoặc giả phản hồi đồng thuận.

### 9.3. Phụ thuộc cần giải quyết trước khi tích hợp

| Đầu vào                                                                                                     | Người cung cấp                             | Phạm vi ảnh hưởng                                                   |
| ----------------------------------------------------------------------------------------------------------- | ------------------------------------------ | ------------------------------------------------------------------- |
| Đối chiếu thiết kế thu/hoàn và liên kết hóa đơn mục 6.1–6.4 với ledger/gateway, operationKey và thứ tự khóa | Minh, phối hợp Khang/Phúc                  | Phương án owner đã có; cần phản hồi cụ thể để triển khai thống nhất |
| Thời hạn giữ tồn/lượt, hết hạn, callback muộn và phương thức hoàn gateway                                   | Minh + Khang; Trường cung cấp rule voucher | Trạng thái giữ/giải phóng lượt và mapping reservation               |
| Snapshot và thao tác đóng phiên/hủy đơn                                                                     | Phúc + Khang                               | Dữ liệu hóa đơn, trả tồn và hoàn voucher                            |
| Migration/PR của từng owner và dữ liệu chuyển đổi                                                           | Các owner liên quan                        | Thứ tự tích hợp, backfill và kiểm thử trên DB mới                   |
| Policy khóa sau cấp token                                                                                   | Đại                                        | Kiểm thử truy cập sau khi tài khoản bị vô hiệu hóa                  |

Các quyết định tại mục 9.1 là thiết kế owner để bàn giao. Trường đã chọn phương án lưu nguồn,
liên kết hóa đơn và chống thu/hoàn lặp tại mục 6.1–6.4; chưa đồng nghĩa Minh/Khang/Phúc đã xác nhận contract chung.
Các bên cung cấp đầu vào của module mình và phản hồi những xung đột cụ thể trước khi triển khai.
Không tự thay quy tắc hoặc duy trì hai schema/rule khác nhau.

### 9.4. Bổ sung theo yêu cầu Trưởng nhóm ngày 05/11/2026

Trưởng nhóm yêu cầu giữ merge PR #8 tại 229d7af và bổ sung phần thiếu để review tiếp.
Yêu cầu thực hiện bổ sung không phải quyết định nghiệm thu SCRUM-12 hoặc xác nhận thay các owner.

| Nội dung | Ghi nhận / nơi đối chiếu | Trạng thái |
| --- | --- | --- |
| Nền bổ sung | Giữ merge 229d7af; sửa tài liệu trên nhánh riêng từ dev | Theo yêu cầu trực tiếp của Trưởng nhóm |
| Giờ hoàn thiếu từ provider | Phương án ConfirmedAt/ProviderOccurredAt/PaymentTimeBasis, quy tắc ngày báo cáo ở mục 6.1.1 | Đề xuất bổ sung chờ review; Minh đối chiếu adapter và dữ liệu |
| Khóa phát hành | InvoiceIssueRequests, hash yêu cầu và lưu cùng transaction ở mục 6.3.1 | Đề xuất bổ sung chờ review; Trường triển khai, Khang/Phúc đối chiếu caller |
| Phản hồi liên module | Các đầu vào tại mục 6.5 và 9.2–9.3 | Chưa có bằng chứng phản hồi mới; giữ P1 còn mở |
| Nghiệm thu cuối | Từng tiêu chí mục 10.2 và quyết định của Trưởng nhóm | Chưa ghi nhận duyệt; không chuyển Done từ việc đã merge hoặc sửa tài liệu |

Khi nhận phản hồi, bổ sung commit/PR hoặc nội dung xác nhận có thể đối chiếu theo từng giao điểm;
nếu có khác biệt thì cập nhật cùng một contract trước khi viết migration. Không ghi “đã đồng ý”
chỉ vì đã rà code, được giao bổ sung, hoặc thành viên không phản hồi.

## 10. Kiểm chứng và tiêu chí bàn giao

### 10.1. Bằng chứng hiện có

| Hạng mục             | Kết quả và giới hạn                                                                                                                                                                                                                                                                                                                                                                                             |
| -------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Schema               | Đối chiếu ApplicationDbContext nền dev local a975f8d: 35 DbSet; mapping các bảng liên quan tại mục 3                                                                                                                                                                                                                                                                                                            |
| AuditLogs            | Đối chiếu ngày 05/11/2026 tại dev a975f8d: AuditLog.cs, AuditLogConfiguration.cs và migration 20261102065247_InitialCreate.Up tạo AuditLogs (các liên kết ghim ở mục 11); kiểu/nullability/FK đúng dòng mapping mục 3. CreatedAt mặc định DateTime.UtcNow ở C#, SQL datetime2 không có default/constraint UTC. Chưa chạy ca lỗi audit/rollback/thu muộn; mục 10.3 là yêu cầu kiểm thử, không là kết quả đã pass |
| Quyền                | Đối chiếu RolePermissionCatalog và policy handler SCRUM-2; permission được ghi tại từng endpoint                                                                                                                                                                                                                                                                                                                |
| Auth                 | Lượt kiểm tra trước tại commit 15d537d: 35 HTTP checks đạt, exit 0, TestServer + EF InMemory                                                                                                                                                                                                                                                                                                                    |
| Phạm vi test auth    | Login đúng/sai, permission, token bị sửa, customer login/ownership và logout; chưa chứng minh SQL hoặc tích hợp module kho/voucher                                                                                                                                                                                                                                                                              |
| Tài liệu             | Rà liên kết, tên bảng/field, permission, mapping và tính nhất quán của các quyết định                                                                                                                                                                                                                                                                                                                           |
| Xác nhận liên module | Theo trạng thái tại mục 9.2; chưa ghi nhận đầy đủ phản hồi trực tiếp hoặc quyết định duyệt                                                                                                                                                                                                                                                                                                                      |

Lệnh kiểm tra auth đã sử dụng:

```powershell
dotnet run --project Backend/tests/CyberManagement.API.Checks/CyberManagement.API.Checks.csproj --no-launch-profile
```

### 10.2. Checklist theo SCRUM-12

| Tiêu chí                       | Nội dung bàn giao                                                                     | Điều kiện xác nhận                                             |
| ------------------------------ | ------------------------------------------------------------------------------------- | -------------------------------------------------------------- |
| Schema kho/chứng từ/voucher    | Mapping giữ/thay đổi/thêm mới, quan hệ, ràng buộc và phân công migration tại mục 3, 8 | Các giao điểm schema/dữ liệu được owner đối chiếu              |
| Rule và API voucher            | Tính giảm, phân bổ, lượt dùng, snapshot, request/response và permission tại mục 4, 5  | Đại/Khang xác nhận cách sử dụng chung                          |
| Hợp đồng chứng từ và doanh thu | Phát hành gộp, nguồn thu/hoàn, transaction và chống lặp tại mục 6, 7                  | Minh/Phúc/Khang cung cấp và đối chiếu nguồn bắt buộc           |
| Review                         | Tài liệu này và thay đổi thuộc SCRUM-12                                               | Trưởng nhóm duyệt sau khi các đầu vào bắt buộc được giải quyết |

### 10.3. Ca kiểm thử bàn giao cho các task triển khai

- Kho: Receive/Adjust hợp lệ và sai biên, tồn âm, rollback, tranh tồn, Sale/Return không ghi trùng.
- Voucher: Percent/Fixed, có/không trần, ngưỡng đơn, đầu/cuối hiệu lực, giảm 100%, làm tròn và phân bổ.
- Lượt dùng: tổng/theo khách, hai request tranh lượt cuối, retry, rollback, giữ chỗ/hết hạn,
  hủy một phần/toàn bộ và hoàn lượt lặp.
- Hóa đơn: nhiều đơn cùng khách/phiên, chống phát hành trùng, snapshot bất biến,
  thu/hoàn lặp, hóa đơn 0 đồng và hủy một phần hóa đơn gộp; callback thành công sau phát hành
  và tranh chấp phát hành/callback vẫn liên kết đúng nguồn, không thu lại.
- Khóa phát hành: cùng khóa/cùng nguồn sau restart trả InvoiceId cũ; thứ tự orderIds khác hoặc ID
  lặp được chuẩn hóa như nhau; cùng khóa đổi khách/nhân viên/kind/phiên/tập đơn trả 409.
  Hai request cùng khóa khác tập nguồn chỉ một request commit; request thua không để lại hóa đơn,
  chi tiết hoặc liên kết tiền. Khóa khác cùng nguồn không tạo hóa đơn thứ hai. Thử hóa đơn 0 đồng,
  mất response, quyền bị thu hồi, lỗi insert khóa và hóa đơn lịch sử chưa có khóa.
- Thời gian hoàn: tạo trước nửa đêm, query xác nhận sau nửa đêm không có giờ provider thì Succeeded
  với Basis=Confirmation, ghi đúng ngày xác nhận và chỉ giảm PaidAmount/doanh thu một lần.
  Query trùng ngày sau không đổi mốc/Basis; rollback không lưu mốc thành công. Có giờ provider
  đến sau thì giữ ngày báo cáo đã ghi nhận, bổ sung bằng chứng/audit, không thêm khoản hoàn;
  không coi CreatedAt của Refund hoặc ngày trong mã hoàn là giờ thực hoàn.
- Thanh toán: XOR nguồn, khóa bắt buộc cho ghi mới, retry cùng/khác nội dung, cash batch nguyên tử,
  ví/Payment không cộng hai lần; hai yêu cầu hoàn cạnh tranh tính cả Pending, timeout hoàn không
  giải phóng giữ chỗ sai, Refund không thay Amount/Status của khoản thu gốc.
- Quyền: Manager quản lý voucher; Cashier chỉ dùng quyền được cấp;
  Waiter/Technician không truy cập kho hoặc hóa đơn quản trị; khách không xem hóa đơn người khác.
- Báo cáo: đúng nguồn và ngày ghi nhận, tách nạp ví, không cộng cùng khoản Payment/Wallet/Invoice nhiều lần.
- Payment/Refund: mọi chuyển trạng thái mục 6.2.2, đặc biệt Failed/Expired nhận xác nhận thu muộn,
  Succeeded nhận callback lặp, Refund Failed nhả giữ chỗ đúng một lần, không tự Failed → Succeeded.
- Quyền tiền: Admin/Manager xác nhận thu/hoàn cash, Cashier chỉ thu, Waiter/Technician/Customer
  bị từ chối thao tác quản trị; không coi được hủy món là đủ quyền hoàn tiền.
- Gateway: query hoàn timeout và qua ngày; mã cũ Submitted/Processing/Succeeded không sinh mã mới;
  query lỗi không cho đổi mã, NotFound chỉ đổi khi đã loại trừ writer cũ; OperationKey/giữ chỗ không đổi;
  kiểm tra không dùng `refund_fee_amount` và lưu lịch sử các mã theo app; restart không gửi hoàn song song.
- Minh (gateway), Khang phối hợp hủy món: giao dịch có khuyến mãi provider/đối tác chỉ cho hoàn
  toàn bộ nhưng hủy một dòng; chặn gửi hoàn một phần nếu đã biết điều kiện, giữ số tiền gốc,
  không tự hoàn toàn bộ/đổi phương thức, ghi chưa hoàn và chuyển xử lý thủ công đúng quyền.
  Kiểm thử riêng voucher quán và kênh cho phép hoàn một phần để tránh chặn nhầm. Khi chưa biết điều kiện,
  hệ thống gửi đúng một lần và xử lý theo phản hồi provider, không chặn trước và không gửi lặp.
- Minh: mô phỏng phản hồi hợp lệ `return_code`=2/-32 từ cả Refund và Query Refund; lưu đúng API,
  Refund Failed/nhả giữ chỗ đúng một lần khi đủ bằng chứng; retry/qua ngày không cấp mã né hạn chế.
  Nếu attempt cũ còn chưa rõ thì giữ Pending/giữ chỗ; không cộng hoàn vào PaidAmount/doanh thu.
  Ghi riêng kết quả mô phỏng và sandbox; không coi fixture -32 là bằng chứng provider thật đã trả mã.
- Audit: worker EmployeeId null, nhân viên có FK hợp lệ, Action không vượt 255 và CreatedAt UTC;
  gây lỗi ghi audit để kiểm tra cập nhật SQL rollback, query lại kết quả thu muộn rồi retry không
  thu lại/không ghi audit trùng; không suy rollback SQL đã hoàn tác tiền provider. Kiểm thử bảng phản hồi callback:
  mac sai trả 2 (Invalid) và không ghi gì; lỗi tạm/rollback trả 0 và không báo thành công; chỉ trả 1 sau commit hoặc khi
  đã xử lý trước đó; worker đối soát chốt được khoản còn treo khi callback không đến lại.
- Nguồn chứng từ: callback SessionChargeId tìm đúng unique InvoiceDetails, callback OrderId tìm đúng
  InvoiceOrders; thử hai nguồn cùng khách nhưng khác đơn/phiên và cạnh tranh phát hành/thu/hoàn theo thứ tự khóa.
- Hủy và tồn: cạnh tranh ApplyReturn/ApplySale/Adjust trên nhiều Product, kiểm tra thứ tự ProductId
  tăng dần, rollback đồng thời hủy/trả tồn/tạo hoàn, không giữ khóa Product trong lúc chờ gateway.
- Kiểm tra tầng ràng buộc: CHECK chỉ dùng cột nội bảng; attempt/ledger/cùng khách/tổng hoàn do service
  kiểm tra trong transaction. Failed→Succeeded thu muộn phải có AuditLogs nguyên tử, không lặp audit.
- Mã lỗi/nguồn: QueryRefund -101 khác Refund -101; QueryRefund -13/-2 kết thúc thủ công và không retry;
  Refund -13 không bị hiểu thành kết quả QueryRefund. Sandbox xác nhận các giới hạn theo từng locale.

## 11. Tài liệu và mã nguồn tham chiếu

Nguồn schema đã đọc: dev local tại a975f8d6f329266d954090521469975c5e8dcdb6.
Nguồn auth đã kiểm tra: SCRUM-2 tại 15d537d. Các nguồn được ghim theo commit để tái hiện việc đối chiếu.

- [ApplicationDbContext và nền 35 bảng](https://github.com/imLCKKK/CyberCafe-Management-System-Project/blob/a975f8d6f329266d954090521469975c5e8dcdb6/Backend/src/CyberManagement.Infrastructure/Persistance/ApplicationDbContext.cs).
- [AuditLog: cột và DateTime.UtcNow ở ứng dụng](https://github.com/imLCKKK/CyberCafe-Management-System-Project/blob/a975f8d6f329266d954090521469975c5e8dcdb6/Backend/src/CyberManagement.Domain/Entities/AuditLog.cs).
- [AuditLogConfiguration: độ dài, required và FK Restrict](https://github.com/imLCKKK/CyberCafe-Management-System-Project/blob/a975f8d6f329266d954090521469975c5e8dcdb6/Backend/src/CyberManagement.Infrastructure/Persistance/Configuration/AuditLogConfiguration.cs).
- [InitialCreate: kiểu SQL/nullability/identity/FK thực của AuditLogs](https://github.com/imLCKKK/CyberCafe-Management-System-Project/blob/a975f8d6f329266d954090521469975c5e8dcdb6/Backend/src/CyberManagement.Infrastructure/Migrations/20261102065247_InitialCreate.cs).
- [Định danh và ma trận quyền](https://github.com/imLCKKK/CyberCafe-Management-System-Project/blob/a975f8d6f329266d954090521469975c5e8dcdb6/Backend/docs/001-monorepo.md).
- [RolePermissionCatalog](https://github.com/imLCKKK/CyberCafe-Management-System-Project/blob/a975f8d6f329266d954090521469975c5e8dcdb6/Backend/src/CyberManagement.Application/Security/RolePermissionCatalog.cs).
- [Backlog và tiêu chí](../../codex/SPRINT_TASKS.md).
- [Kế hoạch và owner](../../planning/team-delivery-plan.md).
- [Quy trình nhóm](../../planning/team-workflow.md).

- [SCRUM-5: WalletLedger của nguồn cũ, chỉ đối chiếu rule; chưa áp mapping sang CyberManagement](https://github.com/imLCKKK/CyberCafe-Management-System-Project/blob/92ffe808da30225bfa05ebb82132dfd4d5bb6096/backend/CyberCafe.Api/Features/Walletledger.cs).

- [SCRUM-7: contract phiên và phí giờ, giao điểm tiền/chứng từ còn chờ xác nhận](https://github.com/imLCKKK/CyberCafe-Management-System-Project/blob/c0aae97be218ea936a203e7c0bb9e9efd1b48636/Backend/docs/002-computer-session-contract.md).

- [SQL Server: unique filtered index cho các mã nullable](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-filtered-indexes).

- [ZaloPay: callback, mã tham chiếu và mã giao dịch](https://docs.zalopay.vn/vi/docs/specs/callback-api/).

- [ZaloPay: yêu cầu hoàn và truy vấn kết quả cuối](https://docs.zalopay.vn/vi/docs/specs/order-refund/).

- [ZaloPay Create Order: `app_trans_id` string(40), format ngày và app](https://docs.zalopay.vn/vi/docs/specs/order-create/).

- [ZaloPay Query Refund: truy vấn theo `app_id` và `m_refund_id` đã lưu](https://docs.zalopay.vn/docs/specs/order-query-refund/).

- [ZaloPay Query Order: server_time của giao dịch thu; khác response Query Refund không có thời điểm thực hoàn](https://docs.zalopay.vn/docs/specs/order-query/).

- [ZaloPay status codes: phân biệt Processing, query lỗi và `M_REFUND_ID_NOT_FOUND`](https://docs.zalopay.vn/docs/developer-tools/knowledge-base/status-codes/).

- [ZaloPay FAQ mục 2: giao dịch có khuyến mãi provider/đối tác yêu cầu hoàn toàn bộ](https://docs.zalopay.vn/vi/docs/faq/).

- [ZaloPay Callback API: mã `return_code` merchant trả về](https://docs.zalopay.vn/docs/specs/callback-api).

- [ZaloPay knowledge-base callback: mã mẫu, khuyến nghị query sau 15 phút và xử lý trùng idempotent](https://docs.zalopay.vn/docs/developer-tools/knowledge-base/callback).
