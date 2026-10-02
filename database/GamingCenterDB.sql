-- =====================================================
-- Database: GamingCenterDB (SQL Server / T-SQL)  -- v2
-- Thay doi so voi v1: CHECK constraint, index day du,
-- Orders them SessionId/TotalAmount, Invoices nullable FK,
-- seed data mau.
-- =====================================================
SET QUOTED_IDENTIFIER ON;  -- bat buoc cho filtered index
GO

USE master;
GO

IF DB_ID(N'GamingCenterDB') IS NOT NULL
BEGIN
    ALTER DATABASE GamingCenterDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE GamingCenterDB;
END
GO

CREATE DATABASE GamingCenterDB;
GO

USE GamingCenterDB;
GO

-- ---------------------- Roles ------------------------
CREATE TABLE Roles (
    RoleId   INT IDENTITY(1,1) PRIMARY KEY,
    RoleName NVARCHAR(100) NOT NULL UNIQUE
);

-- ---------------------- Employees --------------------
CREATE TABLE Employees (
    EmployeeId   INT IDENTITY(1,1) PRIMARY KEY,
    RoleId       INT NOT NULL,
    FullName     NVARCHAR(150) NOT NULL,
    Username     VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    Phone        VARCHAR(20),
    CONSTRAINT FK_Employees_Roles
        FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);

-- ---------------------- Shifts -----------------------
CREATE TABLE Shifts (
    ShiftId    INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NOT NULL,
    StartTime  DATETIME NOT NULL,
    EndTime    DATETIME NULL,
    CONSTRAINT FK_Shifts_Employees
        FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId),
    CONSTRAINT CK_Shifts_Time CHECK (EndTime IS NULL OR EndTime >= StartTime)
);

-- ---------------------- AuditLogs --------------------
CREATE TABLE AuditLogs (
    LogId       INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId  INT NOT NULL,
    Action      NVARCHAR(255) NOT NULL,
    [Timestamp] DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_AuditLogs_Employees
        FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId)
);

-- ---------------------- Categories -------------------
CREATE TABLE Categories (
    CategoryId   INT IDENTITY(1,1) PRIMARY KEY,
    CategoryName NVARCHAR(150) NOT NULL UNIQUE
);

-- ---------------------- Products ---------------------
CREATE TABLE Products (
    ProductId   INT IDENTITY(1,1) PRIMARY KEY,
    CategoryId  INT NOT NULL,
    ProductName NVARCHAR(150) NOT NULL,
    Price       DECIMAL(12,2) NOT NULL,
    Stock       INT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Products_Categories
        FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
    CONSTRAINT CK_Products_Price CHECK (Price >= 0),
    CONSTRAINT CK_Products_Stock CHECK (Stock >= 0)
);

-- ---------------------- Computers --------------------
CREATE TABLE Computers (
    ComputerId   INT IDENTITY(1,1) PRIMARY KEY,
    ComputerName NVARCHAR(100) NOT NULL UNIQUE,
    Status       NVARCHAR(50) NOT NULL DEFAULT N'Trống',
    PricePerHour DECIMAL(12,2) NOT NULL,
    CONSTRAINT CK_Computers_Status
        CHECK (Status IN (N'Trống', N'Đang chơi', N'Offline', N'Bảo trì')),
    CONSTRAINT CK_Computers_Price CHECK (PricePerHour >= 0)
);

-- ---------------------- Customers --------------------
CREATE TABLE Customers (
    CustomerId    INT IDENTITY(1,1) PRIMARY KEY,
    FullName      NVARCHAR(150) NOT NULL,
    Phone         VARCHAR(20),
    Email         VARCHAR(150),
    WalletBalance DECIMAL(12,2) NOT NULL DEFAULT 0,
    CONSTRAINT CK_Customers_Balance CHECK (WalletBalance >= 0)
);

-- ---------------------- GamingSessions ---------------
CREATE TABLE GamingSessions (
    SessionId  INT IDENTITY(1,1) PRIMARY KEY,
    ComputerId INT NOT NULL,
    CustomerId INT NOT NULL,
    StartTime  DATETIME NOT NULL,
    EndTime    DATETIME NULL,
    TotalCost  DECIMAL(12,2) NULL,
    CONSTRAINT FK_Sessions_Computers
        FOREIGN KEY (ComputerId) REFERENCES Computers(ComputerId),
    CONSTRAINT FK_Sessions_Customers
        FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT CK_Sessions_Time CHECK (EndTime IS NULL OR EndTime >= StartTime),
    CONSTRAINT CK_Sessions_Cost CHECK (TotalCost IS NULL OR TotalCost >= 0)
);

-- ---------------------- Orders -----------------------
-- CustomerId NULL: khach vang lai mua tai quay
-- SessionId  NULL: don khong gan voi phien choi
CREATE TABLE Orders (
    OrderId     INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId  INT NULL,
    EmployeeId  INT NOT NULL,
    SessionId   INT NULL,
    OrderTime   DATETIME NOT NULL DEFAULT GETDATE(),
    Status      NVARCHAR(50) NOT NULL DEFAULT N'Pending',
    TotalAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_Orders_Customers
        FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT FK_Orders_Employees
        FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId),
    CONSTRAINT FK_Orders_Sessions
        FOREIGN KEY (SessionId) REFERENCES GamingSessions(SessionId),
    CONSTRAINT CK_Orders_Status
        CHECK (Status IN (N'Pending', N'Confirmed', N'Paid', N'Cancelled')),
    CONSTRAINT CK_Orders_Total CHECK (TotalAmount >= 0)
);

-- ---------------------- OrderDetails -----------------
CREATE TABLE OrderDetails (
    OrderDetailId INT IDENTITY(1,1) PRIMARY KEY,
    OrderId       INT NOT NULL,
    ProductId     INT NOT NULL,
    Quantity      INT NOT NULL,
    UnitPrice     DECIMAL(12,2) NOT NULL,
    CONSTRAINT FK_OrderDetails_Orders
        FOREIGN KEY (OrderId) REFERENCES Orders(OrderId),
    CONSTRAINT FK_OrderDetails_Products
        FOREIGN KEY (ProductId) REFERENCES Products(ProductId),
    CONSTRAINT CK_OrderDetails_Qty   CHECK (Quantity > 0),
    CONSTRAINT CK_OrderDetails_Price CHECK (UnitPrice >= 0)
);

-- ---------------------- Invoices ---------------------
-- Hoa don co the cho don goi mon, phien choi, hoac ca hai
CREATE TABLE Invoices (
    InvoiceId   INT IDENTITY(1,1) PRIMARY KEY,
    OrderId     INT NULL,
    SessionId   INT NULL,
    TotalAmount DECIMAL(12,2) NOT NULL,
    PaymentTime DATETIME NULL,
    CONSTRAINT FK_Invoices_Orders
        FOREIGN KEY (OrderId) REFERENCES Orders(OrderId),
    CONSTRAINT FK_Invoices_Sessions
        FOREIGN KEY (SessionId) REFERENCES GamingSessions(SessionId),
    CONSTRAINT CK_Invoices_Source CHECK (OrderId IS NOT NULL OR SessionId IS NOT NULL),
    CONSTRAINT CK_Invoices_Total  CHECK (TotalAmount >= 0)
);

-- ---------------------- Payments ---------------------
CREATE TABLE Payments (
    PaymentId   INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId   INT NOT NULL,
    Amount      DECIMAL(12,2) NOT NULL,
    Method      NVARCHAR(50) NULL,
    PaymentTime DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Payments_Invoices
        FOREIGN KEY (InvoiceId) REFERENCES Invoices(InvoiceId),
    CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
    CONSTRAINT CK_Payments_Method
        CHECK (Method IS NULL OR Method IN (N'Cash', N'Wallet', N'Transfer'))
);

-- ---------------------- WalletTransactions -----------
CREATE TABLE WalletTransactions (
    TransactionId INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId    INT NOT NULL,
    Amount        DECIMAL(12,2) NOT NULL,
    [Type]        NVARCHAR(50) NOT NULL,
    CreatedAt     DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_WalletTx_Customers
        FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT CK_WalletTx_Amount CHECK (Amount > 0),
    CONSTRAINT CK_WalletTx_Type
        CHECK ([Type] IN (N'Deposit', N'Payment', N'Refund'))
);
GO

-- ---------------------- Index ------------------------
-- Index cho cac khoa ngoai hay join
CREATE INDEX IX_Employees_Role      ON Employees(RoleId);
CREATE INDEX IX_Shifts_Employee     ON Shifts(EmployeeId);
CREATE INDEX IX_AuditLogs_Employee  ON AuditLogs(EmployeeId);
CREATE INDEX IX_Products_Category   ON Products(CategoryId);
CREATE INDEX IX_Sessions_Computer   ON GamingSessions(ComputerId);
CREATE INDEX IX_Sessions_Customer   ON GamingSessions(CustomerId);
CREATE INDEX IX_Orders_Customer     ON Orders(CustomerId);
CREATE INDEX IX_Orders_Employee     ON Orders(EmployeeId);
CREATE INDEX IX_Orders_Session      ON Orders(SessionId);
CREATE INDEX IX_OrderDetails_Order  ON OrderDetails(OrderId);
CREATE INDEX IX_OrderDetails_Product ON OrderDetails(ProductId);
CREATE INDEX IX_Invoices_Order      ON Invoices(OrderId);
CREATE INDEX IX_Invoices_Session    ON Invoices(SessionId);
CREATE INDEX IX_Payments_Invoice    ON Payments(InvoiceId);
CREATE INDEX IX_WalletTx_Customer   ON WalletTransactions(CustomerId);

-- Moi so dien thoai khach hang chi xuat hien 1 lan (bo qua NULL)
CREATE UNIQUE INDEX UX_Customers_Phone
    ON Customers(Phone) WHERE Phone IS NOT NULL;

-- Moi may chi co toi da 1 phien dang choi (EndTime IS NULL)
CREATE UNIQUE INDEX UX_Sessions_ActivePerComputer
    ON GamingSessions(ComputerId) WHERE EndTime IS NULL;
GO

-- ====================== SEED DATA =====================
INSERT INTO Roles (RoleName) VALUES (N'Admin'), (N'Manager'), (N'Staff');

-- PasswordHash la gia tri tam. Hash that se do app/Seeder tao ra,
-- khong hard-code mat khau that trong script.
INSERT INTO Employees (RoleId, FullName, Username, PasswordHash, Phone) VALUES
(1, N'Quản trị viên',  'admin',     'TEMP_HASH_REPLACE_BY_APP', '0900000001'),
(2, N'Quản lý ca',     'manager01', 'TEMP_HASH_REPLACE_BY_APP', '0900000002'),
(3, N'Nhân viên 01',   'staff01',   'TEMP_HASH_REPLACE_BY_APP', '0900000003');

INSERT INTO Computers (ComputerName, Status, PricePerHour) VALUES
(N'PC01', N'Trống',     8000),
(N'PC02', N'Trống',     8000),
(N'PC03', N'Bảo trì',   8000),
(N'PC04', N'Trống',     8000),
(N'PC05', N'Trống',    10000),
(N'PC06', N'Trống',    10000),
(N'PC07', N'Offline',  10000),
(N'PC08', N'Trống',    12000);

INSERT INTO Customers (FullName, Phone, Email, WalletBalance) VALUES
(N'Nguyễn Văn A', '0911111111', 'a@example.com', 50000),
(N'Trần Thị B',   '0922222222', 'b@example.com', 100000),
(N'Lê Văn C',     '0933333333', NULL,            0);

INSERT INTO Categories (CategoryName) VALUES
(N'Nước uống'), (N'Mì & đồ ăn nhanh'), (N'Snack');

INSERT INTO Products (CategoryId, ProductName, Price, Stock) VALUES
(1, N'Sting',          12000, 50),
(1, N'Nước suối',       8000, 60),
(1, N'Coca-Cola',      12000, 40),
(2, N'Mì gói',         15000, 40),
(2, N'Mì ly',          20000, 30),
(3, N'Snack khoai tây', 10000, 45),
(3, N'Snack bắp',       8000, 45);

-- Lich su nap tien cho cac khach co so du ban dau
INSERT INTO WalletTransactions (CustomerId, Amount, [Type]) VALUES
(1,  50000, N'Deposit'),
(2, 100000, N'Deposit');
GO