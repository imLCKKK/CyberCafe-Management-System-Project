# Client tournaments / OP.GG

Home → “Chi tiết & đăng ký” → `/client/tournaments?tournament=1`. Nhập tên trong game và tag ở hai ô riêng, có dấu `#` cố định ở giữa, rồi chọn vai trò. Tag nhận cả chữ và số. Form nhắc chuyển hồ sơ OP.GG từ Private sang Public để truy vấn thông tin. Rank tự lấy từ OP.GG; không có ô chọn rank. Trang tự tải thống kê thực cho các dòng đang xem khi mở trang hoặc đăng ký; bấm **Đồng bộ OP.GG** để cập nhật/thử lại. Lỗi không tự lặp request. Liên kết **Mở OP.GG** mở hồ sơ nguồn. Danh sách giải/đăng ký vẫn dùng localStorage chung với Home, chưa có database hay xác minh quyền sở hữu Riot ID.

## Chạy thử

Sửa lỗi ngày 22/10/2026: OP.GG chuyển đường dẫn một số bundle sang bản ghi chuỗi/import trong React Flight. Bộ đọc cũ chỉ tìm thẻ script HTML nên không tìm thấy entry point và trả 502 dù hồ sơ hợp lệ. Bộ đọc mới hỗ trợ cả hai dạng. Đã chạy đạt 60 bài model, 9 nhóm browser regression và bài `opgg-live` đi qua HTTP + giao diện thật. Artifact live của phiên này: `.tools/frontend-retest/2026-10-22T09-14-40-059Z/` (response JSON và ảnh admin/client).

Yêu cầu Node.js 22+ (đã thử trên Node 24). Chạy lại dev server nếu đang mở bản cũ:

```powershell
npm.cmd run dev
```

Vite tự chạy middleware thống kê tại cùng origin: `GET /api/valorant/opgg/stats?riotId=Blue%2300h`. Không cần API key hay tự cấu hình `.env`. Mặc định client gọi `/api`; nếu đã đặt `VITE_VALORANT_API_BASE_URL` từ trước, bỏ cấu hình cũ hoặc đặt về `/api`.

Mở đúng URL mà Vite in ra, không dùng Live Server/static preview để thử đồng bộ. Nếu `/api` trả HTML/404 từ static server, UI báo thiếu backend thay vì báo nhầm không tìm thấy hồ sơ OP.GG. Ngày 22/10/2026 đã kiểm tra trực tiếp `dylannx#1603`: Platinum 2, 4 trận ở V26 - ACT6, win rate 100%, HS 30,85%, ADR 162,65, KDA 1,65.

`npm.cmd run preview` cũng có middleware. Khi host bản build bằng static server khác, cần chạy `npm.cmd run stats:server` và reverse proxy `/api/valorant/opgg/stats` đến `http://127.0.0.1:8787` (đổi cổng bằng `STATS_PORT`). Chỉ upload `dist` lên static hosting sẽ không chạy được backend.

## Nguồn và cách lấy

[OP.GG cho biết nhìn chung không cấm crawling/scraping, yêu cầu ghi nguồn và không gây quá tải](https://help.op.gg/hc/en-us/articles/31091405109401-Can-I-use-OP-GG-data). Đây là adapter thử nghiệm đọc trang công khai, **không phải API được OP.GG cam kết hỗ trợ**; cấu trúc trang và server action có thể đổi.

Backend tải trang hồ sơ PC, đọc JSON React Flight, bao gồm các module import và chuỗi đường dẫn chunk được stream riêng (không chỉ thẻ script HTML), để xác định đúng Riot ID, trạng thái PUBLIC và mùa Competitive đang được chọn. Sau đó tìm đúng action chỉ đọc `getPlayerStatistics` trong bundle công khai mà trang sử dụng và gọi action đó. ID action được phát hiện theo asset của trang, không hard-code. Không thực thi JavaScript tải về; không gọi cập nhật hồ sơ, sửa quyền riêng tư, đăng nhập hoặc vượt kiểm soát truy cập. Chỉ gửi request tới origin OP.GG cố định và CDN asset cho phép; không nhận URL đích từ client và không theo redirect.

Cache thành công 10 phút, lỗi 30 giây, tối đa 200 hồ sơ; gộp các yêu cầu đang xử lý cùng Riot ID. Tối đa 2 hồ sơ xử lý đồng thời và 30 hồ sơ mới/phút cho mỗi instance. Tối đa 12 bundle để tìm action, mỗi response tối đa 4 MiB. Timeout toàn bộ lần đọc 25 giây, mỗi request 10 giây; client chờ tối đa 30 giây/hồ sơ. Đồng bộ tối đa 10 dòng trên trang đang xem, tuần tự. Không tự động quét toàn bộ giải.

Nếu gặp hồ sơ riêng tư, OP.GG từ chối, 429, timeout hoặc cấu trúc thay đổi, UI báo lỗi riêng từng dòng và không bịa số liệu. Bản thống kê đã tải trước đó được giữ kèm lỗi và thời điểm lấy. Snapshot rank và thống kê được lưu trong `OpggSnapshot` của đăng ký, dùng chung giữa admin/client và giữ qua tải lại. Tự làm mới snapshot quá 10 phút khi mở trang, tối đa một lần tự động cho mỗi dòng trong một lần mở trang; lỗi cần bấm thử lại.

## Chỉ số và độ mới

Bốn chỉ số cùng lấy từ `playerStatistics` của **Competitive, PC, mùa hiện tại được OP.GG chọn**:

- Win rate = wins / gameCount × 100; gameCount bao gồm hòa.
- Headshot = headShots / (headShots + bodyShots + legShots) × 100.
- Damage/round = damage / rounds.
- KDA = (kills + assists) / deaths, không dùng K/D thay thế.

Làm tròn 2 chữ số thập phân. Thiếu trường hoặc mẫu số bằng 0 trả `null` và hiển thị `—`. Không có trận trả bốn chỉ số null. Rank lấy từ `profile.competitiveTier`, đối chiếu metadata `tiers` của OP.GG để có tên và bậc (ví dụ Gold 3), không suy ra từ KDA hay người dùng khai báo. `RankId` ban đầu là null; sau đồng bộ sẽ ánh xạ tên hạng vào `GameRanks`. Nếu chưa có thống kê nhưng có rank, vẫn trả rank, `matches` và các chỉ số là null. Hạng Unrank do nguồn trả về được giữ riêng với trường hợp chưa lấy được rank.

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

Backend hiện chỉ đọc hồ sơ công khai theo Riot ID, chưa có login/RSO và không xác minh người đăng ký sở hữu hồ sơ. Khi làm hệ thống thật cần xác thực đăng ký, lưu database, kiểm soát quyền và giới hạn truy vấn tại cổng vào; không coi dữ liệu localStorage là dữ liệu đã xác thực. Chia đội trong demo chỉ chạy khi mọi người đã có rank OP.GG hợp lệ; không dùng rank khai báo cũ hoặc mặc định 0 cho người chưa đồng bộ. Chưa chạy migration. DBML cần thêm hồ sơ game/Riot ID và snapshot nullable của HS%, ADR, KDA, scope, nguồn, thời điểm lấy/cập nhật.

## Kiểm tra

```powershell
npm.cmd run test:opgg
node --test --test-isolation=none web/src/client/home/homeModel.test.js
node web/src/client/tournaments/render-smoke.mjs
npm.cmd run build

# Kiểm tra form hai trường, lưu rank, lỗi provider và thử lại (fixture):
npm.cmd run test:frontend -- opgg

# Kiểm tra HTTP và giao diện với OP.GG thật (cần mạng):
npm.cmd run test:frontend -- opgg-live
# Đặt OPGG_RIOT_ID để thử hồ sơ khác, ví dụ Player#VN1.
```

Các bài model và suite `opgg` dùng fixture cô lập. Suite `opgg-live` gọi OP.GG thật qua endpoint Vite và kiểm tra cả giao diện client lẫn admin. Để thử HTTP với hồ sơ thật khi dev server chạy:

```powershell
Invoke-RestMethod 'http://localhost:5173/api/valorant/opgg/stats?riotId=Blue%2300h'
```
