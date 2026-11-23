# SCRUM-23 — ZaloPay: nạp ví và điểm xác nhận Order

Task `S2-M-3`, base `432973f` (SCRUM-22 đã ghép dev `490303a`). Đối chiếu 18/11/2026.
Minh thực hiện; **Trưởng nhóm** duyệt cuối.
Lượt sửa review ngày **23–24/11/2026** dựa trên commit SCRUM-23 `662ca24`;
kết quả kiểm tra mới và giới hạn Sandbox thật được phân biệt bên dưới.

## Phạm vi

Nối receipt đã được adapter SCRUM-22 xác minh vào shared wallet writer hoặc Payment của Order.
Giữ secret ở server, kiểm tra chữ ký/ID/tiền, Query độc lập và không fallback mock.
Redirect và trạng thái Succeeded của receipt chưa đủ để UI báo đã cộng ví.

Theo phạm vi người dùng đã xác nhận ngày 18/11, cung cấp `IZaloPayOrderPaymentBridge` trước khi Khang có S2-K-2;
không tự xây checkout, giữ/trả tồn, giữ/trả voucher hoặc nhận làm món.
Order trong demo là **fixture nguồn theo contract**, không phải bằng chứng checkout hoàn chỉnh.

## Transaction và ghi nhận tiền

Receipt được commit trước. Consumer dùng transaction SQL riêng, khóa Customer → Order nếu có →
Intent → Payment/ledger. Lỗi ghi nghiệp vụ rollback toàn bộ nghiệp vụ và giữ receipt để retry.
Callback, Query, worker và retry đi qua cùng consumer.

| Nguồn | Tác động commit cùng transaction |
| --- | --- |
| TopUp | Shared writer Credit/TopUp, balance, một ledger, Intent.WalletTransactionId/AppliedAtUtc |
| Order | Payment Pending → Succeeded, Order AwaitingPayment → Pending, Intent.PaymentId/AppliedAtUtc |

TopUp dùng `SourceType=ZaloPayTopUp`, `SourceId=MerchantTransactionId`,
`OperationKey=ZaloPayTopUp:{MerchantTransactionId}`, actor System. Số tiền lấy từ snapshot server,
không áp tiền tặng nạp tại quầy. Không tạo Payment không có nguồn Order/Session;
nạp ví không được tính thêm thành doanh thu dịch vụ.

Order chỉ tự xác nhận Payment Pending đã đăng ký đúng nguồn/tiền/merchant, khách Active,
Order AwaitingPayment, còn hạn Order/intent/reservation, không có khoản thu thành công khác.
Không chuyển món sang Confirmed; stock/voucher không bị trừ thêm.
ConfirmedAt/PaymentTime dùng thời điểm Query xác minh đầu tiên, PaymentTimeBasis=Confirmation;
không coi server_time của Query là thời điểm thanh toán tại provider.

Replay giữ nguyên ID ledger/Payment và mốc xác minh/áp dụng. Worker phục hồi receipt Succeeded chưa áp dụng,
kể cả khi tắt provider. TopUp xác minh đúng hạn vẫn được phục hồi sau khi QR hết hạn.
Order vẫn phải còn hạn giữ chỗ lúc áp dụng để không khôi phục đơn đã trả giữ chỗ.

Sai tiền, khóa khách, muộn, đổi nguồn, đơn đóng/đã thanh toán hoặc xung đột mã gateway:
giữ proof để đối soát, không tự ghi tiền. Thiếu đăng ký checkout trả CheckoutRegistrationMissing,
giữ receipt để caller đăng ký rồi retry; không tạo Payment thay caller.
Hoàn gateway/hủy checkout hoàn chỉnh phụ thuộc Khang và các task đối soát sau.

## Điểm nối cho Khang

Interface Application `IZaloPayOrderPaymentBridge`:

```csharp
Task<Payment> RegisterAsync(int customerId, int orderId, int intentId,
    DateTimeOffset reservationExpiresAtUtc, CancellationToken cancellationToken);
Task<ZaloPaySettlementResult> ConfirmAsync(int intentId, DateTimeOffset now,
    CancellationToken cancellationToken);
```

Cả hai yêu cầu transaction SQL của caller; không gọi HTTP, SaveChanges hoặc commit thay caller.
Register tạo Payment Pending chuẩn. Confirm chỉ nhận receipt đã được adapter xác minh.
Consumer thông thường gọi `IZaloPaySettlementService.ApplyAsync`, tự sở hữu transaction và phát event sau commit.

Caller checkout cần:

1. Trong SQL, khóa Customer trước rồi Order và tài nguyên giữ chỗ; tính/lưu tổng tiền và snapshot,
   trạng thái AwaitingPayment, hạn giữ chỗ; commit.
2. Ngoài transaction, gọi IZaloPayPaymentService.CreateOrderAsync với UUID ổn định.
   Không giữ transaction SQL trong lúc gọi provider; số tiền lấy từ Order đã lưu.
3. Trong transaction SQL mới, Register đúng Customer/Order/Intent và hạn giữ chỗ thực tế;
   SaveChanges/commit. Chỉ đưa URL/QR cho khách sau đăng ký thành công.
4. Callback/Query xác minh receipt; consumer xác nhận Payment/Order và phát event sau commit.
   Checkout xử lý hết hạn/hủy và giải phóng giữ chỗ bằng lock Customer/Order tương ứng.

Không dùng Order legacy Pending/tiền mặt làm checkout ZaloPay mới. Bản S23 chỉ cho tạo intent khi
Order ở **AwaitingPayment**; Order Pending bị trả 409 trước khi lưu intent hoặc gọi provider.
Retry một intent chưa thanh toán cũng kiểm tra lại trạng thái nguồn: nguồn đã trở về Pending
không được mở hoặc phục hồi Create. Replay khoản đã áp dụng vẫn trả kết quả đã ghi nhận.
Danh sách Order ở UI S23 chỉ hiển thị intent đã đăng ký Payment chuẩn.

## API, UI và realtime

Giữ API Create/Get/Sync/callback của [SCRUM-22](007-zalopay-gateway-callbacks.md). Bổ sung API chính chủ:

| API | Kết quả |
| --- | --- |
| GET `/api/customers/me/payments/zalopay/config` | Enabled/giới hạn tiền, không secret |
| GET `/api/customers/me/payments/zalopay?page=1&pageSize=10&purpose=TopUp` | Danh sách có phân trang của chính khách; purpose TopUp/Order |

DTO thêm appliedAtUtc, walletTransactionId, paymentId, settlementFailureReason.
Succeeded + AppliedAtUtc + ID nghiệp vụ mới là đã áp dụng. Callback trả 1 khi đã áp dụng hoặc giữ để đối soát;
receipt chờ đăng ký checkout trả 0 để còn retry. Cấu hình tắt không ngăn phục hồi receipt cũ.

Callback đã qua MAC/metadata vẫn phải có transaction ID và amount khớp receipt của Query **trước**
khi gọi RecordReceiptAsync hoặc settlement. Nếu lệch, trả 2 và lưu
`RequiresReconciliation=true`, `FailureReason=CallbackReceiptMismatch`; không ghi receipt mới từ
kết quả lệch, không cộng ví, không xác nhận Payment/Order và không phát event hoàn tất nghiệp vụ.
Trạng thái đối soát này được lưu bền: callback gửi lại, sync và worker sau đó đều không tự áp dụng,
kể cả receipt hợp lệ đã được lưu từ trước nhưng chưa settle. Cần xử lý đối soát rõ ràng;
không xóa cờ hoặc thay key để tiếp tục thanh toán. Callback sai đến sau một khoản đã áp dụng
không làm hỏng receipt, ledger/Payment hoặc trạng thái đã commit của khoản đó.

Ví giữ nạp tại quầy và ZaloPay riêng. Request key và tiền được giữ trước khi gửi, retry dùng lại cùng khóa.
QR tạo cục bộ từ **đúng order_url provider trả về**, không dựa vào trang hosted bị kẹt loading.
QR chỉ xuất hiện khi Pending, còn hạn, không đối soát. UI có chờ/thất bại/hết hạn/đã xác minh đang ghi nhận/
đã cộng/cần kiểm tra. API lỗi không fallback số dư demo hoặc tự báo thành công.
QR hết hạn không quét lại; kiểm tra kết quả trước khi tạo lượt mới.

Sau commit mới phát WalletChanged cho chủ ví và quyền wallet.view, hoặc OrderChanged cho chủ đơn
và quyền order.view; thêm ZaloPayPaymentChanged cho chính chủ intent. Replay không phát lần ghi nhận mới.
UI nhận event rồi đọc lại API/SQL, reconnect cũng đọc lại; poll/sync dự phòng.

## Phục hồi Create chưa có kết quả

Intent được commit trước HTTP. Lần Create đầu và các đường Query dùng chung lease có token;
SaveCreateResultAsync kiểm tra token để phản hồi cũ không ghi đè lượt mới hoặc receipt đã ghi nhận.
Retry cùng idempotency key khi chưa có PaymentUrl/provider transaction đi vào Query ngay với mã cũ.
Chỉ Query **NotFound rõ ràng** mới cho phép phục hồi Create, giữ nguyên intent/merchant ID,
amount, app_time và deadline. Không giữ transaction SQL trong lúc gọi provider.

Trước khi gửi Create phục hồi, server kiểm tra lại khách Active, Order AwaitingPayment/đúng snapshot/
chưa thu thành công, còn ít nhất 300 giây và còn đúng ngày Việt Nam của merchant ID.
Nguồn đổi, thiếu hạn, đổi ngày hoặc Query chưa rõ kết quả thì giữ để đối soát; không tạo ID hay
một cửa sổ thanh toán mới. Query Pending nhưng thiếu URL dùng CheckoutUrlMissing;
không suy ra URL hoặc coi redirect là đã thanh toán.

Create phục hồi bị từ chối sau một lần gửi cũ chưa rõ vẫn giữ Pending + CreateRecoveryRejected
và tiếp tục Query; lần từ chối mới không chứng minh lần cũ chưa tồn tại. Intent Pending chưa có
kết luận không bị loại vì đã quá 24 giờ: worker vẫn Query theo lịch, giãn một giờ/lượt khi đã quá
24 giờ sau expiry. Receipt tìm thấy muộn giữ proof và đánh dấu đối soát, không hồi sinh nguồn đã hết hạn.

## Migration và kiểm tra

Migration mới `20261117121536_ZaloPayReceiptSettlement` thêm FK/index unique/check, liên kết nghiệp vụ,
thời điểm áp dụng, hạn giữ chỗ Payment. Không sửa migration đã áp dụng của task cũ.
Schema **41 bảng, 16 migrations**, không thêm bảng mới; legacy dùng cột nullable.
Down từ chối xóa liên kết nếu đã áp dụng/đăng ký/đối soát, không được bỏ guard để hạ schema có tiền.

Từ đúng root checkout S23, cài đủ cả dependency phát triển trước khi kiểm tra QR:

```powershell
.\scripts\npm-local.cmd --prefix frontend ci --include=dev
.\scripts\npm-local.cmd --prefix frontend run test:zalopay-payments
.\scripts\npm-local.cmd --prefix frontend run test:zalopay-payments:render
node --test frontend/tests/computer-proxy.test.mjs
.\scripts\npm-local.cmd --prefix frontend run build

# Output mới, đường dẫn ngắn; không dùng DLL đang bị host/build cũ giữ.
$s23Artifacts = Join-Path $env:TEMP ('cyber-s23-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
.\scripts\dotnet-local.cmd restore Backend\CyberManagement.slnx --artifacts-path $s23Artifacts
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx --artifacts-path $s23Artifacts --no-restore --disable-build-servers -p:UseSharedCompilation=false -nodeReuse:false
$s23Checks = Join-Path $s23Artifacts 'bin\CyberManagement.API.Checks\debug\CyberManagement.API.Checks.dll'
$env:AUTH_CHECKS_SQL_CONNECTION = 'Server=127.0.0.1\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True'
.\scripts\dotnet-local.cmd $s23Checks --gateway-only
.\scripts\dotnet-local.cmd $s23Checks --settlement-only
.\scripts\dotnet-local.cmd $s23Checks
```

`jsqr@1.4.0` đã có trong devDependencies và lockfile, `qrcode@1.5.4` có trong runtime dependencies.
Cài với omit-dev/NODE_ENV=production có thể thiếu decoder dùng cho test; `ci --include=dev`
đã được kiểm tra bằng bản giải nén mới từ Git và lockfile, không cần sửa manifest hoặc thay decoder bằng mock.
Build vào artifacts mới và tắt shared compiler/build servers cho lượt gọi này tránh tranh chấp DLL;
không dừng hàng loạt tiến trình dotnet/MSBuild của workspace khác.

SQL runner tạo/dọn DB thử riêng. Provider của ca SQL là **fixture nội bộ có nhãn**;
SQL repositories, shared writer, bridge, transactions, constraints và SignalR dùng implementation thật.

**Mốc kiểm tra 18/11/2026:** full SQL 1.054 ca trước khi thêm ca Down guard; focused cuối 318 ca gồm Down guard,
rollback, replay đồng thời, khôi phục, phân quyền event và giữ tồn/voucher; invoice 145, voucher 143, inventory 104;
frontend API/QR 12, proxy 3 và render trạng thái. Build .NET/frontend thành công.
Restore .NET ở thư mục dài có cảnh báo NU1900 vì không truy cập được NuGet audit lúc đó;
build Inventory với artifacts path ngắn đạt 0 warning/0 error. Windows có thể cần artifacts path ngắn
để nạp native SqlClient.SNI.dll, không sửa driver hay tắt kiểm tra SQL để né lỗi đường dẫn.

**Lượt sửa review 23–24/11/2026 trên base 662ca24:** không đổi schema, vẫn 41 bảng/16 migrations.

| Kiểm tra | Kết quả đã chạy |
| --- | --- |
| Build đầy đủ solution .NET vào artifacts mới, không dùng shared compiler/build servers | 0 warning, 0 error |
| Gateway HTTP/SQL tập trung | 363 checks pass |
| Settlement HTTP/SQL tập trung | 380 checks pass |
| API SQL tổng | 1.166 checks pass |
| API + QR từ cài dependency sạch có include-dev | 12/12 pass: 7 API và 5 QR; decoder jsQR độc lập đọc SVG đã render |
| Render trạng thái thanh toán | Pass |
| Frontend build | Pass |
| Vite dev/preview API và WebSocket proxy | 3/3 pass |

Các suite có phần trùng nhau, không cộng các con số thành số ca độc lập.
Các ca SQL mới kiểm tra receipt Query lệch callback cho cả TopUp/Order, không phát event/ghi tiền,
giữ cờ đối soát qua retry/worker, receipt đã commit không bị callback sai làm hỏng,
phục hồi Create cùng mã/lease, Order Pending bị từ chối và AwaitingPayment vẫn đi được.
Log cục bộ `.tools/review-20261123-*.log` và thư mục cài frontend sạch bị Git ignore;
đây là bằng chứng test nội bộ, chưa thay bằng chứng trả tiền trên app Sandbox.

## Sandbox thật và phần còn chờ

**Bằng chứng lịch sử 18/11/2026:** S23 đã gọi Create chính thức cho TopUp 20.000 và Order fixture 30.000: có order_url hợp lệ,
trạng thái ban đầu Pending. Callback qua HTTPS tạm trả 2 với fixture chữ ký sai.
Đối chiếu lại Query/SQL: cả hai intent đã Expired, chưa có receipt/AppliedAtUtc; balance 0,
ledger 0, Payment của Order vẫn Pending. Các mã này không được quét lại.
Đó là bằng chứng kết nối/không tự ghi nhận, **chưa là bằng chứng nhận tiền S23**.
Người dùng xác nhận lượt thành công trước là ở bản cũ và chưa thử lại S23.
Đây là trạng thái tại mốc 18/11, không dùng kết quả bản cũ thay bằng chứng của bản sửa.

Lúc `2026-11-23T21:20:25Z` (04:20 ngày 24/11 giờ Việt Nam), bản đã sửa gọi Create chính thức thành công
cho TopUp intent #5, 20.000đ và Order intent #4, nguồn Order #2, 30.000đ; cả hai có URL và đang Pending.
Callback HTTPS mới đã trả 2 với fixture MAC sai; listener realtime thật đã kết nối và đọc baseline
balance 0/ledger 0. Đây vẫn là bằng chứng chuẩn bị, chưa phải thanh toán thành công.
Sau đó người dùng tự tạo lượt nạp mới 50.000đ và xác nhận đã thanh toán thành công; kết quả đối chiếu dưới đây.

### TopUp đã thanh toán trên bản sửa ngày 24/11/2026

- Intent #6, purpose TopUp, 50.000đ, AppId Sandbox 554; provider transaction `261124000000004`.
- Receipt Succeeded được xác minh lúc `2026-11-23T21:35:51.8099482Z`, áp dụng lúc
  `2026-11-23T21:35:51.8431805Z` (04:35:51 ngày 24/11 giờ Việt Nam); không cần đối soát.
- SQL xác nhận ví khách fixture `0900000023` có số dư 50.000đ và đúng một ledger Credit/TopUp #1,
  SourceType ZaloPayTopUp. Intent.WalletTransactionId=1; TopUp không tạo Payment dịch vụ.
- Listener SignalR thật nhận WalletChanged lúc `21:35:51.986Z` và ZaloPayPaymentChanged lúc
  `21:35:51.992Z`; API đọc ngay sau event đã thấy balance 50.000/ledger count 1.
- Lúc `2026-11-23T22:16:19Z`, gọi Sync lại cùng intent ba lần giữ nguyên receipt, thời điểm áp dụng,
  ledger ID, số dư và tổng số ledger. Không ghi thêm lượt cộng.

Receipt được xác minh bằng Query; log đã thu chưa chứng minh callback thanh toán thật của provider.
Các fixture sai/lệch/lặp callback và concurrency vẫn có bằng chứng SQL ở bảng kiểm tra phía trên.
Log SQL, event và replay cục bộ trong `.tools/integration-sandbox/review-20261124-*` không chứa khóa,
MAC, JWT hoặc URL checkout trong phần bằng chứng đối chiếu.

Order intent #4, nguồn Order #2, 30.000đ đã Expired và Payment #2 vẫn Pending; **chưa có lượt Order
Sandbox thanh toán thành công trên bản sửa**. Bridge/settlement Order đã pass SQL fixture nhưng
chưa thay bằng chứng trả tiền thật hoặc checkout S2-K-2 hoàn chỉnh của Khang. Chưa xác minh trực tiếp
trên máy Trưởng nhóm. Giữ các giới hạn này khi review, không tự đánh dấu toàn task Done.
Sau khi xác nhận nạp ví, người dùng yêu cầu commit/push bản sửa; Trưởng nhóm quyết định duyệt cuối.

Demo giữ database/ID trong `.tools/integration-sandbox/` (Git ignore):

```powershell
# Secret server cấu hình theo S1-M-2; tunnel HTTPS tới callback-only 5100.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-zalopay-integration.ps1 -ArtifactsPath $s23Artifacts -CallbackUrl https://YOUR-TUNNEL/api/payments/zalopay/callback
# Chuẩn bị Order fixture hạn 15 phút, chỉ chạy khi sắp thanh toán thử:
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-zalopay-integration.ps1 -ArtifactsPath $s23Artifacts -PrepareOrder -CallbackUrl https://YOUR-TUNNEL/api/payments/zalopay/callback
# Nếu provider đã xác minh fixture trước Expired và chưa trả tiền, tạo nguồn thử mới có lưu lịch sử:
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-zalopay-integration.ps1 -ArtifactsPath $s23Artifacts -PrepareOrder -NewOrderFixture -CallbackUrl https://YOUR-TUNNEL/api/payments/zalopay/callback
```

Ví `http://localhost:5177/client/wallet`, API loopback 5099, callback-only loopback 5100 từ chối route khác.
Không tunnel cổng API admin. Private fixture: `0900000023` / `SandboxCustomer-2026!`.
Chỉ dùng app/số dư Sandbox. Script không tải công cụ, sửa User Secrets hoặc dừng process chiếm port.
Không tạo lại DB đã mất sau thanh toán, xóa state hoặc dùng khóa mới để che lỗi lượt cũ.
`-NewOrderFixture` chỉ hoạt động sau khi Query provider xác nhận nguồn trước Expired, không có receipt/
provider transaction/AppliedAtUtc và không cần đối soát. Nguồn SQL cũ vẫn giữ nguyên;
state lưu PreviousOrderIds để đối chiếu, không ghi đè lịch sử bằng nguồn mới.
