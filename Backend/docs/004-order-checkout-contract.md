# 004 – Hợp đồng đơn món và checkout (S1-K-2)

| Thông tin | Nội dung |
| --- | --- |
| Task | Define-Order-Checkout-Contracts / S1-K-2; chưa có Jira key được cung cấp |
| Owner | Khang; Minh đối chiếu ví/gateway, Trường đối chiếu tồn/voucher/chứng từ |
| Reviewer cuối | Trưởng nhóm |
| Phiên bản | 0.2, ngày 07/11/2026, đã đối chiếu nguồn và sửa các điểm lệch; đề nghị Trưởng nhóm review |
| Backend đích | CyberManagement .NET 10 / SQL Server, nền 35 bảng |
| Phụ thuộc | S1-M-1 và S1-T-1; giao diện gateway bàn giao tiếp cho S1-M-2 |

**Trạng thái:** sẵn sàng để Trưởng nhóm review bản thiết kế sau đối chiếu tĩnh với các nguồn tại mục 1.
Kết quả đối chiếu kỹ thuật và các quyết định còn cần nhóm xác nhận được tách rõ ở mục 11;
chưa có phản hồi chấp thuận của Minh/Trường/Phúc hoặc quyết định duyệt của Trưởng nhóm.
Các endpoint, DTO, trạng thái và phần bổ sung schema bên dưới là thiết kế đề xuất, chưa phải API đang chạy.
Quyết định chung đã có trong PROJECT_GUIDE được giữ; các lựa chọn cụ thể cần đối chiếu được gom tại mục 11.
S1-K-2 bàn giao thiết kế. Code checkout thuộc S2-K-1/S2-K-2; UI thuộc S2-K-3;
hoàn thiện cạnh tranh/hủy/hoàn thuộc S3-K-1. Không tick Done chỉ vì đã có tài liệu.

## 1. Căn cứ và giới hạn hiện trạng

- [PROJECT_GUIDE](../../codex/PROJECT_GUIDE.md), [task S1-K-2](../../codex/SPRINT_TASKS.md#s1-k-2--define-order-checkout-contracts)
  và [các quyết định cần nhóm chốt](../../planning/team-delivery-plan.md#11-quyết-định-nghiệp-vụ-cần-nhóm-chốt-trong-s1).
- Nhánh làm việc: `SCRUM-11-Define-Order-Checkout-Contracts`; base từ `dev` tại
  `e14a362e5f635db62deebf7752493672e755a24e`. Nội dung hợp đồng đang là thay đổi chưa commit.
  [Order](../src/CyberManagement.Domain/Entities/Order.cs),
  [OrderDetail](../src/CyberManagement.Domain/Entities/OrderDetail.cs),
  [Payment](../src/CyberManagement.Domain/Entities/Payment.cs) và EF configuration hiện có
  chưa đủ snapshot giảm, trạng thái từng dòng, giữ chỗ và chống lặp. Chưa có controller/use case đơn món trên base này.
- [Identity](001-monorepo.md) và [RolePermissionCatalog](../src/CyberManagement.Application/Security/RolePermissionCatalog.cs)
  là nguồn định danh/quyền. CustomerId và EmployeeId là hai miền int khác nhau.
- Đầu vào ví đã đọc: commit `8b2e6a21bb3d3d1f081bd4b1497b39154af971b5`,
  Git path `backend/docs/002-wallet-ledger.md` và code `WalletLedger`/`ICustomerRepository` tương ứng.
  Dùng `OrderPayment` âm, `OrderRefund` dương; Record không tự lưu/commit.
- Đầu vào kho/chứng từ/voucher đã đọc: commit `761333c4bc32fe3ae5f6bc524b6a2ba8526db864`,
  Git path `docs/contracts/inventory-invoice-voucher.md`, phiên bản 1.7 chờ duyệt.
  Giữ thiết kế một voucher/đơn, phân bổ giảm theo dòng, Payments neo nguồn trước hóa đơn,
  hoàn theo phương thức đã trả và thứ tự khóa chung. Đây là đầu vào đối chiếu, chưa phải xác nhận liên module.
- Đã kiểm tra thêm bản 1.6 của S1-T-1 tại `280ed713627d037d770f6e02bc1d248ef4f98a70`
  (`origin/SCRUM-12-Define-Inventory-Invoice-Voucher-Contracts` trong các ref local).
  Quy tắc hoàn từng phần và ngoại lệ thu muộn Failed của bản 1.6/1.7 khớp nhau; bổ sung
  InvoiceIssueRequests và các mốc thời gian của bản 1.7 vẫn là phương án chưa được nhóm duyệt.
- Đầu vào phiên đã đọc: `761333c4bc32fe3ae5f6bc524b6a2ba8526db864`, Git path
  `Backend/docs/002-computer-session-contract.md`, mục 3–5: quyết toán phí phiên trước debit ví khác.
  Hợp đồng ví cùng thời điểm lại đề xuất thu trước theo chu kỳ; giao điểm này chưa có quyết định chung,
  phương án checkout để review ghi tại mục 7.3 và 11.2.
- Các đầu vào trên chưa có trong cây base đang mở; tra bằng `git show <commit>:<path>` khi có ref.
  Nếu clone chưa có commit, cần owner cung cấp PR/commit trước tích hợp; không phụ thuộc đường dẫn `.tools` của máy người viết.
- UI hiện dùng [demoModel](../../frontend/src/client/shared/demoModel.js) và
  [adminModel](../../frontend/src/admin/shared/adminModel.js): ví trừ lúc đặt, cash chờ thu,
  khách gửi yêu cầu hủy, nhân viên xử lý từng dòng Pending. Hợp đồng dưới đây giữ luồng đó,
  bổ sung quyền phía server, voucher và gateway; không coi localStorage là bằng chứng API/SQL.

## 2. Quy ước và bất biến

- JSON camelCase; enum là chuỗi phân biệt hoa/thường. ID là int dương. Thời gian API dùng ISO 8601 UTC (`Z`),
  tính toán bằng một mốc `now` phía server cho mỗi transaction; UI hiển thị giờ Việt Nam (+07:00).
- Tiền là số nguyên VND, C# decimal. Tạm giới hạn mỗi số tiền và tổng đơn ở `999_999_999` VND,
  phù hợp cột `decimal(12,2)` hiện tại và số nguyên JSON an toàn. Phần lẻ/tràn bị từ chối trước ghi.
  S1-T-1 đề xuất tiền `decimal(18,0)`, DiscountValue `decimal(18,2)`; owner cần đối chiếu migration,
  không âm thầm đổi precision hoặc nâng giới hạn API trong task thiết kế này.
- Server lấy giá, tính giảm/tổng, kiểm tra tồn, khách, phiên, quyền và trạng thái hiện tại.
  Request chứa giá, tổng tiền, số dư, status, người xử lý hoặc trường ngoài DTO bị trả 400.
- Mỗi đơn chọn một phương thức `Wallet`, `Cash` hoặc `ZaloPay`, không đổi phương thức hay sửa giỏ sau khi tạo.
  Chưa hỗ trợ chia nhiều phương thức trong một đơn. Đổi giỏ/phương thức cần hủy hợp lệ và tạo ý định mới.
- `OriginalPayable = Subtotal - DiscountAmount`; snapshot gốc không đổi sau hủy.
  `CancelledAmount` là tổng giá trị sau giảm của các dòng đã hủy.
  `PayableAmount = OriginalPayable - CancelledAmount`.
  `CollectedAmount`/`RefundedAmount` chỉ tính khoản thu/hoàn Succeeded.
  `RefundPendingAmount` chỉ cộng các dòng Refund Pending đang giữ hạn mức.
  `RefundUnresolvedAmount = max(0, min(CancelledAmount, CollectedAmount) - RefundedAmount)`
  là số tiền của phần hủy đã thu nhưng chưa hoàn xong, gồm cả trường hợp Refund Failed cần xử lý tiếp.
  `OutstandingAmount = max(0, PayableAmount - (CollectedAmount - RefundedAmount))`.
  Tiền thực thu dư do callback muộn vẫn được hiển thị và đối soát, không cắt xuống bằng PayableAmount.
- Không âm ví/tồn; không vượt lượt voucher; mỗi nguồn chỉ thu/hoàn/trừ/trả tồn đúng một lần.
  Lỗi SQL rollback toàn bộ thay đổi trong thao tác. Không giữ SQL transaction khi gọi/chờ gateway.
- Đơn 0 đồng sau giảm: `paymentStatus=NotRequired`, không gọi ví/gateway, không tạo Payment 0 đồng.
  Vẫn kiểm tra quyền, tồn, snapshot và lượt voucher như đơn thông thường.

## 3. HTTP và quyền

Tất cả route dưới đây yêu cầu JWT. Kiểm tra loại principal và trạng thái tài khoản từ DB tại thao tác,
không chỉ tin role/permission còn hạn trong token. Nhân viên bị vô hiệu hóa nhận 403.
Khách tạo đơn phải Active; hoàn theo nguồn đã được duyệt vẫn thực hiện được khi khách Locked.
Khách đọc/yêu cầu hủy chỉ được thao tác dữ liệu của mình; route người khác trả 404.

| Method / route đề xuất | Đầu vào / kết quả | Quyền server |
| --- | --- | --- |
| `POST /api/client/orders` | CreateOrderRequest → 201 OrderDto | Customer; CustomerId từ `customer_id`; phiên Active thuộc khách |
| `GET /api/client/orders` | `page=1&pageSize=20`, tối đa 100 → OrderPageDto | Customer; luôn lọc theo claim |
| `GET /api/client/orders/{orderId}` | 200 OrderDto | Customer sở hữu đơn |
| `POST /api/client/orders/{orderId}/cancellation-requests` | CancellationRequest → 201 CancellationDto | Customer sở hữu đơn; chỉ tạo yêu cầu, không tự hoàn |
| `POST /api/admin/orders` | StaffCreateOrderRequest → 201 OrderDto | `order.create`; Wallet/ZaloPay thêm `payment.create`; Waiter chỉ tạo Cash |
| `GET /api/admin/orders` | page/pageSize như client, thêm status/customerId tùy chọn → OrderPageDto | `order.view` |
| `GET /api/admin/orders/{orderId}` | 200 OrderDto | `order.view` |
| `POST /api/admin/orders/{orderId}/items/{orderDetailId}/confirm` | VersionRequest → 200 OrderDto | `order.status.update` |
| `POST /api/admin/orders/{orderId}/cancellations` | CancelOrderRequest → 200 OrderDto | `order.cancel`; nếu phát sinh hoàn ví thêm `wallet.refund`, gateway thêm `payment.refund` |
| `POST /api/admin/orders/{orderId}/cancellation-requests/{requestId}/reject` | RejectCancellationRequest → 200 OrderDto | `order.cancel` |
| `POST /api/admin/orders/{orderId}/cash-collections` | VersionRequest → 200 OrderDto | `payment.create`; server tính khoản còn phải thu |
| `POST /api/admin/orders/{orderId}/refunds/{refundId}/approve` | VersionRequest → 200 OrderDto, chưa đồng nghĩa đã hoàn | `payment.refund`; chỉ Refund ZaloPay Pending đã có, server lấy nguồn/amount; Failed/Succeeded trả 409 nếu không phải replay đã lưu |

Mọi POST trong bảng bắt buộc `Idempotency-Key`; quy tắc retry ở mục 8. API kiểm tra dòng/request/Refund
thuộc đúng orderId trên route. Tạo đơn staff nhận CustomerId vì là đặt thay; EmployeeId luôn lấy JWT.
Staff có thể tạo tại quầy với GamingSessionId null; nếu có phiên phải Active và thuộc khách đã chọn.
Client phải truyền phiên Active của chính mình; không lấy CustomerId/EmployeeId từ body.
`order.status.update` không cho đổi trạng thái tự do hay hoàn tiền. Cashier/Waiter thiếu `order.cancel`,
`wallet.refund`, `payment.refund` theo catalog hiện tại; không tự mở rộng quyền qua endpoint hủy.
Callback/query gateway là adapter server của Minh, có xác minh provider; không dùng JWT khách
hoặc quyền nhân viên giả để ghi thanh toán. Redirect browser chỉ dẫn về trang đọc lại OrderDto.
page phải >=1, pageSize trong 1..100; status thuộc tập ở mục 5, customerId là int dương.

## 4. DTO đề xuất

DTO ở đây là đặc tả để tạo C# record/validator trong Application khi triển khai S2;
entity EF không được dùng trực tiếp làm request/response.

| DTO | Trường / kiểu / validation |
| --- | --- |
| CreateOrderRequest | `gamingSessionId: int` bắt buộc; `items: CreateOrderItem[]` 1..100 dòng; `paymentMethod: Wallet/Cash/ZaloPay`; `voucherCode: string?` |
| StaffCreateOrderRequest | Như CreateOrderRequest nhưng `gamingSessionId: int?`, thêm `customerId: int` bắt buộc |
| CreateOrderItem | `productId: int`, `quantity: int` 1..99; không lặp productId; giữ thứ tự dòng để phân bổ đồng dư |
| VersionRequest | `expectedVersion: string` bắt buộc, token rowversion dạng base64, server cấp; không tự tăng tại client |
| CancellationRequest | `itemIds: int[]?`, `reason: string` trim 1..250; null nghĩa toàn đơn; mảng phải khác rỗng, không trùng, cùng đơn; hủy cả số lượng của dòng |
| CancelOrderRequest | CancellationRequest + expectedVersion; thêm `cancellationRequestId: int?`; nếu có phải Pending và khớp tập dòng yêu cầu |
| RejectCancellationRequest | VersionRequest + `reason: string` trim 1..250; requestId trên route phải Pending |
| CancellationDto | `id`, `orderId`, `itemIds: int[]` đã chuẩn hóa; `status: Pending/Approved/Rejected`; `reason`, `resolutionReason: string?`, `requestedAtUtc`, `resolvedAtUtc: string?` |
| OrderItemDto | `id` map OrderDetailId; `productId`, `productNameSnapshot`, `quantity`, `unitPrice`, `discountAmount`, `netAmount`, `status: Pending/Confirmed/Cancelled`, `cancelReason: string?` |
| PaymentSummaryDto | `method`, `paymentStatus`, `collectedAmount`, `refundedAmount`, `refundPendingAmount`, `refundUnresolvedAmount`, `outstandingAmount`; `paymentId: int?`; `paymentUrl: string?` chỉ URL sandbox do adapter trả; `expiresAtUtc: string?`; `requiresReconciliation: bool` |
| OrderDto | `id` map OrderId; `customerId`, `gamingSessionId: int?`, `createdByEmployeeId: int?`, `createdAtUtc`, `status`, `version`, `subtotal`, `discountAmount`, `originalPayable`, `cancelledAmount`, `payableAmount`, `voucher: VoucherSnapshotDto?`, `items: OrderItemDto[]`, `payment: PaymentSummaryDto`, `cancellationRequests: CancellationDto[]` |
| VoucherSnapshotDto | `promotionId`, `code`, `discountType: Percent/Fixed`, `discountValue`, `discountAmount`; tất cả do server chụp lúc đặt |
| OrderPageDto | `items: OrderDto[]`, `page`, `pageSize`, `totalCount`; sắp `createdAtUtc` giảm dần, rồi id giảm dần |

Voucher code trim, uppercase invariant, 1..50 ký tự ASCII chữ/số/`-`/`_`; null là không áp dụng.
Mã sai/hết hạn/hết lượt trả lỗi, không tự bỏ voucher rồi thu giá cao hơn. Reason lưu nội bộ đầy đủ;
Minh chuyển sang mô tả provider theo adapter đã xác minh, không cắt reason lịch sử.
V1 hủy cả dòng OrderDetail, chưa hủy một phần quantity. Snapshot unitPrice dùng chính cột UnitPrice hiện có.

Ví dụ tạo đơn (ID minh họa, giá do server đọc):

```http
POST /api/client/orders
Authorization: Bearer <customer-token>
Idempotency-Key: checkout:550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json

{
  "gamingSessionId": 12,
  "items": [
    { "productId": 7, "quantity": 2 },
    { "productId": 9, "quantity": 1 }
  ],
  "paymentMethod": "Wallet",
  "voucherCode": "FOOD20"
}
```

Nếu server đọc đơn giá 50000 và 50000, giảm cố định 30000 thì subtotal=150000,
discountAmount=30000, originalPayable=120000; hai dòng có discountAmount=20000/10000,
netAmount=80000/40000. Ví bị trừ 120000 đúng một lần. Hủy dòng đầu hoàn 80000,
payableAmount còn 40000, không tính lại voucher dù đơn còn lại thấp hơn ngưỡng ban đầu.

## 5. Vòng đời đơn, dòng và yêu cầu hủy

Đơn tách trạng thái phục vụ khỏi thanh toán. Trạng thái tổng được tính lại sau mỗi đổi dòng:
toàn bộ Cancelled → Cancelled; toàn bộ Confirmed → Confirmed; toàn bộ Pending → Pending;
các tổ hợp khác → Mixed. Riêng AwaitingPayment/Expired được xác định bởi luồng gateway.

| Nguồn → đích | Điều kiện / tác động |
| --- | --- |
| Tạo Wallet → Pending | SQL chấp nhận đơn và debit cùng transaction; đây là thời điểm “xác nhận đơn” với ví, trước khi nhân viên xác nhận từng món |
| Tạo Cash → Pending | Giữ/trừ tồn, dùng lượt voucher; PendingCollection; chưa có khoản thu |
| Tạo đơn 0 đồng → Pending | Bất kỳ phương thức; NotRequired, giữ tồn/lượt nhưng không tạo khoản thu hay yêu cầu gateway |
| Tạo ZaloPay → AwaitingPayment | Tổng > 0; giữ tồn/lượt, lưu Payment Pending và mã merchant trước lời gọi ngoài SQL |
| AwaitingPayment → Pending | Server xác minh thu đúng giao dịch/số tiền, giữ chỗ còn hạn; không trừ tồn/lượt lần nữa |
| AwaitingPayment → Expired | Hết hạn hoặc tạo gateway bị từ chối dứt khoát; hủy các dòng, giải phóng tồn/lượt một lần; chưa chứng minh provider chưa thu |
| Pending/Mixed → Confirmed/Mixed | Chỉ confirm dòng Pending; gateway phải đã Succeeded/NotRequired; chưa áp dụng cho AwaitingPayment/Expired |
| Pending/Mixed → Cancelled/Mixed | Admin/Manager duyệt hủy dòng Pending; hoàn/giảm phải thu và trả tồn theo mục 7 |
| AwaitingPayment → Cancelled | Chỉ hủy toàn đơn; vô hiệu giữ chỗ và giải phóng; payment tiếp tục được đối soát nếu còn chưa rõ |
| Expired/Cancelled + thu muộn | Giữ nguyên đơn và tồn/lượt đã giải phóng; ghi nhận khoản thực thu, tạo nghĩa vụ hoàn/đối soát, không khôi phục đơn |

Dòng chỉ chuyển `Pending → Confirmed` hoặc `Pending → Cancelled`; Confirmed/Cancelled là cuối cho
luồng thông thường. Hủy toàn đơn chỉ thành công nếu tất cả dòng còn hiệu lực đều Pending; có dòng
Confirmed trả 409, không hủy một phần âm thầm. Khi cần hủy các dòng Pending còn lại, gửi itemIds cụ thể.
AwaitingPayment chỉ cho yêu cầu/hủy toàn đơn để số tiền gửi gateway giữ nguyên; không hủy một dòng
khi khoản thu còn chưa xác định. Đơn hết hạn/hủy không được đổi lại thành Pending bằng route nhân viên.

Khách gửi yêu cầu hủy chỉ tạo CancellationDto Pending. Một dòng không nằm trong hai yêu cầu Pending;
request toàn đơn lưu tập dòng tại thời điểm gửi. Yêu cầu không giữ quyền hủy: nếu nhân viên confirm
dòng trước, yêu cầu Pending liên quan được Rejected cùng transaction với lý do dòng đã xác nhận.
Duyệt yêu cầu phải kiểm tra lại toàn bộ tập dòng dưới khóa; nếu có dòng không còn hợp lệ trả 409,
không duyệt một phần. Hủy trực tiếp dòng đóng các yêu cầu Pending giao nhau thành Rejected
(hoặc Approved nếu đúng request được duyệt); ghi người xử lý, thời gian và lý do trong cùng transaction.

## 6. Snapshot, voucher và tồn

1. Server khóa và đọc lại Product đang bán, đủ tồn; snapshot tên món, UnitPrice, Quantity, thứ tự dòng.
2. Tính subtotal; gọi rule Trường cho một voucher món ăn, không áp phí phiên/nạp ví.
   Hiệu lực `StartDate <= now < EndDate`, ngưỡng trên subtotal trước giảm.
   Giảm % hoặc cố định, áp trần và không vượt subtotal; làm tròn đồng `AwayFromZero`.
3. Phân giảm theo tỷ lệ giá trị dòng: lấy phần nguyên, cấp đồng dư theo phần lẻ giảm dần,
   hòa theo thứ tự dòng lúc tạo. Tổng giảm dòng bằng giảm đơn. Không tính lại sau hủy.
4. Giữ tồn bằng cách giảm `Products.Stock` (tồn khả dụng) ngay khi tạo đơn được chấp nhận,
   ghi một InventoryTransaction `Sale` âm/dòng. Áp dụng cả Wallet, Cash và ZaloPay đang chờ.
   Callback/confirm chỉ chuyển trạng thái, không giảm lần hai. Không thêm ReservedStock trùng nghĩa.
5. Voucher có một PromotionUsage/đơn ngay khi tạo, kể cả cash chưa thu/gateway đang chờ;
   usage chưa ReleasedAt chiếm hạn mức. Preview không chiếm lượt. Cạnh tranh kiểm tra dưới khóa Promotion.
6. Hủy/expired cả dòng Pending trả đúng Quantity bằng một `Return` dương/dòng.
   Hủy một phần đơn không hoàn lượt; tất cả dòng đã Cancelled (gồm Expired) giải phóng usage đúng một lần.
   Không đổi voucher trên đơn cũ, không phục hồi lượt nếu callback muộn và không gia hạn voucher.

Product/voucher đổi giá, tên, trạng thái bán, hiệu lực hoặc hạn mức sau khi đặt không sửa snapshot cũ.
Gateway còn giữ chỗ hợp lệ có thể xác nhận theo snapshot đã chốt mà không chạy lại điều kiện voucher mới.
Hủy dòng 0 đồng vẫn trả tồn; không ghi ledger/Refund 0 đồng. Không trả tồn cho dòng đã Confirmed
theo policy thông thường; không tự suy từ hoàn tiền là hàng đã quay lại kho.

## 7. Thu tiền, hết hạn và hoàn

### 7.1. Ví và tiền mặt

| Tình huống | Quy tắc đề xuất |
| --- | --- |
| Wallet đặt đơn | Khóa khách và nguồn theo mục 8, xử lý phí phiên theo phương án mục 7.3 trước khi xét số dư đơn; tìm khóa `OrderPayment:{orderId}`, gọi WalletLedger.Record với `OrderPayment`, amount âm; phí phiên/đơn/tồn/usage/ledger/Payment cùng transaction |
| Wallet hủy dòng | Khóa nguồn, hoàn netAmount snapshot bằng `OrderRefund`, khóa `OrderRefund:{orderId}:{orderDetailId}`; liên kết Refund với khoản thu gốc và ledger; khách Locked vẫn nhận được hoàn |
| Cash tạo/chờ | PendingCollection; không debit/credit ví, không ghi khoản thu Succeeded |
| Cash thu | Chỉ khi tất cả dòng còn hiệu lực Confirmed, PayableAmount > 0; người có payment.create xác nhận thực nhận; server ghi đúng số còn phải thu, không lấy amount từ browser |
| Cash hủy trước thu | Giảm PayableAmount; trả tồn theo dòng; không tạo Refund và không cộng ví |
| Cash đã thu | Luồng thường không hủy dòng Confirmed. Nếu có xử lý ngoại lệ được duyệt ở S3, phải xác nhận thực hoàn theo payment.refund, truy khoản thu gốc; không tự hoàn vào ví |

Khóa ledger và khóa HTTP là hai lớp khác nhau: ledger ngăn trừ/hoàn cùng nguồn hai lần;
khóa HTTP ngăn tạo nhiều OrderId từ cùng ý định. Không ghép khóa client dài vào khóa ledger.
Tổng Refund Pending + Succeeded không vượt khoản thu gốc hoặc giá trị hủy đủ điều kiện.
Yêu cầu hoàn khác khóa cũng không được hoàn lại cùng dòng. Wallet hủy cả đơn ghi các khoản hoàn
theo dòng chưa hoàn với khóa OrderRefund ở bảng trên. ZaloPay hủy toàn đơn có thể dùng một Refund
OrderDetailId null cho phần còn lại: amount do server tính từ tập dòng chưa được bao phủ bởi Refund
Pending/Succeeded. Lưu tập nguồn hủy trong thao tác, khóa khoản thu gốc và trừ cả phần đang chờ;
không thêm khoản hoàn tổng trùng khoản dòng. Khóa ổn định `OrderRefund:{orderId}:All` cho phần hoàn này.
V1 thu cash từng đơn; thu cash gộp/hóa đơn gộp dùng contract chứng từ Trường, không tự mở rộng DTO này.

### 7.2. ZaloPay sandbox

**Đề xuất TTL giữ chỗ 15 phút**, cấu hình server `Checkout:ReservationMinutes`, giá trị phiên bản này là 15;
Minh xác nhận khớp thời hạn yêu cầu thanh toán của adapter trước tích hợp. Lưu ReservedUntilUtc một lần
từ thời điểm SQL chấp nhận đơn, trả qua expiresAtUtc; retry/restart không gia hạn.

1. Transaction ngắn tạo Order AwaitingPayment, Sale/usage, Payment Pending, operationKey
   `OrderPayment:{orderId}` và mã merchant duy nhất gắn đúng order/customer/amount. Commit trước gọi provider.
2. Adapter nhận `{ orderId, customerId, paymentId, amountVnd, expiresAtUtc, operationKey }` từ server.
   Trả `{ paymentId, status: Pending/Failed, paymentUrl?, expiresAtUtc }`; timeout không được suy thành Failed.
   Đây là giao diện logic bàn giao S1-M-2, chưa xác nhận chữ ký C# hoặc route callback của Minh.
3. API vẫn trả 201 OrderDto khi ý định đã lưu nhưng URL chưa có: paymentUrl=null, paymentStatus=Pending,
   requiresReconciliation=true. Client đọc lại GET, không tạo đơn/mã merchant mới. Nếu chưa lưu được, trả lỗi.
4. Callback/query của Minh cung cấp kết quả đã kiểm chứng app/merchant, paymentId, mã giao dịch,
   amount, purpose=Order và trạng thái. Chỉ Succeeded hợp lệ mới ghi thu; sai chữ ký/sai số tiền/sai nguồn
   không chuyển Paid, không debit/credit ví. Callback TopUp không được áp vào Order.
   Riêng Payment đang Failed: callback hợp lệ chỉ là tín hiệu kích hoạt Query Order độc lập từ server,
   hoặc worker tự query khi callback không đến. Chỉ Query Order xác minh đúng app_id, MerchantTransactionId,
   GatewayTransactionId và amount mới cho phép ngoại lệ Failed → Succeeded của ZaloPay.
   Query lỗi/chưa rõ giữ nguyên trạng thái và chờ đối soát; không tự đổi Failed → Pending/Expired.
   Lời gọi query ở ngoài SQL transaction; khi ghi kết quả phải lấy lại khóa và kiểm tra trạng thái hiện tại.
   Cùng transaction cập nhật tiền phải thêm AuditLogs: EntityType=Payment, EntityId=PaymentId,
   Action tối đa 255 ký tự gồm Failed→Succeeded, LateVerifiedZaloPay, mã giao dịch và nguồn query;
   CreatedAt UTC, EmployeeId null cho callback/worker hệ thống. Không giả định có cột Details/JSON,
   không lưu secret/raw payload. Retry đã xử lý không thêm tiền/audit lần nữa; lỗi ghi audit rollback cả lần ghi SQL.
   Adapter chỉ xác nhận callback đã xử lý sau commit hoặc replay đã khớp giao dịch cũ.
   SQL rollback không hoàn tác tiền provider: trả kết quả lỗi phù hợp để retry và để worker query mã cũ,
   không gửi lại yêu cầu thu hoặc tạo mã merchant mới.
5. Trước hết kiểm tra kết quả đã ghi: Payment đã Succeeded thì callback/query lặp chỉ trả kết quả cũ,
   không xét lại TTL hoặc đổi trạng thái đơn/hoàn lần nữa. Với lần xác nhận thành công đầu tiên:
   nhận dưới khóa, đơn còn AwaitingPayment và `now < ReservedUntilUtc` → Payment Succeeded và đơn Pending,
   paymentStatus=Paid. Nhận tại/sau hạn hoặc đơn đã Cancelled/Expired → giải phóng và đánh dấu Expired nếu cần, ghi khoản thực thu,
   giữ nguyên đơn Expired/Cancelled, không phục hồi tồn/lượt. Ranh giới lấy thời điểm server xử lý dưới khóa,
   kể cả provider thu trước hạn nhưng callback đến sau. Minh/Trường cần xác nhận lựa chọn này.
6. Worker Khang quét AwaitingPayment quá hạn kể cả sau restart, dùng cùng đường hủy/khóa nguồn;
   worker Minh đối soát khoản Pending/Expired/Failed chưa có kết luận chắc chắn và Refund Pending.
   Callback và worker cạnh tranh chỉ một đường giải phóng. TTL hết không chứng minh chưa bị thu.
7. Thu muộn tạo nghĩa vụ hoàn theo khoản thu gốc trong cùng transaction ghi nhận tiền. Đề xuất chuyển
   Admin/Manager có payment.refund duyệt gửi hoàn; không tự gọi hoàn hoặc cộng ví từ callback.
   Lưu ApprovedByEmployeeId/ApprovedAtUtc trên Refund; callback tạo nghĩa vụ với hai trường null.
   Route approve khóa đúng nguồn/Refund, kiểm tra version và hạn mức, ghi actor/thời điểm một lần.
   Refund sinh từ thao tác hủy của người có payment.refund có thể ghi duyệt ngay cùng transaction hủy.
   Worker chỉ gửi sau duyệt và sau kiểm tra điều kiện provider; duyệt không đánh dấu Succeeded.
   Pending Refund giữ hạn mức; chỉ kết quả provider đã xác minh mới tăng RefundedAmount.
   Timeout hoàn giữ Pending, query mã cũ trước gửi lại; metadata attempt thuộc adapter Minh.
8. Hủy dòng sau ZaloPay đã Paid hoàn đúng netAmount về phương thức gốc, theo S1-T-1 mục 6.4/6.4.1:
   - Đã biết giao dịch chỉ cho hoàn toàn bộ: không gửi hoàn từng phần; chuyển phần tiền chưa hoàn sang
     xử lý thủ công. Nếu có Refund Pending thì chỉ chuyển Failed/nhả giữ chỗ sau khi xác nhận mọi attempt
     đã thất bại/không thực hiện và không còn attempt đang gửi, xử lý hoặc chưa rõ.
   - Chưa biết giao dịch có hạn chế của chương trình provider: sau duyệt gửi yêu cầu hoàn từng phần
     đúng một lần theo OperationKey/attempt để lấy kết quả; không giữ chờ vô hạn chỉ vì chưa biết khả năng hoàn.
     Minh có thể cung cấp điều kiện đã xác minh để chặn trước cho chương trình cụ thể.
   - Timeout/đang xử lý/query lỗi: giữ Pending và hạn mức, query mã cũ trước mọi retry kỹ thuật.
     Kết quả thất bại dứt khoát và mọi attempt đã chốt không thực hoàn: Pending → Failed,
     giải phóng hạn mức trong cùng transaction; giữ lịch sử và số tiền còn phải hoàn để xử lý tiếp.
     Failed là trạng thái cuối của yêu cầu hoàn đó, không tự Failed → Pending/Succeeded hoặc gửi lại bằng mã mới.
   - Refund Failed không xóa nghĩa vụ từ snapshot hủy: refundUnresolvedAmount còn dương,
     requiresReconciliation=true và không báo đã trả tiền. Phương án hoàn khác cần payment.refund duyệt,
     truy nguồn và kiểm tra lại số đã hoàn/đang giữ trước khi tạo yêu cầu nghiệp vụ mới với khóa mới.
     Không tăng Amount thành hoàn toàn bộ đơn còn món đã phục vụ hoặc tự chuyển sang cash/ví.
   - Nếu query sau đó chứng minh đã thực hoàn trái với Refund Failed, giữ bằng chứng attempt và chuyển
     đối soát có duyệt trước khi điều chỉnh; không bỏ qua khoản thực hoàn hoặc tự sửa trạng thái cuối.

Mỗi đơn chỉ có một ý định thu. Không tạo lần thu mới cho cùng đơn khi lần cũ chưa đối soát;
khách muốn đặt lại sau hết hạn tạo đơn mới có khóa mới, khoản thu muộn của đơn cũ vẫn được theo dõi độc lập.
`requiresReconciliation=true` khi chưa rõ kết quả gateway hoặc còn nghĩa vụ hoàn chưa được xử lý/xác minh,
bao gồm Refund Failed nhưng refundUnresolvedAmount vẫn dương;
Refund đang chờ duyệt chưa được worker gửi. Phân biệt khoản thu/hoàn Succeeded với trạng thái tóm tắt sau
(xét theo thứ tự từ trên xuống):

| paymentStatus | Điều kiện |
| --- | --- |
| RefundPending | refundUnresolvedAmount > 0; gồm chờ duyệt, đang hoàn hoặc hoàn thất bại cần xử lý tiếp. Đây là trạng thái tổng hợp của đơn; không đổi Refund Failed thành Pending hay giữ hạn mức giả |
| Refunded / PartiallyRefunded | Không còn hoàn chờ, RefundedAmount > 0; bằng CollectedAmount thì Refunded, thấp hơn thì PartiallyRefunded |
| Paid | Có khoản thu Succeeded, không có hoàn thành công/đang chờ |
| NotRequired | OriginalPayable=0; hoặc cash hủy hết khi chưa thu, không còn nghĩa vụ thu |
| PendingCollection | Cash còn tiền phải thu và chưa thu |
| Failed / Expired | Gateway chưa có khoản thu Succeeded và yêu cầu thu ở trạng thái tương ứng; requiresReconciliation vẫn true nếu chưa chắc provider chưa thu |
| Pending | Các trường hợp gateway chưa rõ còn lại |

Thu muộn không được che bằng Expired. Dòng Payment gốc vẫn Succeeded sau hoàn; Refund là dòng riêng.

Ví dụ đối soát sau hủy một dòng trị giá 40000 của đơn đã thu 100000, PayableAmount còn 60000:

| Mốc xử lý | RefundPendingAmount | RefundUnresolvedAmount | RefundedAmount | paymentStatus |
| --- | --- | --- | --- | --- |
| Yêu cầu hoàn Pending | 40000 | 40000 | 0 | RefundPending |
| Xác minh thất bại, không còn attempt chưa rõ; Refund Failed | 0 | 40000 | 0 | RefundPending, requiresReconciliation=true |
| Phương án mới được duyệt và xác minh hoàn 40000 thành công | 0 | 0 | 40000 | PartiallyRefunded |

Mốc Failed chỉ nhả phần giữ hạn mức kỹ thuật; khách vẫn còn 40000 chưa nhận. Mốc cuối phải truy được
nguồn hủy/khoản thu/yêu cầu mới, không đổi dòng Failed cũ thành Succeeded hoặc ghi hoàn thêm từ retry.

### 7.3. Giao điểm phí phiên trước thanh toán ví

**Phương án đề nghị nhóm review:** theo contract phiên Phúc, trước khi debit ví cho đơn có OriginalPayable > 0,
caller khóa Customer → Computer → GamingSession/SessionCharge, gọi logic phiên để quyết toán tới cùng
mốc now phía server; sau đó mới xét số dư còn lại cho đơn. Điểm gọi logic
`SessionSettlement.BeforeWalletDebit(customerId, gamingSessionId?, nowUtc)` trả số dư sau quyết toán
và trạng thái phiên; dùng cùng DbContext/transaction, không tự commit. Đây chưa phải interface đã có.
Khách đặt tại quầy không truyền GamingSessionId nhưng vẫn đang có phiên thì tìm/khóa phiên thuộc khách;
không bỏ qua nghĩa vụ phí chỉ vì đơn không gắn phiên. Không có phiên đang mở thì bước này không ghi phí.
Không đọc số dư trước quyết toán để chấp nhận đơn; nếu phiên phải kết thúc hoặc số dư sau quyết toán
không đủ cho đơn, rollback toàn request (gồm khoản phí vừa tính trong request), không tạo đơn/debit/tồn/usage.
Worker phiên quyết toán lại độc lập theo contract phiên. Replay đơn đã thành công ở mục 8 trả kết quả cũ
trước bước này, không tính phí hay debit lại từ retry checkout.

Ví dụ minh họa thứ tự: ví 50000, phí phiên đến now còn phải thu 10000, đơn món 45000 → từ chối đơn,
rollback request; không chấp nhận đơn dựa vào số dư 50000 rồi để phí phiên không còn tiền thu.
Cash/ZaloPay và đơn gốc 0 đồng không gọi settlement vì checkout không debit ví; vòng đời phiên vẫn do module phiên xử lý.

S1-M-1 hiện đề xuất GamingCharge thu trước đầu chu kỳ; contract phiên đề xuất thu phí tích lũy.
Chỉ một policy được triển khai chung. Minh/Phúc/Trường cần thống nhất với Khang điểm gọi và phần phí
đã thu để không thu cả chu kỳ trả trước lẫn phí tích lũy cho cùng thời gian. Nếu chọn policy trả trước,
owner phiên cung cấp BeforeWalletDebit tương đương theo policy đó và cập nhật contract trước tích hợp;
Khang không tự viết công thức phí trong checkout. Đây là quyết định còn mở, không phải lỗi đã sửa trong code phiên/ví.

## 8. Chống lặp, transaction và lỗi

### 8.1. Idempotency và cạnh tranh

- Header `Idempotency-Key`: 8..100 ký tự ASCII `[A-Za-z0-9._:-]`, giữ nguyên cho một ý định/retry.
  Lưu bền scope `(actorType, actorId, operation/route resource, key)`, fingerprint DTO đã normalize
  (bao gồm thứ tự items, tập dòng hủy chuẩn hóa và expectedVersion), result resource và thời gian cùng transaction.
  itemIds null giữ nguyên null trong fingerprint; tập dòng thực tế đã chốt lưu riêng cùng thao tác,
  không suy lại từ trạng thái mới khi tính fingerprint cho replay.
  Có unique constraint trên scope. Không cache key chỉ trong RAM; không xóa key của nguồn còn lưu lịch sử.
- Cùng scope/key + payload giống: lần đầu create/yêu cầu hủy 201, replay 200 với
  `Idempotency-Replayed: true` và cùng resource ID, DTO đọc trạng thái hiện tại; không tạo side effect mới.
  Các thao tác khác 200 cả lần đầu và replay. Cùng key khác payload → 409 IdempotencyConflict.
- Xác thực, quyền và sở hữu luôn kiểm tra trước replay; replay đã thành công được tìm trước kiểm tra
  version/trạng thái nghiệp vụ (ví dụ khách vừa Locked, đơn vừa hết hạn) để không tạo giao dịch mới.
  Nếu scope chưa có thì áp toàn bộ kiểm tra Active/version/trạng thái. Request thất bại rollback không chiếm key.
- Chỉ kiểm tra key trước insert là chưa đủ: khóa Customer của chủ đơn để serialize tạo đơn và tìm lại key
  trong transaction; unique scope là bảo vệ cuối. Wallet ledger còn tìm khóa riêng sau khóa khách trước Record.
- Thứ tự khóa đề xuất đồng bộ S1-T-1: Customer → Computer → GamingSession → SessionCharge → Order
  → Product → Promotion → Payment gốc → Refund → RefundAttempt → Invoice → InvoiceIssueRequest → audit.
  Chỉ lấy nhóm cần dùng; trong nhóm ID tăng dần. Mọi writer/callback/worker/phát hành dùng cùng thứ tự;
  không giữ Product rồi quay lại lấy Customer/Order. Đọc ID trước được, phải đọc lại sau khóa.
- Caller mở một SQL transaction/DbContext dùng chung. Inventory/Voucher/Wallet/Invoice chỉ sửa dữ liệu,
  không tự commit hoặc transaction lồng. Lưu snapshot/nguồn/ledger/thu-hủy/usage/key trong transaction;
  sau commit mới gửi event hoặc gọi gateway. Lỗi SaveChanges/deadlock rollback toàn thao tác, trả 409;
  client GET và retry cùng key, không tự retry với khóa mới.
- OrderId/OrderDetailId hiện là SQL identity, trong khi khóa tiền/tồn cần ID nguồn. Đề xuất caller flush
  Order/Details bằng SaveChanges trong transaction đang giữ khóa để lấy ID, rồi ghi ledger/tồn/usage/Payment/key
  và SaveChanges trước một lần commit cuối. Lỗi bất kỳ bước nào rollback cả lần flush đầu, không lộ đơn dở dang.
  Không gọi gateway/phát event giữa hai lần lưu. Đối chiếu code S1-M-1 cho thấy SaveChangesAsync không commit;
  ExecuteInTransactionAsync chỉ commit sau khi callback hoàn tất, nên cấu trúc code cho phép cách lưu này
  nếu các module dùng cùng DbContext. Hướng dẫn “một SaveChanges” của S1-M-1 cần được Minh đồng bộ
  thành “một transaction, một commit cuối; có thể flush identity trước khi lưu phần còn lại”. Đây là đề xuất
  tương thích về cấu trúc code, chưa có test rollback tích hợp đơn/ví và chưa phải Minh đã chấp thuận sửa contract.
  Không giả định có OrderId thật trước khi EF nhận identity hoặc commit đơn riêng để lấy ID.
- Order có rowversion. VersionRequest kiểm tra expectedVersion dưới khóa; bất kỳ thay đổi dòng,
  yêu cầu hủy, thu/hoàn hay trạng thái gateway đều tăng version của aggregate. Version cũ trả 409
  OrderVersionConflict, không ghi ví/tồn/lượt. Replay đã lưu được trả trước kiểm tra version.

### 8.2. Lỗi HTTP

Giữ dạng ProblemDetails có title/status/detail/instance/traceId như ApiExceptionHandler hiện có.
Đề xuất bổ sung `code` cho module đơn; code này chưa được handler nền xuất ra. 401/403 từ middleware
có thể chưa có ProblemDetails; client phải xử lý được bằng HTTP status.

| HTTP / code đề xuất | Điều kiện |
| --- | --- |
| 400 ValidationError | DTO/header sai, unknown field, enum sai, số lượng/tiền vượt giới hạn, thiếu reason/version |
| 401 | Chưa đăng nhập/token sai hoặc hết hạn |
| 403 | Sai loại principal, thiếu permission, nhân viên không hoạt động |
| 404 OrderNotFound / OrderItemNotFound | Không tồn tại hoặc khách không sở hữu; dòng không thuộc đơn |
| 409 CustomerLocked / SessionNotActive | Tạo mới khi khách Locked, phiên không Active/không thuộc khách; không áp vào replay đã lưu |
| 409 ProductUnavailable / InsufficientStock | Món ngừng bán, thiếu tồn tại lúc giữ khóa |
| 409 VoucherInvalid / VoucherLimitExceeded | Mã không hợp lệ/hết hạn/không đạt ngưỡng/hết lượt |
| 409 InsufficientWalletBalance | Không đủ tiền sau tính giá/giảm; không có đơn/ledger/tồn/usage mới |
| 409 InvalidOrderTransition / OrderVersionConflict | Confirm/hủy/thu sai trạng thái hoặc version cũ |
| 409 CancellationConflict / IdempotencyConflict | Trùng yêu cầu Pending hoặc dùng lại key khác payload |
| 409 ConcurrencyConflict | Unique/deadlock/concurrency ở SQL; toàn bộ thao tác chưa commit rollback |
| 500 | Lỗi nội bộ chưa phân loại; không lộ SQL/secret; client GET và retry cùng key khi chưa rõ commit |

## 9. Giao điểm module, chứng từ và event

| Owner | Điểm tích hợp đề xuất | Bất biến cần giữ |
| --- | --- | --- |
| Khang | Order application service/DTO, snapshot, version, yêu cầu/duyệt hủy, TTL worker | Orchestrate cùng transaction; không tự ghi ví hoặc tạo bộ kho/voucher khác |
| Minh | WalletLedger.Record; FindTrackedForWalletUpdateAsync; FindWalletTransactionByKeyAsync | OrderPayment âm / OrderRefund dương; SaveChanges/commit do caller; khóa ledger <=100 ký tự |
| Minh | Payments neo OrderId; adapter tạo/query/hoàn purpose Order, worker đối soát | Unique operationKey/mã provider; RefundOfPaymentId; lưu Pending trước gọi ngoài transaction; không ghi tiền từ redirect |
| Trường | Inventory.ApplySale/ApplyReturn; Voucher.ValidateAndUse/ReleaseForCancelledOrder | Tồn khả dụng + lịch sử; một usage/đơn; snapshot/phân bổ và giải phóng idempotent |
| Trường | Invoice.IssueForOrders/IssueForSession; Invoice.ApplyFinancialChange | Hóa đơn gộp chỉ liên kết nguồn, không thu lại; OrderDetailId truy snapshot; thu/hủy/hoàn cập nhật đúng hóa đơn nếu đã phát hành |
| Phúc | Hạ tầng realtime theo S1-P-2 | Khang nối event sau commit vào nhóm customer/nhân viên được server cấp quyền |
| Phúc + Minh | SessionSettlement.BeforeWalletDebit theo mục 7.3 | Quyết toán phí theo một policy chung trước khi chi ví cho đơn; cùng khóa/transaction, replay không ghi phí mới |

Tên Inventory/Voucher/Invoice trên là giao diện logic của S1-T-1, chưa phải class/interface đã có trên base.
Application chỉ tham chiếu Domain; Infrastructure giữ EF/SQL, API giữ HTTP/claims/hub. Adapter callback
chuyển kết quả đã xác minh vào use case đơn, không gọi HTTP vòng qua API công khai hay giả principal.

Không tạo hóa đơn riêng cho mỗi checkout để lách Payment.InvoiceId bắt buộc. Khoản thu/hoàn lưu ở
Payments theo nguồn OrderId, InvoiceId null trước phát hành theo thiết kế S1-T-1; cần migration mới.
Khi nhân viên phát hành hóa đơn theo tập đơn/phiên, nối các khoản đã có, không debit/thu lần hai.
Hủy/hoàn sau phát hành cập nhật chứng từ đã gắn nguồn, không gắn sang hóa đơn tùy ý cùng khách/phiên.
Wallet ledger là nguồn tiền ví, Payment Wallet là dòng đối soát: báo cáo không cộng cả hai;
nạp ví không phải doanh thu món. Khoản Refund Pending không được tính là đã trả tiền khách.

Event logic `OrderChanged { orderId, customerId, version, occurredAtUtc }` sau commit tạo/xử lý/hủy/thu/hoàn/hết hạn.
Tên hub/event/group cụ thể đối chiếu contract realtime Phúc khi tích hợp; không khẳng định tên này đã được publish.
Payload chỉ báo thay đổi; client GET lại API, reconnect cũng GET lại; browser không tự trừ tồn/cộng ví.
Nhóm staff yêu cầu order.view; không broadcast dữ liệu đơn cho khách khác. Event lỗi không rollback tiền
đã commit; GET là nguồn trạng thái thật, không tự fallback localStorage thành công.

## 10. Mapping schema cần triển khai sau

Đây là danh sách giao việc trong các task triển khai hiện hữu, không áp migration trong S1-K-2.
Nền 35 bảng là điểm xuất phát; bảng/cột bổ sung phải được Trường điều phối và Trưởng nhóm xem xét.

| Nguồn hiện tại | Bổ sung/điều chỉnh đề xuất | Owner |
| --- | --- | --- |
| Orders | Giữ OrderId/CustomerId/GamingSessionId/OrderTime; TotalAmount map OriginalPayable; thêm Subtotal, DiscountAmount, snapshot voucher, PaymentMethod, ReservedUntilUtc, rowversion; Status theo mục 5 | Khang |
| Orders.EmployeeId NOT NULL | Nullable cho khách/hệ thống; actor type + liên kết khách qua CustomerId, staff qua EmployeeId; không đặt ID 0 hoặc CustomerId vào FK nhân viên | Khang + Đại đối chiếu actor |
| OrderDetails | Giữ UnitPrice làm snapshot; thêm ProductNameSnapshot, DiscountAmount, thứ tự dòng, Status, thông tin confirm/hủy/reason; Quantity không sửa sau tạo | Khang |
| OrderStatusHistory (table theo EF config) | ChangedBy nullable cho khách/hệ thống, thêm ActorType/reason; truy customer qua Order, không giả nhân viên; lịch sử dòng và yêu cầu hủy cũng lưu được actor/time | Khang + Đại |
| Chưa có nguồn lưu yêu cầu hủy | OrderCancellationRequests + tập OrderDetailId được chuẩn hóa; người yêu cầu/xử lý, lý do, trạng thái/thời gian; kiểm tra trùng Pending dưới khóa Order | Khang |
| Chưa có khóa thao tác đơn | OrderOperations: scope/key/fingerprint/orderId/result ID/thời gian; unique scope; cùng transaction nguồn, không chỉ dùng WalletTransactions để chống tạo trùng đơn | Khang |
| Products/InventoryTransactions | Stock là khả dụng; Sale/Return theo OrderDetailId, unique nguồn/type; Stock>=0, Before/After; EmployeeId nullable cho khách/hệ thống theo S1-T-1 | Khang Product; Trường lịch sử tồn |
| Promotions và lượt dùng | Snapshot/validation/rule + PromotionUsages unique OrderId; ReleasedAt và hạn giữ chỗ truy từ Orders, giữ chỗ chiếm hạn mức | Trường; Khang cung cấp trạng thái/hạn |
| Payments.InvoiceId NOT NULL, chưa có nguồn/khóa hoàn | Theo S1-T-1: nullable InvoiceId, OrderId/SessionChargeId XOR, Type, RefundOfPaymentId, OrderDetailId cho hoàn dòng, OperationKey unique, trạng thái/mốc thời gian/mã gateway; RefundAttempts thuộc adapter | Minh + Trường |
| Refund chưa có nguồn duyệt trên base | Đề xuất thêm ApprovedByEmployeeId/ApprovedAtUtc cho khoản hoàn gateway; null khi hệ thống tạo nghĩa vụ thu muộn; worker không gửi trước duyệt, không giả actor nhân viên. Nghĩa vụ chưa hoàn suy từ snapshot hủy + khoản thu/hoàn, không xóa dòng Failed | Minh; Trường đối chiếu với S1-T-1 |
| WalletTransactions chưa có IdempotencyKey trên base | Nhận migration/code S1-M-1 từ commit được nhóm tích hợp, không viết lại migration của Minh | Minh |
| InvoiceOrders/InvoiceDetails | Unique nguồn, snapshot và cập nhật tài chính theo contract Trường; không tạo Invoice.OrderId thay quan hệ gộp | Trường |

DB constraint bảo vệ giá trị nội dòng/FK/unique. Đúng chủ ví, dòng thuộc đơn, tổng giảm, hạn mức hoàn,
usage cuối và sự khớp provider là kiểm tra nghiệp vụ dưới khóa, không tuyên bố FK/unique tự chứng minh đủ.
Không sửa migration đã áp. Không nhập migration MySQL/schema 19 bảng hoặc chuyển/xóa dữ liệu cũ.

## 11. Các điểm cần xác nhận để chốt S1-K-2

### 11.1. Kết quả đối chiếu kỹ thuật của bản 0.2

Nguồn ghim tại mục 1; kết quả dưới đây là đối chiếu tài liệu/code, không phải phản hồi chấp thuận của owner
hoặc bằng chứng đã chạy tích hợp. Các finding đã được sửa ngay trong hợp đồng để Trưởng nhóm review.

| Giao điểm | Nguồn đối chiếu | Kết quả trong bản 0.2 |
| --- | --- | --- |
| Loại/dấu ví, số tiền 0 và khách Locked | S1-M-1 tại 8b2e6a2, WalletLedger.Record và mục 1/2/4 hợp đồng ví | Khớp: OrderPayment âm, OrderRefund dương; không ghi giao dịch 0; hoàn được cho khách Locked; tìm khóa trước ghi |
| Snapshot, voucher và tồn | S1-T-1 mục 3.1/3.2/4 | Khớp quy tắc sau giảm/phân bổ/hoàn lượt; giữ UnitPrice sẵn có, Sale/Return theo dòng, không giảm/trả hai lần |
| Tiền và hóa đơn | S1-T-1 mục 6.1–6.4/7 | Khớp thiết kế Payments neo nguồn trước hóa đơn, Refund tách khoản thu; không debit lại khi phát hành hoặc cộng đôi ledger/Payment |
| Hoàn từng phần chưa rõ/bị từ chối | S1-T-1 bản 1.6/1.7, mục 6.4/6.4.1 | Đã sửa mục 7.2: chưa biết thì gửi một lần sau duyệt; chỉ giữ Pending khi chưa chốt; thất bại dứt khoát chuyển Failed đúng điều kiện, vẫn theo dõi tiền còn phải hoàn |
| Thu muộn của Payment Failed | S1-T-1 bản 1.6/1.7, bảng trạng thái và quy tắc audit mục 6.2.2 | Đã bổ sung query độc lập, đối chiếu giao dịch, audit cùng transaction, callback chỉ ack sau commit và worker query khi rollback |
| Flush identity và commit | CustomerRepository.ExecuteInTransactionAsync/SaveChangesAsync tại 8b2e6a2 | Code cho phép nhiều lần lưu trong một transaction; tài liệu ví còn ghi một lần lưu. Bản này nêu phương án và nội dung cần đồng bộ, chưa khẳng định test rollback đã pass |
| Phí phiên trước OrderPayment | Contract phiên tại 761333c mục 3–5 và ví tại 8b2e6a2 mục 5 | Đã bổ sung điểm gọi/phạm vi rollback và ví dụ ở mục 7.3; khác biệt thu phí tích lũy/trả trước vẫn cần nhóm chọn chung |
| Schema đích so với base e14a362 | Order/OrderDetail/Payment và EF configuration | Đã phân biệt phần có sẵn/phần cần migration ở mục 10; chưa có API checkout, không coi đề xuất schema là đã triển khai |

### 11.2. Quyết định nghiệp vụ và phản hồi liên module còn chờ

Các dòng dưới **chờ nhóm xác nhận**, tách khỏi phần đối chiếu kỹ thuật đã thực hiện. Khi chốt ghi
người/ngày/commit hoặc phản hồi thực tế vào đây, rồi cập nhật mục 11 kế hoạch bàn giao nếu quyết định chung thay đổi.

| Quyết định đề xuất | Người đối chiếu | Kết quả cần xác nhận |
| --- | --- | --- |
| Ví trừ khi server chấp nhận tạo đơn, trước confirm từng món; 0 đồng không ghi ledger | Khang + Minh | Thống nhất nghĩa “xác nhận đơn” của S1-M-1 và khóa debit/refund theo dòng |
| Hủy cả dòng Pending; hủy toàn đơn có Confirmed bị từ chối; cash thu sau confirm | Khang + Minh + Trường | Quyền hủy/hoàn và xử lý ngoại lệ cash đã thu; không tự tăng quyền Cashier/Waiter |
| Giữ tồn bằng Sale khi đặt, Return khi hủy/expired; usage chiếm lượt ngay, chỉ hoàn khi hủy toàn bộ | Khang + Trường | Không trừ/trả hai lần; tương thích InventoryTransaction/PromotionUsage và phân bổ giảm |
| TTL ZaloPay 15 phút, không gia hạn; callback xử lý tại/sau hạn là thu muộn | Khang + Minh + Trường | Khớp hạn provider, chủ worker, không khôi phục đơn; trách nhiệm xử lý tiền đã thu |
| Hoàn gateway theo nguồn gốc; thu muộn cần Admin/Manager duyệt; partial theo quy tắc đã đối chiếu mục 7.2 | Minh + Khang + Trường | Interface adapter/approval, điều kiện kênh/chương trình và đường xử lý nghĩa vụ còn lại khi Refund Failed; không tự hoàn ví |
| Payments neo nguồn trước hóa đơn, thứ tự khóa và flush identity mục 8; actor khách/hệ thống nullable | Minh + Trường + Khang; Đại đối chiếu actor | Migration/DI cùng DbContext, nhiều SaveChanges trong một transaction nếu cần ID; ledger và Payment không đếm tiền hai lần, không giả nhân viên |
| Phí phiên được xử lý trước OrderPayment qua điểm gọi chung mục 7.3 | Phúc + Minh + Trường + Khang | Chọn policy phí tích lũy hay trả trước; xác nhận phần phí đã thu, thứ tự gọi/khóa và rollback; không thu hai policy cho cùng thời gian |
| DTO/route/version/idempotency/error/event và precision tiền | Khang + Minh + Trường; Phúc đối chiếu event | Các caller dùng cùng cấu trúc; phần bổ sung schema được điều phối, chưa coi có sẵn |
| Base tích hợp của contract chứng từ 1.6/1.7 | Trường + Minh + Khang; Trưởng nhóm quyết định base | Chỉ định commit chung và quyết định dùng InvoiceIssueRequests/các mốc thời gian của 1.7 trước khi triển khai schema tương ứng |
| Duyệt cuối hợp đồng chung | Trưởng nhóm | Chỉ ghi đã duyệt/Done khi có quyết định thực tế |

S1-K-2 phụ thuộc contract của S1-M-1/S1-T-1; S1-M-2 nhận giao diện Order từ đây để chốt gateway.
Credentials, callback HTTPS và kiểm chứng provider thuộc các task Minh, không chặn soạn bản thiết kế này;
thiếu interface/xác nhận/migration phải nêu khi nghiệm thu tích hợp S2, không ghi sandbox đã chạy.

## 12. Tiêu chí kiểm chứng khi triển khai

Các ca dưới là yêu cầu bàn giao cho S2/S3, **chưa được chạy trong task tài liệu**.

| Ca | Kết quả cần kiểm tra ở API/SQL |
| --- | --- |
| Wallet hợp lệ / thiếu tiền / lỗi SaveChanges | Thành công có một đơn/debit/Sale/usage/Payment đúng nguồn; lỗi rollback toàn bộ, ví không âm |
| Ví đủ giá món nhưng thiếu sau phí phiên; retry cùng key; lỗi sau flush identity | Theo phương án 7.3: đơn bị từ chối và toàn request rollback, không tạo nguồn dở dang hoặc thu thêm phí từ replay; worker phiên quyết toán độc lập |
| Gửi cùng key tuần tự/đồng thời; mất response/restart | Cùng resource ID, không trừ ví/tồn/dùng lượt lần hai; payload khác 409; key khác vẫn không thu/hoàn trùng nguồn |
| Hai khách tranh tồn/lượt cuối | Chỉ một bên thành công; bên thua 409, không có ledger hoặc nguồn dở dang |
| Giá/tên/voucher sửa sau đặt; giả giá/tổng/status trong body | Snapshot và hoàn theo giá cũ; body ngoài DTO bị từ chối; không dùng giá browser |
| Voucher %/fixed, trần, ngưỡng, đồng dư, giảm 100% | Tổng giảm dòng bằng giảm đơn; không vượt subtotal; đơn 0 không tạo khoản tiền 0 |
| Khách khác/phiên khác; Waiter thu/hủy; Cashier hoàn; token cũ sau khóa | 404/403/409 theo contract; không ghi side effect; khách Locked vẫn nhận hoàn được duyệt |
| Yêu cầu hủy lặp, confirm đua hủy, version cũ | Khách không tự hoàn; chỉ một chuyển trạng thái, yêu cầu liên quan được đóng đúng; lỗi không ghi tồn/ví |
| Hủy dòng đã giảm / hủy toàn bộ / hủy lặp | Hoàn netAmount một lần, Return một lần; partial không hoàn lượt, full hoàn đúng một lượt |
| Cash trước/sau confirm, thu lặp, đơn 0 | Chờ thu không debit; thu đúng khoản còn lại một lần, không giả Paid khi chưa thực nhận |
| Gateway Pending, callback sai/lặp/TopUp nhầm Order | Không tự Paid từ redirect hoặc dữ liệu sai; callback hợp lệ ghi một lần; không mở transaction lúc chờ |
| Hết hạn đua callback, callback sau hủy, provider đã thu nhưng SQL rollback | Không giữ/trả tồn/lượt hai lần, không hồi sinh đơn; query giao dịch cũ để ghi nhận và đối soát khoản thực thu |
| Payment Failed nhận callback thành công muộn / query khớp / lỗi audit | Callback đơn lẻ không sửa Failed; query khớp mới ghi Succeeded + audit một lần, không hồi sinh đơn; lỗi audit rollback SQL, retry query mã cũ |
| Hoàn gateway chưa rõ khả năng partial / timeout / bị từ chối dứt khoát | Chưa rõ điều kiện thì gửi một lần sau duyệt; timeout giữ Pending/hạn mức; xác minh thất bại và không còn attempt chưa rõ mới Failed/nhả giữ chỗ; vẫn hiện đúng refundUnresolvedAmount, không báo đã hoàn |
| Refund Failed cần xử lý tiếp / worker restart / query trái kết luận cũ | Worker không tự gửi lại Failed; phương án mới cần duyệt và kiểm tra hạn mức/nguồn; kết quả mâu thuẫn chuyển đối soát có duyệt, không bỏ qua thực hoàn |
| Hóa đơn gộp trước/sau thu/hủy/hoàn | Chỉ nối nguồn đúng đơn, không debit/thu lại, số thu/hoàn khớp; không cộng đôi ledger + Payment |
| Event và reconnect | Phát sau commit đúng người nhận; GET thấy trạng thái đã lưu, không fallback localStorage |

Tự kiểm tra thiết kế dùng [REVIEW_GUIDE](../../codex/REVIEW_GUIDE.md). Khi bàn giao S1-K-2 cần
diff/tài liệu, kiểm tra link/JSON/nhất quán và phản hồi mục 11; không yêu cầu test SQL thay cho xác nhận thiết kế,
cũng không dùng kiểm tra tài liệu để tuyên bố các ca API/SQL/gateway trên đã pass.
