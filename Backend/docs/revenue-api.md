# SCRUM-33 — Báo cáo doanh thu và đối soát

## API và quyền

| Method/route | Kết quả |
| --- | --- |
| `GET /api/admin/reports?from=2026-11-01&to=2026-11-16` | Tổng thu/hoàn/ròng, nạp ví, nguồn Food/Gaming và từng ngày |
| `GET /api/admin/reports/reconciliation?from=2026-11-01&to=2026-11-16` | Sai lệch, tham chiếu nguồn, giao dịch và thống kê căn cứ thời gian Confirmation |

Cả hai route yêu cầu JWT nhân viên và `permission:report.view`. Catalog hiện cấp cho Admin/Manager.
Service đọc lại trạng thái nhân viên và quyền trong SQL; token cũ không vượt quyền đã bị thu hồi.
EmployeeId lấy từ `sub`, không nhận ID do browser gửi; Customer không dùng route quản trị.
Phản hồi dùng `Cache-Control: no-store`.

`from/to` dạng `YYYY-MM-DD`, gồm cả hai ngày theo UTC+07:00; truy vấn từ đầu ngày from đến
trước đầu ngày sau to. Tối đa 366 ngày. Bỏ to dùng ngày hiện tại Việt Nam; bỏ from dùng đầu tháng
của to đã chuẩn hóa. Ngày rỗng/sai định dạng/trùng tham số, from > to hoặc khoảng quá dài trả 400.

## Nguồn số liệu

| Chỉ tiêu | Nguồn và thời gian |
| --- | --- |
| Thu/hoàn món qua ví | Ledger OrderPayment âm / OrderRefund dương; truy đúng đơn/dòng và debit gốc; ngày CreatedAt UTC đổi sang +07 |
| Tiền giờ qua ví | Ledger GamingCharge nguồn SessionCharge; Payment Wallet chỉ đối soát, không cộng thêm |
| Cash/ZaloPay món | Payments Succeeded; Type Payment cộng, Refund trừ; ngày PaymentTime |
| Nạp quầy | CounterTopUpRequests Approved, ApprovedAt, CashAmountReceived đối chiếu Amount; không cộng BonusAmount/TotalAmount như tiền thực nhận |
| Nạp ZaloPay | Ledger TopUp/Credit nguồn ZaloPayTopUp đã được writer xác nhận; báo riêng, không là doanh thu dịch vụ |
| Hóa đơn/phí phiên | Snapshot và liên kết dùng đối chiếu; không cộng tổng hóa đơn hoặc SessionCharge.Amount lần thứ hai |

NetRevenue = thu dịch vụ từ nguồn đã chuẩn hóa - hoàn dịch vụ. Pending/Failed không được tính là
đã thu/hoàn; Refund Pending chỉ giữ hạn mức. Khoản thu chưa có InvoiceId vẫn được tính đúng nguồn.
Hoàn trong kỳ có thể cần khoản thu gốc ngoài kỳ; service lọc ngày khi cộng, không cộng mọi dòng truy vấn trả về.
Nguồn giải đấu TournamentFee không nằm trong báo cáo Food/Gaming này.

Mỗi giao dịch đối soát có EntityType/EntityId, OrderId/OrderDetailId/SessionChargeId, Method/Type/Status,
Amount và PaymentTime/ConfirmedAt/ProviderOccurredAt/PaymentTimeBasis. ZaloPay không có giờ provider
được giữ Basis=Confirmation và PaymentTime=ConfirmedAt; không giả ngày thực hoàn. ConfirmationSummary
chỉ thống kê khoản thành công trong kỳ dùng căn cứ này. Bằng chứng provider đến sau không tự đổi kỳ báo cáo.
Payment đối soát ví có thể đang lưu Basis=Confirmation từ điểm quyết toán phiên hiện có; báo cáo vẫn lấy
CreatedAt của ledger làm thời gian nguồn và Basis=Source cho dòng WalletTransaction. Chỉ chấp nhận khi
PaymentTime khớp ledger và ConfirmedAt/Basis nhất quán. Nguồn hoàn dạng OrderId:OrderDetailId được đọc
trực tiếp; dạng ID dòng do SCRUM-31 hiện bàn giao chỉ được đọc qua FK Payment chứng minh đủ đơn/dòng
và debit gốc. Không đoán nguồn từ nội dung mô tả.

## Đối soát và lỗi

- Kiểm tra snapshot đơn, xuất/hoàn kho, số học tồn trước/sau, usage/hoàn lượt voucher, nguồn thu/hoàn,
  hạn mức hoàn, liên kết Payment/ledger/hóa đơn và snapshot các dòng hóa đơn.
- Nghĩa vụ hóa đơn = TotalAmount - tổng InvoiceDetail.CancelledAmount. Hóa đơn Paid phải có thu ròng
  đủ nghĩa vụ còn hiệu lực; khoản dư sau hủy được báo InvoiceRefundUnresolved cho đến khi thực hoàn.
- OrderDetail.Status xác định dòng đã hủy và yêu cầu hoàn tồn. Return không tự làm dòng món thành Cancelled.
  Đối chiếu CancelledAmount với trạng thái nguồn; tiền thực hoàn không thay thế snapshot tiền hủy.
  Dòng chưa hủy mà đã Return, dòng đã hủy thiếu Return hoặc snapshot hủy chưa đồng bộ đều được báo rõ.
- Snapshot voucher trên hóa đơn được đối chiếu với snapshot đơn; không dùng promotion/giá sản phẩm hiện tại.
- CheckedOrders/CheckedSessionCharges/CheckedInvoices đếm nguồn thực sự được kiểm tra, gồm nguồn
  liên quan ngoài kỳ. Transactions cũng giữ dữ liệu liên quan để truy vết; không phải danh sách chỉ trong kỳ.
- Chưa xác định nguồn/căn cứ thời gian tiền: báo cáo tổng trả 409, đối soát trả 200 với Issues và tham chiếu.
  Không dùng Description để đoán nguồn và không đổi giá trị chưa biết thành số 0.
- Truy vấn chỉ đọc, không tự sửa tiền/tồn/lượt/chứng từ. Khóa khách theo ID tăng dần trong transaction
  đọc riêng; writer tuân theo khóa khách sẽ phải chờ. Không gọi báo cáo trong transaction ghi nghiệp vụ.

401: thiếu/token không hợp lệ. 403: sai loại tài khoản, bị khóa hoặc thiếu quyền hiện tại. 400: bộ lọc sai.
409: chưa đủ căn cứ báo cáo tiền; dùng endpoint reconciliation để xem chi tiết.

## Kiểm thử

Chạy từ repository root:

```powershell
.\scripts\dotnet-local.cmd run --project Backend/tests/CyberManagement.API.Checks -- --revenue-service-checks
.\scripts\dotnet-local.cmd run --project Backend/tests/CyberManagement.API.Checks -- --revenue-query-checks
$env:AUTH_CHECKS_SQL_CONNECTION = 'YOUR_SQL_SERVER_CONNECTION'
.\scripts\dotnet-local.cmd run --project Backend/tests/CyberManagement.API.Checks -- --revenue-sql-checks
.\scripts\dotnet-local.cmd run --project Backend/tests/CyberManagement.API.Checks -- --revenue-api-checks
Remove-Item Env:AUTH_CHECKS_SQL_CONNECTION
```

Service checks dùng fixture qua interface; query checks dùng InMemory cho phần đọc dữ liệu và DI.
SQL/API checks bắt buộc SQL Server, tạo và dọn database GUID riêng, không dùng database ứng dụng.
API checks dùng JWT fixture đã ký qua middleware thật và repository/SQL thật. SQL checks còn gọi các
service đã có: phát hành hóa đơn từ khoản thu sẵn, ghi snapshot hủy, ApplyReturn, shared wallet refund,
ApplyFinancialChange và replay. Nguồn đơn ban đầu là fixture, không phải API checkout.
Chưa có API checkout đơn/worker phiên đầy đủ của Khang/Phúc trên nền dev đã tích hợp; SCRUM-33 không
triển khai thay hai phần này. Kiểm thử đầu-cuối với đơn/phiên thật chờ các owner bàn giao.
Không có lời gọi ZaloPay sandbox trong các ca báo cáo; trạng thái gateway là fixture đã lưu SQL.
