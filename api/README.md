# CyberNet - EF Core Code-First (MySQL / Pomelo)

Sinh tu dong tu so do ERD he thong Quan ly Quan Net (35 bang).

## Cau truc
```
Models/   35 file entity (moi bang 1 file .cs)
Data/     CyberNetDbContext.cs
```
Namespace: `CyberNet.Models`, `CyberNet.Data` (doi bang Find & Replace neu can).

## Cai package
```bash
dotnet add package Microsoft.EntityFrameworkCore --version 9.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 9.0.0
dotnet add package Pomelo.EntityFrameworkCore.MySql --version 9.0.0
```

## Dang ky DbContext (Program.cs)
```csharp
using CyberNet.Data;
using Microsoft.EntityFrameworkCore;

var cs = builder.Configuration.GetConnectionString("Default")!;
builder.Services.AddDbContext<CyberNetDbContext>(opt =>
    opt.UseMySql(cs, ServerVersion.AutoDetect(cs)));
```
appsettings.json:
```json
"ConnectionStrings": {
  "Default": "Server=localhost;Port=3306;Database=cybernet;User=root;Password=your_password;"
}
```

## Migration
```bash
dotnet tool install --global dotnet-ef --version 9.0.0
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Quy uoc thiet ke
- Ten bang/cot giu dung nhu ERD (bang viet thuong, cot PascalCase).
- Moi FK mac dinh `DeleteBehavior.Restrict` de tranh cascade vong lap.
  Chi cascade cho bang con "so huu" thuc su: OrderDetails, OrderStatusHistory,
  InvoiceDetails, StockReceiptDetails, SessionCharges, TeamMembers (theo Team),
  RolePermissions.
- Quan he N-N: Role <-> Permission qua `RolePermission`; Team <-> TournamentRegistration
  qua `TeamMember` (co them cot AssignedRole). Ca hai dung khoa chinh phuc hop (Fluent API).
- Unique index: Customers.Username, Employees.Username, Roles.RoleName,
  Permissions.PermissionName, Games.GameName, Categories.CategoryName,
  Computers.ComputerName, ComputerRooms.RoomName, ComputerTypes.TypeName,
  (TournamentId, CustomerId), (TournamentId, TeamName), (GameId, RankOrder).
- Cot `CreatedAt/ChangedAt/...` co default `CURRENT_TIMESTAMP`.

## Cac gia dinh (vi anh ERD khong the hien het)
- Cac kieu bi cat chu trong anh (VARCHAR(25.., VARCHAR(10.., VARCHAR(15..) duoc suy ra:
  255 / 100 / 150.
- Nullability duoc suy luan: cac cot nhu Orders.CustomerId/SessionId/InvoiceId,
  Invoices.SessionId/PromotionId, Payments.WalletTransactionId,
  TournamentRegistrations.RankId, EndTime cua session/maintenance, Phone/Email/Description
  la nullable. Hay chinh lai neu nghiep vu cua ban khac.
- `InventoryTransactions.ReferenceId` la tham chieu mem (khong co FK trong ERD).
- Cac cot `ChangedBy` / `CreatedBy` tro toi Employees; ten navigation:
  `ChangedByEmployee`, `CreatedByEmployee`.
