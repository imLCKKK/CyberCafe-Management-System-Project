namespace CyberManagement.Application.Exceptions;

// HTTP translation belongs to the future preview adapter, not to Application.
public sealed class TeamBalanceException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
