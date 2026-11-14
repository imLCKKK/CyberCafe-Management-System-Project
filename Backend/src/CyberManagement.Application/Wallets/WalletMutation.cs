using CyberManagement.Domain.Entities;

namespace CyberManagement.Application.Wallets;

public enum WalletMutationKind { Credit, Debit, Refund }

public sealed record WalletActor(string Type, int? EmployeeId = null);

public sealed record WalletMutation(
    int CustomerId,
    WalletMutationKind Kind,
    decimal Amount,
    string Type,
    string SourceType,
    string SourceId,
    string OperationKey,
    WalletActor Actor,
    DateTimeOffset OccurredAtUtc,
    int? OriginalDebitId = null,
    string? Description = null);

public sealed record WalletMutationResult(WalletTransaction Entry, bool Replayed);
