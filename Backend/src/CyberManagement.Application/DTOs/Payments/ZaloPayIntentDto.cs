using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.DTOs.Payments;

public sealed record ZaloPayIntentDto(int Id, int CustomerId, string Purpose, int SourceId,
    decimal ExpectedAmountVnd, string Status, string? PaymentUrl, string MerchantTransactionId,
    DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc, string? ProviderTransactionId,
    decimal? ProviderAmountVnd, DateTimeOffset? VerifiedAtUtc, bool RequiresReconciliation, string? FailureReason,
    DateTimeOffset? AppliedAtUtc = null, int? WalletTransactionId = null, int? PaymentId = null,
    string? SettlementFailureReason = null);

public sealed record ZaloPayIntentPageDto(IReadOnlyList<ZaloPayIntentDto> Items, int Page, int PageSize, int TotalCount);
public sealed record ZaloPayConfigurationDto(bool Enabled, decimal MinAmountVnd, decimal MaxAmountVnd);

public sealed record GatewayIntentCreation(ZaloPayIntent Intent, bool Created);
public sealed record GatewayQueryLease(ZaloPayIntent Intent, Guid? Token, bool Acquired);
public sealed record GatewayCreateLease(ZaloPayIntent Intent, Guid? Token, bool Acquired);
public sealed record GatewayReceiptResult(ZaloPayIntent Intent, bool Recorded);
