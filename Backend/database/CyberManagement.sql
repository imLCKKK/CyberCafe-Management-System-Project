IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [CategoryId] int NOT NULL IDENTITY,
        [CategoryName] nvarchar(150) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([CategoryId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [ComputerRooms] (
        [RoomId] int NOT NULL IDENTITY,
        [RoomName] nvarchar(100) NOT NULL,
        [Floor] int NOT NULL,
        [Description] nvarchar(255) NULL,
        CONSTRAINT [PK_ComputerRooms] PRIMARY KEY ([RoomId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [ComputerTypes] (
        [ComputerTypeId] int NOT NULL IDENTITY,
        [TypeName] nvarchar(100) NOT NULL,
        [CPU] nvarchar(100) NULL,
        [RAM] nvarchar(50) NULL,
        [GPU] nvarchar(100) NULL,
        [PricePerHour] decimal(12,2) NOT NULL,
        [Description] nvarchar(255) NULL,
        CONSTRAINT [PK_ComputerTypes] PRIMARY KEY ([ComputerTypeId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Customers] (
        [CustomerId] int NOT NULL IDENTITY,
        [FullName] nvarchar(150) NOT NULL,
        [Phone] nvarchar(20) NOT NULL,
        [Email] nvarchar(150) NULL,
        [WalletBalance] decimal(12,2) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([CustomerId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Games] (
        [GameId] int NOT NULL IDENTITY,
        [GameName] nvarchar(100) NOT NULL,
        [TeamSize] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Games] PRIMARY KEY ([GameId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Permissions] (
        [PermissionId] int NOT NULL IDENTITY,
        [PermissionName] nvarchar(100) NOT NULL,
        [Description] nvarchar(255) NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([PermissionId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Promotions] (
        [PromotionId] int NOT NULL IDENTITY,
        [PromoName] nvarchar(150) NOT NULL,
        [DiscountType] nvarchar(50) NOT NULL,
        [DiscountValue] decimal(12,2) NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Promotions] PRIMARY KEY ([PromotionId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [RoleId] int NOT NULL IDENTITY,
        [RoleName] nvarchar(100) NOT NULL,
        [Description] nvarchar(255) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Suppliers] (
        [SupplierId] int NOT NULL IDENTITY,
        [SupplierName] nvarchar(150) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [Email] nvarchar(150) NULL,
        [Address] nvarchar(255) NULL,
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([SupplierId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [ProductId] int NOT NULL IDENTITY,
        [CategoryId] int NOT NULL,
        [ProductName] nvarchar(150) NOT NULL,
        [Price] decimal(12,2) NOT NULL,
        [Stock] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([ProductId]),
        CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([CategoryId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Computers] (
        [ComputerId] int NOT NULL IDENTITY,
        [RoomId] int NOT NULL,
        [ComputerTypeId] int NOT NULL,
        [ComputerName] nvarchar(100) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_Computers] PRIMARY KEY ([ComputerId]),
        CONSTRAINT [FK_Computers_ComputerRooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [ComputerRooms] ([RoomId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Computers_ComputerTypes_ComputerTypeId] FOREIGN KEY ([ComputerTypeId]) REFERENCES [ComputerTypes] ([ComputerTypeId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [WalletTransactions] (
        [TransactionId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [Amount] decimal(12,2) NOT NULL,
        [BalanceAfter] decimal(12,2) NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [Description] nvarchar(255) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_WalletTransactions] PRIMARY KEY ([TransactionId]),
        CONSTRAINT [FK_WalletTransactions_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [GameRanks] (
        [RankId] int NOT NULL IDENTITY,
        [GameId] int NOT NULL,
        [RankName] nvarchar(50) NOT NULL,
        [RankOrder] int NOT NULL,
        CONSTRAINT [PK_GameRanks] PRIMARY KEY ([RankId]),
        CONSTRAINT [FK_GameRanks_Games_GameId] FOREIGN KEY ([GameId]) REFERENCES [Games] ([GameId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Employees] (
        [EmployeeId] int NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [Username] nvarchar(50) NOT NULL,
        [PasswordHash] nvarchar(255) NOT NULL,
        [FailedLoginCount] int NOT NULL,
        [LockoutEnd] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([EmployeeId]),
        CONSTRAINT [FK_Employees_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([RoleId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [RoleId] int NOT NULL,
        [PermissionId] int NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([PermissionId]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([RoleId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [GamingSessions] (
        [GamingSessionId] int NOT NULL IDENTITY,
        [ComputerId] int NOT NULL,
        [CustomerId] int NOT NULL,
        [StartTime] datetime2 NOT NULL,
        [EndTime] datetime2 NULL,
        [LastChargedAt] datetime2 NULL,
        [TotalCost] decimal(12,2) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_GamingSessions] PRIMARY KEY ([GamingSessionId]),
        CONSTRAINT [FK_GamingSessions_Computers_ComputerId] FOREIGN KEY ([ComputerId]) REFERENCES [Computers] ([ComputerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GamingSessions_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [LogId] int NOT NULL IDENTITY,
        [EmployeeId] int NULL,
        [EntityType] nvarchar(50) NOT NULL,
        [EntityId] int NOT NULL,
        [Action] nvarchar(255) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([LogId]),
        CONSTRAINT [FK_AuditLogs_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [ComputerMaintenance] (
        [MaintenanceId] int NOT NULL IDENTITY,
        [ComputerId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [Description] nvarchar(255) NULL,
        [StartTime] datetime2 NOT NULL,
        [EndTime] datetime2 NULL,
        [Status] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_ComputerMaintenance] PRIMARY KEY ([MaintenanceId]),
        CONSTRAINT [FK_ComputerMaintenance_Computers_ComputerId] FOREIGN KEY ([ComputerId]) REFERENCES [Computers] ([ComputerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ComputerMaintenance_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [CustomerStatusHistory] (
        [HistoryId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [OldStatus] nvarchar(50) NULL,
        [NewStatus] nvarchar(50) NOT NULL,
        [Reason] nvarchar(255) NULL,
        [ChangedBy] int NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CustomerStatusHistory] PRIMARY KEY ([HistoryId]),
        CONSTRAINT [FK_CustomerStatusHistory_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomerStatusHistory_Employees_ChangedBy] FOREIGN KEY ([ChangedBy]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Shifts] (
        [ShiftId] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [StartTime] datetime2 NOT NULL,
        [EndTime] datetime2 NULL,
        CONSTRAINT [PK_Shifts] PRIMARY KEY ([ShiftId]),
        CONSTRAINT [FK_Shifts_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [StockReceipts] (
        [ReceiptId] int NOT NULL IDENTITY,
        [SupplierId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [ReceiptDate] datetime2 NOT NULL,
        [TotalAmount] decimal(12,2) NOT NULL,
        CONSTRAINT [PK_StockReceipts] PRIMARY KEY ([ReceiptId]),
        CONSTRAINT [FK_StockReceipts_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockReceipts_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([SupplierId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Tournaments] (
        [TournamentId] int NOT NULL IDENTITY,
        [GameId] int NOT NULL,
        [CreatedBy] int NOT NULL,
        [TournamentName] nvarchar(150) NOT NULL,
        [RegistrationStart] datetime2 NOT NULL,
        [RegistrationEnd] datetime2 NOT NULL,
        [StartTime] datetime2 NOT NULL,
        [EntryFee] decimal(12,2) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_Tournaments] PRIMARY KEY ([TournamentId]),
        CONSTRAINT [FK_Tournaments_Employees_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Tournaments_Games_GameId] FOREIGN KEY ([GameId]) REFERENCES [Games] ([GameId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Invoices] (
        [InvoiceId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [GamingSessionId] int NULL,
        [EmployeeId] int NOT NULL,
        [PromotionId] int NULL,
        [InvoiceDate] datetime2 NOT NULL,
        [Subtotal] decimal(12,2) NOT NULL,
        [Discount] decimal(12,2) NOT NULL,
        [TotalAmount] decimal(12,2) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([InvoiceId]),
        CONSTRAINT [FK_Invoices_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Invoices_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Invoices_GamingSessions_GamingSessionId] FOREIGN KEY ([GamingSessionId]) REFERENCES [GamingSessions] ([GamingSessionId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Invoices_Promotions_PromotionId] FOREIGN KEY ([PromotionId]) REFERENCES [Promotions] ([PromotionId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Orders] (
        [OrderId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [GamingSessionId] int NULL,
        [EmployeeId] int NOT NULL,
        [OrderTime] datetime2 NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [TotalAmount] decimal(12,2) NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([OrderId]),
        CONSTRAINT [FK_Orders_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Orders_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Orders_GamingSessions_GamingSessionId] FOREIGN KEY ([GamingSessionId]) REFERENCES [GamingSessions] ([GamingSessionId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [SessionCharges] (
        [ChargeId] int NOT NULL IDENTITY,
        [GamingSessionId] int NOT NULL,
        [StartTime] datetime2 NOT NULL,
        [EndTime] datetime2 NOT NULL,
        [Amount] decimal(12,2) NOT NULL,
        CONSTRAINT [PK_SessionCharges] PRIMARY KEY ([ChargeId]),
        CONSTRAINT [FK_SessionCharges_GamingSessions_GamingSessionId] FOREIGN KEY ([GamingSessionId]) REFERENCES [GamingSessions] ([GamingSessionId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [StockReceiptDetails] (
        [ReceiptDetailId] int NOT NULL IDENTITY,
        [ReceiptId] int NOT NULL,
        [ProductId] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitCost] decimal(12,2) NOT NULL,
        CONSTRAINT [PK_StockReceiptDetails] PRIMARY KEY ([ReceiptDetailId]),
        CONSTRAINT [FK_StockReceiptDetails_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([ProductId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockReceiptDetails_StockReceipts_ReceiptId] FOREIGN KEY ([ReceiptId]) REFERENCES [StockReceipts] ([ReceiptId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Teams] (
        [TeamId] int NOT NULL IDENTITY,
        [TournamentId] int NOT NULL,
        [TeamName] nvarchar(100) NOT NULL,
        [AvgSkillScore] decimal(6,2) NULL,
        CONSTRAINT [PK_Teams] PRIMARY KEY ([TeamId]),
        CONSTRAINT [FK_Teams_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([TournamentId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [TournamentRegistrations] (
        [RegistrationId] int NOT NULL IDENTITY,
        [TournamentId] int NOT NULL,
        [CustomerId] int NOT NULL,
        [RankId] int NOT NULL,
        [PreferredRole] nvarchar(50) NULL,
        [WinRate] decimal(5,2) NOT NULL,
        [HoursPlayed] int NOT NULL,
        [SkillScore] decimal(6,2) NULL,
        [Status] nvarchar(50) NOT NULL,
        [RegisteredAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TournamentRegistrations] PRIMARY KEY ([RegistrationId]),
        CONSTRAINT [FK_TournamentRegistrations_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TournamentRegistrations_GameRanks_RankId] FOREIGN KEY ([RankId]) REFERENCES [GameRanks] ([RankId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TournamentRegistrations_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([TournamentId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [InvoiceDetails] (
        [InvoiceDetailId] int NOT NULL IDENTITY,
        [InvoiceId] int NOT NULL,
        [ItemType] nvarchar(50) NOT NULL,
        [Description] nvarchar(255) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(12,2) NOT NULL,
        [Amount] decimal(12,2) NOT NULL,
        CONSTRAINT [PK_InvoiceDetails] PRIMARY KEY ([InvoiceDetailId]),
        CONSTRAINT [FK_InvoiceDetails_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([InvoiceId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [PaymentId] int NOT NULL IDENTITY,
        [InvoiceId] int NOT NULL,
        [WalletTransactionId] int NULL,
        [Amount] decimal(12,2) NOT NULL,
        [Method] nvarchar(50) NOT NULL,
        [PaymentTime] datetime2 NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([PaymentId]),
        CONSTRAINT [FK_Payments_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([InvoiceId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_WalletTransactions_WalletTransactionId] FOREIGN KEY ([WalletTransactionId]) REFERENCES [WalletTransactions] ([TransactionId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [InventoryTransactions] (
        [InvTransId] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [Quantity] int NOT NULL,
        [ReferenceType] nvarchar(50) NOT NULL,
        [ReceiptId] int NULL,
        [OrderId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_InventoryTransactions] PRIMARY KEY ([InvTransId]),
        CONSTRAINT [FK_InventoryTransactions_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryTransactions_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryTransactions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([ProductId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryTransactions_StockReceipts_ReceiptId] FOREIGN KEY ([ReceiptId]) REFERENCES [StockReceipts] ([ReceiptId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [InvoiceOrders] (
        [InvoiceId] int NOT NULL,
        [OrderId] int NOT NULL,
        CONSTRAINT [PK_InvoiceOrders] PRIMARY KEY ([InvoiceId], [OrderId]),
        CONSTRAINT [FK_InvoiceOrders_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([InvoiceId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InvoiceOrders_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderDetails] (
        [OrderDetailId] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [ProductId] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(12,2) NOT NULL,
        CONSTRAINT [PK_OrderDetails] PRIMARY KEY ([OrderDetailId]),
        CONSTRAINT [FK_OrderDetails_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderDetails_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([ProductId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderStatusHistory] (
        [HistoryId] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [OldStatus] nvarchar(50) NULL,
        [NewStatus] nvarchar(50) NOT NULL,
        [ChangedBy] int NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderStatusHistory] PRIMARY KEY ([HistoryId]),
        CONSTRAINT [FK_OrderStatusHistory_Employees_ChangedBy] FOREIGN KEY ([ChangedBy]) REFERENCES [Employees] ([EmployeeId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrderStatusHistory_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE TABLE [TeamMembers] (
        [TeamId] int NOT NULL,
        [RegistrationId] int NOT NULL,
        [AssignedRole] nvarchar(50) NULL,
        CONSTRAINT [PK_TeamMembers] PRIMARY KEY ([TeamId], [RegistrationId]),
        CONSTRAINT [FK_TeamMembers_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([TeamId]) ON DELETE CASCADE,
        CONSTRAINT [FK_TeamMembers_TournamentRegistrations_RegistrationId] FOREIGN KEY ([RegistrationId]) REFERENCES [TournamentRegistrations] ([RegistrationId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EmployeeId] ON [AuditLogs] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ComputerMaintenance_ComputerId] ON [ComputerMaintenance] ([ComputerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ComputerMaintenance_EmployeeId] ON [ComputerMaintenance] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Computers_ComputerTypeId] ON [Computers] ([ComputerTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Computers_RoomId_ComputerName] ON [Computers] ([RoomId], [ComputerName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Customers_Phone] ON [Customers] ([Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerStatusHistory_ChangedBy] ON [CustomerStatusHistory] ([ChangedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerStatusHistory_CustomerId] ON [CustomerStatusHistory] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Employees_RoleId] ON [Employees] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Employees_Username] ON [Employees] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GameRanks_GameId_RankName] ON [GameRanks] ([GameId], [RankName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GamingSessions_ComputerId] ON [GamingSessions] ([ComputerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GamingSessions_CustomerId] ON [GamingSessions] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_EmployeeId] ON [InventoryTransactions] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_OrderId] ON [InventoryTransactions] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_ProductId] ON [InventoryTransactions] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_ReceiptId] ON [InventoryTransactions] ([ReceiptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceDetails_InvoiceId] ON [InvoiceDetails] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceOrders_OrderId] ON [InvoiceOrders] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_CustomerId] ON [Invoices] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_EmployeeId] ON [Invoices] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_GamingSessionId] ON [Invoices] ([GamingSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_PromotionId] ON [Invoices] ([PromotionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderDetails_OrderId] ON [OrderDetails] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderDetails_ProductId] ON [OrderDetails] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_CustomerId] ON [Orders] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_EmployeeId] ON [Orders] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_GamingSessionId] ON [Orders] ([GamingSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistory_ChangedBy] ON [OrderStatusHistory] ([ChangedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistory_OrderId] ON [OrderStatusHistory] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_InvoiceId] ON [Payments] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_WalletTransactionId] ON [Payments] ([WalletTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_PermissionName] ON [Permissions] ([PermissionName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_RoleName] ON [Roles] ([RoleName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SessionCharges_GamingSessionId] ON [SessionCharges] ([GamingSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shifts_EmployeeId] ON [Shifts] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockReceiptDetails_ProductId] ON [StockReceiptDetails] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockReceiptDetails_ReceiptId] ON [StockReceiptDetails] ([ReceiptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockReceipts_EmployeeId] ON [StockReceipts] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockReceipts_SupplierId] ON [StockReceipts] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TeamMembers_RegistrationId] ON [TeamMembers] ([RegistrationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Teams_TournamentId] ON [Teams] ([TournamentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TournamentRegistrations_CustomerId] ON [TournamentRegistrations] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TournamentRegistrations_RankId] ON [TournamentRegistrations] ([RankId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TournamentRegistrations_TournamentId_CustomerId] ON [TournamentRegistrations] ([TournamentId], [CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tournaments_CreatedBy] ON [Tournaments] ([CreatedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tournaments_GameId] ON [Tournaments] ([GameId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WalletTransactions_CustomerId] ON [WalletTransactions] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102065247_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102065247_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102073816_CustomerPasswordAuthentication'
)
BEGIN
    ALTER TABLE [Customers] ADD [FailedLoginCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102073816_CustomerPasswordAuthentication'
)
BEGIN
    ALTER TABLE [Customers] ADD [LockoutEnd] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102073816_CustomerPasswordAuthentication'
)
BEGIN
    ALTER TABLE [Customers] ADD [PasswordHash] nvarchar(255) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102073816_CustomerPasswordAuthentication'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102073816_CustomerPasswordAuthentication', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102083200_RemoveCustomerLoginLockout'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Customers]') AND [c].[name] = N'FailedLoginCount');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Customers] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Customers] DROP COLUMN [FailedLoginCount];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102083200_RemoveCustomerLoginLockout'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Customers]') AND [c].[name] = N'LockoutEnd');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Customers] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [Customers] DROP COLUMN [LockoutEnd];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102083200_RemoveCustomerLoginLockout'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102083200_RemoveCustomerLoginLockout', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102084459_RemoveEmployeeLoginLockout'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Employees]') AND [c].[name] = N'FailedLoginCount');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Employees] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [Employees] DROP COLUMN [FailedLoginCount];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102084459_RemoveEmployeeLoginLockout'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Employees]') AND [c].[name] = N'LockoutEnd');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Employees] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [Employees] DROP COLUMN [LockoutEnd];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102084459_RemoveEmployeeLoginLockout'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102084459_RemoveEmployeeLoginLockout', N'10.0.14');
END;

COMMIT;
GO
