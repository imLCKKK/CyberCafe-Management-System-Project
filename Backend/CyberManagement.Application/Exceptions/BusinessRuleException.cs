namespace CyberManagement.Application.Exceptions;

// Lỗi quy tắc nghiệp vụ để API trả 409, phân biệt với lỗi cấu hình hoặc lỗi lập trình.
public class BusinessRuleException : InvalidOperationException
{
    public BusinessRuleException(string message) : base(message) { }
}
