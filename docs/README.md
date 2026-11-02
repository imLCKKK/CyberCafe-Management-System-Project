# Đặc tả và báo cáo

Thư mục `docs/` dành cho tài liệu mô tả hệ thống và báo cáo. [Bản đồ kiến trúc](architecture.md) ghi baseline của code hiện tại và luồng đích; đây là tài liệu tham chiếu để viết đặc tả chính thức, chưa phải bằng chứng mọi chức năng đã hoàn thành.

**Database:** [DBML ban đầu](../backend/database/schema.dbml) và [SQL xuất](../backend/database/InitialCreate.sql) nằm trong `backend/database/`; entity, `DbContext` và migration EF Core nằm trong [`backend/CyberCafe.Api/Data/`](../backend/CyberCafe.Api/Data/). SQL Server thực tế phải được tạo bằng migration theo [hướng dẫn backend](../backend/README.md); các file này chưa phải database đang chạy.

## Báo cáo lịch sử

- [Tiến độ demo admin ngày 21/10](reports/admin-progress-2026-10-21.md).
- [Kiểm thử frontend ngày 22/10](reports/frontend-retest-2026-10-22.md).

Các báo cáo này ghi kết quả ở thời điểm nêu trong tên file, không thay thế kiểm thử bản hiện tại.

## Tài liệu làm việc của nhóm

- [Kế hoạch 4 sprint](../planning/team-delivery-plan.md) và [quy trình làm nhóm](../planning/team-workflow.md) nằm trong `planning/`.
- [Hướng dẫn AI](../codex/PROJECT_GUIDE.md), [review](../codex/REVIEW_GUIDE.md) và [55 Task Jira](../codex/SPRINT_TASKS.md) nằm trong `codex/`.
- [Hướng dẫn demo admin](../frontend/ADMIN_DEMO.md) nằm cạnh frontend.

## Tích hợp và kiểm thử

- [SCRUM-13: DB thử, seed và luồng đọc kho](scrum-13-inventory-setup.md): hướng dẫn chạy, bằng chứng hiện có và phần auth/UI còn chờ tích hợp.
