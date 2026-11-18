-- Menu đã chốt ngày 17/11/2026 cho SCRUM-34: 49 món lẻ, 6 combo, 7 món thêm.
-- Chạy thủ công sau EF migrations; không chạy tự động khi API khởi động.
-- Chỉ thêm dòng còn thiếu, giữ nguyên giá/ảnh/trạng thái/tồn của sản phẩm đã có.
-- Tồn sản phẩm mới = 0; combo/món thêm hiện là danh mục sản phẩm, chưa có logic cấu thành.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_WARNINGS ON;

IF DB_NAME() <> N'CyberManagement_Dev'
   AND DB_NAME() NOT LIKE N'CyberManagement[_]AuthChecks[_]%'
    THROW 51000, 'Nexora menu seed requires CyberManagement_Dev or an isolated auth-check database.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
               WHERE MigrationId = N'20261117130111_AddProductImageUrl')
    THROW 51000, 'Apply all CyberManagement migrations including AddProductImageUrl before seeding.', 1;

DECLARE @Menu TABLE (
    CategoryName nvarchar(150) NOT NULL,
    ProductName nvarchar(150) NOT NULL PRIMARY KEY,
    Price decimal(18,0) NOT NULL,
    ImageUrl nvarchar(500) NOT NULL
);
INSERT @Menu (CategoryName, ProductName, Price, ImageUrl) VALUES
    (N'Nước ngọt đóng chai', N'Coca-Cola', 15000, N'/food/coca-cola-v1.640.jpg'),
    (N'Nước ngọt đóng chai', N'Coca-Cola không đường', 15000, N'/food/coca-cola-khong-duong-v1.640.jpg'),
    (N'Nước ngọt đóng chai', N'Pepsi', 15000, N'/food/pepsi-v1.640.jpg'),
    (N'Nước ngọt đóng chai', N'Sprite', 15000, N'/food/sprite-v1.640.jpg'),
    (N'Nước ngọt đóng chai', N'7Up', 15000, N'/food/7up-v1.640.jpg'),
    (N'Nước ngọt đóng chai', N'Fanta cam', 15000, N'/food/fanta-cam-v1.640.jpg'),
    (N'Nước ngọt đóng chai', N'Fanta nho', 15000, N'/food/fanta-nho-v1.640.jpg'),
    (N'Nước ngọt đóng chai', N'Sting dâu', 15000, N'/food/sting-dau-v1.640.jpg'),
    (N'Nước ngọt đóng chai', N'Twister cam', 15000, N'/food/twister-cam-v1.640.jpg'),
    (N'Trà sữa & trà trái cây', N'Trà sữa truyền thống trân châu', 25000, N'/food/tra-sua-truyen-thong-tran-chau-v1.640.jpg'),
    (N'Trà sữa & trà trái cây', N'Trà sữa Thái xanh', 25000, N'/food/tra-sua-thai-xanh-v1.640.jpg'),
    (N'Trà sữa & trà trái cây', N'Trà sữa socola', 30000, N'/food/tra-sua-socola-v1.640.jpg'),
    (N'Trà sữa & trà trái cây', N'Trà sữa khoai môn', 30000, N'/food/tra-sua-khoai-mon-v1.640.jpg'),
    (N'Trà sữa & trà trái cây', N'Trà sữa ô long', 30000, N'/food/tra-sua-o-long-v1.640.jpg'),
    (N'Trà sữa & trà trái cây', N'Sữa tươi trân châu đường đen', 30000, N'/food/sua-tuoi-tran-chau-duong-den-v1.640.jpg'),
    (N'Trà sữa & trà trái cây', N'Trà đào cam sả', 22000, N'/food/tra-dao-cam-sa-v1.640.jpg'),
    (N'Trà sữa & trà trái cây', N'Trà vải', 22000, N'/food/tra-vai-v1.640.jpg'),
    (N'Trà sữa & trà trái cây', N'Trà chanh dây', 22000, N'/food/tra-chanh-day-v1.640.jpg'),
    (N'Trà sữa & trà trái cây', N'Trà chanh', 15000, N'/food/tra-chanh-v1.640.jpg'),
    (N'Trà sữa & trà trái cây', N'Trà tắc mật ong', 20000, N'/food/tra-tac-mat-ong-v1.640.jpg'),
    (N'Cơm', N'Cơm gà xối mỡ', 38000, N'/food/com-ga-xoi-mo-v1.640.jpg'),
    (N'Cơm', N'Cơm gà sốt teriyaki', 38000, N'/food/com-ga-sot-teriyaki-v1.640.jpg'),
    (N'Cơm', N'Cơm gà sốt cay', 38000, N'/food/com-ga-sot-cay-v1.640.jpg'),
    (N'Cơm', N'Cơm bò xào tiêu đen', 42000, N'/food/com-bo-xao-tieu-den-v1.640.jpg'),
    (N'Cơm', N'Cơm sườn nướng', 40000, N'/food/com-suon-nuong-v1.640.jpg'),
    (N'Cơm', N'Cơm chiên trứng', 25000, N'/food/com-chien-trung-v1.640.jpg'),
    (N'Cơm', N'Cơm chiên dương châu', 32000, N'/food/com-chien-duong-chau-v1.640.jpg'),
    (N'Cơm', N'Cơm chiên hải sản', 38000, N'/food/com-chien-hai-san-v1.640.jpg'),
    (N'Mì', N'Mì xào bò', 32000, N'/food/mi-xao-bo-v1.640.jpg'),
    (N'Mì', N'Mì xào hải sản', 38000, N'/food/mi-xao-hai-san-v1.640.jpg'),
    (N'Mì', N'Mì xào trứng xúc xích', 28000, N'/food/mi-xao-trung-xuc-xich-v1.640.jpg'),
    (N'Mì', N'Mì trộn bò', 35000, N'/food/mi-tron-bo-v1.640.jpg'),
    (N'Mì', N'Mì trộn gà cay', 32000, N'/food/mi-tron-ga-cay-v1.640.jpg'),
    (N'Mì', N'Mì trộn trứng xúc xích', 28000, N'/food/mi-tron-trung-xuc-xich-v1.640.jpg'),
    (N'Mì', N'Mì cay gà', 40000, N'/food/mi-cay-ga-v1.640.jpg'),
    (N'Mì', N'Mì cay bò', 42000, N'/food/mi-cay-bo-v1.640.jpg'),
    (N'Mì', N'Mì cay hải sản', 45000, N'/food/mi-cay-hai-san-v1.640.jpg'),
    (N'Mì', N'Mì cay thập cẩm', 50000, N'/food/mi-cay-thap-cam-v1.640.jpg'),
    (N'Ăn vặt', N'Khoai tây chiên', 22000, N'/food/khoai-tay-chien-v2.640.jpg'),
    (N'Ăn vặt', N'Khoai tây lắc phô mai', 25000, N'/food/khoai-tay-lac-pho-mai-v2.640.jpg'),
    (N'Ăn vặt', N'Xúc xích chiên', 12000, N'/food/xuc-xich-chien-v2.640.jpg'),
    (N'Ăn vặt', N'Cá viên chiên', 20000, N'/food/ca-vien-chien-v2.640.jpg'),
    (N'Ăn vặt', N'Bò viên chiên', 25000, N'/food/bo-vien-chien-v2.640.jpg'),
    (N'Ăn vặt', N'Gà viên chiên', 25000, N'/food/ga-vien-chien-v2.640.jpg'),
    (N'Ăn vặt', N'Gà popcorn', 30000, N'/food/ga-popcorn-v2.640.jpg'),
    (N'Ăn vặt', N'Phô mai que', 25000, N'/food/pho-mai-que-v2.640.jpg'),
    (N'Ăn vặt', N'Mẹt viên chiên thập cẩm', 45000, N'/food/met-vien-chien-thap-cam-v2.640.jpg'),
    (N'Ăn vặt', N'Hoành thánh chiên', 25000, N'/food/hoanh-thanh-chien-v2.640.jpg'),
    (N'Ăn vặt', N'Bánh gạo lắc phô mai', 25000, N'/food/banh-gao-lac-pho-mai-v2.640.jpg'),
    (N'Combo', N'Tiếp Trận', 40000, N'/food/combo-tiep-tran-poster-v1.640.jpg'),
    (N'Combo', N'No Bụng', 45000, N'/food/combo-no-bung-poster-v1.640.jpg'),
    (N'Combo', N'Bùng Vị', 55000, N'/food/combo-bung-vi-poster-v1.640.jpg'),
    (N'Combo', N'Ăn Vặt Solo', 40000, N'/food/combo-an-vat-solo-poster-v1.640.jpg'),
    (N'Combo', N'Đồng Đội — 2 người', 110000, N'/food/combo-dong-doi-poster-v1.640.jpg'),
    (N'Combo', N'Gà Giòn Solo', 65000, N'/food/combo-ga-gion-solo-poster-v1.640.jpg'),
    (N'Món thêm', N'Cơm thêm', 5000, N'/food/com-them-v1.640.jpg'),
    (N'Món thêm', N'Mì thêm', 7000, N'/food/mi-them-v1.640.jpg'),
    (N'Món thêm', N'Trứng thêm', 7000, N'/food/trung-them-v1.640.jpg'),
    (N'Món thêm', N'Xúc xích thêm', 8000, N'/food/xuc-xich-them-v1.640.jpg'),
    (N'Món thêm', N'Bò thêm', 15000, N'/food/bo-them-v1.640.jpg'),
    (N'Món thêm', N'Gà thêm', 15000, N'/food/ga-them-v1.640.jpg'),
    (N'Món thêm', N'Hải sản thêm', 20000, N'/food/hai-san-them-v1.640.jpg');

BEGIN TRY
    BEGIN TRANSACTION;
    -- Range locks make repeated/concurrent runs safe without adding catalog constraints.
    IF EXISTS (
        SELECT c.CategoryName FROM dbo.Categories c WITH (UPDLOCK, HOLDLOCK)
        WHERE c.CategoryName IN (SELECT CategoryName FROM @Menu)
        GROUP BY c.CategoryName HAVING COUNT(*) > 1
    )
        THROW 51000, 'Duplicate menu categories; inspect data without resetting it.', 1;

    IF EXISTS (
        SELECT p.ProductName FROM dbo.Products p WITH (UPDLOCK, HOLDLOCK)
        WHERE p.ProductName IN (SELECT ProductName FROM @Menu)
        GROUP BY p.ProductName HAVING COUNT(*) > 1
    )
        THROW 51000, 'Duplicate menu products; inspect data without resetting it.', 1;

    IF EXISTS (
        SELECT 1 FROM dbo.Products p
        JOIN @Menu m ON m.ProductName = p.ProductName
        JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
        WHERE c.CategoryName <> m.CategoryName
    )
        THROW 51000, 'An existing menu product belongs to another category; inspect mapping.', 1;

    INSERT dbo.Categories (CategoryName, IsActive)
    SELECT DISTINCT m.CategoryName, 1 FROM @Menu m
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories c WHERE c.CategoryName = m.CategoryName);
    DECLARE @AddedCategories int = @@ROWCOUNT;

    INSERT dbo.Products (CategoryId, ProductName, Price, Stock, IsActive, ImageUrl)
    SELECT c.CategoryId, m.ProductName, m.Price, 0, 1, m.ImageUrl
    FROM @Menu m JOIN dbo.Categories c ON c.CategoryName = m.CategoryName
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Products p WHERE p.ProductName = m.ProductName);
    DECLARE @AddedProducts int = @@ROWCOUNT;

    COMMIT TRANSACTION;
    SELECT DB_NAME() AS DatabaseName, @AddedCategories AS AddedCategories, @AddedProducts AS AddedProducts,
           (SELECT COUNT(*) FROM dbo.Products p JOIN @Menu m ON p.ProductName = m.ProductName) AS MatchedMenuProducts;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
