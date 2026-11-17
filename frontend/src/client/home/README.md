# Client Home — SCRUM-24

Trang `/client/home` dùng theme NEXORA CYBER. Máy, loại/cấu hình, phiên đang mở, snapshot giá, phí đã thu và số dư đọc từ `GET /api/client/home`, giới hạn theo khách đăng nhập. Không lấy máy/phiên/tiền giờ từ `useClientDemo` hoặc localStorage.

`useSessionHome` dùng thêm API ví chính chủ và hóa đơn để hiển thị lịch sử tiền/chứng từ thật. Loading, lỗi và không có phiên được phân biệt; lỗi không giữ phí demo. Nhận `SessionChanged`, `WalletChanged`, `CounterTopUpChanged`, `InvoiceChanged` và ready/reconnect sẽ đọc lại API; có refetch mỗi 30 giây để phục hồi khi mất event. Các lượt đọc được tuần tự hóa với một lượt đọc bù khi event tới lúc đang tải. Đổi tài khoản hủy và bỏ phản hồi của tài khoản cũ.

Đồng hồ chỉ hiển thị thời gian; browser không tính khoản ghi sổ. `totalCost` là tổng đã thu phía server. Thời gian còn lại là ước tính theo số dư và snapshot giá, chưa tính các khoản chi tương lai. Không cộng lại SessionCharges vào TotalCost.

Thực đơn và giải đấu tiếp tục dùng API của module tương ứng. Bấm dấu `+` chuyển sang `/client/ordering` với navigation state `{ openCart: true, initialCartItems: [{ ProductId, Quantity: 1 }] }`; thao tác này chưa ghi đơn hoặc trừ ví.

Dữ liệu đơn món/chi tiết đơn và tên chào còn dùng provider demo hiện có, thuộc luồng chưa chuyển trong task này. `useHomeData.js`/`homeModel.js` là model demo được giữ cho caller và kiểm thử cũ; không dùng để hiển thị máy/phiên/thu phí trên Home. Sidebar/thông báo thuộc ClientLayout.

Từ repository root:

```powershell
npm.cmd --prefix frontend run build
npm.cmd --prefix frontend run test:frontend:render
node --test frontend/tests/session-api.test.mjs
node frontend/tests/run.mjs sessions-billing
dotnet run --project Backend/tests/CyberManagement.API.Checks -- --sessions-only
```

Browser suite dùng HTTP/SDK fixtures cho CRUD/bảo trì/mở-đóng phiên, Home loading/error/empty/retry, event/reconnect và 320px. SQL suite yêu cầu AUTH_CHECKS_SQL_CONNECTION, dùng DB thử riêng cho transaction, retry/cạnh tranh, rollback và hai kết nối SignalR. Kiểm tra local không chứng minh hai máy LAN thật.
