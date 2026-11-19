# SCRUM-31 — Phát hành, xem và in hóa đơn

Dùng [contract chứng từ](../../docs/contracts/inventory-invoice-voucher.md), nền CyberManagement hiện tại.
Hóa đơn món và hóa đơn tiền giờ phát hành riêng. Giá/tên/giảm phân bổ được chụp vào InvoiceDetails,
không đọc lại giá hoặc tên sản phẩm khi xem/in. Bản in là chứng từ thanh toán nội bộ.

## API và quyền

| Method | Route | Quyền |
| --- | --- | --- |
| GET | /api/admin/invoices | invoice.view; tối đa 500 mới nhất |
| GET | /api/admin/invoices/{id} | invoice.view |
| GET | /api/admin/invoices/{id}/print | invoice.view; HTML UTF-8, nội dung được encode |
| POST | /api/admin/invoices | invoice.create; header Idempotency-Key |
| GET | /api/client/invoices | Customer; tối đa 200 của chính mình |
| GET | /api/client/invoices/{id} | Customer sở hữu; người khác nhận 404 |
| GET | /api/client/invoices/{id}/print | Customer sở hữu; người khác nhận 404 |

Admin/Manager/Cashier theo catalog hiện tại có invoice.view/invoice.create. Waiter/Technician không có quyền.
Service còn kiểm tra Employee.IsActive và quyền hiện tại trong SQL, kể cả replay một khóa cũ.
CustomerId được xác minh từ nguồn khi phát hành hoặc lấy JWT khi khách xem; EmployeeId lấy từ principal.
Body không nhận tiền, CustomerId, EmployeeId hoặc ActorType. JSON chứa trường ngoài DTO bị từ chối.

Phát hành món (ID ví dụ phải thay bằng đơn SQL thật):

```http
POST /api/admin/invoices
Authorization: Bearer <token nhân viên>
Idempotency-Key: Invoice:checkout:12
Content-Type: application/json

{"kind":"Orders","orderIds":[12,18],"gamingSessionId":null}
```

Phát hành tiền giờ: `{"kind":"Session","orderIds":[],"gamingSessionId":7}`.
Phiên phải Ended, có EndTime và phí chưa có chứng từ đã có khoản thu xác minh đủ.
Các đơn món phải cùng khách/cùng phiên hoặc cùng không có phiên. Không trộn phí giờ vào hóa đơn món mới.
POST thành công trả 201 và Location của chi tiết. Retry cùng khóa/nguồn trả lại cùng InvoiceId;
đổi nội dung/nhân viên với cùng khóa trả 409. ID đơn được loại trùng và sắp tăng dần trước khi hash.
Idempotency-Key giữ nguyên khi mất response: 1..100 ký tự ASCII chữ/số/:/_/-, phân biệt hoa/thường.
Staff không được dùng tiền tố SessionAutoInvoice: hoặc SessionAutoClose:, kể cả khác hoa/thường.

400: input/header sai; 401: thiếu đăng nhập; 403: không đủ quyền; 404: không có/không sở hữu nguồn;
409: trùng nguồn/khóa khác nội dung, snapshot hoặc nguồn tiền không khớp.
Không có endpoint tùy ý sửa giá, đánh dấu Paid, hoàn tiền hoặc giả actor System.

## Ý nghĩa tiền

- Subtotal/Discount/GrossAmount là snapshot phát hành; GrossAmount = Subtotal - Discount.
- CancelledAmount là giá trị dòng đã hủy sau giảm; giữ riêng với thực hoàn.
- RefundAmount chỉ gồm hoàn Succeeded; Refund Pending vẫn hiển thị trong lịch sử nhưng chưa trừ tiền đã thu.
- PaidAmount là thực thu ròng: debit ví dịch vụ - credit hoàn ví + thu cash/gateway - hoàn cash/gateway.
- Với ví, lấy giá trị từ WalletTransactions đã đối chiếu; không cộng thêm Payment đối soát.
- AmountDue = max(0, GrossAmount - CancelledAmount - PaidAmount).
- OverpaidAmount thể hiện phần dư/cần đối soát; không tự cắt tiền thực thu hoặc giả đã hoàn.

Phát hành không debit ví, không tạo Payment/Refund và không gọi gateway; chỉ gắn InvoiceId cho khoản
Succeeded thuộc đúng nguồn chưa gắn hóa đơn. Không chuyển một Payment sang hóa đơn khác.
Nguồn ví thiếu/sai ledger, thu/hoàn sai nguồn hoặc tổng hoàn vượt thu trả 409 để đối soát, không giả số 0.
Chi tiết/bản in giữ giá và giảm gốc; trạng thái thu/hoàn đọc từ giao dịch đã xác minh hiện tại.
Danh sách, chi tiết và bản in đọc hóa đơn/thu/hoàn/ledger trong cùng transaction, giữ khóa Customer
đến khi đọc xong để tránh ghép số tiền trước và sau một lần hoàn. Tập hóa đơn được chốt theo ID,
khóa các khách theo CustomerId tăng dần trước khi đọc nguồn/Payment/Invoice; hóa đơn mới xuất hiện
trong lúc đọc sẽ được lấy ở lần tải sau. Lần đọc chỉ commit transaction do chính nó mở;
nếu caller đã mở transaction thì dùng chung và để caller quyết định commit/rollback.
Mỗi giao dịch trả paymentTime, confirmedAt, providerOccurredAt (có thể null) và paymentTimeBasis.
paidAt được giữ làm alias của paymentTime cho client cũ. UI và bản in hiển thị theo +07:00;
Basis=Confirmation được ghi rõ là mốc hệ thống xác nhận, không khẳng định thời điểm thực hoàn.
Nếu provider bổ sung bằng chứng thời gian sau đó, Basis và mốc ghi nhận đã lưu vẫn được giữ nguyên.
UI từ chối giá trị ngoài Number.MAX_SAFE_INTEGER để tránh làm tròn tiền ngầm; API và bản in HTML server dùng decimal(18,0).

## Điểm tích hợp nội bộ

IInvoiceService.IssueAsync dùng transaction có sẵn hoặc tự mở khi gọi từ API. Các module dùng chung
scoped DbContext; con không commit transaction của caller. Caller giữ khóa theo contract chung:
Customer → phiên/đơn/dòng/phí → Product tăng dần nếu cần → Promotion nếu cần → Payment → Invoice.
Lưu thay đổi nguồn/Payment/ledger trong transaction chung trước khi gọi module hóa đơn.
Repository chụp nguồn mới từ SQL dưới khóa và từ chối entity nguồn còn thay đổi chưa lưu.

- Đặt/thu món: lưu đơn, dòng và khoản thu theo nguồn, rồi IssueAsync nếu cần chứng từ.
- Callback/cash/refund: lưu trạng thái tiền/ledger đã xác minh, gọi ApplyFinancialChangeAsync(orderId, null)
  hoặc ApplyFinancialChangeAsync(null, chargeId) trong cùng transaction. Nguồn chưa có chứng từ trả null.
  Nguồn đã có chứng từ được liên kết đúng InvoiceId, cập nhật trạng thái, không tạo khoản tiền mới.
- Hủy từng dòng: caller Khang xác minh và lưu nghiệp vụ hủy/tồn/tiền trước, rồi gọi
  RecordCancelledOrderLinesAsync(orderId, cancelledOrderDetailIds) trong transaction chung.
  Chỉ gán giá trị hủy bằng snapshot dòng; gọi lặp không cộng lần hai. Đây là điểm gọi server,
  không là API cho khách tự hủy. Nếu chưa phát hành, caller giữ dữ liệu hủy trên nguồn để chụp khi phát hành.
- Hủy toàn đơn: nguồn Status=Cancelled được đối chiếu khi phát hành hoặc ApplyFinancialChange;
  các giá trị gốc giữ nguyên, giá trị hủy được ghi riêng.
- Phúc/worker: sau quyết toán phí cuối, debit/Payment, lưu Ended/EndTime và trạng thái máy trong
  transaction chung, gọi ISessionInvoiceIssuer.IssueForAutoClosedSessionAsync(sessionId).
  Worker nội bộ xác minh lý do hết tiền; service kiểm tra phiên đã kết thúc và số dư không dương,
  tạo khóa SessionAutoInvoice:{id}, hash v2 và actor System/EmployeeId null. Không có đường HTTP System.
  Phiên 0 đồng hoặc không còn phí chưa lên chứng từ trả null; replay khóa đã thành công trả InvoiceId cũ.
  Worker đóng phiên và adapter callback thuộc task Phúc/Minh/Khang; SCRUM-31 cung cấp điểm gọi để họ nối.

Audit phát hành System ghi rõ session, reason=InsufficientBalance và key=SessionAutoInvoice:{id}.
Replay dùng khóa đã lưu và không thêm audit phát hành lần hai.

Invoice/InvoiceOrders/InvoiceDetails/khóa phát hành/liên kết tiền/audit nằm trong cùng transaction.
Chỉ publish realtime sau khi caller commit. Unique nguồn và khóa bền vững bảo vệ cạnh tranh/restart.

## Migration và chạy

Hai migration mới AddInvoiceIssuingSnapshots và TrackOrderDetailCancellation (sau tích hợp dev: tổng 14 migration, 40 bảng) mở rộng schema:
Invoice.EmployeeId nullable, ActorType Staff/System; InvoiceDetail thêm voucher snapshot và CancelledAmount.
OrderDetail.Status theo contract checkout: Pending/Confirmed/Cancelled, default Pending. Service đối chiếu trạng thái này khi chụp/ghi hủy, kể cả hủy trước phát hành.
Hóa đơn cũ được backfill Staff qua default; giữ tiền/EmployeeId cũ, không tạo khóa hoặc voucher giả cho lịch sử.
Down từ chối khi có trạng thái dòng khác Pending, System hoặc snapshot/hủy mới để không mất metadata hay gán nhân viên giả.
Đối soát/lưu trữ metadata trước khi cân nhắc downgrade; không chạy trên DB chung nếu chưa được nhóm duyệt.
SQL/DBML đã export từ EF model. Không sửa migration đã áp trước đó.

API dùng connection/JWT User Secrets như các module hiện có. Khi Database:InitializeOnStartup=true,
F5 API sẽ áp migration mới trên database đã cấu hình. Trỏ database thử khi review.
Frontend chạy `npm ci` rồi `npm run dev` trong frontend; mở /admin/invoices hoặc /client/invoices,
đăng nhập đúng loại tài khoản. Danh sách/chi tiết đọc SQL, lỗi không dùng lại dữ liệu localStorage demo.

## Kiểm tra

Từ gốc repository sau khi bật MSSQLLocalDB:

```powershell
$env:AUTH_CHECKS_SQL_CONNECTION='Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks -- --invoice-only
node frontend/tests/run-invoices-live.mjs
```

Các fixture tạo database GUID riêng, migrate và tự dọn; giữ nguyên database ứng dụng.
Browser live kiểm tra login thật → phát hành UI → SQL → reload → đổi giá món → chi tiết/PDF giữ snapshot,
admin/client ownership, mobile và lỗi/rỗng/recovery. Không thay fetch hoặc inject principal trong suite live.
Fixture UI và unit client chạy riêng bằng `npm run test:invoices` và `node tests/run.mjs invoices` trong frontend.
