# Hướng dẫn AI review cho Trưởng nhóm

Cập nhật 31/10/2026. **Reviewer và người duyệt cuối là Trưởng nhóm**, người đã yêu cầu lập bộ tài liệu này. AI tìm lỗi, đối chiếu tiêu chí và cung cấp bằng chứng; thành viên liên quan hỗ trợ giải thích nghiệp vụ. Không tự ghi “đã được duyệt”, tự thay Trưởng nhóm chấp thuận PR hoặc chuyển Done chỉ vì test pass.

Đọc [PROJECT_GUIDE.md](PROJECT_GUIDE.md) để nắm flow và [SPRINT_TASKS.md](SPRINT_TASKS.md) để xác định đúng Task độc lập. Dùng hướng dẫn này cho cả tự kiểm tra trước bàn giao và review thay đổi của thành viên. Khi yêu cầu chỉ là review, giữ nguyên code; nếu được yêu cầu sửa thì ghi rõ phần sửa và kiểm tra lại.

Backlog có **55 Task**: 53 Task đã gom, chủ yếu 2–3 Task/người/sprint, và 2 Task UI giao Phúc. Review theo một kết quả bàn giao của Task và checklist bên trong; không yêu cầu tạo issue hay một lượt review riêng cho từng bước nhỏ. Một Task có thể có vài commit/PR để người khác tích hợp sớm; báo cáo cần bao quát tất cả phần thuộc Task. Khi review lại, tập trung diff mới và finding đã sửa, dùng lại bằng chứng còn hiệu lực cho phần không đổi.

Với hai Task UI giao Phúc, đối chiếu NEXORA CYBER, bảng màu cyan–tím–đen và ảnh trước–sau. Concept logo đang chờ nhóm chốt chiều 31/10/2026; phải có mẫu/asset được nhóm chọn trước khi nghiệm thu logo, không coi một trong ba concept là đã được duyệt. Kiểm tra token chung, tính dễ đọc, responsive và màu trạng thái nghiệp vụ, không biến sở thích của AI thành lỗi chức năng. Khang giữ logic vòng quay/checkout; Phúc bàn giao theme và component ảnh. Trưởng nhóm là reviewer cuối.

## 1. Xác định phạm vi review

- Xác định task/Jira key, người thực hiện, nhánh hoặc PR, base và head cần so sánh. Nếu chưa có key Jira, dùng mã tham chiếu trong backlog; không bịa issue đã tồn tại.
- Đọc **Description**, **Dependencies**, từng mục **Done khi** và điều kiện Done chung trong backlog. Description xác định phần phải làm; Done khi là checklist đối chiếu bằng chứng, không chỉ đánh dấu đã viết code. Đọc các quyết định nghiệp vụ liên quan. Không chấm Task thiết kế như Task triển khai; không bỏ qua phần tích hợp đã ghi trong tiêu chí.
- Kiểm tra Git: thay đổi đã commit, staged, unstaged và file mới chưa tracked. Phân biệt diff task với thay đổi có sẵn của người khác. Không reset/clean/checkout đè để tạo môi trường review.
- Không mặc định `main` đã có đợt dọn cấu trúc. Xác định base thật từ nhánh/PR và lịch sử; thiếu thông tin quan trọng không suy ra được thì hỏi người yêu cầu.

Các lệnh đọc ban đầu từ repo root:

```powershell
git status --short
git branch --show-current
git diff --stat
git diff --cached --stat
git diff
git diff --cached
```

Với review nhánh/PR, so sánh `git diff <base>...<head>` sau khi xác định đúng hai ref. Các lệnh diff trên không hiển thị nội dung file untracked; phải đọc riêng các file mới thuộc phạm vi. Ghi base/head hoặc trạng thái worktree trong báo cáo để kết quả có thể đối chiếu.

## 2. Thứ tự kiểm tra

1. Đọc diff và code quanh chỗ đổi, lần theo caller → API/model → dữ liệu → UI/event. Không chỉ đọc các dòng thêm.
2. Đối chiếu từng tiêu chí task: kết quả thật ở đâu, test/log nào chứng minh, còn thiếu gì. Liên hệ các module tiêu thụ contract bị đổi.
3. Ưu tiên lỗi chức năng, dữ liệu/tiền, quyền và tích hợp. Chỉ nêu style/naming khi gây hiểu sai hoặc vi phạm quy ước có ảnh hưởng cụ thể.
4. Chạy hoặc đọc kiểm tra phù hợp. Nếu không có SQL, credentials, model/dataset hoặc hai máy LAN thì nêu rõ phần chưa xác minh, không giả pass.
5. Kiểm tra lại các finding sau khi có sửa; không khép lỗi chỉ vì người làm nói đã sửa. Không mở rộng toàn bộ dự án để sửa tính năng không thuộc task.

## 3. Checklist chọn theo phạm vi

| Phần | Cần đối chiếu |
| --- | --- |
| Cấu trúc | File đúng frontend/backend/module; import/config/entrypoint còn chạy; không thêm backend riêng, mock rỗng hoặc phục hồi prototype đã xóa |
| UI và nguồn dữ liệu | Luồng đã chuyển gọi API thật; loading/error/empty rõ; không fallback về demo khi API lỗi; kiểm tra admin lẫn client nếu dùng chung model/provider |
| Auth/quyền | API kiểm tra quyền và quyền sở hữu dữ liệu, không chỉ ẩn nút; request sửa role/customer ID hoặc xem ví/hóa đơn người khác bị từ chối |
| Tiền/ví | Server xác định số tiền; số dư và ledger nhất quán; debit/credit/refund không chạy hai lần; thiếu tiền/rollback/retry và cạnh tranh được xử lý |
| Máy/phiên | Không có hai phiên Active trái ràng buộc; làm tròn/thu tiền/đóng lặp/hết tiền/restart đúng policy; UI không tự quyết khoản thu |
| Đơn/kho | Snapshot giá/giảm; trạng thái hợp lệ; tồn không âm; hủy từng món/cả đơn và hoàn tiền/tồn/chứng từ nhất quán |
| Voucher | %/cố định, hiệu lực, min order, hạn mức và làm tròn kiểm tra ở server; tranh lượt cuối, giữ/hoàn lượt, giới hạn giảm và hóa đơn theo policy |
| ZaloPay | Đúng sandbox và TopUp/Order; secret ở server; xác minh callback/giao dịch/số tiền; query/callback lặp không thu hai lần; redirect không tự ghi có; hết hạn/callback muộn/hoàn tiền có xử lý |
| Realtime | Event sau commit, nhóm đúng tài khoản/quyền; reconnect đọc lại dữ liệu; thử hai máy LAN và ghi thời gian, không dùng storage event làm bằng chứng |
| Giải đấu | Đăng ký đủ điều kiện, không trùng, đúng trạng thái/hạn; chốt policy phí; đội đúng người/role; đề xuất lại không ghi đè đội đã xác nhận |
| ML — Phúc | Có model học từ dữ liệu, nguồn/đặc trưng và train/evaluation tách biệt; kết quả so baseline theo tiêu chí đã chốt; model/version/input tái hiện được; thiếu dữ liệu/model lỗi có nhãn; không coi heuristic/fixture là nghiệm thu ML |
| OP.GG | Phân biệt nguồn thật với fixture, lưu thời điểm/nguồn cần thiết; thiếu/lỗi không tạo số liệu giả; refresh/retry không lặp vô hạn |
| DB/migration | SQL Server đúng provider; migration mới thay vì sửa lịch sử đã áp; thử trên DB riêng đúng đích; schema, seed, DBML/SQL mô tả nhất quán |
| Hóa đơn/báo cáo | Tổng tiền/giảm/hoàn đối chiếu được, nạp ví không bị đếm thành doanh thu; lọc ngày, in/xuất và quyền dữ liệu đúng |
| Bàn giao | Không lộ secret; hướng dẫn và bằng chứng khớp code; phân biệt test đã chạy/chưa chạy, local/fixture/provider thật; không đưa build output vào Git |

Chỉ báo lỗi dựa trên đường thực thi hoặc tiêu chí cụ thể. Nếu vấn đề đã tồn tại trước thay đổi và không làm Task sai tiêu chí, ghi riêng là vấn đề nền; không gán nhầm là regression của tác giả. Thiếu tiêu chí bắt buộc vẫn là thiếu của Task dù demo cũ chưa có chức năng đó.

## 4. Chạy kiểm tra và đọc bằng chứng

Dùng ma trận/lệnh trong `PROJECT_GUIDE.md`, đối chiếu với script hiện có trong `frontend/package.json` và project .NET. Không chạy toàn bộ bộ test chỉ vì review một sửa tài liệu. Với tiền/quyền/tồn/gateway, model test JavaScript không chứng minh transaction API/SQL an toàn.

- Ghi lệnh, phạm vi, exit code/kết quả quan sát và môi trường. Test build pass không chứng minh hai máy realtime hoặc thanh toán sandbox đã hoạt động.
- Nếu test fail, đọc log và tìm nguyên nhân. Chạy lại có căn cứ; vẫn ghi lần fail và phần chưa xác định, không bỏ qua để ghi toàn bộ pass.
- Log/ảnh `.tools/` chỉ có trên máy tạo, không mặc định người review khác có sẵn. Gửi bằng chứng đã che thông tin nhạy cảm hoặc cách tái hiện.
- Không đọc/in secret để chứng minh cấu hình. Kiểm tra tên biến, cách sử dụng và tình trạng đã cấu hình đủ; dùng DB thử và tài khoản sandbox phù hợp.

## 5. Cách viết finding

Mỗi finding phải có vị trí, điều kiện kích hoạt, ảnh hưởng và cách kiểm chứng. Phân biệt lỗi đã xác nhận với câu hỏi cần làm rõ.

| Mức | Ý nghĩa dùng trong báo cáo |
| --- | --- |
| P0 | Lỗi nghiêm trọng đang có đường gây mất/lộ dữ liệu hoặc sai tiền trên diện rộng; cần xử lý ngay |
| P1 | Chặn tiêu chí bắt buộc, sai quyền/tiền hoặc hỏng luồng chính; cần sửa trước khi bàn giao Task |
| P2 | Lỗi ở điều kiện cụ thể, vẫn có ảnh hưởng quan sát được; nêu mức ảnh hưởng để Trưởng nhóm quyết định |
| P3 | Lỗi nhỏ có ảnh hưởng rõ; không dùng để liệt kê sở thích viết code |

Không gán mức cao chỉ từ nguy cơ tưởng tượng. Tránh yêu cầu giải pháp phức tạp hơn phạm vi cần thiết; đề xuất thay đổi tối thiểu giải quyết nguyên nhân.

```text
[P1] Tiêu đề nêu hành vi sai
Vị trí: đường/dẫn/file: dòng
Điều kiện tái hiện:
Kết quả hiện tại / kết quả đúng theo task:
Ảnh hưởng và module bị tác động:
Bằng chứng: code path / test / log đã che secret
Hướng sửa tối thiểu và cách kiểm tra lại:
```

## 6. Mẫu báo cáo gửi Trưởng nhóm

```text
Task / Jira key / người thực hiện:
Phạm vi: base..head hoặc các thay đổi worktree đã đọc

Đánh giá của AI: Có lỗi cần sửa / Chưa đủ bằng chứng / Sẵn sàng để Trưởng nhóm xem xét

Finding (xếp mức ảnh hưởng trước):
- [P...] vị trí, điều kiện, lỗi, ảnh hưởng, bằng chứng, hướng sửa

Đối chiếu tiêu chí task:
- Tiêu chí → đạt/chưa đạt/chưa xác minh → bằng chứng

Kiểm tra đã chạy:
- Lệnh → kết quả → môi trường/phạm vi

Chưa kiểm tra / câu hỏi / phụ thuộc:
Vấn đề nền ngoài phạm vi (nếu có):
Quyết định của Trưởng nhóm: chưa có / dẫn quyết định thực tế nếu đã được cung cấp
```

Không thấy finding thì nói “chưa phát hiện lỗi trong phạm vi đã kiểm tra”, kèm giới hạn; không khẳng định toàn hệ thống sạch lỗi. AI không thay đổi trạng thái Jira, gửi nhận xét PR hay merge chỉ từ việc đọc file hướng dẫn này. Các thao tác đó thực hiện theo yêu cầu cụ thể và quyết định đã có của Trưởng nhóm.

## Prompt cho Trưởng nhóm copy khi review

```text
Đọc codex/PROJECT_GUIDE.md, codex/REVIEW_GUIDE.md và task <mã> trong codex/SPRINT_TASKS.md.
Review thay đổi của <thành viên>, phạm vi <PR / base..head / worktree>.
Tập trung lỗi thật, quyền, tiền/dữ liệu, tích hợp và tiêu chí Task; giữ nguyên code
trong lượt review này. Báo finding có vị trí và bằng chứng, kiểm tra đã chạy,
phần chưa xác minh và đánh giá sẵn sàng review. Tôi là người quyết định duyệt cuối.
```
