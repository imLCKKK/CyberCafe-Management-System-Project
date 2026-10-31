using CyberManagement.Application.DTOs;

namespace CyberManagement.Application.Validators;

public static class CreateOrderRequestValidator
{
    // Chỉ kiểm tra dữ liệu đầu vào; quyền truy cập, phiên chơi, sản phẩm và ví được kiểm tra tại service.
    // Báo lỗi ngay khi request thiếu dữ liệu, ID không hợp lệ hoặc số lượng vượt giới hạn; không truy vấn database.
    public static void Validate(CreateOrderRequest request)
    {
        // Từ chối request null trước khi đọc các thuộc tính bên trong.
        ArgumentNullException.ThrowIfNull(request);

        // Chỉ kiểm tra ID phiên có giá trị dương; phiên có tồn tại và còn Active hay không do service kiểm tra.
        if (request.SessionId is not > 0)
            throw new ArgumentException("An active gaming session is required.");

        // Giỏ phải có món, không có dòng null, ProductId dương và số lượng từng dòng từ 1 đến 99.
        if (request.Items == null || request.Items.Count == 0 || request.Items.Any(i =>
            i == null || i.ProductId <= 0 || i.Quantity < 1 || i.Quantity > 99))
            throw new ArgumentException("Choose at least one product, with 1 to 99 units per product.");

        // Kiểm tra cả tổng số lượng của sản phẩm trùng ID, tránh vượt giới hạn khi gộp các dòng trong giỏ.
        if (request.Items.GroupBy(i => i.ProductId).Any(g => g.Sum(i => (long)i.Quantity) > 99))
            throw new ArgumentException("The combined quantity per product cannot exceed 99.");
    }
}
