/*
SCRUM-13: dữ liệu kiểm thử luồng ĐỌC tồn kho.
Chỉ chạy toàn bộ file trên DB local CyberCafe_SCRUM13_Test.
Tạo 1 danh mục và 3 sản phẩm; không xóa/ghi đè dữ liệu.
Số tồn là dữ liệu fixture, không chứng minh luồng nhập/trừ/hoàn kho.
Script không tạo giao dịch ví, đơn hoặc lịch sử biến động.
*/
USE [CyberCafe_SCRUM13_Test];
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
        SELECT 1
        FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20261102061018_AddInventoryProductFields'
    )
        THROW 52200, N'Chưa áp dụng migration AddInventoryProductFields.', 1;

    IF EXISTS (
        SELECT 1
        FROM dbo.product_categories WITH (UPDLOCK, HOLDLOCK)
        WHERE name = N'SCRUM13_TEST_INVENTORY'
    )
        THROW 52201, N'Seed đã tồn tại. Không chèn trùng hoặc ghi đè.', 1;

    DECLARE @CategoryId bigint;

    INSERT dbo.product_categories (name)
    VALUES (N'SCRUM13_TEST_INVENTORY');

    SET @CategoryId = CONVERT(bigint, SCOPE_IDENTITY());

    INSERT dbo.products
        (category_id, name, current_price, stock_quantity, is_active)
    VALUES
        (@CategoryId, N'TEST_Nước suối', 10000, 20, 1),
        (@CategoryId, N'TEST_Mì ly hết hàng', 20000, 0, 1),
        (@CategoryId, N'TEST_Coca ngừng bán', 15000, 5, 0);

    COMMIT TRANSACTION;

    SELECT id, name, stock_quantity, is_active, current_price, category_id
    FROM dbo.products
    WHERE category_id = @CategoryId
    ORDER BY id;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
