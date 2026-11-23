# SCRUM-22 — ZaloPay Gateway và callback

Task `S2-M-2 — Implement-ZaloPay-Gateway-Callbacks`. Ngày đối chiếu: **16/11/2026**,
base dev `c6fd920`. Owner: Minh; reviewer cuối: **Trưởng nhóm**.

## 1. Phạm vi và luồng

Adapter dùng chung cho `TopUp` và `Order`: server lưu ý định thanh toán trong `ZaloPayIntents`,
commit ID/nguồn/số tiền dự kiến, rồi mới gọi Create qua HTTPS. Callback và Query đi vào cùng
đường ghi receipt đã xác minh. Không giữ transaction SQL trong thời gian gọi provider.

SCRUM-22 ghi nhận **receipt gateway**, chưa cộng ví/ledger, tạo Payment dịch vụ, xác nhận món,
giữ/trừ tồn, phát hành hóa đơn hoặc phát event hoàn tất nghiệp vụ. Các tác động đó thuộc
`S2-M-3` và caller checkout. `Succeeded` của intent không có nghĩa ví đã được cộng hoặc đơn đã được làm.
Luồng tiền mặt và tiền tặng SCRUM-21 giữ theo [hợp đồng ví](003-customer-wallet-contract.md).

**Điểm nối S2-K-2 bổ sung local ngày 20/11/2026:** Order mới có ReservedUntilUtc và Payment Pending
giữ sẵn mã merchant; intent dùng lại mã, thời điểm tạo và hạn nguồn. Callback/Query sau receipt commit
gọi `IOrderZaloPaySettlement` để hoàn tất nghiệp vụ Order, chỉ ACK sau khi thao tác/replay thành công.
Worker Order phục hồi receipt đã commit nhưng chưa áp dụng; worker query tiếp tục truy vấn Failed/Expired
cho Order có reservation. Các tác động Order này thuộc S2-K-2, không thay phạm vi TopUp của S2-M-3
hoặc suy diễn dữ liệu Order lịch sử thành giữ chỗ. Bộ gateway fixture vẫn kiểm tra tương thích nguồn cũ.

Nguồn áp dụng: [cấu hình S1-M-2](005-zalopay-sandbox-contract.md),
[checkout](004-order-checkout-contract.md), [chứng từ](../../docs/contracts/inventory-invoice-voucher.md).
`Zalosandbox.pdf` là hướng dẫn quy trình tham khảo; giữ stack .NET/EF/SQL Server và contract của dự án.

## 2. API

Các endpoint chính chủ yêu cầu JWT Customer hợp lệ, `customer_id` khớp `sub`, khách còn Active.
Không nhận CustomerId của người khác từ body. Hai endpoint Create yêu cầu `Idempotency-Key` là UUID khác rỗng.

| Method / path | Body / kết quả |
| --- | --- |
| POST `/api/customers/me/payments/zalopay/topups` | `{ "amountVnd": 50000 }`; server kiểm tra số nguyên 1..999.999.999 và lưu số tiền dự kiến |
| POST `/api/customers/me/payments/zalopay/orders/{orderId}` | `{}`; số tiền lấy từ `Order.TotalAmount` đã lưu, không nhận giá/tổng từ browser |
| GET `/api/customers/me/payments/zalopay/{intentId}` | Đọc intent của chính khách, không gọi provider |
| POST `/api/customers/me/payments/zalopay/{intentId}/sync` | Query bằng AppId/merchant ID đã lưu, trả trạng thái được đọc lại |
| POST `/api/payments/zalopay/callback` | Public server-to-server `{ "data": "...", "mac": "...", "type": 1 }`; không dùng JWT của khách |

Body có trường ngoài contract bị từ chối. Request xem/sync intent không thuộc khách trả 404;
sai quyền trả 403, dữ liệu sai 400, key khác payload hoặc nguồn không còn đủ điều kiện trả 409.
Gateway chưa bật trả 503. Lỗi hệ thống/provider không trả số dư hoặc kết quả thanh toán giả.

Response intent gồm ID, CustomerId, Purpose, SourceId, ExpectedAmountVnd, Status, PaymentUrl,
MerchantTransactionId, CreatedAtUtc/ExpiresAtUtc, ProviderTransactionId/ProviderAmountVnd,
VerifiedAtUtc, RequiresReconciliation và FailureReason; không chứa Key1/Key2.
SourceId là IntentId với TopUp, OrderId với Order. PaymentUrl là URL checkout có token: chỉ đưa cho
khách chính chủ, không đưa vào log hoặc bằng chứng công khai.

Key được namespace theo purpose và khách. Retry cùng key/payload dùng lại intent, merchant ID,
số tiền và deadline đã lưu; cùng key khác nội dung trả 409. Một Order chỉ có một intent.
Khi chưa có URL/receipt, retry hoặc worker Query mã cũ trước; chỉ kết quả NotFound rõ ràng và nguồn
còn hợp lệ mới cho phép gửi lại Create với mã cũ. Query lỗi/đang xử lý không cho phép tạo mã mới.
Đơn phải thuộc khách, ở AwaitingPayment theo contract checkout (vẫn đọc được Pending cũ),
có số tiền hợp lệ và chưa có khoản Payment Succeeded. Cả tạo intent và ghi receipt dùng cùng tập trạng thái này.

## 3. Thời hạn, chữ ký và xác minh

- TopUp có hạn 15 phút. Order dùng deadline gốc `OrderTime UTC + 15 phút`; tạo intent không gia hạn đơn.
  Create Order chỉ được thực hiện khi còn ít nhất 300 giây. Lifetime của request gốc nằm trong
  khoảng 300..900 giây. Phục hồi giữ nguyên tham số của intent; không đổi app_time/merchant ID
  hoặc kéo dài deadline nội bộ. Trước khi gửi lại Create phải còn ít nhất 300 giây và vẫn đúng ngày
  Việt Nam của merchant ID; hết điều kiện thì giữ để Query/đối soát, không tự sinh giao dịch thay thế.
- Merchant ID do server sinh: ngày Việt Nam GMT+7 `yyMMdd_` và suffix duy nhất, tối đa 40 ký tự.
  AppId, khách, purpose, nguồn, tiền và thời điểm tạo lấy từ intent đã commit.
- Create ký HMAC-SHA256 bằng Key1 trên đúng các trường/JSON đã gửi; Query ký merchant ID đã lưu.
  Giữ bound ứng dụng `embed_data` 1024 ký tự để tương thích fixture S1, thấp hơn giới hạn provider 2048.
- Callback xác minh Key2 với UTF-8 của **nguyên giá trị data** trước khi parse; không serialize JSON lại.
  Sau đó so AppId, merchant ID, app_user, app_time, số tiền và provider transaction ID với intent.
  Trường JSON trùng, MAC sai, số tiền lẻ, ID sai hoặc dữ liệu quá lớn không trở thành receipt.
- Merchant dùng thử có khóa công khai: dự án luôn Query độc lập theo intent trước khi xác nhận callback mới.
  Đây là biện pháp bảo vệ của dự án. Callback hợp lệ về MAC chưa đủ chứng minh có giao dịch provider.
- Query không có MAC/echo merchant ID trong schema; response được gắn với request server đã ký.
  `zp_trans_id` được giữ dạng chuỗi chữ số Int64, tránh mất chính xác qua JavaScript.
  `server_time` của Query là thời gian xử lý; VerifiedAtUtc là giờ xác nhận tại server, không giả là giờ trả tiền.
  Với trial keys công khai, callback.server_time cũng không được nâng thành thời điểm trả tiền đã xác minh
  độc lập: receipt lấy từ Query giữ ProviderOccurredAtUtc null, dùng cơ sở giờ xác nhận khi tích hợp chứng từ.

URL Create/Query cố định ở sandbox chính thức; không tự follow HTTP redirect. Mỗi lần gọi giới hạn
10 giây gồm đọc body, response tối đa 128 KiB. JSON/HTTP/timeout lỗi không fallback mock.
`order_url` và redirect browser chỉ phục vụ điều hướng, không xác nhận đã thanh toán.

Nguồn chính thức: [Create](https://docs.zalopay.vn/vi/docs/specs/order-create/),
[Query](https://docs.zalopay.vn/vi/docs/specs/order-query/),
[Callback](https://docs.zalopay.vn/vi/docs/specs/callback-api/),
[mã trạng thái](https://docs.zalopay.vn/vi/docs/developer-tools/knowledge-base/status-codes/),
[callback retry](https://docs.zalopay.vn/vi/docs/developer-tools/knowledge-base/callback/).

## 4. Trạng thái và chống lặp

| Kết quả quan sát | Trạng thái / xử lý |
| --- | --- |
| Vừa lưu intent, Create trả URL, Query `return_code=3` | Pending; URL không phải receipt |
| Create đầu tiên bị từ chối rõ `2/-401` hoặc `2/-402` | Failed; không ghi receipt thành công |
| Create phục hồi bị từ chối sau lần gửi cũ chưa rõ | Pending + CreateRecoveryRejected; tiếp tục Query vì lần từ chối mới không kết luận được lần gửi cũ |
| Query Pending nhưng không có checkout URL | Pending + CheckoutUrlMissing và RequiresReconciliation; không tạo ID khác hay suy ra URL |
| Query `2/-54` | Expired theo provider |
| Query `2/-101` | Chưa có receipt; nếu thiếu URL và nguồn còn hợp lệ thì phục hồi Create với cùng mã, nếu không tiếp tục đối soát |
| Thiếu số dư/lỗi lần thanh toán còn có thể thử lại | Pending; ghi nhận thất bại có thể thử lại, không đóng intent như đã thu |
| Timeout/HTTP/auth/lỗi hệ thống/JSON không rõ | Giữ trạng thái đã lưu, RequiresReconciliation; không tự kết luận chưa thu tiền |
| Query Succeeded và receipt khớp | Succeeded, lưu một receipt đã xác minh |
| Receipt sai tiền, trùng provider ID ở intent khác hoặc receipt mâu thuẫn | Giữ bằng chứng và đánh dấu đối soát; không ghi nhận thêm thành công |
| Thu muộn sau hết hạn/thất bại, khách khóa hoặc Order đã đổi/đóng | Lưu receipt đã xác minh, RequiresReconciliation; không hồi sinh Order/cộng ví |

Không dùng `is_processing` để ghi đè `return_code`: trường này dành riêng cho ZaloPay và Sandbox
có thể trả true cùng lỗi hết hạn. `-63` có thể thử lại trước khi hết hạn; đừng coi là trạng thái đã trả tiền
hoặc một lần thất bại không thể phục hồi.

Ghi intent/receipt là transaction SQL riêng, khóa Customer trước intent. Lease có token giới hạn
cả Create và Query cạnh tranh; kết quả Create cũ không ghi đè lượt mới đã giữ lease khác.
Unique operation key, merchant ID, OrderId và cặp AppId/provider ID bảo vệ nhiều API instance.
Receipt đã lưu không bị retry thay ID/amount/time. Callback/query cùng giao dịch trả lại kết quả cũ,
không tạo thêm receipt, Payment, ledger hoặc tác động nguồn.

Callback trả `return_code=1` sau receipt hợp lệ đã commit hoặc duplicate khớp; `2` dữ liệu sai;
`0` khi tạm thời chưa ghi nhận được để provider retry. Không phản chiếu exception, MAC, body hay keys.
Worker tra cứu tối đa 5 intent đang chờ mỗi chu kỳ 15 giây; restart đọc trạng thái SQL đã lưu.
Intent Pending chưa có kết luận không bị loại chỉ vì hết hạn đã quá 24 giờ; tiếp tục tra cứu theo
lịch đã lưu. Từ 24 giờ sau expiry, các lần Query chưa chốt được kết quả giãn còn một giờ/lượt/intent;
batch vẫn tối đa 5. Lấy được receipt sau downtime dài vẫn ghi nhận proof và đánh dấu LatePayment nếu đã quá hạn.
Local expiry không tự chứng minh chưa thu tiền; Query/callback muộn vẫn phải được đối soát.

S2-M-3 chỉ tiêu thụ receipt đúng nguồn, khớp tiền, chưa tiêu thụ và không cần đối soát; lưu thao tác nguồn,
Payment/ledger cùng transaction nghiệp vụ với khóa chống lặp, rồi mới phát realtime. SCRUM-22 chưa hiện thực consumer này.

Migration `20261115162202_ZaloPayGatewayIntents` bổ sung bảng staging `ZaloPayIntents`, chỉ thêm schema;
không sửa ledger, bonus hay receipt tiền mặt đã có. Model hiện tại là **41 bảng/13 migration** từ nền dev
thực tế. Tài liệu baseline 35 hoặc SCRUM-21 39 bảng không thay cho số lượng schema của checkout mới.

## 5. Cấu hình và kiểm tra

AppId/Key1/Key2 cùng bộ, CallbackUrl HTTPS công khai và RedirectUrl do server cấu hình theo
[mục 2 của S1](005-zalopay-sandbox-contract.md#2-cấu-hình-server).
Keys dùng User Secrets/biến môi trường server, không nằm trong Git, frontend hoặc log.
Callback tunnel chỉ công khai route callback; HTTPS probe/fixture không thay bằng chứng callback của giao dịch thật.

Chạy từ repository root:

```powershell
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks -- --zalopay-gateway-checks
$env:AUTH_CHECKS_SQL_CONNECTION = 'Server=127.0.0.1\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True;Encrypt=False'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks -- --gateway-only
Remove-Item Env:AUTH_CHECKS_SQL_CONNECTION
```

Protocol fixture dùng keys và HTTP handler nội bộ; SQL checks dùng DB ngẫu nhiên riêng để kiểm
key/payload, hai purpose, quyền sở hữu, callback/query lặp/cạnh tranh, trạng thái lỗi và không đổi ví/Order.
Provider runtime lỗi luôn đi qua nhánh lỗi thật; handler fixture chỉ được đăng ký trong project kiểm tra.

### 5.1. Gọi sandbox chính thức

Helper đọc AppId/Key1/Key2 từ User Secrets của API hoặc biến môi trường server. Không truyền keys
trên command line. Từ root checkout, đã cấu hình đủ bộ sandbox và callback HTTPS hợp lệ:

```powershell
$env:AUTH_CHECKS_SQL_CONNECTION = 'Server=127.0.0.1\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True;Encrypt=False'
$env:Payments__ZaloPaySandbox__Enabled = 'true'
# Nếu dùng tunnel mới, đặt URL callback server; đây không phải URL checkout có token.
# $env:Payments__ZaloPaySandbox__CallbackUrl = 'https://<tunnel>/api/payments/zalopay/callback'
# Đặt biến sau nếu cần giữ listener callback-only tại 127.0.0.1:5097 để thử HTTPS:
# $env:ZALOPAY_LIVE_STAY_OPEN = '1'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks -- --zalopay-live-checks
```

Helper gọi Create/Query thật, dùng SQL GUID riêng và giữ database, source IDs/request keys trong
`.tools/live-sandbox/state.json`. Khóa chạy và workspace guard ngăn dùng nhầm state. Lần chạy sau
tái sử dụng intent đã lưu; không tự tạo lượt mới vì mất phản hồi hoặc hết hạn. URL đã lưu có thể đã
hết hạn: phải xem Status/ExpiresAt, không coi riêng `paymentUrlPresent` là bằng chứng còn trả tiền được.
Proof đã che dữ liệu nhạy cảm ở `.tools/live-sandbox/proof.json`; không in keys, MAC hoặc URL token.
Khi giữ listener, chỉ POST đúng route callback được phục vụ, các route khác trả 404.

### 5.2. Bằng chứng 15–16/11/2026

| Kiểm tra | Kết quả thực chạy | Phạm vi |
| --- | --- | --- |
| Build .NET | 0 warning, 0 error | Solution hiện tại |
| Runner chữ ký/adapter | 77 fixture S1 + 115 fixture protocol đạt | HTTP handler và keys nội bộ; không gọi provider |
| `--gateway-only` | 314 HTTP/SQL checks đạt | Gồm fixture/auth, hai purpose, retry/cạnh tranh, lease cũ không ghi đè kết quả mới và receipt chống lặp |
| API SQL đầy đủ | 981 checks đạt | Hồi quy các module nền và gateway; không phải 981 ca mới của SCRUM-22 |
| API InMemory | 581 checks đạt | Không thay bằng chứng transaction/khóa SQL |
| Inventory SQL | 104 checks đạt | Model 41 bảng/13 migration và hồi quy kho/schema |
| Build frontend | Đạt | Giữ luồng frontend nền; không chứng minh thao tác thanh toán trên điện thoại |
| Vite proxy | 3 tests đạt | Dev/preview chuyển tiếp callback, API gateway và WebSocket, giữ các route nền |

Lúc **00:20:50 ngày 16/11/2026 (GMT+7)**, tương ứng `2026-11-15T17:20:50Z`, Create chính thức
chấp nhận cả TopUp intent #1 **20.000đ** và Order intent #2 **30.000đ**, trả URL checkout.
Query thật sau Create cho cả hai là Pending, không cần đối soát, không có FailureReason;
chưa có provider transaction ID hoặc VerifiedAtUtc. Đây là bằng chứng gọi Create/Query sandbox thật,
không phải bằng chứng đã thanh toán.

POST bad-MAC qua localhost và HTTPS công khai đến controller callback-only cổng 5097 đều nhận
`return_code=2`. Kết quả này chứng minh đường callback truy cập được và từ chối MAC sai;
**chưa chứng minh callback từ một khoản thanh toán provider thật hoặc Key2 nhận callback trả tiền thành công**.

Bằng chứng TopUp đã trả tiền ở thử nghiệm ngày 10/11 thuộc luồng cũ, không được gộp thành Order
đã trả tiền/callback thật của SCRUM-22. SCRUM-22 chưa cộng ví, hoàn tất Order hoặc phát event nghiệp vụ.
Nhánh triển khai từ `c6fd920` chưa commit/push khi ghi bằng chứng này. Trưởng nhóm quyết định nghiệm thu;
giữ checklist chưa tick cho tới khi review và quyết định duyệt của Trưởng nhóm.

### 5.3. Ghép dev ngày 17/11/2026

Đã giải conflict giữa commit SCRUM-22 `7e5e689` và dev `490303a`: giữ đồng thời các mode
gateway/invoice/voucher, proxy `payments`/`client` và WebSocket. SQL/DBML được xuất lại từ
model đã ghép: 41 bảng, 15 migration; không sửa lịch sử migration. Kiểm tra downgrade hóa đơn
được đặt tại mốc schema của module, giữ assertion bảo vệ metadata/history rồi phục hồi đầy đủ
các migration đã ghép.

Build .NET (0 warning/0 error) và frontend đạt; 981 checks API/SQL tổng, 314 gateway,
145 invoice, 143 voucher, 104 inventory, 36 frontend unit và 3 proxy đạt. Các bộ tổng/tập trung
có kiểm tra trùng nhau, không cộng các con số thành số ca độc lập. `--check-schema` đạt;
source adapter/service/repository gateway không thay đổi so với `7e5e689`, không gọi lại provider.
Các giới hạn bằng chứng thanh toán/callback thật tại mục 5.2 vẫn giữ nguyên.

### 5.4. Sửa phản hồi review ngày 19/11/2026

Phạm vi bản sửa: worktree trên `432973f`, sau review `490303a..432973f`.

- Create và ghi receipt nhận Order AwaitingPayment; vẫn tương thích Pending cũ và chặn nguồn đóng.
- Phục hồi intent đã commit trước HTTP: cùng key giữ intent/merchant/tiền/app_time/deadline;
  Query trước, chỉ NotFound mới gửi lại Create nếu nguồn còn hợp lệ. Create/Query dùng chung lease
  30 giây, gia hạn trước gửi Create phục hồi; completion có token nên phản hồi cũ không ghi đè lượt mới.
  Khách khóa, nguồn đổi/đóng, thiếu thời hạn hoặc đổi ngày merchant không được gửi lại Create.
- Worker giữ intent Pending chưa có kết luận sau downtime quá 24 giờ; receipt tìm thấy sau hạn
  lưu Succeeded + LatePayment/RequiresReconciliation để consumer không hồi sinh nguồn đã hết hạn.
  Create phục hồi bị từ chối sau lần gửi chưa rõ vẫn được Query tiếp.

Không đổi schema/migration, public HTTP API hoặc cấu hình secret. Interface repository thêm
AcquireCreateAsync và tham số token/now cho SaveCreateResultAsync để bảo vệ completion.

Kiểm tra bản sửa đã chạy thành công:

| Kiểm tra | Kết quả |
| --- | --- |
| Build solution .NET | 0 error; 5 cảnh báo NU1900 do NuGet vulnerability service không truy cập được khi restore |
| Cấu hình/chữ ký và protocol adapter | 77 + 115 fixture nội bộ pass |
| Gateway HTTP/SQL tập trung | 364 checks pass, gồm ba ca tái hiện và recovery/concurrency/token fencing |
| API SQL tổng | 1.031 checks pass, bao gồm các ca gateway; không cộng hai suite thành số ca độc lập |
| git diff --check | Pass |

SQL checks dùng repositories/services thật và database ngẫu nhiên riêng đã dọn; chỉ HTTP provider
được thay bằng fixture nội bộ. Build artifacts đặt ở đường dẫn ngắn trong workspace để tránh giới hạn
native SqlClient DLL trên Windows. Log cục bộ `.tools/review-fix-*.log` bị Git ignore.
Không gọi lại Sandbox thật trong lượt sửa review này; không thay bằng chứng/giới hạn tại mục 5.2.
SCRUM-23 ở checkout riêng được giữ nguyên. Các kiểm tra trên được thực hiện trước commit/push;
Trưởng nhóm quyết định duyệt cuối.

### 5.5. Ghép dev và giải conflict ngày 19/11/2026

Ghép `dev c697aa8` vào SCRUM-22 `b0f6ec5` trên checkout riêng. Giữ ba mode độc lập
`--menu-seed-only`, `--zalopay-live-checks` và `--zalopay-gateway-checks` trong runner.
SQL/DBML được xuất lại từ EF model/migrations đã ghép: **41 bảng, 16 migrations**,
có cả `ZaloPayGatewayIntents` và `AddProductImageUrl`; không sửa migration lịch sử.

Build solution đạt 0 warning/0 error với restore từ cache và tắt NuGet audit do giới hạn mạng.
Các bộ kiểm tra đạt: 77 + 115 fixture adapter, 364 gateway HTTP/SQL, 337 catalog HTTP/SQL,
9 menu seed SQL và `--check-schema`. Những bộ này có kiểm tra trùng nhau, không cộng tổng.
DB mới được kiểm tra qua EF migrations trong các suite SQL. Ca nâng cấp riêng dựng baseline
bằng đúng 15 migrations của dev, lưu sẵn sản phẩm/ảnh/giá/tồn, rồi chạy SQL đã ghép hai lần:
đủ 16 migrations, bảng gateway truy cập được, dữ liệu cũ giữ nguyên. Các DB thử đã dọn.

Giới hạn có sẵn: thử chạy toàn bộ script SQL của dev trên DB trống gặp lỗi biên dịch batch
tham chiếu `BonusAmount`/`TotalAmount` tại migration `CounterTopUpBonuses`, trước phần gateway.
Không dùng kết quả nâng cấp nói trên làm bằng chứng script SQL toàn bộ chạy được từ DB trống;
đường EF migrations trên DB mới đã pass. Không sửa lỗi script lịch sử ngoài phạm vi ghép conflict.
Không gọi sandbox thật trong lượt này; kết quả còn chờ Trưởng nhóm xem xét.
