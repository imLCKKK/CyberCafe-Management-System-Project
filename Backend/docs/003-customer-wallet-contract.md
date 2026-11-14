# SCRUM-5 — Customer profiles and wallet queries

Ngày cập nhật: 08/11/2026. Task: `SCRUM-5` / `S1-M-1` — `Implement-Customer-Wallet-Queries`.
Owner: Minh; Phúc/Khang/Trường tích hợp nguồn phiên/đơn/chứng từ. Reviewer cuối: **Trưởng nhóm**.

## 1. Phạm vi và căn cứ

SCRUM-5 triển khai tạo/đọc hồ sơ, đọc số dư và lịch sử ví qua API/SQL trên CyberManagement,
CustomerId int. Base PR #17 là `93cf457`, đã tích hợp migration SCRUM-13 mở rộng nền 35 thành 38 bảng
và tiền VND `decimal(18,0)`. Không nhập backend CyberCafe/ID long từ bản cũ.
Không thay schema/migration hoặc thực thi nạp/chi/hoàn ví trong task này; writer ví thuộc `S2-M-1`.

Nguồn thiết kế liên quan: [identity và quyền](001-monorepo.md),
[phiên/tiền giờ](002-computer-session-contract.md), [checkout](004-order-checkout-contract.md),
[kho/chứng từ/voucher](../../docs/contracts/inventory-invoice-voucher.md).
Quy tắc checkout D01/D09 và đối soát D11 ngày 08/11 được giữ nguyên.

Người dùng đã chọn ngày 08/11/2026: **nạp tại quầy thu tiền xong mới cộng ví, không nạp nợ**.
Các điểm ghi ví ở mục 4–6 là contract bàn giao cho S2, chưa phải bảo đảm đã được code thực thi.
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
Các thao tác sửa hồ sơ/mật khẩu và nạp/hoàn demo không tiếp tục chạy trên các trang đã chuyển;
API tương ứng và các luồng còn demo ngoài task được bàn giao theo backlog.

## 4. Contract credit / debit / refund cho S2

Đây là giao diện nghiệp vụ **dự kiến**, không phải endpoint hoặc service writer đang chạy.
Caller tạo số tiền từ nguồn đã được server xác minh; browser không được gọi một API cộng/trừ ví chung.

```text
Record(customerId, kind: Credit|Debit|Refund, amountPositive,
       type, sourceType, sourceId, operationKey, actor, nowUtc,
       originalDebitId? [Refund])
  -> { entry: trackedWalletEntry, balanceAfter, replayed }
```

Caller truyền cùng unit of work/DbContext và transaction đang dùng để sửa nguồn nghiệp vụ.
Record không tự SaveChanges/commit, không mở transaction độc lập, không gọi HTTP/provider.
Caller lưu nguồn, số dư, ledger và Payment/Refund đối soát cùng transaction rồi commit một lần.
Entry mới chỉ có TransactionId identity sau khi caller SaveChanges; caller dùng navigation/entry để
liên kết nguồn trước flush, chỉ trả transactionId ra response sau commit. Replay trả entry đã lưu có ID.
Nếu lỗi bất kỳ bước nào thì rollback toàn bộ; sau commit mới phát event/refetch theo task realtime.
Tạo hóa đơn sau đó chỉ liên kết các khoản thu, không debit lại.

| Nguồn | Type / dấu Amount ledger | OperationKey bền vững |
| --- | --- | --- |
| Nạp quầy đã thu | `TopUp` / dương | `CounterTopUp:{serverRequestId}` |
| Nạp ZaloPay đã xác minh | `TopUp` / dương | `ZaloPayTopUp:{merchantTransactionId}` |
| Phí phiên | `GamingCharge` / âm | `SessionCharge:{sessionId}:{chargedThroughUtcTicks}` |
| Đơn ví | `OrderPayment` / âm | `OrderPayment:{orderId}` |
| Hoàn dòng đơn ví | `OrderRefund` / dương | `OrderRefund:{orderId}:{orderDetailId}` |

Mỗi operationKey tối đa 100 ký tự theo contract checkout, duy nhất trong DB, không chỉ cache/process lock; lưu payload chuẩn gồm customer,
kind/type/nguồn, tiền và debit gốc khi có. Retry cùng key và payload trả kết quả đã commit,
không thu/cộng hay quyết toán lại. Cùng key khác payload trả 409; khác key cùng một nguồn đã thu/hoàn
cũng bị ràng buộc nguồn duy nhất chặn. Description chỉ là diễn giải, không dùng làm khóa hoặc FK.

Quy tắc tiền:

- `0 < amountPositive <= 999_999_999` VND theo D10, khoản mới là số nguyên do server tính. Ledger Debit âm, Credit/Refund dương;
  BalanceAfter là số dư sau cùng thao tác. Không tạo ledger/Payment tiền 0; nguồn 0 đồng vẫn có audit/trạng thái.
- Debit yêu cầu khách Active và số dư sau quyết toán đủ chi; không âm, không cho thấu chi.
  Từ chối thiếu tiền trước commit; không ghi một nửa nguồn/ledger. Credit nạp mới yêu cầu khách Active.
- Hoàn ví chỉ cho khoản debit ví đã commit có nguồn hợp lệ; tổng hoàn thành công + phần đang giữ
  không vượt khoản thu hoặc giá trị hủy hợp lệ. Khách Locked vẫn được nhận hoàn; retry không hoàn hai lần.
  Cash/ZaloPay hoàn về phương thức gốc theo contract checkout, không tự đổi thành hoàn ví.
- Base hiện tại đã chuyển tiền sang `decimal(18,0)` theo D10/SCRUM-13; fixture SQL dùng số nguyên VND.
  Dữ liệu lẻ của schema lịch sử phải được đối soát trước migration, không âm thầm làm tròn hoặc nâng
  hạn mức API 999_999_999 theo precision mới. Công thức ngân sách phí phiên giữ theo contract phiên.
- Khóa SQL bắt đầu tại Customer, rồi Computer → GamingSession/SessionCharge khi liên quan,
  tiếp đến nguồn đơn/chi tiết và ledger/Payment theo thứ tự thống nhất của contract chứng từ.
  Mọi writer ví dùng cùng thứ tự/khóa trên DB để chạy nhiều API instance an toàn.

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

## 6. Nạp tại quầy và khoảng trống schema

Nhân viên có `wallet.topup` xác nhận đã nhận tiền trước khi server ghi credit/ledger trong cùng transaction.
Request chưa thu không đổi balance; không dùng trạng thái demo `CreditedUnpaid` như khoản nạp thành công.
Nếu API mất phản hồi sau commit, tra/retry cùng khóa để lấy kết quả cũ. Nếu SQL rollback sau khi đã nhận
tiền ngoài hệ thống, giữ phiếu/khóa tiếp nhận và đối soát hoặc hoàn tiền có bằng chứng; không thu khách lần hai.
Không nhập dữ liệu demo hoặc tự tạo ledger để hợp thức hóa số dư cũ trong SCRUM-5.

Schema hiện tại chưa có OperationKey, source/refund linkage và các ràng buộc writer kể trên.
Minh phối hợp Trường thêm migration mới ở S2 cho nguồn nạp/ledger/payment và khóa duy nhất,
backfill có bằng chứng nếu phát sinh; Phúc bổ sung snapshot/charge nguồn phiên, Khang nguồn đơn.
Không sửa migration đã áp hoặc chạy writer trước khi đủ constraint/transaction kiểm chứng.

## 7. Kiểm chứng và bàn giao tích hợp

Kiểm tra HTTP/persistence tại `Backend/tests/CyberManagement.API.Checks/CustomerChecks.cs` bao gồm
ma trận quyền, tạo/đọc/reload, validation, trùng phone/cạnh tranh SQL, own-profile/ledger, pagination,
Locked sau login và không lộ credential. Browser SQL có runner `frontend/tests/run-customers-live.mjs`.
Mỗi runner SQL chỉ tạo/xóa database thử tên ngẫu nhiên riêng; không migrate DB đang dùng của người dùng.

```powershell
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks
$env:AUTH_CHECKS_SQL_CONNECTION = 'Server=.\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks
.\scripts\npm-local.cmd --prefix frontend run build
.\scripts\npm-local.cmd --prefix frontend run test:customer-wallet
node frontend/tests/run-customers-live.mjs
Remove-Item Env:AUTH_CHECKS_SQL_CONNECTION
```

Đây là hướng dẫn chạy, không phải tuyên bố các lệnh đã pass. Kết quả thực chạy và giới hạn bàn giao
trực tiếp trong chat. S2 phải kiểm thêm debit/credit/refund cạnh tranh, retry/crash, rollback nguồn–ledger,
settlement tranh đơn, chống âm/hoàn vượt và realtime hai máy. SCRUM-5 chưa chứng minh các writer đó.
Owner liên quan nhận contract này để triển khai cùng một hướng; nghiệm thu và Done thuộc Trưởng nhóm.
