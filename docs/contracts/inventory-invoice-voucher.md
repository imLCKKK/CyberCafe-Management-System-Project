# SCRUM-12 - Hợp đồng kho, hóa đơn và voucher

| Thông tin           | Nội dung                                                                 |
| ------------------- | ------------------------------------------------------------------------ |
| Công việc           | Define-Inventory-Invoice-Voucher-Contracts                               |
| Mã Jira / kế hoạch  | SCRUM-12 / S1-T-1                                                        |
| Người phụ trách     | Lê Vũ Huy Trường                                                         |
| Phiên bản           | 1.0 - cập nhật 04/11/2026                                                |
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

| Nội dung              | Contract SCRUM-2                                                  |
| --------------------- | ----------------------------------------------------------------- |
| Nhân viên đăng nhập   | POST /api/auth/login, body username/password                      |
| Khách đăng nhập       | POST /api/auth/customer/login, body phone/password                |
| Response              | accessToken, expiresAtUtc, employee hoặc customer                 |
| Request sau đăng nhập | Authorization: Bearer <token>                                     |
| Claims nhân viên      | sub=EmployeeId, role và các claim permission                      |
| Claims khách          | sub=CustomerId, customer_id, role=Customer, account_type=Customer |
| Ánh xạ claims         | MapInboundClaims=false, RoleClaimType=role, NameClaimType=name    |

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

| Bảng/nguồn hiện có                                        | Giữ                                                                                                                   | Thay đổi / bổ sung trên bảng sẵn có                                                                                                                                                               | Thêm mới riêng                                              | Owner                                     |
| --------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------- | ----------------------------------------- |
| Employees, Customers, Roles, Permissions, RolePermissions | Identity int, JWT sub/role/permission; hai miền ID nhân viên/khách riêng                                              | Chỉ mapping dữ liệu cũ và nối principal; không tạo Account                                                                                                                                        | Không                                                       | Đại; Minh đối chiếu Customer              |
| Categories                                                | CategoryId, CategoryName và FK sản phẩm                                                                               | Đối chiếu dữ liệu trước migration                                                                                                                                                                 | Không                                                       | Khang                                     |
| Products                                                  | ProductId, CategoryId, ProductName, Price, Stock, IsActive đã có                                                      | Constraint Stock >= 0, concurrency/khóa khi cập nhật tồn nếu nền chưa có; không thêm StockQuantity trùng Stock                                                                                    | Không                                                       | Khang cấu hình Product; Trường rule kho   |
| InventoryTransactions                                     | InvTransId, ProductId, EmployeeId, Type, Quantity, ReferenceType, ReceiptId, OrderId                                  | Thêm Before/After, Reason, OrderDetailId; chuẩn hóa Type/dấu Quantity sau đối soát; CreatedAt sang thời điểm có offset nếu triển khai; EmployeeId nullable cho nguồn khách/hệ thống theo mục 3.1  | Không tạo lại bảng                                          | Trường; Khang đối chiếu nguồn đơn         |
| Suppliers, StockReceipts, StockReceiptDetails             | Các bảng/FK có sẵn, ReceiptId, SupplierId, EmployeeId, chi tiết nhập                                                  | Nối nguồn Receive và chống ghi tồn hai lần; không tạo bộ phiếu nhập khác                                                                                                                          | Không                                                       | Trường đối chiếu nguồn nhập               |
| Orders                                                    | OrderId, CustomerId, GamingSessionId, OrderTime, quan hệ nguồn                                                        | Thêm snapshot voucher/code/type/value và DiscountAmount; nguồn payment/thu-hủy do Khang/Minh xác định                                                                                             | Không                                                       | Khang; Trường contract snapshot           |
| OrderDetails, OrderStatusHistories                        | OrderDetailId, OrderId, ProductId, Quantity, UnitPrice và lịch sử sẵn có                                              | Thêm tên món snapshot, giảm phân bổ và trạng thái/hủy theo contract đơn; đây là các phần bổ sung vào schema hiện tại                                                                              | Không trong task này                                        | Khang                                     |
| Invoices                                                  | InvoiceId, CustomerId, GamingSessionId, EmployeeId, PromotionId, InvoiceDate, Subtotal, Discount, TotalAmount, Status | Snapshot tổng/giảm/thu/hoàn và mapper từ nguồn thực theo mục 3.3; chuyển cột tiền sang decimal(18,0) theo mục 2.3 sau đối soát                                                                    | Không tạo lại bảng                                          | Trường                                    |
| InvoiceOrders                                             | Khóa kép (InvoiceId,OrderId), hai FK và mô hình gộp đơn                                                               | Kiểm soát đơn/phí không lên hai chứng từ, same-customer/session và chống lặp; không đổi sang Invoice.OrderId XOR                                                                                  | Không                                                       | Trường + Khang                            |
| InvoiceDetails                                            | InvoiceDetailId, InvoiceId, ItemType, Description, Quantity, UnitPrice, Amount                                        | Bổ sung truy vết OrderDetail/phí phiên và giảm phân bổ nếu cần; giữ snapshot bất biến                                                                                                             | Không                                                       | Trường; Phúc/Khang cung cấp nguồn         |
| Promotions (entity Promotion)                             | PromotionId, PromoName, DiscountType, DiscountValue, StartDate, EndDate, IsActive                                     | Thêm Code unique, MinOrderAmount, MaxDiscountAmount nullable, TotalUsageLimit/PerCustomerUsageLimit nullable; chuẩn hóa loại/offset; tiền decimal(18,0), DiscountValue decimal(18,2) theo mục 2.3 | Không tạo bảng Voucher khác                                 | Trường                                    |
| Chưa có PromotionUsage                                    | Không có bảng để tái sử dụng                                                                                          | FK int đến Promotions/Orders/Customers; xem rule usage                                                                                                                                            | PromotionUsages: từng lần dùng và hoàn lượt, unique OrderId | Trường                                    |
| Payments                                                  | PaymentId, InvoiceId, WalletTransactionId nullable, Amount, Method, PaymentTime, Status và FK đã có                   | Minh/Trường chốt status thành công, liên kết nguồn gateway/hoàn và operationKey; chưa coi PaymentTime là ngày thu thật nếu chưa có quy tắc nguồn; không coi Amount tự là tiền thu ròng            | Không tạo Payments thứ hai hoặc ledger thanh toán song song | Minh xử lý tiền; Trường đối soát chứng từ |
| WalletTransactions                                        | TransactionId, CustomerId, Amount, BalanceAfter, Type, Description, CreatedAt                                         | Đầu vào SCRUM-5: dấu/type, source IDs, idempotency, ngày thu/hoàn; chưa khẳng định có RecordedAt/OrderId/OrderDetailId                                                                            | Không tạo lại ví                                            | Minh                                      |
| GamingSessions, SessionCharges                            | Hai bảng phí phiên đã có, FK máy/khách và phí được ghi nhận                                                           | Phúc cung cấp snapshot giá giờ/khoảng phí và tổng đã trừ, chống đóng/thu lặp                                                                                                                      | Không                                                       | Phúc                                      |
| Top-up                                                    | Chưa có TopUp trong 35 DbSet đã kiểm tra                                                                              | Nhận schema/contract SCRUM-5 trước khi quyết định nguồn nạp/thu; không khẳng định top_ups đã có                                                                                                   | Theo phần Minh, chưa tự thiết kế bảng mới ở đây             | Minh                                      |
| Các bảng giải/team/khác                                   | Giữ nguyên schema ngoài phạm vi                                                                                       | Chỉ đối chiếu nguồn thu/hoàn nếu báo cáo dùng                                                                                                                                                     | Không                                                       | Owner tương ứng                           |

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

InvoiceDetail giữ Description/Quantity/UnitPrice/Amount làm snapshot. Cần truy vết dòng về
OrderDetail hoặc phí phiên; giảm phân bổ phải đọc từ snapshot bất biến, không từ voucher hiện tại.
Nếu field snapshot/truy vết còn thiếu thì bổ sung ở task triển khai sau, không đọc tên/giá mới để in đơn cũ.
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

| Method + route                                    | Policy bắt buộc                              | Request                             | Thành công                      |
| ------------------------------------------------- | -------------------------------------------- | ----------------------------------- | ------------------------------- |
| GET /api/admin/inventory                          | permission:stockreceipt.view                 | Không body                          | 200: item[]                     |
| GET /api/admin/inventory/movements                | permission:stockreceipt.view                 | Không body                          | 200: tối đa 500, mới nhất trước |
| POST /api/admin/inventory/movements, type Receive | permission:stockreceipt.create               | productId,type,quantityDelta,reason | 201: movement                   |
| POST /api/admin/inventory/movements, type Adjust  | permission:inventory.adjust                  | productId,type,quantityDelta,reason | 201: movement                   |
| PUT /api/admin/products/{id}/status               | permission:product.manage                    | isActive bắt buộc bool              | 200: item                       |
| GET /api/admin/invoices                           | permission:invoice.view                      | Không body                          | 200: tối đa 500, mới nhất trước |
| GET /api/client/invoices                          | role Customer, customer_id hợp lệ, ownership | ID khách từ token                   | 200: tối đa 200 của mình        |
| GET /api/admin/reports                            | permission:report.view                       | from/to tùy chọn YYYY-MM-DD         | 200: báo cáo theo nguồn thực    |

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

| Vai trò seed | API kho quản trị (xem/lịch sử/nhập/điều chỉnh/bật-tắt bán)                                        | Hóa đơn quản trị                                             | Voucher                                       |
| ------------ | ------------------------------------------------------------------------------------------------- | ------------------------------------------------------------ | --------------------------------------------- |
| Admin        | Được theo các permission mục 5.1                                                                  | invoice.view/create/void theo thao tác                       | promotion.view/manage                         |
| Manager      | Được theo các permission mục 5.1                                                                  | invoice.view/create/void theo thao tác                       | promotion.view/manage, được tạo/sửa/bật-tắt   |
| Cashier      | Không có stockreceipt.view/create, inventory.adjust hoặc product.manage: từ chối các endpoint này | Có invoice.view/create; không có invoice.void/payment.refund | Có promotion.view; không có promotion.manage  |
| Waiter       | Không thao tác hoặc xem sổ kho quản trị; product.view chỉ xem sản phẩm                            | Không có invoice.view/create/void: từ chối                   | Không có promotion.view/manage                |
| Technician   | Từ chối các endpoint kho                                                                          | Từ chối hóa đơn quản trị                                     | Từ chối voucher quản trị                      |
| Customer     | Từ chối endpoint quản trị                                                                         | Chỉ API hóa đơn của mình                                     | Chỉ preview giỏ của mình, không CRUD quản trị |

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
  Không tự tạo một hóa đơn cho từng lần đặt rồi lại gộp chúng; phát hành theo lần chốt tập nguồn đã chọn. Việc trả ví/cash/gateway vẫn ghi theo giao dịch thực, không đợi hóa đơn.
- Đơn trả ví trước khi chốt vẫn giữ debit và snapshot; phát hành không trừ ví thêm lần nữa.
  Cash chưa thu là Unpaid (API Pending); thu được xác minh mới Paid.
  Gateway chưa xác minh không Paid. Phiên dùng phí đã tính, không tự thu phí lần hai.
- Hóa đơn gộp chỉ Paid khi tổng phải trả còn lại đã thanh toán đủ theo nguồn thực.
  Khi đã phát hành không tùy ý thêm/bớt đơn để đổi snapshot; hủy/hoàn có truy vết riêng.
- Nhân viên phải có permission invoice.create; khách xem hóa đơn theo CustomerId của token.
  Tác vụ tự động không giả nhân viên: chỉ thực hiện khi có cơ chế actor hệ thống được nhóm định nghĩa.
- Caller mở transaction/DbContext chung; module con không tự commit hoặc mở transaction lồng.
  Kiểm tra cùng khách/cùng phiên trước khi nối; khóa các đơn theo ID tăng dần và kiểm tra nguồn
  đã được lập chứng từ trước khi tạo InvoiceOrder. Mọi caller phải theo cùng thứ tự khóa.
- Retry phát hành dùng operationKey ổn định và kiểm tra danh sách nguồn; không tạo chứng từ mới
  từ cùng lần chốt. Số tiền/EmployeeId/CustomerId không nhận tùy ý từ browser.
- Nguồn ledger/payment và operationKey chi tiết nhận từ Minh; chưa suy diễn tên cột hoặc trạng thái nguồn.
- Hủy toàn bộ một đơn chỉ hoàn lượt voucher của đơn đó, không hoàn lượt các đơn khác cùng hóa đơn.
- IssuedAt map InvoiceDate, không thay bằng ngày thu; SignalR chỉ phát sau commit.

## 7. Nguồn báo cáo và đối soát

Báo cáo đọc giao dịch đã ghi nhận, không cộng tổng Invoice làm doanh thu.

| Chỉ tiêu                             | Nguồn hiện có / cần nối                                                                                | Ngày ghi nhận                                                                 |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------- |
| Thu/hoàn qua ví                      | WalletTransactions đã có Amount/Type/CreatedAt; Minh phải cung cấp quy ước dấu/type và nguồn nghiệp vụ | CreatedAt, sau đối chiếu múi giờ; không giả định cột recorded_at đã có        |
| Thu qua Payment                      | Payments có Method/Status/Amount/PaymentTime, liên kết InvoiceId/WalletTransactionId                   | PaymentTime chỉ dùng khi contract xác nhận đó là lúc thu thành công           |
| Cash thực thu/hoàn                   | Nguồn cash đã xác nhận từ Khang/Minh, có thể nối Payment theo policy                                   | Ngày thực thu/hoàn; không lấy OrderTime/InvoiceDate thay thế                  |
| ZaloPay món                          | Payment/giao dịch Order được server xác minh                                                           | Ngày ghi nhận thu/hoàn thành công, không dùng redirect                        |
| Tiền chơi                            | GamingSessions/SessionCharges và nguồn thanh toán thực                                                 | Theo phí đã ghi nhận và khoản thực thu tương ứng; không cộng cùng phí hai lần |
| Nạp ví                               | Schema/contract nhận từ SCRUM-5; hiện không có TopUp trong 35 DbSet                                    | Báo riêng, không tính doanh thu dịch vụ                                       |
| Invoice/InvoiceOrders/InvoiceDetails | Đối soát snapshot, nguồn đơn/phiên, phải thu/đã thu/hủy                                                | InvoiceDate là ngày phát hành, không mặc định ngày dòng tiền                  |

Không áp enum dấu/type từ DBML cũ vào WalletTransactions.Type hiện tại khi Minh chưa bàn giao.
Payments và WalletTransactions có thể đại diện cùng khoản qua WalletTransactionId:
chọn một nguồn tính, không cộng cả hai. Payment.Amount không tự là số thu ròng hoặc khoản hoàn.
Method/Status thành công, ID nguồn/operationKey và cách thể hiện refund phải nhận từ contract tiền.

Công thức logic sau khi nguồn đã chuẩn hóa và không trùng:

```text
netRevenue =
    Wallet thu dịch vụ - Wallet hoàn dịch vụ
  + Cash thực thu - Cash thực hoàn
  + ZaloPay món thực thu - ZaloPay món thực hoàn
```

Nạp ZaloPay vào ví vẫn là nạp ví, không thuộc doanh thu ZaloPay món.
Khoản hoàn tính ngày thực ghi nhận hoàn, không sửa lại doanh thu ngày đặt.
Thiếu nguồn thu/hoàn thì báo chưa đối soát được, không trả số 0 như thể đã kiểm tra đủ.

Ngày from/to dạng YYYY-MM-DD; từ đầu ngày +07:00 đến trước đầu ngày sau to.
Đơn đếm theo OrderTime của nguồn đích (DTO placedAt nếu có); các bộ đếm hệ thống dùng dữ liệu
hiện tại theo định nghĩa báo cáo đã chốt. Pending top-up chưa triển khai khi chưa có nguồn từ Minh.
Không lấy dữ liệu hóa đơn seed không có ledger/payment làm chứng cứ doanh thu thực.

## 8. Owner, phần bổ sung và thứ tự migration

| Owner  | Giữ và chịu trách nhiệm nguồn                                          | Phần bổ sung / contract giao nhận                                                            |
| ------ | ---------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| Đại    | Identity Employee/Customer, role/permission, auth JWT; UI voucher      | Claims/quyền theo SCRUM-2; không thay code auth trong SCRUM-12                               |
| Minh   | Customers/WalletTransactions/Payments và nguồn nạp/gateway             | SCRUM-5 xác nhận dấu/type, nguồn thu/hoàn, operationKey và thời gian                         |
| Phúc   | Computers/GamingSessions/SessionCharges                                | Snapshot phí/giá giờ, tổng đã ghi nhận, kết thúc phiên và actor                              |
| Khang  | Categories/Products/Orders/OrderDetails                                | Giữ Stock/IsActive; thêm snapshot món/giảm, trạng thái hủy và điểm gọi rule                  |
| Trường | InventoryTransactions/Promotions/Invoices/InvoiceOrders/InvoiceDetails | Mở rộng các bảng sẵn có; thêm PromotionUsages, query/DTO và rule; điều phối thứ tự migration |

Thứ tự tích hợp trên nền CyberManagement 35 bảng:

1. Giữ lịch sử migration nguồn, xác nhận commit nền và đúng database thử.
   Không tạo lại Employees/Customers/Products/Invoices hoặc áp InitialCreate của hệ 19 bảng vào đây.
2. Minh/Khang/Phúc bổ sung nguồn payment/đơn/phiên và snapshot thuộc phạm vi họ.
   Trường đối chiếu chỉ những field/constraint Product thực sự thiếu; không thêm StockQuantity cạnh Stock.
3. Trường mở rộng Promotions đã có; backfill Code/giá trị trước unique/check, kiểm tra ngày/precision.
   Cùng Khang thêm snapshot voucher trên Orders/OrderDetails; tạo PromotionUsages sau khi FK nguồn đã có.
4. Trường mở rộng InventoryTransactions, InvoiceDetails và phần truy vết/tiền chứng từ cần thiết.
   Giữ InvoiceOrders; đối soát trùng đơn/nguồn trước khi áp constraint chống phát hành trùng.
5. Rà backfill/type/precision/offset, thêm constraint/index sau dữ liệu hợp lệ, chạy trên DB thử mới
   với seed nhất quán; cập nhật schema/SQL mô tả theo migration đã tích hợp ở S1-T-2.

Mỗi owner viết entity/config phần mình. Một PR migration xử lý tại một thời điểm.
Không sửa/xóa migration đã áp vào DB chung; migration bổ sung phải nối tiếp lịch sử CyberManagement hiện tại.
ID long cần mapping/range check sang int; không tạo ID nhân viên giả, không đổi tiền/ngày cũ không đối soát.
Code/seed ở CyberCafe_SCRUM13_Test trước đây chỉ chứng minh baseline cũ, không là bằng chứng nền 35 bảng đã tích hợp.
Phần migration được thực hiện và kiểm thử trong task tích hợp theo thứ tự trên.

## 9. Kết quả đối chiếu và đầu vào còn chờ

### 9.1. Quyết định thiết kế

| Nội dung          | Quyết định                                                                                 |
| ----------------- | ------------------------------------------------------------------------------------------ |
| Backend và schema | CyberManagement 35 bảng, cấu trúc Backend/src và ApplicationDbContext                      |
| Định danh         | EmployeeId/CustomerId kiểu int; không bổ sung Account                                      |
| Kho               | Mở rộng InventoryTransactions hiện có; giữ Products.Stock/IsActive                         |
| Voucher           | Mở rộng Promotions, thêm PromotionUsages; một voucher trên mỗi đơn                         |
| Hóa đơn           | Giữ InvoiceOrders, cho phép gộp nhiều đơn cùng khách; phát hành khi nhân viên chốt hóa đơn |
| Quyền             | Theo permission từng endpoint tại mục 5, khớp catalog SCRUM-2                              |
| Doanh thu         | Tính theo nguồn thu/hoàn đã xác nhận; tách nạp ví và chống cộng trùng                      |

### 9.2. Kết quả đối chiếu theo thành viên

Bảng dưới phân biệt kết quả rà soát mã nguồn với xác nhận trực tiếp của owner.
Các quyết định thiết kế được dùng làm cơ sở bàn giao; phản hồi của từng bên được cập nhật khi nhận.

| Thành viên | Kết quả đối chiếu                                                                                                                                                                                                                   | Đầu vào / xác nhận còn chờ                                                                                                |
| ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| Đại        | Đã rà SCRUM-2 tại 15d537d và schema/catalog trên dev local tại a975f8d. Contract sử dụng JWT/permission, int, Employee/Customer; giữ nền 35 bảng, Payments và InvoiceOrders. Bộ auth có 35 HTTP checks đạt trên TestServer/InMemory | Đối chiếu trực tiếp contract endpoint; policy khóa tài khoản sau cấp token nếu yêu cầu có hiệu lực ngay                   |
| Minh       | Đã xác định SCRUM-5 tương ứng S1-M-1. Mapping giữ WalletTransactions/Payments; chưa xác nhận schema nạp ví hoặc quy ước dấu/type của contract mới                                                                                   | Contract credit/debit/refund, source IDs, operationKey, trạng thái/ngày thu-hoàn, nạp tại quầy/gateway và mapping dữ liệu |
| Phúc       | Đã đối chiếu nền GamingSessions/SessionCharges và phần phân công. Hóa đơn lấy phí đã ghi nhận, không thu lại hoặc cộng trùng tiền chơi                                                                                              | Snapshot giá giờ/khoảng phí, thời điểm kết thúc, chống thu/đóng lặp, actor và điểm gọi phát hành                          |
| Khang      | Đã đối chiếu Products/Orders/OrderDetails và phần phân công. Giữ Stock/IsActive, thêm snapshot món/giảm theo contract; voucher áp từng đơn                                                                                          | Contract đặt/hủy/cash, field snapshot cần bổ sung, điểm gọi transaction; cùng Minh xác nhận policy checkout/gateway       |

Chưa có bằng chứng xác nhận trực tiếp của cả bốn owner hoặc quyết định duyệt cuối.
Việc rà code không thay cho xác nhận giao điểm cần bàn giao.

### 9.3. Phụ thuộc cần giải quyết trước khi tích hợp

| Đầu vào                                                                   | Người cung cấp                             | Phạm vi ảnh hưởng                                     |
| ------------------------------------------------------------------------- | ------------------------------------------ | ----------------------------------------------------- |
| Nguồn ledger/payment, operationKey và thứ tự khóa ví/phiên                | Minh, phối hợp Khang/Phúc                  | Thu/hoàn, cập nhật chứng từ và đối soát doanh thu     |
| Thời hạn giữ tồn/lượt, hết hạn, callback muộn và phương thức hoàn gateway | Minh + Khang; Trường cung cấp rule voucher | Trạng thái giữ/giải phóng lượt và mapping reservation |
| Snapshot và thao tác đóng phiên/hủy đơn                                   | Phúc + Khang                               | Dữ liệu hóa đơn, trả tồn và hoàn voucher              |
| Migration/PR của từng owner và dữ liệu chuyển đổi                         | Các owner liên quan                        | Thứ tự tích hợp, backfill và kiểm thử trên DB mới     |
| Policy khóa sau cấp token                                                 | Đại                                        | Kiểm thử truy cập sau khi tài khoản bị vô hiệu hóa    |

Rule voucher, API của Trường và các quyết định tại mục 9.1 là nội dung thiết kế do owner đề xuất để bàn giao; chưa đồng nghĩa các bên liên quan đã xác nhận contract chung.
Các bên cung cấp đầu vào của module mình và phản hồi những xung đột cụ thể trước khi triển khai.
Không tự thay quy tắc hoặc duy trì hai schema/rule khác nhau.

## 10. Kiểm chứng và tiêu chí bàn giao

### 10.1. Bằng chứng hiện có

| Hạng mục             | Kết quả và giới hạn                                                                                                                |
| -------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| Schema               | Đối chiếu ApplicationDbContext nền dev local a975f8d: 35 DbSet; mapping các bảng liên quan tại mục 3                               |
| Quyền                | Đối chiếu RolePermissionCatalog và policy handler SCRUM-2; permission được ghi tại từng endpoint                                   |
| Auth                 | Lượt kiểm tra trước tại commit 15d537d: 35 HTTP checks đạt, exit 0, TestServer + EF InMemory                                       |
| Phạm vi test auth    | Login đúng/sai, permission, token bị sửa, customer login/ownership và logout; chưa chứng minh SQL hoặc tích hợp module kho/voucher |
| Tài liệu             | Rà liên kết, tên bảng/field, permission, mapping và tính nhất quán của các quyết định                                              |
| Xác nhận liên module | Theo trạng thái tại mục 9.2; chưa ghi nhận đầy đủ phản hồi trực tiếp hoặc quyết định duyệt                                         |

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
  thu/hoàn lặp, hóa đơn 0 đồng và hủy một phần hóa đơn gộp.
- Quyền: Manager quản lý voucher; Cashier chỉ dùng quyền được cấp;
  Waiter/Technician không truy cập kho hoặc hóa đơn quản trị; khách không xem hóa đơn người khác.
- Báo cáo: đúng nguồn và ngày ghi nhận, tách nạp ví, không cộng cùng khoản Payment/Wallet/Invoice nhiều lần.
- Gateway: trạng thái chờ/thành công/hủy/hoàn, hết hạn và callback muộn theo policy được xác nhận.

## 11. Tài liệu và mã nguồn tham chiếu

Nguồn schema đã đọc: dev local tại a975f8d6f329266d954090521469975c5e8dcdb6.
Nguồn auth đã kiểm tra: SCRUM-2 tại 15d537d. Các nguồn được ghim theo commit để tái hiện việc đối chiếu.

- [ApplicationDbContext và nền 35 bảng](https://github.com/imLCKKK/CyberCafe-Management-System-Project/blob/a975f8d6f329266d954090521469975c5e8dcdb6/Backend/src/CyberManagement.Infrastructure/Persistance/ApplicationDbContext.cs).
- [Định danh và ma trận quyền](https://github.com/imLCKKK/CyberCafe-Management-System-Project/blob/a975f8d6f329266d954090521469975c5e8dcdb6/Backend/docs/001-monorepo.md).
- [RolePermissionCatalog](https://github.com/imLCKKK/CyberCafe-Management-System-Project/blob/a975f8d6f329266d954090521469975c5e8dcdb6/Backend/src/CyberManagement.Application/Security/RolePermissionCatalog.cs).
- [Backlog và tiêu chí](../../codex/SPRINT_TASKS.md).
- [Kế hoạch và owner](../../planning/team-delivery-plan.md).
- [Quy trình nhóm](../../planning/team-workflow.md).
