# Bản đồ hệ thống

## App hiện tại chạy thế nào?

```text
scripts/start.cmd / npm --prefix frontend run dev
  → Vite (cấu hình trong frontend/)
  → frontend/index.html
  → frontend/src/main.jsx
  → frontend/src/app/routes.jsx
      /admin/*  → AdminLayout → các màn hình admin
      /client/* → ClientLayout → các màn hình khách

Vite /api/valorant/opgg/stats → backend/integrations/opgg → nguồn OP.GG

backend/CyberCafe.Api → /health và schema EF/SQL Server (chưa cấp dữ liệu nghiệp vụ cho UI)
```

Frontend hiện có 16 màn hình demo có xử lý và 3 route placeholder: đăng nhập admin, đăng nhập client và khuyến mãi. Dữ liệu nghiệp vụ chủ yếu ở localStorage. API .NET hiện mới có health check/schema; SignalR và ZaloPay sandbox là công việc trong [kế hoạch sprint](../planning/team-delivery-plan.md), chưa được triển khai bởi đợt dọn thư mục.

## Tìm code ở đâu?

| Nhu cầu | Vị trí |
| --- | --- |
| Route, layout, theme | `frontend/src/app/` |
| Màn hình quản trị | `frontend/src/admin/<chức năng>/` |
| Màn hình khách hàng | `frontend/src/client/<chức năng>/` |
| UI chung theo khu vực | `admin/shared/`, `client/shared/` |
| UI dùng cho cả admin/client | `frontend/src/shared/` |
| Dữ liệu/logic demo dùng cho nhiều màn hình | Xem bảng kho dưới đây; đây là phần cần chuyển sang API |
| Backend nghiệp vụ .NET | `backend/CyberCafe.Api/` |
| Adapter và helper OP.GG | `backend/integrations/opgg/` |
| Browser test | `frontend/tests/` |
| Model test | Các file `*.test.js` cạnh model |

`backend/integrations/opgg/shared.js` chỉ chứa xử lý Riot ID/URL dùng được ở cả browser và Node. Tên `shared` trong thư mục này không đại diện cho nghiệp vụ chung toàn hệ thống.

## Ba kho demo đang tồn tại

| Khóa localStorage | Nơi quản lý | Nơi dùng |
| --- | --- | --- |
| `tram.client.dbml.demo.v2` | `client/shared/ClientDemoContext.jsx`, `demoData.js`, `demoModel.js` | Tài khoản, ví, đơn, phiên mẫu; cả admin/client |
| `tram.client.home.demo.v1` | `client/home/useHomeData.js`, `homeModel.js`, `mockData.js` | Giải đấu và dữ liệu mẫu cũ |
| `net-cafe.admin.computers.v1` | `admin/computers/computerStore.js` | Sơ đồ máy admin, dashboard/báo cáo |

`AdminContext` hiện bọc `ClientDemoProvider` và lấy thêm `useHomeData`. Đây là sự phụ thuộc tạm của demo. Đợt dọn nền giữ nguyên khóa và dữ liệu đã lưu; việc chuyển nguồn và thống nhất schema phải được làm theo nghiệp vụ, không đổi tên thư mục rồi coi là xong.

Sự kiện `storage` chỉ đồng bộ giữa các tab cùng trình duyệt/origin; không đáp ứng yêu cầu hai máy. Khi triển khai, mọi máy phải dùng cùng API/SQL Server.

## Đích triển khai đã chốt

```text
Máy A: React admin/staff ─┐
                          ├─ ASP.NET Core API ─ EF Core ─ SQL Server
Máy B: React client ──────┘          │
                               commit dữ liệu
                                    ↓
                               SignalR Hub
                                    ↓
                       thông báo đúng tài khoản/vai trò
                                    ↓
                         UI đọc lại dữ liệu từ API

API .NET ↔ ZaloPay sandbox
              ↓ callback đã xác minh → transaction tiền/ví → SignalR
```

- Hai máy mở web trên LAN; không điều khiển hay khóa Windows máy trạm.
- API .NET là nơi kiểm tra quyền, giá, số dư, trạng thái, giảm giá và cập nhật SQL.
- REST xử lý lệnh nghiệp vụ; SignalR báo thay đổi sau khi transaction thành công. Client không tự cộng/trừ ví theo event.
- Hub xác định nhóm từ tài khoản/quyền server; không nhận tên nhóm tùy ý để xem ví/hóa đơn người khác.
- Khi reconnect hoặc quay lại màn hình, đọc lại trạng thái chuẩn. Dữ liệu SQL là nguồn chính; thông báo realtime có thể bị bỏ lỡ.
- Mục tiêu demo: thay đổi được phản ánh ở máy kia trong tối đa 2 giây khi LAN ổn định; phải đo khi kiểm thử, không phải bảo đảm của thư viện.
- ZaloPay dùng API sandbox chính thức. Callback cần URL mà ZaloPay truy cập được; LAN riêng cần một đường HTTPS công khai phù hợp. Thông tin tích hợp nằm ở server. Redirect của browser không phải bằng chứng đã trả tiền.

Tài liệu tham chiếu: [SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction?view=aspnetcore-10.0), [reconnect](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-10.0), [ZaloPay callback](https://docs.zalopay.vn/docs/developer-tools/knowledge-base/callback/).

## Quy tắc chuyển tiếp

ML chia đội giải đấu là yêu cầu bổ sung, **Phúc phụ trách dữ liệu/model/suy luận và đề xuất đội; Đại tích hợp vào luồng giải đấu, xác nhận và lưu đội**. Luồng đích: danh sách đăng ký hợp lệ → ML đề xuất đội → người quản trị xác nhận → API lưu SQL → SignalR thông báo. Model, nguồn dữ liệu, cách đánh giá và môi trường chạy được chốt trong S1; chưa tạo thêm service hay thư mục ML rỗng. Xem phân công và tiêu chí trong [kế hoạch sprint](../planning/team-delivery-plan.md).

1. Giữ UI và các bài kiểm tra hữu ích. Thêm lớp gọi API, rồi chuyển từng nhóm màn hình liên quan sang cùng một nguồn dữ liệu.
2. Đánh dấu rõ chức năng đang dùng demo hay API. API lỗi phải hiện lỗi; không tự trả dữ liệu localStorage thay thế.
3. Không tạo thêm backend theo từng người hoặc theo admin/client. Mỗi module đóng góp entity, service, endpoint và test vào API chung.
4. Chốt schema thống nhất giữa code demo, DBML và các nhánh cũ; chọn lọc code phù hợp. EF migration, DBML và SQL xuất phải được ghi rõ vai trò.
5. Chỉ gom/di chuyển model demo khi module đã có ranh giới dữ liệu rõ. Tách PR dọn đường dẫn khỏi PR đổi logic tiền/quyền.
6. Frontend tự chứa package/config/assets/tests trong `frontend/`. Backend .NET, tích hợp OP.GG và schema/SQL nằm trong `backend/`. Script Windows nằm ở `scripts/`; không chứa nghiệp vụ.

## Đợt dọn nền này

- Tham khảo cách chia ranh giới của BrewLite: mỗi frontend/backend tự chứa cấu hình và lệnh chạy, không đổi stack sang Next.js/NestJS.
- Chuyển `web/` thành `frontend/`, đưa package/Vite/HTML/public và browser test về cùng chỗ; chuyển `api/` và schema/SQL vào `backend/`.
- Chuyển các file `.cmd` vào `scripts/`, hướng dẫn nhóm vào `planning/team-workflow.md`.
- Chuyển `server/` và `shared/opgg.js` vào `backend/integrations/opgg/`, cập nhật config và script.
- Giữ việc xóa `cybernet-html/` theo yêu cầu người dùng; bản cũ còn trong lịch sử Git. Làm rõ frontend chính và trạng thái API trong tài liệu.
- Dọn mock rỗng không được dùng; đưa dialog dùng chung ra khỏi module Home.
- Wrapper chạy được với công cụ cài trên máy; chọn SDK .NET 10 ổn định bằng `global.json`.

### Kiểm tra sau khi di chuyển — 30/10/2026

- Build frontend và API .NET: đạt; API build không có warning/error.
- Model test: 60/60 đạt. Ba kiểm tra render admin/Home/giải đấu đạt; còn cảnh báo SSR `useLayoutEffect` đã có trước đợt dọn.
- Browser: 8 nhóm nghiệp vụ đạt trong lượt chạy đầy đủ. Nhóm route bị timeout sau năm màn hình, chạy lại riêng đạt 80 kiểm tra ở năm kích thước màn hình và 4 redirect; không sửa code để bỏ qua lỗi. Nguyên nhân timeout ban đầu chưa xác định, chưa tái hiện ở lượt chạy lại.
- Log nằm trong `.tools/foundation-checks/`: `frontend-final-build.log`, `dotnet-final-build.log`, `models-final.log`, `render-final.log`, `browser-final.log`, `routes-final.log`. Đây là artifact cục bộ, không đưa lên Git.

Các kết quả trên kiểm tra việc giữ hành vi demo sau khi di chuyển. Chưa chạy migration trên SQL Server, nghiệm thu hai máy SignalR hay gọi ZaloPay sandbox; các phần này cần triển khai và kiểm tra theo sprint. Bộ browser mặc định dùng fixture OP.GG, không chứng minh provider bên ngoài đang hoạt động.
