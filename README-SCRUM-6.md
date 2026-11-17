# SCRUM-6 — Configure-ZaloPay-Sandbox-Contracts

Gói mã nguồn đầy đủ để bàn giao **SCRUM-6 / S1-M-2**. Nhánh local:
`SCRUM-6-Configure-ZaloPay-Sandbox-Contracts`, nền `d2141da` (SCRUM-5 đã ghép dev/realtime).
Chưa commit hoặc push. Reviewer cuối là Trưởng nhóm.

## Phạm vi bàn giao

- Cấu hình server `Payments:ZaloPaySandbox` qua User Secrets/environment, validation URL/môi trường và giữ khóa ở server.
- Contract TopUp/Order: nguồn/ID/số tiền/trạng thái, chống xử lý lặp, callback/query và ranh giới ghi tiền.
- HMAC create/query/callback, 77 kiểm tra fixture nội bộ.
- Script cấu hình và probe HTTPS riêng trên loopback 5090; probe không ghi ví/đơn hoặc xác nhận thu tiền.
- Giữ nguyên policy/hub, kiểm thử realtime, DI catalog/kho/khách và proxy của nền `d2141da`.

Luồng QR, controller nạp ví, migration TopUp và ghi tiền thực tế thuộc **S2**; source đó đang được giữ
riêng trong checkout `zalopay-topup` của workspace gốc, không trộn vào PR contract SCRUM-6.
Gói này không phải bản UI nạp ví đã chạy trên cổng 5175.

## Cách kiểm tra

Chạy từ chính thư mục chứa README này; cần .NET 10 SDK. Thư viện frontend giữ lockfile của base.

```powershell
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx -m:1
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks --no-build -- --zalopay-checks
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks --no-build
```

Các fixture không gọi provider và không ghi tiền. SQL checks tạo/dọn database thử ngẫu nhiên riêng:

```powershell
$env:AUTH_CHECKS_SQL_CONNECTION = 'Server=127.0.0.1\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True'
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks --no-build
Remove-Item Env:AUTH_CHECKS_SQL_CONNECTION
```

Kết quả kiểm tra lại ngay trên gói này ngày 10/11/2026:

- Build solution: 0 warning, 0 error.
- 77 kiểm tra fixture ZaloPay: pass.
- 402 kiểm tra API/persistence trên InMemory: pass.
- 412 kiểm tra API/persistence/contract trên SQL Server: pass; database thử đã tự dọn.

Để kiểm tra HTTPS, cài `cloudflared` chính thức trong PATH hoặc `.tools/scrum6/cloudflared.exe`,
rồi chạy theo [Backend README](Backend/README.md#s1-m-2-zalopay-sandbox). User Secrets không đi cùng Git.
Nếu còn probe S1 từ checkout khác giữ cổng 5090, dừng nó từ đúng checkout đã mở trước khi chạy probe mới.
Không cần dừng API nạp ví S2 ở cổng 5091 để đọc hoặc build gói SCRUM-6 này.

## Giới hạn và bằng chứng

[Contract 005](Backend/docs/005-zalopay-sandbox-contract.md) ghi rõ ngày/phạm vi của HTTPS probe,
Query thật và fixture nội bộ. Bằng chứng S1 cũ được chạy trên `4816802`; gói này đã ghép lên `d2141da`.
Lỗi ComputerChecks `10000.50` trước đây đã được nền mới sửa, không ghi nó là blocker hiện tại.
Callback HTTPS qua tunnel tạm hiện chưa được xác nhận ổn định; cấp URL và kiểm tra lại khi demo.
Không coi probe/fixture là callback thanh toán thật hoặc tự tick Done trước khi Trưởng nhóm duyệt.

## Đưa lên Git

Thư mục này đã là Git repository riêng, có remote project và nhánh SCRUM-6; không cần `git init`.
Mở terminal **trong thư mục này**, kiểm tra rồi tự commit/push:

```powershell
git status
git diff --stat
git add .
git commit -m "SCRUM-6: configure ZaloPay sandbox contracts"
git push -u origin HEAD
```

Đưa source của repository này lên nhánh; không chép cả thư mục `srum 6-...` thành một thư mục con
trong project khác. Nếu remote từ chối do đã có thay đổi, fetch/ghép theo workflow nhóm; không force push.
`core.longpaths=true` đã đặt riêng cho repository vì tên thư mục bàn giao dài trên Windows.
Không đưa `.tools`, bin/obj, node_modules, log/state, `appsettings.*.local`, User Secrets, keys hoặc cloudflared.exe lên Git.
