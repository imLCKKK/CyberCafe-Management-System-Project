namespace CyberManagement.Application.Interfaces;

// Lấy thông tin từ ngữ cảnh đã xác thực ở server, không lấy từ nội dung request.
public interface ICurrentUser
{
    long AccountId { get; }
    long? CustomerId { get; }
    long? EmployeeId { get; }
    string Role { get; }
    bool IsActive { get; }
    // Cờ phục vụ luồng tài khoản nếu cần; không phải điều kiện để khách được đặt món.
    bool MustChangePassword { get; }
}
