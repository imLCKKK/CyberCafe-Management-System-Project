    /*
  Gaming Cafe / Net Center — SQL Server schema
  Chạy trên database trống hoặc đổi tên DB bên dưới.
  SQL Server 2016+ (dùng DROP TABLE IF EXISTS).
*/

-- CREATE DATABASE GamingCafe;
-- GO
-- USE GamingCafe;
-- GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ========== Drop (child → parent) ========== */
IF OBJECT_ID(N'dbo.TeamMembers', N'U') IS NOT NULL DROP TABLE dbo.TeamMembers;
IF OBJECT_ID(N'dbo.Teams', N'U') IS NOT NULL DROP TABLE dbo.Teams;
IF OBJECT_ID(N'dbo.TournamentRegistrations', N'U') IS NOT NULL DROP TABLE dbo.TournamentRegistrations;
IF OBJECT_ID(N'dbo.Tournaments', N'U') IS NOT NULL DROP TABLE dbo.Tournaments;
IF OBJECT_ID(N'dbo.GameRanks', N'U') IS NOT NULL DROP TABLE dbo.GameRanks;
IF OBJECT_ID(N'dbo.Games', N'U') IS NOT NULL DROP TABLE dbo.Games;
IF OBJECT_ID(N'dbo.Payments', N'U') IS NOT NULL DROP TABLE dbo.Payments;
IF OBJECT_ID(N'dbo.InvoiceDetails', N'U') IS NOT NULL DROP TABLE dbo.InvoiceDetails;
IF OBJECT_ID(N'dbo.InvoiceOrders', N'U') IS NOT NULL DROP TABLE dbo.InvoiceOrders;
IF OBJECT_ID(N'dbo.Invoices', N'U') IS NOT NULL DROP TABLE dbo.Invoices;
IF OBJECT_ID(N'dbo.InventoryTransactions', N'U') IS NOT NULL DROP TABLE dbo.InventoryTransactions;
IF OBJECT_ID(N'dbo.OrderStatusHistory', N'U') IS NOT NULL DROP TABLE dbo.OrderStatusHistory;
IF OBJECT_ID(N'dbo.OrderDetails', N'U') IS NOT NULL DROP TABLE dbo.OrderDetails;
IF OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL DROP TABLE dbo.Orders;
IF OBJECT_ID(N'dbo.StockReceiptDetails', N'U') IS NOT NULL DROP TABLE dbo.StockReceiptDetails;
IF OBJECT_ID(N'dbo.StockReceipts', N'U') IS NOT NULL DROP TABLE dbo.StockReceipts;
IF OBJECT_ID(N'dbo.Promotions', N'U') IS NOT NULL DROP TABLE dbo.Promotions;
IF OBJECT_ID(N'dbo.SessionCharges', N'U') IS NOT NULL DROP TABLE dbo.SessionCharges;
IF OBJECT_ID(N'dbo.GamingSessions', N'U') IS NOT NULL DROP TABLE dbo.GamingSessions;
IF OBJECT_ID(N'dbo.ComputerMaintenance', N'U') IS NOT NULL DROP TABLE dbo.ComputerMaintenance;
IF OBJECT_ID(N'dbo.Computers', N'U') IS NOT NULL DROP TABLE dbo.Computers;
IF OBJECT_ID(N'dbo.ComputerTypes', N'U') IS NOT NULL DROP TABLE dbo.ComputerTypes;
IF OBJECT_ID(N'dbo.ComputerRooms', N'U') IS NOT NULL DROP TABLE dbo.ComputerRooms;
IF OBJECT_ID(N'dbo.Products', N'U') IS NOT NULL DROP TABLE dbo.Products;
IF OBJECT_ID(N'dbo.Categories', N'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID(N'dbo.Suppliers', N'U') IS NOT NULL DROP TABLE dbo.Suppliers;
IF OBJECT_ID(N'dbo.WalletTransactions', N'U') IS NOT NULL DROP TABLE dbo.WalletTransactions;
IF OBJECT_ID(N'dbo.CustomerStatusHistory', N'U') IS NOT NULL DROP TABLE dbo.CustomerStatusHistory;
IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL DROP TABLE dbo.Customers;
IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NOT NULL DROP TABLE dbo.AuditLogs;
IF OBJECT_ID(N'dbo.Shifts', N'U') IS NOT NULL DROP TABLE dbo.Shifts;
IF OBJECT_ID(N'dbo.Employees', N'U') IS NOT NULL DROP TABLE dbo.Employees;
IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NOT NULL DROP TABLE dbo.RolePermissions;
IF OBJECT_ID(N'dbo.Permissions', N'U') IS NOT NULL DROP TABLE dbo.Permissions;
IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL DROP TABLE dbo.Roles;
GO

/* ========== IAM ========== */
CREATE TABLE dbo.Roles (
    RoleId       INT            NOT NULL IDENTITY(1, 1),
    RoleName     NVARCHAR(100)  NOT NULL,
    Description  NVARCHAR(255)  NULL,
    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (RoleId),
    CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName)
);

CREATE TABLE dbo.Permissions (
    PermissionId   INT            NOT NULL IDENTITY(1, 1),
    PermissionName NVARCHAR(100)  NOT NULL,
    Description    NVARCHAR(255)  NULL,
    CONSTRAINT PK_Permissions PRIMARY KEY CLUSTERED (PermissionId),
    CONSTRAINT UQ_Permissions_PermissionName UNIQUE (PermissionName)
);

CREATE TABLE dbo.RolePermissions (
    RoleId       INT NOT NULL,
    PermissionId INT NOT NULL,
    CONSTRAINT PK_RolePermissions PRIMARY KEY CLUSTERED (RoleId, PermissionId),
    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId)
        REFERENCES dbo.Roles (RoleId),
    CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId)
        REFERENCES dbo.Permissions (PermissionId)
);

CREATE TABLE dbo.Employees (
    EmployeeId  INT            NOT NULL IDENTITY(1, 1),
    RoleId      INT            NOT NULL,
    FullName    NVARCHAR(150)  NOT NULL,
    Phone       NVARCHAR(20)   NULL,
    IsActive    BIT            NOT NULL CONSTRAINT DF_Employees_IsActive DEFAULT (1),
    CreatedAt   DATETIME2(0)   NOT NULL CONSTRAINT DF_Employees_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Employees PRIMARY KEY CLUSTERED (EmployeeId),
    CONSTRAINT FK_Employees_Roles FOREIGN KEY (RoleId)
        REFERENCES dbo.Roles (RoleId)
);

CREATE TABLE dbo.Shifts (
    ShiftId     INT          NOT NULL IDENTITY(1, 1),
    EmployeeId  INT          NOT NULL,
    StartTime   DATETIME2(0) NOT NULL,
    EndTime     DATETIME2(0) NULL,
    CONSTRAINT PK_Shifts PRIMARY KEY CLUSTERED (ShiftId),
    CONSTRAINT FK_Shifts_Employees FOREIGN KEY (EmployeeId)
        REFERENCES dbo.Employees (EmployeeId)
);

CREATE TABLE dbo.AuditLogs (
    LogId       INT            NOT NULL IDENTITY(1, 1),
    EmployeeId  INT            NULL,
    EntityType  NVARCHAR(50)   NULL,
    EntityId    INT            NULL,
    Action      NVARCHAR(255)  NOT NULL,
    CreatedAt   DATETIME2(0)   NOT NULL CONSTRAINT DF_AuditLogs_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (LogId),
    CONSTRAINT FK_AuditLogs_Employees FOREIGN KEY (EmployeeId)
        REFERENCES dbo.Employees (EmployeeId)
);

/* ========== Customers & wallet ========== */
CREATE TABLE dbo.Customers (
    CustomerId     INT             NOT NULL IDENTITY(1, 1),
    FullName       NVARCHAR(150)   NOT NULL,
    Phone          NVARCHAR(20)    NOT NULL,
    Email          NVARCHAR(150)   NULL,
    WalletBalance  DECIMAL(12, 2)  NOT NULL CONSTRAINT DF_Customers_WalletBalance DEFAULT (0),
    Status         NVARCHAR(50)    NOT NULL,
    CreatedAt      DATETIME2(0)    NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Customers PRIMARY KEY CLUSTERED (CustomerId),
    CONSTRAINT UQ_Customers_Phone UNIQUE (Phone)
);

CREATE TABLE dbo.CustomerStatusHistory (
    HistoryId   INT            NOT NULL IDENTITY(1, 1),
    CustomerId  INT            NOT NULL,
    OldStatus   NVARCHAR(50)   NULL,
    NewStatus   NVARCHAR(50)   NOT NULL,
    Reason      NVARCHAR(255)  NULL,
    ChangedBy   INT            NOT NULL,
    ChangedAt   DATETIME2(0)   NOT NULL CONSTRAINT DF_CustomerStatusHistory_ChangedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_CustomerStatusHistory PRIMARY KEY CLUSTERED (HistoryId),
    CONSTRAINT FK_CustomerStatusHistory_Customers FOREIGN KEY (CustomerId)
        REFERENCES dbo.Customers (CustomerId),
    CONSTRAINT FK_CustomerStatusHistory_Employees FOREIGN KEY (ChangedBy)
        REFERENCES dbo.Employees (EmployeeId)
);

CREATE TABLE dbo.WalletTransactions (
    TransactionId  INT             NOT NULL IDENTITY(1, 1),
    CustomerId     INT             NOT NULL,
    Amount         DECIMAL(12, 2)  NOT NULL,
    BalanceAfter   DECIMAL(12, 2) NOT NULL,
    Type           NVARCHAR(50)    NOT NULL,
    Description    NVARCHAR(255)   NULL,
    CreatedAt      DATETIME2(0)    NOT NULL CONSTRAINT DF_WalletTransactions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_WalletTransactions PRIMARY KEY CLUSTERED (TransactionId),
    CONSTRAINT FK_WalletTransactions_Customers FOREIGN KEY (CustomerId)
        REFERENCES dbo.Customers (CustomerId)
);

/* ========== Computers ========== */
CREATE TABLE dbo.ComputerRooms (
    RoomId       INT            NOT NULL IDENTITY(1, 1),
    RoomName     NVARCHAR(100)  NOT NULL,
    Floor        INT            NULL,
    Description  NVARCHAR(255)  NULL,
    CONSTRAINT PK_ComputerRooms PRIMARY KEY CLUSTERED (RoomId)
);

CREATE TABLE dbo.ComputerTypes (
    ComputerTypeId  INT             NOT NULL IDENTITY(1, 1),
    TypeName        NVARCHAR(100)   NOT NULL,
    CPU             NVARCHAR(100)   NULL,
    RAM             NVARCHAR(50)    NULL,
    GPU             NVARCHAR(100)   NULL,
    PricePerHour    DECIMAL(12, 2)  NOT NULL,
    Description     NVARCHAR(255)   NULL,
    CONSTRAINT PK_ComputerTypes PRIMARY KEY CLUSTERED (ComputerTypeId)
);

CREATE TABLE dbo.Computers (
    ComputerId       INT           NOT NULL IDENTITY(1, 1),
    RoomId           INT           NOT NULL,
    ComputerTypeId   INT           NOT NULL,
    ComputerName     NVARCHAR(100) NOT NULL,
    Status           NVARCHAR(50)  NOT NULL,
    CONSTRAINT PK_Computers PRIMARY KEY CLUSTERED (ComputerId),
    CONSTRAINT UQ_Computers_Room_ComputerName UNIQUE (RoomId, ComputerName),
    CONSTRAINT FK_Computers_ComputerRooms FOREIGN KEY (RoomId)
        REFERENCES dbo.ComputerRooms (RoomId),
    CONSTRAINT FK_Computers_ComputerTypes FOREIGN KEY (ComputerTypeId)
        REFERENCES dbo.ComputerTypes (ComputerTypeId)
);

CREATE TABLE dbo.ComputerMaintenance (
    MaintenanceId  INT            NOT NULL IDENTITY(1, 1),
    ComputerId     INT            NOT NULL,
    EmployeeId     INT            NOT NULL,
    Description    NVARCHAR(255)  NULL,
    StartTime      DATETIME2(0)   NOT NULL,
    EndTime        DATETIME2(0)   NULL,
    Status         NVARCHAR(50)   NOT NULL,
    CONSTRAINT PK_ComputerMaintenance PRIMARY KEY CLUSTERED (MaintenanceId),
    CONSTRAINT FK_ComputerMaintenance_Computers FOREIGN KEY (ComputerId)
        REFERENCES dbo.Computers (ComputerId),
    CONSTRAINT FK_ComputerMaintenance_Employees FOREIGN KEY (EmployeeId)
        REFERENCES dbo.Employees (EmployeeId)
);

CREATE TABLE dbo.GamingSessions (
    GamingSessionId  INT             NOT NULL IDENTITY(1, 1),
    ComputerId       INT             NOT NULL,
    CustomerId       INT             NOT NULL,
    StartTime        DATETIME2(0)    NOT NULL,
    EndTime          DATETIME2(0)    NULL,
    LastChargedAt    DATETIME2(0)    NULL,
    TotalCost        DECIMAL(12, 2)  NOT NULL CONSTRAINT DF_GamingSessions_TotalCost DEFAULT (0),
    Status           NVARCHAR(50)    NOT NULL,
    CONSTRAINT PK_GamingSessions PRIMARY KEY CLUSTERED (GamingSessionId),
    CONSTRAINT FK_GamingSessions_Computers FOREIGN KEY (ComputerId)
        REFERENCES dbo.Computers (ComputerId),
    CONSTRAINT FK_GamingSessions_Customers FOREIGN KEY (CustomerId)
        REFERENCES dbo.Customers (CustomerId)
);

CREATE TABLE dbo.SessionCharges (
    ChargeId         INT             NOT NULL IDENTITY(1, 1),
    GamingSessionId  INT             NOT NULL,
    StartTime        DATETIME2(0)    NOT NULL,
    EndTime          DATETIME2(0)    NOT NULL,
    Amount           DECIMAL(12, 2)  NOT NULL,
    CONSTRAINT PK_SessionCharges PRIMARY KEY CLUSTERED (ChargeId),
    CONSTRAINT FK_SessionCharges_GamingSessions FOREIGN KEY (GamingSessionId)
        REFERENCES dbo.GamingSessions (GamingSessionId)
);

/* ========== Inventory & products ========== */
CREATE TABLE dbo.Categories (
    CategoryId    INT            NOT NULL IDENTITY(1, 1),
    CategoryName  NVARCHAR(150)  NOT NULL,
    IsActive      BIT            NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT (1),
    CONSTRAINT PK_Categories PRIMARY KEY CLUSTERED (CategoryId)
);

CREATE TABLE dbo.Products (
    ProductId    INT             NOT NULL IDENTITY(1, 1),
    CategoryId   INT             NOT NULL,
    ProductName  NVARCHAR(150)   NOT NULL,
    Price        DECIMAL(12, 2)  NOT NULL,
    Stock        INT             NOT NULL CONSTRAINT DF_Products_Stock DEFAULT (0),
    IsActive     BIT             NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT (1),
    CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (ProductId),
    CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId)
        REFERENCES dbo.Categories (CategoryId)
);

CREATE TABLE dbo.Suppliers (
    SupplierId    INT            NOT NULL IDENTITY(1, 1),
    SupplierName  NVARCHAR(150)  NOT NULL,
    Phone         NVARCHAR(20)   NULL,
    Email         NVARCHAR(150)  NULL,
    Address       NVARCHAR(255)  NULL,
    CONSTRAINT PK_Suppliers PRIMARY KEY CLUSTERED (SupplierId)
);

CREATE TABLE dbo.StockReceipts (
    ReceiptId    INT             NOT NULL IDENTITY(1, 1),
    SupplierId   INT             NOT NULL,
    EmployeeId   INT             NOT NULL,
    ReceiptDate  DATETIME2(0)    NOT NULL,
    TotalAmount  DECIMAL(12, 2)  NOT NULL,
    CONSTRAINT PK_StockReceipts PRIMARY KEY CLUSTERED (ReceiptId),
    CONSTRAINT FK_StockReceipts_Suppliers FOREIGN KEY (SupplierId)
        REFERENCES dbo.Suppliers (SupplierId),
    CONSTRAINT FK_StockReceipts_Employees FOREIGN KEY (EmployeeId)
        REFERENCES dbo.Employees (EmployeeId)
);

CREATE TABLE dbo.StockReceiptDetails (
    ReceiptDetailId  INT             NOT NULL IDENTITY(1, 1),
    ReceiptId        INT             NOT NULL,
    ProductId        INT             NOT NULL,
    Quantity         INT             NOT NULL,
    UnitCost         DECIMAL(12, 2)  NOT NULL,
    CONSTRAINT PK_StockReceiptDetails PRIMARY KEY CLUSTERED (ReceiptDetailId),
    CONSTRAINT FK_StockReceiptDetails_StockReceipts FOREIGN KEY (ReceiptId)
        REFERENCES dbo.StockReceipts (ReceiptId),
    CONSTRAINT FK_StockReceiptDetails_Products FOREIGN KEY (ProductId)
        REFERENCES dbo.Products (ProductId)
);

CREATE TABLE dbo.Promotions (
    PromotionId    INT             NOT NULL IDENTITY(1, 1),
    PromoName      NVARCHAR(150)   NOT NULL,
    DiscountType   NVARCHAR(50)    NOT NULL,
    DiscountValue  DECIMAL(12, 2)  NOT NULL,
    StartDate      DATETIME2(0)    NOT NULL,
    EndDate        DATETIME2(0)    NOT NULL,
    IsActive       BIT             NOT NULL CONSTRAINT DF_Promotions_IsActive DEFAULT (1),
    CONSTRAINT PK_Promotions PRIMARY KEY CLUSTERED (PromotionId)
);

/* ========== Orders & inventory ledger ========== */
CREATE TABLE dbo.Orders (
    OrderId          INT             NOT NULL IDENTITY(1, 1),
    CustomerId       INT             NOT NULL,
    GamingSessionId  INT             NULL,
    EmployeeId       INT             NOT NULL,
    OrderTime        DATETIME2(0)    NOT NULL CONSTRAINT DF_Orders_OrderTime DEFAULT (SYSUTCDATETIME()),
    Status           NVARCHAR(50)    NOT NULL,
    TotalAmount      DECIMAL(12, 2)  NOT NULL,
    CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED (OrderId),
    CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId)
        REFERENCES dbo.Customers (CustomerId),
    CONSTRAINT FK_Orders_GamingSessions FOREIGN KEY (GamingSessionId)
        REFERENCES dbo.GamingSessions (GamingSessionId),
    CONSTRAINT FK_Orders_Employees FOREIGN KEY (EmployeeId)
        REFERENCES dbo.Employees (EmployeeId)
);

CREATE TABLE dbo.OrderDetails (
    OrderDetailId  INT             NOT NULL IDENTITY(1, 1),
    OrderId        INT             NOT NULL,
    ProductId      INT             NOT NULL,
    Quantity       INT             NOT NULL,
    UnitPrice      DECIMAL(12, 2)  NOT NULL,
    CONSTRAINT PK_OrderDetails PRIMARY KEY CLUSTERED (OrderDetailId),
    CONSTRAINT FK_OrderDetails_Orders FOREIGN KEY (OrderId)
        REFERENCES dbo.Orders (OrderId),
    CONSTRAINT FK_OrderDetails_Products FOREIGN KEY (ProductId)
        REFERENCES dbo.Products (ProductId)
);

CREATE TABLE dbo.OrderStatusHistory (
    HistoryId   INT            NOT NULL IDENTITY(1, 1),
    OrderId     INT            NOT NULL,
    OldStatus   NVARCHAR(50)   NULL,
    NewStatus   NVARCHAR(50)   NOT NULL,
    ChangedBy   INT            NOT NULL,
    ChangedAt   DATETIME2(0)   NOT NULL CONSTRAINT DF_OrderStatusHistory_ChangedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_OrderStatusHistory PRIMARY KEY CLUSTERED (HistoryId),
    CONSTRAINT FK_OrderStatusHistory_Orders FOREIGN KEY (OrderId)
        REFERENCES dbo.Orders (OrderId),
    CONSTRAINT FK_OrderStatusHistory_Employees FOREIGN KEY (ChangedBy)
        REFERENCES dbo.Employees (EmployeeId)
);

CREATE TABLE dbo.InventoryTransactions (
    InvTransId     INT            NOT NULL IDENTITY(1, 1),
    ProductId      INT            NOT NULL,
    EmployeeId     INT            NOT NULL,
    Type           NVARCHAR(50)   NOT NULL,
    Quantity       INT            NOT NULL,
    ReferenceType  NVARCHAR(50)   NOT NULL,
    ReceiptId      INT            NULL,
    OrderId        INT            NULL,
    CreatedAt      DATETIME2(0)   NOT NULL CONSTRAINT DF_InventoryTransactions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_InventoryTransactions PRIMARY KEY CLUSTERED (InvTransId),
    CONSTRAINT FK_InventoryTransactions_Products FOREIGN KEY (ProductId)
        REFERENCES dbo.Products (ProductId),
    CONSTRAINT FK_InventoryTransactions_Employees FOREIGN KEY (EmployeeId)
        REFERENCES dbo.Employees (EmployeeId),
    CONSTRAINT FK_InventoryTransactions_StockReceipts FOREIGN KEY (ReceiptId)
        REFERENCES dbo.StockReceipts (ReceiptId),
    CONSTRAINT FK_InventoryTransactions_Orders FOREIGN KEY (OrderId)
        REFERENCES dbo.Orders (OrderId)
);

/* ========== Invoices & payments ========== */
CREATE TABLE dbo.Invoices (
    InvoiceId        INT             NOT NULL IDENTITY(1, 1),
    CustomerId       INT             NOT NULL,
    GamingSessionId  INT             NULL,
    EmployeeId       INT             NOT NULL,
    PromotionId      INT             NULL,
    InvoiceDate      DATETIME2(0)    NOT NULL CONSTRAINT DF_Invoices_InvoiceDate DEFAULT (SYSUTCDATETIME()),
    Subtotal         DECIMAL(12, 2)  NOT NULL,
    Discount         DECIMAL(12, 2)  NOT NULL CONSTRAINT DF_Invoices_Discount DEFAULT (0),
    TotalAmount      DECIMAL(12, 2)  NOT NULL,
    Status           NVARCHAR(50)    NOT NULL,
    CONSTRAINT PK_Invoices PRIMARY KEY CLUSTERED (InvoiceId),
    CONSTRAINT FK_Invoices_Customers FOREIGN KEY (CustomerId)
        REFERENCES dbo.Customers (CustomerId),
    CONSTRAINT FK_Invoices_GamingSessions FOREIGN KEY (GamingSessionId)
        REFERENCES dbo.GamingSessions (GamingSessionId),
    CONSTRAINT FK_Invoices_Employees FOREIGN KEY (EmployeeId)
        REFERENCES dbo.Employees (EmployeeId),
    CONSTRAINT FK_Invoices_Promotions FOREIGN KEY (PromotionId)
        REFERENCES dbo.Promotions (PromotionId)
);

CREATE TABLE dbo.InvoiceOrders (
    InvoiceId  INT NOT NULL,
    OrderId    INT NOT NULL,
    CONSTRAINT PK_InvoiceOrders PRIMARY KEY CLUSTERED (InvoiceId, OrderId),
    CONSTRAINT FK_InvoiceOrders_Invoices FOREIGN KEY (InvoiceId)
        REFERENCES dbo.Invoices (InvoiceId),
    CONSTRAINT FK_InvoiceOrders_Orders FOREIGN KEY (OrderId)
        REFERENCES dbo.Orders (OrderId)
);

CREATE TABLE dbo.InvoiceDetails (
    InvoiceDetailId  INT             NOT NULL IDENTITY(1, 1),
    InvoiceId        INT             NOT NULL,
    ItemType         NVARCHAR(50)    NOT NULL,
    Description      NVARCHAR(255)   NULL,
    Quantity         INT             NOT NULL,
    UnitPrice        DECIMAL(12, 2)  NOT NULL,
    Amount           DECIMAL(12, 2)  NOT NULL,
    CONSTRAINT PK_InvoiceDetails PRIMARY KEY CLUSTERED (InvoiceDetailId),
    CONSTRAINT FK_InvoiceDetails_Invoices FOREIGN KEY (InvoiceId)
        REFERENCES dbo.Invoices (InvoiceId)
);

CREATE TABLE dbo.Payments (
    PaymentId            INT             NOT NULL IDENTITY(1, 1),
    InvoiceId            INT             NOT NULL,
    WalletTransactionId  INT             NULL,
    Amount               DECIMAL(12, 2)  NOT NULL,
    Method               NVARCHAR(50)    NOT NULL,
    PaymentTime          DATETIME2(0)    NOT NULL CONSTRAINT DF_Payments_PaymentTime DEFAULT (SYSUTCDATETIME()),
    Status               NVARCHAR(50)    NOT NULL,
    CONSTRAINT PK_Payments PRIMARY KEY CLUSTERED (PaymentId),
    CONSTRAINT FK_Payments_Invoices FOREIGN KEY (InvoiceId)
        REFERENCES dbo.Invoices (InvoiceId),
    CONSTRAINT FK_Payments_WalletTransactions FOREIGN KEY (WalletTransactionId)
        REFERENCES dbo.WalletTransactions (TransactionId)
);

/* ========== Tournaments ========== */
CREATE TABLE dbo.Games (
    GameId    INT           NOT NULL IDENTITY(1, 1),
    GameName  NVARCHAR(100) NOT NULL,
    TeamSize  INT           NOT NULL,
    IsActive  BIT           NOT NULL CONSTRAINT DF_Games_IsActive DEFAULT (1),
    CONSTRAINT PK_Games PRIMARY KEY CLUSTERED (GameId)
);

CREATE TABLE dbo.GameRanks (
    RankId     INT           NOT NULL IDENTITY(1, 1),
    GameId     INT           NOT NULL,
    RankName   NVARCHAR(50)  NOT NULL,
    RankOrder  INT           NOT NULL,
    CONSTRAINT PK_GameRanks PRIMARY KEY CLUSTERED (RankId),
    CONSTRAINT UQ_GameRanks_Game_RankName UNIQUE (GameId, RankName),
    CONSTRAINT FK_GameRanks_Games FOREIGN KEY (GameId)
        REFERENCES dbo.Games (GameId)
);

CREATE TABLE dbo.Tournaments (
    TournamentId       INT             NOT NULL IDENTITY(1, 1),
    GameId             INT             NOT NULL,
    CreatedBy          INT             NOT NULL,
    TournamentName     NVARCHAR(150)   NOT NULL,
    RegistrationStart  DATETIME2(0)    NOT NULL,
    RegistrationEnd    DATETIME2(0)    NOT NULL,
    StartTime          DATETIME2(0)    NOT NULL,
    EntryFee           DECIMAL(12, 2)  NOT NULL CONSTRAINT DF_Tournaments_EntryFee DEFAULT (0),
    Status             NVARCHAR(50)    NOT NULL,
    CONSTRAINT PK_Tournaments PRIMARY KEY CLUSTERED (TournamentId),
    CONSTRAINT FK_Tournaments_Games FOREIGN KEY (GameId)
        REFERENCES dbo.Games (GameId),
    CONSTRAINT FK_Tournaments_Employees FOREIGN KEY (CreatedBy)
        REFERENCES dbo.Employees (EmployeeId)
);

CREATE TABLE dbo.TournamentRegistrations (
    RegistrationId  INT             NOT NULL IDENTITY(1, 1),
    TournamentId    INT             NOT NULL,
    CustomerId      INT             NOT NULL,
    RankId          INT             NOT NULL,
    PreferredRole   NVARCHAR(50)    NULL,
    WinRate         DECIMAL(5, 2)   NULL,
    HoursPlayed     INT             NULL,
    SkillScore      DECIMAL(6, 2)   NULL,
    Status          NVARCHAR(50)    NOT NULL,
    RegisteredAt    DATETIME2(0)    NOT NULL CONSTRAINT DF_TournamentRegistrations_RegisteredAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_TournamentRegistrations PRIMARY KEY CLUSTERED (RegistrationId),
    CONSTRAINT UQ_TournamentRegistrations_Tournament_Customer UNIQUE (TournamentId, CustomerId),
    CONSTRAINT FK_TournamentRegistrations_Tournaments FOREIGN KEY (TournamentId)
        REFERENCES dbo.Tournaments (TournamentId),
    CONSTRAINT FK_TournamentRegistrations_Customers FOREIGN KEY (CustomerId)
        REFERENCES dbo.Customers (CustomerId),
    CONSTRAINT FK_TournamentRegistrations_GameRanks FOREIGN KEY (RankId)
        REFERENCES dbo.GameRanks (RankId)
);

CREATE TABLE dbo.Teams (
    TeamId          INT             NOT NULL IDENTITY(1, 1),
    TournamentId    INT             NOT NULL,
    TeamName        NVARCHAR(100)   NOT NULL,
    AvgSkillScore   DECIMAL(6, 2)   NULL,
    CONSTRAINT PK_Teams PRIMARY KEY CLUSTERED (TeamId),
    CONSTRAINT FK_Teams_Tournaments FOREIGN KEY (TournamentId)
        REFERENCES dbo.Tournaments (TournamentId)
);

CREATE TABLE dbo.TeamMembers (
    TeamId           INT NOT NULL,
    RegistrationId   INT NOT NULL,
    AssignedRole     NVARCHAR(50) NULL,
    CONSTRAINT PK_TeamMembers PRIMARY KEY CLUSTERED (TeamId, RegistrationId),
    CONSTRAINT UQ_TeamMembers_RegistrationId UNIQUE (RegistrationId),
    CONSTRAINT FK_TeamMembers_Teams FOREIGN KEY (TeamId)
        REFERENCES dbo.Teams (TeamId),
    CONSTRAINT FK_TeamMembers_TournamentRegistrations FOREIGN KEY (RegistrationId)
        REFERENCES dbo.TournamentRegistrations (RegistrationId)
);
GO

/* ========== Indexes (tra cứu thường dùng) ========== */
CREATE NONCLUSTERED INDEX IX_GamingSessions_ComputerId_Status
    ON dbo.GamingSessions (ComputerId, Status);
CREATE NONCLUSTERED INDEX IX_GamingSessions_CustomerId_StartTime
    ON dbo.GamingSessions (CustomerId, StartTime DESC);
CREATE NONCLUSTERED INDEX IX_Orders_GamingSessionId
    ON dbo.Orders (GamingSessionId)
    WHERE GamingSessionId IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_WalletTransactions_CustomerId_CreatedAt
    ON dbo.WalletTransactions (CustomerId, CreatedAt DESC);
CREATE NONCLUSTERED INDEX IX_AuditLogs_CreatedAt
    ON dbo.AuditLogs (CreatedAt DESC);
GO

PRINT N'Schema Gaming Cafe đã tạo xong.';
GO
