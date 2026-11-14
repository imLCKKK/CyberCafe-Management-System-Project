# SCRUM-5 - Implement-Customer-Wallet-Queries

Nhánh này bổ sung hồ sơ khách và query ví trên nền CyberManagement của nhóm.
Reviewer cuối là Trưởng nhóm; code/test không tự thay thế quyết định duyệt.

PR #17 tại `bd53a24` đã chép các file dùng chung từ baseline local cũ lên base `93cf457`.
Bản sửa sau review ghép lại catalog/kho và các kiểm thử đã có, giữ phần khách hàng.
Không dùng lại gói ZIP snapshot cũ để chép đè các file chung. Hai manifest/hash của gói cũ đã được gỡ
khỏi source vì không còn mô tả nhánh hiện tại; đánh giá thay đổi bằng Git diff theo commit.

## Tích hợp

- Application/Infrastructure DI giữ đồng thời catalog, kho, máy, auth và khách hàng.
- Ghép `dev` tại `4948b15` vào bản sửa SCRUM-5 `4816802`, giữ policy khách hàng và `RealtimeConnect`, cùng các kiểm thử realtime/catalog/khách.
- Vite dev/preview chuyển tiếp đủ `/api/auth`, `/api/admin`, `/api/menu`, `/api/customers` và HTTP/WebSocket `/hubs/realtime`; OP.GG dùng middleware riêng.
- Base đã có migration SCRUM-13 với 38 bảng, tiền `decimal(18,0)`; SCRUM-5 không thêm migration.
- Writer ví và realtime số dư thuộc S2-M-1; Hub/hook realtime chung được nhận từ SCRUM-8 trên dev. Xem [contract ví](Backend/docs/003-customer-wallet-contract.md).

## Kiểm tra từ gốc repo

```powershell
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx -m:1
.\scripts\npm-local.cmd --prefix frontend run build
.\scripts\npm-local.cmd --prefix frontend run test:catalog
.\scripts\npm-local.cmd --prefix frontend run test:customer-wallet
node --test frontend/tests/computer-proxy.test.mjs
$env:AUTH_CHECKS_SQL_CONNECTION = '<SQL Server thử, không dùng DB ứng dụng>'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks --no-build -- --catalog-only
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks --no-build -- --customer-only
.\scripts\npm-local.cmd --prefix frontend run test:catalog:sql
node frontend/tests/run-customers-live.mjs
node frontend/tests/run-realtime-live.mjs
Remove-Item Env:AUTH_CHECKS_SQL_CONNECTION
```

Hướng dẫn kho và browser SQL đã có được giữ tại [Backend README](Backend/README.md).
Runner realtime dùng SQL thật và hai browser context trên một máy; hướng dẫn hai máy LAN ở
[SCRUM-8](Backend/docs/004-realtime-lan-checks.md). Host realtime kiểm thử giữ đủ DI khách/kho sau khi ghép.
Các chế độ focused tách catalog/khách để kiểm chứng độc lập; chế độ mặc định vẫn chạy đầy đủ,
không bỏ qua `ComputerChecks` hoặc `RealtimeChecks`. Dev `4948b15` đã đổi fixture giá máy sang VND nguyên,
phù hợp `decimal(18,0)`, khắc phục lỗi phần lẻ đã ghi ở lượt review trước. Vẫn phải chạy bộ đầy đủ riêng;
không suy từ focused pass thành toàn bộ suite pass. Kết quả chạy lại gửi trực tiếp trong chat review.
