/*
DU LIEU TEST API DOC HOA DON - chi dung tren DB phat trien CyberManagement.
Tao 2 khach, 4 don, 1 phien da ket thuc va 5 hoa don.
Cac nguon don/phien la fixture toi thieu, khong mo phong nghiep vu:
khong tao ledger/Payment, chi tiet mon, khong tru vi/kho.
Khong dung bo du lieu nay de doi soat doanh thu hay test dat/huy mon.
Khong xoa/ghi de du lieu. Chay trung se bao loi truoc khi chen.
ID do SQL sinh; xem bang ket qua cuoi de lay CustomerId va InvoiceId.
*/
USE [CyberManagement];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20261102025517_AddVoucherAndInvoiceContracts'
    )
        THROW 52100, N'Chua ap dung migration hoa don/voucher.', 1;

    IF EXISTS (
        SELECT 1 FROM dbo.Customers WITH (UPDLOCK, HOLDLOCK)
        WHERE UserName IN (N'__invoice_test_a', N'__invoice_test_b')
    )
        THROW 52101, N'Da co khach test hoa don. Khong chen trung hoac ghi de.', 1;

    DECLARE @CustomerA bigint, @CustomerB bigint;
    DECLARE @OrderA1 bigint, @OrderA2 bigint, @OrderB1 bigint, @OrderB2 bigint;
    DECLARE @RoomId bigint, @ComputerTypeId bigint, @ComputerId bigint, @SessionId bigint;
    DECLARE @FixtureInvoices TABLE (InvoiceId bigint PRIMARY KEY);
    DECLARE @BaseTime datetimeoffset = '2026-11-02T09:00:00+07:00';

    -- PasswordHash chi la marker, khong phai mat khau dang nhap.
    -- Dung DevAuth de cap cookie voi CustomerId duoc tra o cuoi script.
    INSERT dbo.Customers
        (FullName, UserName, PasswordHash, Phone, Email,
         WalletBalance, WalletVersion, Status, CreatedAt)
    VALUES
        (N'TEST Invoice A', N'__invoice_test_a', N'!TEST_ONLY_NOT_A_PASSWORD_HASH!',
         N'', N'invoice-a@example.invalid', 0, 0, N'Active',
         CAST(SWITCHOFFSET(@BaseTime, '+00:00') AS datetime2));
    SET @CustomerA = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT dbo.Customers
        (FullName, UserName, PasswordHash, Phone, Email,
         WalletBalance, WalletVersion, Status, CreatedAt)
    VALUES
        (N'TEST Invoice B', N'__invoice_test_b', N'!TEST_ONLY_NOT_A_PASSWORD_HASH!',
         N'', N'invoice-b@example.invalid', 0, 0, N'Active',
         CAST(SWITCHOFFSET(@BaseTime, '+00:00') AS datetime2));
    SET @CustomerB = CONVERT(bigint, SCOPE_IDENTITY());

    -- Khong dung voucher o fixture nay: DiscountAmount = 0.
    INSERT dbo.Orders (CustomerId, OrderTime, Status, TotalAmount, DiscountAmount)
    VALUES (@CustomerA, '2026-11-02T02:00:00', N'Confirmed', 100000, 0);
    SET @OrderA1 = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT dbo.Orders (CustomerId, OrderTime, Status, TotalAmount, DiscountAmount)
    VALUES (@CustomerA, '2026-11-02T02:10:00', N'Pending', 60000, 0);
    SET @OrderA2 = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT dbo.Orders (CustomerId, OrderTime, Status, TotalAmount, DiscountAmount)
    VALUES (@CustomerB, '2026-11-02T02:20:00', N'Mixed', 80000, 0);
    SET @OrderB1 = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT dbo.Orders (CustomerId, OrderTime, Status, TotalAmount, DiscountAmount)
    VALUES (@CustomerB, '2026-11-02T02:30:00', N'Cancelled', 0, 0);
    SET @OrderB2 = CONVERT(bigint, SCOPE_IDENTITY());

    -- Tao rieng may/phong test de khong thay doi may dang su dung.
    INSERT dbo.ComputerRooms (RoomName, Floor, Description)
    VALUES (N'__invoice_test_room', 0, N'Fixture API doc hoa don');
    SET @RoomId = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT dbo.ComputerTypes
        (ComputerTypeName, CPU, RAM, GPU, PricePerHour, Description)
    VALUES
        (N'__invoice_test_type', N'Test', N'Test', N'Test', 20000,
         N'Fixture API doc hoa don');
    SET @ComputerTypeId = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT dbo.Computers (RoomId, ComputerTypeId, ComputerName, Status)
    VALUES (@RoomId, @ComputerTypeId, N'__invoice_test_pc', 1);
    SET @ComputerId = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT dbo.GamingSessions
        (ComputerId, CustomerId, StartTime, EndTime, LastChargedAt, TotalCost, Status)
    VALUES
        (@ComputerId, @CustomerA, '2026-11-02T01:00:00',
         '2026-11-02T02:40:00', '2026-11-02T02:40:00', 30000, N'Ended');
    SET @SessionId = CONVERT(bigint, SCOPE_IDENTITY());

    -- Thu tu phat hanh co chu dich de kiem tra sap xep moi nhat truoc.
    INSERT dbo.Invoices
        (CustomerId, OrderId, SessionId, SubTotal, Discount,
         GrossAmount, RefundAmount, PaidAmount, Status, IssuedAt)
    OUTPUT inserted.InvoiceId INTO @FixtureInvoices
    VALUES
        (@CustomerA, @OrderA1, NULL, 100000, 0,
         100000, 0, 100000, N'Paid', @BaseTime),
        (@CustomerA, @OrderA2, NULL, 60000, 0,
         60000, 0, 0, N'Pending', DATEADD(minute, 10, @BaseTime)),
        (@CustomerB, @OrderB1, NULL, 100000, 0,
         100000, 20000, 80000, N'Paid', DATEADD(minute, 20, @BaseTime)),
        (@CustomerB, @OrderB2, NULL, 50000, 0,
         50000, 50000, 0, N'Cancelled', DATEADD(minute, 30, @BaseTime)),
        (@CustomerA, NULL, @SessionId, 30000, 0,
         30000, 0, 30000, N'Paid', DATEADD(minute, 40, @BaseTime));

    COMMIT TRANSACTION;

    SELECT CustomerId, UserName,
           CASE WHEN CustomerId = @CustomerA THEN 3 ELSE 2 END AS ExpectedInvoiceCount
    FROM dbo.Customers
    WHERE CustomerId IN (@CustomerA, @CustomerB);

    SELECT i.InvoiceId, i.CustomerId, i.OrderId, i.SessionId,
           i.GrossAmount, i.RefundAmount, i.PaidAmount, i.Status, i.IssuedAt
    FROM dbo.Invoices i
    JOIN @FixtureInvoices f ON f.InvoiceId = i.InvoiceId
    ORDER BY i.IssuedAt DESC, i.InvoiceId DESC;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
