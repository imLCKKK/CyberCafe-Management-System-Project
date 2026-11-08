# Tài liệu hệ thống

- [Kiến trúc và luồng dữ liệu](architecture.md): nền CyberManagement/SQL Server mới 35 bảng, frontend demo và OP.GG.
- [Hướng dẫn backend](../Backend/README.md): build, config secrets, SQL/EF và kiểm tra auth.
- [Định danh và ma trận quyền](../Backend/docs/001-monorepo.md).
- [Entity](../Backend/src/CyberManagement.Domain/Entities/) và [cấu hình EF](../Backend/src/CyberManagement.Infrastructure/Persistance/Configuration/) mô tả schema hiện tại; [migrations](../Backend/src/CyberManagement.Infrastructure/Migrations/) là lịch sử chính.
- [Kế hoạch nhóm](../planning/team-delivery-plan.md), [quy trình](../planning/team-workflow.md), [55 Task](../codex/SPRINT_TASKS.md).
- [Hướng dẫn AI](../codex/PROJECT_GUIDE.md), [review](../codex/REVIEW_GUIDE.md).

## Báo cáo lịch sử

- [Demo admin 21/10/2026](reports/admin-progress-2026-10-21.md).
- [Kiểm tra frontend 22/10/2026](reports/frontend-retest-2026-10-22.md).

Giữ các báo cáo cũ để đối chiếu đúng thời điểm. Review/bàn giao hiện tại gửi trực tiếp trong chat; chỉ tạo file báo cáo mới khi người dùng yêu cầu.

## Contract nghiệp vụ

- [SCRUM-9: pipeline ML chia đội](contracts/ml-team-balancing-pipeline.md): kiểm kê OP.GG/baseline, contract input/output, ràng buộc, train/evaluation, metric/runtime và blocker dữ liệu/S1-D-3.
- [SCRUM-12: kho, hóa đơn và voucher](contracts/inventory-invoice-voucher.md): mapping schema CyberManagement, quy tắc voucher, phân quyền, hóa đơn và các giao điểm còn chờ xác nhận.

- [SCRUM-13: migration, seed và luồng đọc kho](scrum-13-inventory-setup.md).
