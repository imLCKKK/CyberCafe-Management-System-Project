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

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    SET ANSI_WARNINGS ON;
    SET ANSI_NULLS ON;
    SET QUOTED_IDENTIFIER ON;
    SET ANSI_PADDING ON;
    SET CONCAT_NULL_YIELDS_NULL ON;
    SET ARITHABORT ON;
    SET NUMERIC_ROUNDABORT OFF;
    IF EXISTS (SELECT 1 FROM [dbo].[WalletTransactions] WHERE [BalanceAfter] <> ROUND([BalanceAfter], 0)) THROW 51001, 'SCRUM13: fractional VND in WalletTransactions.BalanceAfter; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[WalletTransactions] WHERE [Amount] <> ROUND([Amount], 0)) THROW 51001, 'SCRUM13: fractional VND in WalletTransactions.Amount; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[StockReceipts] WHERE [TotalAmount] <> ROUND([TotalAmount], 0)) THROW 51001, 'SCRUM13: fractional VND in StockReceipts.TotalAmount; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[StockReceiptDetails] WHERE [UnitCost] <> ROUND([UnitCost], 0)) THROW 51001, 'SCRUM13: fractional VND in StockReceiptDetails.UnitCost; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[SessionCharges] WHERE [Amount] <> ROUND([Amount], 0)) THROW 51001, 'SCRUM13: fractional VND in SessionCharges.Amount; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[Products] WHERE [Price] <> ROUND([Price], 0)) THROW 51001, 'SCRUM13: fractional VND in Products.Price; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[Payments] WHERE [Amount] <> ROUND([Amount], 0)) THROW 51001, 'SCRUM13: fractional VND in Payments.Amount; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[Orders] WHERE [TotalAmount] <> ROUND([TotalAmount], 0)) THROW 51001, 'SCRUM13: fractional VND in Orders.TotalAmount; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[OrderDetails] WHERE [UnitPrice] <> ROUND([UnitPrice], 0)) THROW 51001, 'SCRUM13: fractional VND in OrderDetails.UnitPrice; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[Invoices] WHERE [TotalAmount] <> ROUND([TotalAmount], 0)) THROW 51001, 'SCRUM13: fractional VND in Invoices.TotalAmount; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[Invoices] WHERE [Subtotal] <> ROUND([Subtotal], 0)) THROW 51001, 'SCRUM13: fractional VND in Invoices.Subtotal; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[Invoices] WHERE [Discount] <> ROUND([Discount], 0)) THROW 51001, 'SCRUM13: fractional VND in Invoices.Discount; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[InvoiceDetails] WHERE [UnitPrice] <> ROUND([UnitPrice], 0)) THROW 51001, 'SCRUM13: fractional VND in InvoiceDetails.UnitPrice; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[InvoiceDetails] WHERE [Amount] <> ROUND([Amount], 0)) THROW 51001, 'SCRUM13: fractional VND in InvoiceDetails.Amount; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[GamingSessions] WHERE [TotalCost] <> ROUND([TotalCost], 0)) THROW 51001, 'SCRUM13: fractional VND in GamingSessions.TotalCost; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[Customers] WHERE [WalletBalance] <> ROUND([WalletBalance], 0)) THROW 51001, 'SCRUM13: fractional VND in Customers.WalletBalance; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[ComputerTypes] WHERE [PricePerHour] <> ROUND([PricePerHour], 0)) THROW 51001, 'SCRUM13: fractional VND in ComputerTypes.PricePerHour; reconcile before migration.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[InventoryTransactions]) THROW 51002, 'SCRUM13: InventoryTransactions has legacy rows requiring verified source/snapshot/time backfill; migration stopped without resetting data.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[Promotions]) THROW 51002, 'SCRUM13: Promotions has legacy rows requiring verified source/snapshot/time backfill; migration stopped without resetting data.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[Orders]) THROW 51002, 'SCRUM13: Orders has legacy rows requiring verified source/snapshot/time backfill; migration stopped without resetting data.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[OrderDetails]) THROW 51002, 'SCRUM13: OrderDetails has legacy rows requiring verified source/snapshot/time backfill; migration stopped without resetting data.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[Invoices]) THROW 51002, 'SCRUM13: Invoices has legacy rows requiring verified source/snapshot/time backfill; migration stopped without resetting data.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[InvoiceDetails]) THROW 51002, 'SCRUM13: InvoiceDetails has legacy rows requiring verified source/snapshot/time backfill; migration stopped without resetting data.', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[Payments]) THROW 51002, 'SCRUM13: Payments has legacy rows requiring verified source/snapshot/time backfill; migration stopped without resetting data.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [InvoiceDetails] DROP CONSTRAINT [FK_InvoiceDetails_Invoices_InvoiceId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [OrderDetails] DROP CONSTRAINT [FK_OrderDetails_Orders_OrderId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] DROP CONSTRAINT [FK_Payments_Invoices_InvoiceId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] DROP CONSTRAINT [FK_Payments_WalletTransactions_WalletTransactionId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DROP INDEX [IX_Payments_WalletTransactionId] ON [Payments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DROP INDEX [IX_InvoiceOrders_OrderId] ON [InvoiceOrders];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WalletTransactions]') AND [c].[name] = N'BalanceAfter');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [WalletTransactions] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [WalletTransactions] ALTER COLUMN [BalanceAfter] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WalletTransactions]') AND [c].[name] = N'Amount');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [WalletTransactions] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [WalletTransactions] ALTER COLUMN [Amount] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StockReceipts]') AND [c].[name] = N'TotalAmount');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [StockReceipts] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [StockReceipts] ALTER COLUMN [TotalAmount] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StockReceiptDetails]') AND [c].[name] = N'UnitCost');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [StockReceiptDetails] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [StockReceiptDetails] ALTER COLUMN [UnitCost] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SessionCharges]') AND [c].[name] = N'Amount');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [SessionCharges] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [SessionCharges] ALTER COLUMN [Amount] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Promotions]') AND [c].[name] = N'StartDate');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Promotions] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [Promotions] ALTER COLUMN [StartDate] datetimeoffset NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Promotions]') AND [c].[name] = N'EndDate');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Promotions] DROP CONSTRAINT ' + @var10 + ';');
    ALTER TABLE [Promotions] ALTER COLUMN [EndDate] datetimeoffset NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var11 nvarchar(max);
    SELECT @var11 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Promotions]') AND [c].[name] = N'DiscountValue');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [Promotions] DROP CONSTRAINT ' + @var11 + ';');
    ALTER TABLE [Promotions] ALTER COLUMN [DiscountValue] decimal(18,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var12 nvarchar(max);
    SELECT @var12 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Promotions]') AND [c].[name] = N'DiscountType');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [Promotions] DROP CONSTRAINT ' + @var12 + ';');
    ALTER TABLE [Promotions] ALTER COLUMN [DiscountType] nvarchar(10) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Promotions] ADD [Code] nvarchar(50) COLLATE Latin1_General_100_CI_AS NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Promotions] ADD [MaxDiscountAmount] decimal(18,0) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Promotions] ADD [MinOrderAmount] decimal(18,0) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Promotions] ADD [PerCustomerUsageLimit] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Promotions] ADD [TotalUsageLimit] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var13 nvarchar(max);
    SELECT @var13 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'Price');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT ' + @var13 + ';');
    ALTER TABLE [Products] ALTER COLUMN [Price] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var14 nvarchar(max);
    SELECT @var14 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payments]') AND [c].[name] = N'Status');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [Payments] DROP CONSTRAINT ' + @var14 + ';');
    ALTER TABLE [Payments] ALTER COLUMN [Status] nvarchar(10) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var15 nvarchar(max);
    SELECT @var15 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payments]') AND [c].[name] = N'PaymentTime');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [Payments] DROP CONSTRAINT ' + @var15 + ';');
    ALTER TABLE [Payments] ALTER COLUMN [PaymentTime] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var16 nvarchar(max);
    SELECT @var16 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payments]') AND [c].[name] = N'Method');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [Payments] DROP CONSTRAINT ' + @var16 + ';');
    ALTER TABLE [Payments] ALTER COLUMN [Method] nvarchar(10) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var17 nvarchar(max);
    SELECT @var17 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payments]') AND [c].[name] = N'InvoiceId');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [Payments] DROP CONSTRAINT ' + @var17 + ';');
    ALTER TABLE [Payments] ALTER COLUMN [InvoiceId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var18 nvarchar(max);
    SELECT @var18 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payments]') AND [c].[name] = N'Amount');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [Payments] DROP CONSTRAINT ' + @var18 + ';');
    ALTER TABLE [Payments] ALTER COLUMN [Amount] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD [ConfirmedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD [CreatedAt] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD [GatewayTransactionId] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD [MerchantTransactionId] nvarchar(40) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD [OperationKey] nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD [OrderDetailId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD [OrderId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD [PaymentTimeBasis] nvarchar(12) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD [ProviderOccurredAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD [Reason] nvarchar(250) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD [RefundOfPaymentId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD [SessionChargeId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD [Type] nvarchar(10) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var19 nvarchar(max);
    SELECT @var19 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'TotalAmount');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT ' + @var19 + ';');
    ALTER TABLE [Orders] ALTER COLUMN [TotalAmount] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Orders] ADD [DiscountAmount] decimal(18,0) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Orders] ADD [VoucherCodeSnapshot] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Orders] ADD [VoucherDiscountTypeSnapshot] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Orders] ADD [VoucherDiscountValueSnapshot] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var20 nvarchar(max);
    SELECT @var20 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderDetails]') AND [c].[name] = N'UnitPrice');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [OrderDetails] DROP CONSTRAINT ' + @var20 + ';');
    ALTER TABLE [OrderDetails] ALTER COLUMN [UnitPrice] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [OrderDetails] ADD [DiscountAmount] decimal(18,0) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [OrderDetails] ADD [ProductNameSnapshot] nvarchar(150) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var21 nvarchar(max);
    SELECT @var21 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Invoices]') AND [c].[name] = N'TotalAmount');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [Invoices] DROP CONSTRAINT ' + @var21 + ';');
    ALTER TABLE [Invoices] ALTER COLUMN [TotalAmount] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var22 nvarchar(max);
    SELECT @var22 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Invoices]') AND [c].[name] = N'Subtotal');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [Invoices] DROP CONSTRAINT ' + @var22 + ';');
    ALTER TABLE [Invoices] ALTER COLUMN [Subtotal] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var23 nvarchar(max);
    SELECT @var23 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Invoices]') AND [c].[name] = N'Discount');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [Invoices] DROP CONSTRAINT ' + @var23 + ';');
    ALTER TABLE [Invoices] ALTER COLUMN [Discount] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var24 nvarchar(max);
    SELECT @var24 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceDetails]') AND [c].[name] = N'UnitPrice');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceDetails] DROP CONSTRAINT ' + @var24 + ';');
    ALTER TABLE [InvoiceDetails] ALTER COLUMN [UnitPrice] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var25 nvarchar(max);
    SELECT @var25 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InvoiceDetails]') AND [c].[name] = N'Amount');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [InvoiceDetails] DROP CONSTRAINT ' + @var25 + ';');
    ALTER TABLE [InvoiceDetails] ALTER COLUMN [Amount] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [InvoiceDetails] ADD [OrderDetailId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [InvoiceDetails] ADD [SessionChargeId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var26 nvarchar(max);
    SELECT @var26 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryTransactions]') AND [c].[name] = N'EmployeeId');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [InventoryTransactions] DROP CONSTRAINT ' + @var26 + ';');
    ALTER TABLE [InventoryTransactions] ALTER COLUMN [EmployeeId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var27 nvarchar(max);
    SELECT @var27 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[InventoryTransactions]') AND [c].[name] = N'CreatedAt');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [InventoryTransactions] DROP CONSTRAINT ' + @var27 + ';');
    ALTER TABLE [InventoryTransactions] ALTER COLUMN [CreatedAt] datetimeoffset NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [InventoryTransactions] ADD [After] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [InventoryTransactions] ADD [Before] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [InventoryTransactions] ADD [OrderDetailId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [InventoryTransactions] ADD [Reason] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var28 nvarchar(max);
    SELECT @var28 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[GamingSessions]') AND [c].[name] = N'TotalCost');
    IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [GamingSessions] DROP CONSTRAINT ' + @var28 + ';');
    ALTER TABLE [GamingSessions] ALTER COLUMN [TotalCost] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var29 nvarchar(max);
    SELECT @var29 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Customers]') AND [c].[name] = N'WalletBalance');
    IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [Customers] DROP CONSTRAINT ' + @var29 + ';');
    ALTER TABLE [Customers] ALTER COLUMN [WalletBalance] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    DECLARE @var30 nvarchar(max);
    SELECT @var30 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ComputerTypes]') AND [c].[name] = N'PricePerHour');
    IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [ComputerTypes] DROP CONSTRAINT ' + @var30 + ';');
    ALTER TABLE [ComputerTypes] ALTER COLUMN [PricePerHour] decimal(18,0) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE TABLE [InvoiceIssueRequests] (
        [OperationKey] nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [RequestHash] char(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [InvoiceId] int NOT NULL,
        CONSTRAINT [PK_InvoiceIssueRequests] PRIMARY KEY ([OperationKey]),
        CONSTRAINT [CK_InvoiceIssueRequests_Hash] CHECK (LEN([RequestHash]) = 64 AND [RequestHash] NOT LIKE '%[^0-9a-f]%' COLLATE Latin1_General_100_BIN2),
        CONSTRAINT [CK_InvoiceIssueRequests_Key] CHECK (LEN([OperationKey]) BETWEEN 1 AND 100 AND [OperationKey] NOT LIKE '%[^-A-Za-z0-9:_]%' COLLATE Latin1_General_100_BIN2),
        CONSTRAINT [FK_InvoiceIssueRequests_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([InvoiceId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE TABLE [PromotionUsages] (
        [PromotionUsageId] int NOT NULL IDENTITY,
        [PromotionId] int NOT NULL,
        [OrderId] int NOT NULL,
        [CustomerId] int NOT NULL,
        [UsedAt] datetimeoffset NOT NULL,
        [ReleasedAt] datetimeoffset NULL,
        CONSTRAINT [PK_PromotionUsages] PRIMARY KEY ([PromotionUsageId]),
        CONSTRAINT [CK_PromotionUsages_Release] CHECK ([ReleasedAt] IS NULL OR [ReleasedAt] >= [UsedAt]),
        CONSTRAINT [FK_PromotionUsages_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]),
        CONSTRAINT [FK_PromotionUsages_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId]),
        CONSTRAINT [FK_PromotionUsages_Promotions_PromotionId] FOREIGN KEY ([PromotionId]) REFERENCES [Promotions] ([PromotionId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE TABLE [RefundAttempts] (
        [RefundAttemptId] int NOT NULL IDENTITY,
        [PaymentId] int NOT NULL,
        [ProviderAppId] int NOT NULL,
        [MerchantRefundId] nvarchar(45) NOT NULL,
        [GatewayRefundId] nvarchar(50) NULL,
        [Status] nvarchar(10) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [LastQueriedAt] datetimeoffset NULL,
        [LastApi] nvarchar(12) NULL,
        [LastReturnCode] int NULL,
        [LastSubReturnCode] int NULL,
        CONSTRAINT [PK_RefundAttempts] PRIMARY KEY ([RefundAttemptId]),
        CONSTRAINT [CK_RefundAttempts_Values] CHECK ([ProviderAppId] > 0 AND LEN(LTRIM(RTRIM([MerchantRefundId]))) > 0 AND [Status] IN ('Created','Submitted','Processing','NotFound','Failed','Succeeded') AND ([LastApi] IS NULL OR [LastApi] IN ('Refund','QueryRefund'))),
        CONSTRAINT [FK_RefundAttempts_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([PaymentId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Promotions_Code] ON [Promotions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [Promotions] ADD CONSTRAINT [CK_Promotions_Code] CHECK (LEN([Code]) BETWEEN 1 AND 50 AND [Code] COLLATE Latin1_General_100_BIN2 NOT LIKE ''%[^-A-Z0-9_]%'' COLLATE Latin1_General_100_BIN2)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [Promotions] ADD CONSTRAINT [CK_Promotions_Dates] CHECK ([StartDate] < [EndDate])');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [Promotions] ADD CONSTRAINT [CK_Promotions_Discount] CHECK (([DiscountType] = ''Percent'' AND [DiscountValue] > 0 AND [DiscountValue] <= 100) OR ([DiscountType] = ''Fixed'' AND [DiscountValue] > 0 AND [DiscountValue] = ROUND([DiscountValue], 0)))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [Promotions] ADD CONSTRAINT [CK_Promotions_Limits] CHECK ([MinOrderAmount] >= 0 AND ([MaxDiscountAmount] IS NULL OR [MaxDiscountAmount] > 0) AND ([TotalUsageLimit] IS NULL OR [TotalUsageLimit] > 0) AND ([PerCustomerUsageLimit] IS NULL OR [PerCustomerUsageLimit] > 0))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [Products] ADD CONSTRAINT [CK_Products_Values] CHECK ([Stock] >= 0 AND [Price] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_Method_GatewayTransactionId] ON [Payments] ([Method], [GatewayTransactionId]) WHERE [GatewayTransactionId] IS NOT NULL AND [Type] = ''Payment''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_Method_MerchantTransactionId] ON [Payments] ([Method], [MerchantTransactionId]) WHERE [MerchantTransactionId] IS NOT NULL AND [Type] = ''Payment''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_OperationKey] ON [Payments] ([OperationKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE INDEX [IX_Payments_OrderDetailId] ON [Payments] ([OrderDetailId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE INDEX [IX_Payments_OrderId] ON [Payments] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE INDEX [IX_Payments_RefundOfPaymentId_Status] ON [Payments] ([RefundOfPaymentId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE INDEX [IX_Payments_SessionChargeId] ON [Payments] ([SessionChargeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_WalletTransactionId] ON [Payments] ([WalletTransactionId]) WHERE [WalletTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [Payments] ADD CONSTRAINT [CK_Payments_Method] CHECK (([Method] <> ''Wallet'' OR [Status] <> ''Succeeded'' OR [WalletTransactionId] IS NOT NULL) AND ([Method] <> ''ZaloPay'' OR [Type] <> ''Payment'' OR ([MerchantTransactionId] IS NOT NULL AND LEN(LTRIM(RTRIM([MerchantTransactionId]))) > 0 AND ([Status] <> ''Succeeded'' OR ([GatewayTransactionId] IS NOT NULL AND LEN(LTRIM(RTRIM([GatewayTransactionId]))) > 0)))))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [Payments] ADD CONSTRAINT [CK_Payments_Source] CHECK (([OrderId] IS NOT NULL AND [SessionChargeId] IS NULL) OR ([OrderId] IS NULL AND [SessionChargeId] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [Payments] ADD CONSTRAINT [CK_Payments_Time] CHECK (([Status] = ''Succeeded'' AND [PaymentTime] IS NOT NULL AND [ConfirmedAt] IS NOT NULL AND [PaymentTimeBasis] IS NOT NULL AND ([PaymentTimeBasis] = ''Source'' OR ([PaymentTimeBasis] = ''Confirmation'' AND [PaymentTime] = [ConfirmedAt]))) OR ([Status] <> ''Succeeded'' AND [PaymentTime] IS NULL AND [ConfirmedAt] IS NULL AND [PaymentTimeBasis] IS NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [Payments] ADD CONSTRAINT [CK_Payments_Type] CHECK (([Type] = ''Payment'' AND [RefundOfPaymentId] IS NULL AND [OrderDetailId] IS NULL) OR ([Type] = ''Refund'' AND [RefundOfPaymentId] IS NOT NULL AND [RefundOfPaymentId] <> [PaymentId] AND [Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0 AND [Status] <> ''Expired''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [Payments] ADD CONSTRAINT [CK_Payments_Values] CHECK ([Amount] > 0 AND [Method] IN (''Wallet'',''Cash'',''ZaloPay'') AND [Status] IN (''Pending'',''Succeeded'',''Failed'',''Expired'') AND LEN(LTRIM(RTRIM([OperationKey]))) > 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [Orders] ADD CONSTRAINT [CK_Orders_Amounts] CHECK ([TotalAmount] >= 0 AND [DiscountAmount] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [Orders] ADD CONSTRAINT [CK_Orders_VoucherSnapshot] CHECK (([VoucherCodeSnapshot] IS NULL AND [VoucherDiscountTypeSnapshot] IS NULL AND [VoucherDiscountValueSnapshot] IS NULL AND [DiscountAmount] = 0) OR ([VoucherCodeSnapshot] IS NOT NULL AND LEN(LTRIM(RTRIM([VoucherCodeSnapshot]))) > 0 AND [VoucherDiscountTypeSnapshot] IS NOT NULL AND [VoucherDiscountValueSnapshot] IS NOT NULL AND (([VoucherDiscountTypeSnapshot] = ''Percent'' AND [VoucherDiscountValueSnapshot] > 0 AND [VoucherDiscountValueSnapshot] <= 100) OR ([VoucherDiscountTypeSnapshot] = ''Fixed'' AND [VoucherDiscountValueSnapshot] > 0 AND [VoucherDiscountValueSnapshot] = ROUND([VoucherDiscountValueSnapshot], 0)))))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [OrderDetails] ADD CONSTRAINT [CK_OrderDetails_NameSnapshot] CHECK (LEN(LTRIM(RTRIM([ProductNameSnapshot]))) > 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [OrderDetails] ADD CONSTRAINT [CK_OrderDetails_Values] CHECK ([Quantity] > 0 AND [UnitPrice] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [UnitPrice] * [Quantity])');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [Invoices] ADD CONSTRAINT [CK_Invoices_Amounts] CHECK ([Subtotal] >= 0 AND [Discount] >= 0 AND [Discount] <= [Subtotal] AND [TotalAmount] = [Subtotal] - [Discount])');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [Invoices] ADD CONSTRAINT [CK_Invoices_Status] CHECK ([Status] IN (''Unpaid'',''Paid'',''Cancelled''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InvoiceOrders_OrderId] ON [InvoiceOrders] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_InvoiceDetails_OrderDetailId] ON [InvoiceDetails] ([OrderDetailId]) WHERE [OrderDetailId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_InvoiceDetails_SessionChargeId] ON [InvoiceDetails] ([SessionChargeId]) WHERE [SessionChargeId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [InvoiceDetails] ADD CONSTRAINT [CK_InvoiceDetails_Source] CHECK (([OrderDetailId] IS NOT NULL AND [SessionChargeId] IS NULL) OR ([OrderDetailId] IS NULL AND [SessionChargeId] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [InvoiceDetails] ADD CONSTRAINT [CK_InvoiceDetails_Values] CHECK ([Quantity] > 0 AND [UnitPrice] >= 0 AND [Amount] >= 0 AND [Amount] <= [UnitPrice] * [Quantity])');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_InventoryTransactions_OrderDetailId_Type] ON [InventoryTransactions] ([OrderDetailId], [Type]) WHERE [OrderDetailId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [InventoryTransactions] ADD CONSTRAINT [CK_InventoryTransactions_Reason] CHECK (LEN(LTRIM(RTRIM([Reason]))) BETWEEN 1 AND 200)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [InventoryTransactions] ADD CONSTRAINT [CK_InventoryTransactions_Type] CHECK ((([Type] IN (''Receive'',''Return'') AND [Quantity] > 0) OR ([Type] = ''Sale'' AND [Quantity] < 0) OR ([Type] = ''Adjust'' AND [Quantity] <> 0)) AND (([Type] IN (''Receive'',''Adjust'') AND [OrderDetailId] IS NULL) OR ([Type] IN (''Sale'',''Return'') AND [OrderDetailId] IS NOT NULL AND [OrderId] IS NOT NULL)))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'ALTER TABLE [InventoryTransactions] ADD CONSTRAINT [CK_InventoryTransactions_Values] CHECK ([Before] >= 0 AND [After] >= 0 AND CAST([Before] AS bigint) + [Quantity] = [After])');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InvoiceIssueRequests_InvoiceId] ON [InvoiceIssueRequests] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE INDEX [IX_PromotionUsages_CustomerId] ON [PromotionUsages] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PromotionUsages_OrderId] ON [PromotionUsages] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_PromotionUsages_PromotionId_CustomerId] ON [PromotionUsages] ([PromotionId], [CustomerId]) WHERE [ReleasedAt] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE INDEX [IX_RefundAttempts_PaymentId] ON [RefundAttempts] ([PaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_RefundAttempts_ProviderAppId_GatewayRefundId] ON [RefundAttempts] ([ProviderAppId], [GatewayRefundId]) WHERE [GatewayRefundId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefundAttempts_ProviderAppId_MerchantRefundId] ON [RefundAttempts] ([ProviderAppId], [MerchantRefundId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [InventoryTransactions] ADD CONSTRAINT [FK_InventoryTransactions_OrderDetails_OrderDetailId] FOREIGN KEY ([OrderDetailId]) REFERENCES [OrderDetails] ([OrderDetailId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [InvoiceDetails] ADD CONSTRAINT [FK_InvoiceDetails_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [InvoiceDetails] ADD CONSTRAINT [FK_InvoiceDetails_OrderDetails_OrderDetailId] FOREIGN KEY ([OrderDetailId]) REFERENCES [OrderDetails] ([OrderDetailId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [InvoiceDetails] ADD CONSTRAINT [FK_InvoiceDetails_SessionCharges_SessionChargeId] FOREIGN KEY ([SessionChargeId]) REFERENCES [SessionCharges] ([ChargeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [OrderDetails] ADD CONSTRAINT [FK_OrderDetails_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([InvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_OrderDetails_OrderDetailId] FOREIGN KEY ([OrderDetailId]) REFERENCES [OrderDetails] ([OrderDetailId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_Payments_RefundOfPaymentId] FOREIGN KEY ([RefundOfPaymentId]) REFERENCES [Payments] ([PaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_SessionCharges_SessionChargeId] FOREIGN KEY ([SessionChargeId]) REFERENCES [SessionCharges] ([ChargeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_WalletTransactions_WalletTransactionId] FOREIGN KEY ([WalletTransactionId]) REFERENCES [WalletTransactions] ([TransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261107071954_IntegrateInventoryInvoiceVoucherContracts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261107071954_IntegrateInventoryInvoiceVoucherContracts', N'10.0.14');
END;

COMMIT;
GO
