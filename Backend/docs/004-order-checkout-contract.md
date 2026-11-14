# 004 – Hợp đồng đơn món và checkout (S1-K-2)

| Thông tin | Nội dung |
| --- | --- |
| Task | Define-Order-Checkout-Contracts / S1-K-2 / SCRUM-11 |
| Owner | Khang; Minh đối chiếu ví/gateway, Trường đối chiếu tồn/voucher/chứng từ |
| Reviewer cuối | Trưởng nhóm |
| Phiên bản | 0.5, ngày 14/11/2026, Trưởng nhóm chọn thanh toán đủ khi đặt rồi nhân viên mới nhận món để làm |
| Backend đích | CyberManagement .NET 10 / SQL Server, nền 35 bảng |
| Phụ thuộc | S1-M-1 và S1-T-1; giao diện gateway bàn giao tiếp cho S1-M-2 |

**Trạng thái:** đã chốt các lựa chọn thiết kế tại mục 11.2 theo chỉ dẫn trực tiếp ngày 08/11/2026:
người dùng giao AI tự chọn phương án hợp lý cho các điểm đang chờ. Mỗi quyết định ghi lựa chọn,
lý do và owner phối hợp triển khai. Việc chốt thiết kế này không tiếp tục chờ phản hồi riêng của từng owner.
Nguồn quyết định là ủy quyền trong cuộc trò chuyện; chưa có bằng chứng Minh/Trường/Phúc đã phản hồi đồng ý,
và chưa có quyết định duyệt cuối của Trưởng nhóm. Đây là hợp đồng để triển khai, chưa phải API đang chạy.
Phụ thuộc code/migration và kiểm chứng thực tế được tách ở mục 11.3; các quyết định chung trong PROJECT_GUIDE được giữ.
S1-K-2 bàn giao thiết kế. Code checkout thuộc S2-K-1/S2-K-2; UI thuộc S2-K-3;
hoàn thiện cạnh tranh/hủy/hoàn thuộc S3-K-1. Không tick Done chỉ vì đã có tài liệu.

## 1. Căn cứ và giới hạn hiện trạng

- [PROJECT_GUIDE](../../codex/PROJECT_GUIDE.md), [task S1-K-2](../../codex/SPRINT_TASKS.md#s1-k-2--define-order-checkout-contracts)
  và [lịch sử quyết định trong kế hoạch bàn giao](../../planning/team-delivery-plan.md#11-quyết-định-nghiệp-vụ-cần-nhóm-chốt-trong-s1).
- Nhánh làm việc: `SCRUM-11-Define-Order-Checkout-Contracts`; base từ `dev` tại
  `e14a362e5f635db62deebf7752493672e755a24e`. Bản 0.3 tại
  `66f1a29498c8bae860e49372802bf0f0b03ce548` đã sửa hai finding của review 06ba492.
  Bản 0.4 bổ sung quyết định ngày 08/11/2026 theo ủy quyền người dùng; thay đổi sau 66f1a29 chưa commit.
  [Order](../src/CyberManagement.Domain/Entities/Order.cs),
  [OrderDetail](../src/CyberManagement.Domain/Entities/OrderDetail.cs),
  [Payment](../src/CyberManagement.Domain/Entities/Payment.cs) và EF configuration hiện có
  chưa đủ snapshot giảm, trạng thái từng dòng, giữ chỗ và chống lặp. Chưa có controller/use case đơn món trên base này.
- [Identity](001-monorepo.md) và [RolePermissionCatalog](../src/CyberManagement.Application/Security/RolePermissionCatalog.cs)
  là nguồn định danh/quyền. CustomerId và EmployeeId là hai miền int khác nhau.
- Đầu vào ví đã đọc: commit `8b2e6a21bb3d3d1f081bd4b1497b39154af971b5`,
  Git path `backend/docs/002-wallet-ledger.md` và code `WalletLedger`/`ICustomerRepository` tương ứng.
  Dùng `OrderPayment` âm, `OrderRefund` dương; Record không tự lưu/commit.
- Bổ sung SCRUM-5 ngày 08/11/2026: [contract ví trên CyberManagement int](003-customer-wallet-contract.md)
  thay cho đường code CyberCafe cũ khi tích hợp S2. Giữ OrderPayment/OrderRefund, D09 và D11;
  hiện mới triển khai query/tạo hồ sơ, chưa có writer Record hoặc settlement đang chạy.
- Đầu vào kho/chứng từ/voucher đã đọc: commit `761333c4bc32fe3ae5f6bc524b6a2ba8526db864`,
  Git path `docs/contracts/inventory-invoice-voucher.md`, phiên bản 1.7 chờ duyệt.
  Giữ thiết kế một voucher/đơn, phân bổ giảm theo dòng, Payments neo nguồn trước hóa đơn,
  hoàn theo phương thức đã trả và thứ tự khóa chung. Đây là đầu vào đối chiếu, chưa phải xác nhận liên module.
- Đã kiểm tra thêm bản 1.6 của S1-T-1 tại `280ed713627d037d770f6e02bc1d248ef4f98a70`
  (`origin/SCRUM-12-Define-Inventory-Invoice-Voucher-Contracts` trong các ref local).
  Quy tắc hoàn từng phần và ngoại lệ thu muộn Failed của bản 1.6/1.7 khớp nhau; bổ sung
  InvoiceIssueRequests và các mốc thời gian của bản 1.7 chưa được nhóm duyệt trong ảnh chụp nguồn.
  Bản 0.4 chọn thiết kế 1.7 làm đầu vào chứng từ cho checkout theo D11 mục 11.2; không coi code/migration đã được tích hợp.
- Đầu vào phiên đã đọc: `761333c4bc32fe3ae5f6bc524b6a2ba8526db864`, Git path
  `Backend/docs/002-computer-session-contract.md`, mục 3–5: quyết toán phí phiên trước debit ví khác.
  Hợp đồng ví tại ảnh chụp cùng thời điểm đề xuất thu trước theo chu kỳ. Bản 0.4 chọn phí tích lũy
  theo giây, quyết toán trước debit món theo mục 7.3/D09; owner đồng bộ contract/code khi tích hợp.
- Các đầu vào trên chưa có trong cây base đang mở; tra bằng `git show <commit>:<path>` khi có ref.
  Nếu clone chưa có commit, cần owner cung cấp PR/commit trước tích hợp; không phụ thuộc đường dẫn `.tools` của máy người viết.
- UI hiện dùng [demoModel](../../frontend/src/client/shared/demoModel.js) và
  [adminModel](../../frontend/src/admin/shared/adminModel.js): ví trừ lúc đặt, cash chờ thu,
  khách gửi yêu cầu hủy, nhân viên xử lý từng dòng Pending. Hợp đồng dưới đây giữ luồng đó,
  bổ sung quyền phía server, voucher và gateway; không coi localStorage là bằng chứng API/SQL.

## 2. Quy ước và bất biến

**Quyết định thanh toán ngày 14/11/2026.** Luồng phục vụ là đặt món → thanh toán đủ → nhân viên nhận món → chuẩn bị/phục vụ. Confirm dòng Pending sang Confirmed là nhân viên nhận món để bắt đầu làm. Server kiểm tra số còn phải thu bằng 0 và khoản thu đã được xác minh dưới khóa trước mỗi confirm. Cash được thu khi các dòng còn Pending, nhân viên có payment.create xác nhận thực nhận. Wallet ghi nhận debit khi tạo đơn, ZaloPay ghi nhận sau callback/query hợp lệ. Đơn 0 đồng sau giảm dùng NotRequired. Quy tắc áp dụng cho cả đơn khách và nhân viên đặt thay.

- JSON camelCase; enum là chuỗi phân biệt hoa/thường. ID là int dương. Thời gian API dùng ISO 8601 UTC (`Z`),
  tính toán bằng một mốc `now` phía server cho mỗi transaction; UI hiển thị giờ Việt Nam (+07:00).
- Tiền là số nguyên VND, C# decimal. Tạm giới hạn mỗi số tiền và tổng đơn ở `999_999_999` VND,
  phù hợp cột `decimal(12,2)` hiện tại và số nguyên JSON an toàn. Phần lẻ/tràn bị từ chối trước ghi.
  Chốt kiểu SQL đích `decimal(18,0)` cho tiền VND, DiscountValue `decimal(18,2)` để giữ tỷ lệ lẻ;
  migration do owner bảng điều phối theo S1-T-1. Giữ giới hạn API 999_999_999, không tự nâng theo precision mới.
  Dữ liệu có phần lẻ VND phải được đối soát trước migration, không âm thầm làm tròn; task này chưa sửa schema.
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

| Method / route | Đầu vào / kết quả | Quyền server |
| --- | --- | --- |
| `POST /api/client/orders` | CreateOrderRequest → 201 OrderDto | Customer; CustomerId từ `customer_id`; phiên Active thuộc khách |
| `GET /api/client/orders` | `page=1&pageSize=20`, tối đa 100 → OrderPageDto | Customer; luôn lọc theo claim |
| `GET /api/client/orders/{orderId}` | 200 OrderDto | Customer sở hữu đơn |
| `POST /api/client/orders/{orderId}/cancellation-requests` | CancellationRequest → 201 CancellationDto | Customer sở hữu đơn; chỉ tạo yêu cầu; Cash đã có khoản thu Succeeded trả 409 CashCancellationLocked |
| `POST /api/admin/orders` | StaffCreateOrderRequest → 201 OrderDto | `order.create`; Wallet/ZaloPay thêm `payment.create`; Waiter chỉ tạo Cash |
| `GET /api/admin/orders` | page/pageSize như client, thêm status/customerId tùy chọn → OrderPageDto | `order.view` |
| `GET /api/admin/orders/{orderId}` | 200 OrderDto | `order.view` |
| `POST /api/admin/orders/{orderId}/items/{orderDetailId}/confirm` | VersionRequest → 200 OrderDto | `order.status.update` và server xác minh đã thanh toán đủ hoặc NotRequired |
| `POST /api/admin/orders/{orderId}/cancellations` | CancelOrderRequest → 200 OrderDto | `order.cancel`; hoàn ví thêm `wallet.refund`, gateway thêm `payment.refund`; Cash đã có khoản thu Succeeded trả 409 CashCancellationLocked |
| `POST /api/admin/orders/{orderId}/cancellation-requests/{requestId}/reject` | RejectCancellationRequest → 200 OrderDto | `order.cancel` |
| `POST /api/admin/orders/{orderId}/cash-collections` | CashCollectionRequest → 200 OrderDto | `payment.create`; xác nhận đã nhận đủ tiền mặt khi món còn Pending, server tính số tiền |
| `GET /api/admin/orders/{orderId}/refunds` | page/pageSize như trên → 200 RefundPageDto, có cả Pending/Succeeded/Failed | `order.view` và `payment.refund`; không cấp cho Customer/Cashier/Waiter |
| `GET /api/admin/orders/{orderId}/refunds/{refundId}` | 200 RefundDto | `order.view` và `payment.refund`; Refund phải thuộc đơn trên route |
| `POST /api/admin/orders/{orderId}/refunds/{refundId}/approve` | VersionRequest → 200 RefundDto, chưa đồng nghĩa đã hoàn | `order.view` và `payment.refund`; chỉ Refund ZaloPay Pending chưa duyệt; Failed/Succeeded/đã duyệt trả 409 nếu không phải replay đã lưu |
| `POST /api/admin/orders/{orderId}/refunds/{refundId}/replacements` | RefundReplacementRequest → 201 RefundDto mới, chưa duyệt | `order.view` và `payment.refund`; chỉ thay thế Refund ZaloPay Failed đủ điều kiện mục 7.4, không hủy lại món |

Mọi POST trong bảng bắt buộc `Idempotency-Key`; quy tắc retry ở mục 8. API kiểm tra dòng/request/Refund
thuộc đúng orderId trên route. Tạo đơn staff nhận CustomerId vì là đặt thay; EmployeeId luôn lấy JWT.
Staff có thể tạo tại quầy với GamingSessionId null; nếu có phiên phải Active và thuộc khách đã chọn.
Client phải truyền phiên Active của chính mình; không lấy CustomerId/EmployeeId từ body.
`order.status.update` không cho đổi trạng thái tự do hay hoàn tiền. Cashier/Waiter thiếu `order.cancel`,
`wallet.refund`, `payment.refund` theo catalog hiện tại; không tự mở rộng quyền qua endpoint hủy.
Callback/query gateway là adapter server của Minh, có xác minh provider; không dùng JWT khách
hoặc quyền nhân viên giả để ghi thanh toán. Redirect browser chỉ dẫn về trang đọc lại OrderDto.
page phải >=1, pageSize trong 1..100; status thuộc tập ở mục 5, customerId là int dương.
Danh sách Refund không lọc mất các dòng Failed hoặc chưa duyệt; sắp createdAtUtc giảm dần, rồi id giảm dần.
Route Refund không tồn tại hoặc thuộc orderId khác trả 404, không ghi tác động. GET không thay đổi version,
không tạo/duyệt/claim khoản hoàn và không trả secret, chữ ký, raw payload hoặc thông tin ví khách khác.

## 4. DTO của hợp đồng

DTO ở đây là đặc tả để tạo C# record/validator trong Application khi triển khai S2;
entity EF không được dùng trực tiếp làm request/response.

| DTO | Trường / kiểu / validation |
| --- | --- |
| CreateOrderRequest | `gamingSessionId: int` bắt buộc; `items: CreateOrderItem[]` 1..100 dòng; `paymentMethod: Wallet/Cash/ZaloPay`; `voucherCode: string?` |
| StaffCreateOrderRequest | Như CreateOrderRequest nhưng `gamingSessionId: int?`, thêm `customerId: int` bắt buộc |
| CreateOrderItem | `productId: int`, `quantity: int` 1..99; không lặp productId; giữ thứ tự dòng để phân bổ đồng dư |
| VersionRequest | `expectedVersion: string` bắt buộc, token rowversion dạng base64, server cấp; không tự tăng tại client |
| CashCollectionRequest | VersionRequest + `cashReceived: bool` bắt buộc true để xác nhận thực nhận đủ tiền mặt; server tính số còn phải thu từ snapshot đơn |
| CancellationRequest | `itemIds: int[]?`, `reason: string` trim 1..250; null nghĩa toàn đơn; mảng phải khác rỗng, không trùng, cùng đơn; hủy cả số lượng của dòng |
| CancelOrderRequest | CancellationRequest + expectedVersion; thêm `cancellationRequestId: int?`; nếu có phải Pending và khớp tập dòng yêu cầu |
| RejectCancellationRequest | VersionRequest + `reason: string` trim 1..250; requestId trên route phải Pending |
| CancellationDto | `id`, `orderId`, `itemIds: int[]` đã chuẩn hóa; `status: Pending/Approved/Rejected`; `reason`, `resolutionReason: string?`, `requestedAtUtc`, `resolvedAtUtc: string?` |
| OrderItemDto | `id` map OrderDetailId; `productId`, `productNameSnapshot`, `quantity`, `unitPrice`, `discountAmount`, `netAmount`, `status: Pending/Confirmed/Cancelled`, `cancelReason: string?` |
| PaymentSummaryDto | `method`, `paymentStatus`, `collectedAmount`, `refundedAmount`, `refundPendingAmount`, `refundUnresolvedAmount`, `outstandingAmount`; `paymentId: int?` chỉ khoản thu Type=Payment; `paymentUrl: string?` chỉ URL sandbox do adapter trả; `expiresAtUtc: string?`; `requiresReconciliation: bool` |
| OrderDto | `id` map OrderId; `customerId`, `gamingSessionId: int?`, `createdByEmployeeId: int?`, `createdAtUtc`, `status`, `version`, `subtotal`, `discountAmount`, `originalPayable`, `cancelledAmount`, `payableAmount`, `voucher: VoucherSnapshotDto?`, `items: OrderItemDto[]`, `payment: PaymentSummaryDto`, `cancellationRequests: CancellationDto[]` |
| VoucherSnapshotDto | `promotionId`, `code`, `discountType: Percent/Fixed`, `discountValue`, `discountAmount`; tất cả do server chụp lúc đặt |
| OrderPageDto | `items: OrderDto[]`, `page`, `pageSize`, `totalCount`; sắp `createdAtUtc` giảm dần, rồi id giảm dần |
| RefundSourceItemDto | `orderDetailId: int`, `amount: decimal` nguyên đồng > 0; lấy từ snapshot phần hủy được khoản hoàn bao phủ; tổng amount các phần bằng amount của Refund |
| RefundDto | `id` map PaymentId của dòng Type=Refund; `orderId`, `refundOfPaymentId` trỏ khoản thu gốc; `orderDetailId: int?`, `sourceItems: RefundSourceItemDto[]`; `amount`, `method: Wallet/Cash/ZaloPay`, `status: Pending/Succeeded/Failed`, `reason`, `createdAtUtc`; `approvedByEmployeeId: int?`, `approvedAtUtc: string?`, `failureReason: string?` đã lọc dữ liệu nội bộ; `replacementOfRefundId: int?`, `replacedByRefundId: int?`; `canApprove: bool`, `canReplace: bool`, `orderVersion: string` |
| RefundPageDto | `items: RefundDto[]`, `page`, `pageSize`, `totalCount`; version trên các dòng cùng snapshot đọc của Order |
| RefundReplacementRequest | VersionRequest + `reason: string` trim 1..250 giải thích phương án xử lý mới; không nhận amount, method, nguồn hủy, trạng thái, actor hoặc mã provider từ browser |

RefundDto dành cho API quản trị Refund, không ghép dữ liệu duyệt/attempt vào OrderDto của khách.
`canApprove/canReplace` phản ánh quyền và điều kiện server tại lúc đọc; POST vẫn kiểm tra lại dưới khóa.
`orderVersion` là version của Order aggregate, dùng làm expectedVersion cho approve/replacements;
đọc GET mới khi có 409, không tự tăng token. ApprovedAt/ApprovedBy cùng null khi chưa duyệt,
cùng có giá trị khi đã duyệt; việc có duyệt không chứng minh đã thực hoàn.
`replacedByRefundId` được suy từ quan hệ ngược ReplacementOfRefundId, không lưu hai cột trùng chức năng.
Hủy toàn đơn có orderDetailId=null vẫn trả đầy đủ sourceItems từ snapshot thao tác hoàn đã lưu;
không suy nguồn lại từ danh sách món hiện tại hoặc chỉ từ tổng tiền.

Ví dụ body của GET `/api/admin/orders/34/refunds/8101` sau khi đơn hết hạn được xác minh thu muộn
40000, tạo một nghĩa vụ hoàn chưa duyệt; các ID/version/thời gian là dữ liệu minh họa:

```json
{
  "id": 8101,
  "orderId": 34,
  "refundOfPaymentId": 7001,
  "orderDetailId": null,
  "sourceItems": [{ "orderDetailId": 901, "amount": 40000 }],
  "amount": 40000,
  "method": "ZaloPay",
  "status": "Pending",
  "reason": "LateVerifiedZaloPay: expired order",
  "createdAtUtc": "2026-11-07T03:20:00Z",
  "approvedByEmployeeId": null,
  "approvedAtUtc": null,
  "failureReason": null,
  "replacementOfRefundId": null,
  "replacedByRefundId": null,
  "canApprove": true,
  "canReplace": false,
  "orderVersion": "AAAAAAAAAAE="
}
```

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
| Tạo Wallet → Pending | SQL chấp nhận đơn, debit ví và Payment Succeeded cùng transaction; món chờ nhân viên nhận sau thanh toán |
| Tạo Cash → Pending | Giữ/trừ tồn, dùng lượt voucher; PendingCollection; chưa có khoản thu |
| Tạo đơn 0 đồng → Pending | Bất kỳ phương thức; NotRequired, giữ tồn/lượt nhưng không tạo khoản thu hay yêu cầu gateway |
| Tạo ZaloPay → AwaitingPayment | Tổng > 0; giữ tồn/lượt, lưu Payment Pending và mã merchant trước lời gọi ngoài SQL |
| AwaitingPayment → Pending | Server xác minh thu đúng giao dịch/số tiền, giữ chỗ còn hạn; không trừ tồn/lượt lần nữa |
| AwaitingPayment → Expired | Hết hạn hoặc tạo gateway bị từ chối dứt khoát; hủy các dòng, giải phóng tồn/lượt một lần; chưa chứng minh provider chưa thu |
| Pending/Mixed → Confirmed/Mixed | Nhân viên có order.status.update nhận dòng Pending để làm sau khi server xác minh đã thanh toán đủ hoặc NotRequired cho cả Wallet/Cash/ZaloPay. AwaitingPayment/Expired bị chặn |
| Pending/Mixed → Cancelled/Mixed | Admin/Manager duyệt hủy dòng Pending theo mục 7. Cash đã có khoản thu Succeeded khóa hủy dù dòng còn Pending |
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

### 5.1. Thanh toán đủ trước khi nhân viên nhận món — 14/11/2026

Nhân viên có order.status.update chủ động confirm dòng Pending sau khi thanh toán đủ. Confirmed biểu thị món đã được nhận để chuẩn bị/phục vụ. Mỗi lần confirm, server khóa nguồn theo mục 8, kiểm tra version, trạng thái đơn/dòng và đối chiếu khoản thu thành công với giá trị các dòng còn hiệu lực. Khoản thu phải đủ cho toàn bộ phần còn phục vụ, số còn phải thu bằng 0. Đơn có PayableAmount=0 chỉ dùng NotRequired khi chưa có khoản thu/hoàn Succeeded và không còn nghĩa vụ tiền hoặc gateway chờ đối soát theo mục 7.2; các trạng thái tiền đã ghi nhận được giữ để truy vết.

| Phương thức | Căn cứ đã thanh toán | Thao tác của nhân viên |
| --- | --- | --- |
| Wallet | Debit OrderPayment, ledger và Payment Succeeded đã commit khi tạo đơn | Đọc trạng thái đã thanh toán rồi confirm món |
| Cash | Nhân viên có payment.create nhận đủ tiền, POST cash-collections với cashReceived=true, Payment Succeeded đã commit | Confirm món sau kết quả thu thành công |
| ZaloPay | Callback/query hợp lệ, Payment Succeeded và đơn đã trở về Pending trong hạn giữ chỗ | Đọc trạng thái đã thanh toán rồi confirm món |
| 0 đồng sau giảm hoặc hủy dòng | PayableAmount=0, chưa có khoản thu/hoàn Succeeded và không còn nghĩa vụ tiền hoặc gateway chờ đối soát; NotRequired theo bảng mục 7.2 | Confirm dòng còn Pending theo quyền order.status.update; không phục hồi dòng đã Cancelled |

Đơn Cash PendingCollection và ZaloPay AwaitingPayment hiển thị “Chờ thanh toán”. Nút nhận/làm món chỉ hoạt động khi server xác minh đủ tiền. Gọi trực tiếp endpoint confirm khi chưa đủ điều kiện trả 409 PaymentRequired, giữ trạng thái dòng, tồn và sổ tiền. Waiter có thể nhận món đã thanh toán theo order.status.update, thao tác xác nhận thu tiền mặt cần payment.create.

Server quyết định trạng thái thanh toán từ khoản thu đã lưu và snapshot giá/giảm. Request confirm nhận expectedVersion theo VersionRequest. Giao diện cập nhật từ OrderDto sau commit, phát event tới nhóm được phép. Trường hợp callback/query chưa đến hoặc bị lỗi tiếp tục chờ thanh toán cho tới khi server xác minh được kết quả hoặc đơn hết hạn theo mục 7.2.

Ví dụ body CashCollectionRequest dưới đây minh họa nhân viên xác nhận đã nhận đủ tiền. Sau thành công, lấy version mới từ OrderDto cho request confirm món.

```json
{"expectedVersion":"AAAAAAAAAAE=","cashReceived":true}
```

Tiêu chí triển khai S2 gồm Cash chưa thu bị chặn confirm, Cash thu đủ trước confirm, Wallet thiếu tiền rollback tạo đơn, ZaloPay pending/sai chữ ký bị chặn, callback hợp lệ vẫn chờ nhân viên nhận món, đơn 0 đồng nhận được, thu/confirm lặp và cạnh tranh thu–hủy xử lý đúng khóa/version.

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

| Tình huống | Quy tắc đã chốt |
| --- | --- |
| Wallet đặt đơn | Khóa khách và nguồn theo mục 8, xử lý phí phiên theo phương án mục 7.3 trước khi xét số dư đơn; tìm khóa `OrderPayment:{orderId}`, gọi WalletLedger.Record với `OrderPayment`, amount âm; phí phiên/đơn/tồn/usage/ledger/Payment cùng transaction |
| Wallet hủy dòng | Khóa nguồn, hoàn netAmount snapshot bằng `OrderRefund`, khóa `OrderRefund:{orderId}:{orderDetailId}`; liên kết Refund với khoản thu gốc và ledger; khách Locked vẫn nhận được hoàn |
| Cash tạo/chờ | PendingCollection; không debit/credit ví, không ghi khoản thu Succeeded; không áp TTL của gateway, nhân viên xử lý hoặc người có order.cancel hủy theo trạng thái |
| Cash thu | PayableAmount > 0, còn dòng Pending và mọi dòng còn hiệu lực đều Pending. Nhân viên có payment.create gửi cashReceived=true sau khi thực nhận đủ tiền. Server ghi Payment Succeeded theo số còn phải thu, tăng version, món tiếp tục Pending chờ nhân viên nhận để làm |
| Cash hủy trước thu | Giảm PayableAmount; trả tồn theo dòng; không tạo Refund và không cộng ví |
| Cash đã thu | Quyết định 14/11/2026 khóa yêu cầu hủy khách và thao tác hủy admin khi đơn đã có khoản thu Succeeded, kể cả món còn Pending. Trả 409 CashCancellationLocked. Quản lý xử lý ngoại lệ tại quầy qua nghiệp vụ tiền có audit ở S3, cần payment.refund, bằng chứng thực hoàn và nguồn gốc |

Khóa ledger và khóa HTTP là hai lớp khác nhau: ledger ngăn trừ/hoàn cùng nguồn hai lần;
khóa HTTP ngăn tạo nhiều OrderId từ cùng ý định. Không ghép khóa client dài vào khóa ledger.
Tổng Refund Pending + Succeeded không vượt khoản thu gốc hoặc giá trị hủy đủ điều kiện.

**Hủy đơn tiền mặt đã thu — quyết định 14/11/2026.** Khi Cash đã có khoản thu Succeeded, UI khóa nút yêu cầu hủy và hủy trực tiếp, hiển thị “Đã thu tiền, liên hệ quản lý để xử lý tại quầy”. API kiểm tra lại khoản thu dưới khóa và trả 409 CashCancellationLocked cho cả request khách và admin. Nếu có yêu cầu hủy gửi trước lúc thu, thao tác duyệt sau lúc thu cũng bị từ chối. Thu và hủy cạnh tranh chỉ cho một kết quả hợp lệ theo version/khóa. Khoản thu đã commit được giữ để đối soát. Ngoại lệ quản lý ghi nhận theo nghiệp vụ điều chỉnh tiền có audit ở S3.
Yêu cầu hoàn khác khóa cũng không được hoàn lại cùng dòng. Wallet hủy cả đơn ghi các khoản hoàn
theo dòng chưa hoàn với khóa OrderRefund ở bảng trên. ZaloPay hủy toàn đơn có thể dùng một Refund
OrderDetailId null cho phần còn lại: amount do server tính từ tập dòng chưa được bao phủ bởi Refund
Pending/Succeeded. Lưu tập nguồn hủy trong thao tác, khóa khoản thu gốc và trừ cả phần đang chờ;
không thêm khoản hoàn tổng trùng khoản dòng. Khóa ổn định `OrderRefund:{orderId}:All` cho phần hoàn này.
V1 thu cash từng đơn; thu cash gộp/hóa đơn gộp dùng contract chứng từ Trường, không tự mở rộng DTO này.

#### 7.1.1. Đã nhận tiền mặt nhưng chưa ghi nhận được khoản thu

Rollback SQL hoặc lỗi version không hoàn tác tiền đã nhận tại quầy. Trước khi gửi cash-collections, nhân viên giữ phiếu tiếp nhận có mã phiếu duy nhất, OrderId, số tiền thực nhận theo số phải thu server vừa hiển thị, người nhận, thời điểm, Idempotency-Key và expectedVersion. Phiếu/bằng chứng phải còn tra được khi browser hoặc API khởi động lại; có thể dùng phiếu quầy trong giai đoạn bàn giao, không chỉ giữ trong RAM. Phiếu không thay thế Payment hoặc làm đơn Paid.

1. Mất phản hồi hoặc chưa rõ transaction đã commit: tra/retry cùng key và nguyên body để lấy kết quả theo mục 8.1. UI giữ trạng thái đang đối soát, không hướng dẫn thu khách lần hai, đổi version/key hoặc tạo đơn thay thế. GET đơn thấy Paid chưa đủ chứng minh chính phiếu này đã được ghi; phải khớp kết quả thao tác/khoản thu và phiếu.
2. Retry xác nhận thao tác đã thành công thì dùng cùng Payment/kết quả đã lưu, kết thúc phiếu tiếp nhận. Không thu lại hoặc ghi thêm Payment. Nếu vẫn lỗi hạ tầng thì tiếp tục giữ bằng chứng và chuyển quản lý, không suy từ timeout rằng tiền chưa được ghi.
3. Khi server xác nhận request thất bại, quản lý có order.cancel và payment.refund đối chiếu phiếu, thao tác và khoản thu dưới cùng khóa nguồn. Nếu khoản thu Succeeded đã xuất hiện, áp dụng khóa hủy và quy trình ngoại lệ khoản đã thu tại mục 7.1; không trả tiền theo nhánh chưa có Payment. Nếu chưa có khoản thu, có thể tiếp tục đúng ý định cũ bằng replay; khi chọn trả lại tiền, trước hết phải chốt hủy toàn bộ dòng còn Pending và đổi version của đơn, hoặc xác nhận đơn đã Cancelled. Thao tác hủy dùng đường hủy/khóa/version hiện có, có key riêng và audit liên kết phiếu. Nếu thu thắng tranh chấp, hủy trả CashCancellationLocked và phải quay lại nhánh có Payment. Chỉ trả tiền sau khi kết quả hủy đã commit và không có khoản thu Succeeded; request thu cũ đến muộn bị chặn bởi version/trạng thái, không hồi sinh đơn.
4. Quản lý xác nhận tiền mặt đã thực trả đủ theo phiếu và lưu kết quả đối soát bền vững cùng mã phiếu, OrderId, key thu cũ, số tiền, người nhận/trả/xác nhận, thời điểm và bằng chứng. Phiếu đã trả được đóng một lần; lặp thao tác chỉ tra kết quả cũ. Đây là tiền nhận rồi trả ngoài Payment: không tạo Payment/Refund giả, không cộng ví và không tính thành doanh thu. Nếu chưa trả được hoặc chưa lưu được xác nhận, giữ phiếu mở để đối soát, không báo đã hoàn tất.

UI/quy trình quầy S2 phải bàn giao được phiếu và kết quả tra cứu; nghiệp vụ điều chỉnh có audit thuộc phần ngoại lệ tiền S3 đã nêu ở mục 7.1. Trường hợp ghi nhận thu thành công vẫn khóa hủy theo lựa chọn Trưởng nhóm; bước trả tiền ngoài Payment ở đây chỉ dành cho khoản thực nhận chưa được ghi thành công.

### 7.2. ZaloPay sandbox

**Chốt TTL giữ chỗ nội bộ 15 phút**, cấu hình server `Checkout:ReservationMinutes=15`.
Lưu ReservedUntilUtc một lần từ thời điểm SQL chấp nhận đơn, trả qua expiresAtUtc; retry/restart không gia hạn.
Adapter yêu cầu hạn provider tương ứng nếu provider hỗ trợ; expiresAtUtc cho UI luôn là hạn nội bộ đã lưu.
Nếu provider còn cho trả tiền sau hạn nội bộ, UI dừng hiển thị nút/link trả tiền khi hết hạn;
callback/query thu sau đó vẫn theo chính sách thu muộn bên dưới, không kéo dài giữ tồn/lượt.
Khả năng cấu hình hạn provider phải được Minh kiểm chứng trong task gateway, không mặc định đã được sandbox xác nhận.

1. Transaction ngắn tạo Order AwaitingPayment, Sale/usage, Payment Pending, operationKey
   `OrderPayment:{orderId}` và mã merchant duy nhất gắn đúng order/customer/amount. Commit trước gọi provider.
2. Adapter nhận `{ orderId, customerId, paymentId, amountVnd, expiresAtUtc, operationKey }` từ server.
   Trả `{ paymentId, status: Pending/Failed, paymentUrl?, expiresAtUtc }`; timeout không được suy thành Failed.
   [Contract S1-M-2](005-zalopay-sandbox-contract.md) chốt cấu hình, chữ ký và route callback logic;
   adapter gọi provider/settlement thuộc S2. Listener probe S1 chỉ kiểm tra HTTPS/chữ ký, không ghi tiền.
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
   kể cả provider thu trước hạn nhưng callback đến sau. Đây là ranh giới đã chốt theo D05 mục 11.2.
6. Worker Khang quét AwaitingPayment quá hạn kể cả sau restart, dùng cùng đường hủy/khóa nguồn;
   worker Minh đối soát khoản Pending/Expired/Failed chưa có kết luận chắc chắn và Refund Pending.
   Worker hết hạn quét mỗi 15 giây và quét ngay sau startup; callback cũng kiểm tra hạn dưới khóa,
   không phụ thuộc worker đã chạy hay chưa. Callback và worker cạnh tranh chỉ một đường giải phóng.
   TTL hết không chứng minh chưa bị thu; chu kỳ quét không gia hạn điều kiện chấp nhận thanh toán.
7. Thu muộn tạo nghĩa vụ hoàn theo khoản thu gốc trong cùng transaction ghi nhận tiền. Chuyển
   Admin/Manager có payment.refund duyệt gửi hoàn; không tự gọi hoàn hoặc cộng ví từ callback.
   Lưu ApprovedByEmployeeId/ApprovedAtUtc trên Refund; callback tạo nghĩa vụ với hai trường null.
   Admin đọc GET danh sách Refund của đơn, lấy id/orderVersion của dòng Pending chưa duyệt,
   rồi POST approve với Idempotency-Key và expectedVersion đó. Route approve khóa đúng nguồn/Refund,
   kiểm tra version và hạn mức, ghi actor/thời điểm một lần, trả RefundDto có thông tin duyệt/version mới.
   Pending đã chiếm hạn mức từ lúc tạo; approve không tạo Refund mới hoặc giữ thêm cùng số tiền lần nữa.
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
     truy nguồn và kiểm tra lại số đã hoàn/đang giữ trước khi tạo yêu cầu nghiệp vụ mới với khóa mới
     theo POST replacements ở mục 7.4; không gọi lại cancellations cho dòng đã Cancelled.
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
| NotRequired | PayableAmount=0, chưa có khoản thu/hoàn Succeeded và không còn nghĩa vụ thu/hoàn hoặc giao dịch gateway chờ đối soát. Gồm đơn gốc 0 đồng, Cash hủy hết chưa thu và Cash hủy dòng có tiền chỉ còn dòng Pending netAmount=0; giữ nguyên OriginalPayable/snapshot |
| PendingCollection | Cash còn tiền phải thu và chưa thu |
| Failed / Expired | Gateway chưa có khoản thu Succeeded và yêu cầu thu ở trạng thái tương ứng; requiresReconciliation vẫn true nếu chưa chắc provider chưa thu |
| Pending | Các trường hợp gateway chưa rõ còn lại |

Thu muộn không được che bằng Expired. Dòng Payment gốc vẫn Succeeded sau hoàn; Refund là dòng riêng.

Ví dụ hai dòng giá 5000, giảm cố định 9999: phân bổ giảm 5000/4999, netAmount=0/1 và OriginalPayable=1. Cash chưa thu hủy dòng netAmount=1 thì PayableAmount=0, paymentStatus=NotRequired; dòng netAmount=0 còn Pending nhận được, không tạo Payment/ledger 0 đồng. Nếu đã có khoản thu/hoàn hoặc gateway chưa rõ kết quả thì giữ các trạng thái Paid/RefundPending/Refunded/đối soát tương ứng, không dùng NotRequired để che lịch sử tiền.

Ví dụ đối soát sau hủy một dòng trị giá 40000 của đơn đã thu 100000, PayableAmount còn 60000:

| Mốc xử lý | RefundPendingAmount | RefundUnresolvedAmount | RefundedAmount | paymentStatus |
| --- | --- | --- | --- | --- |
| Yêu cầu hoàn Pending | 40000 | 40000 | 0 | RefundPending |
| Xác minh thất bại, không còn attempt chưa rõ; Refund Failed | 0 | 40000 | 0 | RefundPending, requiresReconciliation=true |
| Phương án mới được duyệt và xác minh hoàn 40000 thành công | 0 | 0 | 40000 | PartiallyRefunded |

Mốc Failed chỉ nhả phần giữ hạn mức kỹ thuật; khách vẫn còn 40000 chưa nhận. Mốc cuối phải truy được
nguồn hủy/khoản thu/yêu cầu mới, không đổi dòng Failed cũ thành Succeeded hoặc ghi hoàn thêm từ retry.

### 7.3. Giao điểm phí phiên trước thanh toán ví

**Quyết định D09:** áp dụng phí phiên tích lũy theo giây của contract phiên Phúc. Trước khi debit ví cho đơn có OriginalPayable > 0,
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

Chốt cách tính: P là giá VND/giờ snapshot khi mở phiên; s là số giây hoàn tất từ StartTime đến now;
`C = ceil(P * s / 3600)`, `D = C - tổng SessionCharge đã commit`. Chỉ ghi GamingCharge=-D khi D>0,
D=0 không thêm ledger; D<0 báo lệch sổ, không tự hoàn. Worker phí tính lại mỗi 60 giây từ StartTime, khi đóng phiên,
trước chi ví khác và khi phục hồi; mỗi lần dùng tổng tích lũy để tránh cộng trùng sai số làm tròn.
Giới hạn phí theo ngân sách đã thu + số dư có sẵn, kết thúc phiên khi hết khả năng tài trợ theo contract phiên.
Ví dụ P=10000 VND/giờ: C tại 60/120/180 giây là 167/334/500; các khoản tăng là 167/167/166,
không phải ba lần làm tròn độc lập thành 167. Giá sửa sau khi mở phiên không đổi P đã snapshot.
Không triển khai thu trả trước đầu chu kỳ song song. Minh/Phúc cập nhật phần mô tả trả trước trong contract ví
khi triển khai theo quyết định này; đây là công việc đồng bộ phụ thuộc, không phải lựa chọn chính sách còn mở.
Khang gọi dịch vụ phiên, không sao chép công thức tính phí vào module đơn. Task tài liệu chưa sửa code phiên/ví.

### 7.4. Đọc, duyệt và thay thế khoản hoàn Failed

Khang cung cấp các route quản trị ở mục 3; Application gọi logic hoàn của Minh trong cùng transaction.
Các thao tác này không phụ thuộc món còn Pending: nguồn hoàn là phần hủy đã được xác minh và khoản thu
gốc đã Succeeded. Đơn/món vẫn Cancelled/Expired; không hủy lần nữa, trả tồn/lượt lần nữa hoặc thu thêm.

**Đọc và duyệt sau thu muộn:** admin có order.view + payment.refund gọi GET `/api/admin/orders/34/refunds`;
trong RefundPageDto đọc được dòng 8101 như ví dụ mục 4, bao gồm nguồn 7001, amount và thông tin chưa duyệt.
POST `/api/admin/orders/34/refunds/8101/approve` với expectedVersion từ GET chỉ ghi duyệt dòng đó.
Worker ghi/claim RefundAttempt bền vững cho Refund đã duyệt rồi commit trước lời gọi provider;
retry/worker khác phải query/claim cùng attempt theo adapter Minh, không tạo một khoản hoàn mới.
Sau query xác minh thành công, GET trả dòng 8101 Succeeded; replay approve cùng key trả cùng RefundDto,
không tạo/duyệt/gửi hoàn thêm. Người không đủ quyền nhận 403 và không được đọc/duyệt nguồn này.

**Phương án thay thế V1:** chỉ tạo yêu cầu ZaloPay mới cùng khoản thu gốc, cùng nguồn hủy, cùng amount
và cùng method với Refund Failed. Không đổi sang cash/ví hoặc tăng số tiền qua route này.
POST `/api/admin/orders/{orderId}/refunds/{failedRefundId}/replacements` nhận reason/expectedVersion
và Idempotency-Key mới cho ý định nghiệp vụ thay thế; không nhận dữ liệu tiền/nguồn do client tự quyết.

1. Xác thực và kiểm tra quyền/sở hữu nguồn; tìm replay trước version như mục 8. Lấy khóa chung Customer
   → Order → Payment gốc → các Refund liên quan theo ID → RefundAttempt theo mục 8; kiểm tra lại version.
2. Refund tiền nhiệm phải Type=Refund, Method=ZaloPay, Status=Failed, thuộc đúng đơn/khoản thu gốc Succeeded.
   Mọi attempt cũ đã được xác minh thất bại/không thực hiện, không còn attempt đang gửi/xử lý/chưa rõ;
   mọi claim cũ đã kết thúc/vô hiệu để worker không thể tiếp tục gửi. Nếu đang đối soát hoặc có bằng chứng kết quả mâu thuẫn thì trả 409,
   không lấy việc nhấn nút thay thế làm bằng chứng provider chưa hoàn.
3. Tiền nhiệm chưa có yêu cầu thay thế. Server sao chép sourceItems snapshot bất biến và amount cũ;
   kiểm tra cho từng nguồn hủy: phần đã hoàn Succeeded + đang giữ bởi Refund Pending + phần mới
   không vượt netAmount đã hủy. Kiểm tra cùng hạn mức trên khoản thu gốc và số tiền chưa hoàn của đơn.
   Refund Failed không được tính như đã hoàn hoặc đang giữ; không được bỏ qua các yêu cầu khác cùng nguồn.
   Nguồn đã hoàn/đang giữ không còn đủ cho toàn amount tiền nhiệm thì 409, không giảm amount ngầm.
4. Nếu khoản thay thế là hoàn từng phần nhưng điều kiện giao dịch đã xác minh cấm hoàn từng phần,
   không tạo yêu cầu để thử vượt hạn chế;
   trả 409 RefundReplacementNotAllowed, giữ lịch sử Failed và requiresReconciliation để nhóm xử lý phương án khác.
   Nếu chưa biết điều kiện thì theo cơ chế gửi một lần sau duyệt của mục 7.2, không mặc định chặn trước.
5. Tạo một Payments.Type=Refund mới: RefundOfPaymentId vẫn là ID khoản thu gốc, ReplacementOfRefundId
   trỏ tiền nhiệm Failed, Status=Pending, ApprovedByEmployeeId/ApprovedAtUtc=null. Dùng OperationKey ổn định
   `OrderRefundReplacement:{failedRefundId}` trong Payments; lưu OrderOperations theo scope/key HTTP,
   fingerprint reason/version và resource ID mới, cùng sourceItems snapshot. Giữ phần hạn mức mới,
   tăng version Order và ghi actor/reason/audit của thao tác trong cùng transaction; commit mới trả 201 RefundDto.
   Tạo yêu cầu thay thế chưa đồng nghĩa đã duyệt hoặc đã gửi provider.
6. Admin đọc RefundDto mới và POST approve bằng một Idempotency-Key riêng, expectedVersion mới.
   Worker chỉ gửi Refund mới sau duyệt, với attempt/mã hoàn riêng do adapter Minh cấp; không sửa hoặc gửi
   lại dòng Failed/attempt cũ. Query thành công mới ghi Succeeded và cập nhật tiền/chứng từ một lần.

Mỗi Refund có tối đa một yêu cầu thay thế trực tiếp, bảo vệ bằng unique filtered index trên
Payments.ReplacementOfRefundId. Cùng key/payload replay trả 200 cùng ID mới và Idempotency-Replayed=true;
cùng key khác payload 409. Khác key nhưng tiền nhiệm đã có yêu cầu thay thế trả 409 RefundReplacementConflict
(hoặc OrderVersionConflict nếu version cũ), không tạo nhánh hoàn song song. GET tiền nhiệm trả replacedByRefundId
để admin theo tiếp. Nếu yêu cầu mới lại Failed thì quy trình có thể tiếp tục từ chính ID mới đó;
không tạo thêm con từ tiền nhiệm cũ. Không tự tạo chuỗi yêu cầu mới bằng retry kỹ thuật.

Ví dụ tiếp nối: dòng 8101 đã được xác minh Failed, vẫn còn 40000 chưa hoàn, mọi attempt cũ đã chốt
và kênh hiện cho phép hoàn số tiền đó. Admin GET 8101 lấy orderVersion mới rồi gửi:

```http
POST /api/admin/orders/34/refunds/8101/replacements
Authorization: Bearer <admin-or-manager-token>
Idempotency-Key: refund-replace:8101:manual-1
Content-Type: application/json

{
  "expectedVersion": "AAAAAAAAAAM=",
  "reason": "Da doi soat that bai dut khoat; de nghi yeu cau hoan thay the cung nguon"
}
```

| Mốc xử lý minh họa | Refund 8101 | Refund 8102, ReplacementOfRefundId=8101 | Tổng thực hoàn | RefundPendingAmount | RefundUnresolvedAmount |
| --- | --- | --- | --- | --- | --- |
| Tạo thay thế thành công, trả id=8102 | Failed, giữ nguyên lịch sử | Pending, amount=40000, chưa duyệt | 0 | 40000 | 40000 |
| Duyệt 8102 và worker/query xác minh thành công | Failed, không gửi lại | Succeeded, amount=40000 | 40000 | 0 | 0 |
| Replay create/approve, callback/query lặp | Failed, replacedByRefundId=8102 | Cùng id=8102, không thêm khoản tiền/attempt do replay | 40000 | 0 | 0 |

Nếu query phát hiện kết quả cũ trái kết luận Failed khi đang có yêu cầu thay thế, dừng claim/gửi mới
cho nguồn đang tranh chấp và chuyển đối soát có duyệt; query các attempt liên quan trước điều chỉnh tiền.
Không tự giải phóng khoản đang xử lý ở provider, che khoản thực hoàn hoặc cấp thêm yêu cầu thay thế.

## 8. Chống lặp, transaction và lỗi

### 8.1. Idempotency và cạnh tranh

- Header `Idempotency-Key`: 8..100 ký tự ASCII `[A-Za-z0-9._:-]`, giữ nguyên cho một ý định/retry.
  Lưu bền scope `(actorType, actorId, operation/route resource, key)`, fingerprint DTO đã normalize
  (bao gồm thứ tự items, tập dòng hủy chuẩn hóa và expectedVersion), result resource và thời gian cùng transaction.
  itemIds null giữ nguyên null trong fingerprint; tập dòng thực tế đã chốt lưu riêng cùng thao tác,
  không suy lại từ trạng thái mới khi tính fingerprint cho replay.
  Có unique constraint trên scope. Không cache key chỉ trong RAM; không xóa key của nguồn còn lưu lịch sử.
- Cùng scope/key + payload giống: lần đầu create đơn/yêu cầu hủy/yêu cầu hoàn thay thế 201, replay 200 với
  `Idempotency-Replayed: true` và cùng resource ID, DTO đọc trạng thái hiện tại; không tạo side effect mới.
  Các thao tác khác 200 cả lần đầu và replay. Cùng key khác payload → 409 IdempotencyConflict.
- Xác thực, quyền và sở hữu luôn kiểm tra trước replay; replay đã thành công được tìm trước kiểm tra
  version/trạng thái nghiệp vụ (ví dụ khách vừa Locked, đơn vừa hết hạn) để không tạo giao dịch mới.
  Nếu scope chưa có thì áp toàn bộ kiểm tra Active/version/trạng thái. Request thất bại rollback không chiếm key.
- Chỉ kiểm tra key trước insert là chưa đủ: khóa Customer của chủ đơn để serialize tạo đơn và tìm lại key
  trong transaction; unique scope là bảo vệ cuối. Wallet ledger còn tìm khóa riêng sau khóa khách trước Record.
- Thứ tự khóa chốt theo S1-T-1: Customer → Computer → GamingSession → SessionCharge → Order
  → Product → Promotion → Payment gốc → Refund → RefundAttempt → Invoice → InvoiceIssueRequest → audit.
  Chỉ lấy nhóm cần dùng; trong nhóm ID tăng dần. Mọi writer/callback/worker/phát hành dùng cùng thứ tự;
  không giữ Product rồi quay lại lấy Customer/Order. Đọc ID trước được, phải đọc lại sau khóa.
- Caller mở một SQL transaction/DbContext dùng chung. Inventory/Voucher/Wallet/Invoice chỉ sửa dữ liệu,
  không tự commit hoặc transaction lồng. Lưu snapshot/nguồn/ledger/thu-hủy/usage/key trong transaction;
  sau commit mới gửi event hoặc gọi gateway. Lỗi SaveChanges/deadlock rollback toàn thao tác, trả 409;
  client GET và retry cùng key, không tự retry với khóa mới.
- OrderId/OrderDetailId hiện là SQL identity, trong khi khóa tiền/tồn cần ID nguồn. Chốt caller flush
  Order/Details bằng SaveChanges trong transaction đang giữ khóa để lấy ID, rồi ghi ledger/tồn/usage/Payment/key
  và SaveChanges trước một lần commit cuối. Lỗi bất kỳ bước nào rollback cả lần flush đầu, không lộ đơn dở dang.
  Không gọi gateway/phát event giữa hai lần lưu. Đối chiếu code S1-M-1 cho thấy SaveChangesAsync không commit;
  ExecuteInTransactionAsync chỉ commit sau khi callback hoàn tất, nên cấu trúc code cho phép cách lưu này
  nếu các module dùng cùng DbContext. Hướng dẫn “một SaveChanges” của S1-M-1 cần được Minh đồng bộ
  thành “một transaction, một commit cuối; có thể flush identity trước khi lưu phần còn lại” theo D08.
  Code nguồn hỗ trợ cấu trúc này; chưa có test rollback tích hợp đơn/ví hoặc phản hồi chấp thuận riêng của Minh.
  Không giả định có OrderId thật trước khi EF nhận identity hoặc commit đơn riêng để lấy ID.
- Order có rowversion. VersionRequest kiểm tra expectedVersion dưới khóa; bất kỳ thay đổi dòng,
  yêu cầu hủy, thu/hoàn hay trạng thái gateway đều tăng version của aggregate. Version cũ trả 409
  OrderVersionConflict, không ghi ví/tồn/lượt. Replay đã lưu được trả trước kiểm tra version.

### 8.2. Lỗi HTTP

Giữ dạng ProblemDetails có title/status/detail/instance/traceId như ApiExceptionHandler hiện có.
Đề xuất bổ sung `code` cho module đơn; code này chưa được handler nền xuất ra. 401/403 từ middleware
có thể chưa có ProblemDetails; client phải xử lý được bằng HTTP status.

| HTTP / code | Điều kiện |
| --- | --- |
| 400 ValidationError | DTO/header sai, unknown field, enum sai, số lượng/tiền vượt giới hạn, thiếu reason/version |
| 401 | Chưa đăng nhập/token sai hoặc hết hạn |
| 403 | Sai loại principal, thiếu permission, nhân viên không hoạt động |
| 404 OrderNotFound / OrderItemNotFound | Không tồn tại hoặc khách không sở hữu; dòng không thuộc đơn |
| 404 RefundNotFound | Không có Refund hoặc Refund không thuộc orderId trên route; không trả dữ liệu nguồn khác |
| 409 CustomerLocked / SessionNotActive | Tạo mới khi khách Locked, phiên không Active/không thuộc khách; không áp vào replay đã lưu |
| 409 ProductUnavailable / InsufficientStock | Món ngừng bán, thiếu tồn tại lúc giữ khóa |
| 409 VoucherInvalid / VoucherLimitExceeded | Mã không hợp lệ/hết hạn/không đạt ngưỡng/hết lượt |
| 409 InsufficientWalletBalance | Không đủ tiền sau tính giá/giảm; không có đơn/ledger/tồn/usage mới |
| 409 PaymentRequired | Confirm món khi số còn phải thu > 0 hoặc khoản thu chưa được server xác minh; giữ trạng thái món và tiền/tồn |
| 409 CashCancellationLocked | Yêu cầu/duyệt hủy Cash đã có khoản thu Succeeded, kể cả dòng Pending; giữ khoản thu, tồn và trạng thái món |
| 409 InvalidOrderTransition / OrderVersionConflict | Confirm/hủy/thu sai trạng thái hoặc version cũ |
| 409 CancellationConflict / IdempotencyConflict | Trùng yêu cầu Pending hoặc dùng lại key khác payload |
| 409 RefundReplacementNotAllowed / RefundReplacementConflict | Tiền nhiệm không phải Refund ZaloPay Failed đủ điều kiện, còn attempt chưa rõ, nguồn/tiền đã hoàn hoặc đang giữ không đủ, kênh cấm hoàn, hoặc đã có yêu cầu thay thế trực tiếp |
| 409 ConcurrencyConflict | Unique/deadlock/concurrency ở SQL; toàn bộ thao tác chưa commit rollback |
| 500 | Lỗi nội bộ chưa phân loại; không lộ SQL/secret; client GET và retry cùng key khi chưa rõ commit |

## 9. Giao điểm module, chứng từ và event

| Owner | Điểm tích hợp của hợp đồng | Bất biến cần giữ |
| --- | --- | --- |
| Khang | Order application service/DTO, snapshot, version, yêu cầu/duyệt hủy, TTL worker | Orchestrate cùng transaction; không tự ghi ví hoặc tạo bộ kho/voucher khác |
| Minh | WalletLedger.Record; FindTrackedForWalletUpdateAsync; FindWalletTransactionByKeyAsync | OrderPayment âm / OrderRefund dương; SaveChanges/commit do caller; khóa ledger <=100 ký tự |
| Minh | Payments neo OrderId; adapter tạo/query/hoàn purpose Order, worker đối soát | Unique operationKey/mã provider; RefundOfPaymentId; lưu Pending trước gọi ngoài transaction; không ghi tiền từ redirect |
| Khang + Minh | GET Refunds/RefundDto, POST approve/replacements và logic ứng dụng mục 7.4 | Admin đọc được refundId/nguồn/amount/thông tin duyệt; thay thế Failed bằng khoản mới có nguồn/lineage và cùng transaction/hạn mức; không hủy lại món |
| Trường | Inventory.ApplySale/ApplyReturn; Voucher.ValidateAndUse/ReleaseForCancelledOrder | Tồn khả dụng + lịch sử; một usage/đơn; snapshot/phân bổ và giải phóng idempotent |
| Trường | Invoice.IssueForOrders; Invoice.ApplyFinancialChange | Hóa đơn món chỉ lấy nguồn OrderId/OrderDetailId và liên kết khoản thu đã có. Phí phiên phát hành riêng bằng IssueForSession theo quyết định 14/11/2026 |
| Phúc | Hạ tầng realtime theo S1-P-2 | Khang nối event sau commit vào nhóm customer/nhân viên được server cấp quyền |
| Phúc + Minh | SessionSettlement.BeforeWalletDebit theo mục 7.3 | Quyết toán phí theo một policy chung trước khi chi ví cho đơn; cùng khóa/transaction, replay không ghi phí mới |

Tên Inventory/Voucher/Invoice trên là giao diện logic của S1-T-1, chưa phải class/interface đã có trên base.
Application chỉ tham chiếu Domain; Infrastructure giữ EF/SQL, API giữ HTTP/claims/hub. Adapter callback
chuyển kết quả đã xác minh vào use case đơn, không gọi HTTP vòng qua API công khai hay giả principal.

Không tạo hóa đơn riêng cho mỗi checkout để lách Payment.InvoiceId bắt buộc. Khoản thu/hoàn lưu ở
Payments theo nguồn OrderId, InvoiceId null trước phát hành theo thiết kế S1-T-1; cần migration mới.
Khi nhân viên phát hành hóa đơn theo tập đơn/phiên, nối các khoản đã có, không debit/thu lần hai.

Quyết định 14/11/2026 chọn xuất riêng hóa đơn món ăn và tiền giờ. IssueForOrders nhận tập OrderId cụ thể cùng khách, các dòng chỉ mang OrderDetailId. IssueForSession chỉ nhận nguồn SessionCharge và orderIds rỗng. Hóa đơn món vẫn truy được GamingSessionId của đơn để đối soát, còn hóa đơn tiền giờ tự đóng chỉ chứa phí giờ. Snapshot hóa đơn lịch sử gộp được đọc/in theo dữ liệu đã lưu.
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

| Nguồn hiện tại | Bổ sung/điều chỉnh cần triển khai | Owner |
| --- | --- | --- |
| Orders | Giữ OrderId/CustomerId/GamingSessionId/OrderTime; TotalAmount map OriginalPayable; thêm Subtotal, DiscountAmount, snapshot voucher, PaymentMethod, ReservedUntilUtc, rowversion; Status theo mục 5 | Khang |
| Orders.EmployeeId NOT NULL | Nullable cho khách/hệ thống; actor type + liên kết khách qua CustomerId, staff qua EmployeeId; không đặt ID 0 hoặc CustomerId vào FK nhân viên | Khang + Đại đối chiếu actor |
| OrderDetails | Giữ UnitPrice làm snapshot; thêm ProductNameSnapshot, DiscountAmount, thứ tự dòng, Status, thông tin confirm/hủy/reason; Quantity không sửa sau tạo | Khang |
| OrderStatusHistory (table theo EF config) | ChangedBy nullable cho khách/hệ thống, thêm ActorType/reason; truy customer qua Order, không giả nhân viên; lịch sử dòng và yêu cầu hủy cũng lưu được actor/time | Khang + Đại |
| Chưa có nguồn lưu yêu cầu hủy | OrderCancellationRequests + tập OrderDetailId được chuẩn hóa; người yêu cầu/xử lý, lý do, trạng thái/thời gian; kiểm tra trùng Pending dưới khóa Order | Khang |
| Chưa có khóa thao tác đơn | OrderOperations: scope/key/fingerprint/orderId/result type/ID/thời gian; snapshot nguồn RefundSourceItems gồm OrderDetailId/Amount do server tạo, bất biến và lưu cùng thao tác hoàn (kể cả hoàn toàn đơn); unique scope; cùng transaction nguồn, không chỉ dùng WalletTransactions để chống tạo trùng đơn | Khang; Minh đối chiếu liên kết Refund |
| Products/InventoryTransactions | Stock là khả dụng; Sale/Return theo OrderDetailId, unique nguồn/type; Stock>=0, Before/After; EmployeeId nullable cho khách/hệ thống theo S1-T-1 | Khang Product; Trường lịch sử tồn |
| Promotions và lượt dùng | Snapshot/validation/rule + PromotionUsages unique OrderId; ReleasedAt và hạn giữ chỗ truy từ Orders, giữ chỗ chiếm hạn mức | Trường; Khang cung cấp trạng thái/hạn |
| Payments.InvoiceId NOT NULL, chưa có nguồn/khóa hoàn | Theo S1-T-1: nullable InvoiceId, OrderId/SessionChargeId XOR, Type, RefundOfPaymentId, OrderDetailId cho hoàn dòng, OperationKey unique, trạng thái/mốc thời gian/mã gateway; RefundAttempts thuộc adapter | Minh + Trường |
| Refund chưa có nguồn duyệt trên base | Thêm ApprovedByEmployeeId/ApprovedAtUtc cho khoản hoàn gateway; null khi hệ thống tạo nghĩa vụ thu muộn; worker không gửi trước duyệt, không giả actor nhân viên. Nghĩa vụ chưa hoàn suy từ snapshot hủy + khoản thu/hoàn, không xóa dòng Failed | Minh; Trường đối chiếu với S1-T-1 |
| Refund chưa có quan hệ thay thế trên base | Payments.ReplacementOfRefundId int? tự FK Payments.PaymentId, NoAction; chỉ Type=Refund, khác PaymentId; unique filtered khi khác null để mỗi tiền nhiệm có tối đa một con trực tiếp. Service kiểm tra tiền nhiệm Failed cùng nguồn/amount/method và mọi attempt đã chốt; replacedByRefundId trong DTO suy ngược từ FK | Minh; Khang gọi nghiệp vụ, Trường đối chiếu |
| WalletTransactions chưa có IdempotencyKey trên base | S1-M-1 cung cấp query/contract theo 003; writer và migration ví thuộc S2-M-1. Nhận migration của Minh khi nhóm tích hợp, không viết lại lịch sử | Minh |
| InvoiceOrders/InvoiceDetails | Unique nguồn, snapshot và cập nhật tài chính theo contract Trường; không tạo Invoice.OrderId thay quan hệ gộp | Trường |

DB constraint bảo vệ giá trị nội dòng/FK/unique. Đúng chủ ví, dòng thuộc đơn, tổng giảm, hạn mức hoàn,
usage cuối và sự khớp provider là kiểm tra nghiệp vụ dưới khóa, không tuyên bố FK/unique tự chứng minh đủ.
Không sửa migration đã áp. Không nhập migration MySQL/schema 19 bảng hoặc chuyển/xóa dữ liệu cũ.

## 11. Quyết định thiết kế và bàn giao S1-K-2

### 11.1. Kết quả đối chiếu kỹ thuật và review

Nguồn ghim tại mục 1; kết quả dưới đây là đối chiếu tài liệu/code, không phải phản hồi chấp thuận của owner
hoặc bằng chứng đã chạy tích hợp. Review do người dùng cung cấp ngày 08/11/2026 cho commit 66f1a29
xác nhận hai finding P1/P2 đã được xử lý ở mức thiết kế, chưa phát hiện lỗi mới trong diff so với 06ba492.
Bản 0.4 tiếp tục chốt chính sách theo ủy quyền mới, vẫn cần Trưởng nhóm review thay đổi này.

| Giao điểm | Nguồn đối chiếu | Kết quả bàn giao |
| --- | --- | --- |
| Loại/dấu ví, số tiền 0 và khách Locked | S1-M-1 tại 8b2e6a2, WalletLedger.Record và mục 1/2/4 hợp đồng ví | Khớp: OrderPayment âm, OrderRefund dương; không ghi giao dịch 0; hoàn được cho khách Locked; tìm khóa trước ghi |
| Snapshot, voucher và tồn | S1-T-1 mục 3.1/3.2/4 | Khớp quy tắc sau giảm/phân bổ/hoàn lượt; giữ UnitPrice sẵn có, Sale/Return theo dòng, không giảm/trả hai lần |
| Tiền và hóa đơn | S1-T-1 mục 6.1–6.4/7 | Khớp thiết kế Payments neo nguồn trước hóa đơn, Refund tách khoản thu; không debit lại khi phát hành hoặc cộng đôi ledger/Payment |
| Hoàn từng phần chưa rõ/bị từ chối | S1-T-1 bản 1.6/1.7, mục 6.4/6.4.1 | Đã sửa mục 7.2: chưa biết thì gửi một lần sau duyệt; chỉ giữ Pending khi chưa chốt; thất bại dứt khoát chuyển Failed đúng điều kiện, vẫn theo dõi tiền còn phải hoàn |
| Thu muộn của Payment Failed | S1-T-1 bản 1.6/1.7, bảng trạng thái và quy tắc audit mục 6.2.2 | Đã bổ sung query độc lập, đối chiếu giao dịch, audit cùng transaction, callback chỉ ack sau commit và worker query khi rollback |
| P1: admin không lấy được refundId để duyệt | Review SCRUM-11 tại 06ba492 | Đã bổ sung GET danh sách/chi tiết, RefundDto có nguồn/amount/trạng thái/actor duyệt/version và ví dụ; approve trả đúng RefundDto; case thu muộn → đọc → duyệt → worker → replay ở mục 7.4/12 |
| P2: thiếu đường tiếp tục sau Refund Failed | Review SCRUM-11 tại 06ba492; S1-T-1 mục 6.4 | Đã có POST replacements, lineage unique, cùng nguồn/amount/method, kiểm tra attempt/hạn mức và duyệt khoản mới; ví dụ 40000 thất bại rồi thành công, giữ dòng Failed và tổng thực hoàn 40000 |
| Flush identity và commit | CustomerRepository.ExecuteInTransactionAsync/SaveChangesAsync tại 8b2e6a2 | Code hỗ trợ nhiều lần lưu trong một transaction; D08 chọn cách này, tài liệu ví cần được owner đồng bộ; chưa khẳng định test rollback đã pass |
| Phí phiên trước OrderPayment | Contract phiên tại 761333c mục 3–5 và ví tại 8b2e6a2 mục 5 | D09 chọn phí tích lũy theo giây và quyết toán trước debit món; phần mô tả trả trước trong ảnh chụp ví cần được owner đồng bộ khi triển khai |
| Schema đích so với base e14a362 | Order/OrderDetail/Payment và EF configuration | Đã phân biệt phần có sẵn/phần cần migration ở mục 10; chưa có API checkout, không coi đề xuất schema là đã triển khai |

### 11.2. Quyết định thiết kế đã chốt

**Ngày chốt: 08/11/2026. Căn cứ:** người dùng trực tiếp yêu cầu trong cuộc trò chuyện:
“bạn tự chốt theo cách hợp lý nhất”. AI lựa chọn các phương án dưới đây trong phạm vi SCRUM-11.
Đây là quyết định thiết kế theo ủy quyền của người dùng, không phải biên bản Minh/Trường/Phúc đã đồng ý.
Các owner là người nhận đầu vào và phối hợp triển khai; việc hoàn thành Task vẫn cần Trưởng nhóm duyệt cuối.
Nếu thay đổi lựa chọn dưới đây, ghi quyết định mới cùng lý do và cập nhật các hợp đồng bị ảnh hưởng.

**Bổ sung trực tiếp của Trưởng nhóm ngày 14/11/2026.** D02 được đổi sang thanh toán đủ trước nhận món và khóa hủy Cash đã thu. D12/D13 ghi actor System và xuất riêng hóa đơn tiền giờ/món ăn theo lựa chọn được xác nhận lại trong phiên làm việc. Trưởng nhóm trực tiếp hoàn thiện phần contract Sprint 1, Khang/Minh/Phúc/Trường triển khai các phần S2 theo phân công module.

| Mã | Quyết định đã chốt | Lý do | Owner phối hợp triển khai |
| --- | --- | --- | --- |
| D01 | Ví trừ ngay khi server chấp nhận tạo đơn và commit thành công, trước confirm từng món; OrderPayment âm, OrderRefund dương; gốc 0 đồng không có ledger/Payment | Khách biết số tiền đã trả khi đặt; đơn và tiền cùng rollback; phù hợp WalletLedger và demo hiện có | Khang + Minh |
| D02 | Quyết định 14/11/2026 chọn thanh toán đủ trước nhận món. Cash thu đủ khi món còn Pending rồi nhân viên confirm để làm. Cash đã có khoản thu Succeeded khóa hủy, quản lý xử lý ngoại lệ tại quầy. Wallet debit khi tạo, ZaloPay xác minh thu trước confirm, hủy Pending theo mục 7. Quyền và xử lý lặp theo mục 3/8 | Thu tiền ngay lúc đặt, kiểm soát quyền nhận món, thu tiền và hủy bằng dữ liệu server | Khang + Minh + Trường |
| D03 | Products.Stock là tồn khả dụng; tạo đơn ghi Sale âm/dòng, hủy/expired ghi Return dương một lần. Callback/confirm không trừ thêm; không phục hồi tồn khi thu muộn | Một cách ghi tồn cho ba phương thức, ngăn bán vượt và trả tồn lặp | Khang + Trường |
| D04 | Một voucher/đơn, chỉ tiền món; %/fixed, ngưỡng trước giảm, hiệu lực theo now server; làm tròn AwayFromZero, phân bổ đồng dư theo mục 6. Usage chiếm lượt khi tạo; partial không nhả, full cancel/expired nhả một lần | Khớp contract Trường, giữ lịch sử và số hoàn theo snapshot, không tính lại voucher sau hủy | Trường + Khang; Đại nhận rule dùng chung |
| D05 | Giữ chỗ nội bộ ZaloPay 15 phút từ lúc nhận đơn, không gia hạn. Xác nhận lần đầu dưới khóa tại/sau hạn hoặc sau hủy là thu muộn; ghi thực thu, giữ đơn đã hủy/expired, không cấp lại món. Worker hết hạn mỗi 15 giây và lúc startup | Ranh giới xử lý tái hiện được, không chiếm tồn vô hạn; cùng luật cho callback và worker | Khang + Minh + Trường |
| D06 | Hoàn về đúng phương thức đã trả. Thu muộn tạo Refund Pending chưa duyệt; Admin/Manager có payment.refund duyệt rồi worker mới gửi. Timeout giữ Pending; thất bại chắc chắn Failed nhưng còn nghĩa vụ xử lý; partial theo mục 7.2 | Truy được khoản thu gốc, không tự biến tiền gateway thành số dư ví hoặc báo hoàn khi tiền chưa về khách | Minh + Khang + Trường |
| D07 | Dùng GET danh sách/chi tiết Refund và POST approve/replacements ở mục 3/7.4. Thay thế chỉ ZaloPay Failed đủ điều kiện, giữ nguồn/amount/method, một con trực tiếp, khoản mới phải duyệt; không hủy lại món | Khép hai finding review, admin có đường thao tác và không tạo nhánh hoàn trùng | Khang + Minh; Trường nhận nguồn đối soát |
| D08 | Một DbContext/SQL transaction, một commit cuối; được flush identity trước khi lưu các phần còn lại. Khóa theo mục 8; actor khách/hệ thống để FK nhân viên nullable, staff lấy JWT; không giả nhân viên | Tạo được khóa theo OrderId thật mà vẫn rollback toàn bộ ví/đơn/tồn/lượt; tránh deadlock và nguồn actor sai | Khang + Minh + Trường; Đại nhận mapping actor |
| D09 | Phí phiên tích lũy theo giây: ceil(P*s/3600), trừ phần SessionCharge đã thu; worker 60 giây, khi đóng/phục hồi và trước OrderPayment. Quyết toán phí trước khi xét số dư món, thiếu tiền rollback request; không thu trả trước chu kỳ song song | Không để món tiêu phần tiền giờ đã phát sinh; tính lại tổng tránh trùng phí và sai số làm tròn | Phúc + Minh + Trường + Khang |
| D10 | Giữ DTO/route/error/version/idempotency của bản này; event thông báo sau commit, client GET lại. SQL tiền đích decimal(18,0), tỷ lệ DiscountValue decimal(18,2); giới hạn API vẫn 999_999_999 VND | Dùng chung một hợp đồng; giữ tỷ lệ lẻ, số nguyên JSON an toàn và chặn việc tự nâng giới hạn tiền khi đổi schema | Khang + Minh + Trường; Phúc nối event |
| D11 | Chọn thiết kế chứng từ 1.7 tại 761333c, Git path docs/contracts/inventory-invoice-voucher.md: Payments neo nguồn trước hóa đơn, InvoiceIssueRequests, ConfirmedAt/ProviderOccurredAt/PaymentTimeBasis. Giữ lineage/approval bổ sung ở bản này; schema base vẫn e14a362, nền code SCRUM-11 là 66f1a29 | Có khóa phát hành bền vững và mốc báo cáo rõ khi provider thiếu giờ thực hoàn; không thu lại khi nối hóa đơn | Trường + Minh + Khang; Phúc cung cấp nguồn phí |
| D12 | Quyết định 14/11/2026 chọn System tự quyết toán/đóng phiên khi hết tiền, lưu riêng người mở, quyền nội bộ và khóa/hash bền vững theo contract phiên mục 5.1 và chứng từ mục 6.3.2 | Truy đúng actor và nguồn thu qua retry/restart | Phúc + Minh + Trường |
| D13 | Quyết định 14/11/2026 chọn xuất riêng hóa đơn món và tiền giờ. IssueForOrders chỉ gồm dòng món, IssueForSession chỉ gồm phí giờ với orderIds rỗng. Mỗi nguồn lên chứng từ một lần | Đơn món được trả tiền khi đặt, tiền giờ được chốt chứng từ độc lập khi hết tiền | Trường + Khang + Phúc |

### 11.3. Phụ thuộc triển khai và duyệt cuối

Không còn lựa chọn thiết kế nào ở mục 11.2 phải chờ owner tự chọn lại. Những việc sau vẫn phải có
bằng chứng trước khi tuyên bố hoàn tất tích hợp; quyền chốt thiết kế không thay cho việc đã chạy hệ thống:

| Việc cần hoàn thành | Người/module phụ trách | Bằng chứng cần có |
| --- | --- | --- |
| Duyệt cuối SCRUM-11 bản 0.4 | Trưởng nhóm | Quyết định review thực tế; checklist Task vẫn chưa tick trong phiên này |
| Đồng bộ các giao điểm đã chọn | Trưởng nhóm hoàn thiện contract S1, các owner triển khai S2 theo module | Contract/interface của provider khớp D01–D13, phần triển khai được chứng minh bằng code/migration/kiểm thử tương ứng |
| Tích hợp code/migration đúng backend chung | Owner bảng, Trường điều phối | Chuỗi migration mới, source/DTO/DI dùng chung DbContext, không sửa migration đã áp hoặc giả dữ liệu actor |
| Cấu hình và kiểm chứng gateway | Minh | Sandbox/HTTPS/adapter thực tế, hạn provider, khả năng partial và kết quả query/hoàn; không coi lựa chọn TTL nội bộ là test sandbox |
| Chứng minh nhất quán và giao diện | Các task S2/S3 của owner | Các ca mục 12: rollback/retry/cạnh tranh, không thu/hoàn trùng, nguồn chứng từ và UI/API/SQL/realtime khớp |

S1-K-2 vẫn sử dụng đầu vào S1-M-1/S1-T-1; S1-M-2 nhận giao diện Order đã chọn ở đây.
Các phụ thuộc triển khai trên không trở lại thành yêu cầu chờ chốt chính sách của bản thiết kế này.

## 12. Tiêu chí kiểm chứng khi triển khai

Các ca dưới là yêu cầu bàn giao cho S2/S3, **chưa được chạy trong task tài liệu**.

| Ca | Kết quả cần kiểm tra ở API/SQL |
| --- | --- |
| Wallet hợp lệ / thiếu tiền / lỗi SaveChanges | Thành công có một đơn/debit/Sale/usage/Payment đúng nguồn; lỗi rollback toàn bộ, ví không âm |
| Ví đủ giá món nhưng thiếu sau phí phiên; retry cùng key; lỗi sau flush identity | Theo quyết định D09/mục 7.3: đơn bị từ chối và toàn request rollback, không tạo nguồn dở dang hoặc thu thêm phí từ replay; worker phiên quyết toán độc lập |
| Gửi cùng key tuần tự/đồng thời; mất response/restart | Cùng resource ID, không trừ ví/tồn/dùng lượt lần hai; payload khác 409; key khác vẫn không thu/hoàn trùng nguồn |
| Hai khách tranh tồn/lượt cuối | Chỉ một bên thành công; bên thua 409, không có ledger hoặc nguồn dở dang |
| Giá/tên/voucher sửa sau đặt; giả giá/tổng/status trong body | Snapshot và hoàn theo giá cũ; body ngoài DTO bị từ chối; không dùng giá browser |
| Voucher %/fixed, trần, ngưỡng, đồng dư, giảm 100% | Tổng giảm dòng bằng giảm đơn; không vượt subtotal; đơn 0 không tạo khoản tiền 0 |
| Khách khác/phiên khác; Waiter thu/hủy; Cashier hoàn; token cũ sau khóa | 404/403/409 theo contract; không ghi side effect; khách Locked vẫn nhận hoàn được duyệt |
| Yêu cầu hủy lặp, confirm đua hủy, version cũ | Khách không tự hoàn; chỉ một chuyển trạng thái, yêu cầu liên quan được đóng đúng; lỗi không ghi tồn/ví |
| Cash chưa thu, Waiter gọi trực tiếp confirm | 409 PaymentRequired, món tiếp tục Pending, SQL giữ tồn/sổ tiền |
| Cash nhân viên xác nhận cashReceived=true khi món Pending → nhận món | Thu đủ đúng một Payment Succeeded, response tăng version, confirm dùng version mới và order.status.update |
| Cash đã thu nhưng món còn Pending, khách/admin yêu cầu hủy | 409 CashCancellationLocked, khoản thu và trạng thái/tồn giữ nguyên, UI hướng dẫn liên hệ quản lý tại quầy |
| Cash thực nhận rồi mất response; SQL rollback hoặc hủy thắng trước lúc ghi thu | Tra/retry nguyên key/body, không thu lần hai. Thành công chỉ một Payment; nếu trả tiền ngoài Payment thì quản lý chốt hủy/version trước, xác nhận thực trả một lần và lưu phiếu/audit; request thu cũ đến muộn không ghi được tiền hoặc hồi sinh đơn |
| Cash hai dòng netAmount=0/1, chưa thu, hủy dòng netAmount=1 | PayableAmount=0, OriginalPayable=1 giữ nguyên, NotRequired; dòng 0 còn Pending nhận được, không tạo khoản tiền 0. Có thu/hoàn hoặc gateway chưa rõ thì không che bằng NotRequired |
| ZaloPay chưa xác minh hoặc redirect giả thành công → confirm | PaymentRequired, tiếp tục chờ xác minh; callback hợp lệ cập nhật Paid nhưng nhân viên vẫn phải nhận món |
| Hóa đơn món và hóa đơn tự đóng tiền giờ | Chứng từ mới mỗi loại nguồn riêng, orderIds rỗng cho phí giờ, các khoản đã thu chỉ được liên kết một lần |
| Hủy dòng đã giảm / hủy toàn bộ / hủy lặp | Hoàn netAmount một lần, Return một lần; partial không hoàn lượt, full hoàn đúng một lượt |
| Cash thu đủ khi Pending rồi mới confirm, thu lặp, đơn 0 | Chờ thu bị chặn confirm, thu đúng khoản còn lại một lần, đơn 0 dùng NotRequired |
| Gateway Pending, callback sai/lặp/TopUp nhầm Order | Không tự Paid từ redirect hoặc dữ liệu sai; callback hợp lệ ghi một lần; không mở transaction lúc chờ |
| Hết hạn đua callback, callback sau hủy, provider đã thu nhưng SQL rollback | Không giữ/trả tồn/lượt hai lần, không hồi sinh đơn; query giao dịch cũ để ghi nhận và đối soát khoản thực thu |
| P1: hết hạn → thu muộn → GET Refunds → duyệt → worker → retry | Admin đọc được đúng refundId/orderId/khoản thu gốc/amount/nguồn/trạng thái duyệt; expectedVersion từ DTO dùng được; approve chỉ đổi thông tin duyệt, worker xác minh hoàn một lần; replay trả cùng ID, không thêm khoản hoàn |
| Đọc/duyệt Refund sai quyền hoặc khác orderId | Customer/Cashier/Waiter nhận 403; Refund khác đơn 404; không lộ nguồn tiền hoặc tạo/duyệt/gửi hoàn; GET không đổi version |
| Payment Failed nhận callback thành công muộn / query khớp / lỗi audit | Callback đơn lẻ không sửa Failed; query khớp mới ghi Succeeded + audit một lần, không hồi sinh đơn; lỗi audit rollback SQL, retry query mã cũ |
| Hoàn gateway chưa rõ khả năng partial / timeout / bị từ chối dứt khoát | Chưa rõ điều kiện thì gửi một lần sau duyệt; timeout giữ Pending/hạn mức; xác minh thất bại và không còn attempt chưa rõ mới Failed/nhả giữ chỗ; vẫn hiện đúng refundUnresolvedAmount, không báo đã hoàn |
| Refund Failed cần xử lý tiếp / worker restart / query trái kết luận cũ | Worker không tự gửi lại Failed; phương án mới cần duyệt và kiểm tra hạn mức/nguồn; kết quả mâu thuẫn chuyển đối soát có duyệt, không bỏ qua thực hoàn |
| P2: Refund 8101 Failed 40000 → tạo 8102 → duyệt → hoàn thành → replay | 8101 vẫn Failed, 8102 có ReplacementOfRefundId=8101/cùng nguồn/amount/method; chỉ 8102 đã duyệt được gửi; tổng Succeeded Refund=40000, unresolved=0; món/đơn/tồn/lượt không đổi do thay thế |
| Hai request thay thế cùng tiền nhiệm, khác key; nguồn đã hoàn/đang giữ hoặc attempt cũ chưa rõ | Tối đa một Refund mới; request thua 409; cùng key replay cùng ID, khác payload 409; không vượt hạn mức khoản thu/nguồn hủy, không claim/gửi trước duyệt hoặc gửi lại tiền nhiệm Failed |
| Hóa đơn gộp trước/sau thu/hủy/hoàn | Chỉ nối nguồn đúng đơn, không debit/thu lại, số thu/hoàn khớp; không cộng đôi ledger + Payment |
| Event và reconnect | Phát sau commit đúng người nhận; GET thấy trạng thái đã lưu, không fallback localStorage |

Tự kiểm tra thiết kế dùng [REVIEW_GUIDE](../../codex/REVIEW_GUIDE.md). Khi bàn giao S1-K-2 cần
diff/tài liệu, kiểm tra link/JSON/nhất quán và căn cứ chốt tại mục 11.2 cùng quyết định duyệt cuối; không yêu cầu test SQL thay cho quyết định thiết kế,
cũng không dùng kiểm tra tài liệu để tuyên bố các ca API/SQL/gateway trên đã pass.
