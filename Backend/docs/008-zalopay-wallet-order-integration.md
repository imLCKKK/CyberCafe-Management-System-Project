# SCRUM-23 — ZaloPay: nạp ví và điểm xác nhận Order

Task `S2-M-3`, base `432973f` (SCRUM-22 đã ghép dev `490303a`). Đối chiếu 18/11/2026.
Minh thực hiện; **Trưởng nhóm** duyệt cuối.

## Phạm vi

Nối receipt đã được adapter SCRUM-22 xác minh vào shared wallet writer hoặc Payment của Order.
Giữ secret ở server, kiểm tra chữ ký/ID/tiền, Query độc lập và không fallback mock.
Redirect và trạng thái Succeeded của receipt chưa đủ để UI báo đã cộng ví.

Người dùng xác nhận Khang chưa có S2-K-2: cung cấp `IZaloPayOrderPaymentBridge` trước;
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

Không dùng Order legacy Pending/tiền mặt làm checkout ZaloPay mới. Endpoint adapter S22 vẫn có thể
tạo receipt cho nguồn legacy, nhưng consumer S23 từ chối tự áp dụng nếu không AwaitingPayment/không đăng ký.
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

Ví giữ nạp tại quầy và ZaloPay riêng. Request key và tiền được giữ trước khi gửi, retry dùng lại cùng khóa.
QR tạo cục bộ từ **đúng order_url provider trả về**, không dựa vào trang hosted bị kẹt loading.
QR chỉ xuất hiện khi Pending, còn hạn, không đối soát. UI có chờ/thất bại/hết hạn/đã xác minh đang ghi nhận/
đã cộng/cần kiểm tra. API lỗi không fallback số dư demo hoặc tự báo thành công.
QR hết hạn không quét lại; kiểm tra kết quả trước khi tạo lượt mới.

Sau commit mới phát WalletChanged cho chủ ví và quyền wallet.view, hoặc OrderChanged cho chủ đơn
và quyền order.view; thêm ZaloPayPaymentChanged cho chính chủ intent. Replay không phát lần ghi nhận mới.
UI nhận event rồi đọc lại API/SQL, reconnect cũng đọc lại; poll/sync dự phòng.

## Migration và kiểm tra

Migration mới `20261117121536_ZaloPayReceiptSettlement` thêm FK/index unique/check, liên kết nghiệp vụ,
thời điểm áp dụng, hạn giữ chỗ Payment. Không sửa migration đã áp dụng của task cũ.
Schema **41 bảng, 16 migrations**, không thêm bảng mới; legacy dùng cột nullable.
Down từ chối xóa liên kết nếu đã áp dụng/đăng ký/đối soát, không được bỏ guard để hạ schema có tiền.

Từ đúng root checkout S23:

```powershell
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx
$env:AUTH_CHECKS_SQL_CONNECTION = 'Server=127.0.0.1\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True'
.\scripts\dotnet-local.cmd run --no-build --project Backend\tests\CyberManagement.API.Checks -- --settlement-only
.\scripts\dotnet-local.cmd run --no-build --project Backend\tests\CyberManagement.API.Checks
.\scripts\npm-local.cmd --prefix frontend run test:zalopay-payments
.\scripts\npm-local.cmd --prefix frontend run test:zalopay-payments:render
node --test frontend/tests/computer-proxy.test.mjs
.\scripts\npm-local.cmd --prefix frontend run build
```

SQL runner tạo/dọn DB thử riêng. Provider của ca SQL là **fixture nội bộ có nhãn**;
SQL repositories, shared writer, bridge, transactions, constraints và SignalR dùng implementation thật.
Đã pass: full SQL 1.054 ca trước khi thêm ca Down guard; focused cuối 318 ca gồm Down guard,
rollback, replay đồng thời, khôi phục, phân quyền event và giữ tồn/voucher; invoice 145, voucher 143, inventory 104;
frontend API/QR 12, proxy 3 và render trạng thái. Build .NET/frontend thành công.
Restore .NET ở thư mục dài có cảnh báo NU1900 vì không truy cập được NuGet audit lúc đó;
build Inventory với artifacts path ngắn đạt 0 warning/0 error. Windows có thể cần artifacts path ngắn
để nạp native SqlClient.SNI.dll, không sửa driver hay tắt kiểm tra SQL để né lỗi đường dẫn.

## Sandbox thật và phần còn chờ

S23 đã gọi Create chính thức cho TopUp 20.000 và Order fixture 30.000: có order_url hợp lệ,
trạng thái ban đầu Pending. Callback qua HTTPS tạm trả 2 với fixture chữ ký sai.
Đối chiếu lại Query/SQL: cả hai intent đã Expired, chưa có receipt/AppliedAtUtc; balance 0,
ledger 0, Payment của Order vẫn Pending. Các mã này không được quét lại.
Đó là bằng chứng kết nối/không tự ghi nhận, **chưa là bằng chứng nhận tiền S23**.
Người dùng xác nhận lượt thành công trước là ở bản cũ và chưa thử lại S23.
Còn chờ quét mới và đối chiếu SQL một lần ghi có ví/ledger, một Payment Order và UI sau commit.
Không dùng kết quả bản cũ thay bằng chứng của bản này; chưa đánh dấu toàn task Done.

Demo giữ database/ID trong `.tools/integration-sandbox/` (Git ignore):

```powershell
# Secret server cấu hình theo S1-M-2; tunnel HTTPS tới callback-only 5100.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-zalopay-integration.ps1 -CallbackUrl https://YOUR-TUNNEL/api/payments/zalopay/callback
# Chuẩn bị Order fixture hạn 15 phút, chỉ chạy khi sắp thanh toán thử:
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-zalopay-integration.ps1 -PrepareOrder -CallbackUrl https://YOUR-TUNNEL/api/payments/zalopay/callback
```

Ví `http://localhost:5177/client/wallet`, API loopback 5099, callback-only loopback 5100 từ chối route khác.
Không tunnel cổng API admin. Private fixture: `0900000023` / `SandboxCustomer-2026!`.
Chỉ dùng app/số dư Sandbox. Script không tải công cụ, sửa User Secrets hoặc dừng process chiếm port.
Không tạo lại DB đã mất sau thanh toán, xóa state hoặc dùng khóa mới để che lỗi lượt cũ.
