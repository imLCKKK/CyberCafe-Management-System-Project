# 003 – Hợp đồng nhân viên, đổi mật khẩu, giải đấu, đăng ký và đội

| Thông tin | Nội dung |
| --- | --- |
| Task | Đặc tả endpoint, DTO, trạng thái, lỗi cho nhân viên, đổi mật khẩu, giải đấu, đăng ký, đội; chốt phí giải, mapping UI/DBML, dữ liệu đăng ký cho Phúc |
| Owner | Đại; Minh đối chiếu ví/ledger, Phúc nhận dữ liệu đăng ký và thuật toán chia đội |
| Reviewer cuối | Trưởng nhóm |
| Phiên bản | 0.6, ngày 10/11/2026; bổ sung sau PR #18: thống nhất fallback `rank-snake-v1` theo rank OP.GG của SCRUM-9 |
| Backend đích | CyberManagement .NET 10 / SQL Server, nền 35 bảng |
| Phụ thuộc | `S1-D-1` (nội dung chưa được đối chiếu), 001 (identity, đã đối chiếu), 002 (phiên/phí giờ), 004 (idempotency, khóa, lỗi) |
| File đi kèm | `tournament-registrations-seed.csv` (dữ liệu mẫu cho Phúc); quyết định và mapping UI/DBML tại Phụ lục A, B |

**Trạng thái:** bản hợp đồng cập nhật theo review Trưởng nhóm, chưa phải API đang chạy. Các phương án ở Phụ lục A là lựa chọn thiết kế để triển khai; xác nhận liên module của Minh/Phúc và duyệt cuối của Trưởng nhóm được ghi riêng, không suy diễn từ việc cập nhật tài liệu.

**Quyết định bổ sung ngày 10/11/2026:** Trưởng nhóm chọn baseline/fallback `rank-snake-v1` theo [SCRUM-9, mục 5](https://github.com/imLCKKK/CyberCafe-Management-System-Project/blob/1b2ceb9b34cf91b81c61d5dc5c30b5918487527b/docs/contracts/ml-team-balancing-pipeline.md#L146-L153), thay cách xếp theo SkillScore tổng hợp trong v0.5 đã merge ở PR #18. Giữ yêu cầu ML; không chờ xác nhận riêng của Minh/Phúc để chốt phương án này. Bản bổ sung vẫn do Trưởng nhóm duyệt cuối.

**Thay đổi v0.3 (sau khi đọc 001):** dùng đúng tên permission của ma trận quyền (không tạo permission mới); `Username`/`PasswordHash` đã có nên không còn là cột đề xuất; JWT không bị thu hồi phía server nên bỏ giả định "thu hồi token" và bỏ giới hạn thử sai mật khẩu; đổi route đổi mật khẩu về `/api/auth/...`; staff đăng ký thay khách được hỗ trợ (`tournament.register`); chia đội do `team.assign` chạy ML.

**Thay đổi v0.4 (theo review Trưởng nhóm):** thêm `riotId` vào request/response/lưu trữ/export; cấm đăng ký lại sau khi đã huỷ để khóa ledger theo registration không bị tái sử dụng; sửa dấu công thức doanh thu; hoàn thiện `UpdateTeamRequest`; chuẩn hóa timestamp CSV; ghi lựa chọn phí/ledger và các xác nhận owner còn chờ.

**Thay đổi so với v0.1:** schema thật cho thấy đội do nhân viên/thuật toán chia theo điểm kỹ năng chứ không phải khách tự lập đội; `EntryFee` tính theo từng đăng ký; `Employees` không có username/email/mật khẩu. Các mục tương ứng đã viết lại.

## 1. Căn cứ và giới hạn

- Schema đối chiếu: `gaming_cafe_schema.sql` (35 bảng). Bảng liên quan: `Roles`, `Permissions`, `RolePermissions`, `Employees`, `AuditLogs`, `Customers`, `WalletTransactions`, `Games`, `GameRanks`, `Tournaments`, `TournamentRegistrations`, `Teams`, `TeamMembers`.
- Quy ước chung lấy từ [001-monorepo](001-monorepo.md), [002](002-computer-session-contract.md), [004](004-order-checkout-contract.md).
- **Đã đối chiếu 001-monorepo:** `Employees` có `Username` (unique) và `PasswordHash`; `Customers` đăng nhập bằng `Phone` + `PasswordHash` (các cột này do migration EF thêm, không có trong `gaming_cafe_schema.sql`); JWT access token sống 20 phút, không có refresh/revocation phía server; sai mật khẩu không khóa tài khoản; khách `Locked` không nạp/trừ ví; mọi thay đổi nhân viên ghi `AuditLogs` với `EntityType='Employee'`.
- **Chưa đối chiếu được:** nội dung `S1-D-1`, mockup UI, mã nguồn `RolePermissionCatalog`.
- Permission dùng đúng tên trong ma trận quyền của 001 (`employee.view/manage`, `tournament.view/manage/register`, `team.assign`, `game.manage`, `wallet.refund`); không tạo permission mới. Điểm ma trận chưa đủ ghi ở mục 3.4.

## 2. Quy ước và bất biến

- JSON camelCase; enum là chuỗi phân biệt hoa/thường; ID là int dương; thời gian ISO 8601 UTC (`Z`), UI hiển thị +07:00; một mốc `now` server mỗi transaction.
- Tiền là số nguyên VND, tối đa `999_999_999`; phần lẻ bị từ chối (cùng 004). Ví (`WalletTransactions.Amount/BalanceAfter`) đã là `decimal(18,0)`; `Tournaments.EntryFee` hiện còn `decimal(12,2)`, hướng đích `decimal(18,0)` theo 004/D10.
- `CustomerId`, `EmployeeId` là hai miền int khác nhau; principal lấy từ JWT, không nhận từ body.
- Trường ngoài DTO, hoặc client gửi điểm kỹ năng/phí/trạng thái/người xử lý → 400.
- Mọi POST/PUT/PATCH/DELETE ghi dữ liệu bắt buộc `Idempotency-Key` (8..100 ký tự `[A-Za-z0-9._:-]`), quy tắc replay/fingerprint như 004 mục 8.1.
- Aggregate có `version` (rowversion base64); sửa dùng `expectedVersion`, cũ → 409 `VersionConflict`.
- Mật khẩu không xuất hiện trong response, log, audit hay lỗi.
- Không xóa cứng nhân viên, giải, đăng ký đã có lịch sử; dùng `IsActive`/`Status`.

## 3. HTTP và quyền

Tất cả route yêu cầu JWT; trạng thái tài khoản kiểm tra từ DB tại thao tác. Nhân viên `IsActive=0` nhận 403.

### 3.1. Nhân viên và đổi mật khẩu

| Method / route | Đầu vào → kết quả | Quyền server |
| --- | --- | --- |
| `GET /api/admin/roles` | → RoleDto[] | `employee.view` |
| `GET /api/admin/employees` | `page,pageSize(1..100),isActive?,roleId?,q?` → EmployeePageDto | `employee.view` |
| `GET /api/admin/employees/{employeeId}` | → 200 EmployeeDto | `employee.view` |
| `POST /api/admin/employees` | CreateEmployeeRequest → 201 EmployeeDto | `employee.manage` |
| `PUT /api/admin/employees/{employeeId}` | UpdateEmployeeRequest → 200 EmployeeDto | `employee.manage`; không đổi role của chính mình |
| `POST /api/admin/employees/{employeeId}/deactivate` | VersionRequest + `reason` → 200 | `employee.manage`; không tự vô hiệu mình; không vô hiệu Admin cuối |
| `POST /api/admin/employees/{employeeId}/activate` | VersionRequest → 200 | `employee.manage` |
| `POST /api/admin/employees/{employeeId}/password-reset` | VersionRequest → 200 PasswordResetDto | `employee.manage`; không áp cho chính mình |
| `POST /api/auth/change-password` | ChangePasswordRequest → 204 | Token nhân viên, chỉ tài khoản của mình |
| `POST /api/auth/customer/change-password` | ChangePasswordRequest → 204 | Token khách, chỉ tài khoản của mình |

Theo 001, `employee.manage` chỉ Admin có; Manager chỉ xem nhân viên. Đổi/reset mật khẩu **không thu hồi** JWT đang dùng (001 mục 5): token tự hết hạn theo TTL cấu hình (mặc định 20 phút, cấu hình 1..30 phút); muốn thu hồi ngay cần cơ chế lưu DB. Vô hiệu hoá nhân viên vẫn có hiệu lực ngay vì trạng thái `IsActive` kiểm tra từ DB ở mỗi thao tác (004 mục 3). Mỗi thay đổi ghi `AuditLogs` (`EntityType='Employee'` hoặc `'Customer'`, `Action=PasswordChanged/PasswordReset/...`), không ghi giá trị mật khẩu. Sai mật khẩu hiện tại không khóa tài khoản (khớp 001 mục 4).

### 3.2. Giải đấu và đăng ký

| Method / route | Đầu vào → kết quả | Quyền server |
| --- | --- | --- |
| `GET /api/client/games` | → GameDto[] (`IsActive`) | Customer |
| `GET /api/client/games/{gameId}/ranks` | → RankDto[] theo `RankOrder` | Customer |
| `GET /api/client/tournaments` | `page,pageSize,status?` → TournamentPageDto (không gồm Draft) | Customer |
| `GET /api/client/tournaments/{id}` | → 200 TournamentDto | Customer |
| `POST /api/client/tournaments/{id}/registrations` | RegisterRequest → 201 RegistrationDto | Customer Active |
| `GET /api/client/registrations` | page → RegistrationPageDto của chính khách | Customer |
| `POST /api/client/registrations/{registrationId}/cancel` | CancelRegistrationRequest → 200 | Customer sở hữu |
| `GET /api/client/tournaments/{id}/my-team` | → 200 TeamDto; 404 nếu chưa chia đội | Customer đã đăng ký |
| `GET /api/admin/games`, `GET /api/admin/games/{gameId}/ranks` | → như client, thêm game ngừng dùng | `tournament.view` |
| `GET /api/admin/tournaments`, `GET /api/admin/tournaments/{id}` | page, `status?` → DTO | `tournament.view` |
| `POST /api/admin/tournaments` | CreateTournamentRequest → 201 | `tournament.manage`; `CreatedBy` = EmployeeId từ JWT |
| `PUT /api/admin/tournaments/{id}` | UpdateTournamentRequest → 200 | `tournament.manage` |
| `POST /api/admin/tournaments/{id}/transitions` | TransitionRequest → 200 | `tournament.manage`; huỷ giải thêm `wallet.refund` |
| `GET /api/admin/tournaments/{id}/registrations` | `page,pageSize,status?` → RegistrationPageDto | `tournament.view` |
| `GET /api/admin/tournaments/{id}/registrations/export` | `format=csv\|json,status?` → file gồm `riotId` | `tournament.manage` |
| `POST /api/admin/tournaments/{id}/registrations` | StaffRegisterRequest → 201 RegistrationDto | `tournament.register` (đăng ký thay khách) |
| `PATCH /api/admin/tournaments/{id}/registrations/{registrationId}` | UpdateRegistrationRequest → 200 | `tournament.manage`; chỉ khi chưa chia đội |
| `POST /api/admin/tournaments/{id}/registrations/{registrationId}/cancel` | CancelRegistrationRequest → 200 | `tournament.manage` + `wallet.refund` nếu có hoàn |

### 3.3. Đội (do nhân viên chia, không có đội trưởng do khách lập)

| Method / route | Đầu vào → kết quả | Quyền server |
| --- | --- | --- |
| `POST /api/admin/tournaments/{id}/teams/preview` | TeamPreviewRequest → 200 TeamPlanDto, **không ghi dữ liệu**; nếu đã có đội thì trả snapshot đã chốt, không tạo plan/token mới | `team.assign` |
| `POST /api/admin/tournaments/{id}/teams` | ConfirmTeamsRequest → 201 TeamPageDto; replay cùng key trả kết quả cũ, key mới khi đội vẫn tồn tại trả 409 `TeamsAlreadyFormed` | `team.assign` |
| `GET /api/admin/tournaments/{id}/teams` | page → TeamPageDto | `tournament.view` |
| `PUT /api/admin/teams/{teamId}` | UpdateTeamRequest (đổi tên) → 200 | `team.assign`; chỉ trước `InProgress` |
| `POST /api/admin/tournaments/{id}/teams/swap` | SwapRequest → 200 TeamPageDto | `team.assign` |
| `DELETE /api/admin/tournaments/{id}/teams` | VersionRequest → 200 (xoá toàn bộ đội để chia lại) | `team.assign`; chỉ khi chưa `InProgress` |

Khách khác/đăng ký không thuộc mình → 404.

### 3.4. Điểm ma trận quyền 001 cần nhóm xác nhận

- `tournament.register` có cả Cashier, nhưng 001 không có permission riêng cho việc trừ ví khách khi đăng ký thay. Contract chọn cho phép trong phạm vi đăng ký giải: khách phải `Active`, phí trừ vào ví của chính khách đó và ghi `AuditLogs` với EmployeeId nhân viên; Minh cần xác nhận luồng ledger (D-T12).
- Hoàn phí (huỷ đăng ký, huỷ giải) cần `wallet.refund` (chỉ Admin/Manager), nên Cashier đăng ký thay khách được nhưng không huỷ/hoàn được.
- Không có permission riêng cho đăng ký/đội: dùng `tournament.view/manage/register` và `team.assign`.

## 4. DTO

| DTO | Trường / kiểu / validation |
| --- | --- |
| RoleDto | `roleId`, `roleName` |
| EmployeeDto | `employeeId`, `fullName`, `phone: string?`, `roleId`, `roleName`, `isActive`, `createdAtUtc`, `username`, `version` (cần rowversion, mục 10). Không có hash/mật khẩu |
| CreateEmployeeRequest | `fullName` trim 1..150; `phone?` 8..20; `roleId` tồn tại; `username` 3..50 duy nhất không phân biệt hoa/thường; `initialPassword` theo chính sách |
| UpdateEmployeeRequest | `fullName`, `phone?`, `roleId` + `expectedVersion` |
| PasswordResetDto | `temporaryPassword` hiển thị đúng một lần; `mustChangePassword: true` |
| ChangePasswordRequest | `currentPassword`, `newPassword` 12..72 ký tự, ≥1 chữ và ≥1 số, khác mật khẩu cũ; `confirmNewPassword` bằng `newPassword` |
| GameDto / RankDto | `gameId`, `gameName`, `teamSize`, `isActive` / `rankId`, `rankName`, `rankOrder` |
| TournamentDto | `id`, `gameId`, `gameName`, `teamSize`, `name` 1..150, `registrationStartUtc`, `registrationEndUtc`, `startTimeUtc`, `entryFee` (≥0, **mỗi đăng ký**), `maxParticipants: int?`, `registeredCount`, `fullRefundBeforeUtc`, `refundPercentAfter` 0..100, `status`, `createdByEmployeeId`, `version` |
| CreateTournamentRequest | `gameId`, `name`, `registrationStartUtc`, `registrationEndUtc`, `startTimeUtc`, `entryFee`, `maxParticipants?` (bội số `teamSize`), `fullRefundBeforeUtc?` (mặc định = `registrationEndUtc`), `refundPercentAfter` (mặc định 0) |
| UpdateTournamentRequest | Đúng các trường của `CreateTournamentRequest` (`gameId`, `name`, `registrationStartUtc`, `registrationEndUtc`, `startTimeUtc`, `entryFee`, `maxParticipants?`, `fullRefundBeforeUtc?`, `refundPercentAfter`) + `expectedVersion`; không có `fullName`, `phone`, `roleId`. Sau đăng ký đầu tiên không đổi `gameId/entryFee/fullRefundBeforeUtc/refundPercentAfter` (409 `TournamentLocked`) |
| TransitionRequest | `toStatus`, `expectedVersion`, `reason?` (bắt buộc khi Cancelled) |
| RegisterRequest | `riotId` dạng `Tên#TAG` (trim từng phần, đúng một `#`, tên 1..100 ký tự, tag 1..20 ký tự, không ký tự điều khiển, tag không có khoảng trắng; chuẩn hóa thành `Tên#TAG`); `rankId` (thuộc game của giải), `preferredRole: string?` ≤50, `winRate: decimal?` 0..100 (2 chữ số lẻ), `hoursPlayed: int?` 0..100000. **Không nhận `skillScore`, phí, trạng thái** |
| StaffRegisterRequest | `customerId` + các trường của RegisterRequest; khách phải `Active`; phí trừ ví của khách (mục 3.4) |
| RegistrationDto | `id`, `tournamentId`, `customerId`, `customerName`, `riotId`, `rankId`, `rankName`, `preferredRole`, `winRate`, `hoursPlayed`, `skillScore`, `status`, `feeCharged`, `feeRefunded`, `registeredAtUtc`, `cancelledAtUtc?`, `cancelReason?`, `teamId: int?`, `version`. Danh sách client chỉ trả của chính khách |
| UpdateRegistrationRequest | `rankId?`, `preferredRole?`, `winRate?`, `hoursPlayed?`, `reason` 1..250, `expectedVersion`; server tính lại `skillScore`, ghi audit |
| CancelRegistrationRequest | `expectedVersion`, `reason` 1..250 |
| TeamPreviewRequest | `strategy: Balanced` (giá trị của contract này, ánh xạ baseline `rank-snake-v1` ở SCRUM-9), `expectedVersion` của giải |
| TeamPlanDto | `teams: { teamName, avgSkillScore, members: { registrationId, customerName, skillScore, rankName, assignedRole? }[] }[]`, `unassigned: int[]`, `scoreSpread` (max − min điểm TB giữa các đội), `planToken: UUID?` (có ở đề xuất mới; snapshot đã chốt không cấp token mới), `modelSource: Balanced\|ML` (fallback/ML), `modelVersion: string?` (version ML nếu dùng ML), `baselineVersion: string?` (`rank-snake-v1` khi `modelSource=Balanced`, null khi ML) |
| ConfirmTeamsRequest | `planToken` (nối liên kết với preview), `expectedVersion`, `teams: { teamName 1..100 duy nhất trong giải, members: { registrationId, assignedRole? }[] }[]`. Server kiểm tra lại và tính `AvgSkillScore`, **không tin điểm client**, lưu `modelSource`, `modelVersion`, `planSnapshot` vào DB |
| SwapRequest | `registrationIdA`, `registrationIdB` (khác đội), `expectedVersion` |
| UpdateTeamRequest | `name` trim 1..100 ký tự + `expectedVersion`; chỉ đổi tên khi giải ở `RegistrationClosed` hoặc `TeamsAssigned`, trước `InProgress`; tên không trùng (sau trim, không phân biệt hoa/thường) trong cùng giải |
| TeamDto | `id`, `tournamentId`, `name`, `avgSkillScore`, `members: TeamMemberDto[]`, `modelSource: Balanced\|ML?`, `modelVersion: string?`, `baselineVersion: string?` (đọc từ `planSnapshot`), `version` |
| TeamMemberDto | `registrationId`, `customerId`, `displayName`, `skillScore`, `assignedRole?` |
| Page / VersionRequest | `items,page,pageSize,totalCount` sắp `createdAt` giảm dần rồi id giảm dần / `expectedVersion` |

## 5. Trạng thái

### Giải đấu

| Từ → Sang | Điều kiện |
| --- | --- |
| `Draft` → `RegistrationOpen` | Đủ trường bắt buộc; game `IsActive`; `now >= registrationStartUtc` hoặc staff mở sớm |
| `RegistrationOpen` → `RegistrationClosed` | `now >= registrationEndUtc` (worker) hoặc staff đóng sớm |
| `RegistrationClosed` → `RegistrationOpen` | Chỉ khi `now < registrationEndUtc` và chưa chia đội |
| `RegistrationClosed` → `TeamsAssigned` | Đã chia đội; mọi đội đủ `teamSize`; confirm lưu metadata (modelSource, modelVersion, planToken từ preview) |
| `TeamsAssigned` → `InProgress` | `now >= startTimeUtc` hoặc staff bắt đầu |
| `InProgress` → `Completed` | Nhân viên xác nhận kết thúc |
| `TeamsAssigned` → `RegistrationClosed` | **Chia lại:** DELETE tất cả teams (sau này có thể Confirm lại với dữ liệu/key khác); mọi registration vẫn `Registered`, chưa hoàn phí |
| `Draft/RegistrationOpen/RegistrationClosed/TeamsAssigned` → `Cancelled` | Có `reason`; hoàn 100% mọi đăng ký `Registered` (mục 7) |
| Các tổ hợp khác | 409 `InvalidTournamentTransition` |

Ràng buộc: `registrationStartUtc < registrationEndUtc <= startTimeUtc`; `startTimeUtc > now` khi tạo.

### Đăng ký

`Registered` → `Cancelled`. Không có `Pending`: phí trừ cùng transaction chấp nhận đăng ký (phí 0 thì không có ledger). Khách tự huỷ chỉ khi giải `RegistrationOpen` và chưa chia đội; khi đã chia đội chỉ nhân viên huỷ được (xoá dòng `TeamMembers`, đội thiếu người, phải chia lại).

### Nhân viên

`IsActive` 1 ↔ 0. Vô hiệu hoá chặn đăng nhập và mọi request tiếp theo (trạng thái kiểm tra từ DB, không cần thu hồi token). Không còn Admin hoạt động → 409 `LastAdminProtected`.

## 6. Quy tắc nghiệp vụ

1. Mỗi khách một đăng ký trong một giải (`UNIQUE (TournamentId, CustomerId)`). Nếu registration đã `Cancelled`, mọi lần đăng ký tiếp theo trả 409 `AlreadyRegistered`; không khôi phục dòng cũ, không tạo registration mới. Quy tắc này giữ nguyên ID bất biến suốt vòng đời và ngăn tái sử dụng khóa thu/hoàn.
2. Đăng ký khi giải `RegistrationOpen`, `now` trong `[registrationStartUtc, registrationEndUtc)`, còn chỗ (`maxParticipants` nếu có), khách `Active`.
3. Hết chỗ cạnh tranh: kiểm tra dưới khóa Tournament; bên thua 409 `TournamentFull`, không ledger.
4. **SkillScore tổng hợp của đăng ký** là dữ liệu tham khảo do server tính, không dùng để sắp người hoặc đánh giá baseline/fallback `rank-snake-v1`; công thức được giữ cho dữ liệu này: `round(100 × (0.5 × rankOrder/maxRankOrder + 0.3 × winRate/100 + 0.2 × min(hoursPlayed,2000)/2000), 2)`; thiếu `winRate`/`hoursPlayed` coi là 0 và đánh dấu dữ liệu tự khai chưa xác minh. Điểm tính lại khi staff sửa đầu vào, không tính lại sau khi chia đội. Dữ liệu tự khai này không thay snapshot rank OP.GG và không tự trở thành feature/label ML.
5. **Chia đội** do nhân viên có `team.assign` chạy (001 ghi "chạy ML chia đội"). Preview trả `planToken` (UUID) và metadata cần lưu lại (`modelSource: Balanced|ML`, `modelVersion: string?`); Confirm nhận `planToken` để server liên kết với preview tương ứng, lưu `modelSource`, `modelVersion`, `planSnapshot` vào bảng `Teams`. Luồng: (1) client gọi Preview, nhận planToken và teams đề xuất; (2) nhân viên xem xét, chỉnh sửa nếu cần; (3) gọi Confirm với cùng `planToken`, server kiểm tra token còn hiệu lực, registration không đổi, tính lại `AvgSkillScore` và lưu snapshot. Nếu đội đã được chốt, Preview/truy vấn lại trả snapshot hiện có, không ghi đè hay tạo plan mới. Giao diện preview/confirm nhận cùng contract dù backend dùng model ML của Phúc hay quy tắc dự phòng deterministic: baseline `rank-snake-v1` theo rank OP.GG ở mục 6.1, không dùng SkillScore tổng hợp hoặc tự đổi thứ tự snake để ưu tiên vai trò. Dù dùng cách nào: `n` không chia hết `teamSize` → 409 `TeamSizeMismatch` kèm số dư; Preview không ghi; Confirm kiểm tra lại toàn bộ dưới khóa và server tự tính `AvgSkillScore`, không tin điểm từ client. Phúc cung cấp model/version/seed khi triển khai ML; baseline/fallback đã được Trưởng nhóm chọn ngày 10/11/2026.
6. Chỉ chia đội khi giải `RegistrationClosed`. Confirm cùng Idempotency-Key và payload replay kết quả cũ. Confirm bằng key mới khi vẫn còn đội đã chốt trả 409 `TeamsAlreadyFormed`, không cập nhật snapshot. Muốn chia lại phải DELETE thành công các đội hiện tại trước (TeamMembers được cascade-delete), sau đó Preview/Confirm mới; chỉ lần Confirm mới sau DELETE hợp lệ mới được ghi snapshot mới (mỗi bước trong transaction riêng).
7. Khách `Locked` không đăng ký mới nhưng vẫn nhận hoàn phí.

### 6.1. Baseline/fallback `rank-snake-v1`

- Nguồn xếp người là `OpggSnapshot.rank.tier` từ snapshot do module OP.GG phía server đọc và xác minh cho đúng Riot ID/game/queue; `rank.tier` là số nguyên 1..27 trong phạm vi VALORANT v1 của SCRUM-9. Không dùng `GameRanks.RankId`, `RankOrder`, `Registration.SkillScore`, winRate/hoursPlayed tự khai hoặc fixture để thay rank provider. Thiếu/sai nguồn/rank hợp lệ trả 422 `STATS_UNAVAILABLE`, không tự gán rank 0 và không ghi đội.
- Sắp toàn bộ đăng ký hợp lệ theo `rank.tier` giảm dần; hòa tier thì `RegistrationId` tăng dần. Với `n` người, `teamSize` hợp lệ, yêu cầu ít nhất hai đội và `n % teamSize == 0`; sai trả 409 `TeamSizeMismatch`. Đặt `k = n / teamSize`; người tại chỉ số `i` (từ 0) vào đội `i % k` khi `floor(i/k)` chẵn, vào đội `k - 1 - (i % k)` khi lẻ. Đây là cùng thứ tự/tie-break của `generateTeams`, không gọi model demo từ production backend.
- Baseline không tự hoán đổi người để tối ưu `preferredRole`. Sau khi chia, validator kiểm tra roster/vai trò theo policy của luồng tích hợp; vi phạm vai trò trả 422 `ROLE_CONSTRAINT_UNSATISFIABLE`, không đổi thuật toán rồi vẫn gắn nhãn `rank-snake-v1`. Phạm vi ML v1 và các gate dữ liệu của SCRUM-9 giữ nguyên.
- Khi `modelSource=Balanced`, `skillScore` của thành viên trong `TeamPlanDto`/`TeamMemberDto` là tier OP.GG đã chụp; `avgSkillScore`/`Teams.AvgSkillScore` là trung bình tier mỗi đội, `scoreSpread` là chênh lệch trung bình tier lớn nhất và nhỏ nhất. Chúng có thang điểm rank, không phải SkillScore tổng hợp của `RegistrationDto` và không phải xác suất thắng. `modelVersion=null`, `baselineVersion=rank-snake-v1`; UI ghi rõ phương pháp. Không trả kết quả dự phòng dưới nhãn ML.
- Confirm lưu trong `PlanSnapshot` phương pháp/baselineVersion, roster có thứ tự chuẩn, tier và ID/hash snapshot nguồn của từng người, cùng phân đội cuối. Snapshot giúp server tái hiện và trả các điểm theo đúng phương pháp, thay vì đọc lại SkillScore tổng hợp. `baselineVersion` nằm trong JSON này, không cần cột SQL mới riêng. Roster/snapshot đổi trước Confirm phải bị từ chối theo version/plan token; Preview/retry giữ nguyên đội đã chốt theo quy tắc 6 của mục 6.

## 7. Phí giải và ví

| Mã | Hợp đồng đã chọn |
| --- | --- |
| D-T01 | `EntryFee` tính **mỗi đăng ký** (mỗi người chơi); 0 = miễn phí, không ledger |
| D-T02 | Chỉ thu bằng Wallet trong V1; không thu tiền mặt/ZaloPay phí giải |
| D-T03 | Ledger `TournamentFee` (Amount âm) khi thu và `TournamentRefund` (Amount dương) khi hoàn; khóa duy nhất lần lượt `TournamentFee:{registrationId}` và `TournamentRefund:{registrationId}` |
| D-T04 | Trước khi xét số dư, gọi `SessionSettlement.BeforeWalletDebit` (004 mục 7.3); thiếu tiền → rollback cả request, 409 `InsufficientWalletBalance` |
| D-T05 | Hoàn 100% nếu huỷ trước `fullRefundBeforeUtc`; sau mốc đó hoàn `refundPercentAfter`% (mặc định 0); nhân viên huỷ hoặc huỷ giải → 100%; làm tròn xuống đồng. Số hoàn tính theo `feeCharged` snapshot, không theo `EntryFee` hiện tại |
| D-T06 | Sau đăng ký đầu tiên không đổi `EntryFee`/chính sách hoàn; 0 VND không ghi ledger |

Tổng `feeRefunded ≤ feeCharged`. Với quy ước ledger `TournamentFee < 0`, `TournamentRefund > 0`, doanh thu giải = `-SUM(TournamentFee.Amount) - SUM(TournamentRefund.Amount)`; nạp ví không phải doanh thu giải. Ví dụ thu 100.000 rồi hoàn đủ: `-(-100.000) - 100.000 = 0`.

## 8. Chống lặp, transaction và khóa

- Thứ tự khóa (mở rộng 004 mục 8.1): Customer (ID tăng dần) → Computer → GamingSession → SessionCharge → **Tournament → Registration → Team** → Order → ... → audit.
- Đăng ký: một transaction gồm quyết toán phí phiên (nếu khách đang chơi), kiểm tra chỗ/unique (kể cả `Cancelled`), tạo Registration, ledger `TournamentFee`, tăng đếm, một commit cuối. Flush lấy `RegistrationId` trước khi ghi ledger (D08 của 004).
- Huỷ: một transaction gồm đổi trạng thái, ledger `TournamentRefund`, xoá `TeamMembers` nếu có, audit. Replay cùng key trả kết quả cũ; unique ledger key ngăn hoàn hai lần.
- Huỷ giải có nhiều đăng ký: mỗi đăng ký một ledger; chia batch được, idempotent theo khóa ledger. Mỗi ledger row giữ khóa duy nhất `IdempotencyKey` (migration, unique filtered index khi khác null) để bảo vệ cả khi request idempotency record hết hạn/không còn truy vấn được.
- Event `TournamentChanged`/`RegistrationChanged` sau commit; client GET lại.

## 9. Lỗi HTTP

ProblemDetails như `ApiExceptionHandler`, thêm `code` (đề xuất).

| HTTP / code | Điều kiện |
| --- | --- |
| 400 `ValidationError` | DTO/header sai, trường lạ, enum/độ dài/tiền vượt giới hạn, `riotId` sai định dạng, `rankId` không thuộc game, `confirmNewPassword` lệch |
| 400 `InvalidCurrentPassword` | Sai mật khẩu hiện tại (không dùng 401 để client không tự logout); không khóa tài khoản vì sai nhiều lần, khớp 001 |
| 401 / 403 | Chưa đăng nhập; thiếu permission, nhân viên không hoạt động, thao tác lên chính mình bị cấm |
| 404 `EmployeeNotFound / TournamentNotFound / RegistrationNotFound / TeamNotFound` | Không tồn tại hoặc không thuộc quyền xem |
| 409 `LastAdminProtected` | Vô hiệu/giáng quyền Admin cuối |
| 409 `InvalidTournamentTransition / TournamentLocked / RegistrationClosed` | Chuyển sai, sửa trường khoá, ngoài cửa sổ đăng ký |
| 409 `TournamentFull / AlreadyRegistered` | Hết chỗ; đã đăng ký giải, kể cả registration đã `Cancelled` |
| 409 `TeamSizeMismatch / TeamsAlreadyFormed / TeamsNotFormed / TeamPlanInvalid` | Số người không chia hết; đã chia đội; chưa chia đội; plan có đăng ký trùng/không hợp lệ |
| 409 `TeamNameConflict / TournamentLocked` | Tên đội trùng trong giải (trim, không phân biệt hoa/thường); giải đã bắt đầu/kết thúc/huỷ nên không được đổi tên |
| 409 `CustomerLocked / InsufficientWalletBalance` | Khách khoá; ví không đủ sau quyết toán phí phiên |
| 409 `RefundNotAllowed` | Đăng ký đã Cancelled hoặc đã hoàn đủ |
| 409 `VersionConflict / IdempotencyConflict / ConcurrencyConflict` | Version cũ; key khác payload; unique/deadlock SQL (rollback toàn bộ) |
| 422 `STATS_UNAVAILABLE / ROLE_CONSTRAINT_UNSATISFIABLE` | Baseline thiếu/sai snapshot rank OP.GG hợp lệ; phân đội không thỏa validator vai trò, không ghi đội hoặc thay âm thầm thuật toán |
| 500 | Lỗi nội bộ, không lộ SQL/secret |

## 10. Mapping UI ↔ DBML

Chi tiết ở Phụ lục B (cột hiện có và cột đề xuất được tách rõ). Tóm tắt:

| Màn hình / chức năng | Bảng hiện có | Cần bổ sung (đề xuất, cần migration) |
| --- | --- | --- |
| Nhân viên | `Employees(RoleId, FullName, Phone, IsActive, CreatedAt)` + `Username`, `PasswordHash` (đã có theo 001), `Roles` | Rowversion; `MustChangePassword` (cho reset) |
| Đổi mật khẩu | `AuditLogs` (`Action` ≤255 ký tự) | Không cần cột mới: dùng `Employees.PasswordHash`, `Customers.PasswordHash` đã có |
| Giải / phí / chính sách | `Tournaments(TournamentId, GameId, CreatedBy, TournamentName, RegistrationStart, RegistrationEnd, StartTime, EntryFee decimal(12,2), Status)` | Thêm `MaxParticipants: int? NULL`, `FullRefundBeforeUtc: datetime2 NOT NULL` (backfill từ `RegistrationEnd`), `RefundPercentAfter: int NOT NULL` (mặc định 0, range 0..100), `rowversion`; đổi `EntryFee` thành `decimal(18,0)` sau khi xử lý dữ liệu lẻ. Không có default SQL hiện tại cho các cột hiện hữu |
| Đăng ký | `TournamentRegistrations(RegistrationId, TournamentId, CustomerId, RankId, PreferredRole, WinRate decimal(5,2) NOT NULL, HoursPlayed int NOT NULL, SkillScore decimal(6,2) NULL, Status, RegisteredAt)`, unique `(TournamentId, CustomerId)` | Thêm `RiotId nvarchar(128) NOT NULL` (backfill trước khi áp NOT NULL nếu có dữ liệu); `FeeCharged decimal(18,0) NULL`, `FeeRefunded decimal(18,0) NULL`, `ChargeTransactionId nvarchar(128) NULL`, `RefundTransactionId nvarchar(128) NULL`, `CancelledAtUtc datetime2 NULL`, `CancelReason nvarchar(250) NULL`, `rowversion`. Request cho phép thiếu WinRate/HoursPlayed; normalize null thành 0 theo mục 6.4 để giữ schema/entity NOT NULL, chỉ migrate nullable nếu cần lưu sự khác biệt giữa thiếu và 0 |
| Đội & chia đội | `Teams(TeamId, TournamentId, TeamName, AvgSkillScore decimal(6,2) NULL)`, `TeamMembers(TeamId, RegistrationId, AssignedRole)` với unique index `RegistrationId` | Thêm `ModelSource nvarchar(50)`, `ModelVersion nvarchar(100) NULL`, `PlanToken nvarchar(36) NULL`, `PlanSnapshot nvarchar(max) NULL`, `rowversion` trên Teams; thêm unique `(TournamentId, TeamName)` nếu cần bảo đảm chống trùng ở DB. Không có `TeamId` trên `TournamentRegistrations`; lấy quan hệ qua `TeamMembers` |
| Ví | `WalletTransactions(TransactionId, CustomerId, Amount decimal(18,0), BalanceAfter decimal(18,0), Type, Description, CreatedAt)` | Thêm giá trị nghiệp vụ `TournamentFee`, `TournamentRefund` cho `Type` (không cần đổi schema nvarchar); thêm `IdempotencyKey nvarchar(128) NULL` + unique filtered index cho giá trị khác null. Không đổi precision `Amount`/`BalanceAfter` (đã `decimal(18,0)`) |
| Idempotency API | Quy ước chung ở 004 | Dùng kho idempotency API dùng chung theo 004; ledger vẫn có unique `IdempotencyKey` riêng làm bảo vệ cuối cho thu/hoàn |

`Teams` không có `CaptainCustomerId` và `TeamMembers` nối qua `RegistrationId` (không qua `CustomerId`): thiết kế này khớp mô hình đội do hệ thống chia. DB constraint chỉ bảo vệ unique/FK; số người đủ `teamSize`, cùng giải, hạn mức hoàn là kiểm tra nghiệp vụ dưới khóa. Không sửa migration đã áp.

## 11. Dữ liệu đăng ký đầu vào cho Phúc

Phúc cần danh sách đăng ký làm đầu vào cho bước tính điểm/chia đội. Bộ dữ liệu mẫu: `tournament-registrations-seed.csv` (11 bản ghi, trong đó 10 `Registered` và 1 `Cancelled`, game 5 người/đội, ID/Riot ID minh hoạ, **không phải đăng ký thật**). Riot ID là dữ liệu bắt buộc cho đăng ký Valorant và phải được bảo toàn qua API, lưu trữ và export để dùng với adapter OP.GG.

Cột (cùng thứ tự CSV, UTF-8 không BOM): `registrationId, tournamentId, customerId, customerName, riotId, rankId, rankName, rankOrder, preferredRole, winRate, hoursPlayed, skillScore, status, registeredAtUtc`.

- Export thật qua `GET .../registrations/export?format=csv|json&status=Registered`; mặc định chỉ `Registered`, gồm `riotId`, không gồm thông tin liên hệ/tài chính khách hàng.
- Chuỗi bắt đầu bằng `=`,`+`,`-`,`@` được thêm tiền tố `'` (chống CSV injection); không xuất số điện thoại, email, số dư ví.
- CSV seed giữ SkillScore tổng hợp để tham khảo, không có snapshot rank tier/provenance OP.GG nên chưa đủ chạy baseline `rank-snake-v1`; S2-P-2 cung cấp snapshot server đã xác minh trước bước đề xuất đội.
- CSV và JSON là hai định dạng bàn giao; CSV seed chỉ là fixture, chưa có dữ liệu đăng ký thật. Phúc xác nhận trường đầu vào cần thêm cho model và cách dùng OP.GG; không gửi dữ liệu khách thật qua seed.

## 12. Tiêu chí kiểm chứng khi triển khai

Các ca dưới là yêu cầu cho S2/S3, **chưa được chạy**.

| Ca | Kết quả cần kiểm tra |
| --- | --- |
| Riot ID hợp lệ/không hợp lệ | Được trim và chuẩn hóa; sai dấu `#`, thiếu phần, quá dài/ký tự điều khiển → 400; cùng giá trị được trả về response, DB và export |
| Tạo/sửa/vô hiệu nhân viên; Admin cuối; tự vô hiệu mình | Đúng quyền; 409 `LastAdminProtected`; token nhân viên bị vô hiệu bị từ chối ngay |
| Đổi mật khẩu đúng/sai/yếu; replay; thử sai nhiều lần | 204 chỉ khi mật khẩu cũ đúng; không lộ mật khẩu ở log/audit; sai nhiều lần không khóa tài khoản; token cũ tự hết hạn (D-T11) |
| Reset mật khẩu bởi Admin | Mật khẩu tạm hiển thị một lần; replay cùng key không sinh mật khẩu mới |
| Chuyển trạng thái giải theo mục 5 | Chỉ chuyển hợp lệ; trường bị khoá sau đăng ký đầu |
| Đăng ký hợp lệ / `rankId` sai game / thiếu tiền | Một đăng ký + một ledger; sai game 400; thiếu tiền rollback không đăng ký |
| Ví đủ phí nhưng thiếu sau phí phiên | Rollback toàn bộ (D-T04) |
| Cashier đăng ký thay khách; Waiter thử đăng ký | Cashier thành công, trừ ví khách, audit có EmployeeId; Waiter 403 |
| Hai khách tranh chỗ cuối; đăng ký đua đóng giải | Một bên thành công; bên thua 409, không ledger dở dang |
| Đăng ký rồi huỷ/refund rồi thử đăng ký lại bằng key mới | Lần đầu tạo một debit; huỷ tạo nhiều nhất một refund; lần đăng ký mới 409 `AlreadyRegistered`, không có registration/debit thứ hai |
| Huỷ/refund replay cùng hoặc khác request key | Chỉ một ledger `TournamentRefund:{registrationId}`; không ghi tiền lần hai |
| Thu 100.000 rồi hoàn đủ 100.000 | Ledger lần lượt -100.000/+100.000; doanh thu theo công thức mục 7 bằng 0 |
| Đăng ký đang tồn tại / cùng key khác payload | 409 `AlreadyRegistered` / `IdempotencyConflict` |
| Client gửi `skillScore`, `feeCharged` | 400, server tính điểm |
| Preview rồi Confirm; plan có đăng ký trùng; số người không chia hết | Preview không ghi, trả `planToken` + `modelSource`/`modelVersion`; Confirm kiểm tra `planToken` hợp lệ, registration không đổi, tính lại `AvgSkillScore`, lưu `modelSource`/`modelVersion`/`planSnapshot`, `TeamSizeMismatch` có số dư |
| Rank tier và SkillScore tổng hợp cho thứ tự ngược nhau | `Balanced` chia đúng theo tier OP.GG; thay SkillScore/winRate/hoursPlayed tự khai không đổi phân đội nếu snapshot rank giữ nguyên |
| Hòa rank, hoán vị thứ tự input | Sắp theo RegistrationId tăng dần khi hòa; cùng roster/snapshot cho cùng phân đội snake và `baselineVersion=rank-snake-v1` |
| Thiếu/sai nguồn snapshot rank OP.GG | 422 `STATS_UNAVAILABLE`; không lấy RankId/RankOrder/SkillScore làm giá trị thay thế, không ghi Teams/TeamMembers |
| Baseline vi phạm validator vai trò | 422 `ROLE_CONSTRAINT_UNSATISFIABLE`; không tự hoán đổi người rồi trả nhãn `rank-snake-v1` |
| Điểm và snapshot của baseline | Điểm thành viên = tier, AvgSkillScore = trung bình tier, scoreSpread cùng thang rank; modelVersion null, PlanSnapshot giữ baselineVersion và snapshot nguồn |
| Confirm mà `planToken` sai/hết hạn | 409 `InvalidPlanToken`; phải Preview lại |
| Confirm/Preview/retry sau khi đã chốt đội | Cùng key + payload trả kết quả cũ; key mới khi vẫn còn đội trả 409 `TeamsAlreadyFormed`; Preview/retry giữ snapshot đã chốt, không cấp token mới hay ghi đè; chỉ Confirm mới sau DELETE thành công mới ghi snapshot mới |
| Huỷ trước/sau mốc hoàn; huỷ lặp; huỷ khi đã chia đội | Hoàn đúng % một lần; khách bị chặn sau khi chia đội; nhân viên huỷ xoá `TeamMembers`, giải chuyển `RegistrationClosed`, phải chia lại |
| Huỷ giải nhiều đăng ký; restart giữa chừng | Mỗi đăng ký hoàn đúng một lần, tổng hoàn = tổng phí |
| Khách Locked đăng ký / nhận hoàn | Đăng ký 409; hoàn vẫn thực hiện |
| Khách khác | 404, không lộ dữ liệu |
| Export đăng ký | Đúng cột/mã hoá, chỉ người có `tournament.manage` |
| Đổi tên đội | `UpdateTeamRequest` yêu cầu `expectedVersion`; cho phép trước `InProgress`; trùng tên sau trim/case-fold → 409 `TeamNameConflict`; version cũ → 409 `VersionConflict` |

## 13. Mapping sang cấu trúc `CyberManagement.Application`

Căn cứ ảnh cây thư mục: `DTOs`, `Exceptions`, `Interfaces/{Repositories,Services,Validation}`, `Security`, `Services`, `Validators`, `DependencyInjection.cs`. Tên file là đề xuất; chưa rõ quy ước con và khác biệt giữa `Interfaces/Validation` với `Validators`.

| Thành phần | Thư mục | File đề xuất |
| --- | --- | --- |
| DTO | `DTOs` | Các DTO/Request mục 4 |
| Validator | `Validators` | Một validator mỗi request (độ dài, enum, tiền, thời gian giải, `confirmNewPassword`) |
| Chính sách mật khẩu | `Security` | `PasswordPolicy`, dùng chung `PasswordHasher` đã có (001) |
| Lỗi nghiệp vụ | `Exceptions` | Exception theo mục 9 mang `Code`; `ApiExceptionHandler` map sang ProblemDetails |
| Interface repository | `Interfaces/Repositories` | `IEmployeeRepository`, `ITournamentRepository`, `ITournamentRegistrationRepository`, `ITeamRepository`, có đọc khóa theo mục 8 |
| Interface service | `Interfaces/Services` | `IEmployeeService`, `IPasswordService`, `ITournamentService`, `IRegistrationService`, `ITeamBalancingService`, `ITeamService` |
| Triển khai | `Services` | `RegistrationService` gọi `WalletLedger.Record` và `SessionSettlement.BeforeWalletDebit` trong transaction của caller, không tự commit; `TeamBalancingService` gọi ML hoặc quy tắc dự phòng ở mục 6 (Phúc xác nhận model/version/seed trước triển khai) |
| DI | `DependencyInjection.cs` | Đăng ký service/validator; Infrastructure đăng ký repository |

Application chỉ tham chiếu Domain; EF/SQL và migration ở Infrastructure; controller, claims, route ở API.

## 14. Việc còn mở / bàn giao

| Việc | Người |
| --- | --- |
| Xác nhận D-T02..D-T04, `IdempotencyKey`/ledger uniqueness, thứ tự quyết toán phí phiên và snapshot/modelSource lưu trữ | Minh (chưa có xác nhận trong bằng chứng hiện tại) |
| Phối hợp dữ liệu OP.GG/model/version/seed, baselineVersion/snapshot và export `riotId`; baseline/fallback đã chốt `rank-snake-v1` theo quyết định 10/11/2026 | Phúc phối hợp triển khai; không chờ xác nhận riêng để chốt fallback |
| Đối chiếu nội dung `S1-D-1`, mockup UI, `RolePermissionCatalog` và `UpdateTournamentRequest` DTO |  Đại |
| Xác nhận các lựa chọn D-T01, D-T05..D-T07 và duyệt cuối contract | Trưởng nhóm |

## Phụ lục A. Biên bản chốt quyết định (phí giải và dữ liệu đăng ký)

Trạng thái: phương án thiết kế được chọn trong contract v0.4 để unblock implementation. Cột xác nhận phân biệt rõ lựa chọn trong tài liệu với sự đồng ý thực tế của owner; không có nội dung nào dưới đây được coi là xác nhận của Minh/Phúc nếu chưa nhận được phản hồi trực tiếp.

| Mã | Câu hỏi | Phương án được chọn | Căn cứ | Owner cần xác nhận | Tình trạng xác nhận |
| --- | --- | --- | --- | --- | --- |
| D-T01 | Phí tính theo gì? | Mỗi người chơi, `EntryFee` cho mỗi registration; 0 = miễn phí | Cột phí thuộc giải; registration thuộc một khách | Trưởng nhóm | Ghi theo lựa chọn contract v0.4; chờ duyệt cuối |
| D-T02 | Thanh toán bằng gì? | Chỉ Wallet trong V1; không tiền mặt/ZaloPay phí giải | Thu/hoàn qua wallet ledger chung | Minh | Chờ Minh xác nhận |
| D-T03 | Ledger và khóa chống lặp | `TournamentFee` âm / `TournamentRefund` dương; `IdempotencyKey` duy nhất `TournamentFee:{registrationId}` / `TournamentRefund:{registrationId}` | Một registration tối đa một lần thu và một lần hoàn; registration huỷ không được tái sử dụng | Minh | Chờ Minh xác nhận mapping vào ledger |
| D-T04 | Trừ phí trước/sau phí phiên? | Quyết toán phí phiên rồi mới debit phí giải; thiếu số dư thì rollback cả transaction | Theo `SessionSettlement.BeforeWalletDebit` trong 004 mục 7.3 | Minh + Phúc | Chờ hai owner xác nhận phối hợp |
| D-T05 | Chính sách hoàn | Trước `FullRefundBeforeUtc`: 100%; sau đó `RefundPercentAfter` (mặc định 0%); nhân viên huỷ/huỷ giải: 100%; làm tròn xuống đồng | Tính trên `feeCharged` snapshot, không theo phí hiện tại | Trưởng nhóm | Được ghi nhận theo đề xuất hiện tại; chờ duyệt cuối |
| D-T06 | Khóa sửa phí | Sau registration đầu tiên không đổi EntryFee/chính sách hoàn | Tránh thay đổi số tiền đã cam kết | Đại | Đã chọn trong contract v0.4; owner triển khai áp dụng |
| D-T07 | Khách huỷ rồi đăng ký lại | Cấm; cancelled row giữ nguyên | Giữ unique `(TournamentId, CustomerId)` và không tái sử dụng ledger key | Đại | Đã chọn trong contract v0.4; không có xác nhận độc lập khác |
| D-T08 | Công thức `SkillScore` tổng hợp | Giữ công thức ở mục 6.4 cho dữ liệu tham khảo của đăng ký; không dùng cho `rank-snake-v1` | Rank OP.GG là đầu vào baseline ở mục 6.1; dữ liệu tự khai không thay provider | Đại + Phúc | Được tách rõ theo quyết định Trưởng nhóm 10/11/2026 |
| D-T09 | Thuật toán chia đội | ML do Phúc; baseline/fallback `rank-snake-v1`: tier OP.GG giảm dần, hòa theo RegistrationId tăng dần rồi snake; validator vai trò không tự sửa thứ tự baseline | SCRUM-9 mục 5 và quyết định Trưởng nhóm 10/11/2026 sau PR #18 | Đại + Phúc triển khai | Phương án đã được Trưởng nhóm chọn; PR bổ sung chờ duyệt cuối |
| D-T10 | Định dạng dữ liệu đăng ký | CSV + JSON; có `riotId`; không xuất PII/tài chính; sample không phải dữ liệu thật | Phù hợp handoff và OP.GG lookup | Phúc | Chọn trong contract v0.4; chờ Phúc xác nhận trường/model input |
| D-T11 | Thu hồi token sau đổi/reset mật khẩu | Không thu hồi tức thời; token tự hết hạn theo TTL cấu hình (mặc định 20 phút, cấu hình 1..30 phút) | 001 mục 5, không có server-side revocation | Owner login | Giữ quyết định đã mô tả ở 001; không thuộc điểm review này |
| D-T12 | Cashier đăng ký thay khách và trừ ví khách | Cho phép nếu có `tournament.register`; khách Active; audit EmployeeId | Dùng permission hiện hữu, không tạo permission mới | Trưởng nhóm + Minh | Theo contract v0.3/001; Minh cần xác nhận luồng ledger |
| D-T13 | Độ dài mật khẩu tối thiểu | 12 ký tự; ít nhất một chữ và một số | Nhất quán chính sách seed/contract auth | Owner login | Giữ quy ước contract hiện tại; không thuộc điểm review này |
| D-T14 | Lưu trữ metadata đề xuất | `planToken` là UUID; `modelSource=Balanced` ánh xạ `rank-snake-v1`, modelVersion null; PlanSnapshot lưu baselineVersion, roster/tier/snapshot nguồn và teams cuối; ML giữ modelVersion riêng | Truy vết nguồn và tái hiện đúng phương pháp theo mục 6.1 | Đại + Phúc | Đặc tả bổ sung v0.6; chưa phải code/migration đã triển khai |

## Phụ lục B. Mapping cột SQL Server

Đối chiếu theo schema/migration hiện tại trên toàn bộ chuỗi migration (`InitialCreate` và các migration sau, gồm `IntegrateInventoryInvoiceVoucherContracts`), `ApplicationDbContextModelSnapshot` và cấu hình EF; `CyberManagement.sql` chỉ là script InitialCreate nên không đủ làm căn cứ. Cột để `—` là chưa tồn tại, không phải cột hiện có; mọi thay đổi schema phải dùng migration mới, không sửa migration đã áp dụng.

### Bảng Tournaments

| DTO/FieldName | SQL Column | Kiểu | Mặc định | Migration |
| --- | --- | --- | --- | --- |
| `id` | `TournamentId` | int PK | Auto | Hiện có |
| `gameId` | `GameId` | int FK | | Hiện có |
| `name` | `TournamentName` | nvarchar(150) | | Hiện có |
| `registrationStartUtc` | `RegistrationStart` | datetime2 | | Hiện có |
| `registrationEndUtc` | `RegistrationEnd` | datetime2 | | Hiện có |
| `startTimeUtc` | `StartTime` | datetime2 | | Hiện có |
| `entryFee` | `EntryFee` | decimal(12,2) NOT NULL | Không có SQL default | Hiện có; migrate → `decimal(18,0)` theo policy VND nguyên, kiểm tra/xử lý dữ liệu lẻ trước |
| `maxParticipants` | — | — | — | Cần thêm int NULL |
| `fullRefundBeforeUtc` | — | — | — | Cần thêm datetime2 NOT NULL; backfill từ `RegistrationEnd`, đặt default cho hàng mới |
| `refundPercentAfter` | — | — | — | Cần thêm int NOT NULL; backfill/default 0, range 0..100 |
| `status` | `Status` | nvarchar(50) NOT NULL | Không có SQL default | Hiện có |
| `createdByEmployeeId` | `CreatedBy` | int FK | | Hiện có |
| `registeredCount` | (tính từ `COUNT(*) TournamentRegistrations WHERE Status='Registered'`) | readonly | | Logic ứng dụng |
| `version` | `rowversion` | rowversion | | Cần thêm |

### Bảng TournamentRegistrations

| DTO/FieldName | SQL Column | Kiểu | Mặc định | Migration |
| --- | --- | --- | --- | --- |
| `id` | `RegistrationId` | int PK | Auto | Hiện có |
| `tournamentId` | `TournamentId` | int FK | | Hiện có |
| `customerId` | `CustomerId` | int FK | | Hiện có |
| `riotId` | `RiotId` | nvarchar(128) NOT NULL | | Cần thêm (backfill trước) |
| `rankId` | `RankId` | int FK | | Hiện có |
| `preferredRole` | `PreferredRole` | nvarchar(50)? | NULL | Hiện có |
| `winRate` | `WinRate` | decimal(5,2) | 0 | Hiện có (NOT NULL, không nullable; null = 0 server-side) |
| `hoursPlayed` | `HoursPlayed` | int | 0 | Hiện có (NOT NULL, không nullable; null = 0 server-side) |
| `skillScore` | `SkillScore` | decimal(6,2) NULL | NULL | Hiện có |
| `status` | `Status` | nvarchar(50) NOT NULL | Không có SQL default | Hiện có |
| `feeCharged` | `FeeCharged` | decimal(18,0)? | NULL | Cần thêm |
| `feeRefunded` | `FeeRefunded` | decimal(18,0)? | NULL | Cần thêm |
| `chargeTransactionId` | `ChargeTransactionId` | nvarchar(128)? | NULL | Cần thêm (IdempotencyKey từ ledger) |
| `refundTransactionId` | `RefundTransactionId` | nvarchar(128)? | NULL | Cần thêm (IdempotencyKey từ ledger) |
| `registeredAtUtc` | `RegisteredAt` | datetime2 NOT NULL | Giá trị khởi tạo UTC ở entity; không có SQL default | Hiện có |
| `cancelledAtUtc` | `CancelledAtUtc` | datetime2? | NULL | Cần thêm |
| `cancelReason` | `CancelReason` | nvarchar(250)? | NULL | Cần thêm |
| `teamId` | — | Không có cột trực tiếp | — | Không thêm; lấy qua `TeamMembers.RegistrationId` |
| `version` | `rowversion` | rowversion | | Cần thêm |
| `Unique(TournamentId, CustomerId)` | | Constraint | | Hiện có |

### Bảng Teams

| DTO/FieldName | SQL Column | Kiểu | Mặc định | Migration |
| --- | --- | --- | --- | --- |
| `id` | `TeamId` | int PK | Auto | Hiện có |
| `tournamentId` | `TournamentId` | int FK | | Hiện có |
| `name` | `TeamName` | nvarchar(100) | | Hiện có |
| `avgSkillScore` | `AvgSkillScore` | decimal(6,2)? | NULL | Hiện có (type thực tế `(6,2)` không `(10,2)`) |
| `modelSource` | `ModelSource` | nvarchar(50) | — | Cần thêm; backfill/default `Balanced` nếu dùng NOT NULL |
| `modelVersion` | `ModelVersion` | nvarchar(100)? | NULL | Cần thêm (version từ ML nếu dùng ML) |
| `planToken` | `PlanToken` | nvarchar(36)? | NULL | Cần thêm (UUID từ preview, xóa sau confirm) |
| `planSnapshot` | `PlanSnapshot` | nvarchar(max)? | NULL | Cần thêm (JSON: phương pháp/baselineVersion, roster/tier/snapshot nguồn và teams đã chốt theo mục 6.1) |
| `version` | `rowversion` | rowversion | | Cần thêm |
| `Unique(TournamentId, TeamName)` | | Constraint | Collation `SQL_Latin1_General_CP1_CI_AS` | Cần thêm |

### Bảng WalletTransactions (thêm loại + IdempotencyKey)

| DTO/FieldName | SQL Column | Kiểu | Mặc định | Migration |
| --- | --- | --- | --- | --- |
| `type` | `Type` | nvarchar(50) NOT NULL | — | Thêm giá trị ứng dụng `TournamentFee`, `TournamentRefund`; không cần đổi kiểu cột |
| `amount` | `Amount` | decimal(18,0) NOT NULL | — | Hiện có; không cần migration precision |
| `balanceAfter` | `BalanceAfter` | decimal(18,0) NOT NULL | — | Hiện có; không cần migration precision |
| `description` | `Description` | nvarchar(255) NULL | NULL | Hiện có |
| (N/A) | `IdempotencyKey` | — | — | Cần thêm nvarchar(128) NULL + unique filtered index WHERE `IdempotencyKey IS NOT NULL` |

### Bảng TeamMembers (không đổi, reference qua RegistrationId)

| DTO/FieldName | SQL Column | Kiểu | Mặc định | Notes |
| --- | --- | --- | --- | --- |
| `registrationId` | `RegistrationId` | int FK | | Hiện có, unique constraint |
| `teamId` | `TeamId` | int FK | | Hiện có |
| `assignedRole` | `AssignedRole` | nvarchar(50)? | NULL | Hiện có |

**Ghi chú:** Schema hiện tại map `name` → `TournamentName`, `registrationStartUtc` → `RegistrationStart`, `registrationEndUtc` → `RegistrationEnd`, `startTimeUtc` → `StartTime`, `createdByEmployeeId` → `CreatedBy`, và `registeredAtUtc` → `RegisteredAt`. `WinRate`/`HoursPlayed` NOT NULL; API optional phải normalize null thành 0 theo mục 6.4 (chỉ đổi nullable nếu cần lưu phân biệt giá trị thiếu). `SkillScore` và `AvgSkillScore` đều nullable `decimal(6,2)`. `TournamentRegistrations` không có `TeamId`: join `TournamentRegistrations.RegistrationId` → `TeamMembers.RegistrationId` → `Teams.TeamId`; `TeamMembers` có PK `(TeamId, RegistrationId)`, FK `TeamId` cascade-delete tới Teams và unique index trên `RegistrationId`. Trước migration precision `EntryFee`, kiểm tra/xử lý dữ liệu lẻ; các cột mới cần backfill/default phù hợp trước khi đặt NOT NULL.
