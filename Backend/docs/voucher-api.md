# Voucher API — SCRUM-32

Rule chung: [contract kho/chứng từ/voucher](../../docs/contracts/inventory-invoice-voucher.md#4-quy-tắc-voucher).
Không thêm migration: dùng Promotions, PromotionUsages và snapshot Order/OrderDetail hiện có.

## Endpoint và quyền

| Method | Route | Quyền | Thành công |
| --- | --- | --- | --- |
| GET | /api/admin/vouchers | promotion.view | 200 danh sách |
| GET | /api/admin/vouchers/{id} | promotion.view | 200 chi tiết |
| POST | /api/admin/vouchers | promotion.manage | 201, Location tới chi tiết |
| PUT | /api/admin/vouchers/{id} | promotion.manage | 200 |
| PUT | /api/admin/vouchers/{id}/status | promotion.manage | 200 |
| POST | /api/client/vouchers/preview | Customer, ID từ JWT | 200 tính thử |

Admin/Manager quản lý; Cashier chỉ đọc theo catalog hiện tại. Waiter/Technician không có quyền voucher.
Role tùy chỉnh quyết định theo permission. Không có DELETE; tắt voucher để giữ lịch sử.
400: dữ liệu sai; 401: chưa đăng nhập; 403: thiếu quyền; 404: không có voucher/món; 409: mã trùng hoặc không đủ điều kiện.

Body tạo/sửa (PUT gửi đủ trường; null là không đặt giới hạn tương ứng):

```json
{
  "code": "SALE10", "name": "Giảm 10% tiền món",
  "discountType": "Percent", "discountValue": 10,
  "minOrderAmount": 0, "maxDiscountAmount": null,
  "startDate": "2026-11-01T00:00:00+07:00",
  "endDate": "2026-12-01T00:00:00+07:00",
  "totalUsageLimit": null, "perCustomerUsageLimit": null,
  "isActive": true
}
```

Body bật/tắt: `{ "isActive": false }`.
Preview (thay productId bằng món thật):

```json
{ "code": "SALE10", "items": [{ "productId": 1, "quantity": 2 }] }
```

Preview đọc giá/trạng thái món và danh mục từ SQL, không nhận giá/tổng tiền/CustomerId từ body.
Preview không giữ lượt; hết lượt sau preview thì đặt đơn vẫn có thể bị từ chối.
Hiệu lực StartDate <= now < EndDate. Percent tối đa hai chữ số thập phân; Fixed và tiền VND nguyên đồng.
Phân bổ giảm theo tỷ lệ; đồng dư ưu tiên phần lẻ lớn, hòa theo OrderDetailId tăng dần.

## Điểm gọi cho đặt/hủy đơn

Inject IVoucherService và dùng cùng scoped DbContext/transaction với đơn, ví và kho.
Thứ tự khóa: Customer → nguồn phiên/đơn → Product tăng dần → Promotion → nguồn tiền/chứng từ theo contract chung.
Caller giữ các khóa nguồn trước khi gọi rule; không mở transaction voucher độc lập để ghi lượt cho đơn.

1. Khóa và kiểm tra khách/nguồn, đọc giá server; tạo đơn và dòng món.
2. SaveChanges trong transaction chung để có ID và lưu hết thay đổi nguồn.
3. Gọi `ValidateAndUseAsync(orderId, code, customerId, cancellationToken)`.
4. Service khóa lại đơn/dòng món và voucher, kiểm tra quota, lưu snapshot/giảm phân bổ/tổng sau giảm và một usage.
5. Hoàn tất các thay đổi nghiệp vụ còn lại theo thứ tự khóa đã chốt; lỗi thì caller rollback toàn bộ.
6. Chỉ caller commit; service không commit transaction của đơn.

Không thay đổi entity nguồn trước khi gọi lại rule mà chưa SaveChanges.
Repository đọc dữ liệu mới dưới khóa, từ chối thay đổi chưa lưu trước khóa để tránh dùng dữ liệu cũ/ghi đè ngầm.
Retry cùng OrderId/mã trả snapshot đã lưu, không tính lại rule hoặc thêm usage.
Có usage (kể cả đã hoàn) thì mã voucher bất biến; sửa tên/rule không sửa đơn cũ.

Khi hủy toàn đơn, caller xác minh chính sách hủy/tiền/tồn, khóa nguồn và lưu Status=Cancelled trong transaction chung,
rồi gọi `ReleaseForCancelledOrderAsync(orderId, cancellationToken)`.
Lần đầu true, gọi lại false. Hoàn lượt cập nhật SQL trực tiếp trong transaction, không cần SaveChanges riêng cho ReleasedAt.
Hủy một phần không gọi hoàn lượt; snapshot và giảm phân bổ giữ nguyên.
Rule voucher không thu/hoàn tiền, trừ/trả tồn hoặc đổi trạng thái đơn thay caller.
Task checkout nối đường đặt/hủy đơn thực tế; bộ kiểm tra này xác minh điểm gọi qua service.

## Kiểm tra tự động

Chạy từ gốc repository sau khi bật MSSQLLocalDB:

```powershell
$env:AUTH_CHECKS_SQL_CONNECTION='Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks -- --voucher-only
```

Chỉ dùng database GUID do fixture tạo, migrate mới và tự dọn; không sửa database ứng dụng.
Kiểm tra quyền HTTP, validation, giá server, trần/làm tròn/phân bổ, hạn mức toàn hệ thống/từng khách,
retry, snapshot sau sửa rule, hoàn lượt một lần, rollback chung và hai DbContext tranh lượt cuối.
