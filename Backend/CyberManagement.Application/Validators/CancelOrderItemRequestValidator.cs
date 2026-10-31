namespace CyberManagement.Application.Validators;

public static class CancelOrderItemRequestValidator
{
    // Dùng ở service để mọi bên gọi đều phải tuân thủ quy tắc lý do hủy, không chỉ request HTTP.
    public static void Validate(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Vui lòng nhập lý do hủy món.");
        if (reason.Length > 250)
            throw new ArgumentException("Lý do hủy món không được vượt quá 250 ký tự.");
    }
}
