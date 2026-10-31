# Script tiện ích Windows

Các file `.cmd` là tập lệnh Windows, không chứa nghiệp vụ ứng dụng. Có thể dùng trực tiếp `npm` và `dotnet`; các wrapper chỉ hỗ trợ máy đã cài công cụ portable trong `.tools/` hoặc muốn chạy nhanh.

`.tools/` là thư mục **cục bộ**, được `.gitignore` bỏ qua nên người khác clone repo không cần nó. Máy hiện tại dùng nó để giữ ảnh/log/kết quả kiểm thử, bản sao và script tạm lúc dọn backlog; nếu cài Node, .NET hoặc `dotnet-ef` portable thì wrapper tìm công cụ tại đó. Các file này không phải source hay dữ liệu nghiệp vụ. Có thể dọn artifact cũ khi không còn cần đối chiếu, nhưng kiểm tra trước xem máy đang dùng SDK/CLI portable trong `.tools/` hay không.

| File | Tác dụng |
| --- | --- |
| `start.cmd` | Chuyển tới `frontend/`, cài dependency nếu thiếu rồi chạy Vite ở `127.0.0.1:5173` |
| `npm-local.cmd` | Chọn Node/npm portable nếu có, nếu không dùng Node/npm đã cài trên máy |
| `dotnet-local.cmd` | Tương tự cho .NET SDK; `global.json` tại root chọn SDK .NET 10 ổn định |
| `ef-local.cmd` | Gọi EF Core CLI để quản lý migration; cần cài `dotnet-ef` trước theo hướng dẫn backend |

Từ thư mục gốc:

```powershell
.\scripts\start.cmd
.\scripts\npm-local.cmd --prefix frontend run build
.\scripts\dotnet-local.cmd build backend\CyberCafe.Api\CyberCafe.Api.csproj
```

`start.cmd` hiện chỉ khởi động frontend demo và middleware OP.GG, chưa chạy API .NET/SQL Server hay cấu hình LAN. Xem [hướng dẫn backend](../backend/README.md) và [quy trình nhóm](../planning/team-workflow.md).
