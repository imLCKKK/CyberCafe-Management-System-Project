# 005 - ZaloPay sandbox: cấu hình và contract TopUp/Order

| Thông tin | Nội dung |
| --- | --- |
| Task | SCRUM-6 / S1-M-2 - Configure-ZaloPay-Sandbox-Contracts |
| Ngày đối chiếu | 10/11/2026; giữ rõ thời điểm của bằng chứng lịch sử |
| Owner / phối hợp | Minh; Khang nhận giao diện Order, Trường nhận nguồn tiền/chứng từ |
| Reviewer cuối | Trưởng nhóm |
| Phụ thuộc | S1-K-2 / SCRUM-11 và S1-M-1 / SCRUM-5; app sandbox, callback HTTPS |
| Nền gói bàn giao | Git `d2141da` đã ghép SCRUM-5 với dev/realtime; .NET 10, SQL Server 38 bảng/5 migration, ID int. Bằng chứng S1 ban đầu ở `4816802` được ghi riêng theo ngày |

## 1. Phạm vi và nguồn áp dụng

S1-M-2 chuẩn bị cấu hình server, chữ ký, contract giao dịch và kiểm tra truy cập callback HTTPS.
Adapter gọi tạo/query, callback ghi nhận tiền, worker, migration và UI thanh toán thuộc S2-M-2/S2-M-3.
Task này không cộng ví, thu đơn, đổi trạng thái đơn hoặc ghi chứng từ.

Tài liệu người dùng cung cấp `Zalosandbox.pdf`, chương 2 trang 5-13, là tài liệu tham khảo
cung cấp cho BrewLite/NestJS/PostgreSQL. Áp dụng quy trình cấu hình sandbox, HMAC, tunnel, chống lặp
và đối soát vào nền CyberManagement hiện tại. Quy tắc cộng điểm, Prisma, role và state machine của
BrewLite không thay hợp đồng [ví](003-customer-wallet-contract.md), [checkout](004-order-checkout-contract.md)
hoặc [chứng từ](../../docs/contracts/inventory-invoice-voucher.md) của dự án này.

Ngày 08/11 người dùng chọn sandbox chính thức và tunnel HTTPS tạm. PDF chỉ chứa placeholder AppId/keys.
Trang [Bắt đầu của ZaloPay](https://developer.zalopay.vn/v1/start/) hiện công bố merchant dùng thử;
khả năng sử dụng với endpoint hiện tại phải được kiểm tra riêng, không coi việc công bố khóa là bằng chứng
giao dịch đã thành công. Không chép giá trị khóa vào tài liệu, Git hoặc log.

## 2. Cấu hình server

Section .NET: `Payments:ZaloPaySandbox`. Các giá trị được cấp bằng environment variables hoặc .NET
User Secrets cho project `Backend/src/CyberManagement.API`. .NET không tự đọc `Backend/.env`.

| Property | Biến môi trường | Quy tắc |
| --- | --- | --- |
| Enabled | `Payments__ZaloPaySandbox__Enabled` | Mặc định false; khi bật chỉ cho Development/Testing/Test |
| AppId | `Payments__ZaloPaySandbox__AppId` | Số nguyên dương của bộ thông tin sandbox |
| Key1 | `Payments__ZaloPaySandbox__Key1` | Khóa ký request, chỉ ở server |
| Key2 | `Payments__ZaloPaySandbox__Key2` | Khóa xác minh callback, chỉ ở server |
| CallbackUrl | `Payments__ZaloPaySandbox__CallbackUrl` | HTTPS, DNS công khai, cổng 443, path `/api/payments/zalopay/callback` |
| RedirectUrl | `Payments__ZaloPaySandbox__RedirectUrl` | HTTPS; HTTP loopback chỉ dành local Development/Testing/Test |

Khi bật sandbox phải có đủ AppId/Key1/Key2 thuộc cùng một bộ. URL không chứa userinfo, query hoặc
fragment; callback không dùng localhost/IP. Cờ tắt không bắt buộc keys và không tạo provider request.
Validation chỉ báo tên trường lỗi, không đưa giá trị khóa vào exception hoặc endpoint cấu hình.
Frontend không nhận keys, raw cấu hình hoặc raw request có chữ ký.

Địa chỉ provider cố định trong code, không nhận từ browser hoặc cấu hình production:

```text
POST https://sb-openapi.zalopay.vn/v2/create
POST https://sb-openapi.zalopay.vn/v2/query
```

Khi triển khai adapter S2: POST form-urlencoded, timeout hữu hạn, không tự theo HTTP redirect và
không giữ SQL transaction trong lúc chờ mạng. Chỉ nhận payment URL HTTPS trên các host sandbox
đã cho phép; không dùng URL bất kỳ trả về để chuyển người dùng.

## 3. Giao dịch nội bộ chung

`TopUp` là nạp vào ví khách CyberManagement; `Order` là thu tiền đơn món. Cả hai là luồng thu tiền
qua Create Order. Không dùng API chi hộ/transfer-fund có tên TopUp của provider để nạp ví khách.

Các trường dưới đây là contract logic cho S2; mục 7 phân biệt cột đã có trên base SCRUM-13 với phần còn thiếu:

| Trường | Quy tắc |
| --- | --- |
| paymentId | ID int dương của ý định thu đã lưu; không lấy từ browser để bỏ qua kiểm tra nguồn |
| purpose | `TopUp` hoặc `Order`, bất biến sau khi tạo |
| customerId | Khách của nguồn, lấy từ JWT/nguồn server đã kiểm tra quyền |
| sourceId | ID nguồn nạp hoặc OrderId; nguồn phải tồn tại và thuộc đúng khách |
| amountVnd | Số nguyên VND `1..999_999_999`, server xác định và lưu một lần |
| merchantTransactionId | `app_trans_id`, duy nhất bền vững, tối đa 40 ký tự |
| gatewayTransactionId | `zp_trans_id` int64, lưu/trao đổi bằng chuỗi chữ số dương không mất độ chính xác |
| operationKey | Tối đa 100 ký tự, duy nhất theo ý định/nguồn nghiệp vụ; giữ nguyên qua retry |
| status | `Pending`, `Succeeded`, `Failed`, `Expired`; không dùng trạng thái UI làm bằng chứng thu tiền |
| createdAtUtc / expiresAtUtc | Mốc server đã lưu; retry/restart không gia hạn |
| requiresReconciliation | Phân biệt tiền đã thu nhưng chưa hoàn tất nghiệp vụ hoặc kết quả provider chưa rõ |

Giới hạn tiền là policy dự án theo D10, không phải cam kết min/max chung của ZaloPay. Amount của
Order lấy từ snapshot checkout; TopUp lấy từ ý định nạp đã qua validation/quyền server.
`paymentId`, purpose, khách, nguồn, amount và merchant ID được lưu trước khi gọi provider.
Metadata do callback trả lại không được tự tạo một nguồn mới hoặc đổi purpose đã lưu.

Merchant ID có tiền tố ngày `yyMMdd` theo Việt Nam UTC+7, ví dụ dạng `yyMMdd_<32 ký tự hex>`.
Sinh đúng một lần cho ý định thu; chỉ có unique constraint/khóa bền vững mới bảo đảm chống lặp.
Ngày/múi giờ của mã không thay thế `createdAtUtc`, thời điểm provider thu hoặc thời điểm xác nhận.
Quy tắc provider về các trường create và tiền tố ngày được đối chiếu tại
[Create Order](https://docs.zalopay.vn/vi/docs/specs/order-create/).

Giao diện Order nhận từ SCRUM-11:

```text
CreateOrderPayment({ orderId, customerId, paymentId, amountVnd, expiresAtUtc, operationKey })
  -> { paymentId, status: Pending|Failed, paymentUrl?, expiresAtUtc }
```

Đây là giao diện nghiệp vụ, chưa phải API/C# adapter đang gọi provider. `Pending` được trả khi ý định
đã lưu nhưng URL/kết quả chưa rõ; timeout không được chuyển thành Failed để mở lần thu mới.
TopUp sử dụng cùng dữ liệu xác minh, nhưng lấy nguồn từ ý định nạp riêng, không giả một Order/SessionCharge.

## 4. Chuỗi ký và callback

HMAC-SHA256 dùng UTF-8; số được định dạng invariant, MAC trả hex. Key1 ký request, Key2 xác minh
callback theo [Secure Data Transmission](https://docs.zalopay.vn/docs/developer-tools/security/secure-data-transmission/).

```text
createInput = app_id + "|" + app_trans_id + "|" + app_user + "|" + amount
              + "|" + app_time + "|" + embed_data + "|" + item
createMac   = HMAC_SHA256_HEX(Key1, createInput)

queryInput  = app_id + "|" + app_trans_id + "|" + Key1
queryMac    = HMAC_SHA256_HEX(Key1, queryInput)

callbackMac = HMAC_SHA256_HEX(Key2, originalDataString)
```

Create ký đúng hai chuỗi JSON `embed_data` và `item` sẽ gửi đi; không serialize lại sau khi ký.
`app_time` dùng Unix milliseconds; app_user không rỗng/tối đa 50, description tối đa 256, item tối đa
2048 ký tự. `expire_duration_seconds=900` nằm trong khoảng được công bố 300..2.592.000; vẫn cần
kiểm chứng sandbox thực tế. Giới hạn embed_data 1024 ký tự, nếu áp dụng, là lựa chọn tương thích
thận trọng với tài liệu v2 cũ; không suy thành giới hạn mới được mọi trang spec xác nhận.

Callback chính thức là POST JSON `{ data: string, mac: string, type: 1 }`, không dùng JWT của nhân viên.
Xác minh MAC 64 hex bằng so sánh constant time trên nguyên chuỗi `data` trước parse; không ký JSON
đã deserialize/serialize lại. Helper S1 giới hạn data 65.536 byte UTF-8 để chặn đầu vào quá lớn;
đây là giới hạn nội bộ thận trọng, không phải số ký tự Unicode của payload.
Sau xác minh ở S2, phải đối chiếu app, merchant ID, payment/khách/nguồn/purpose đã lưu, amount và
gateway transaction ID. Callback TopUp không thanh toán Order và ngược lại. Chữ ký đúng chưa đủ
để tự chuyển nghiệp vụ hoặc công nhận một transaction ID đã thuộc khoản thu khác.
[Spec callback](https://docs.zalopay.vn/vi/docs/specs/callback-api/) mô tả `1` hợp lệ và `2` không hợp lệ.

Callback S2 chỉ trả acknowledgment thành công sau commit hoặc replay khớp kết quả đã commit.
Mẫu knowledge-base dùng `0` cho lỗi tạm và có mã lỗi khác spec; không xem mẫu đó là cam kết retry
hay tự đổi bảng mã. Nếu chưa thể xử lý bền vững, không trả `return_code=1` để giả đã ghi nhận tiền;
phải có query/đối soát độc lập để phục hồi khi callback thiếu hoặc lỗi.

## 5. Query, trạng thái và chống ghi nhận lặp

Query gắn với merchant ID lấy từ ý định đã lưu, gọi đúng endpoint HTTPS bằng request server tự ký.
Response query không có MAC hoặc echo AppId/merchant ID; không yêu cầu provider trả những trường
không nằm trong schema. Gắn response với chính request/ý định đó, kiểm tra trạng thái, amount,
gateway ID và thời điểm trước khi đi vào cùng đường xác nhận như callback.
Khuyến nghị polling/đối soát khi mất callback được nêu tại
[Query Order](https://docs.zalopay.vn/vi/docs/specs/order-query/).

| Tình huống | Kết quả nghiệp vụ S2 |
| --- | --- |
| Create thành công, URL hợp lệ | Giữ ý định Pending, lưu URL; chưa coi là đã thu |
| Create/query timeout, HTTP lỗi, dữ liệu chưa rõ | Giữ dữ liệu hiện tại và yêu cầu đối soát; không tạo merchant ID mới |
| Xác minh khoản thu khớp | Ghi receipt Succeeded và tác động nguồn đủ điều kiện cùng transaction |
| Callback/query lặp cùng giao dịch/amount/nguồn | Trả kết quả cũ, không thu/cộng/ghi ledger/audit lại |
| Cùng key khác payload hoặc cùng gateway ID khác nguồn | Từ chối thay đổi, giữ bằng chứng để đối soát |
| Hết giờ local | Không chứng minh provider chưa thu; phải query/đối soát |

S2 khóa nguồn theo thứ tự chung, đọc lại trạng thái dưới khóa và dùng unique constraints cho merchant ID,
gateway ID, operation key và nguồn. Một DbContext/transaction/commit cho tác động tiền liên quan.
Không dùng Description hoặc cache một API instance làm khóa chống lặp. Browser redirect chỉ quay về
màn hình đọc trạng thái server; không tự ghi Succeeded, cộng ví hay đổi đơn Paid.

Order giữ nguyên D05/D06/D09/D11 của SCRUM-11: TTL nội bộ 15 phút từ SQL chấp nhận đơn, không
gia hạn. Thu muộn vẫn ghi thực thu nhưng không hồi sinh đơn Cancelled/Expired hoặc phục hồi tồn/lượt.
Tạo nghĩa vụ hoàn đúng phương thức gốc; Admin/Manager có quyền duyệt trước khi worker gửi hoàn.
Payment đang Failed chỉ được nhận ngoại lệ thu muộn sau query độc lập đã xác minh theo contract checkout.
Gateway Order không debit ví và không gọi settlement phí phiên D09; đó là giao điểm của thanh toán Wallet.
Chứng từ liên kết khoản đã thu, không thu lại khi phát hành và không cộng trùng ledger/Payment.

TopUp chỉ credit khi đã xác minh khoản thu đúng ý định và khách vẫn đủ điều kiện Active. Key ledger là
`ZaloPayTopUp:{merchantTransactionId}`, Type TopUp dương; nạp ví không phải doanh thu dịch vụ.
Nếu provider đã thu nhưng khách Locked hoặc nguồn không còn đủ điều kiện tự credit, phải ghi nhận
khoản thực thu và trạng thái cần đối soát, giữ tiền/nguồn để xử lý có quyền; không làm mất dấu khoản thu,
không tự credit trái rule Active và không tự đổi sang hoàn tiền chưa được duyệt.

## 6. HTTPS probe của S1

Probe chỉ nằm trong project checks, trỏ tunnel về listener local riêng `127.0.0.1:5090`.
Nó không phải controller thanh toán production và không cần mở các route quản trị/SQL ra internet.
Callback URL có path `/api/payments/zalopay/callback`; GET trả marker vận chuyển, POST kiểm
tra chữ ký fixture và trả rõ `probeOnly=true`, `businessStateChanged=false` cùng kết quả xác minh.
Probe không trả `return_code=1` của một khoản tiền đã xử lý và không ghi ví/đơn/database.

Giữ tunnel và probe hoạt động khi kiểm tra. Tunnel đổi host thì cập nhật server config; không xem URL
cũ đã gửi cho provider là tự đổi theo. Ngrok trong PDF hoặc quick tunnel tương đương chỉ chuyển tiếp
HTTPS về listener; tunnel không cấp AppId/keys và không chứng minh callback từ provider đã được xử lý.
Chỉ chuyển tunnel sang handler production khi S2 có persistence và quy trình xác nhận thực sự.

## 7. Hiện trạng schema và bàn giao S2

Đích tích hợp của nhóm đã đối chiếu tại base SCRUM-5 `4816802`, kế thừa `93cf457`/PR17 có migration SCRUM-13
`20261107071954_IntegrateInventoryInvoiceVoucherContracts`, phát triển từ nền 35 lên 38 bảng nghiệp vụ.
Ba bảng bổ sung là PromotionUsages, InvoiceIssueRequests và RefundAttempts. S1-M-2 được thực hiện
trên Git worktree riêng từ `4816802`; không chép đè file dùng chung từ ảnh workspace cũ 35 bảng.
Mô tả này không khẳng định database local hoặc database dùng chung đã được nâng cấp.

Trên base SCRUM-13, Payment đã có InvoiceId nullable, nguồn OrderId/SessionChargeId, Type,
OrderDetailId/RefundOfPaymentId, OperationKey, MerchantTransactionId, GatewayTransactionId và các mốc
CreatedAt/PaymentTime/ConfirmedAt/ProviderOccurredAt/PaymentTimeBasis. OperationKey tối đa 100 ký tự
và unique; mã merchant 40, mã gateway 50, có unique index theo Method cho khoản thu. Tiền Payment,
Customer.WalletBalance và ledger đã cấu hình decimal(18,0); migration chặn dữ liệu lẻ trước đổi kiểu,
không âm thầm làm tròn. S1-M-2 tái sử dụng các trường/ràng buộc này, không thêm lại hoặc sửa migration đã áp.

Phần còn thiếu để thực thi TopUp là nguồn/ý định nạp bền vững, phân biệt purpose và writer xác nhận.
CHECK nguồn Payments hiện chỉ cho OrderId XOR SessionChargeId; chưa có TopUp trong DbContext.
WalletTransaction vẫn chưa có OperationKey hoặc định danh nguồn riêng. Minh phối hợp Trường thiết kế
phần bổ sung ở S2 trước khi ghi nạp; không giả TopUp thành Order/SessionCharge hoặc bỏ CHECK để chạy qua.
Writer và tính nguyên tử nguồn-ví-ledger/chứng từ phải được kiểm tra riêng dù các cột Payment đã có.
S1-M-2 không thêm migration, đổi bảng tiền hoặc dùng schema BrewLite làm schema đích.

## 8. Bằng chứng và phần còn chờ

Gói SCRUM-6 ngày 10/11 ghép thay đổi S1 lên `d2141da`, giữ policy/hub và kiểm thử realtime,
catalog, kho, khách hàng. Các số liệu 08–09/11 dưới đây là lịch sử trên `4816802`, không tự coi
là kết quả chạy lại của gói mới; lệnh và kết quả bàn giao mới ghi tại `README-SCRUM-6.md`.

| Hạng mục | Trạng thái tại bản hợp đồng này |
| --- | --- |
| Đọc PDF và contract ví/checkout/chứng từ | Đã đối chiếu phạm vi và khác biệt stack |
| Config validation và fixture create/query/callback | Trên base `4816802`: 77 kiểm tra fixture đạt, build 0 warning/error; regression InMemory tổng 358 ca đạt ngày 08/11/2026 |
| Hồi quy catalog/khách trên SQL Server | Trên `.\SQLEXPRESS`, database thử riêng đã dọn: `--catalog-only` đạt 205, `--customer-only` đạt 206 kiểm tra (mỗi bộ bao gồm 77 fixture ZaloPay). DI/proxy/scripts catalog/kho/khách giữ nguyên từ `4816802` |
| Toàn bộ suite SQL | Trên base S1 `4816802`, chưa chạy toàn bộ và từng có lỗi fixture ComputerChecks `10000.50`/`decimal(18,0)`. Base bàn giao `d2141da` đã sửa fixture thành `10001`; đây không còn là blocker hiện tại. Kết quả chạy lại xem `README-SCRUM-6.md` |
| Script PowerShell | Parser PowerShell 5.1 đạt; 11 ca AST/stub nội bộ kiểm tra guard, cleanup flag và giữ lỗi gốc. Chạy lại helper lúc 13:35 ngày 09/11 đạt build 0 warning/error, cấu hình, hai controls Query và 4 fixture HTTPS |
| App sandbox/bộ keys đã cấu hình phía server | Bộ merchant demo AppId 554 công bố trên trang Bắt đầu được helper lưu vào API User Secrets và probe đọc được. Hai controls Query bằng AppId/Key1 đạt ngày 09/11; 4 fixture xác nhận binding Key2 với probe. Key2 chưa được kiểm chứng bằng callback của giao dịch provider thật |
| URL HTTPS và marker GET/POST probe | 13:35 ngày 09/11/2026 trên worktree `4816802`: tunnel cũ hết hiệu lực đã được thay; public HTTPS GET marker và 4 POST fixture đúng/sai cho metadata TopUp/Order đạt, providerAcknowledged=false, không ghi tiền. URL hiện tại xem `.tools/scrum6/zalopay-demo-state.json`; host thay khi mở tunnel mới |
| Query sanity với provider thật | 13:26 ngày 09/11/2026 (UTC+7): POST `/v2/query` với mã mới chưa tồn tại nhận HTTP 200, `2/-101`; giữ nguyên AppId/mã và đổi MAC sai nhận HTTP 200, `2/-402`. AppId 554/Key1 dùng thử đạt hai controls Query, gỡ blocker kết nối ngày 08/11. Không gọi Create, không tạo giao dịch; bằng chứng không chứa khóa ở `.tools/scrum6/query-connection-recheck.json` |
| Callback do một khoản thanh toán sandbox thật tạo ra | Chưa kiểm chứng ở S1; thuộc luồng tích hợp S2 |
| Persistence/ghi tiền/rollback/realtime | Chưa triển khai ở S1-M-2 |
| Phản hồi liên module / nghiệm thu | Chưa có bằng chứng owner đồng thuận hoặc Trưởng nhóm duyệt |

Giới hạn vận hành khi bàn giao: URL quick tunnel có thể hết hiệu lực; callback HTTPS hiện chưa
được xác nhận ổn định. Bằng chứng HTTPS ở bảng là probe/fixture theo đúng thời điểm, không phải
callback của một khoản thu thật. Đây là blocker cụ thể theo checklist cấu hình; chạy start script
để cấp URL mới và kiểm tra lại trước khi xác nhận truy cập tại thời điểm demo.
Luồng QR/nạp ví thực tế được làm tiếp ở S2 trong checkout `zalopay-topup`, không nằm trong gói
contract SCRUM-6 này; bằng chứng thanh toán của S2 không được dùng để khẳng định callback S1 đã pass.

Fixture dùng khóa riêng chỉ phục vụ kiểm thử, phải phân biệt với cấu hình sandbox runtime. GET marker
qua HTTPS chỉ chứng minh đường truy cập; POST fixture chỉ chứng minh binding/chữ ký trên đường đó.
Build pass, config hợp lệ hoặc query mã không tồn tại không chứng minh khách đã thanh toán, callback
provider đã đến, ví đã cộng hay Order đã Paid. Ghi đúng blocker nếu app/keys/tunnel/provider không hoạt động.

Các ca 77 dùng khóa fixture riêng; 4 ca HTTPS dùng bộ demo đã cấu hình để kiểm tra binding/chữ ký giữa
client thử và listener. Chúng không chứng minh Key2 đã nhận callback từ một giao dịch provider thật.
`Enabled=true` chỉ xác nhận người vận hành bật cấu hình trong môi trường cho phép. Hai controls Query
ngày 09/11 xác minh khả năng dùng AppId/Key1 công bố với endpoint Query; trường `providerCompatibilityVerified`
trong bằng chứng này chỉ có phạm vi đó. Create, Key2/callback và xử lý tiền đúng nguồn vẫn cần kiểm chứng
ở luồng tích hợp S2.

Chạy lại probe bằng `scripts/start-zalopay-demo.ps1 -UseOfficialDemo`; cần cloudflared chính thức.
Nếu đã có probe/tunnel, chạy `scripts/stop-zalopay-demo.ps1` trước khi start lại. Quick tunnel có thể
hết hiệu lực; lỗi `Tunnel not found` cần cấp URL mới bằng start script, không dùng lại URL đã hết hạn.
Nếu Query timeout, script ghi trạng thái chưa xác minh rồi tiếp tục riêng phần config/fixture; nếu provider
trả kết quả không qua controls thì dừng, không giả credential đã hợp lệ. Với mã mới chưa tồn tại,
positive control phải là `return_code=2/sub_return_code=-101` (ORDER_NOT_EXIST); giữ nguyên AppId/mã
và chỉ đổi MAC cho negative control, phải nhận `2/-402` (ILLEGAL_APP/SIGNATURE_REQUEST).
`-54` là TIME_INVALID của trường hợp hết hạn, không phải kết quả not-found cho phép kiểm tra này.
Các mã này theo [bảng trạng thái Query chính thức](https://docs.zalopay.vn/vi/docs/developer-tools/knowledge-base/status-codes/).
Positive control bị từ chối hoặc trả mã ngoài dự kiến luôn dừng độc lập với lỗi của negative control;
timeout của một request chỉ cho tiếp tục kiểm tra fixture, `providerCompatibilityVerified` vẫn false.
Dừng tunnel/probe đã ghi
nhận bằng `scripts/stop-zalopay-demo.ps1`. Cả hai dùng listener thử `127.0.0.1:5090` riêng.

Với merchant riêng, cấu hình User Secrets theo mục 2 và bật Enabled trước khi chạy start script không
có `-UseOfficialDemo`. Script ghi CallbackUrl mới theo tunnel được cấp, giữ nguyên bộ keys và cờ Enabled.
Chỉ lưu URL sau khi marker public probe xác nhận cấu hình đang bật/hợp lệ và đường HTTPS truy cập được.
Nhánh official demo sẽ tắt lại cờ do nó quản lý nếu bước enable/probe/fixture thất bại; cleanup không
tự tắt cờ merchant riêng. Cờ Enabled vẫn không chứng minh credentials đã được provider chấp nhận.
