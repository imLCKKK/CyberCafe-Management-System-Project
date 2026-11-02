# TEAM GUIDE

Kế hoạch mới: [4 sprint cho 5 người](team-delivery-plan.md). Công nghệ đã chốt: React/Vite, ASP.NET Core API, EF Core, SQL Server; bổ sung SignalR và ZaloPay sandbox trong các sprint. Xem [kiến trúc hiện tại và đích triển khai](../docs/architecture.md) trước khi nhận task.

Khi dùng AI trên máy thành viên, bắt đầu bằng [codex/PROJECT_GUIDE.md](../codex/PROJECT_GUIDE.md). **Reviewer của tất cả Task là Trưởng nhóm**, người đã yêu cầu lập kế hoạch này; thành viên là người thực hiện/phối hợp, AI hỗ trợ kiểm tra theo [REVIEW_GUIDE.md](../codex/REVIEW_GUIDE.md).

## Chạy

Từ thư mục repo, với Node.js 22+ đã cài hoặc có bản portable trong `.tools`, khởi động bằng:

```powershell
.\scripts\start.cmd
```

Mở http://127.0.0.1:5173. Dừng server bằng `Ctrl+C`.

Nếu vừa cài Node.js hoặc .NET, mở terminal mới để nhận PATH. Có thể dùng trực tiếp:

```powershell
node --version
npm.cmd --version
dotnet --version
npm.cmd --prefix frontend run dev
```

Ngoài VS Code, dùng `.\scripts\npm-local.cmd` và `.\scripts\dotnet-local.cmd`.
Build frontend bằng `.\scripts\npm-local.cmd --prefix frontend run build`.
Các wrapper ưu tiên bản portable nếu có, rồi dùng công cụ đã cài trên máy. `.tools` không được đưa lên Git. Khi clone sang máy khác, cài Node.js 22+ và .NET 10 SDK, rồi chạy `npm.cmd --prefix frontend ci`. `global.json` chọn SDK .NET 10 ổn định, không tự chọn SDK preview 11.

## .NET / C#

Kiểm tra SDK dùng cho repo và build API:

```powershell
.\scripts\dotnet-local.cmd --version
.\scripts\dotnet-local.cmd build backend\CyberCafe.Api\CyberCafe.Api.csproj
```

Repository có dự án `.csproj` trong `backend/CyberCafe.Api` và migration EF Core.
Giao diện React/Vite vẫn dùng dữ liệu mẫu; trang admin chưa kết nối API hoặc SQL Server.
Xem `frontend/ADMIN_DEMO.md` để biết kho dữ liệu và các giới hạn của bản demo.

## Các module trước đợt chỉnh kiến trúc

Danh sách dưới đây ghi nhận cách chia cũ. Owner, reviewer và task của bốn sprint nằm trong [kế hoạch triển khai](team-delivery-plan.md); mỗi owner phụ trách cả UI, API và dữ liệu của nghiệp vụ.

- Đại: admin/auth, admin/staff, client/auth, client/account, client/tournaments
- Minh: admin/customers, admin/tournaments, admin/promotions, client/wallet
- Phúc: admin/dashboard, admin/computers, client/home
- Khang: admin/orders, admin/products, client/ordering
- Trường: admin/inventory, admin/invoices, admin/reports, client/invoices

## Phần dùng chung cần review

Thay đổi các file sau thông qua PR để Trưởng nhóm review, owner liên quan hỗ trợ đối chiếu phần tích hợp. Không coi đây là các file cấm sửa:

- frontend/src/app/routes.jsx
- frontend/src/app/theme.js
- frontend/src/app/theme.css
- frontend/src/app/AdminLayout.jsx
- frontend/src/app/ClientLayout.jsx

Chỉ tạo `components/` hoặc dữ liệu mẫu khi module thực sự sử dụng. Không tạo `mockData.js` rỗng cho mọi màn hình. Các kho demo hiện tại được mô tả trong [architecture.md](../docs/architecture.md); chuyển sang API theo từng nhóm nghiệp vụ liên quan.

## Luồng làm việc

1. Nhận một Task độc lập trong [backlog Jira](../codex/SPRINT_TASKS.md), có sprint, người phụ trách, reviewer, phụ thuộc và tiêu chí nghiệm thu. Backlog chủ yếu 2–3 Task/người/sprint; các bước nhỏ là checklist trong cùng thẻ. Các nhóm theo người trong kế hoạch chỉ để đọc; không tạo Task cha/Sub-task. Ghi issue key thực tế khi đã tạo thẻ.
2. Copy nguyên tên dạng `Build-Admin-Client-Login` vào Summary Jira; copy Description, Dependencies, Done khi và điều kiện Done chung vào nội dung thẻ. Chọn người phụ trách/sprint theo đầu nhóm trong backlog. Tạo branch từ Jira theo flow của nhóm, không viết thêm trường Branch hoặc lặp Assignee/Sprint/Reviewer trong Description. Chọn nhánh tích hợp mới nhất làm base; dùng `main` khi đợt dọn nền đã được tích hợp.
3. Chốt endpoint, DTO, trạng thái và thay đổi schema với các module liên quan trước khi code.
4. Mỗi thay đổi migration, router, auth/provider hoặc API client chung có một PR xử lý tại một thời điểm. Owner module viết thay đổi; Đại/Trường phối hợp thứ tự tích hợp/migration; Trưởng nhóm review và quyết định duyệt.
5. PR nhỏ, tách di chuyển file khỏi đổi nghiệp vụ. Một Task có thể gồm vài commit/PR cùng mã để bàn giao sớm; chạy kiểm tra liên quan và gửi một báo cáo review tổng hợp theo Task. Chỉ tách thêm thẻ khi có kết quả độc lập hoặc blocker riêng, không tạo thẻ mới cho từng bước kiểm tra.
6. Không sửa migration đã áp dụng ở DB chung; bổ sung migration tiếp theo. Không chạy SQL riêng rồi bỏ quên thay đổi trong repo.
7. Khi nghiệm thu chức năng backend, phải chạy qua API/SQL. LocalStorage chỉ chứng minh bản demo; realtime phải thử trên hai máy theo kế hoạch.
8. Theo dõi `To Do → In Progress → Review → Done`, ánh xạ vào trạng thái board hiện có. Mỗi người ưu tiên một việc đang làm; ghi blocker/người gỡ và nối `is blocked by` tới thẻ phụ thuộc. Người làm/AI bàn giao ở trạng thái sẵn sàng review; chỉ ghi Done khi tiêu chí của thẻ đạt và Trưởng nhóm đã duyệt.

## Kiểm tra

```powershell
.\scripts\npm-local.cmd --prefix frontend run build
.\scripts\npm-local.cmd --prefix frontend run test:frontend:models
.\scripts\npm-local.cmd --prefix frontend run test:frontend:render
.\scripts\npm-local.cmd --prefix frontend run test:frontend
.\scripts\dotnet-local.cmd build backend\CyberCafe.Api\CyberCafe.Api.csproj
```

Browser test tự mở Chrome/Edge headless bằng profile riêng; xem [báo cáo kiểm thử cũ](../docs/reports/frontend-retest-2026-10-22.md) để tham khảo các ca đã chạy. Kiểm thử API/SQL, SignalR hai máy và ZaloPay sẽ được bổ sung khi các luồng đó được triển khai.
