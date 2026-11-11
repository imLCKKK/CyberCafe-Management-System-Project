# SCRUM-9 — Pipeline ML đề xuất đội

| Thông tin | Giá trị |
| --- | --- |
| Task | SCRUM-9 / S1-P-3 — Define-ML-Team-Balancing-Pipeline |
| Phiên bản / ngày | Thiết kế v1.1 / 11/11/2026; bổ sung mapping preview → confirm |
| Base kiểm kê | `dev` tại `93cf457`; nhánh `SCRUM-9-define-ml-team-balancing-pipeline` |
| Owner / phối hợp | Phúc / Đại; Trưởng nhóm là reviewer cuối theo backlog |
| Phụ thuộc | S1-D-3 — Define-Staff-Tournament-Contracts |
| Trạng thái | Đặc tả để triển khai S2; chưa có bằng chứng Đại đối chiếu hoặc nghiệm thu của Trưởng nhóm |

SCRUM-9 kiểm kê và định nghĩa pipeline, không triển khai model, endpoint, migration hoặc thay thuật toán demo. Theo yêu cầu trực tiếp, công việc không chờ người dùng duyệt từng bước. Các lựa chọn thiết kế dưới đây không được ghi thành kết quả huấn luyện hay xác nhận của owner khác. Huấn luyện/phục vụ thuộc **S2-P-3**; lưu OP.GG thuộc **S2-P-2**; nguồn đăng ký thật thuộc **S2-D-2**.

## 1. Kiểm kê có bằng chứng

| Thành phần | Đã kiểm chứng trong source | Tận dụng / phần thiếu |
| --- | --- | --- |
| [Adapter OP.GG](../../Backend/integrations/opgg/opgg.js), [middleware](../../Backend/integrations/opgg/statsMiddleware.js), [helper Riot ID](../../Backend/integrations/opgg/shared.js) | VALORANT PC Competitive; đọc hồ sơ công khai, chuẩn hóa rank và bốn chỉ số; có cache/giới hạn/timeout | Giữ nguyên endpoint, công thức, schema và giới hạn; chưa có collector dataset lịch sử |
| [Model dữ liệu frontend](../../frontend/src/client/tournaments/tournamentModel.js) | `validateStats`, `fetchParticipantStats`, lưu `OpggSnapshot` vào kho demo | Dùng validator/contract làm chuẩn đối chiếu; localStorage chưa phải hồ sơ SQL tin cậy |
| [Hàm `generateTeams`](../../frontend/src/admin/shared/tournamentAdminModel.js) | Sắp `rank.tier` giảm dần, hòa điểm theo RegistrationId tăng dần, phân snake; kiểm tra đủ người/không trùng khách/chưa có đội | Baseline `rank-snake-v1`, **thuật toán viết tay**, không phải model ML; hiện chưa tối ưu vai trò |
| Nhánh Phúc lịch sử | Ref local `origin/Phuc` tại `52db755` (22/10/2026); remote hiện không còn nhánh `Phuc` | `server/opgg.js` và model giải frontend chỉ khác import so với đường dẫn mới; `tournamentAdminModel.js` giống nguyên nội dung. Không khôi phục cấu trúc cũ |
| [Entity đăng ký](../../Backend/src/CyberManagement.Domain/Entities/TournamentRegistration.cs), [đội](../../Backend/src/CyberManagement.Domain/Entities/Team.cs), [thành viên](../../Backend/src/CyberManagement.Domain/Entities/TeamMember.cs) | RegistrationId/TournamentId/CustomerId/RankId là int; PreferredRole nullable; WinRate, HoursPlayed, SkillScore; unique TournamentId + CustomerId và unique RegistrationId trong TeamMembers | Chưa có RiotId, OpggSnapshot, nguồn/thời điểm thống kê, modelVersion hoặc snapshot đề xuất trong entity hiện tại |
| Model/dataset ML | Không tìm thấy training code, dependency ML, artifact `.zip` ML được định danh, `.onnx/.pkl/.joblib/.pt/.h5`, notebook hoặc tập `.csv/.parquet/.jsonl` huấn luyện trong file versioned của base và cây lịch sử Phúc đã kiểm kê | Chưa có model, nhãn kết quả trận, manifest train/evaluation hay báo cáo đánh giá để tái sử dụng. Không kết luận về file cá nhân chưa được cung cấp |
| Contract S1-D-3 / SCRUM-4 | [Contract v0.7 tại `aad623a`](https://github.com/imLCKKK/CyberCafe-Management-System-Project/blob/aad623ac7b7bf120f8d08e88276ae317909dc789/Backend/docs/003-staff-tournament-contract.md), mục 3–6, 10–12 và phụ lục A–B; đã đọc từ ref `origin/fix/SCRUM-4-rank-snake-fallback` ngày 11/11/2026 | Đã có route/DTO/status/phí/Riot ID/PlanSnapshot; baseline đã thống nhất `rank-snake-v1`. V0.7 chỉ nhận `strategy: Balanced`; mapping bổ sung ML và gắn token với proposal tại mục 4 dưới đây, không coi là API đã triển khai |
| Nguồn ngoài repo được người dùng cung cấp | [OP.GG](https://op.gg/) | Đây là trang provider; chưa cung cấp file model/dataset có nhãn. Phạm vi adapter hiện hữu là VALORANT, không lấy dữ liệu LoL trang chủ để thay vào |
| Test và dữ liệu demo | [Adapter tests](../../Backend/integrations/opgg/opgg.test.js), [tournament tests](../../frontend/src/client/tournaments/tournamentModel.test.js), [baseline test](../../frontend/src/admin/shared/adminModel.test.js), mock Home và browser fixtures | Dùng kiểm tra kỹ thuật; loại khỏi dataset huấn luyện/đánh giá. Fixture cũng mang `source: "opgg"`, nên riêng trường này không chứng minh nguồn thật |

Phạm vi rà soát không bao gồm DB cá nhân, ổ đĩa ngoài repo hoặc file chưa được gửi. `GameRanks.RankId` là khóa SQL, **không phải** `rank.tier` của OP.GG; `WinRate/HoursPlayed/SkillScore` cũ chưa có provenance nên chưa đủ làm feature/label.

## 2. Contract OP.GG được giữ nguyên

`GET /api/valorant/opgg/stats?riotId=Name%23TAG` trả JSON; không có endpoint train hoặc lịch sử từng trận trong adapter này. Schema được [frontend validator](../../frontend/src/client/tournaments/tournamentModel.js) kiểm tra:

| Trường | Kiểu / ý nghĩa hiện tại |
| --- | --- |
| riotId, source, queue | Riot ID đã chuẩn hóa; `source = opgg`, `queue = competitive` |
| scope | Mùa/act Competitive do OP.GG chọn; không được đổi nhãn thành “N trận gần nhất” |
| fetchedAt, sourceUpdatedAt | ISO timestamp lấy tại adapter; timestamp cập nhật ở provider có thể null. Lấy mới không buộc provider cập nhật |
| rank | Null hoặc `{tier, name, division, label}`; validator nhận tier 0..27, division 0..3. Tier 0/unrank khác với chưa đọc được rank |
| matches | Số trận tổng hợp trong scope, integer ≥ 0 hoặc null |
| winRate | `wins / gameCount * 100`, gồm hòa trong mẫu số; number 0..100 hoặc null |
| headshotRate | `headShots / (headShots + bodyShots + legShots) * 100`; number 0..100 hoặc null |
| damagePerRound, kda | `damage / rounds`; `(kills + assists) / deaths`; number ≥ 0 hoặc null |

Bốn chỉ số được làm tròn hai chữ số; thiếu trường/mẫu số bằng 0 trả null. Không có trận thì bốn chỉ số null; thiếu statistics không được chuyển thành zero games. Không suy ra HoursPlayed, ACS, lịch sử trận, agent, vai trò, xác suất thắng hoặc nhãn ML từ các trường chưa có.

Giữ lỗi `{message}` và mã 400/403/404/409/429/502; HTTP 405 cho method khác GET. Cache thành công 10 phút, lỗi 30 giây, tối đa 200 hồ sơ; gộp request cùng Riot ID; tối đa 2 lookup đồng thời, 30 lookup mới/phút/instance, 12 bundle/lượt, 4 MiB/response, 10 giây/request và 25 giây/lượt. Không tăng các giới hạn để thu dataset.

[OP.GG Help](https://help.op.gg/hc/en-us/articles/31091405109401-Can-I-use-OP-GG-data) yêu cầu ghi nguồn và tránh gây tải quá mức; đây không phải cam kết cung cấp API/dataset cho bên thứ ba. Chỉ đọc nguồn công khai, không vượt hồ sơ riêng tư hoặc tự đổi policy. Khi thu dữ liệu sau này, manifest phải ghi quyền sử dụng/phạm vi nguồn cụ thể; trang chủ không thay manifest.

## 3. Bài toán và ràng buộc v1

**Bài toán:** học từ kết quả trận 5v5 thật để dự đoán xác suất đội A thắng B, rồi tìm hai đội có xác suất gần 0,5. Trọng số học từ dataset; không lấy công thức cộng rank/ADR viết tay rồi gọi là ML. Chất lượng dự đoán thắng và chất lượng chia đội được đo riêng.

Phạm vi v1: **VALORANT PC Competitive, đúng 10 người, hai đội 5 người**. Giữ khả năng nhiều đội/game khác của demo; endpoint ML v1 chưa hỗ trợ chúng và phải báo rõ, không bỏ người để ép về 10. Mở rộng cần version contract mới.

Các điều kiện bắt buộc:

1. API lấy giải và toàn bộ đăng ký đủ điều kiện từ SQL sau khi xác thực: giải `RegistrationClosed`, chưa có đội xác nhận; đăng ký `Registered`, khách `Active`, phí đã xử lý theo S1-D-3 (miễn phí hoặc Wallet thu cùng transaction đăng ký). Không nhận danh sách con do browser chọn. Confirm chuyển giải sang `TeamsAssigned`, đăng ký vẫn `Registered`; không dùng trạng thái demo `Closed/Assigned` cho API. Các lựa chọn phí/ledger trong S1-D-3 không đồng nghĩa đã được owner ví xác nhận hoặc đã có code.
2. Có đúng 10 RegistrationId, 10 CustomerId và 10 Riot ID chuẩn hóa không trùng; dùng `parseRiotId` hiện có và khóa so sánh không phân biệt hoa/thường (`toLocaleLowerCase('en')`) như adapter. Mỗi đăng ký thuộc đúng giải, khách còn đủ điều kiện. Mỗi người xuất hiện đúng một lần trong output, mỗi đội đúng 5 người. Không tự loại người thiếu dữ liệu.
3. Vai trò chỉ thuộc `Duelist/Initiator/Controller/Sentinel/Flex`. Mỗi đội phải phủ bốn vai trò đầu; người thứ năm có thể trùng vai trò hoặc Flex. Người chọn vai trò cụ thể giữ nguyên vai trò; chỉ người chọn Flex được gán sang vai trò thiếu. Nếu không có cách phủ đủ, trả `ROLE_CONSTRAINT_UNSATISFIABLE`. Đây là policy sản phẩm v1 cho giao điểm này, không phải luật game. DTO S1-D-3 cho phép preferredRole null/chuỗi khác; các giá trị đó chưa đủ chạy v1, trả lỗi roster thay vì tự gán Flex.
4. Snapshot phải đúng Riot ID, game/queue và cùng scope hỗ trợ; có provenance từ lần đọc provider thật. V1 yêu cầu rank tier 1..27 và ít nhất 10 trận. Các chỉ số nullable được xử lý theo mục 6, không dùng mặc định rank 0 hoặc số liệu khách khai báo.
5. Khi suy luận: fetchedAt không ở tương lai và không quá 24 giờ. Nếu sourceUpdatedAt có giá trị thì không ở tương lai/không quá 7 ngày; nếu null, cảnh báo `SOURCE_FRESHNESS_UNKNOWN`, không tự nhận số liệu provider là mới. Ngưỡng này là policy chất lượng v1, tách biệt cache 10 phút của adapter.
6. Input bất biến + model/version + policy như nhau phải cho cùng đề xuất. Không nhận khóa nhóm, điểm cân bằng, quyền hoặc đường dẫn model do browser quyết định.

## 4. Input/output và giao điểm với Đại

### 4.1. Route/DTO chung và mapping SCRUM-4 → SCRUM-9

Dùng route của **S1-D-3 / SCRUM-4 v0.7**; `/team-proposals` trong bản SCRUM-9 v1.0 được thay bằng mapping này, không triển khai thêm route công khai đó. Phần mở rộng dưới đây là contract thiết kế dùng cho S2-D-4/S2-P-3, chưa phải thay đổi source/API hoặc xác nhận owner đã tích hợp.

| Giao điểm | Contract dùng khi triển khai |
| --- | --- |
| Preview | `POST /api/admin/tournaments/{id}/teams/preview`, permission **team.assign** (Admin/Manager); `TeamPreviewRequest = {strategy: ML\|Balanced, expectedVersion: string}`. Hai trường bắt buộc, enum phân biệt hoa/thường, không mặc định phương pháp. `ML` là giá trị bổ sung cho DTO v0.7 |
| Chọn phương pháp | `strategy: ML` → input nội bộ `strategy: ml` → output `method: ml`, DTO `modelSource: ML`, `modelVersion` từ artifact, `baselineVersion: null`. `strategy: Balanced` → `rank-snake-v1` → `method: rank-snake-v1`, DTO `modelSource: Balanced`, `modelVersion: null`, `baselineVersion: rank-snake-v1`. `Manual` chỉ xuất hiện sau swap, không phải strategy preview |
| Version giải | `expectedVersion` là `TournamentDto.version` rowversion base64 do server cấp. Preview mới trả lại cùng giá trị ở `TeamPlanDto.version`; Confirm dùng chính giá trị đó. Mọi thay đổi đăng ký/cấu hình giải ảnh hưởng plan phải tăng version aggregate giải trong cùng transaction. Không chuyển rowversion thành hash roster hoặc ngược lại |
| Roster | Không gửi `registrationIds` hoặc `rosterVersion` trong request công khai. API đọc toàn bộ roster đủ điều kiện, snapshot/provenance tin cậy và tính `rosterVersion` nội bộ theo mục 4.2; trả hash để truy vết. Confirm tính lại hash ngay cả khi rowversion giải chưa đổi |
| Kết quả preview mới | Giữ các trường `TeamPlanDto` của SCRUM-4; bổ sung `version`, `rosterVersion`, `proposalId`, `expiresAtUtc`, `quality`, `warnings`. `planToken` là UUID do server sinh, khác `proposalId` hash nội dung. Server giữ toàn bộ output mục 4.3 trong binding token; browser không tự cấp model/version/điểm |
| Confirm | `POST /api/admin/tournaments/{id}/teams`, cùng quyền, header `Idempotency-Key` theo SCRUM-4; `ConfirmTeamsRequest = {planToken, expectedVersion, teams: [{teamName, members: [{registrationId, assignedRole?}]}]}`. Không nhận proposalId/rosterVersion/model metadata do client khai thay binding server. Thành công `201 TeamPageDto`; replay cùng key/payload trả kết quả cũ |
| Đội đã xác nhận | Sau xác thực/quyền và validation DTO, Preview ưu tiên trả snapshot/version hiện tại, `planToken: null`, không tạo plan hoặc so expectedVersion để tạo plan mới; giữ nguồn `ML/Balanced/Manual` thực tế. Muốn chia lại theo SCRUM-4 phải DELETE hợp lệ rồi Preview/Confirm mới; không dùng token cũ |

ML lỗi trả lỗi rõ; UI chỉ gọi lại với `Balanced` khi người có quyền chọn baseline. Mỗi lần preview phương pháp khác có token/binding riêng; không tái sử dụng token ML cho baseline. `Balanced` tổng quát vẫn giữ phạm vi số đội của SCRUM-4 mục 6.1; giao điểm/đánh giá ML v1 dưới đây dùng đúng roster 10 người. Không gọi OP.GG trong preview/tìm phương án hoặc transaction confirm; refresh và lưu snapshot là bước riêng S2-P-2 trước preview.

### 4.2. Input nội bộ Phúc nhận từ module giải

| Trường | Kiểu / quy tắc |
| --- | --- |
| contractVersion, featureVersion | `team-balance.v1`, `opgg-competitive.v1` |
| policyVersion | `valorant-5v5.v1`: ràng buộc vai trò và gate dữ liệu mục 3; đổi rule phải đổi version, không sửa nội dung dưới cùng version |
| strategy | `ml` hoặc `rank-snake-v1`, do API ánh xạ từ ML/Balanced |
| tournamentId, gameId | int dương; gameId lấy SQL, resolve thành `valorant`; không hard-code GameId = 1 |
| teamSize, teamCount | 5 và 2 |
| expectedVersion, rosterVersion, generatedAtUtc | Rowversion giải được kiểm tra ở API; hash roster riêng; thời điểm server UTC |
| participants | Đúng 10 phần tử, sắp RegistrationId tăng dần |
| participant.registrationId/customerId | int dương từ đăng ký SQL |
| status, feePolicy; participant.customerStatus/registrationStatus/feeCharged/feeRefunded/feeEligible | Trạng thái và policy từ SQL; số tiền snapshot nullable giữ nguyên; feeEligible do server xác minh theo S1-D-3, không suy ra từ số dư ví hiện tại hoặc dữ liệu browser |
| participant.riotId/preferredRole | Riot ID chuẩn hóa; vai trò enum ở mục 3 |
| participant.stats | Toàn bộ object OP.GG ở mục 2, không đổi tên hoặc đơn vị |
| participant.provenance | `{origin: provider, snapshotId, snapshotSha256, capturedAtUtc}` do collector/server tin cậy ghi; có bản lưu đối chiếu |

`rosterVersion` hash đúng object `{tournamentId, gameId, status, teamSize, feePolicy, participants}`. `feePolicy` gồm đúng `{entryFee, fullRefundBeforeUtc, refundPercentAfter}`; mỗi participant trong phép hash gồm đúng `{registrationId, customerId, customerStatus, riotId, preferredRole, registrationStatus, feeCharged, feeRefunded, feeEligible, snapshotSha256}`, sắp RegistrationId tăng dần. Giữ null của số tiền chưa có; `feeEligible` là boolean server xác minh việc miễn phí/thu phí hợp lệ, bắt buộc true trước khi tạo proposal. Không âm thầm bỏ đăng ký Registered có khách bị khóa/phí không hợp lệ để tạo roster nhỏ hơn: trả `409 TeamPlanInvalid` để xử lý đăng ký trước.

`snapshotSha256` hash đúng object `{stats, provenance: {origin, snapshotId, capturedAtUtc}}`, không chứa chính snapshotSha256; snapshotId là chuỗi định danh do collector cấp. Giữ nguyên toàn bộ stats và chuỗi thời gian do adapter trả, không đổi null thành 0. Trường thời gian do server tạo (`capturedAtUtc`, `fullRefundBeforeUtc`) chuẩn hóa UTC `YYYY-MM-DDTHH:mm:ss.SSSZ` trước hash. Đổi rank, scope, thời điểm hoặc nguồn cũng đổi hash. Không hash SkillScore tổng hợp như một feature sức mạnh. Quy ước chung: canonical JSON theo [RFC 8785 JCS](https://www.rfc-editor.org/rfc/rfc8785), encode UTF-8, SHA-256, trả `sha256:` + 64 ký tự hex thường; đủ các key đã liệt kê, không bỏ key null hoặc hash JSON có thứ tự key tùy ý. `expectedVersion` bắt cả thay đổi aggregate không làm đổi feature. S1-D-3 đã có thiết kế rowversion/PlanSnapshot; S2-D-4/S2-P-2 còn phải triển khai lưu trữ và test vector JCS chung Node/.NET, chưa có các cột này trong SQL của checkout này.

Preview mới đọc version/roster/snapshot trong cùng một ảnh chụp dữ liệu nhất quán, suy luận ngoài transaction giữ khóa; trước khi cấp token kiểm tra lại version/hash với dữ liệu hiện tại. Nếu đã đổi, trả `409 VersionConflict` hoặc `409 TeamPlanInvalid` như confirm, không cấp token cho đầu vào trộn từ nhiều thời điểm.

### 4.3. Output nội bộ và binding token

Output Phúc trả gồm `contractVersion=team-balance.v1`, `tournamentId`, `rosterVersion`, `proposalId`, `method`, `modelVersion`, `modelSha256`, `featureVersion`, `policyVersion`, `datasetVersion`, `baselineVersion`, `teams: [{teamKey, members: [{registrationId, assignedRole}]}]`, `quality`, `warnings`, `persisted: false`. ML lấy model/dataset/hash từ artifact manifest và baselineVersion null; baseline có baselineVersion `rank-snake-v1`, modelVersion/modelSha256/datasetVersion null, xác suất/imbalance null.

`proposalId` hash JCS/SHA-256 của đúng các trường `contractVersion, tournamentId, rosterVersion, method, modelVersion, modelSha256, featureVersion, policyVersion, datasetVersion, baselineVersion, teams`. Chuẩn hóa nhóm theo dãy RegistrationId tăng dần so sánh từ điển, gán teamKey A/B theo thứ tự nhóm, members sắp RegistrationId; giữ assignedRole. Không hash tên đội, proposalId, UUID token, thời điểm sinh hoặc cảnh báo. Hash nội dung không phải bằng chứng quyền/token. Model v1 chưa mô hình hóa map/agent/party synergy hoặc chứng minh hiệu lực trên giải custom.

API kiểm tra output rồi tạo UUID ngẫu nhiên cho `planToken`, giữ binding bất biến phía server: **token → EmployeeId từ JWT + tournamentId + expectedVersion + rosterVersion + proposalId + toàn bộ input/output + version/hash artifact/policy + thời điểm cấp/hết hạn**. TTL 10 phút tính bằng giờ UTC server. Preview không ghi SQL, Teams/TeamMembers, audit nghiệp vụ hoặc đổi trạng thái/version; binding tạm nằm trong cache server, không ở browser. Cache mất/restart hoặc token hết hạn → `409 InvalidPlanToken`, phải preview lại. Nhiều instance phải dùng cùng cache binding hoặc định tuyến tới instance giữ token; không tin UUID do client tự dựng. Cách bố trí cache thuộc bước triển khai, không thêm dependency/schema trong SCRUM-9.

Mapping đội sang `TeamPlanDto` giữ nguyên phân nhóm/vai trò. Trong giao điểm VALORANT v1 này, điểm thành viên/trung bình/chênh lệch hiển thị là **tier OP.GG** cho cả ML và Balanced; ghi `pointScale=OpggRankTier` trong snapshot. `quality` chứa xác suất ML riêng. `RegistrationDto.skillScore` tổng hợp vẫn chỉ tham khảo, không dùng sắp baseline, không ghi xác suất vào `TournamentRegistrations.SkillScore` hoặc `Teams.AvgSkillScore`.

Các trường bổ sung của `TeamPlanDto`: `version: string`, `rosterVersion: string?`, `proposalId: string?`, `expiresAtUtc: string?`, `quality: object?`, `warnings: string[]`. Preview mới bắt buộc có hash, expiry và quality; quality gồm `predictedWinProbabilityA`, `predictedImbalance`, `rankMeanGap`, `roleCoverageValid` như hai mẫu dưới. Mảng teams của preview và đọc lại nguồn ML giữ thứ tự nhóm A rồi B theo sourceProposal, kể cả khi confirm gửi ngược thứ tự mảng; xác suất A không phụ thuộc tên đội người dùng sửa.

| Kết quả đọc lại | Metadata trả về |
| --- | --- |
| Đã confirm, chưa swap | `version` hiện tại của giải; `planToken/ expiresAtUtc = null`; proposalId/rosterVersion/quality/warnings lấy từ snapshot đã chốt, không suy luận hoặc refresh lại. Xác suất là đánh giá lúc đề xuất; A là nhóm thành viên A trong sourceProposal, kể cả sau đổi tên hoặc đảo thứ tự mảng confirm |
| Đã swap → Manual | `modelVersion/baselineVersion/proposalId/quality = null`, `planToken/expiresAtUtc = null`; tính điểm tier/scoreSpread theo đội hiện tại. rosterVersion vẫn là hash đầu vào gốc để truy vết; không gọi nó là version roster hiện tại. ProposalId, xác suất và cảnh báo cân bằng ML gốc chỉ giữ trong sourceProposal, không trình bày như đánh giá của đội đã chỉnh tay; warnings chỉ chứa cảnh báo áp dụng cho snapshot hiện tại |
| Snapshot cũ thiếu metadata | Trường hash/quality không có bằng chứng trả null, warnings có `LEGACY_PLAN_METADATA_UNAVAILABLE`; không tạo hash/model/token giả. Không coi snapshot này là đầu vào confirm mới |

### 4.4. Confirm và dữ liệu thay đổi trước confirm

1. Xác thực/quyền/trạng thái staff ở server. Cùng Idempotency-Key và payload đã thành công replay kết quả cũ **trước** kiểm tra version/token đã hết hạn hoặc bị tiêu thụ; cùng key khác payload → `409 IdempotencyConflict`.
2. Với confirm mới, dùng transaction/khóa theo S1-D-3 (Customer theo ID → Tournament → Registration → Team; bỏ qua nhóm không dùng). Đội đã tồn tại → `409 TeamsAlreadyFormed`. Token phải thuộc đúng staff/giải, chưa hết hạn; request.expectedVersion phải bằng binding.expectedVersion và version giải hiện tại, sai → `409 VersionConflict`. Giải phải còn `RegistrationClosed`.
3. Đọc lại toàn bộ roster/eligibility/snapshot/provenance dưới khóa, tính lại hash và kiểm tra độ mới ở thời điểm confirm. Hash khác binding → `409 TeamPlanInvalid` dù version giải chưa đổi. Snapshot đã quá hạn → `422 STATS_STALE`. Kiểm tra proposalId từ input/output đã giữ, các version contract/feature/policy/baseline và model/dataset/hash trong manifest đang được cấu hình; khác binding → `409 TeamPlanInvalid`. Chỉ ML phụ thuộc model/dataset/hash artifact: thiếu/hỏng/không kiểm chứng được → `503 MODEL_UNAVAILABLE`; kích hoạt model ML khác không tự làm mất hiệu lực token Balanced. Ghim cấu hình artifact/policy trong khoảng kiểm tra đến commit, phối hợp cơ chế kích hoạt model để không đổi giữa chừng. Không chạy lại bằng model mới hoặc chuyển baseline khi confirm.
4. Chuẩn hóa thứ tự nhóm/thành viên của payload; bỏ qua tên đội, assignedRole thiếu thì lấy từ chính registration trong đề xuất; gửi null/vai trò ngoài enum của v1 → `400 ValidationError`. So sánh với **proposal của binding**, khác nhóm/vai trò → `409 TeamPlanMismatch` dù vẫn đủ roster và role. Chỉ cho đổi tên đội (trim 1..100, duy nhất không phân biệt hoa/thường). Server luôn lấy phương pháp/version từ token đã gửi: hai proposal có thể cùng phân hoạch nhưng khác phương pháp, không được suy ra phương pháp từ danh sách đội. UI gửi đúng token của preview đang hiển thị.
5. Chỉ sau mọi kiểm tra mới ghi Teams/TeamMembers, `TeamsAssigned`, snapshot và kết quả idempotency/audit cùng transaction. `PlanSnapshot.sourceProposal` giữ input/output bất biến, proposalId, rosterVersion, expectedVersion, model/baseline/feature/policy/dataset versions, modelSha256 và tier/provenance; snapshot hiện tại giữ tên đội cuối/pointScale. Sau commit mới tiêu thụ token và phát event. Lỗi/rollback **không ghi đội, snapshot, audit, idempotency hoặc đổi version/trạng thái**; hai confirm cạnh tranh không tạo hai bộ đội. Swap về sau theo SCRUM-4 mục 6.2 ghi nguồn Manual và giữ sourceProposal, không sửa nguồn gốc.

Khóa ở bước 2–3 phải bảo vệ cả tập đăng ký (ngăn thêm/hủy chen ngang) và con trỏ snapshot hiện hành đến hết commit. Luồng sửa đăng ký, khóa khách và refresh OP.GG phải tham gia cùng cơ chế khóa/version; snapshot đã thu là bất biến, refresh tạo snapshot mới rồi đổi con trỏ dưới khóa. Chỉ đọc hash một lần mà cho writer đổi dữ liệu trước commit không đáp ứng contract. Model/policy cũng dùng một thế hệ cấu hình bất biến được ghim đến commit; version và hash không được tái sử dụng cho nội dung khác.

### 4.5. Hai ví dụ preview → confirm

Hai JSON dưới là **kịch bản contract minh họa**, không phải request đã chạy, model thật hay test vector hash. Hash/UUID/version và xác suất là giá trị mẫu; preview lúc `2026-11-11T03:00:00Z`. Server đã giữ snapshot hợp lệ có tier của RegistrationId 101..110 lần lượt 27..18; preferredRole đúng như vai trò các member bên dưới. Các member ở response lược bỏ customerName/rankName để dễ đọc; triển khai vẫn trả DTO đầy đủ. Hai luồng thành công chạy độc lập từ cùng trạng thái ban đầu; mỗi nhánh `rejectIfChanged` thay thế cho confirm thành công. Riêng ca trộn token giả định cả hai preview cùng còn hạn trong cache.

**ML:** chọn `ML`; token chỉ trỏ output `method=ml`, artifact `valorant-win.v1.example` và manifest/hash được giữ phía server.

```json
{
  "preview": {
    "method": "POST",
    "url": "/api/admin/tournaments/7/teams/preview",
    "body": {"strategy": "ML", "expectedVersion": "AAAAAAAAAAc="},
    "status": 200,
    "response": {
      "version": "AAAAAAAAAAc=",
      "rosterVersion": "sha256:1111111111111111111111111111111111111111111111111111111111111111",
      "proposalId": "sha256:2222222222222222222222222222222222222222222222222222222222222222",
      "planToken": "5e8c9d10-1111-4222-8333-444455556666",
      "expiresAtUtc": "2026-11-11T03:10:00Z",
      "modelSource": "ML", "modelVersion": "valorant-win.v1.example", "baselineVersion": null,
      "teams": [
        {"teamName":"Doi A","avgSkillScore":22.2,"members":[{"registrationId":101,"skillScore":27,"assignedRole":"Duelist"},{"registrationId":104,"skillScore":24,"assignedRole":"Initiator"},{"registrationId":106,"skillScore":22,"assignedRole":"Controller"},{"registrationId":108,"skillScore":20,"assignedRole":"Sentinel"},{"registrationId":110,"skillScore":18,"assignedRole":"Flex"}]},
        {"teamName":"Doi B","avgSkillScore":22.8,"members":[{"registrationId":102,"skillScore":26,"assignedRole":"Duelist"},{"registrationId":103,"skillScore":25,"assignedRole":"Initiator"},{"registrationId":105,"skillScore":23,"assignedRole":"Controller"},{"registrationId":107,"skillScore":21,"assignedRole":"Sentinel"},{"registrationId":109,"skillScore":19,"assignedRole":"Flex"}]}
      ],
      "unassigned": [], "scoreSpread": 0.6,
      "quality": {"predictedWinProbabilityA":0.52,"predictedImbalance":0.02,"rankMeanGap":0.6,"roleCoverageValid":true},
      "warnings": []
    }
  },
  "confirm": {
    "method": "POST", "url": "/api/admin/tournaments/7/teams",
    "headers": {"Idempotency-Key":"example-ml-confirm-001"},
    "body": {
      "planToken":"5e8c9d10-1111-4222-8333-444455556666", "expectedVersion":"AAAAAAAAAAc=",
      "teams":[
        {"teamName":"Nexora A","members":[{"registrationId":101,"assignedRole":"Duelist"},{"registrationId":104,"assignedRole":"Initiator"},{"registrationId":106,"assignedRole":"Controller"},{"registrationId":108,"assignedRole":"Sentinel"},{"registrationId":110,"assignedRole":"Flex"}]},
        {"teamName":"Nexora B","members":[{"registrationId":102,"assignedRole":"Duelist"},{"registrationId":103,"assignedRole":"Initiator"},{"registrationId":105,"assignedRole":"Controller"},{"registrationId":107,"assignedRole":"Sentinel"},{"registrationId":109,"assignedRole":"Flex"}]}
      ]
    },
    "status":201, "resultDto":"TeamPageDto", "tournamentStatus":"TeamsAssigned", "createdTeams":2, "createdMembers":10
  },
  "rejectIfChanged":[
    {"change":"registration cancelled; tournament version changed","status":409,"code":"VersionConflict","createdTeams":0,"createdMembers":0},
    {"change":"active model version or artifact hash changed","status":409,"code":"TeamPlanInvalid","createdTeams":0,"createdMembers":0},
    {"change":"confirm swaps registrations 101 and 102 but keeps their roles","status":409,"code":"TeamPlanMismatch","createdTeams":0,"createdMembers":0}
  ]
}
```

**Baseline:** chọn `Balanced`; token mới trỏ `method=rank-snake-v1`. Cùng tier đầu vào, snake cho A = 101/104/105/108/109, B = 102/103/106/107/110; không đổi chỗ để tối ưu role.

```json
{
  "preview": {
    "method": "POST",
    "url": "/api/admin/tournaments/7/teams/preview",
    "body": {"strategy": "Balanced", "expectedVersion": "AAAAAAAAAAc="},
    "status": 200,
    "response": {
      "version": "AAAAAAAAAAc=",
      "rosterVersion": "sha256:1111111111111111111111111111111111111111111111111111111111111111",
      "proposalId": "sha256:3333333333333333333333333333333333333333333333333333333333333333",
      "planToken": "7a9b0c11-2222-4333-8444-555566667777",
      "expiresAtUtc": "2026-11-11T03:10:00Z",
      "modelSource": "Balanced", "modelVersion": null, "baselineVersion": "rank-snake-v1",
      "teams": [
        {"teamName":"Doi A","avgSkillScore":22.6,"members":[{"registrationId":101,"skillScore":27,"assignedRole":"Duelist"},{"registrationId":104,"skillScore":24,"assignedRole":"Initiator"},{"registrationId":105,"skillScore":23,"assignedRole":"Controller"},{"registrationId":108,"skillScore":20,"assignedRole":"Sentinel"},{"registrationId":109,"skillScore":19,"assignedRole":"Flex"}]},
        {"teamName":"Doi B","avgSkillScore":22.4,"members":[{"registrationId":102,"skillScore":26,"assignedRole":"Duelist"},{"registrationId":103,"skillScore":25,"assignedRole":"Initiator"},{"registrationId":106,"skillScore":22,"assignedRole":"Controller"},{"registrationId":107,"skillScore":21,"assignedRole":"Sentinel"},{"registrationId":110,"skillScore":18,"assignedRole":"Flex"}]}
      ],
      "unassigned": [], "scoreSpread": 0.2,
      "quality": {"predictedWinProbabilityA":null,"predictedImbalance":null,"rankMeanGap":0.2,"roleCoverageValid":true},
      "warnings": []
    }
  },
  "confirm": {
    "method": "POST", "url": "/api/admin/tournaments/7/teams",
    "headers": {"Idempotency-Key":"example-baseline-confirm-001"},
    "body": {
      "planToken":"7a9b0c11-2222-4333-8444-555566667777", "expectedVersion":"AAAAAAAAAAc=",
      "teams":[
        {"teamName":"Nexora A","members":[{"registrationId":101,"assignedRole":"Duelist"},{"registrationId":104,"assignedRole":"Initiator"},{"registrationId":105,"assignedRole":"Controller"},{"registrationId":108,"assignedRole":"Sentinel"},{"registrationId":109,"assignedRole":"Flex"}]},
        {"teamName":"Nexora B","members":[{"registrationId":102,"assignedRole":"Duelist"},{"registrationId":103,"assignedRole":"Initiator"},{"registrationId":106,"assignedRole":"Controller"},{"registrationId":107,"assignedRole":"Sentinel"},{"registrationId":110,"assignedRole":"Flex"}]}
      ]
    },
    "status":201, "resultDto":"TeamPageDto", "tournamentStatus":"TeamsAssigned", "createdTeams":2, "createdMembers":10
  },
  "rejectIfChanged":[
    {"change":"OP.GG snapshot changed; tournament version unchanged","status":409,"code":"TeamPlanInvalid","createdTeams":0,"createdMembers":0},
    {"change":"role policy version changed","status":409,"code":"TeamPlanInvalid","createdTeams":0,"createdMembers":0},
    {"change":"confirm uses ML token with baseline partition","status":409,"code":"TeamPlanMismatch","createdTeams":0,"createdMembers":0}
  ]
}
```

### 4.6. Lỗi và mapping HTTP

| HTTP / code | Trường hợp |
| --- | --- |
| 400 / ValidationError | Strategy sai/thiếu, ID sai kiểu/trùng, request thiếu trường hoặc có trường ngoài DTO |
| 401 / 403 | Chưa đăng nhập/thiếu team.assign; giữ auth chung hiện tại |
| 404 / TournamentNotFound | Không có giải theo quyền truy cập |
| 409 / VersionConflict | expectedVersion khác binding hoặc rowversion giải hiện tại; tương ứng ROSTER_CHANGED khi aggregate đổi |
| 409 / TeamPlanInvalid | Hash roster/proposal, model/hash hoặc policy/feature version đổi; roster không hợp lệ. ROSTER_CHANGED khi rowversion chưa đổi và MODEL_VERSION_MISMATCH nội bộ map về lỗi này ở confirm |
| 409 / InvalidPlanToken, TeamPlanMismatch | Token sai staff/giải/hết hạn/mất binding; phân nhóm hoặc vai trò khác đề xuất gắn token |
| 409 / TeamsAlreadyFormed, TournamentLocked, IdempotencyConflict | Tương ứng TEAMS_ALREADY_CONFIRMED, TOURNAMENT_NOT_CLOSED và cùng key khác payload |
| 409 / TeamSizeMismatch, TeamNameConflict | Sai số người/chia không hết (SCRUM-4); tên đội trùng sau trim/case-fold |
| 422 / UNSUPPORTED_GAME_OR_SIZE | ML ngoài VALORANT 10 người/2 đội × 5; không thu hẹp baseline nhiều đội của SCRUM-4 |
| 422 / STATS_UNAVAILABLE, STATS_STALE, STATS_SCOPE_MISMATCH | Thiếu/sai nguồn, rank/matches không đạt, snapshot cũ hoặc khác scope |
| 422 / ROLE_CONSTRAINT_UNSATISFIABLE | Không có phân chia/gán Flex thỏa vai trò |
| 503 / MODEL_UNAVAILABLE | Artifact thiếu/hỏng, chưa qua gate đánh giá hoặc schema không tương thích; không tự fallback |

HTTP dùng **ProblemDetails + code** như SCRUM-4, không trả body `{code, message}` riêng như bản SCRUM-9 v1.0; `detail` là thông báo an toàn, `registrationIds?` chỉ cho staff được đọc roster. Các mã PascalCase trên giữ quy ước S1-D-3; lỗi stats/role/model hiện dùng uppercase như bảng. Không trả token, đường dẫn model hoặc lỗi thô trong lỗi. Không đổi body lỗi endpoint OP.GG hiện hữu.

## 5. Baseline, model và tìm phương án

1. **Baseline phân đội B0:** tái sử dụng quy tắc `generateTeams` hiện tại thành `rank-snake-v1`. Không gọi trực tiếp hàm demo từ production backend; S2 port phần thuật toán thuần, giữ thứ tự và tie-break. Baseline cũng phải qua validator nguồn/roster/output; nếu vi phạm vai trò thì báo không hợp lệ, không sửa âm thầm và vẫn gọi kết quả đó là baseline cũ.
2. **Baseline dự đoán B1:** xác suất hằng 0,5. **B2:** logistic regression chỉ dùng chênh lệch rank trung bình, fit trên train. B2 phân biệt giá trị tăng thêm của stats so với rank; không tự đặt hệ số thắng từ rank.
3. **Model M1:** binary logistic regression được huấn luyện từ kết quả thắng thật, dùng [ML.NET SDCA Logistic Regression](https://learn.microsoft.com/en-us/dotnet/api/microsoft.ml.trainers.sdcalogisticregressionbinarytrainer). Output Probability là xác suất; normalization/preprocessing và model được đóng gói cùng nhau. Đây là lựa chọn thiết kế chưa có artifact trong repo.
4. Với 10 người, cố định RegistrationId nhỏ nhất ở A, chọn thêm 4 trong 9 người: **C(9,4) = 126** phân hoạch duy nhất, B là phần bù. Kiểm tra vai trò; Flex gán vào vai trò thiếu theo thứ tự Duelist → Initiator → Controller → Sentinel, xử lý người theo RegistrationId. Loại phương án không hợp lệ; không dùng random search.
5. Với feature chênh lệch d và xác suất pipeline q(d), dùng `p(A,B) = (q(d) + 1 - q(-d)) / 2` để đảo hai đội cho xác suất bổ sung. Đánh giá model bằng chính phép biến đổi này; không chỉ báo metric của q chưa đối xứng hóa.
6. Chọn phương án có `abs(p - 0.5)` nhỏ nhất, sau đó `rankMeanGap = abs(mean(tierA) - mean(tierB))` nhỏ nhất, rồi dãy RegistrationId đội A nhỏ nhất theo thứ tự từ điển. So sánh số ở độ chính xác đầy đủ; chỉ làm tròn khi hiển thị. `abs(p - 0.5) > 0.10` trả cảnh báo `BALANCE_TARGET_UNMET`, không bịa phương án tốt hơn.

Chi phí là 126 phương án × hai hướng dự đoán; xử lý 5 role slots/đội với số người cố định. Tính feature mỗi người một lần, batch các dự đoán; không query SQL, gọi OP.GG hoặc load model lại cho mỗi phương án. Seed train là 42; inference không cần RNG. Cùng artifact và input, hoán vị thứ tự input không đổi output.

## 6. Dataset và đặc trưng

### 6.1. Đơn vị dữ liệu và nhãn

Một bản ghi gốc là **một trận 5v5 đã kết thúc**, có matchId, competition/groupId, patch/act, startedAtUtc, hai roster và 10 snapshot **đã được thu trước startedAtUtc**, cùng winner A/B có nguồn xác minh. Hòa, remake, trận bỏ dở hoặc không có kết quả rõ bị loại và ghi số lượng/lý do. Một trận không thành 10 nhãn độc lập theo từng người.

Hiện adapter chỉ cung cấp aggregate mùa. Một snapshot tải hôm nay không được gắn vào trận quá khứ để tạo training row: có thể đã chứa chính kết quả đó và các trận tương lai. Chưa có lịch sử snapshot trước trận hoặc nguồn kết quả trận trong repo; đây là blocker dữ liệu, không thể khắc phục bằng đổi tên mock thành dataset. Chọn nguồn match-level hợp lệ/collector thuộc S2-P-3 và cần xác minh quyền sử dụng; chưa giả định OP.GG homepage có API xuất tập này.

Nguồn thật phải có manifest: datasetVersion/hash, URL/nguồn và phạm vi sử dụng, collectorCommit, collectedAtUtc, featureVersion, rawSnapshot hashes, cách xác minh kết quả, số trận/người/scope/nhãn, số dòng bị loại, quy tắc định danh. Lưu matchId định danh gốc để dedup; nhóm theo người dùng khóa giả danh ổn định. Không dùng CustomerId, Riot ID, tên hoặc thông tin ví làm feature. File chia sẻ không chứa credential hoặc mapping định danh không cần thiết.

Provenance `origin = provider` chỉ được collector/server tin cậy ghi sau lần đọc thật, có bản lưu/hash đối chiếu. `origin = fixture/demo/unknown` hoặc thiếu provenance bị loại. Chỉ chuỗi `source: opgg` trong JSON không đủ chứng minh; dữ liệu mock đang dùng cùng giá trị này.

### 6.2. Feature schema `opgg-competitive.v1`

Feature mỗi người theo thứ tự: **rankTier, winRate/100, headshotRate/100, damagePerRound, kda, log(1 + matches)**. PreferredRole chỉ dùng ràng buộc/gán vai trò vì là lựa chọn đăng ký; không giả định nó là vai trò/agent thật ở trận lịch sử. Scope/patch là metadata để lọc/đánh giá, không mã hóa arbitrary ID thành sức mạnh.

Rank và matches phải đạt mục 3. Bốn chỉ số còn lại được phép null: fit median từng chỉ số trên **train** rồi impute ở cả train/validation/test/inference; thêm bốn cờ thiếu tương ứng. Median không được fit bằng toàn dataset; một cột không có giá trị train thì chặn build model. Zero thật giữ nguyên, không đổi thành missing. Không clip ngầm số ngoài schema; loại dữ liệu sai bằng validator.

Vector đội là trung bình 6 feature đã xử lý và tỷ lệ thiếu của 4 chỉ số. Vector trận **10 chiều** là vector A trừ vector B. Fit normalization trên train, lưu cùng model; mọi nơi dùng cùng đơn vị/thứ tự. Không đưa winner, kết quả trận mục tiêu, thông số sau trận hoặc SkillScore tự sinh vào X.

## 7. Train / validation / evaluation

1. Chuẩn hóa nguồn/định danh; dedup matchId và alias cùng trận trước khi split. Giữ nhóm giải/series, không để hai bản sao hoặc hai hướng của cùng trận sang hai fold.
2. Split theo thời gian **70% train / 15% validation / 15% test** ở mức trận gốc; giữ nguyên competition/series group. Chọn hai mốc UTC gần tỷ lệ nhất; bỏ/quarantine nhóm trải qua ranh giới, không xé nhóm để đạt tỷ lệ. Bảo đảm `max(train time) < min(validation time)` và `max(validation time) < min(test time)`; ghi tỷ lệ thực tế vào split manifest.
3. Định hướng A/B bằng hash matchId trước khi split, độc lập nhãn. Chỉ augment đảo A/B và nhãn ở **train**; fit preprocessing theo train và không dùng test để chọn model/ngưỡng. Đếm kích thước theo trận gốc, không tính bản augment là mẫu mới.
4. Dùng validation chọn L2 trong `{0.01, 0.1, 1}`, L1 = 0, tối đa 100 iterations, seed 42, một training thread và tắt shuffle; giữ nguyên hyperparameters sau lựa chọn. Chốt manifest rồi đánh giá test một lần. Mỗi lần dùng lại test để sửa model phải công bố, tạo evaluation mới trước khi tuyên bố đạt.
5. Người chơi có thể xuất hiện ở nhiều fold thời gian: kết quả chính chỉ chứng minh dự đoán trận tương lai trong quần thể đó. Báo riêng overlap và evaluation cho roster người chưa thấy khi có đủ trận; chưa có cold-player holdout thì ghi **chưa xác minh người chơi mới**, không gọi split thời gian là disjoint theo người.
6. Mục tiêu data gate v1: ≥ **1.000 trận gốc**, ≥ **200 người**, train ≥ 600, validation và test mỗi tập ≥ 150 trận và ≥ 30 mẫu mỗi nhãn. Nếu bỏ nhóm ở ranh giới làm không đủ thì cần thêm dữ liệu, không nới gate sau khi nhìn test. Đây là ngưỡng vận hành của thiết kế, không phải định lý về số mẫu đủ cho mọi dataset.

Split manifest phải liệt kê matchId/group/player keys mỗi fold, mốc thời gian, snapshot cutoff và hashes để người khác kiểm tra leakage. Bộ kiểm tra bắt buộc thất bại nếu có cùng trận/alias/group qua fold, snapshot sau giờ bắt đầu, provenance fixture, missing label, sai số người hoặc schema khác.

## 8. Chỉ số và ngưỡng bàn giao S2

Các ngưỡng dưới đây là **gate thiết kế chưa được đo**; không phải kết quả thực nghiệm hay bảo đảm cân bằng trận thực tế.

| Hạng mục | Cách đo / ngưỡng |
| --- | --- |
| Data/leakage | 100% row dùng đánh giá có provenance/cutoff/nhãn; zero match/group overlap, zero fixture; đạt data gate mục 7 |
| Khả năng phân biệt | Test ROC-AUC ≥ 0,60; báo thêm accuracy/F1, label counts và AUC của B1/B2. Accuracy không phải gate duy nhất |
| Xác suất | Brier `mean((p-y)^2)` ≤ 0,24 **và** ≤ 95% Brier của cả B1 và B2; log loss không tệ hơn baseline dự đoán tốt nhất. Báo calibration bins và bootstrap CI theo competition group, không theo bản augment |
| Ràng buộc | 100% case hợp lệ đủ hai đội × 5, đủ role, không trùng/bỏ sót, không đổi roster/team đã chốt; case không khả thi trả đúng lỗi |
| Tái hiện | Hoán vị input/đảo A-B/retry cùng version đều nhất quán; sai khác xác suất giữa hai hướng ≤ 1e-6 |
| So baseline chia đội | Trên cùng roster/snapshot, báo tỷ lệ B0 hợp lệ, rankMeanGap, xác suất surrogate của M1, cảnh báo role/thiếu dữ liệu. Không coi B0 vi phạm role là một phương án hợp lệ để so thắng; không loại case khó khỏi báo cáo |
| Mục tiêu tối ưu | Nếu tồn tại phương án hợp lệ có `abs(p-0,5) ≤ 0,10` thì chọn trong mục tiêu; nếu không có, trả phương án hợp lệ tốt nhất kèm cảnh báo. B0 hợp lệ cũng nằm trong 126 phân hoạch nên không được có objective tốt hơn phương án đã chọn |
| Hiệu năng | CPU, model đã load: p95 ≤ 2 giây trên 100 lượt roster 10 người; tính chuẩn hóa/validation/126 phương án/predict/output, tách thời gian gọi provider và cold load. Ghi CPU/RAM/OS/.NET/model hash và từng lượt, không dùng số build làm latency |

Metric của chính M1 trên đội do M1 chọn chỉ là **surrogate**, không chứng minh đội sẽ có tỷ lệ thắng 50/50. Đánh giá cân bằng thật cần trận sau đề xuất và kết quả được thu hợp lệ; chưa có thì ghi chưa xác minh. Không dùng nhãn do rank-snake hoặc M1 tự tạo làm ground truth. Hướng dẫn [ML.NET metrics](https://learn.microsoft.com/en-us/dotnet/machine-learning/resources/metrics) là nguồn định nghĩa AUC/accuracy; ngưỡng ở đây là lựa chọn của task.

## 9. Môi trường và artifact suy luận

- Giữ React/Vite + API .NET 10/EF/SQL Server hiện có; adapter OP.GG tiếp tục Node ≥ 22. Thiết kế trainer offline C# và inference CPU Windows x64/.NET 10, package **Microsoft.ML 5.0.1 stable** ghim khi làm S2. [NuGet chính thức](https://www.nuget.org/packages/Microsoft.ML) liệt kê target .NET Standard 2.0 và tương thích computed net10.0; khả năng restore/chạy thực tế của package trên dự án **chưa kiểm chứng**.
- Không cài package/tạo service Python/GPU hoặc project rỗng trong SCRUM-9. Khi triển khai: Application sở hữu DTO/interface; implementation ML.NET ở Infrastructure, API ghép DI, Domain không phụ thuộc ML.NET/HTTP. Tool training offline không chạy trong request nghiệp vụ.
- Artifact gồm model + preprocessing, manifest JSON (contract/feature/policy/model/dataset versions, game/platform/queue, supportedScopes/patches, trainingTimeCutoff, modelSha256, splitManifestHash, codeCommit, runtime/package, seed/options), metrics và nguồn dữ liệu. [ML.NET save/load](https://learn.microsoft.com/en-us/dotnet/machine-learning/how-to-guides/save-load-machine-learning-models-ml-net) hỗ trợ đóng gói transformer; chưa có `model.zip` thật để bàn giao.
- Inference chỉ load artifact tin cậy từ cấu hình server, kiểm tra hash/schema/gate/version lúc khởi động. Không nhận đường dẫn model từ browser. Load một lần; batch prediction theo request, không dùng chung đối tượng prediction mutable giữa các request đồng thời. Không train online hoặc tự thay model đã dùng cho đề xuất đang chờ xác nhận.
- Nếu model/data/schema không đạt, trả lỗi ở mục 4.6; UI có thể cho người có quyền **chọn rõ** baseline. Baseline vẫn giữ nhãn phương pháp và giới hạn, không đóng yêu cầu ML bằng luồng dự phòng.

## 10. Blocker và trách nhiệm

| ID | Blocker hiện tại | Người phối hợp / điều kiện gỡ |
| --- | --- | --- |
| B1 | **Đã có đầu vào contract, không còn blocker “chưa có S1-D-3”**: đã đọc SCRUM-4 v0.7 tại `aad623a`; mục 3–4 bổ sung mapping ML/Balanced → proposal → UUID token → confirm, status/phí và từ chối dữ liệu đổi | Đại + Phúc triển khai cùng mapping mục 4 ở S2-D-4/S2-P-3; kiểm chứng cache/token/transaction và đối chiếu bản bổ sung. Trưởng nhóm duyệt cuối; chưa coi tài liệu hoặc JSON mẫu là tích hợp đạt |
| B2 | Chưa có dataset trận gốc có nhãn và snapshot trước trận; adapter hiện tại chỉ trả aggregate mùa | Phúc: xác định nguồn/điều kiện sử dụng, thu hoặc nhận tập có manifest và cutoff; đạt data gate. OP.GG homepage hoặc 14 trận của một profile không thay 14 training rows |
| B3 | Chưa có model/artifact/manifest/metrics; không có training/inference runtime trong repo | Phúc ở S2-P-3: trainer, artifact và kiểm chứng package/CPU/gate; không tạo model từ fixture để đóng blocker |
| B4 | SQL hiện chưa lưu đủ Riot ID/OP.GG/provenance/model/input version; S1-D-3 đã thiết kế RiotId/rowversion/PlanSnapshot nhưng chưa triển khai trong checkout này | Đại + Phúc ở S2-D-4/S2-P-2; Trường điều phối migration nếu cần: lưu input/output/sourceProposal và metadata mục 4 trong PlanSnapshot, không tự đổi schema ở SCRUM-9 |
| B5 | Chưa có dataset/holdout cho người chơi mới và trận sau chia đội | Phúc + Đại: ghi phạm vi generalization và thu đánh giá thật; không tự tuyên bố độ cân bằng thực tế từ predicted probability |

Đã đối chiếu tài liệu S1-D-3 thực tế; đây là bằng chứng đọc contract của AI, không phải xác nhận trực tiếp của Đại/Phúc hoặc quyết định nghiệm thu của Trưởng nhóm.

## 11. Kiểm chứng đã chạy và ma trận bàn giao

**Bằng chứng lần trước, ngày 08/11/2026**, từ repo root (giữ nguyên; không chạy lại Node/live trong đợt sửa mapping):

```powershell
node --test Backend/integrations/opgg/opgg.test.js frontend/src/client/tournaments/tournamentModel.test.js frontend/src/admin/shared/adminModel.test.js
git diff --check
```

- **28 kiểm tra Node đạt**: 6 adapter, 6 model giải/OP.GG và 16 admin model, gồm baseline đủ người/không trùng/chưa có stats và giữ nguyên dữ liệu đầu vào. Fixture kiểm tra contract/công thức/cache/lỗi; không phải đánh giá ML hoặc dữ liệu provider thật.
- **Live HTTP adapter + validator frontend đạt** với profile công khai `Blue#00h`: HTTP 200, `source=opgg`, `queue=competitive`, scope `V26 - ACT6 · Competitive`, matches 14; fetchedAt `2026-11-08T13:17:38.876Z`, sourceUpdatedAt `2026-11-08T12:07:36+00:00`. Lượt thành công đi qua middleware localhost → GET profile → ba GET asset → action chỉ đọc POST → chuẩn hóa → validateStats, không thay response bằng fixture. Không sửa adapter.
- Lượt live đầu trả 502 sau 10.755 ms; kiểm tra kết nối sau đó GET profile 200, rồi chạy lại đầy đủ HTTP đạt. Giữ kết quả đầu là lỗi kết nối/timeout đã quan sát; chưa xác định chắc request upstream nào của lượt đầu lỗi. Không gọi lần fixture đạt là provider pass.
- Kiểm tra tài liệu đạt: 51 link tương đối trên bốn file, hai example JSON parse được, đủ 11 mục và không có whitespace/conflict marker. Lượt kiểm tra heading đầu gặp encoding stdin PowerShell làm mất ký tự Unicode; đổi kiểm tra sang số mục và chạy lại đạt. `git diff --check` đạt; source adapter/frontend/backend và dependency manifest không có diff.
- Browser suites OP.GG cũ trong repo dựa trên route demo trước auth hiện tại; chưa chạy lại trong task thiết kế này và không dùng báo cáo 22/10 thay bằng chứng mới. Chưa train model/chạy metrics, benchmark inference hoặc API/SQL đề xuất đội vì chúng chưa triển khai. Không cần build ứng dụng cho thay đổi tài liệu.

**Đợt cập nhật 11/11/2026:** chỉ sửa tài liệu/mapping và hai JSON minh họa mục 4.5. Giữ bằng chứng **28/28** trên vì source/test adapter, frontend baseline/model giải, backend và dependency không đổi so với `1b2ceb9`. Kiểm tra tài liệu đạt: `git diff --check`; script Node đọc Markdown xác nhận **2/2 JSON** parse/khớp route/strategy/version/token, đủ 10 thành viên/role, điểm tier/trung bình và phân hoạch snake; **51/51 link tương đối**, đủ 11 mục, không conflict marker. Script chỉ kiểm tra dữ liệu minh họa, không phải Node regression hoặc thực thi endpoint. Không chạy thêm Node regression, live OP.GG, ML, API hoặc SQL; JSON không chứng minh cơ chế từ chối/rollback đã chạy thật.

Lượt so sánh nguyên văn bằng chứng cũ đầu tiên lỗi do mốc tiếng Việt bị encoding stdin PowerShell làm sai; đổi bộ trích đoạn sang regex ASCII, chạy lại đạt và xác nhận nguyên khối bằng chứng 08/11 được giữ nguyên. Không sửa source để xử lý lỗi của script kiểm tra này.

Rà soát bổ sung theo yêu cầu kiểm tra lại: hoàn thiện projection hash roster/provenance và policyVersion, dữ liệu đọc nhất quán khi preview, khóa chống refresh/sửa roster chen ngang confirm, metadata null khi đọc lại Manual và thứ tự A/B khi trả xác suất. Đối chiếu SCRUM-4 v0.7, kiểm tra lại hai JSON, 51 link và diff đạt; bằng chứng 28/28 giữ nguyên. B2–B5 là phụ thuộc dữ liệu/triển khai/đánh giá S2, không phải kết quả ML đã hoàn thành trong task thiết kế SCRUM-9.

| Tiêu chí SCRUM-9 | Bằng chứng / trạng thái |
| --- | --- |
| Kiểm kê phần Phúc đã làm, nguồn và feature; giữ contract adapter | Mục 1–2 và kiểm tra Node/live ở trên; model/dataset ngoài nguồn đã cung cấp không được tự giả định có |
| Input/output, constraint, baseline, split, metric/gate, runtime và blocker | Đặc tả mục 3–10; mapping route/DTO/token và hai luồng ML/baseline ở mục 4 đã bổ sung theo S1-D-3 v0.7; chưa có kết quả ML/tích hợp |
| Đại đối chiếu / Trưởng nhóm nghiệm thu | Đã đối chiếu nguồn contract do Đại viết; chưa có xác nhận trực tiếp của owner về bản mapping này hoặc duyệt cuối. Checklist backlog giữ nguyên, không tự chuyển Done |

Các ca S2 phải thêm khi triển khai: 9/11 người; duplicate IDs/customer/Riot ID; sai giải/game/status; stats null/stale/khác act/provenance fixture; missing role/Flex đủ và không đủ; model thiếu/sai version; input reorder/A-B symmetry; roster/model đổi trước confirm; xác nhận lặp/restart/concurrency; baseline được chọn rõ khi ML lỗi. Chúng là kế hoạch kiểm thử, **chưa phải tests đã chạy**.

Ca tích hợp bổ sung cho mapping: refresh OP.GG đua confirm không được commit với input đã đổi; khách bị khóa không bị âm thầm bỏ khỏi roster; hai phương pháp cho cùng phân hoạch vẫn lưu metadata đúng token; đảo thứ tự đội khi confirm không đảo nghĩa xác suất A lúc đọc lại; swap chuyển Manual phải xóa quality/proposalId hiện tại nhưng giữ sourceProposal gốc; token hết hạn/mất cache trả InvalidPlanToken còn replay thành công cùng key vẫn trả kết quả cũ.
