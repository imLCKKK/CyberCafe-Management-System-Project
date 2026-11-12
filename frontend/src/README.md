# Bản đồ mã nguồn frontend

Ứng dụng React hiện chạy ở chế độ demo trình duyệt. Backend ASP.NET Core và
SQL Server chưa cấp dữ liệu nghiệp vụ cho các màn hình này. OP.GG là một
integration riêng để đọc thống kê game, không phải API quản lý phòng máy.

## Nhận diện NEXORA CYBER — S1-U-1

Logo gốc do Trưởng nhóm cung cấp ngày 09/11/2026 nằm tại
[`public/branding/nexora-cyber.png`](../public/branding/nexora-cyber.png).
[`nexora-mark.svg`](../public/branding/nexora-mark.svg) là bản biểu tượng N
dựng theo mẫu gốc để hiển thị rõ trên favicon và các vùng nhỏ; PNG gốc được
giữ nguyên và hiển thị trên trang đăng nhập desktop.

Admin, client và auth dùng chung [`Brand`](shared/branding/Brand.jsx):
`<Brand area="Quản trị"/>`, `<Brand area="Khách hàng"/>`; `compact` chỉ ẩn chữ
về thị giác, vẫn giữ tên thương hiệu cho công cụ hỗ trợ. Nhãn khu vực tách
khỏi tên NEXORA CYBER.

Token chung ở [`app/theme.css`](app/theme.css): `--bg` đen `#0B0F14`,
`--accent` cyan `#22D3EE`, `--purple` tím `#A855F7`, `--text` sáng `#E5F0FF`;
`--panel`, `--panel2`, `--muted`, `--line` dùng cho bề mặt/chữ/viền. Màu
`--success`, `--warning`, `--danger`, `--info` dành cho trạng thái nghiệp vụ.
CSS module dùng token này; layout sidebar/header do `app/layout.css` và
`app/client.css` quản lý. Giữ các khóa dữ liệu demo bên dưới khi đổi thương hiệu.

Dialog admin/client dùng `shared/dialogFocus.js` để vòng Tab/Shift+Tab nằm
trong modal; Escape và trả focus về nút mở vẫn dùng lifecycle của dialog.
Bảng dài có vùng cuộn được đặt tên và nhận focus để cuộn bằng phím mũi tên.

Chạy `npm.cmd --prefix frontend run test:frontend -- branding` từ repo root
để kiểm tra nhận diện, kích thước 1440/1024/390/320px, điều hướng bàn phím,
dialog và cuộn bảng bằng phím. Suite dùng HTTP fixture cho phần trình bày;
không thay thế kiểm chứng SQL, realtime hay nghiệp vụ backend.

## Đi từ đâu?

`main.jsx` → `app/routes.jsx` → `AdminLayout` hoặc `ClientLayout` → trang của
từng chức năng. Router là nơi kiểm tra màn hình nào thực sự được sử dụng;
`/admin/auth`, `/client/auth` và `/admin/promotions` hiện vẫn là placeholder.

| Thư mục | Vai trò hiện tại |
| --- | --- |
| `app/` | Router, layout, theme và CSS khung ứng dụng |
| `admin/<chức năng>/` | Màn hình quản trị theo nghiệp vụ: máy, khách, món, kho, hóa đơn, giải đấu, nhân viên, báo cáo |
| `client/<chức năng>/` | Màn hình người chơi: trang chủ, gọi món, ví, hóa đơn, tài khoản, giải đấu |
| `admin/shared/` | UI/CSS quản trị dùng chung; hiện còn chứa provider, seed và model demo của nhiều nghiệp vụ |
| `client/shared/` | UI dùng chung như `Dialog.jsx`, provider, seed và model demo dùng bởi cả client và phần lớn admin |
| `shared/` | UI dùng chung cho cả admin và client, hiện có trường nhập Riot ID |

Component chỉ dùng trong một chức năng đặt cạnh trang đó. UI dùng ở nhiều
chức năng cùng phía đặt trong `admin/shared/` hoặc `client/shared/`. Không tạo
`mockData.js` rỗng để đánh dấu phân công; chỉ thêm file khi có dữ liệu hoặc code
được sử dụng. Test model nằm cạnh model; bài kiểm tra luồng trình duyệt nằm ở
`../tests/`.

## Ba nguồn dữ liệu demo đang tồn tại

| Nguồn | Khóa localStorage | Nơi dùng |
| --- | --- | --- |
| `client/shared/ClientDemoContext.jsx` | `tram.client.dbml.demo.v2` | Tài khoản, ví, sản phẩm, kho, đơn và phiên; phần lớn admin/client dùng chung |
| `client/home/useHomeData.js` | `tram.client.home.demo.v1` | Dữ liệu giải đấu cũ, được Home và giải đấu admin/client đọc |
| `admin/computers/computerStore.js` | `net-cafe.admin.computers.v1` | Sơ đồ/trạng thái máy; trang máy, dashboard và báo cáo |

`AdminContext` hiện bọc `ClientDemoProvider` và dùng thêm `useHomeData`.
Đây là phụ thuộc còn lại của bản demo, cần đọc rõ trước khi sửa nghiệp vụ.
`home/useHomeData` không còn là nguồn dữ liệu chính của trang chủ. Sự kiện
`storage` chỉ đồng bộ các tab cùng origin trong cùng trình duyệt, không đồng
bộ hai máy khác nhau.

`admin/computers/components/CafeUI.jsx` hiện chứa cả UI dùng chung và UI riêng
cho máy, còn phụ thuộc nhãn trong mock máy. Việc tách component đó cần làm
riêng; CSS nền dùng chung đã nằm trong `admin/shared/cafe.css`. Dialog client
dùng chung nằm trong `client/shared/Dialog.jsx`, giữ nguyên CSS `home-*` đang
được layout nạp để không đổi giao diện.

## Ranh giới khi nối API

Phần này là hướng triển khai, chưa phải cấu trúc API đã hoàn thành. Thêm
service/adapter theo từng nghiệp vụ để trang gọi API hoặc demo một cách rõ
ràng. Không tự quay lại dữ liệu demo khi API lỗi. Khi nghiệp vụ chuyển sang
API, quyền, giá, tồn kho, số dư và giao dịch do server kiểm tra và ghi nhận;
frontend gửi yêu cầu thao tác và hiển thị kết quả. Giữ ba store demo hiện tại
trong quá trình chuyển để tránh làm hỏng các luồng đang dùng.

Xem [kiến trúc repository](../../docs/architecture.md) và
[kế hoạch bàn giao của nhóm](../../planning/team-delivery-plan.md) để theo dõi phạm
vi, phân công, thứ tự tích hợp và các mốc demo.
