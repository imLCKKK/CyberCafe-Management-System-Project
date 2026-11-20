namespace CyberManagement.Application.Exceptions;

public sealed class PaymentUnavailableException(string message) : Exception(message);
