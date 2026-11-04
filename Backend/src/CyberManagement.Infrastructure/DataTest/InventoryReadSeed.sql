-- Chỉ seed fixture đọc kho vào DB thử SCRUM-13; không sửa dữ liệu nghiệp vụ.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_WARNINGS ON;

IF DB_NAME() NOT LIKE N'CyberManagement[_]SCRUM13[_]%'
    THROW 51000, 'Seed requires a separate CyberManagement_SCRUM13_ database.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
               WHERE MigrationId = N'20261102084459_RemoveEmployeeLoginLockout')
    THROW 51000, 'Apply the CyberManagement 35-table migrations before seed.', 1;

BEGIN TRANSACTION;
DECLARE @CategoryId int;
DECLARE @CategoryCount int;
SELECT @CategoryCount = COUNT(*), @CategoryId = MIN(CategoryId)
FROM dbo.Categories WITH (UPDLOCK, HOLDLOCK)
WHERE CategoryName = N'SCRUM13_TEST_INVENTORY';

IF @CategoryCount > 1
    THROW 51000, 'Duplicate test categories; inspect this test database.', 1;

IF @CategoryId IS NULL
BEGIN
    INSERT dbo.Categories (CategoryName, IsActive) VALUES (N'SCRUM13_TEST_INVENTORY', 1);
    SET @CategoryId = CONVERT(int, SCOPE_IDENTITY());
END;

DECLARE @Expected TABLE (ProductName nvarchar(150), Stock int, IsActive bit, Price decimal(12,2));
INSERT @Expected VALUES
    (N'TEST_Nước suối', 20, 1, 10000),
    (N'TEST_Mì ly hết hàng', 0, 1, 20000),
    (N'TEST_Coca ngừng bán', 5, 0, 15000);

-- Chạy lại không thêm dòng hoặc ghi đè tồn/giá đã thay đổi.
IF EXISTS (SELECT 1 FROM dbo.Products WHERE CategoryId = @CategoryId)
BEGIN
    IF (SELECT COUNT(*) FROM dbo.Products WHERE CategoryId = @CategoryId) <> 3
       OR EXISTS (
          SELECT ProductName, Stock, IsActive, Price FROM @Expected
          EXCEPT
          SELECT ProductName, Stock, IsActive, Price FROM dbo.Products WHERE CategoryId = @CategoryId)
        THROW 51000, 'Existing test fixtures differ; inspect them without resetting data.', 1;
END
ELSE
    INSERT dbo.Products (CategoryId, ProductName, Stock, IsActive, Price)
    SELECT @CategoryId, ProductName, Stock, IsActive, Price FROM @Expected;

COMMIT TRANSACTION;
