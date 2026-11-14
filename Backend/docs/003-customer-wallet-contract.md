# SCRUM-5 / SCRUM-21 — Hồ sơ, ví và nạp tại quầy

Ngày cập nhật: 14/11/2026. Query: `SCRUM-5` / `S1-M-1` — `Implement-Customer-Wallet-Queries`.
Writer và nạp quầy: `SCRUM-21` / `S2-M-1` — `Implement-Wallet-Ledger-Counter-Topups`.
Owner: Minh; Phúc/Khang/Trường tích hợp nguồn phiên/đơn/chứng từ. Reviewer cuối: **Trưởng nhóm**.

## 1. Phạm vi và căn cứ

SCRUM-5 triển khai tạo/đọc hồ sơ, đọc số dư và lịch sử ví qua API/SQL trên CyberManagement,
CustomerId int. Base PR #17 là `93cf457`, đã tích hợp migration SCRUM-13 mở rộng nền 35 thành 38 bảng
và tiền VND `decimal(18,0)`. Không nhập backend CyberCafe/ID long từ bản cũ.
SCRUM-5 không thay schema/migration hoặc thực thi nạp/chi/hoàn ví. SCRUM-21 bổ sung writer và hai
migration mới trên base tích hợp `790d9c1`, giữ query và các quyết định liên module đã có.

Nguồn thiết kế liên quan: [identity và quyền](001-monorepo.md),
[phiên/tiền giờ](002-computer-session-contract.md), [checkout](004-order-checkout-contract.md),
[kho/chứng từ/voucher](../../docs/contracts/inventory-invoice-voucher.md).
Quy tắc checkout D01/D09 và đối soát D11 ngày 08/11 được giữ nguyên.

Người dùng đã chọn ngày 08/11/2026: **nạp tại quầy thu tiền xong mới cộng ví, không nạp nợ**.
SCRUM-21 hiện thực shared writer và luồng nạp tiền mặt tại mục 4, 6. Các điểm gọi của phiên/đơn tại
mục 5 vẫn là trách nhiệm tích hợp của owner tương ứng; có writer không chứng minh các luồng đó đã hoàn tất.
Theo bổ sung của Trưởng nhóm ngày 09/11/2026 do người dùng cung cấp, khách gửi yêu cầu nạp ở tám mức,
admin thu đủ tiền mặt rồi duyệt và cộng tiền nạp cùng tiền tặng. ZaloPay giữ riêng ở `S2-M-2`/`S2-M-3`.
Không ghi nhận Phúc/Khang/Trường đã phản hồi đồng thuận hoặc Trưởng nhóm đã nghiệm thu.

## 2. API đã triển khai

Các path dưới đây có tiền tố `/api`. Request yêu cầu JWT; không có token trả 401, sai quyền trả 403.

| Method / path | Quyền / danh tính | Response |
| --- | --- | --- |
| GET `/admin/customers?page=1&pageSize=20&search=` | `customer.view` | 200 CustomerPage |
| POST `/admin/customers` | `customer.create` | 201 CustomerProfile, Location trỏ tài nguyên mới |
| GET `/admin/customers/{customerId}` | `customer.view` | 200 CustomerProfile |
| GET `/admin/customers/{customerId}/wallet?page=1&pageSize=20` | `wallet.view` | 200 CustomerWallet |
| GET `/customers/me` | role Customer, claim hợp lệ, khách còn Active trong SQL | 200 CustomerProfile |
| GET `/customers/me/wallet?page=1&pageSize=20` | như trên | 200 CustomerWallet |

Theo catalog hiện tại: Admin/Manager/Cashier tạo khách và xem ví; Waiter chỉ xem hồ sơ;
Technician không có quyền khách/ví. API thực thi permission, UI chỉ hỗ trợ hiển thị theo quyền.
Endpoint `/me` lấy `customer_id` từ JWT đã xác minh, yêu cầu `account_type=Customer`, `sub` khớp ID dương;
không nhận ID người khác từ browser. Token nhân viên không dùng được `/me` của khách.
Client gọi đường admin để đọc tài khoản khác nhận 403. Hồ sơ đọc từ SQL, không lấy tên/số dư cũ trong JWT.

```text
CreateCustomer = { fullName, phone, email?, password }
CustomerProfile = { customerId, fullName, phone, email, status, createdAtUtc }
CustomerPage = { items: CustomerProfile[], page, pageSize, totalCount }
CustomerWallet = { customerId, balance, transactions: WalletEntry[], page, pageSize, totalCount }
WalletEntry = { transactionId, amount, balanceAfter, type, description, createdAtUtc }
```

- Tạo khách: trim tên 1..150 ký tự; phone gồm 8..20 chữ số ASCII, có thể có `+` đầu chuỗi,
  tổng chiều dài kể cả `+` tối đa 20; email tùy chọn, trim, hợp lệ và tối đa 150 ký tự.
  Mật khẩu bắt buộc 12..128 ký tự, được hash bằng hasher auth hiện có; không mặc định bằng phone.
- Server tạo `Active`, số dư 0 và giờ UTC; request có trường ngoài contract (balance, status, ID...)
  trả 400. DTO trả về không chứa mật khẩu/hash hoặc số dư trong hồ sơ; số dư chỉ có qua endpoint ví.
- Phone duy nhất theo index SQL; trùng sau trim hoặc cạnh tranh tạo cùng phone trả 409.
  Không tự chuẩn hóa `+84` thành `0`: hai định dạng chưa được coi là cùng phone trong contract auth.
- `page >= 1`, `1 <= pageSize <= 100`; offset không vượt int, search trim tối đa 150 ký tự.
  Khách sắp CustomerId giảm dần; ledger sắp CreatedAt giảm dần rồi TransactionId giảm dần.
- ID/payload/phân trang sai trả 400; không có khách trả 404; lỗi SQL/hệ thống trả 500 ProblemDetails
  không lộ thông tin nội bộ. Tài khoản khách đã Locked bị từ chối đọc `/me` với 403 kể cả JWT còn hạn.
- Khách chưa có ledger trả `transactions=[]`, `totalCount=0`, balance lấy nguyên từ SQL.
  Tiền VND đọc nguyên giá trị đã lưu ở schema `decimal(18,0)` của base hiện tại;
  query không làm tròn lại hoặc suy balance từ riêng trang ledger.
- Query ví đọc số dư/count/trang ledger trong một transaction Serializable SQL; InMemory chỉ phục vụ
  kiểm tra hành vi, không thay thế bằng chứng isolation. Query không ghi tiền hoặc tạo ledger.

## 3. Luồng UI đã chuyển

Admin Khách hàng dùng API danh sách/tìm kiếm/phân trang, tạo khách và xem hồ sơ/ví theo quyền.
Client Tài khoản đọc hồ sơ `/customers/me`; Ví đọc snapshot `/customers/me/wallet`.
Loading/error/empty và tải lại hiển thị riêng. API lỗi không dùng số dư/hồ sơ localStorage thay thế.
SCRUM-21 thêm form nạp tiền mặt tại `/client/wallet` và hàng chờ thu/duyệt tại `/admin/topups`.
Cả hai phía hiển thị tiền mặt, tiền tặng và tổng cộng ví từ API. Request Pending không làm tăng số dư;
admin phải xác nhận đã thu đủ tiền. Duyệt xong, lịch sử/số dư đọc lại SQL qua API.
Sửa hồ sơ/mật khẩu và nghiệp vụ hoàn/phiên/đơn chưa nối trong các trang này vẫn theo backlog.

## 4. Shared credit / debit / refund đã triển khai

`Application/Wallets/IWalletLedgerWriter.cs` là điểm gọi nội bộ; implementation là
`Infrastructure/Wallets/WalletLedgerWriter.cs`. Không mở endpoint cộng/trừ ví tùy ý cho browser.
Caller xác minh quyền, nguồn và số tiền ở server trước khi gọi writer.

```csharp
Task<Customer> LockCustomerAsync(int customerId, CancellationToken cancellationToken);
Task<WalletMutationResult> RecordAsync(WalletMutation mutation, CancellationToken cancellationToken);

// Namespace CyberManagement.Application.Wallets
WalletMutation(CustomerId, Kind, Amount, Type, SourceType, SourceId, OperationKey,
               Actor, OccurredAtUtc, OriginalDebitId = null, Description = null);
// Kind: WalletMutationKind.Credit | Debit | Refund; Amount luôn dương.
// Actor: new WalletActor("Staff", employeeId) hoặc new WalletActor("System").
// Kết quả: WalletMutationResult(Entry: WalletTransaction, Replayed: bool).
```

Caller dùng cùng scoped `ApplicationDbContext` và transaction SQL Server đang sửa nguồn nghiệp vụ.
Writer từ chối thiếu transaction hoặc provider khác SQL Server; InMemory không thay thế bảo đảm tiền.
Record không tự SaveChanges/commit, không mở transaction độc lập, không gọi HTTP/provider.
Caller lưu nguồn, số dư, ledger và Payment/Refund đối soát cùng transaction rồi commit một lần.
Entry mới chỉ có TransactionId identity sau khi caller SaveChanges; caller dùng navigation/entry để
liên kết nguồn trước flush, chỉ trả transactionId ra response sau commit. Replay trả entry đã lưu có ID.
Nếu lỗi bất kỳ bước nào thì rollback toàn bộ; sau commit mới phát event/refetch theo task realtime.
Tạo hóa đơn sau đó chỉ liên kết các khoản thu, không debit lại.

| Nguồn | Kind / Type / dấu Amount ledger | SourceType / SourceId | OperationKey bền vững |
| --- | --- | --- | --- |
| Nạp quầy đã thu | Credit / `TopUp` / dương | `CounterTopUp` / RequestId | `CounterTopUp:{requestId}` |
| Điểm nối nạp ZaloPay đã xác minh | Credit / `TopUp` / dương | `ZaloPayTopUp` / merchantTransactionId | `ZaloPayTopUp:{merchantTransactionId}` |
| Điểm nối phí phiên | Debit / `GamingCharge` / âm | `SessionCharge` / ID hoặc khóa kỳ thu duy nhất | `SessionCharge:{sessionId}:{chargedThroughUtcTicks}` |
| Điểm nối đơn ví | Debit / `OrderPayment` / âm | `Order` / OrderId | `OrderPayment:{orderId}` |
| Điểm nối hoàn dòng đơn ví | Refund / `OrderRefund` / dương | `OrderDetail` / OrderId:OrderDetailId | `OrderRefund:{orderId}:{orderDetailId}` |

Mỗi operationKey tối đa 100 ký tự, SourceType tối đa 50, SourceId tối đa 100; các trường này không rỗng
hoặc có khoảng trắng đầu/cuối. Key/source/type dùng collation BIN2. Unique index bảo vệ OperationKey
và bộ `(SourceType, SourceId, Type)` ngay trong DB, không chỉ cache/process lock. Payload replay gồm
customer, kind/type/nguồn, tiền, debit gốc, actor và description. Retry cùng key và payload trả kết quả đã commit,
không thu/cộng hay quyết toán lại. Cùng key khác payload trả 409; khác key cùng một nguồn đã thu/hoàn
cũng bị ràng buộc nguồn duy nhất chặn. Writer còn nhận lại entry Added khi cùng key được gọi trước flush,
không thêm entry hoặc tăng ví lần nữa. OccurredAtUtc của lần retry không thay thời điểm đã lưu.
Description chỉ là diễn giải, không dùng làm khóa hoặc FK; giữ nguyên description khi retry cùng operation.

Quy tắc tiền:

- `0 < amountPositive <= 999_999_999` VND theo D10, khoản mới là số nguyên do server tính. Ledger Debit âm, Credit/Refund dương;
  BalanceAfter là số dư sau cùng thao tác. Không tạo ledger/Payment tiền 0; nguồn 0 đồng vẫn có audit/trạng thái.
- Debit yêu cầu khách Active và số dư sau quyết toán đủ chi; không âm, không cho thấu chi.
  Từ chối thiếu tiền trước commit; không ghi một nửa nguồn/ledger. Credit nạp mới yêu cầu khách Active.
- Hoàn ví chỉ cho debit đã commit của cùng Customer, Kind=Debit, Type=OrderPayment, SourceType=Order;
  debit vừa tạo hoặc vừa flush trong chính transaction hiện tại chưa đủ điều kiện hoàn.
  Tổng ledger hoàn đã lưu, refund Added trong cùng unit of work và các Payment hoàn Wallet Pending
  không vượt debit gốc. Khi chuyển đúng reservation cùng OperationKey thành refund, số tiền phải khớp
  và reservation đó không bị tính hai lần. Caller xác minh phần hủy hợp lệ và liên kết Payment/nguồn.
  Khách Locked vẫn được nhận hoàn; retry không hoàn hai lần.
  Cash/ZaloPay hoàn về phương thức gốc theo contract checkout, không tự đổi thành hoàn ví.
- Base hiện tại đã chuyển tiền sang `decimal(18,0)` theo D10/SCRUM-13; fixture SQL dùng số nguyên VND.
  Dữ liệu lẻ của schema lịch sử phải được đối soát trước migration, không âm thầm làm tròn hoặc nâng
  hạn mức API 999_999_999 theo precision mới. Công thức ngân sách phí phiên giữ theo contract phiên.
- Khóa SQL bắt đầu tại Customer, rồi Computer → GamingSession/SessionCharge khi liên quan,
  tiếp đến nguồn đơn/chi tiết và ledger/Payment theo thứ tự thống nhất của contract chứng từ.
  `LockCustomerAsync` lấy `UPDLOCK,HOLDLOCK` trên Customer trước khi đọc số dư để ghi.
  Mọi writer ví dùng cùng thứ tự/khóa trên DB để chạy nhiều API instance an toàn.
  Sau rollback, bỏ scope/DbContext hoặc xóa đúng thay đổi đã rollback trước khi thử lại; không trả entry
  chưa commit như biên nhận thành công. Hạn mức số dư là precision `decimal(18,0)`, không cho tràn.

### 4.1. Ví dụ tích hợp cùng transaction

Các mutation dưới đây minh họa điểm gọi nội bộ. ID và số tiền phải lấy từ nguồn đã xác minh ở server;
caller chọn một mutation phù hợp, không lần lượt chạy cả bốn ví dụ trên cùng một nguồn.

```csharp
var credit = new WalletMutation(customerId, WalletMutationKind.Credit,
    request.TotalAmount, "TopUp", "CounterTopUp", request.RequestId.ToString(),
    $"CounterTopUp:{request.RequestId}", new WalletActor("Staff", employeeId), now);
var debit = new WalletMutation(customerId, WalletMutationKind.Debit,
    orderNetAmount, "OrderPayment", "Order", orderId.ToString(),
    $"OrderPayment:{orderId}", new WalletActor("Staff", employeeId), now);
var refund = new WalletMutation(customerId, WalletMutationKind.Refund,
    refundableNetAmount, "OrderRefund", "OrderDetail", $"{orderId}:{orderDetailId}",
    $"OrderRefund:{orderId}:{orderDetailId}", new WalletActor("Staff", employeeId), now,
    OriginalDebitId: committedDebitId);
var automaticCharge = new WalletMutation(customerId, WalletMutationKind.Debit,
    chargeDelta, "GamingCharge", "SessionCharge", chargeSourceId,
    chargeOperationKey, new WalletActor("System"), now);

await using var transaction = await db.Database.BeginTransactionAsync(ct);
await wallet.LockCustomerAsync(customerId, ct);
// Debit phiên/đơn: caller quyết toán phiên trước, theo mục 5, trong cùng transaction.
var result = await wallet.RecordAsync(debit, ct);
// Caller lưu nguồn đã xác minh và Payment liên kết bằng navigation result.Entry.
// Kiểm tra replay nguồn trước khi tạo đơn/Payment để không tạo chứng từ mới khi replay.
await db.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);
// Chỉ sau commit mới thông báo; phía nhận đọc lại API, không tự cộng tiền từ event.
await realtime.PublishToAccountAsync(RealtimeAccountType.Customer, customerId,
    new RealtimeEvent(Guid.NewGuid(), "WalletChanged", customerId, now), CancellationToken.None);
```

`WalletActor("System")` chỉ được tạo trong ngữ cảnh worker/settlement nội bộ đã được server cho phép;
không nhận actor hoặc employeeId do browser tự khai. Người mở phiên vẫn lưu riêng trên nguồn phiên.
Writer không tự phát realtime vì chưa biết outer transaction của caller đã commit hay chưa.

## 5. Thời điểm thu tiền và điểm gọi chung

**Quyết định 14/11/2026.** Trưởng nhóm trực tiếp hoàn thiện contract còn dang dở của Sprint 1, chốt giao điểm tự quyết toán, đóng phiên và phát hành hóa đơn khi hết tiền. Worker dùng actor System từ ngữ cảnh nội bộ, cùng transaction và khóa với nguồn phiên. Người mở được lưu riêng, actor System và metadata quyết toán được audit theo [contract phiên mục 5.1](002-computer-session-contract.md#51-quyết-định-tự-đóng-phiên-và-người-thực-hiện--14112026). Minh triển khai ví/ledger/Payment ở S2-M-1, phối hợp Phúc ở S2-P-1 và Trường ở S2-T-2.

Phúc: phí `ceil(priceSnapshotPerHour * elapsedSeconds / 3600)` tính tích lũy theo tổng thời gian;
worker 60 giây, quyết toán khi đóng/restart và trước debit khác. Chỉ thu chênh lệch với phí đã commit;
không thu trả trước chu kỳ song song. Mỗi lần thu ghi SessionCharge/ledger/Payment Succeeded nguyên tử.

Payment mỗi lần thu neo SessionChargeId và WalletTransactionId, có InvoiceId nullable trước phát hành. Quyết định 14/11/2026 chọn hóa đơn tiền giờ chỉ chứa các phí chưa có chứng từ của phiên, còn đơn món có hóa đơn riêng. Phát hành liên kết các khoản thu đã commit theo nguồn, các trường hợp retry/restart tra lại cùng khóa.

Khang: kiểm tra replay đơn trước; khóa khách/phiên và gọi
`SessionSettlement.BeforeWalletDebit(customerId, gamingSessionId?, nowUtc)` như D09, rồi mới xét số dư
và debit `OrderPayment` khi tạo đơn ví. Gồm trường hợp đơn quầy không gắn session nhưng khách có phiên mở.
Thiếu tiền thì rollback cả request, kể cả phí vừa tính trong request; worker phiên quyết toán độc lập lại.
Hủy dòng Pending hoàn netAmount snapshot bằng `OrderRefund`, chỉ một lần theo nguồn dòng.

Trường: mỗi debit/credit dịch vụ liên kết Payment/Refund qua WalletTransactionId duy nhất,
nguồn Order/SessionCharge và OperationKey; InvoiceId có thể null trước phát hành theo D11.
Báo cáo lấy ledger ví cho thu/hoàn dịch vụ, không cộng lại Payment ví; nạp ví không phải doanh thu.
Quyết định 14/11/2026 yêu cầu đơn món được thanh toán đủ trước khi nhân viên nhận để làm. Cash được nhân viên có payment.create xác nhận thực nhận khi dòng còn Pending. Wallet ghi debit khi tạo đơn, ZaloPay xác minh thu qua callback/query, sau đó nhân viên có order.status.update mới confirm món. Luồng gateway giữ/thu/hoàn theo [contract checkout mục 5.1](004-order-checkout-contract.md#51-thanh-toán-đủ-trước-khi-nhân-viên-nhận-món--14112026).

Đơn Cash đã có khoản thu Succeeded khóa yêu cầu hủy của khách và hủy từ admin, kể cả dòng Pending. Quản lý xử lý ngoại lệ tại quầy qua nghiệp vụ tiền có audit, kiểm tra payment.refund và bằng chứng thực hoàn theo contract checkout mục 7.1.

## 6. SCRUM-21 — Yêu cầu nạp tại quầy và tiền tặng

Nhân viên có `wallet.topup` xác nhận đã nhận tiền trước khi server ghi credit/ledger trong cùng transaction.
Request chưa thu không đổi balance; không dùng trạng thái demo `CreditedUnpaid` như khoản nạp thành công.
Nếu API mất phản hồi sau commit, tra/retry cùng khóa để lấy kết quả cũ. Nếu SQL rollback sau khi đã nhận
tiền ngoài hệ thống, giữ phiếu/khóa tiếp nhận và đối soát hoặc hoàn tiền có bằng chứng; không thu khách lần hai.
Không nhập dữ liệu demo hoặc tự tạo ledger để hợp thức hóa số dư cũ trong SCRUM-5.

### 6.1. Policy đã chốt và snapshot

| Tiền mặt khách nộp (Amount) | Tiền tặng (BonusAmount) | Tổng cộng ví (TotalAmount) |
| ---: | ---: | ---: |
| 10.000 | 0 | 10.000 |
| 20.000 | 0 | 20.000 |
| 30.000 | 0 | 30.000 |
| 50.000 | 5.000 | 55.000 |
| 100.000 | 15.000 | 115.000 |
| 200.000 | 35.000 | 235.000 |
| 300.000 | 50.000 | 350.000 |
| 500.000 | 100.000 | 600.000 |

API chỉ nhận Amount thuộc bảng, tính và lưu BonusAmount/TotalAmount lúc tạo yêu cầu.
Browser không được gửi tiền tặng, tổng, trạng thái, customerId hoặc actor; trường ngoài DTO trả 400.
Admin thu đúng Amount, tiền tặng không làm tăng tiền mặt phải thu. Duyệt ghi **một ledger TopUp**
có Amount bằng TotalAmount, đồng thời tăng Customer.WalletBalance và chuyển request sang Approved.
Khoản nạp không tạo hóa đơn/Payment dịch vụ và không phải doanh thu.

`CounterTopUpRequests.RequestId` là ID nghiệp vụ do SQL cấp; `(CustomerId, RequestKey)` duy nhất,
RequestKey lấy từ header UUID `Idempotency-Key`. Index lọc bảo đảm chỉ một Pending/Customer.
Gửi lại cùng key và Amount trả snapshot cũ, kể cả Approved hoặc bảng khuyến mãi đã thay đổi;
cùng key khác Amount trả 409. Key mới khi còn Pending trả 409. Approved không được duyệt lại thành
một lượt cộng mới; duyệt lặp/đồng thời trả biên nhận cũ. Lịch sử giữ actor, giờ thu, tiền mặt thực nhận,
WalletTransactionId và BalanceAfter để đối soát.

### 6.2. API nạp tiền mặt

| Method / path đầy đủ | Quyền | Body / kết quả |
| --- | --- | --- |
| GET `/api/customers/me/wallet/counter-topups/options` | Customer Active chính chủ | 200 mảng `{amount, bonusAmount, totalAmount}` |
| GET `/api/customers/me/wallet/counter-topups?page=1&pageSize=20` | Customer Active chính chủ | 200 CounterTopUpPage |
| POST `/api/customers/me/wallet/counter-topups` | Customer Active chính chủ | `{amount}` và Idempotency-Key UUID; 201 mới, 200 replay |
| GET `/api/admin/topups?status=Pending&page=1&pageSize=20` | `wallet.topup` | 200 CounterTopUpPage; status Pending/Approved/All |
| POST `/api/admin/topups/{requestId}/approve` | `wallet.topup`, nhân viên Active | `{cashReceived:true,cashAmount:50000}`; 200 CounterTopUp |

`cashReceived` phải true và `cashAmount` phải bằng Amount đã lưu, không phải TotalAmount.
Quyền mặc định nạp quầy: Admin/Manager/Cashier; Waiter/Technician và khách bị từ chối API admin.
Các endpoint `/me` lấy CustomerId từ JWT hợp lệ và kiểm tra trạng thái SQL; không nhận ID trong body.
Thiếu đăng nhập 401; sai quyền hoặc khách Locked dùng endpoint `/me` nhận 403; duyệt nạp mới cho
khách không Active cũng trả 403. Approved đã ghi nhận vẫn được replay sau khi khách bị khóa, giữ
nguyên biên nhận. Payload/key/phân trang sai 400; không có request 404; xung đột nghiệp vụ/thiếu tiền
trả 409. SQL rollback không để lại credit một nửa.

```text
CounterTopUp = { requestId, customerId, customerName, customerPhone,
  amount, bonusAmount, totalAmount, status, createdAtUtc, approvedAtUtc?,
  approvedByEmployeeId?, transactionId?, balanceAfter? }
CounterTopUpPage = { items: CounterTopUp[], page, pageSize, totalCount }
```

### 6.3. Migration và realtime

`20261114112809_WalletLedgerCounterTopUps` thêm bảng nguồn CounterTopUpRequests và metadata
key/kind/source/refund/actor trên WalletTransactions cùng FK, CHECK và unique index.
Metadata nullable giữ ledger lịch sử nguyên giá trị, không dựng lại nguồn hoặc biên nhận giả.
`20261114112818_CounterTopUpBonuses` bổ sung BonusAmount/TotalAmount: Pending được tính lại theo
bảng tiền tặng; Approved cũ giữ BonusAmount=0 và TotalAmount=Amount đã ghi nhận. Không đổi ledger,
số dư, cash receipt hoặc retry của Approved. Đây là hai migration mới; không sửa năm migration base.
Sau nâng cấp, model có 39 bảng và bảy migration; không áp chuỗi InitialCreate này vào DB 19 bảng cũ.
Down migration tiền tặng từ chối nếu còn snapshot BonusAmount khác 0; Down migration writer từ chối
nếu còn request nạp hoặc ledger có OperationKey. Không dùng downgrade để xóa biên nhận/audit tiền.

`CounterTopUpService.PublishAsync` chạy sau repository commit: CounterTopUpChanged tới quyền
wallet.topup và đúng Customer; sau duyệt thêm WalletChanged tới Customer và quyền wallet.view.
UI khách/admin nhận event hoặc reconnect sẽ đọc lại API/SQL; event không chứa lệnh cộng tiền.
Ví trong hồ sơ admin và lịch sử ví cũng reload khi WalletChanged. Không phát event thành công trước commit hoặc
cho request rollback; thiếu event có thể phục hồi bằng reconnect/tải lại.

## 7. Cách chạy và kiểm chứng

### 7.1. Demo nạp tiền mặt trên DB riêng

Chạy từ root của checkout SCRUM-21 có các file trong hợp đồng này:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-counter-topup.ps1
```

Script chuẩn bị SQL thử riêng và chạy API `http://127.0.0.1:5094`, frontend
`http://127.0.0.1:5176`. Database demo được giữ để tải lại vẫn còn request/ledger.
Tài khoản **fixture cục bộ**: khách `0900000021` / `CounterCustomer-2026!`, admin
`admin` / `CounterAdmin-2026!`. Không dùng mật khẩu này cho môi trường chung.
Mở `/client/auth`, gửi mức 50.000: thấy Pending với tiền tặng 5.000/tổng 55.000 nhưng số dư chưa đổi.
Đăng nhập nhân viên ở `/admin/auth`, mở `/admin/topups`, xác nhận đã thu 50.000 tiền mặt rồi duyệt.
Ví/hồ sơ/lịch sử phải đọc ra tăng đúng 55.000 và một ledger; tải lại hoặc duyệt lại không tăng thêm.
ZaloPay sandbox chạy theo task riêng, không qua form nạp tiền mặt này.

### 7.2. Kiểm thử có thể tái hiện

Kiểm tra HTTP/persistence tại `Backend/tests/CyberManagement.API.Checks/CustomerChecks.cs` bao gồm
ma trận quyền, tạo/đọc/reload, validation, trùng phone/cạnh tranh SQL, own-profile/ledger, pagination,
Locked sau login và không lộ credential. Browser SQL có runner `frontend/tests/run-customers-live.mjs`.
Mỗi runner SQL chỉ tạo/xóa database thử tên ngẫu nhiên riêng; không migrate DB đang dùng của người dùng.

```powershell
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks
$env:AUTH_CHECKS_SQL_CONNECTION = 'Server=127.0.0.1\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks -- --wallet-only
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks -- --wallet-migration-checks
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks
.\scripts\npm-local.cmd --prefix frontend run build
.\scripts\npm-local.cmd --prefix frontend run test:customer-wallet
.\scripts\npm-local.cmd --prefix frontend run test:counter-topups
.\scripts\npm-local.cmd --prefix frontend run test:counter-topups:render
node --test frontend/tests/computer-proxy.test.mjs
node frontend/tests/run-customers-live.mjs
Remove-Item Env:AUTH_CHECKS_SQL_CONNECTION
```

`--wallet-only` yêu cầu SQL, chạy auth, nạp quầy và shared writer. Các ca ở
CounterTopUpChecks/WalletLedgerChecks bao gồm tám mức/tiền tặng, quyền/sở hữu, key/payload,
chỉ một Pending, approve/retry/cạnh tranh, rollback, debit chống âm, refund chống vượt và actor System.
`--wallet-migration-checks` kiểm tra nâng cấp Pending/Approved qua hai migration trên DB thử riêng.
InMemory bỏ qua ca ghi tiền; kết quả InMemory không chứng minh khóa/transaction SQL.

### 7.3. Bằng chứng kiểm tra ngày 14/11/2026

Kết quả chạy trên checkout SCRUM-21, SQL Server Express `127.0.0.1\SQLEXPRESS` và DB thử riêng:

| Phạm vi | Kết quả | Giới hạn |
| --- | --- | --- |
| API SQL đầy đủ | 705 checks pass | Có hồi quy auth/máy/catalog/khách/realtime và tiền; không phải 705 ca mới của SCRUM-21 |
| API SQL `--wallet-only` | 422 checks pass | Gồm auth, 77 fixture ZaloPay nội bộ, nạp quầy và shared writer |
| SQL `--wallet-migration-checks` | 12 checks pass | Nâng cấp Pending/Approved, giữ snapshot/retry cũ và constraint nguồn nạp |
| API InMemory | 402 checks pass | Bỏ qua counter/shared writer SQL; không chứng minh transaction tiền |
| Inventory SQL | 62 checks pass | Đối chiếu schema hiện tại 39 bảng/bảy migration và luồng đọc kho |
| Frontend API | 28 tests pass | 8 ca nạp quầy, 6 khách/ví, 11 auth và 3 catalog |
| Frontend render | Pass | Ba số tiền, escape danh tính và checkbox thu tiền bắt buộc; không thay kiểm tra thao tác browser |
| Vite proxy | 3 tests pass | Dev/preview chuyển tiếp cả API nạp quầy và WebSocket |
| Build | .NET và frontend pass | .NET 0 warning/0 error; frontend có cảnh báo comment annotation từ SignalR có sẵn |

Bảng trên không xác nhận thanh toán ZaloPay, worker/checkout đã tích hợp, kiểm browser mới hoặc
realtime hai máy LAN. Các lệnh ở mục 7.2 dùng để tái hiện đúng phạm vi, không gộp số fixture thành
giao dịch provider thật. Kiểm realtime hai trình duyệt trên một máy không thay bằng chứng hai máy
LAN hoặc mục tiêu dưới hai giây. SCRUM-21 không hiện thực worker/đóng phiên, đặt/hủy đơn hay gateway;
các owner phải nối shared writer/Payment/settlement đúng transaction và kiểm thử luồng của họ.
Hoàn thành riêng nạp quầy không đồng nghĩa các giao điểm credit/debit/refund khác đã được nghiệm thu.
Giữ checklist chưa tick cho tới khi đủ bằng chứng và **Trưởng nhóm** quyết định duyệt/Done.
