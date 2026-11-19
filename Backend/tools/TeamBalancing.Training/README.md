# SCRUM-26 — Huấn luyện và API đề xuất đội VALORANT

Tool offline .NET 10; dùng Application chung và ML.NET tại Infrastructure. Contract gốc:
[SCRUM-9](../../../docs/contracts/ml-team-balancing-pipeline.md).
Đã có trainer ML.NET, lưu/tải model và báo cáo đánh giá. Chưa có dataset trận thật
hoặc model đạt nghiệm thu thực tế. API preview đọc roster và snapshot từ SQL.
Không coi dữ liệu kiểm thử hoặc kết quả baseline là nghiệm thu ML.

## Chạy từ repository root

```powershell
dotnet run --project Backend/tests/CyberManagement.TeamBalancing.Checks
dotnet run --project Backend/tests/CyberManagement.API.Checks -- --opgg-checks
dotnet run --project Backend/tests/CyberManagement.API.Checks

dotnet run --project Backend/tools/TeamBalancing.Training -- prepare .tools/team-balance-data/source.json .tools/team-balance-data/matches.jsonl .tools/team-balance-data/prepared-v1
dotnet run --project Backend/tools/TeamBalancing.Training -- train .tools/team-balance-data/prepared-v1 valorant-win.v1 .tools/team-balance-data/model-v1
```

Đầu vào phải là dữ liệu được phép sử dụng, có nguồn được người phụ trách xác minh.
Tool không tải provider, không sửa SQL và từ chối output đã tồn tại. Exit code:
`0` prepare/train hoàn thành (không đồng nghĩa model đạt gate); `1` dữ liệu/I/O
không hợp lệ hoặc holdout đã dùng; `2` sai cú pháp lệnh.
Không tạo output khi validation/data gate thất bại. Lỗi ghi đĩa có thể để lại output
chưa hoàn chỉnh; chỉ sử dụng output có đủ `artifact-hashes.json` và kiểm tra mọi hash.

## Đầu vào

`source.json` có đúng các trường sau (camelCase):

| Trường | Yêu cầu |
| --- | --- |
| datasetVersion | Phiên bản dataset bất biến |
| featureVersion | `opgg-competitive.v1` |
| dataKind | `observed-matches` cho nguồn trận thật đã đối chiếu; `generated-fixture` chỉ dùng kiểm thử; mặc định `unverified` được prepare nhưng không được train |
| sourceUrl | URL HTTPS của nguồn thật; không chứa username/password |
| usageTerms | Quyền/phạm vi sử dụng được xác minh; không phải giá trị tùy đặt để qua gate |
| collectorCommit | Commit collector đầy đủ: 40 ký tự hex thường |
| collectedAtUtc | ISO-8601 UTC; sau thời điểm xác minh kết quả, không ở tương lai |
| matchesSha256 | `sha256:` + SHA-256 hex thường của **byte nguyên file** matches.jsonl |

`matches.jsonl` chứa một JSON object mỗi dòng, đúng schema `MatchRecord` trong
[DatasetDtos.cs](DatasetDtos.cs). Không nhận field ngoài schema.

| Trường | Yêu cầu |
| --- | --- |
| matchId, aliases | ID trận gốc và các ID tương đương; aliases là mảng, có thể rỗng |
| groupId | Competition/series thực tế, giữ nguyên cả nhóm khi split; không tự tạo group riêng để xé series |
| scope, patch | Scope Competitive và patch của trận |
| startedAtUtc, completedAtUtc | UTC; hoàn thành sau bắt đầu |
| resultOrigin, resultSourceUrl, resultVerifiedAtUtc | `provider`, URL HTTPS kiểm chứng kết quả và thời điểm UTC sau hoàn thành |
| winner | `A`/`B`; `Draw`/`Remake`/`Abandoned` bị loại và ghi lý do; thiếu nhãn bị lỗi |
| teamA, teamB | Mỗi đội 5 phần tử: `{playerKey, preferredRole, snapshot}` |

`playerKey` là khóa giả danh ổn định cùng người qua mọi trận; không dùng tên/Riot ID
làm feature. `preferredRole` là lựa chọn có nguồn của đăng ký, thuộc
`Duelist/Initiator/Controller/Sentinel/Flex`; không suy diễn từ agent.
`snapshot` dùng nguyên `{stats, provenance}` từ collector SCRUM-25; stats có đủ
schema adapter, provenance gồm origin/snapshotId/capturedAtUtc/snapshotSha256.
Giữ bản raw có kiểm soát truy cập để đối chiếu hash; không commit dữ liệu người chơi.

Chỉ nhận `origin=provider`, hash nội dung đúng, cùng scope, tier 1..27 và >=10 trận.
Snapshot phải được thu **trước khi trận bắt đầu**, fetchedAt không quá 24 giờ trước
trận; sourceUpdatedAt nếu có không quá 7 ngày, nếu thiếu ghi cảnh báo độ mới không rõ.
Snapshot ID không được dùng lại cho nội dung khác. Fixture/demo/unknown bị từ chối.
Các trường origin/URL/hash kiểm tra tính nhất quán, **không tự chứng minh tính xác thực**;
người phụ trách phải đối chiếu nguồn/raw/nhãn trước khi dùng dataset.
Không dùng snapshot lấy hôm nay cho kết quả trận cũ.

## Split và feature

- Loại bản ghi giống hoàn toàn cùng matchId trước split; trùng ID khác nội dung hoặc
  alias nối hai trận bị lỗi, phải đối chiếu nguồn trước khi chạy lại.
- Mốc thời gian tại khoảng 70% và 85% số trận; các trận cùng timestamp vào cùng fold.
  Nhóm trải qua ranh giới bị quarantine; không xé nhóm hoặc nới gate.
  Nhóm có kết quả chỉ được xác minh sau/tại mốc bắt đầu fold kế tiếp cũng bị quarantine:
  nhãn train phải có sẵn trước validation, nhãn validation trước test.
- Sau quarantine cần >=1.000 trận, >=200 người, train >=600,
  validation/test mỗi tập >=150 và >=30 mẫu mỗi nhãn sau định hướng.
- A/B được đảo bằng bit thấp byte đầu SHA-256 UTF-8 matchId, độc lập nhãn.
  Chỉ train được augment đảo A/B; đếm mẫu theo trận gốc.
- Median của winRate/headshotRate/ADR/KDA chỉ fit trên train. Cột train hoàn toàn
  thiếu chặn xử lý; giá trị zero thật giữ nguyên, missing có cờ riêng.
- Vector người: tier, winRate/100, headshotRate/100, ADR, KDA, log(1+matches),
  bốn cờ thiếu theo thứ tự trên. Vector trận: mean(A) − mean(B), 10 chiều.
  Trainer fit normalization trên train và lưu trong model.zip; median được ghim trong model manifest cùng hash model/split.
- Báo overlap người chơi; temporal split không chứng minh khả năng trên người chưa thấy.

## Output

`train.jsonl` có hai hướng mỗi trận; `validation.jsonl` và `test.jsonl` có một hướng.
Các file chỉ có matchId/groupId/thời gian/label/features, không có Riot ID hoặc tên.
`split-manifest.json` giữ nguồn, hash nguồn, commit checkout, hash binary tool/Application,
runtime, mốc split, loại/quarantine, từng match/alias/group/player key/snapshot hash,
median train và overlap. Hash binary xác định code thực chạy ngay cả khi chưa commit.
`split-manifest.sha256` và `artifact-hashes.json` dùng kiểm tra tính toàn vẹn output.

`baseline.json` báo B0 `rank-snake-v1` trên **mọi roster test**, tỷ lệ thỏa vai trò,
các case vai trò không khả thi và mean rank gap trên roster hợp lệ; B1 hằng 0,5 có
Brier=0,25 và log loss=ln(2). Đây là chỉ số mô tả, chưa có kết quả trận sau chia đội.
Dữ liệu trận ngoài SQL không có RegistrationId: khi đo B0 offline, đánh số 1..10 theo
playerKey tăng dần làm tie-break, công khai khác biệt này với ID đăng ký production.
B2 logistic chỉ dùng rank, M1 ML.NET và metric dự đoán được bổ sung ở bước 2 bên dưới.

## Gọi preview từ luồng giải đấu

Đại lấy `version` từ TournamentDto và gửi JWT nhân viên có quyền `team.assign` đến
`POST /api/admin/tournaments/{id}/teams/preview`. Ví dụ body (thay version bằng giá trị thực):

~~~json
{"strategy":"Balanced","expectedVersion":"AAAAAAAAAAE="}
~~~

Chọn `ML` để xét 126 cách chia hai đội 5 người. Server lọc phương án theo vai trò,
chấm xác suất theo batch và ưu tiên xác suất gần 0,5 nhất, tiếp đến chênh rank trung bình,
sau đó thứ tự RegistrationId. `Balanced` hỗ trợ từ hai đội VALORANT 5 người trở lên.
Đặt `TeamBalancing__ModelDirectory` và `TeamBalancing__ManifestSha256` trong cấu hình
máy API rồi khởi động lại để sử dụng artifact đã được đối chiếu. Khi thiếu model đạt gate,
yêu cầu ML trả 503 `MODEL_UNAVAILABLE`. Người vận hành có thể chủ động chọn Balanced.

HTTP 200 trả `teams[].members[].registrationId`, tên khách, tier/rank OP.GG,
vai trò, `scoreSpread`, `quality`, `warnings`, nguồn/version model hoặc baseline,
`version`, `rosterVersion`, `proposalId`, `expiresAtUtc` và `planToken`.
Token trong cache một API instance có hiệu lực 10 phút và gắn với nhân viên, giải,
input và proposal. Mỗi lượt preview cấp token mới. Confirm thuộc luồng lưu đội riêng.

Server đọc roster nhất quán, kiểm tra phí/tài khoản/snapshot, suy luận ngoài transaction
và đọc lại để phát hiện version hoặc roster đã đổi. 400 báo request sai, 403 thiếu quyền,
409 báo lỗi version/roster/trạng thái, 422 báo lỗi stats hoặc vai trò.
Đọc `code` và `detail` trong ProblemDetails của các lỗi nghiệp vụ. Schema hiện tại chưa
lưu PlanSnapshot của đội đã chốt nên endpoint trả 409 `TeamsAlreadyFormed` cho trường hợp này.
Policy hoàn phí trong hash dùng mặc định contract (RegistrationEnd, 0%) theo schema hiện có.

Chạy HTTP checks với `dotnet run --project Backend/tests/CyberManagement.API.Checks -- --team-preview-checks`
và `--team-preview-ml-checks`. Mode thứ hai đăng ký scorer huấn luyện bằng dữ liệu kiểm thử
trong test host. Cấu hình `AUTH_CHECKS_SQL_CONNECTION` trên SQL Server thử để harness
tạo, kiểm tra rồi dọn database dùng riêng.

`ITeamBalancingService.Propose(TeamBalanceInput)` được đăng ký DI tại Application.
Input do server giải tạo từ toàn bộ roster SQL đủ điều kiện và snapshot tin cậy;
không nhận trực tiếp object này từ browser. `GameId` lấy SQL, `GameKey=valorant` do
server resolve; không gắn VALORANT với một ID cố định. Rowversion và roster hash khác nhau.

Baseline sắp tier giảm dần, hòa theo RegistrationId tăng dần, snake, giữ vai trò cụ thể;
chỉ Flex được lấp vai trò thiếu. Thiếu vai trò không tự đổi người/đổi thuật toán.
Output có metadata/hash xác định, `persisted=false`, không giả xác suất/model version.

Bộ kiểm tra riêng dùng dữ liệu **sinh để kiểm thử**, kể cả proof provider mô phỏng;
trainer chạy thật trên fixture để kiểm tra kỹ thuật, gắn `generated-fixture` và
`readyForInference=false`; không ghi fixture thành kết quả nghiệm thu ML.
API tests mặc định dùng InMemory; không thay thế kiểm chứng SQL/concurrency/provider thật.

## Huấn luyện và đánh giá — bước 2

Ghim [Microsoft.ML 5.0.1](https://www.nuget.org/packages/Microsoft.ML/5.0.1) tại Infrastructure;
Application/Domain không phụ thuộc ML.NET. Tool tham chiếu Infrastructure để tái sử dụng
cùng bộ lưu/tải và dự đoán; không huấn luyện trong request API.
Implementation dùng [SDCA Logistic Regression](https://learn.microsoft.com/en-us/dotnet/api/microsoft.ml.trainers.sdcalogisticregressionbinarytrainer)
và [ML.NET save/load](https://learn.microsoft.com/en-us/dotnet/machine-learning/how-to-guides/save-load-machine-learning-models-ml-net).

- Trước train, đọc một bản bất biến của sáu file prepared, kiểm tra hash và kiểm tra
  lại số mẫu/nhãn, nhóm/alias, ID, thời gian, vector và augment. Thay đổi schema của
  prepared manifest ở bước 2 yêu cầu chạy lại `prepare` trên nguồn raw đã đối chiếu.
- B1: xác suất hằng 0,5. B2: logistic chỉ giữ feature chênh lệch rank, chín chiều
  còn lại đặt zero trong cả fit/predict. M1: logistic dùng đủ 10 feature.
- Normalization chỉ fit từ train. B2/M1 đều thử L2 `{0.01,0.1,1}`, L1=0,
  tối đa 100 iterations, seed 42, một thread, không shuffle. Chọn theo validation
  Brier, hòa theo log loss rồi L2. Không gộp validation vào train sau lựa chọn.
- Mọi metric dùng xác suất đối xứng `(q(d)+1-q(-d))/2`, cùng cách suy luận.
  Lưu lựa chọn/hai model **trước** khi dự đoán test. `metrics.json` báo count/nhãn,
  accuracy/F1/AUC/Brier/log loss, 10 calibration bins và CI percentile 95% từ
  500 bootstrap theo competition group (seed 42). AUC thiếu lớp trả null;
  báo số resample đủ hai lớp. CI với ít nhóm có giới hạn, không chứng minh calibration ngoài nguồn.
- Chốt test một lần: tool tạo file `.evaluation-<split-hash>.json` bằng CreateNew
  trong thư mục prepared trước lượt dự đoán. Lượt tiếp theo cùng prepared bị từ chối.
  Không xóa marker/đổi tên model/copy lại cùng dữ liệu để tuyên bố một holdout mới;
  cần tập evaluation mới có nguồn, không trùng trận/nhóm. Marker chỉ bảo vệ một
  thư mục local, không phải registry chống dùng lại dataset giữa các máy.
- Prediction gate: AUC>=0,60; Brier<=0,24 và <=95% Brier của **cả** B1/B2;
  log loss không tệ hơn baseline tốt nhất. Kiểm tra đảo đội và save/load sai số<=1e-6.
  Không nới gate khi fixture hoặc model đầu tiên chưa đạt.
- B0 được đo trên toàn bộ roster test; case sai vai trò được báo riêng.
  M1 tính xác suất/imbalance surrogate trên B0 hợp lệ từ feature cùng preprocessing.
  Chưa có so sánh với optimizer 126 phân hoạch hoặc trận thật sau đề xuất ở bước này.

Artifact gồm `model.zip`, `rank-only-model.zip`, `selection.json`, `metrics.json`,
`split-manifest.json`, `model-manifest.json`, hash manifest và `artifact-hashes.json`.
Model zip giữ normalization + trọng số học; manifest giữ median và tên/thứ tự
feature, nguồn dataset/split, contract/policy, phạm vi game/platform/queue/scope/patch,
training cutoff, thời gian train, version model, hash, seed/options, runtime/OS/CPU
architecture và hash binary trainer/scorer. Training cutoff gồm cả thời điểm xác minh
nhãn train, không chỉ giờ bắt đầu trận. ModelVersion phải mới khi nội dung thay đổi.

`readyForInference` chỉ có thể true với `observed-matches` và các gate dự đoán/
đối xứng đạt; không thay thế nghiệm thu dữ liệu thật, ràng buộc tối ưu hoặc tích hợp API.
Artifact fixture luôn false dù metric cao. `unverified` bị từ chối ngay trước train.

`TeamBalanceModelArtifact.Load(directory, expectedManifestHash)` chỉ nhận cấu hình
server tin cậy, kiểm tra hash/version/gate/schema, từ chối fixture hoặc artifact thiếu/hỏng
bằng `MODEL_UNAVAILABLE`. Hash manifest cần được ghim ngoài artifact bởi người vận hành;
file hash đặt cùng artifact không tự chứng minh artifact đáng tin.
Scorer dùng batch IDataView riêng từng lượt, không dùng chung PredictionEngine mutable;
không tự đổi model hoặc cài vào DI production ở bước 2.

**Giới hạn hiện tại:** repo chưa có dữ liệu trận thật đủ gate. Model `.zip` sinh bởi
suite chỉ là artifact kiểm thử. Chưa có kết quả AUC/Brier thực tế để nghiệm thu,
chưa xác minh người chơi mới, map/agent/party, độ cân bằng thực tế hay benchmark
toàn luồng trên người chơi thực tế. Bộ preview checks đo p95 trên 100 lượt suy luận
sau khi load model với roster sinh và ghi các mẫu thời gian riêng với độ trễ HTTP/SQL.
