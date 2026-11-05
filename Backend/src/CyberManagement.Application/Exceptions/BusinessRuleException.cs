namespace CyberManagement.Application.Exceptions;

// Lỗi quy tắc nghiệp vụ để API trả 409
public class BusinessRuleException : InvalidOperationException
{
    public BusinessRuleException(string message) : base(message) { }
}
