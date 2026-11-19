namespace CyberManagement.Application.Exceptions;

public sealed class OrderRuleException(string code, string message) : BusinessRuleException(message)
{
    public string Code { get; } = code;
}
