# SCRUM-5 — Implement-Customer-Wallet-Queries

Gói bàn giao ngày 08/11/2026, phạm vi S1-M-1, Owner Minh.
Phần triển khai đã hoàn tất để gửi review; Trưởng nhóm là người duyệt cuối.
Không tự đánh dấu Jira Done hoặc coi các owner đã xác nhận contract.

## Gói này dùng như thế nào?

Đây là **gói 33 file của SCRUM-5 để ghép vào repo nền**, không phải project độc lập.
Nền cần có React/Vite + CyberManagement .NET 10, auth/quyền và database SQL Server 35 bảng.
Không chép ZIP này lên Git thay cho source; giải nén và đưa source vào nhánh SCRUM-5 của repo đã clone.

1. Mở đúng repo Git đã clone và chọn/tạo nhánh Jira SCRUM-5 theo quy trình của nhóm.
   Thư mục nguồn dùng để đóng gói không có `.git`, nên gói này không có lịch sử/nhánh/commit.
2. Đọc `SCRUM-5-FILES.txt`. Đặt các file theo đúng đường dẫn tương đối từ gốc repo.
   Những file đã có trên nhánh đích cần so sánh và merge; giữ thay đổi của người khác.
3. Đặc biệt merge các file dùng chung ở mục dưới; không chép đè cả file mới hơn từ nhánh đích.
4. Chạy build/test, xem `git status`, `git diff` và `git diff --check`.
   Trong Source Control, stage đúng các thay đổi SCRUM-5 đã kiểm tra, rồi commit/push nhánh để review.
   Commit message gợi ý: `SCRUM-5 Implement-Customer-Wallet-Queries`.

## File dùng chung cần merge

- Application/DependencyInjection.cs: đăng ký ICustomerService → CustomerService.
- Infrastructure/DependencyInjection.cs: đăng ký ICustomerRepository → CustomerRepository.
- API/Security/ApiPolicies.cs: thêm CustomerView, CustomerCreate, WalletView.
- tests/CyberManagement.API.Checks/Program.cs: đăng ký repository, gọi CustomerChecks và dispatch host browser.
- tests/CyberManagement.API.Checks/ComputerBrowserHost.cs: thêm repository khách để DI Development hợp lệ.
- frontend/vite.config.js: proxy `/api/customers` bên cạnh auth/admin; giữ middleware OP.GG.
- frontend/package.json: thêm script `test:customer-wallet`; không có dependency mới.
- Backend README, contract checkout/kho, backlog và kế hoạch: merge ghi chú/link SCRUM-5;
  giữ quyết định và lịch sử của task khác. Các bản nguyên file trong ZIP dùng để đối chiếu.

## Đã làm

- Admin tạo/đọc/tìm kiếm/phân trang khách; mật khẩu hash, ví mới 0, trạng thái Active.
- Client đọc hồ sơ/ví chính chủ từ JWT và SQL; kiểm tra quyền ở server.
- Số dư và ledger cùng snapshot SQL, lịch sử phân trang; UTC và hiển thị giờ Việt Nam.
- UI loading/error/empty/retry; API lỗi không lấy số dư hoặc hồ sơ demo.
- Contract credit/debit/refund, nguồn/khóa chống lặp và transaction chung ở
  `Backend/docs/003-customer-wallet-contract.md`.
- Người dùng chọn nạp quầy thu tiền trước khi cộng ví, không nạp nợ.

Không có migration mới. Writer nạp/chi/hoàn ví và realtime thuộc S2-M-1, chưa triển khai ở SCRUM-5.
Contract bàn giao không thay thế bằng chứng writer tiền hoặc xác nhận liên module.

## Kiểm tra

Đã chạy trên workspace nguồn ngày 08/11/2026: backend build, frontend build/render,
205 checks InMemory, 215 checks SQL Server, 6 frontend tests và browser UI–API–SQL.
Trước đóng gói đã kiểm tra file/DI/proxy còn đủ và chạy lại 6 frontend tests: pass.
Gói ZIP được đối chiếu SHA-256 với source; chưa áp dụng và chạy kiểm tra trên nhánh Git đích của bạn.

Chạy từ gốc repo sau khi merge:

```powershell
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx
.\scripts\dotnet-local.cmd run --project Backend\tests\CyberManagement.API.Checks
.\scripts\npm-local.cmd --prefix frontend run build
.\scripts\npm-local.cmd --prefix frontend run test:customer-wallet
```

Để kiểm tra SQL/browser, cấu hình `AUTH_CHECKS_SQL_CONNECTION` theo `Backend/README.md`
và chạy `node frontend/tests/run-customers-live.mjs`. Runner chỉ tạo/dọn database thử riêng.
SQL Server, .NET 10, Node 22+ và Chrome/Edge là công cụ của máy chạy, không nằm trong ZIP.
Các warning SSR của render và warning localStorage của Node đã có nhưng không làm các kiểm tra trên fail.

## Không nằm trong gói

Không có `.git`, `.tools`, node_modules, bin/obj/dist, log, database/backup, cấu hình local,
JWT key triển khai hoặc script/mật khẩu tài khoản manual demo trên máy nguồn.
Các giá trị hard-code trong project checks chỉ là fixture kiểm thử, không phải cấu hình API triển khai.
Tài khoản test thủ công của máy nguồn không tự được tạo khi giải nén gói này;
cấu hình seed Development hoặc tạo khách qua API/UI trên DB thử của máy đích.

`SHA256SUMS.txt` ghi hash của 33 file source. Ba file hướng dẫn/danh sách/hash phục vụ bàn giao,
không bắt buộc commit vào repo ứng dụng.