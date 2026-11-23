namespace CyberManagement.Application.Exceptions;

public sealed class OpggProviderException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
