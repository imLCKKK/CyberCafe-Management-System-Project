# Client tournaments / OP.GG

Home → “Chi tiết & đăng ký” → `/client/tournaments?tournament=1`. Nhập Riot ID `Tên#TAG`, hạng khai báo và vai trò. Trang tự tải thống kê thực cho các dòng đang xem khi mở trang hoặc đăng ký; bấm **Đồng bộ OP.GG** để cập nhật/thử lại. Lỗi không tự lặp request. Liên kết **Mở OP.GG** mở hồ sơ nguồn. Danh sách giải/đăng ký vẫn dùng localStorage chung với Home, chưa có database hay xác minh quyền sở hữu Riot ID.

## Chạy thử

Yêu cầu Node.js 22+ (đã thử trên Node 24). Chạy lại dev server nếu đang mở bản cũ:

```powershell
npm.cmd run dev
```

Vite tự chạy middleware thống kê tại cùng origin: `GET /api/valorant/opgg/stats?riotId=Blue%2300h`. Không cần API key hay tự cấu hình `.env`. Mặc định client gọi `/api`; nếu đã đặt `VITE_VALORANT_API_BASE_URL` từ trước, bỏ cấu hình cũ hoặc đặt về `/api`.

Mở đúng URL mà Vite in ra, không dùng Live Server/static preview để thử đồng bộ. Nếu `/api` trả HTML/404 từ static server, UI báo thiếu backend thay vì báo nhầm không tìm thấy hồ sơ OP.GG. Đã kiểm tra thêm `dylannx#1603`: endpoint trả 37 trận, Win rate 48,65%, HS 27,08%, ADR 122,36, KDA 1,10 ngày 09/10/2026.

`npm.cmd run preview` cũng có middleware. Khi host bản build bằng static server khác, cần chạy `npm.cmd run stats:server` và reverse proxy `/api/valorant/opgg/stats` đến `http://127.0.0.1:8787` (đổi cổng bằng `STATS_PORT`). Chỉ upload `dist` lên static hosting sẽ không chạy được backend.

## Nguồn và cách lấy

[OP.GG cho biết nhìn chung không cấm crawling/scraping, yêu cầu ghi nguồn và không gây quá tải](https://help.op.gg/hc/en-us/articles/31091405109401-Can-I-use-OP-GG-data). Đây là adapter thử nghiệm đọc trang công khai, **không phải API được OP.GG cam kết hỗ trợ**; cấu trúc trang và server action có thể đổi.

Backend tải trang hồ sơ PC, đọc JSON React Flight để xác định đúng Riot ID, trạng thái PUBLIC và mùa Competitive đang được chọn. Sau đó tìm đúng action chỉ đọc `getPlayerStatistics` trong bundle công khai mà trang sử dụng và gọi action đó. ID action được phát hiện theo asset của trang, không hard-code. Không thực thi JavaScript tải về; không gọi cập nhật hồ sơ, sửa quyền riêng tư, đăng nhập hoặc vượt kiểm soát truy cập. Chỉ gửi request tới origin OP.GG cố định và CDN asset cho phép; không nhận URL đích từ client và không theo redirect.

Cache thành công 10 phút, lỗi 30 giây, tối đa 200 hồ sơ; gộp các yêu cầu đang xử lý cùng Riot ID. Tối đa 2 hồ sơ xử lý đồng thời và 30 hồ sơ mới/phút cho mỗi instance. Tối đa 12 bundle để tìm action, mỗi response tối đa 4 MiB. Timeout toàn bộ lần đọc 25 giây, mỗi request 10 giây; client chờ tối đa 30 giây/hồ sơ. Đồng bộ tối đa 10 dòng trên trang đang xem, tuần tự. Không tự động quét toàn bộ giải.

Nếu gặp hồ sơ riêng tư, OP.GG từ chối, 429, timeout hoặc cấu trúc thay đổi, UI báo lỗi riêng từng dòng và không bịa số liệu. Bản thống kê đã tải trước đó được giữ kèm lỗi và thời điểm lấy. Thống kê client chỉ ở bộ nhớ, cần đồng bộ lại khi rời trang/quay lại (backend có cache).

## Chỉ số và độ mới

Bốn chỉ số cùng lấy từ `playerStatistics` của **Competitive, PC, mùa hiện tại được OP.GG chọn**:

- Win rate = wins / gameCount × 100; gameCount bao gồm hòa.
- Headshot = headShots / (headShots + bodyShots + legShots) × 100.
- Damage/round = damage / rounds.
- KDA = (kills + assists) / deaths, không dùng K/D thay thế.

Làm tròn 2 chữ số thập phân. Thiếu trường hoặc mẫu số bằng 0 trả `null` và hiển thị `—`. Không có trận trả bốn chỉ số null. Hạng trong bảng vẫn là **hạng khai báo** khi đăng ký.

`fetchedAt` là lúc backend lấy thống kê, `sourceUpdatedAt` là `lastUpdatedAt` trên hồ sơ OP.GG. Hai thời điểm hiển thị riêng; nút đồng bộ không buộc OP.GG cập nhật trận mới. Không cam kết số liệu realtime hoặc bao gồm trận chưa được OP.GG ghi nhận.

Đã thử trực tiếp ngày 09/10/2026 với hồ sơ công khai [Blue#00h](https://op.gg/valorant/profile/Blue-00h): V26 - ACT5, 48 trận, Win rate 52,08%, HS 24,53%, ADR 164,19, KDA 1,57; OP.GG ghi cập nhật hồ sơ ngày 04/10/2026. Đây là kết quả kiểm tra tại thời điểm đó, **không phải dữ liệu seed** trong danh sách đăng ký.

## Backend contract

Endpoint nhận Riot ID thay vì ID đăng ký demo. Response:

```json
{
  "riotId": "Blue#00h",
  "source": "opgg",
  "queue": "competitive",
  "scope": "V26 - ACT5 · Competitive",
  "fetchedAt": "2026-10-09T07:07:14.667Z",
  "sourceUpdatedAt": "2026-10-04T10:55:31+00:00",
  "matches": 48,
  "winRate": 52.08,
  "headshotRate": 24.53,
  "damagePerRound": 164.19,
  "kda": 1.57
}
```

HTTP 400: Riot ID sai; 403: riêng tư/từ chối; 404: không có hồ sơ/thống kê; 409: Riot ID nguồn không khớp; 429: giới hạn; 502: lỗi upstream/cấu trúc/timeout. Body lỗi là `{ "message": "..." }`.

Backend hiện chỉ đọc hồ sơ công khai theo Riot ID, chưa có login/RSO và không xác minh người đăng ký sở hữu hồ sơ. Khi làm hệ thống thật cần xác thực đăng ký, lưu database, kiểm soát quyền và giới hạn truy vấn tại cổng vào; không coi dữ liệu localStorage hoặc rank khai báo là đáng tin cậy. Chưa chạy migration. DBML cần thêm hồ sơ game/Riot ID và snapshot nullable của HS%, ADR, KDA, scope, nguồn, thời điểm lấy/cập nhật.

## Kiểm tra

```powershell
npm.cmd run test:opgg
node --test --test-isolation=none web/src/client/home/homeModel.test.js
node web/src/client/tournaments/render-smoke.mjs
npm.cmd run build
```

Tests dùng fixture cô lập, không gửi truy vấn lên OP.GG. Để thử HTTP với hồ sơ thật khi dev server chạy:

```powershell
Invoke-RestMethod 'http://localhost:5173/api/valorant/opgg/stats?riotId=Blue%2300h'
```
