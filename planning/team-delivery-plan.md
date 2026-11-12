# Kế hoạch bàn giao — 4 sprint, 31/10–21/11/2026

Cập nhật: 31/10/2026. Đây là baseline phân công của nhóm sau khi xác nhận phạm vi với người dùng. Các task dưới đây là công việc cần triển khai; việc dọn thư mục không đồng nghĩa ứng dụng đã có API nghiệp vụ, realtime hoặc thanh toán.

Để đưa lên Jira, dùng [backlog 55 Task độc lập](../codex/SPRINT_TASKS.md): 53 Task đã gom, chủ yếu 2–3 Task/người/sprint, và 2 Task UI giao Phúc theo yêu cầu mới. Mỗi Task có Summary tiếng Anh nối bằng dấu gạch ngang, Description, Dependencies và Done khi. Sprint, người phụ trách và reviewer ghi ở đầu phần/nhóm, không lặp lại trong từng thẻ; không cần trường Branch riêng. Đã giữ đủ 100 việc nhỏ cũ trong các checklist. Đại và Trường ở S2 có 4 Task vì các API/UI cần bàn giao riêng trước bước tích hợp/đối chiếu. Phúc có 4 Task ở S1/S2 sau khi nhận thêm hai Task UI; ưu tiên bàn giao contract/hook và component ảnh sớm, không bỏ phần ML/realtime. Các gói S1-D…S4-T trong tài liệu này chỉ tổng hợp phạm vi theo người, không tạo thành Task cha/Sub-task trên Jira.

**Reviewer duy nhất là Trưởng nhóm**, người dùng đã yêu cầu lập kế hoạch này và trực tiếp duyệt thay đổi. Các thành viên được ghi ở phần phối hợp chỉ hỗ trợ đối chiếu nghiệp vụ/tích hợp. AI hỗ trợ kiểm tra theo [hướng dẫn review](../codex/REVIEW_GUIDE.md); quyết định duyệt và Done cần phản ánh quyết định thực tế của Trưởng nhóm, không suy ra từ test pass.

## 1. Ràng buộc đã chốt

- **Stack:** React/Vite + ASP.NET Core API + Entity Framework Core + SQL Server.
- **Nhóm:** Đại, Minh, Phúc, Khang, Trường; năng lực tương đương. Mỗi người sở hữu cả UI, API, dữ liệu và kiểm tra của phần được giao.
- **Lịch:** bắt đầu 31/10/2026, hạn 21/11/2026. Bốn sprint trải từ 31/10 đến 19/11 (20 ngày lịch); ngày 10/11 và 15/11 là mốc bàn giao chung giữa hai sprint liền kề. Dự phòng 20–21/11; không coi số ngày lịch là ngày làm việc toàn thời gian.
- **Phạm vi:** toàn bộ chức năng hiện có, gồm các trang đang là placeholder: đăng nhập/quyền, nhân viên, khách/tài khoản, máy/phiên/tiền giờ, Home/dashboard, sản phẩm/menu/đơn/gợi ý món, ví, kho, hóa đơn/báo cáo, giải đấu/OP.GG và khuyến mãi. Bổ sung bắt buộc **ML hỗ trợ chia đội giải đấu, do Phúc phụ trách**.
- **Khuyến mãi đã duyệt:** voucher cho đơn món ăn, giảm theo phần trăm hoặc số tiền cố định, ngày hiệu lực, giá trị đơn tối thiểu và giới hạn sử dụng. Không áp voucher này vào tiền giờ.
- **Demo:** hai máy mở web trong LAN, cùng dùng API và SQL Server, đồng bộ bằng SignalR. Không xây agent hay điều khiển Windows.
- **Thanh toán:** ZaloPay **sandbox chính thức** cho nạp ví và thanh toán đơn món ăn, không giao dịch tiền thật; giữ các luồng ví/tiền mặt/nạp tại quầy của ứng dụng.
- **Thời gian làm:** các thành viên linh hoạt để bám hạn; chưa định lượng giờ. Không tiếp tục hỏi lại số giờ, không coi linh hoạt là công suất vô hạn hoặc bảo đảm chắc chắn kịp hạn.

Mục tiêu là **đủ chức năng cuối S3 ngày 15/11** để bàn giao sang S4. S4 dùng để chạy lại, sửa lỗi, kiểm tra hai máy và bàn giao. Không tự bỏ giải đấu, ML chia đội, OP.GG, khuyến mãi hoặc ZaloPay khỏi phạm vi khi chậm; đưa việc chậm lên bảng phụ thuộc, chia người hỗ trợ và báo ảnh hưởng lịch ngay.

## 2. Tận dụng code và làm rõ cấu trúc

Quyết định mới ngày 02/11/2026 của Trưởng nhóm: dùng **CyberManagement và DB SQL Server mới 35 bảng**, thay nền CyberCafe 19 bảng trong bản mô tả 31/10. Giữ stack, 55 task, owner và lịch đã chốt; rà ước lượng nếu code/contract bị ảnh hưởng.

| Nguồn | Phần dùng tiếp | Cách phối hợp |
| --- | --- | --- |
| `frontend/` | React/Vite, UI, model demo và tests | Chuyển từng luồng sang API; auth backend không tự nối UI |
| `Backend/src/CyberManagement.Domain` | 35 entity | Owner đối chiếu ID Employee/Customer kiểu int và rule module |
| `Backend/src/CyberManagement.Application` | Auth/use case/interface/validation | Thêm nghiệp vụ theo Task, không dùng EF/HTTP |
| `Backend/src/CyberManagement.Infrastructure` | ApplicationDbContext, config, migrations, repositories/hash | Trường điều phối migration, từng owner viết phần của mình |
| `Backend/src/CyberManagement.API` | Controller/JWT, DI, HTTP host chung | Thêm endpoint theo Task; không tạo backend riêng |
| `Backend/integrations/opgg` | Adapter Node và helper frontend | Giữ endpoint/import/test khi đổi backend .NET |

Solution: `Backend/CyberManagement.slnx`; xem [lệnh chạy/EF](../Backend/README.md) và [kiến trúc](../docs/architecture.md). App hiện có auth/role/health; kho, ví, phiên, voucher, ML và các tích hợp vẫn đối chiếu Task và code thực tế, không nghiệm thu từ số entity.

DB mới dùng bốn migration hiện có. Không áp InitialCreate mới vào DB 19 bảng cũ; chưa yêu cầu chuyển/xóa DB cũ. SQL/DBML mô tả khi bổ sung phải theo schema CyberManagement. Giữ dữ liệu cũ riêng nếu có, việc chuyển dữ liệu cần được chốt thành phạm vi cụ thể.

Nền sửa hiện dừng trước commit trên nhánh riêng; chưa tự đưa lên `dev`. Các thành viên lấy đúng `dev` đã được Trưởng nhóm review/tích hợp trước khi phát triển tiếp. Không phục hồi `cybernet-html/` hoặc nhập migration MySQL từ nhánh cũ.

## 3. Hợp đồng kỹ thuật chung

Luồng dữ liệu: **web → API .NET → nghiệp vụ → EF Core → SQL Server → commit → SignalR → web đọc lại dữ liệu API**.

1. Server xác định người dùng, quyền, giá, giảm giá, số dư và trạng thái. Không tin tổng tiền/role do browser gửi.
2. Các thay đổi ví, ledger, đơn, tồn, lượt dùng voucher và chứng từ thuộc cùng một thao tác phải cùng thành công hoặc cùng thất bại. Thêm chống xử lý lặp và kiểm soát đồng thời; transaction không tự giải quyết mọi xung đột.
3. Snapshot đơn giá/giảm giá tại thời điểm xác nhận để hóa đơn không đổi theo giá hoặc voucher sửa sau đó. Không tính lại tiền từ model demo.
4. LocalStorage chỉ phục vụ tùy chọn UI hoặc chế độ demo được bật rõ ràng. API lỗi phải hiện lỗi; không tự chuyển sang số liệu giả. Luồng nghiệm thu không ghép một nửa SQL với một nửa localStorage.
5. EF migrations là lịch sử schema chính. SQL xuất để rà soát và DBML để mô tả; cập nhật theo migration, không sửa ba nguồn độc lập.
6. OP.GG lỗi/thiếu dữ liệu phải hiển thị trạng thái tương ứng và cho thử lại. Fixture có thể dùng kiểm thử, không được ghi thành kết quả lấy từ provider thật.

### Realtime hai máy

Phúc sở hữu SignalR Hub, hook kết nối dùng chung và hợp đồng sự kiện. Từng owner phát sự kiện của module mình **sau commit** và nối màn hình của mình vào hook; Phúc không làm thay mọi phần tích hợp.

- Nhóm nhận sự kiện phải được xác thực và giới hạn theo tài khoản/vai trò; khách không nhận payload ví/đơn của người khác.
- Mục tiêu nghiệm thu LAN: **không quá 2 giây** từ lúc thao tác trên máy A nhận phản hồi thành công đến lúc trạng thái đã lưu hiển thị đúng trên máy B, không F5.
- Kiểm tra 10 lượt cho mỗi luồng đại diện: máy/phiên, ví, đơn/tồn, giải đấu và voucher; ghi thời gian từng lượt trên cùng một bộ đo của bài kiểm tra để tránh lệch đồng hồ hai máy.
- Mất kết nối phải hiện trạng thái; khi kết nối lại phải đọc lại API, không chỉ trông chờ nhận lại các event bị lỡ.
- Tải lại hoặc restart API không làm mất dữ liệu đã commit. Mất/retry sự kiện không được khiến cộng tiền, trừ tồn hay ghi đơn lần nữa.

Đây là tiêu chí đo khi triển khai, chưa phải kết quả realtime hiện tại.

### ZaloPay sandbox chính thức

**Phụ thuộc S1, owner Minh:** nhận bộ thông tin ứng dụng/khóa sandbox hợp lệ; cấu hình bí mật ở server; chuẩn bị callback **HTTPS truy cập công khai** trỏ về API demo. Đại hỗ trợ endpoint/quyền/cấu hình, Phúc hỗ trợ đường kết nối. Hai máy LAN vẫn dùng web nội bộ; riêng callback cần được ZaloPay gọi tới từ Internet.

- Dùng một adapter thanh toán cho hai mục đích `TopUp` và `Order`. Tạo đơn sandbox ở server, lưu ID, tài khoản/đơn liên quan, số tiền và trạng thái dự kiến; xác minh callback theo tài liệu chính thức trước khi cập nhật ví hoặc xác nhận đơn đã trả tiền.
- Trang redirect hay thông báo thành công ở browser không đủ để ghi nhận thanh toán. Đối chiếu callback đã xác minh hoặc kết quả truy vấn trạng thái chính thức với giao dịch đang chờ.
- Callback lặp/đến muộn hoặc truy vấn đồng thời không được cộng ví, xác nhận đơn, trừ tồn hay dùng voucher lần hai. Sai chữ ký, sai giao dịch hoặc sai số tiền không được ghi nhận thành công.
- Với đơn món ăn: quyết định ngày 08/11/2026 tại hợp đồng checkout D05/D06 giữ tồn/lượt 15 phút, hết hạn giải phóng một lần; thu muộn ghi tiền và tạo nghĩa vụ hoàn có duyệt, không hồi sinh đơn. Hoàn theo phương thức gốc, không tự cộng ví thay cho hoàn gateway hoặc hoàn hai lần. Owner triển khai/kiểm chứng theo mục 11.3 của hợp đồng.
- Kiểm thử chữ ký/trạng thái bằng fixture phải ghi là kiểm thử nội bộ. Nghiệm thu sandbox cần bằng chứng request/response/callback từ môi trường sandbox thật đã che khóa bí mật.
- Nếu chưa có credentials hoặc callback chưa truy cập được, đánh dấu đúng phụ thuộc bị chặn; tiếp tục phần adapter/test độc lập. Không giả lập thành công rồi tính là đã tích hợp.
- Không đưa khóa vào frontend, Git, ảnh demo hoặc log công khai. Không dùng môi trường live/tiền thật.

Nguồn triển khai: [ZaloPay — hướng dẫn bắt đầu](https://developer.zalopay.vn/v2/start/) và [ZaloPay — callback](https://docs.zalopay.vn/docs/developer-tools/knowledge-base/callback/).

### ML chia đội giải đấu

**Owner: Phúc; tích hợp giải đấu: Đại; reviewer: Trưởng nhóm.** Đây là yêu cầu triển khai, chưa phải kết quả đã có. S1 kiểm kê code/model/dataset Phúc đã làm để tận dụng, rồi chốt bài toán học máy, đầu vào, đầu ra, nguồn dữ liệu, cách đánh giá và môi trường chạy. Chưa chốt thư viện hay tạo thêm một service riêng khi chưa biết model hiện có.

- Phúc phụ trách dữ liệu, huấn luyện/đánh giá, phiên bản model, suy luận và logic đề xuất chia đội. Đại phụ trách danh sách đăng ký hợp lệ, màn hình đề xuất/xác nhận và lưu đội qua API giải đấu. API kiểm tra quyền và danh sách người chơi trước khi lưu.
- Phân biệt kết quả học từ dữ liệu với quy tắc ghép đội hoặc công thức điểm viết tay. Có baseline để so sánh; không ghi thuật toán chia đội theo quy tắc là ML. Chốt chỉ số và ngưỡng nghiệm thu ở S1 trước khi đánh giá; không tự hứa model tốt hơn khi chưa có kết quả.
- Quyết định ngày 10/11/2026: baseline/fallback SCRUM-4 và SCRUM-9 dùng chung `rank-snake-v1`: sắp `OpggSnapshot.rank.tier` giảm dần, hòa theo `RegistrationId` tăng dần rồi phân snake; không xếp theo SkillScore tổng hợp. Thiếu rank OP.GG hợp lệ báo lỗi; dự phòng có nhãn/phương pháp riêng. Yêu cầu ML và đánh giá so baseline giữ nguyên. Confirm phải giữ phân đội/vai trò của đề xuất server; swap sau xác nhận mang nguồn Manual, giữ lịch sử và không được tính là kết quả baseline/ML. Đại đồng bộ contract v0.7 bằng PR bổ sung SCRUM-4 sau PR #18; Phúc giữ đặc tả baseline.
- Tách dữ liệu huấn luyện và đánh giá, ghi nguồn/đặc trưng và giới hạn dữ liệu. OP.GG là nguồn đầu vào nếu phù hợp; không mặc định vài số liệu người đăng ký đã đủ để huấn luyện model.
- Đề xuất phải đúng số người/đội, không trùng hoặc bỏ sót người đủ điều kiện và đáp ứng ràng buộc vai trò đã chốt. Lưu phiên bản model và snapshot đầu vào cùng kết quả để tái hiện. Có xử lý dữ liệu thiếu/model lỗi; chia thủ công hoặc baseline dự phòng phải có nhãn rõ, không tính là nghiệm thu ML.
- Đại lưu kết quả xác nhận trong SQL và phát sự kiện sau commit để hai máy thấy cùng đội. Việc gọi lại đề xuất không tự ghi đè đội đã xác nhận.

## 4. Owner và giao điểm

| Người | Owner nghiệp vụ | Phần phối hợp để tránh dồn tải |
| --- | --- | --- |
| Đại | Auth/quyền, nhân viên, đổi mật khẩu; giải đấu, đăng ký, UI xác nhận và lưu đội | Tích hợp ML của Phúc vào luồng chia đội; nối UI admin voucher vào API của Trường; Minh phối hợp tài khoản |
| Minh | Hồ sơ khách, ví/ledger, nạp tại quầy, ZaloPay sandbox | API trừ/hoàn ví dùng chung; không nhận thêm rule voucher |
| Phúc | Máy/phiên/tính tiền giờ, Home/dashboard máy; SignalR, adapter OP.GG; **ML chia đội giải đấu** | Đại tích hợp UI/API lưu đội; module owner tự nối event/UI; ưu tiên ML, không giao thêm UI báo cáo |
| Khang | Danh mục/sản phẩm, menu/gợi ý, đặt/xử lý/hủy đơn; áp voucher vào đơn | Gọi rule voucher của Trường; ghi lượt dùng, trừ/hoàn ví và tồn trong transaction đơn |
| Trường | Kho, hóa đơn/bảng kê/in/xuất, báo cáo; schema/API/quy tắc voucher | Đại làm UI CRUD voucher; tái sử dụng biểu đồ hiện có; điều phối thứ tự migration |

**Voucher chỉ có một owner nghiệp vụ: Trường.** Đại chịu màn hình CRUD, Khang chịu nơi áp dụng vào đơn món ăn. Loại giảm phần trăm/cố định, ngày hiệu lực, ngưỡng đơn và hạn mức đều do rule server dùng chung kiểm tra. Chính sách tính tiền và dùng lại lượt sau hủy phải được thống nhất ngày đầu.

Đại điều phối tích hợp; từng owner sửa lỗi module mình. Trường điều phối migration nhưng không viết thay entity của cả nhóm. Trưởng nhóm review và duyệt; luồng tiền/tồn cần bằng chứng và giải thích từ owner liên quan, không chỉ kiểm tra giao diện.

**Phần frontend chuyển cho Phúc theo yêu cầu mới:** S1-U-1 áp nhận diện NEXORA CYBER và theme cyan–tím–đen; S2-U-1 bổ sung ảnh món và component thẻ món theo theme mới. Giữ mã U để tra liên kết. Bảng màu theo ảnh: nền `#0B0F14`, tím `#A855F7`, cyan `#22D3EE`, chữ `#E5F0FF`; concept logo chờ nhóm chốt chiều 31/10/2026, không tự chọn. Phúc làm token/layout trước và tích hợp đúng asset được duyệt; không đổi màu trạng thái nghiệp vụ hàng loạt. Khang giữ logic vòng quay/checkout trong S2-K-3, hồi quy S3-K-2, phối hợp dữ liệu ảnh và nối component Phúc bàn giao. Hai Task UI được tính vào tải Phúc; Trưởng nhóm giữ review cuối, không thêm thành viên hay thẻ trùng.

## 5. Lịch và mốc giao hàng

| Sprint | Ngày | Mục tiêu và bằng chứng |
| --- | --- | --- |
| S1 | 31/10–05/11 | Chốt schema/contracts đủ mọi chức năng; login và các luồng đọc qua DB; một thay đổi máy đồng bộ LAN; kiểm kê dữ liệu/model ML, chốt tiêu chí đánh giá; credentials/callback sandbox được kiểm tra hoặc ghi rõ blocker |
| S2 | 06–10/11 | Mỗi họ chức năng có đường chạy tích hợp: phiên, ví/sandbox, đơn, kho/chứng từ, đăng ký giải/OP.GG, model ML và đề xuất đội đầu tiên, CRUD và áp voucher |
| S3 | 10–15/11 | Hoàn thành toàn bộ chức năng đã chốt; đầy đủ luồng admin/client; giao dịch và realtime được kiểm tra xuyên module |
| S4 | 15–19/11 | Hồi quy, sửa lỗi, kiểm tra quyền/mạng/retry, chạy từ DB mới, đo LAN, tài liệu và diễn tập |
| Dự phòng | 20–21/11 | Sửa lỗi chặn nghiệm thu và đóng gói; không xếp tính năng mới |

Ngày 10/11 và 15/11 là mốc bàn giao giữa hai sprint cùng diễn ra trên lịch. Mỗi Task thuộc một sprint; kết quả/PR của sprint trước cần sẵn sàng để người ở sprint sau tích hợp trong ngày giao nhau.

Mốc là mục tiêu kế hoạch, không bảo đảm tiến độ khi giờ thực tế chưa đo và phụ thuộc ngoài chưa sẵn sàng. Đầu mỗi ngày nhìn phần còn lại và phụ thuộc để chia hỗ trợ; không âm thầm dồn tính năng chưa làm sang S4 hoặc hai ngày dự phòng.

## 6. Sprint 1 — 31/10 đến 05/11

### S1-D — Đại: tài khoản/quyền và hợp đồng giải đấu

**Reviewer:** Trưởng nhóm. **Phối hợp:** Minh; Phúc đối chiếu giải đấu. **Phụ thuộc:** schema account/customer được chốt cùng nhóm; cấu hình API/SQL.

1. Nối login/logout admin/client; xác thực và kiểm tra quyền ở API; dựng API client dùng chung.
2. Ghi hợp đồng nhân viên, đổi mật khẩu, giải/đăng ký/đội; làm rõ khác biệt phí giải giữa UI và DBML.
3. Kiểm tra login hợp lệ/sai và gọi trực tiếp endpoint bằng quyền không đủ; ghi cách chạy và cấu hình.

**Nghiệm thu:** đăng nhập thật với tài khoản seed; API từ chối quyền sai; hợp đồng đủ cho Minh/Phúc phát triển song song. Không tính auth placeholder là hoàn tất.

### S1-M — Minh: khách/ví và điều kiện sandbox

**Reviewer:** Trưởng nhóm. **Phối hợp:** Đại và Trường. **Phụ thuộc:** S1-D về ID/quyền; credentials và callback công khai.

1. Nối đọc/tạo khách, hồ sơ và số dư từ DB; chốt thao tác credit/debit/refund cùng ledger.
2. Chuẩn bị credentials ZaloPay sandbox, secret server, callback HTTPS; ghi kết quả kiểm tra khả năng truy cập và lỗi còn lại.
3. Thiết kế transaction ID, mục đích nạp ví/thanh toán đơn, trạng thái và idempotency cùng Khang; kiểm tra chữ ký/trạng thái bằng test nội bộ có nhãn rõ.

**Nghiệm thu:** dữ liệu khách/ví còn sau reload; hợp đồng tiền được review; có bằng chứng sandbox đã sẵn sàng hoặc blocker cụ thể, không khai đã thanh toán khi mới chạy fixture.

### S1-P — Phúc: máy/phiên, realtime và thiết kế ML

**Reviewer:** Trưởng nhóm. **Phối hợp:** Đại; Minh đối chiếu công thức tiền. **Phụ thuộc:** S1-D auth, cấu hình LAN/API.

1. Nối danh sách máy/loại máy từ API; chốt trạng thái phiên, công thức tiền và điều kiện mở/đóng.
2. Dựng SignalR Hub/hook, đăng ký nhóm theo quyền; phát thay đổi trạng thái máy sau commit.
3. Kiểm tra hai máy LAN và reconnect; xác nhận adapter OP.GG ở đường dẫn mới vẫn giữ contract.
4. Kiểm kê code/model/dataset ML hiện có; cùng Đại chốt contract đề xuất đội, ràng buộc, dữ liệu huấn luyện/đánh giá và tiêu chí so sánh baseline. Ghi rõ thiếu dữ liệu hoặc model là phụ thuộc cần xử lý từ S1.

**Nghiệm thu:** máy A đổi trạng thái, máy B đọc đúng trạng thái API không F5; có số đo ban đầu theo mục tiêu 2 giây và test quyền nhóm nhận event. Có tài liệu kiểm kê ML, contract và tiêu chí đánh giá được Đại đối chiếu contract và Trưởng nhóm duyệt; thiếu dữ liệu/model được ghi đúng blocker.

### S1-K — Khang: danh mục/menu và hợp đồng đơn

**Reviewer:** Trưởng nhóm. **Phối hợp:** Trường; Minh đối chiếu giao dịch ví. **Phụ thuộc:** S1-D quyền; schema sản phẩm/đơn/tồn cùng S1-T.

1. Port chọn lọc sản phẩm/danh mục; nối admin và menu client vào API.
2. Chốt payload đặt/hủy/xử lý đơn, snapshot giá, phương thức thu và trường voucher với Trường.
3. Kiểm tra quyền CRUD, giá từ server và xử lý API lỗi; rà gợi ý món để dùng cùng danh sách sản phẩm/tồn.

**Nghiệm thu:** món do admin tạo hiện ở menu từ DB; lỗi không đổi sang mock; hợp đồng đơn không dùng giá/tổng tiền client làm căn cứ thu.

### S1-T — Trường: kho/chứng từ/voucher và migration

**Reviewer:** Trưởng nhóm. **Phối hợp:** Khang; Minh đối chiếu tiền, Đại đối chiếu CRUD voucher. **Phụ thuộc:** quyết định nghiệp vụ ngày đầu; thay đổi entity của từng owner.

1. Bổ sung schema kho, chứng từ và voucher đúng phạm vi đã chốt; ghi invariant và định nghĩa doanh thu.
2. Chốt API CRUD/kiểm tra voucher, snapshot giảm giá và lượt sử dụng; chốt hợp đồng phát hành chứng từ cho phiên/đơn.
3. Sắp thứ tự migration, seed và kiểm tra trên DB mới; nối một luồng đọc kho/chứng từ qua API.

**Nghiệm thu:** schema/contract có đủ kho, hóa đơn, promo; migration áp dụng được trên DB thử mới; mỗi owner biết nơi gọi rule/chứng từ, không tạo schema riêng.

**Cổng S1:** nhóm demo login + dữ liệu SQL + một event LAN; kiểm tra sớm sandbox và OP.GG. Fixture hỗ trợ code song song nhưng không thay bằng chứng tích hợp.

## 7. Sprint 2 — 06 đến 10/11

### S2-D — Đại: nhân viên, giải đấu và màn hình voucher

**Reviewer:** Trưởng nhóm. **Phối hợp:** Minh; Phúc đối chiếu giải, Trường đối chiếu voucher. **Phụ thuộc:** S1-D, API voucher S2-T.

1. Hoàn thiện nhân viên/quyền và đổi mật khẩu; nối dữ liệu tài khoản đúng owner.
2. Nối CRUD giải, đăng ký và danh sách người tham gia admin/client; kiểm tra tài khoản và trạng thái giải.
3. Triển khai UI admin voucher bằng API của Trường; phát/nhận event giải và voucher đúng quyền.

**Nghiệm thu:** nhân viên và khách không vượt quyền; giải tạo/đăng ký từ DB; voucher CRUD từ UI được server kiểm tra và lưu, có luồng realtime đại diện.

### S2-M — Minh: nạp ví thật và ZaloPay sandbox

**Reviewer:** Trưởng nhóm. **Phối hợp:** Đại và Trường. **Phụ thuộc:** credentials/callback S1-M, hợp đồng giao dịch S1-T.

1. Hoàn thiện nạp tại quầy và thu tiền theo policy; giao diện ví/lịch sử/hồ sơ dùng API.
2. Tạo đơn ZaloPay sandbox qua adapter chung cho nạp ví và checkout món; xử lý callback đã xác minh và truy vấn trạng thái. Nối nạp ví và cung cấp điểm tích hợp xác nhận đơn cho Khang.
3. Kiểm tra thành công, chờ/thất bại và callback lặp cho cả hai mục đích; phát event sau commit, không dựa vào redirect.

**Nghiệm thu:** có lượt nạp ví và checkout đơn qua sandbox chính thức cùng S2-K, bằng chứng đã che secret; ví/ledger hoặc trạng thái đơn khớp; callback lặp không ghi nhận lần hai. Máy khách nhận trạng thái mới. Chưa gọi sandbox chính thức thì task chưa đạt.

### S2-P — Phúc: phiên tiền giờ, OP.GG và model chia đội đầu tiên

**Reviewer:** Trưởng nhóm. **Phối hợp:** Minh và Trường; Đại đối chiếu OP.GG/giải. **Phụ thuộc:** API ví S2-M, chứng từ S2-T, giải S2-D.

1. Hoàn thiện CRUD/bảo trì máy; mở/đóng phiên và công thức tiền server; cập nhật Home.
2. Gọi nghiệp vụ ví/chứng từ cùng transaction theo policy; kiểm tra phiên trùng và đóng lặp.
3. Nối OP.GG với người tham gia giải đã lưu; hiển thị thiếu/lỗi thật; hỗ trợ owner nối event vào hook SignalR.
4. Chuẩn bị dữ liệu và huấn luyện/tận dụng model đầu tiên; báo cáo kết quả đánh giá so với baseline. Cung cấp chức năng suy luận theo contract S1 để Đại nối một luồng đề xuất đội từ danh sách đăng ký thật.

**Nghiệm thu:** phiên đóng có tiền/ledger/chứng từ nhất quán; máy/phiên hiển thị trên hai máy; OP.GG thành công có nguồn thật, thất bại không tạo chỉ số giả. Có model ML với nguồn dữ liệu, phiên bản, báo cáo đánh giá và luồng đề xuất đội tích hợp; không chỉ có công thức chia điểm viết tay.

### S2-K — Khang: đặt món và áp voucher

**Reviewer:** Trưởng nhóm. **Phối hợp:** Trường và Minh. **Phụ thuộc:** rule/tồn S2-T, ví/adapter ZaloPay S2-M, phiên S2-P.

1. Nối đặt món, trạng thái xử lý và phương thức ví/tiền mặt/ZaloPay sandbox qua adapter của Minh; giữ snapshot giá/giảm giá và trạng thái chờ thanh toán.
2. Áp voucher món ăn bằng rule của Trường; quản lý lượt dùng, ví và tồn trong transaction tương ứng. Với gateway, thực hiện chính sách giữ chỗ/hết hạn và xác nhận sau callback đã kiểm tra; không giữ transaction SQL trong lúc chờ provider.
3. Nối gợi ý món/giỏ hàng với menu thật; kiểm tra thiếu tiền/tồn, giá sửa trên client và voucher sai.

**Nghiệm thu:** admin/client thấy một đơn thật; giá/giảm giá do server xác nhận; voucher hợp lệ có tác dụng và không hợp lệ bị từ chối; thay đổi đơn/tồn được đồng bộ.

### S2-T — Trường: kho, chứng từ đầu-cuối và API voucher

**Reviewer:** Trưởng nhóm. **Phối hợp:** Khang; Minh đối chiếu tiền, Đại đối chiếu hợp đồng UI. **Phụ thuộc:** S1-T; nguồn giao dịch phiên/đơn/ví.

1. Hoàn thiện nhập/điều chỉnh tồn và API chứng từ tối thiểu cho phiên/đơn.
2. Triển khai CRUD/rule voucher: phần trăm/cố định, ngày, ngưỡng đơn, hạn mức; kiểm soát lượt dùng đồng thời cùng đơn.
3. Nối màn hình đọc/in chứng từ và truy vấn báo cáo thô; đối chiếu tiền mẫu, tồn và lượt voucher.

**Nghiệm thu:** tất cả loại voucher đã duyệt được kiểm tra ở server; tồn không âm; giao dịch phát sinh có chứng từ đọc được; doanh thu thô đối chiếu được với nguồn.

**Cổng S2:** demo khách → nạp ví/sandbox → mở phiên → gọi món có voucher → xử lý → đóng phiên → chứng từ; thêm một đơn thanh toán trực tiếp bằng ZaloPay sandbox và luồng tạo/đăng ký giải/OP.GG/đề xuất đội ML. Mọi họ chức năng đã có đường chạy, không để giải đấu/promo bắt đầu ở S3.

## 8. Sprint 3 — 10 đến 15/11: hoàn thành chức năng

### S3-D — Đại: đủ luồng quyền, nhân viên, giải đấu và promo UI

**Reviewer:** Trưởng nhóm. **Phối hợp:** Minh và Phúc; Trường đối chiếu voucher. **Phụ thuộc:** S2-D, adapter OP.GG và rule promo.

1. Hoàn thiện trạng thái giải, đăng ký/hủy theo policy và màn hình quản trị/client; tích hợp đề xuất ML của Phúc, cho xem/xác nhận đội, lưu kết quả và đồng bộ sau commit.
2. Hoàn thiện tài khoản/nhân viên, lỗi form và màn hình voucher; loại placeholder khỏi các luồng bắt buộc.
3. Kiểm tra quyền dữ liệu chéo tài khoản, hành vi giải trùng/đóng đăng ký và đồng bộ hai máy.

**Nghiệm thu:** toàn bộ auth/staff/tournament/promo UI đã dùng API; giải hoạt động theo policy đã chốt; không có thao tác admin chỉ được bảo vệ bằng ẩn nút.

### S3-M — Minh: hoàn chỉnh ví, đối soát và độ tin cậy sandbox

**Reviewer:** Trưởng nhóm. **Phối hợp:** Trường và Đại. **Phụ thuộc:** các transaction phiên/đơn/hủy và sandbox S2-M.

1. Hoàn thiện trừ/hoàn ví, lịch sử/bộ lọc và hồ sơ; đối chiếu mọi nguồn phát sinh.
2. Xử lý callback trễ/lặp, sai chữ ký/số tiền, truy vấn đồng thời và restart theo trạng thái đã lưu cho cả nạp ví và thanh toán đơn; phối hợp Khang xử lý hết hạn/hủy/hoàn gateway theo policy.
3. Kiểm tra ví/ledger cùng giao dịch, phân quyền dữ liệu và realtime số dư; thu bằng chứng sandbox chính thức.

**Nghiệm thu:** không cộng/trừ/hoàn hai lần; không âm ví; giao dịch lỗi không cập nhật nửa chừng; đầy đủ trạng thái nạp ví và checkout sandbox cùng khả năng đối soát.

### S3-P — Phúc: hoàn thiện ML chia đội, Home/dashboard, phiên và realtime

**Reviewer:** Trưởng nhóm. **Phối hợp:** Đại và Minh; Trường đối chiếu báo cáo. **Phụ thuộc:** dữ liệu API của cả nhóm.

1. Hoàn thiện Home/dashboard, tiền giờ, phiên/bảo trì và hành vi khi restart/hết tiền theo policy.
2. Hoàn thiện OP.GG, refresh/lỗi và tích hợp thống kê giải; hoàn thiện model/chia đội theo tiêu chí S1, phối hợp Đại lưu phiên bản/snapshot/kết quả và xử lý dữ liệu thiếu.
3. Chạy đo LAN cho đủ các luồng đại diện; kiểm tra nhóm nhận quyền, reconnect và refetch; sửa nguyên nhân chậm/mất trạng thái.

**Nghiệm thu:** mọi màn hình phụ trách dùng dữ liệu API; hai máy đạt mục tiêu 2 giây theo phép đo; reconnect khôi phục trạng thái đúng, không dựa vào storage event. ML đạt tiêu chí đánh giá đã chốt; chia đội đúng ràng buộc, không trùng/thiếu người; luồng đề xuất–xác nhận–lưu–đồng bộ chạy đầu-cuối.

### S3-K — Khang: đủ vòng đời đơn, voucher và gợi ý món

**Reviewer:** Trưởng nhóm. **Phối hợp:** Trường và Minh. **Phụ thuộc:** rule voucher/hoàn lượt, refund ví, tồn và chứng từ.

1. Hoàn thiện xử lý/hủy từng món hoặc đơn theo policy, thu tiền mặt, hoàn ví/tồn/lượt voucher và trạng thái khách/admin.
2. Hoàn thiện tìm/lọc/menu/gợi ý/giỏ; giữ snapshot giá/giảm giá, chặn thao tác sai trạng thái và gửi lặp.
3. Kiểm tra hai request tranh tồn/lượt voucher cuối, lỗi giữa transaction, retry mạng, checkout ZaloPay chờ/hết hạn/callback trễ và đối chiếu hóa đơn.

**Nghiệm thu:** toàn bộ luồng đơn và voucher hoạt động đầu-cuối; không vượt tồn/hạn mức; tổng thu/hoàn đúng snapshot; lỗi không tạo đơn hay thu tiền trùng.

### S3-T — Trường: đủ kho, hóa đơn, báo cáo và voucher

**Reviewer:** Trưởng nhóm. **Phối hợp:** Khang và Minh; Phúc đối chiếu hiển thị. **Phụ thuộc:** nguồn giao dịch đã ổn định từ phiên/đơn/ví.

1. Hoàn thiện kho/lịch sử; hóa đơn admin/client, chi tiết, lọc, in/xuất và báo cáo như các chức năng hiện có.
2. Hoàn thiện rule/CRUD voucher, hạn mức đồng thời, snapshot và chính sách hủy đã chốt; phối hợp UI với Đại.
3. Đối chiếu doanh thu/hoàn tiền/giảm giá, tách nạp ví khỏi doanh thu; cập nhật DBML/SQL mô tả theo migration.

**Nghiệm thu:** đủ kho/chứng từ/báo cáo/promo backend; báo cáo khớp khoản thu theo policy; chứng từ không thay đổi theo giá/voucher sửa sau này; không còn luồng cần dữ liệu demo.

**Cổng S3 ngày 15/11:** mọi họ chức năng đều hoàn thành API–UI–SQL, có kiểm tra nghiệp vụ tương ứng và một lượt demo hai máy. Task chưa đạt giữ trạng thái chưa đạt và báo ảnh hưởng; không đổi nhãn thành “mở rộng tùy chọn”.

## 9. Sprint 4 — 15 đến 19/11: hồi quy và bàn giao

### S4-D — Đại: quyền, giải đấu và điều phối bản nộp

**Reviewer:** Trưởng nhóm. **Phối hợp:** Minh và Phúc. **Phụ thuộc:** cổng S3, danh sách lỗi chung.

1. Chạy lại ma trận quyền/auth/staff/giải/promo UI và lỗi dữ liệu chéo tài khoản.
2. Sửa lỗi thuộc module, review cấu hình/secret và phối hợp hướng dẫn khởi chạy.
3. Chạy kịch bản thuyết trình toàn hệ thống; ghi bằng chứng, giới hạn và trạng thái kiểm tra thật.

**Nghiệm thu:** không còn lỗi chặn quyền/giải/UI phụ trách; bản demo và tài liệu mô tả đúng kết quả đã kiểm tra.

### S4-M — Minh: hồi quy ví/ZaloPay và chứng cứ sandbox

**Reviewer:** Trưởng nhóm. **Phối hợp:** Đại và Trường. **Phụ thuộc:** sandbox/callback còn hoạt động, dữ liệu nghiệm thu.

1. Chạy lại nạp tại quầy, thanh toán ví, refund và cả nạp ví/checkout qua ZaloPay sandbox, gồm callback sai/lặp/trễ.
2. Đối chiếu số dư–ledger–giao dịch gateway; sửa lỗi và kiểm tra lại sau restart.
3. Chuẩn bị cấu hình sandbox/hướng dẫn tái hiện, log đã che secret và trạng thái cần xử lý nếu provider lỗi.

**Nghiệm thu:** có giao dịch sandbox chính thức tái hiện được; không tiền thật; không coi redirect/fixture là chứng cứ; ví/ledger đối chiếu được.

### S4-P — Phúc: nghiệm thu ML, LAN và khả năng phục hồi

**Reviewer:** Trưởng nhóm. **Phối hợp:** Đại; các owner xác nhận event của mình. **Phụ thuộc:** bản tích hợp S3 và hai máy demo.

1. Chạy bộ đo 10 lượt mỗi luồng theo tiêu chí 2 giây, kèm ngắt/kết nối lại và restart.
2. Kiểm tra Home/dashboard/máy/phiên/OP.GG, trạng thái provider lỗi và UI responsive.
3. Sửa lỗi realtime; ghi IP/cổng/cấu hình LAN và quy trình khởi động hai máy.
4. Chạy lại đánh giá ML và các trường hợp thiếu dữ liệu, không đủ người, vai trò lệch, model lỗi; đóng gói model cùng hướng dẫn huấn luyện/suy luận, dữ liệu đánh giá và giới hạn kết quả. Diễn tập chia đội với Đại trên hai máy.

**Nghiệm thu:** báo cáo thời gian từng lượt, dữ liệu đúng sau reconnect; không lộ event khác quyền; người khác làm theo hướng dẫn chạy được hai máy. Có model/version, báo cáo đánh giá ML và hướng dẫn tái hiện chia đội; giới hạn và luồng dự phòng được ghi đúng.

### S4-K — Khang: hồi quy đơn/menu/voucher

**Reviewer:** Trưởng nhóm. **Phối hợp:** Minh và Trường. **Phụ thuộc:** ví/tồn/voucher/chứng từ tích hợp.

1. Chạy lại đặt/hủy/thu/hoàn, gợi ý món, giỏ và các bộ lọc.
2. Chạy ca tranh tồn/lượt voucher, double click/retry, sửa giá client và lỗi giữa giao dịch.
3. Sửa lỗi, đối chiếu receipt; chuẩn bị sản phẩm/đơn demo và bằng chứng luồng khách–admin.

**Nghiệm thu:** đơn–ví–tồn–lượt voucher–chứng từ nhất quán; toàn bộ hành vi bắt buộc có kết quả kiểm tra.

### S4-T — Trường: dữ liệu, báo cáo và gói DB bàn giao

**Reviewer:** Trưởng nhóm. **Phối hợp:** Khang và Minh; Phúc đối chiếu biểu đồ. **Phụ thuộc:** số liệu các luồng và migrations đã merge.

1. Chạy lại kho/hóa đơn/in/xuất/báo cáo/promo rule với dữ liệu nghiệm thu.
2. Tạo DB thử mới từ migrations/seed; đối chiếu số liệu và cập nhật ERD/hướng dẫn.
3. Sửa lỗi, đóng gói dữ liệu demo và bảng đối chiếu tiền/tồn/giảm giá để cả nhóm giải thích được.

**Nghiệm thu:** cài từ DB thử mới được; số liệu có nguồn kiểm chứng; tài liệu khớp schema và không ghi đè dữ liệu môi trường dùng chung.

**Ngày 20–21/11:** cả nhóm xử lý lỗi chặn còn lại, chạy lại phần bị tác động, đóng gói và diễn tập lần cuối. Không thêm tính năng mới.

## 10. Quy trình task, PR và kiểm tra

1. Mỗi gói S1-D…S4-T là một cụm phạm vi để đọc. Mỗi mục Task có mã như S1-D-1 trong [backlog chi tiết](../codex/SPRINT_TASKS.md) là một thẻ Jira độc lập, gán sprint/người phụ trách và nhận issue key thực tế khi tạo. Các bước nhỏ là checklist trong thẻ; không tạo Task cha/Sub-task hay issue cho từng bước.
2. Nhánh theo thẻ Jira từ nhánh tích hợp mới nhất, ví dụ `feat/SCRUM-123-zalopay-callback` (key minh họa). PR nhỏ, tách di chuyển thư mục khỏi thay đổi nghiệp vụ; không giữ một nhánh cá nhân suốt lịch rồi merge một lần.
3. Trước khi sửa giao điểm, ghi endpoint/payload/trạng thái/lỗi và event. Contract đủ rõ mới để hai người triển khai song song.
4. PR chạy build/check liên quan. Tiền/quyền/tồn/promo/gateway cần kiểm tra nghiệp vụ và API/DB có ý nghĩa; browser smoke bảo vệ luồng đã tích hợp. Không đợi S4 mới kiểm thử. Một Task có thể chia commit/PR nhỏ để bàn giao sớm; gửi kết quả review tổng hợp theo Task trực tiếp trong chat theo mẫu tin nhắn ở mục 6 của [hướng dẫn review](../codex/REVIEW_GUIDE.md), chỉ tạo file báo cáo khi người dùng yêu cầu rõ. Không tách một thẻ chỉ vì có thêm bước test hoặc tài liệu.
5. Trường điều phối thứ tự migration; owner module viết entity/config của mình, Trưởng nhóm review và duyệt. Không sửa/xóa migration đã áp dụng ở DB chung; tạo migration tiếp theo.
6. Đầu ngày cập nhật đã xong/vướng/phụ thuộc. Cuối ngày có thay đổi thì kiểm tra bản tích hợp; ưu tiên lỗi chặn người khác. Chuyển hỗ trợ theo tải quan sát được, không dựa vào số lượng tên task.
7. Routes/layout/provider là phần chung có reviewer. Mỗi người chịu tích hợp module mình, không đẩy tất cả cho Đại hoặc Phúc.
8. Mỗi sprint demo và ghi đạt/chưa đạt. Với Task triển khai, “xong” nghĩa là UI gọi API, DB lưu đúng, quyền ở server, lỗi rõ, kiểm tra phù hợp và event đã nối nếu có; chỉ chạy localStorage không đạt. Với Task thiết kế, bàn giao tài liệu theo tiêu chí. Mọi Task cần được Trưởng nhóm duyệt trước khi ghi Done.
9. Chưa có issue hay tin nhắn giao việc bên ngoài được tạo từ tài liệu này. Các owner là baseline để nhóm triển khai và theo dõi.

Mẫu task nhỏ:

```text
Mã tham chiếu / Jira key thực tế / tên Task:
Sprint / người phụ trách / reviewer:
Luồng người dùng và phạm vi:
Phụ thuộc (is blocked by) / endpoint / schema / event:
Tiêu chí nghiệm thu:
Kiểm tra / bằng chứng:
PR / trạng thái / blocker:
```

## 11. Quyết định nghiệp vụ cần nhóm chốt trong S1

Đây là quyết định triển khai trong phạm vi đã duyệt, không hỏi lại stack, số người, ngày, giờ làm, LAN hay lựa chọn sandbox.

**Cập nhật 08/11/2026 cho SCRUM-11:** theo yêu cầu trực tiếp của người dùng, AI được ủy quyền chọn
phương án hợp lý cho các giao điểm checkout đang chờ. Các lựa chọn đã ghi tại
[hợp đồng checkout, mục 11.2](../Backend/docs/004-order-checkout-contract.md#112-quyết-định-thiết-kế-đã-chốt).
Đây là nguồn chốt thiết kế cho checkout và phí phiên trước chi ví, không ghi nhận các owner đã phản hồi đồng ý
hoặc Trưởng nhóm đã duyệt; việc tích hợp code/migration/kiểm chứng theo mục 11.3 của hợp đồng.

| Quyết định | Chịu trách nhiệm |
| --- | --- |
| Mapping tài khoản khách/nhân viên và quyền từng endpoint; quy tắc khóa tài khoản | Đại + Minh |
| Phiên/giao điểm chi ví: D09 chọn phí tích lũy theo giây, ceil tổng tiền, worker 60 giây và quyết toán trước debit món; không thu trả trước chu kỳ song song. Điều kiện mở/đóng, hết tiền/restart và công thức theo [contract SCRUM-7](../Backend/docs/002-computer-session-contract.md) | Trưởng nhóm điều phối/duyệt cuối; Phúc + Minh + Trường phối hợp; Khang gọi điểm tích hợp |
| Nạp tại quầy: người dùng chốt 08/11 thu tiền xong mới cộng ví/ledger cùng transaction, không nạp nợ; không nhập `CreditedUnpaid` như nạp thành công. Contract và điểm gọi credit/debit/refund ở [SCRUM-5](../Backend/docs/003-customer-wallet-contract.md); writer/migration ở S2 | Minh + Trường |
| Đơn: D01–D03/D05–D08 chốt trừ ví khi tạo, cash thu sau confirm, chỉ hủy dòng Pending, tồn khả dụng Sale/Return một lần; ZaloPay giữ 15 phút, thu muộn không hồi sinh đơn, hoàn về nguồn gốc sau duyệt; Refund Failed có đường thay thế | Khang + Minh + Trường |
| Voucher trong checkout: D04 chốt một mã, ngưỡng trước giảm, hiệu lực server, AwayFromZero/phân bổ snapshot; usage chiếm lượt khi tạo, partial không nhả, full cancel/expired nhả một lần; không giảm vượt tiền món | Trường + Khang + Đại |
| Giải đấu: UI đang miễn phí nhưng DBML có phí; chọn policy nhất quán, thời hạn đăng ký/hủy và điều kiện đủ đội | Đại + Minh + Phúc |
| ML chia đội: [thiết kế SCRUM-9 v1.1](../docs/contracts/ml-team-balancing-pipeline.md) đã đối chiếu S1-D-3 / SCRUM-4 v0.7, bổ sung mapping preview → confirm ML/Balanced và binding token/proposal/roster/model; B1 không còn là thiếu contract. Dataset có nhãn/model, snapshot SQL và kiểm chứng tích hợp còn phải thực hiện; chờ owner đối chiếu mapping mới và Trưởng nhóm duyệt cuối | Phúc + Đại |
| Chứng từ/doanh thu dùng chung: thời điểm ghi nhận, hoàn tiền, giảm giá, phân biệt nạp ví và doanh thu | Trường + Khang + Phúc |
| Chứng từ/checkout: D11 chọn thiết kế 1.7 ghim trong hợp đồng, Payments neo nguồn trước hóa đơn, khóa phát hành bền vững và mốc nguồn/xác nhận; thu/hoàn theo nguồn, không cộng đôi ví/Payment và không tính nạp ví thành doanh thu | Trường + Minh + Khang + Phúc |
| Giờ hoàn ZaloPay khi Query Refund không trả thời điểm: phân biệt thời điểm provider và thời điểm hệ thống xác nhận, ngày báo cáo theo mục 6.1.1 của [contract SCRUM-12](../docs/contracts/inventory-invoice-voucher.md); là đề xuất tại nguồn 05/11, được D11 chọn cho checkout ngày 08/11 | Minh + Trường; Trưởng nhóm review tích hợp |
| Khóa phát hành hóa đơn qua retry/restart và hóa đơn 0 đồng: InvoiceIssueRequests lưu cùng transaction theo mục 6.3.1 của [contract SCRUM-12](../docs/contracts/inventory-invoice-voucher.md); là đề xuất tại nguồn 05/11, được D11 chọn cho checkout ngày 08/11 | Trường; Khang/Phúc đối chiếu caller; Trưởng nhóm review tích hợp |

Credentials ZaloPay và callback HTTPS là **task phụ thuộc S1-M**, không phải lựa chọn cần người dùng xác nhận lại. Kết quả kickoff phải được ghi vào hợp đồng/schema trước khi các module triển khai khác nhau.

Gói SCRUM-6 / S1-M-2 ghép lên base `d2141da`, giữ realtime và các module tích hợp; bằng chứng lịch sử ở `4816802`; xem
[contract ZaloPay](../Backend/docs/005-zalopay-sandbox-contract.md). Cấu hình, HMAC/fixture và probe HTTPS
thuộc S1; adapter ghi tiền/migration gateway thuộc S2. Kết quả kiểm tra provider thực tế được ghi riêng,
không suy từ probe hoặc cấu hình hợp lệ thành thanh toán thành công.

Ngày 03/11/2026, người dùng cho phép soạn contract bước 1 của SCRUM-7 (`S1-P-1`). [Contract máy/phiên](../Backend/docs/002-computer-session-contract.md) ghi phương án trạng thái, tiền giờ tính theo giây/làm tròn tổng lên một đồng, thu chênh lệch mỗi 60 giây/khi đóng/trước debit khác, giới hạn hết tiền và phục hồi SQL sau restart; đồng thời mô tả API đọc máy/loại/phòng và sửa trạng thái theo permission. Snapshot giá, liên kết khoản thu–ledger, khóa chống lặp và các ràng buộc phiên/hóa đơn cần migration riêng ở S2; SCRUM-7 không triển khai thu tiền hoặc đổi schema. Login `S1-D-2` đã được tích hợp vào nhánh SCRUM-7 tại `903e055`.

Ngày 05/11/2026, Trưởng nhóm yêu cầu giữ merge SCRUM-12 (`229d7af`, PR #8) và bổ sung phần thiếu để
review tiếp. [Contract SCRUM-12](../docs/contracts/inventory-invoice-voucher.md) mục 6.1.1/6.3.1/9.4
ghi phương án thời gian hoàn và lưu khóa phát hành, cùng trách nhiệm giao nhận. Đây là đề xuất bổ sung,
chưa phải phản hồi đồng thuận của Đại/Minh/Phúc/Khang hoặc quyết định nghiệm thu; P1 liên module còn mở.

Ngày 06/11/2026, Trưởng nhóm yêu cầu hoàn tất kiểm chứng UI–API–SQL và giải quyết conflict với `dev`, giữ cả ghi chú SCRUM-7/SCRUM-12. Trưởng nhóm điều phối và chốt phương án chung; bàn giao code SCRUM-7 không phải chờ từng owner xác nhận. Các điểm giao nhau cần xử lý trước S2 (thời điểm tạo Payment theo từng khoản thu, actor tự đóng và phạm vi hóa đơn tiền giờ/gộp món) được ghi cụ thể ở mục 5 của contract SCRUM-7, không coi chúng là lỗi của API đổi trạng thái máy.
