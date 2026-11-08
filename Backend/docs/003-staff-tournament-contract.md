# 003 – Hợp đồng nhân viên, đổi mật khẩu, giải đấu, đăng ký và đội

| Thông tin | Nội dung |
| --- | --- |
| Task | Đặc tả endpoint, DTO, trạng thái, lỗi cho nhân viên, đổi mật khẩu, giải đấu, đăng ký, đội; chốt phí giải, mapping UI/DBML, dữ liệu đăng ký cho Phúc |
| Owner | (điền owner task); Minh đối chiếu ví/ledger, Phúc nhận dữ liệu đăng ký và thuật toán chia đội |
| Reviewer cuối | Trưởng nhóm |
| Phiên bản | 0.3, ngày 08/11/2026, nháp để nhóm chốt; đã đối chiếu `gaming_cafe_schema.sql` và 001-monorepo |
| Backend đích | CyberManagement .NET 10 / SQL Server, nền 35 bảng |
| Phụ thuộc | `S1-D-1` (nội dung chưa được đối chiếu), 001 (identity, đã đối chiếu), 002 (phiên/phí giờ), 004 (idempotency, khóa, lỗi) |
| File đi kèm | `tournament-registrations-seed.csv` (dữ liệu mẫu cho Phúc); biên bản quyết định và DBML nằm ở Phụ lục A, B |

**Trạng thái:** bản nháp. Điểm **[CẦN CHỐT]** là đề xuất mặc định của người soạn, chưa có xác nhận của owner hay Trưởng nhóm. Đây là hợp đồng để triển khai, chưa phải API đang chạy.

**Thay đổi v0.3 (sau khi đọc 001):** dùng đúng tên permission của ma trận quyền (không tạo permission mới); `Username`/`PasswordHash` đã có nên không còn là cột đề xuất; JWT không bị thu hồi phía server nên bỏ giả định "thu hồi token" và bỏ giới hạn thử sai mật khẩu; đổi route đổi mật khẩu về `/api/auth/...`; staff đăng ký thay khách được hỗ trợ (`tournament.register`); chia đội do `team.assign` chạy ML.

**Thay đổi so với v0.1:** schema thật cho thấy đội do nhân viên/thuật toán chia theo điểm kỹ năng chứ không phải khách tự lập đội; `EntryFee` tính theo từng đăng ký; `Employees` không có username/email/mật khẩu. Các mục tương ứng đã viết lại.

## 1. Căn cứ và giới hạn

- Schema đối chiếu: `gaming_cafe_schema.sql` (35 bảng). Bảng liên quan: `Roles`, `Permissions`, `RolePermissions`, `Employees`, `AuditLogs`, `Customers`, `WalletTransactions`, `Games`, `GameRanks`, `Tournaments`, `TournamentRegistrations`, `Teams`, `TeamMembers`.
- Quy ước chung lấy từ [001-monorepo](001-monorepo.md), [002](002-computer-session-contract.md), [004](004-order-checkout-contract.md).
- **Đã đối chiếu 001-monorepo:** `Employees` có `Username` (unique) và `PasswordHash`; `Customers` đăng nhập bằng `Phone` + `PasswordHash` (các cột này do migration EF thêm, không có trong `gaming_cafe_schema.sql`); JWT access token sống 20 phút, không có refresh/revocation phía server; sai mật khẩu không khóa tài khoản; khách `Locked` không nạp/trừ ví; mọi thay đổi nhân viên ghi `AuditLogs` với `EntityType='Employee'`.
- **Chưa đối chiếu được:** nội dung `S1-D-1`, mockup UI, mã nguồn `RolePermissionCatalog`.
- Permission dùng đúng tên trong ma trận quyền của 001 (`employee.view/manage`, `tournament.view/manage/register`, `team.assign`, `game.manage`, `wallet.refund`); không tạo permission mới. Điểm ma trận chưa đủ ghi ở mục 3.4.

## 2. Quy ước và bất biến

- JSON camelCase; enum là chuỗi phân biệt hoa/thường; ID là int dương; thời gian ISO 8601 UTC (`Z`), UI hiển thị +07:00; một mốc `now` server mỗi transaction.
- Tiền là số nguyên VND, tối đa `999_999_999`; phần lẻ bị từ chối (cùng 004). Cột SQL hiện `decimal(12,2)`, hướng đích `decimal(18,0)` theo 004/D10.
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

Theo 001, `employee.manage` chỉ Admin có; Manager chỉ xem nhân viên. Đổi/reset mật khẩu **không thu hồi** JWT đang dùng (001 mục 5): token cũ tự hết hạn sau tối đa 30 phút, muốn thu hồi ngay cần cơ chế lưu DB **[CẦN CHỐT: D-T11]**. Vô hiệu hoá nhân viên vẫn có hiệu lực ngay vì trạng thái `IsActive` kiểm tra từ DB ở mỗi thao tác (004 mục 3). Mỗi thay đổi ghi `AuditLogs` (`EntityType='Employee'` hoặc `'Customer'`, `Action=PasswordChanged/PasswordReset/...`), không ghi giá trị mật khẩu. Sai mật khẩu hiện tại không khóa tài khoản (khớp 001 mục 4).

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
| `GET /api/admin/tournaments/{id}/registrations/export` | `format=csv\|json,status?` → file | `tournament.manage` |
| `POST /api/admin/tournaments/{id}/registrations` | StaffRegisterRequest → 201 RegistrationDto | `tournament.register` (đăng ký thay khách) |
| `PATCH /api/admin/tournaments/{id}/registrations/{registrationId}` | UpdateRegistrationRequest → 200 | `tournament.manage`; chỉ khi chưa chia đội |
| `POST /api/admin/tournaments/{id}/registrations/{registrationId}/cancel` | CancelRegistrationRequest → 200 | `tournament.manage` + `wallet.refund` nếu có hoàn |

### 3.3. Đội (do nhân viên chia, không có đội trưởng do khách lập)

| Method / route | Đầu vào → kết quả | Quyền server |
| --- | --- | --- |
| `POST /api/admin/tournaments/{id}/teams/preview` | TeamPreviewRequest → 200 TeamPlanDto, **không ghi dữ liệu** | `team.assign` |
| `POST /api/admin/tournaments/{id}/teams` | ConfirmTeamsRequest → 201 TeamPageDto | `team.assign` |
| `GET /api/admin/tournaments/{id}/teams` | page → TeamPageDto | `tournament.view` |
| `PUT /api/admin/teams/{teamId}` | UpdateTeamRequest (đổi tên) → 200 | `team.assign` |
| `POST /api/admin/tournaments/{id}/teams/swap` | SwapRequest → 200 TeamPageDto | `team.assign` |
| `DELETE /api/admin/tournaments/{id}/teams` | VersionRequest → 200 (xoá toàn bộ đội để chia lại) | `team.assign`; chỉ khi chưa `InProgress` |

Khách khác/đăng ký không thuộc mình → 404.

### 3.4. Điểm ma trận quyền 001 cần nhóm xác nhận

- `tournament.register` có cả Cashier, nhưng 001 không có permission cho việc trừ ví khách khi đăng ký thay. Đề xuất: được phép trong phạm vi đăng ký giải, khách phải `Active`, phí trừ vào ví của chính khách đó và ghi `AuditLogs` với EmployeeId nhân viên **[CẦN CHỐT: D-T12]**.
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
| ChangePasswordRequest | `currentPassword`, `newPassword` 12..72 ký tự **[CẦN CHỐT: khớp mức ≥12 ký tự của mật khẩu seed]**, ≥1 chữ và ≥1 số, khác mật khẩu cũ; `confirmNewPassword` bằng `newPassword` |
| GameDto / RankDto | `gameId`, `gameName`, `teamSize`, `isActive` / `rankId`, `rankName`, `rankOrder` |
| TournamentDto | `id`, `gameId`, `gameName`, `teamSize`, `name` 1..150, `registrationStartUtc`, `registrationEndUtc`, `startTimeUtc`, `entryFee` (≥0, **mỗi đăng ký**), `maxParticipants: int?`, `registeredCount`, `fullRefundBeforeUtc`, `refundPercentAfter` 0..100, `status`, `createdByEmployeeId`, `version` |
| CreateTournamentRequest | `gameId`, `name`, `registrationStartUtc`, `registrationEndUtc`, `startTimeUtc`, `entryFee`, `maxParticipants?` (bội số `teamSize`), `fullRefundBeforeUtc?` (mặc định = `registrationEndUtc`), `refundPercentAfter` (mặc định 0) |
| UpdateTournamentRequest | Như create + `expectedVersion`. Sau đăng ký đầu tiên không đổi `gameId/entryFee/fullRefundBeforeUtc/refundPercentAfter` (409 `TournamentLocked`) |
| TransitionRequest | `toStatus`, `expectedVersion`, `reason?` (bắt buộc khi Cancelled) |
| RegisterRequest | `rankId` (thuộc game của giải), `preferredRole: string?` ≤50, `winRate: decimal?` 0..100 (2 chữ số lẻ), `hoursPlayed: int?` 0..100000. **Không nhận `skillScore`, phí, trạng thái** |
| StaffRegisterRequest | `customerId` + các trường của RegisterRequest; khách phải `Active`; phí trừ ví của khách (mục 3.4) |
| RegistrationDto | `id`, `tournamentId`, `customerId`, `customerName`, `rankId`, `rankName`, `preferredRole`, `winRate`, `hoursPlayed`, `skillScore`, `status`, `feeCharged`, `feeRefunded`, `registeredAtUtc`, `cancelledAtUtc?`, `cancelReason?`, `teamId: int?`, `version`. Danh sách client chỉ trả của chính khách |
| UpdateRegistrationRequest | `rankId?`, `preferredRole?`, `winRate?`, `hoursPlayed?`, `reason` 1..250, `expectedVersion`; server tính lại `skillScore`, ghi audit |
| CancelRegistrationRequest | `expectedVersion`, `reason` 1..250 |
| TeamPreviewRequest | `strategy: Balanced` (V1 chỉ một giá trị), `expectedVersion` của giải |
| TeamPlanDto | `teams: { teamName, avgSkillScore, members: { registrationId, customerName, skillScore, rankName, assignedRole? }[] }[]`, `unassigned: int[]`, `scoreSpread` (max − min điểm TB giữa các đội), `planToken` |
| ConfirmTeamsRequest | `expectedVersion`, `teams: { teamName 1..100 duy nhất trong giải, members: { registrationId, assignedRole? }[] }[]`. Server kiểm tra lại và tính `AvgSkillScore`, **không tin điểm client** |
| SwapRequest | `registrationIdA`, `registrationIdB` (khác đội), `expectedVersion` |
| TeamDto | `id`, `tournamentId`, `name`, `avgSkillScore`, `members: TeamMemberDto[]`, `version` |
| TeamMemberDto | `registrationId`, `customerId`, `displayName`, `skillScore`, `assignedRole?` |
| Page / VersionRequest | `items,page,pageSize,totalCount` sắp `createdAt` giảm dần rồi id giảm dần / `expectedVersion` |

## 5. Trạng thái

### Giải đấu

| Từ → Sang | Điều kiện |
| --- | --- |
| `Draft` → `RegistrationOpen` | Đủ trường bắt buộc; game `IsActive`; `now >= registrationStartUtc` hoặc staff mở sớm |
| `RegistrationOpen` → `RegistrationClosed` | `now >= registrationEndUtc` (worker) hoặc staff đóng sớm |
| `RegistrationClosed` → `RegistrationOpen` | Chỉ khi `now < registrationEndUtc` và chưa chia đội |
| `RegistrationClosed` → `InProgress` | Đã chia đội; mọi đội đủ `teamSize`; `now >= startTimeUtc` hoặc staff bắt đầu |
| `InProgress` → `Completed` | Nhân viên xác nhận kết thúc |
| `Draft/RegistrationOpen/RegistrationClosed` → `Cancelled` | Có `reason`; hoàn 100% mọi đăng ký `Registered` (mục 7) |
| Các tổ hợp khác | 409 `InvalidTournamentTransition` |

Ràng buộc: `registrationStartUtc < registrationEndUtc <= startTimeUtc`; `startTimeUtc > now` khi tạo.

### Đăng ký

`Registered` → `Cancelled`. Không có `Pending`: phí trừ cùng transaction chấp nhận đăng ký (phí 0 thì không có ledger). Khách tự huỷ chỉ khi giải `RegistrationOpen` và chưa chia đội; khi đã chia đội chỉ nhân viên huỷ được (xoá dòng `TeamMembers`, đội thiếu người, phải chia lại).

### Nhân viên

`IsActive` 1 ↔ 0. Vô hiệu hoá chặn đăng nhập và mọi request tiếp theo (trạng thái kiểm tra từ DB, không cần thu hồi token). Không còn Admin hoạt động → 409 `LastAdminProtected`.

## 6. Quy tắc nghiệp vụ

1. Mỗi khách một đăng ký trong một giải (`UNIQUE (TournamentId, CustomerId)`). Đăng ký đã `Cancelled` **không** đăng ký lại được bằng dòng mới do unique; khôi phục dòng cũ về `Registered` qua thao tác đăng ký lại **[CẦN CHỐT]**.
2. Đăng ký khi giải `RegistrationOpen`, `now` trong `[registrationStartUtc, registrationEndUtc)`, còn chỗ (`maxParticipants` nếu có), khách `Active`.
3. Hết chỗ cạnh tranh: kiểm tra dưới khóa Tournament; bên thua 409 `TournamentFull`, không ledger.
4. **SkillScore** do server tính (hoặc do mô hình ML của Phúc, **[CẦN CHỐT với Phúc]**); công thức mặc định: `round(100 × (0.5 × rankOrder/maxRankOrder + 0.3 × winRate/100 + 0.2 × min(hoursPlayed,2000)/2000), 2)`; thiếu `winRate`/`hoursPlayed` coi là 0 và đánh dấu dữ liệu tự khai chưa xác minh. Điểm tính lại khi staff sửa đầu vào, không tính lại sau khi chia đội.
5. **Chia đội** do nhân viên có `team.assign` chạy (001 ghi "chạy ML chia đội"). Thuật toán **[CẦN CHỐT với Phúc]**: (a) mô hình ML của Phúc; (b) quy tắc dự phòng deterministic: sắp `skillScore` giảm dần, hoà theo `registrationId` tăng dần, `k = n / teamSize` đội, chia kiểu rắn (snake draft), ưu tiên phụ là không trùng `preferredRole`. Dù dùng cách nào: `n` không chia hết `teamSize` → 409 `TeamSizeMismatch` kèm số dư; Preview không ghi; Confirm kiểm tra lại toàn bộ dưới khóa và server tự tính `AvgSkillScore`, không tin điểm từ client.
6. Chỉ chia đội khi giải `RegistrationClosed`. Chia lại = `DELETE teams` rồi Confirm mới (một transaction mỗi bước).
7. Khách `Locked` không đăng ký mới nhưng vẫn nhận hoàn phí.

## 7. Phí giải và ví **[CẦN CHỐT: xem Phụ lục A]**

| Mã | Đề xuất mặc định |
| --- | --- |
| D-T01 | `EntryFee` tính **mỗi đăng ký** (mỗi người chơi); 0 = miễn phí, không ledger |
| D-T02 | Ledger `TournamentFee` (âm) khi đăng ký, `TournamentRefund` (dương) khi hoàn; khóa `TournamentFee:{registrationId}`, `TournamentRefund:{registrationId}` |
| D-T03 | Trước khi xét số dư, gọi `SessionSettlement.BeforeWalletDebit` (004 mục 7.3); thiếu tiền → rollback cả request, 409 `InsufficientWalletBalance` |
| D-T04 | Hoàn 100% nếu huỷ trước `fullRefundBeforeUtc`; sau mốc đó hoàn `refundPercentAfter`% (mặc định 0); nhân viên huỷ hoặc huỷ giải → 100%; làm tròn xuống đồng |
| D-T05 | Số hoàn tính theo `feeCharged` đã snapshot trên đăng ký, không theo `EntryFee` hiện tại; khoá sửa phí khi đã có đăng ký |
| D-T06 | Chỉ thanh toán `Wallet`; thu tiền mặt/ZaloPay cho phí giải để sau V1 |

Tổng `feeRefunded ≤ feeCharged`. Doanh thu giải = Σ`TournamentFee` − Σ`TournamentRefund`; nạp ví không phải doanh thu giải.

## 8. Chống lặp, transaction và khóa

- Thứ tự khóa (mở rộng 004 mục 8.1): Customer (ID tăng dần) → Computer → GamingSession → SessionCharge → **Tournament → Registration → Team** → Order → ... → audit.
- Đăng ký: một transaction gồm quyết toán phí phiên (nếu khách đang chơi), kiểm tra chỗ/unique, tạo Registration, ledger `TournamentFee`, tăng đếm, một commit cuối. Flush lấy `RegistrationId` trước khi ghi ledger (D08 của 004).
- Huỷ: một transaction gồm đổi trạng thái, ledger `TournamentRefund`, xoá `TeamMembers` nếu có, audit. Replay cùng key trả kết quả cũ, không hoàn lần hai.
- Huỷ giải có nhiều đăng ký: mỗi đăng ký một ledger; chia batch được, idempotent theo khóa ledger.
- Event `TournamentChanged`/`RegistrationChanged` sau commit; client GET lại.

## 9. Lỗi HTTP

ProblemDetails như `ApiExceptionHandler`, thêm `code` (đề xuất).

| HTTP / code | Điều kiện |
| --- | --- |
| 400 `ValidationError` | DTO/header sai, trường lạ, enum/độ dài/tiền vượt giới hạn, `rankId` không thuộc game, `confirmNewPassword` lệch |
| 400 `InvalidCurrentPassword` | Sai mật khẩu hiện tại (không dùng 401 để client không tự logout); không khóa tài khoản vì sai nhiều lần, khớp 001 |
| 401 / 403 | Chưa đăng nhập; thiếu permission, nhân viên không hoạt động, thao tác lên chính mình bị cấm |
| 404 `EmployeeNotFound / TournamentNotFound / RegistrationNotFound / TeamNotFound` | Không tồn tại hoặc không thuộc quyền xem |
| 409 `LastAdminProtected` | Vô hiệu/giáng quyền Admin cuối |
| 409 `InvalidTournamentTransition / TournamentLocked / RegistrationClosed` | Chuyển sai, sửa trường khoá, ngoài cửa sổ đăng ký |
| 409 `TournamentFull / AlreadyRegistered` | Hết chỗ; đã đăng ký giải |
| 409 `TeamSizeMismatch / TeamsAlreadyFormed / TeamsNotFormed / TeamPlanInvalid` | Số người không chia hết; đã chia đội; chưa chia đội; plan có đăng ký trùng/không hợp lệ |
| 409 `CustomerLocked / InsufficientWalletBalance` | Khách khoá; ví không đủ sau quyết toán phí phiên |
| 409 `RefundNotAllowed` | Đăng ký đã Cancelled hoặc đã hoàn đủ |
| 409 `VersionConflict / IdempotencyConflict / ConcurrencyConflict` | Version cũ; key khác payload; unique/deadlock SQL (rollback toàn bộ) |
| 500 | Lỗi nội bộ, không lộ SQL/secret |

## 10. Mapping UI ↔ DBML

Chi tiết ở Phụ lục B (cột hiện có và cột đề xuất được tách rõ). Tóm tắt:

| Màn hình / chức năng | Bảng hiện có | Cần bổ sung (đề xuất, cần migration) |
| --- | --- | --- |
| Nhân viên | `Employees(RoleId, FullName, Phone, IsActive, CreatedAt)` + `Username`, `PasswordHash` (đã có theo 001), `Roles` | Rowversion; `MustChangePassword` (cho reset); `TokenVersion` chỉ nếu chọn D-T11(b) |
| Đổi mật khẩu | `AuditLogs` (`Action` ≤255 ký tự) | Không cần cột mới: dùng `Employees.PasswordHash`, `Customers.PasswordHash` đã có |
| Giải đấu | `Tournaments(GameId, CreatedBy, TournamentName, RegistrationStart/End, StartTime, EntryFee, Status)`, `Games(TeamSize)` | `MaxParticipants`, `FullRefundBeforeUtc`, `RefundPercentAfter`, `CancelReason`, rowversion |
| Đăng ký | `TournamentRegistrations(RankId, PreferredRole, WinRate, HoursPlayed, SkillScore, Status, RegisteredAt)`, unique `(TournamentId, CustomerId)` | `FeeCharged`, `FeeRefunded`, `ChargeTransactionId`, `RefundTransactionId`, `CancelledAt`, `CancelReason`, rowversion |
| Đội | `Teams(TournamentId, TeamName, AvgSkillScore)`, `TeamMembers(TeamId, RegistrationId, AssignedRole)` unique `RegistrationId` | Unique `(TournamentId, TeamName)`; rowversion |
| Ví | `WalletTransactions(Amount, BalanceAfter, Type, Description)` | Loại `TournamentFee/TournamentRefund`; khóa chống lặp theo migration S1-M-1 |
| Idempotency | Chưa có bảng | Bảng dùng chung (`ApiOperations`) hoặc mở rộng `OrderOperations` của 004 **[CẦN CHỐT]** |

`Teams` không có `CaptainCustomerId` và `TeamMembers` nối qua `RegistrationId` (không qua `CustomerId`): thiết kế này khớp mô hình đội do hệ thống chia. DB constraint chỉ bảo vệ unique/FK; số người đủ `teamSize`, cùng giải, hạn mức hoàn là kiểm tra nghiệp vụ dưới khóa. Không sửa migration đã áp.

## 11. Dữ liệu đăng ký đầu vào cho Phúc

Phúc cần danh sách đăng ký làm đầu vào cho bước tính điểm/chia đội. Bộ dữ liệu mẫu: `tournament-registrations-seed.csv` (11 dòng, trong đó 10 `Registered` và 1 `Cancelled`, game 5 người/đội, ID minh hoạ, **không phải đăng ký thật**).

Cột (cùng thứ tự CSV, UTF-8 có BOM): `registrationId, tournamentId, customerId, customerName, rankId, rankName, rankOrder, preferredRole, winRate, hoursPlayed, skillScore, status, registeredAtUtc`.

- Export thật qua `GET .../registrations/export?format=csv|json&status=Registered`; mặc định chỉ `Registered`.
- Chuỗi bắt đầu bằng `=`,`+`,`-`,`@` được thêm tiền tố `'` (chống CSV injection); không xuất số điện thoại, email, số dư ví.
- **[CẦN CHỐT với Phúc]** có cần thêm trường nào, cần JSON/CSV hay seed SQL, và đã có danh sách đăng ký thật chưa.

## 12. Tiêu chí kiểm chứng khi triển khai

Các ca dưới là yêu cầu cho S2/S3, **chưa được chạy**.

| Ca | Kết quả cần kiểm tra |
| --- | --- |
| Tạo/sửa/vô hiệu nhân viên; Admin cuối; tự vô hiệu mình | Đúng quyền; 409 `LastAdminProtected`; token nhân viên bị vô hiệu bị từ chối ngay |
| Đổi mật khẩu đúng/sai/yếu; replay; thử sai nhiều lần | 204 chỉ khi mật khẩu cũ đúng; không lộ mật khẩu ở log/audit; sai nhiều lần không khóa tài khoản; token cũ tự hết hạn (D-T11) |
| Reset mật khẩu bởi Admin | Mật khẩu tạm hiển thị một lần; replay cùng key không sinh mật khẩu mới |
| Chuyển trạng thái giải theo mục 5 | Chỉ chuyển hợp lệ; trường bị khoá sau đăng ký đầu |
| Đăng ký hợp lệ / `rankId` sai game / thiếu tiền | Một đăng ký + một ledger; sai game 400; thiếu tiền rollback không đăng ký |
| Ví đủ phí nhưng thiếu sau phí phiên | Rollback toàn bộ (D-T03) |
| Cashier đăng ký thay khách; Waiter thử đăng ký | Cashier thành công, trừ ví khách, audit có EmployeeId; Waiter 403 |
| Hai khách tranh chỗ cuối; đăng ký đua đóng giải | Một bên thành công; bên thua 409, không ledger dở dang |
| Đăng ký lặp cùng giải / cùng key khác payload | 409 `AlreadyRegistered` / `IdempotencyConflict` |
| Client gửi `skillScore`, `feeCharged` | 400, server tính điểm |
| Preview rồi Confirm; plan có đăng ký trùng; số người không chia hết | Preview không ghi; Confirm kiểm tra lại, `AvgSkillScore` đúng, `TeamSizeMismatch` có số dư |
| Cùng dữ liệu chia đội hai lần | Quy tắc dự phòng cho kết quả giống nhau; nếu dùng ML phải cố định phiên bản/seed **[CẦN CHỐT]** |
| Huỷ trước/sau mốc hoàn; huỷ lặp; huỷ khi đã chia đội | Hoàn đúng % một lần; khách bị chặn sau khi chia đội; nhân viên huỷ xoá `TeamMembers` |
| Huỷ giải nhiều đăng ký; restart giữa chừng | Mỗi đăng ký hoàn đúng một lần, tổng hoàn = tổng phí |
| Khách Locked đăng ký / nhận hoàn | Đăng ký 409; hoàn vẫn thực hiện |
| Khách khác | 404, không lộ dữ liệu |
| Export đăng ký | Đúng cột/mã hoá, chỉ người có `tournament.manage` |

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
| Triển khai | `Services` | `RegistrationService` gọi `WalletLedger.Record` và `SessionSettlement.BeforeWalletDebit` trong transaction của caller, không tự commit; `TeamBalancingService` gọi ML hoặc quy tắc dự phòng ở mục 6 (owner Phúc, **[CẦN CHỐT]**) |
| DI | `DependencyInjection.cs` | Đăng ký service/validator; Infrastructure đăng ký repository |

Application chỉ tham chiếu Domain; EF/SQL và migration ở Infrastructure; controller, claims, route ở API.

## 14. Việc còn mở / bàn giao

| Việc | Người |
| --- | --- |
| Đối chiếu `S1-D-1` và mockup UI → cập nhật mục 3, 10 | Owner task |
| Chốt D-T01..D-T06, công thức SkillScore, đăng ký lại sau huỷ, thu hồi token, đăng ký thay khách trừ ví, độ dài mật khẩu tối thiểu | Nhóm + Trưởng nhóm |
| Xác nhận loại ledger, khóa `TournamentFee/Refund`, cột khóa chống lặp | Minh |
| Xác nhận định dạng dữ liệu đăng ký, công thức điểm và thuật toán chia đội | Phúc |
| Duyệt cuối | Trưởng nhóm |

## Phụ lục A. Biên bản chốt quyết định (phí giải và dữ liệu đăng ký)

Trạng thái: **chờ chốt**. Cột "Đề xuất" do người soạn đặt, chưa có xác nhận của owner nào; điền cột cuối sau khi họp hoặc nhận phản hồi thực tế. Chỉ tick Done khi đã có xác nhận và Trưởng nhóm duyệt.

| Mã | Câu hỏi | Phương án | Đề xuất | Lý do | Owner xác nhận | Quyết định / Người / Ngày |
| --- | --- | --- | --- | --- | --- | --- |
| D-T01 | Phí tính theo gì? | (a) mỗi người chơi (b) mỗi đội | (a), `EntryFee` mỗi đăng ký | Schema đặt `EntryFee` ở giải và đăng ký theo khách, đội do hệ thống chia sau | Trưởng nhóm | |
| D-T02 | Thanh toán bằng gì? | (a) chỉ ví (b) thêm tiền mặt/ZaloPay | (a) chỉ ví cho V1 | Dùng sẵn WalletLedger, giảm rủi ro thu/hoàn trùng | Minh | |
| D-T03 | Loại ledger và khóa chống lặp | `TournamentFee`/`TournamentRefund`, khóa `...:{registrationId}` | Như đề xuất | Cùng mô hình `OrderPayment/OrderRefund` | Minh | |
| D-T04 | Trừ phí trước/sau phí phiên? | Quyết toán phí phiên rồi mới trừ phí giải | Như đề xuất | Theo 004 mục 7.3 và 002 mục 4 | Phúc + Minh | |
| D-T05 | Chính sách hoàn | Hoàn 100% trước `FullRefundBefore` (mặc định = hết hạn đăng ký); sau đó `RefundPercentAfter` (mặc định 0); nhân viên huỷ/huỷ giải 100% | Như đề xuất | Rõ ràng, tái hiện được | Trưởng nhóm | |
| D-T06 | Khóa sửa phí | Không đổi `EntryFee`/chính sách hoàn sau đăng ký đầu tiên | Như đề xuất | Tránh hoàn sai | Owner task | |
| D-T07 | Khách huỷ rồi đăng ký lại | (a) khôi phục dòng cũ (b) cấm | (a) vì unique `(TournamentId, CustomerId)` | Không cần bỏ unique của schema | Owner task | |
| D-T08 | Công thức `SkillScore` | `round(100×(0.5×rankOrder/maxRankOrder + 0.3×winRate/100 + 0.2×min(hours,2000)/2000),2)` | Như đề xuất | Chuẩn hoá 0..100, vừa cột `decimal(6,2)` | Phúc | |
| D-T09 | Thuật toán chia đội | (a) ML của Phúc (b) snake draft theo `SkillScore` | (a) theo 001 (`team.assign` = chạy ML); (b) làm dự phòng | Có thể kiểm chứng; API preview/confirm dùng được cho cả hai | Phúc | |
| D-T10 | Định dạng dữ liệu đăng ký cho Phúc | CSV / JSON / seed SQL | CSV + JSON (đã có mẫu) | Phúc chọn dạng dễ nạp | Phúc | |
| D-T11 | Thu hồi token sau đổi/reset mật khẩu | (a) không thu hồi, token tự hết hạn ≤30 phút (b) thêm `TokenVersion`/revocation | (a) | 001 mục 5: JWT không bị thu hồi phía server, muốn thu hồi cần cơ chế lưu DB | Owner login | |
| D-T12 | Cashier đăng ký thay khách và trừ ví khách | (a) cho phép trong phạm vi đăng ký giải (b) chỉ Admin/Manager | (a) | `tournament.register` của 001 có Cashier; cần ghi audit EmployeeId | Trưởng nhóm + Minh | |
| D-T13 | Độ dài mật khẩu tối thiểu | 8 hoặc 12 ký tự | 12, khớp mật khẩu seed | Nhất quán với 001 | Owner login | |
