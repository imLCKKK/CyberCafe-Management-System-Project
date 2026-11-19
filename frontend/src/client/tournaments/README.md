# Client tournaments / OP.GG

Home → “Chi tiết & đăng ký” → `/client/tournaments`. Danh sách, giải được chọn và người tham gia tải từ API/SQL; thay đổi giải/đăng ký invalidates hai màn hình qua SignalR rồi client đọc lại API. Nhập tên trong game và tag ở hai ô riêng, chọn rank từ `GameRanks` và chọn vai trò. API lấy CustomerId từ JWT, không từ request body; Riot ID được lưu cùng đăng ký. Khách đăng ký VALORANT được tra cứu OP.GG một lần sau khi API xác nhận đăng ký. Khách có thể đồng bộ lại hồ sơ của mình. Nhân viên có quyền `tournament.manage` có thể làm mới từng người. Hai màn hình đọc snapshot từ SQL và hiển thị riêng hạng OP.GG, hạng tự khai, thời điểm lấy dữ liệu và thời điểm nguồn cập nhật.

## Chạy thử

Sửa lỗi ngày 22/10/2026: OP.GG chuyển đường dẫn một số bundle sang bản ghi chuỗi/import trong React Flight. Bộ đọc cũ chỉ tìm thẻ script HTML nên không tìm thấy entry point và trả 502 dù hồ sơ hợp lệ. Bộ đọc mới hỗ trợ cả hai dạng. Đã chạy đạt 60 bài model, 9 nhóm browser regression và bài `opgg-live` đi qua HTTP + giao diện thật. Artifact live của phiên này: `.tools/frontend-retest/2026-10-22T09-14-40-059Z/` (response JSON và ảnh admin/client).

Yêu cầu Node.js 22+ (đã thử trên Node 24). Chạy lại dev server nếu đang mở bản cũ:

```powershell
# Chạy trong thư mục frontend/
npm.cmd run dev
```

Chạy API .NET, SQL Server và adapter trên cùng máy với API bằng `node Backend/integrations/opgg/index.js` từ gốc repo (hoặc `npm.cmd run stats:server` trong frontend). Adapter lắng nghe loopback cổng 8787. Khi đổi cổng, đặt cùng giá trị cho `STATS_PORT` của Node và `Opgg__AdapterPort` của API. Schema cần migration `20261118094500_RegistrationStatsSnapshots`. Trang giải gọi API đăng ký đã xác thực. Endpoint tra cứu công khai của Vite vẫn dùng được để kiểm tra adapter riêng.

## Nguồn và cách lấy

[OP.GG cho biết nhìn chung không cấm crawling/scraping, yêu cầu ghi nguồn và không gây quá tải](https://help.op.gg/hc/en-us/articles/31091405109401-Can-I-use-OP-GG-data). Đây là adapter thử nghiệm đọc trang công khai, **không phải API được OP.GG cam kết hỗ trợ**; cấu trúc trang và server action có thể đổi.

Backend tải trang hồ sơ PC, đọc JSON React Flight, bao gồm các module import và chuỗi đường dẫn chunk được stream riêng (không chỉ thẻ script HTML), để xác định đúng Riot ID, trạng thái PUBLIC và mùa Competitive đang được chọn. Sau đó tìm đúng action chỉ đọc `getPlayerStatistics` trong bundle công khai mà trang sử dụng và gọi action đó. ID action được phát hiện theo asset của trang, không hard-code. Không thực thi JavaScript tải về; không gọi cập nhật hồ sơ, sửa quyền riêng tư, đăng nhập hoặc vượt kiểm soát truy cập. Chỉ gửi request tới origin OP.GG cố định và CDN asset cho phép; không nhận URL đích từ client và không theo redirect.

Cache thành công 10 phút, lỗi 30 giây, tối đa 200 hồ sơ; gộp các yêu cầu đang xử lý cùng Riot ID. Tối đa 2 hồ sơ xử lý đồng thời và 30 hồ sơ mới/phút cho mỗi instance. Tối đa 12 bundle để tìm action, mỗi response tối đa 4 MiB. Timeout toàn bộ lần đọc 25 giây, mỗi request 10 giây; API .NET chờ adapter tối đa 28 giây mỗi hồ sơ. Không tự động quét toàn bộ giải.

Nếu gặp hồ sơ riêng tư, OP.GG từ chối, 429, timeout hoặc cấu trúc thay đổi, UI báo lỗi riêng từng dòng và không bịa số liệu. Nếu đã có snapshot, hai màn hình tiếp tục đọc bản được lưu thành công gần nhất. Đăng ký và thu phí đã hoàn tất độc lập trước lookup.

## Chỉ số và độ mới

Bốn chỉ số cùng lấy từ `playerStatistics` của **Competitive, PC, mùa hiện tại được OP.GG chọn**:

- Win rate = wins / gameCount × 100; gameCount bao gồm hòa.
- Headshot = headShots / (headShots + bodyShots + legShots) × 100.
- Damage/round = damage / rounds.
- KDA = (kills + assists) / deaths, không dùng K/D thay thế.

Làm tròn 2 chữ số thập phân. Thiếu trường hoặc mẫu số bằng 0 trả `null` và hiển thị `—`. Không có trận trả bốn chỉ số null. Rank lấy từ `profile.competitiveTier`, đối chiếu metadata `tiers` của OP.GG để có tên và bậc (ví dụ Gold 3), không suy ra từ KDA hay người dùng khai báo. `RankId` lưu hạng người đăng ký khai báo. Snapshot lưu riêng toàn bộ `rank` của OP.GG. Nếu chưa có thống kê nhưng có rank, vẫn trả rank, `matches` và các chỉ số là null. Hạng Unrank do nguồn trả về được giữ riêng với trường hợp chưa lấy được rank.

`fetchedAt` là lúc backend lấy thống kê, `sourceUpdatedAt` là `lastUpdatedAt` trên hồ sơ OP.GG. Hai thời điểm hiển thị riêng; nút đồng bộ không buộc OP.GG cập nhật trận mới. Không cam kết số liệu realtime hoặc bao gồm trận chưa được OP.GG ghi nhận.

Đã thử trực tiếp ngày 22/10/2026 với hồ sơ công khai [Blue#00h](https://op.gg/valorant/profile/Blue-00h): Gold 3, V26 - ACT6, 1 trận, Win rate 0%, HS 19,23%, ADR 141,29, KDA 0,71; OP.GG ghi cập nhật hồ sơ ngày 21/10/2026. Đây là kết quả kiểm tra tại thời điểm đó, **không phải dữ liệu seed** trong danh sách đăng ký.

## Backend contract

Endpoint nhận Riot ID thay vì ID đăng ký demo. Response:

```json
{
  "riotId": "Blue#00h",
  "source": "opgg",
  "queue": "competitive",
  "scope": "V26 - ACT6 · Competitive",
  "fetchedAt": "2026-10-22T09:14:42.195Z",
  "sourceUpdatedAt": "2026-10-21T03:53:37+00:00",
  "rank": { "tier": 14, "name": "Gold", "division": 3, "label": "Gold 3" },
  "matches": 1,
  "winRate": 0,
  "headshotRate": 19.23,
  "damagePerRound": 141.29,
  "kda": 0.71
}
```

HTTP 400: Riot ID sai; 403: riêng tư/từ chối; 404: không có hồ sơ/thống kê; 409: Riot ID nguồn không khớp; 429: giới hạn; 502: lỗi upstream/cấu trúc/timeout. Body lỗi là `{ "message": "..." }`.

Backend đọc hồ sơ công khai theo Riot ID, chưa có login/RSO và không xác minh người đăng ký sở hữu hồ sơ. Rank đăng ký hiện là lựa chọn từ `GameRanks` để đáp ứng schema hiện có; không đại diện cho rank OP.GG đã xác minh. Thống kê/đội không dùng dữ liệu demo trong hai màn hình giải đấu. SQL lưu các bản thống kê bất biến theo đăng ký. Contract snapshot và provenance cho ML nằm trong tài liệu adapter.

## Kiểm tra

```powershell
# Chạy trong thư mục frontend/
npm.cmd run test:opgg
node --test --test-isolation=none src/client/home/homeModel.test.js
node src/client/tournaments/render-smoke.mjs
npm.cmd run build

# Kiểm tra form hai trường, lưu rank, lỗi provider và thử lại (fixture):
npm.cmd run test:frontend -- opgg

# Kiểm tra HTTP với OP.GG thật (cần mạng):
npm.cmd run test:frontend -- opgg-live
# Đặt OPGG_RIOT_ID để thử hồ sơ khác, ví dụ Player#VN1.
```

Các bài model và suite `opgg` dùng fixture cô lập. Suite `opgg-live` gọi OP.GG thật qua endpoint Vite. Luồng API đăng ký và SQL Server sử dụng bộ `--opgg-live` trong tài liệu adapter. Để thử HTTP với hồ sơ thật khi dev server chạy:

```powershell
Invoke-RestMethod 'http://localhost:5173/api/valorant/opgg/stats?riotId=Blue%2300h'
```
