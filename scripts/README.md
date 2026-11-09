# Script tiện ích Windows

Wrapper dùng công cụ portable trong `.tools/` nếu có rồi tìm bản đã cài; trả lại đúng exit code. `.tools/` là cục bộ, không đi theo clone.

| Script | Công dụng |
| --- | --- |
| `npm-local.cmd` | Node/npm cho frontend |
| `dotnet-local.cmd` | .NET SDK theo `global.json` |
| `ef-local.cmd` | EF CLI, cần cài `dotnet-ef` 10.0.14 theo Backend README |
| `start.cmd` | Cài dependency frontend nếu thiếu và chạy Vite/OP.GG ở 127.0.0.1:5173 |

Từ repository root:

```powershell
.\scripts\start.cmd
.\scripts\npm-local.cmd --prefix frontend run build
.\scripts\dotnet-local.cmd build Backend\CyberManagement.slnx
.\scripts\ef-local.cmd migrations list --no-connect --project Backend\src\CyberManagement.Infrastructure --startup-project Backend\src\CyberManagement.API
```

`start.cmd` không chạy API .NET/SQL hoặc thiết lập LAN. Cấu hình DB mới 35 bảng/JWT/seed theo [Backend README](../Backend/README.md); quy trình nhận task/review theo [hướng dẫn nhóm](../planning/team-workflow.md).

S1-M-2 bổ sung `configure-zalopay-sandbox.ps1`, `start-zalopay-demo.ps1`, `stop-zalopay-demo.ps1`:
cấu hình User Secrets và chạy/dừng listener kiểm tra chữ ký qua HTTPS riêng. Cách dùng và giới hạn
tại [Backend README](../Backend/README.md) và [contract ZaloPay](../Backend/docs/005-zalopay-sandbox-contract.md).
