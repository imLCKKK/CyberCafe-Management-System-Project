# SCRUM-9 — Pipeline ML đề xuất đội

| Thông tin | Giá trị |
| --- | --- |
| Task | SCRUM-9 / S1-P-3 — Define-ML-Team-Balancing-Pipeline |
| Phiên bản / ngày | Thiết kế v1.0 / 08/11/2026 |
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
| Nguồn ngoài repo được người dùng cung cấp | [OP.GG](https://op.gg/) | Đây là trang provider; chưa cung cấp file model/dataset có nhãn hoặc contract S1-D-3. Phạm vi adapter hiện hữu là VALORANT, không lấy dữ liệu LoL trang chủ để thay vào |
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

1. API lấy giải và toàn bộ đăng ký đủ điều kiện từ SQL sau khi xác thực. Giải đã đóng đăng ký, chưa có đội xác nhận; danh sách caller chọn phải khớp toàn bộ roster đủ điều kiện. S1-D-3 phải chốt trạng thái chính thức và policy phí; không tự coi demo `Registered/Closed` là API đã có.
2. Có đúng 10 RegistrationId, 10 CustomerId và 10 Riot ID chuẩn hóa không trùng; dùng `parseRiotId` hiện có và khóa so sánh không phân biệt hoa/thường (`toLocaleLowerCase('en')`) như adapter. Mỗi đăng ký thuộc đúng giải, khách còn đủ điều kiện. Mỗi người xuất hiện đúng một lần trong output, mỗi đội đúng 5 người. Không tự loại người thiếu dữ liệu.
3. Vai trò chỉ thuộc `Duelist/Initiator/Controller/Sentinel/Flex`. Mỗi đội phải phủ bốn vai trò đầu; người thứ năm có thể trùng vai trò hoặc Flex. Người chọn vai trò cụ thể giữ nguyên vai trò; chỉ người chọn Flex được gán sang vai trò thiếu. Nếu không có cách phủ đủ, trả `ROLE_CONSTRAINT_UNSATISFIABLE`. Đây là policy đề xuất của sản phẩm, không phải khẳng định luật game bắt buộc đội hình này; Đại cần đối chiếu ở S1-D-3.
4. Snapshot phải đúng Riot ID, game/queue và cùng scope hỗ trợ; có provenance từ lần đọc provider thật. V1 yêu cầu rank tier 1..27 và ít nhất 10 trận. Các chỉ số nullable được xử lý theo mục 6, không dùng mặc định rank 0 hoặc số liệu khách khai báo.
5. Khi suy luận: fetchedAt không ở tương lai và không quá 24 giờ. Nếu sourceUpdatedAt có giá trị thì không ở tương lai/không quá 7 ngày; nếu null, cảnh báo `SOURCE_FRESHNESS_UNKNOWN`, không tự nhận số liệu provider là mới. Ngưỡng này là policy chất lượng v1, tách biệt cache 10 phút của adapter.
6. Input bất biến + model/version + policy như nhau phải cho cùng đề xuất. Không nhận khóa nhóm, điểm cân bằng, quyền hoặc đường dẫn model do browser quyết định.

## 4. Input/output và giao điểm với Đại

### 4.1. HTTP đề xuất cho S1-D-3, chưa triển khai

`POST /api/admin/tournaments/{tournamentId}/team-proposals`, permission **team.assign** hiện có (Admin/Manager). Request:

```json
{
  "contractVersion": "team-balance.v1",
  "rosterVersion": "sha256:roster-returned-by-server",
  "registrationIds": [101, 102, 103, 104, 105, 106, 107, 108, 109, 110],
  "strategy": "ml"
}
```

`strategy` nhận `ml` hoặc `rank-snake-v1`; mặc định `ml`. Baseline chỉ chạy khi caller chọn rõ. Không tự fallback sang baseline khi model lỗi rồi trả `method: ml`. Đây là request minh họa tổng hợp, không phải đăng ký hoặc số đo thật.

API xác minh rosterVersion, IDs và quyền rồi tự dựng input nội bộ; không tin stats/Riot ID do browser gửi. Không gọi OP.GG từ vòng tìm phương án. Lookup/refresh là bước riêng của S2-P-2 trước khi tạo snapshot.

### 4.2. Input nội bộ Phúc nhận từ module giải

| Trường | Kiểu / quy tắc |
| --- | --- |
| contractVersion, featureVersion | `team-balance.v1`, `opgg-competitive.v1` |
| tournamentId, gameId | int dương; gameId lấy SQL, resolve thành `valorant`; không hard-code GameId = 1 |
| teamSize, teamCount | 5 và 2 |
| rosterVersion, generatedAtUtc | Hash roster có thứ tự chuẩn; thời điểm server UTC |
| participants | Đúng 10 phần tử, sắp RegistrationId tăng dần |
| participant.registrationId/customerId | int dương từ đăng ký SQL |
| participant.riotId/preferredRole | Riot ID chuẩn hóa; vai trò enum ở mục 3 |
| participant.stats | Toàn bộ object OP.GG ở mục 2, không đổi tên hoặc đơn vị |
| participant.provenance | `{origin: provider, snapshotId, snapshotSha256, capturedAtUtc}` do collector/server tin cậy ghi; có bản lưu đối chiếu |

`rosterVersion` hash canonical UTF-8 gồm tournamentId/gameId/status, eligibility và bộ `{registrationId, customerId, riotId, preferredRole, registrationStatus, snapshotSha256}` sắp theo RegistrationId. Hash snapshot lấy từ toàn bộ stats. Quy ước chung: canonical JSON theo [RFC 8785 JCS](https://www.rfc-editor.org/rfc/rfc8785), encode UTF-8, SHA-256, trả `sha256:` + 64 ký tự hex thường; không hash JSON có thứ tự key tùy ý. S1-D-3/S2-P-2 phải chốt mapping lưu trữ và có test vector JCS dùng chung Node/.NET trước khi triển khai; chưa có các cột này trong SQL.

### 4.3. Output thành công

```json
{
  "contractVersion": "team-balance.v1",
  "tournamentId": 7,
  "rosterVersion": "sha256:roster-returned-by-server",
  "proposalId": "sha256:canonical-proposal",
  "method": "ml",
  "modelVersion": "valorant-win.v1.example",
  "featureVersion": "opgg-competitive.v1",
  "policyVersion": "valorant-5v5.v1",
  "datasetVersion": "example-only",
  "teams": [
    {"teamKey":"A","members":[{"registrationId":101,"assignedRole":"Duelist"},{"registrationId":104,"assignedRole":"Initiator"},{"registrationId":106,"assignedRole":"Controller"},{"registrationId":108,"assignedRole":"Sentinel"},{"registrationId":110,"assignedRole":"Flex"}]},
    {"teamKey":"B","members":[{"registrationId":102,"assignedRole":"Duelist"},{"registrationId":103,"assignedRole":"Initiator"},{"registrationId":105,"assignedRole":"Controller"},{"registrationId":107,"assignedRole":"Sentinel"},{"registrationId":109,"assignedRole":"Flex"}]}
  ],
  "quality": {"predictedWinProbabilityA":0.52,"predictedImbalance":0.02,"rankMeanGap":0.4,"roleCoverageValid":true},
  "warnings": [],
  "persisted": false
}
```

**Toàn bộ example trên là minh họa contract**, không phải model hoặc kết quả đánh giá có thật. `modelVersion/datasetVersion` khi chạy thật lấy từ artifact manifest. `proposalId` là hash nội dung canonical, không phải bằng chứng quyền hoặc token xác nhận. Baseline trả `method: rank-snake-v1`, modelVersion/datasetVersion null và xác suất/imbalance null; không trình bày điểm rank như xác suất thắng.

Hash proposal theo cùng JCS/SHA-256, chỉ gồm contract/tournament/rosterVersion, method, model/feature/policy/dataset versions và teams/members đã sắp chuẩn; không gồm chính proposalId, timestamp sinh hoặc nội dung cảnh báo. Model v1 là tuyến tính trên chênh lệch đặc trưng; chưa mô hình hóa map/agent/party synergy hoặc khẳng định hiệu lực trên giải custom từ dữ liệu Competitive.

Đề xuất không tạo Teams/TeamMembers hoặc chuyển trạng thái. Khi Đại triển khai xác nhận, server đọc lại roster/snapshot, quyền và điều kiện giải trong transaction, kiểm tra proposal theo đúng model/policy đã trả; roster/model thay đổi trả 409. Cùng đề xuất đã xác nhận không tạo đội lần hai; khác đề xuất sau xác nhận bị từ chối. Model không còn để kiểm chứng trả lỗi, không tin lại team list từ browser. Lưu được version/input hash/assignment cùng đội rồi mới phát event sau commit. Cách lưu metadata và endpoint xác nhận thuộc S1-D-3/S2-D-4, chưa được bổ sung trong task này.

Không ghi xác suất đội thắng vào `TournamentRegistrations.SkillScore` hay `Teams.AvgSkillScore`: chúng không có cùng ý nghĩa. Việc dùng hai cột hiện có cần Đại thống nhất rõ semantics trước khi ghi.

### 4.4. Lỗi

| HTTP / code | Trường hợp |
| --- | --- |
| 400 / INVALID_REQUEST | ContractVersion/strategy sai, ID sai kiểu/trùng, request thiếu trường |
| 401 / 403 | Chưa đăng nhập/thiếu team.assign; giữ auth chung hiện tại |
| 404 / TOURNAMENT_NOT_FOUND | Không có giải theo quyền truy cập |
| 409 / ROSTER_CHANGED, TEAMS_ALREADY_CONFIRMED, TOURNAMENT_NOT_CLOSED | Roster/version đổi, đội đã chốt, trạng thái giải không phù hợp |
| 422 / UNSUPPORTED_GAME_OR_SIZE, INVALID_ROSTER | Không phải v1 5v5 VALORANT, thiếu/thừa người, khách/Riot ID trùng hoặc không đủ điều kiện |
| 422 / STATS_UNAVAILABLE, STATS_STALE, STATS_SCOPE_MISMATCH | Thiếu/sai nguồn, rank/matches không đạt, snapshot cũ hoặc khác scope |
| 422 / ROLE_CONSTRAINT_UNSATISFIABLE | Không có phân chia/gán Flex thỏa vai trò |
| 503 / MODEL_UNAVAILABLE, MODEL_VERSION_MISMATCH | Artifact thiếu/hỏng, khác featureVersion, chưa qua gate đánh giá hoặc không còn version để xác nhận |

Body đề xuất `{code, message, registrationIds?}`; registrationIds chỉ trả cho staff đã được phép đọc roster. Không trả token, đường dẫn model hoặc lỗi thô của runtime. Không đổi body lỗi của endpoint OP.GG hiện hữu.

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
- Nếu model/data/schema không đạt, trả lỗi ở mục 4.4; UI có thể cho người có quyền **chọn rõ** baseline. Baseline vẫn giữ nhãn phương pháp và giới hạn, không đóng yêu cầu ML bằng luồng dự phòng.

## 10. Blocker và trách nhiệm

| ID | Blocker hiện tại | Người phối hợp / điều kiện gỡ |
| --- | --- | --- |
| B1 | Chưa có contract S1-D-3 trong các nguồn đã đọc: eligibility/status/phí, Riot ID sở hữu, policy role, snapshot/proposal metadata, endpoint xác nhận | Đại + Phúc: đối chiếu mục 3–4, cung cấp DTO/status/version/cách lưu; ghi quyết định thực tế, không suy ra từ UI demo |
| B2 | Chưa có dataset trận gốc có nhãn và snapshot trước trận; adapter hiện tại chỉ trả aggregate mùa | Phúc: xác định nguồn/điều kiện sử dụng, thu hoặc nhận tập có manifest và cutoff; đạt data gate. OP.GG homepage hoặc 14 trận của một profile không thay 14 training rows |
| B3 | Chưa có model/artifact/manifest/metrics; không có training/inference runtime trong repo | Phúc ở S2-P-3: trainer, artifact và kiểm chứng package/CPU/gate; không tạo model từ fixture để đóng blocker |
| B4 | SQL hiện chưa lưu đủ Riot ID/OP.GG/provenance/model/input version | Đại + Phúc; Trường điều phối migration nếu cần: chốt mapping trong S1-D-3/S2-P-2 trước khi code, không tự đổi schema ở SCRUM-9 |
| B5 | Chưa có dataset/holdout cho người chơi mới và trận sau chia đội | Phúc + Đại: ghi phạm vi generalization và thu đánh giá thật; không tự tuyên bố độ cân bằng thực tế từ predicted probability |

Tài liệu là đầu vào để Đại đối chiếu contract; chưa có bằng chứng bước phối hợp đó đã diễn ra.

## 11. Kiểm chứng đã chạy và ma trận bàn giao

Ngày 08/11/2026, từ repo root:

```powershell
node --test Backend/integrations/opgg/opgg.test.js frontend/src/client/tournaments/tournamentModel.test.js frontend/src/admin/shared/adminModel.test.js
git diff --check
```

- **28 kiểm tra Node đạt**: 6 adapter, 6 model giải/OP.GG và 16 admin model, gồm baseline đủ người/không trùng/chưa có stats và giữ nguyên dữ liệu đầu vào. Fixture kiểm tra contract/công thức/cache/lỗi; không phải đánh giá ML hoặc dữ liệu provider thật.
- **Live HTTP adapter + validator frontend đạt** với profile công khai `Blue#00h`: HTTP 200, `source=opgg`, `queue=competitive`, scope `V26 - ACT6 · Competitive`, matches 14; fetchedAt `2026-11-08T13:17:38.876Z`, sourceUpdatedAt `2026-11-08T12:07:36+00:00`. Lượt thành công đi qua middleware localhost → GET profile → ba GET asset → action chỉ đọc POST → chuẩn hóa → validateStats, không thay response bằng fixture. Không sửa adapter.
- Lượt live đầu trả 502 sau 10.755 ms; kiểm tra kết nối sau đó GET profile 200, rồi chạy lại đầy đủ HTTP đạt. Giữ kết quả đầu là lỗi kết nối/timeout đã quan sát; chưa xác định chắc request upstream nào của lượt đầu lỗi. Không gọi lần fixture đạt là provider pass.
- Kiểm tra tài liệu đạt: 51 link tương đối trên bốn file, hai example JSON parse được, đủ 11 mục và không có whitespace/conflict marker. Lượt kiểm tra heading đầu gặp encoding stdin PowerShell làm mất ký tự Unicode; đổi kiểm tra sang số mục và chạy lại đạt. `git diff --check` đạt; source adapter/frontend/backend và dependency manifest không có diff.
- Browser suites OP.GG cũ trong repo dựa trên route demo trước auth hiện tại; chưa chạy lại trong task thiết kế này và không dùng báo cáo 22/10 thay bằng chứng mới. Chưa train model/chạy metrics, benchmark inference hoặc API/SQL đề xuất đội vì chúng chưa triển khai. Không cần build ứng dụng cho thay đổi tài liệu.

| Tiêu chí SCRUM-9 | Bằng chứng / trạng thái |
| --- | --- |
| Kiểm kê phần Phúc đã làm, nguồn và feature; giữ contract adapter | Mục 1–2 và kiểm tra Node/live ở trên; model/dataset ngoài nguồn đã cung cấp không được tự giả định có |
| Input/output, constraint, baseline, split, metric/gate, runtime và blocker | Đặc tả mục 3–10 đã có để owner đối chiếu; chưa có kết quả ML |
| Đại đối chiếu / Trưởng nhóm nghiệm thu | Chưa có bằng chứng; checklist backlog giữ nguyên, không tự chuyển Done |

Các ca S2 phải thêm khi triển khai: 9/11 người; duplicate IDs/customer/Riot ID; sai giải/game/status; stats null/stale/khác act/provenance fixture; missing role/Flex đủ và không đủ; model thiếu/sai version; input reorder/A-B symmetry; roster/model đổi trước confirm; xác nhận lặp/restart/concurrency; baseline được chọn rõ khi ML lỗi. Chúng là kế hoạch kiểm thử, **chưa phải tests đã chạy**.
