namespace CyberManagement.Application.DTOs.Payments;

// Created by trusted server use cases from the committed, immutable payment intent.
public sealed record GatewayCreateRequest(int IntentId, string Purpose, int CustomerId, int? OrderId,
    int AppId, string MerchantTransactionId, decimal AmountVnd,
    DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc);

public sealed record GatewayCreateResult(string? PaymentUrl, bool Rejected, string? FailureReason);
public sealed record GatewayReceipt(int AppId, string TransactionId, decimal AmountVnd, DateTimeOffset? ProviderOccurredAtUtc);
public sealed record GatewayQueryResult(string State, GatewayReceipt? Receipt, string? FailureReason, bool RetryableFailure = false);
public sealed record GatewayCallback(int AppId, string MerchantTransactionId, string AppUser,
    long AppTime, decimal AmountVnd, string TransactionId, DateTimeOffset ProviderOccurredAtUtc);
