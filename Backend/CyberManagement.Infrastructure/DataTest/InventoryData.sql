/*
DỮ LIỆU KIỂM THỬ KHO CYBERMANAGEMENT
Nguồn: 02_CyberCafe_Synthetic_Data.sql (dữ liệu giả lập).
Tách 6 danh mục và 24 sản phẩm; giữ ID, tên, giá và trạng thái bán.
Stock được đặt về 0 có chủ đích: dùng API Receive để tạo tồn và lịch sử.
Không nhập lịch sử IN/OUT cũ, hóa đơn hoặc giao dịch ví của schema khác.
Không xóa hoặc ghi đè dữ liệu. Nếu trùng ID, toàn bộ thao tác bị hủy.
IDENTITY_INSERT giữ ID gốc từ 1000001, làm bộ đếm ID tăng theo.
Chỉ chạy toàn bộ file trong database kiểm thử CyberManagement.
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

    IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
       OR OBJECT_ID(N'dbo.Products', N'U') IS NULL
        THROW 52001, N'Thiếu bảng Categories hoặc Products. Chưa chèn dữ liệu.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Categories WITH (UPDLOCK, HOLDLOCK)
               WHERE CategoryId IN (1000001, 1000002, 1000003, 1000004, 1000005, 1000006))
       OR EXISTS (SELECT 1 FROM dbo.Products WITH (UPDLOCK, HOLDLOCK)
                  WHERE ProductId IN (1000001, 1000002, 1000003, 1000004, 1000005, 1000006, 1000007, 1000008, 1000009, 1000010, 1000011, 1000012, 1000013, 1000014, 1000015, 1000016, 1000017, 1000018, 1000019, 1000020, 1000021, 1000022, 1000023, 1000024))
        THROW 52002, N'Trùng ID dữ liệu kiểm thử. Không xóa hoặc ghi đè dữ liệu.', 1;

    SET IDENTITY_INSERT dbo.Categories ON;
    INSERT INTO dbo.Categories (CategoryId, CategoryName, IsActive)
    VALUES
    (1000001, N'SYN_Nuoc Dong Chai', 1),
    (1000002, N'SYN_Nuoc Tang Luc', 1),
    (1000003, N'SYN_Ca Phe Tra', 1),
    (1000004, N'SYN_Mi An Lien', 1),
    (1000005, N'SYN_Com Mon Nong', 1),
    (1000006, N'SYN_An Vat', 1);
    SET IDENTITY_INSERT dbo.Categories OFF;

    SET IDENTITY_INSERT dbo.Products ON;
    INSERT INTO dbo.Products
        (ProductId, CategoryId, ProductName, Price, Stock, IsActive)
    VALUES
    (1000001, 1000001, N'SYN_Nuoc Suoi', 10000, 0, 1),
    (1000002, 1000001, N'SYN_Coca', 15000, 0, 1),
    (1000003, 1000001, N'SYN_Pepsi', 15000, 0, 1),
    (1000004, 1000001, N'SYN_Sprite', 15000, 0, 1),
    (1000005, 1000002, N'SYN_Sting', 18000, 0, 1),
    (1000006, 1000002, N'SYN_Red Bull', 20000, 0, 1),
    (1000007, 1000002, N'SYN_Monster', 35000, 0, 1),
    (1000008, 1000002, N'SYN_Revive', 18000, 0, 1),
    (1000009, 1000003, N'SYN_Ca Phe Den', 20000, 0, 1),
    (1000010, 1000003, N'SYN_Ca Phe Sua', 25000, 0, 1),
    (1000011, 1000003, N'SYN_Tra Dao', 25000, 0, 1),
    (1000012, 1000003, N'SYN_Tra Chanh', 20000, 0, 1),
    (1000013, 1000004, N'SYN_Mi Trung', 25000, 0, 1),
    (1000014, 1000004, N'SYN_Mi Xuc Xich', 30000, 0, 1),
    (1000015, 1000004, N'SYN_Mi Bo', 40000, 0, 1),
    (1000016, 1000004, N'SYN_Mi Hai San', 45000, 0, 1),
    (1000017, 1000005, N'SYN_Com Ga', 45000, 0, 1),
    (1000018, 1000005, N'SYN_Com Chien', 40000, 0, 1),
    (1000019, 1000005, N'SYN_Banh Mi', 25000, 0, 1),
    (1000020, 1000005, N'SYN_Xuc Xich Chien', 25000, 0, 1),
    (1000021, 1000006, N'SYN_Snack Khoai Tay', 15000, 0, 1),
    (1000022, 1000006, N'SYN_Banh Quy', 12000, 0, 1),
    (1000023, 1000006, N'SYN_Kho Ga', 30000, 0, 1),
    (1000024, 1000006, N'SYN_Dau Phong', 15000, 0, 1);
    SET IDENTITY_INSERT dbo.Products OFF;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    BEGIN TRY
        SET IDENTITY_INSERT dbo.Categories OFF;
        SET IDENTITY_INSERT dbo.Products OFF;
    END TRY
    BEGIN CATCH
        PRINT N'Không đặt lại được IDENTITY_INSERT; đóng cửa sổ query này trước khi chạy lại.';
    END CATCH;

    THROW;
END CATCH;

SELECT ProductId, CategoryId, ProductName, Price, Stock, IsActive
FROM dbo.Products
WHERE ProductId IN (1000001, 1000002, 1000003, 1000004, 1000005, 1000006, 1000007, 1000008, 1000009, 1000010, 1000011, 1000012, 1000013, 1000014, 1000015, 1000016, 1000017, 1000018, 1000019, 1000020, 1000021, 1000022, 1000023, 1000024)
ORDER BY ProductId;
