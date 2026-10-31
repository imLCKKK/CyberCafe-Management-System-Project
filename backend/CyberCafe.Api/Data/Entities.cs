namespace CyberCafe.Api.Data;

public enum AccountRole { Customer, Staff, Admin }
public enum TopUpStatus { Pending, Rejected, CreditedUnpaid, Collected }
public enum CollectionMethod { Cash, BankTransfer }
public enum GamingSessionStatus { Active, Ended }
public enum GamingSessionEndReason { CustomerLogout, BalanceDepleted, StaffEnded }
public enum OrderItemStatus { Pending, Confirmed, Cancelled }
public enum CancelRequestStatus { Pending, Approved, Rejected }
public enum TournamentStatus { Open, Closed, TeamsFinalized, Cancelled }
public enum RegistrationFeeStatus { Paid, Refunded }
public enum RegistrationRefundReason { Unassigned, TournamentCancelled }
public enum StatsStatus { Available, Unavailable }
public enum WalletTransactionType { TopUpCredit, GamingCharge, OrderDebit, OrderItemRefund, TournamentFee, TournamentRefund }

public class Account
{
    public long Id { get; set; }
    public string Phone { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public AccountRole Role { get; set; }
    public bool MustChangePassword { get; set; }
    public bool IsActive { get; set; } = true;
    public long? CreatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class Customer
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public decimal WalletBalance { get; set; }
    public long WalletVersion { get; set; }
}

public class TopUp
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public decimal Amount { get; set; }
    public bool RequestedByCustomer { get; set; }
    public TopUpStatus Status { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public long? CreditedByAccountId { get; set; }
    public DateTimeOffset? CreditedAt { get; set; }
    public long? CollectedByAccountId { get; set; }
    public DateTimeOffset? CollectedAt { get; set; }
    public CollectionMethod? CollectionMethod { get; set; }
}

public class WalletTransaction
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public WalletTransactionType TransactionType { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public long? TopUpId { get; set; }
    public long? GamingSessionId { get; set; }
    public long? OrderId { get; set; }
    public long? OrderItemId { get; set; }
    public long? TournamentRegistrationId { get; set; }
    public long? RecordedByAccountId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public class ComputerType
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public decimal HourlyRate { get; set; }
}

public class Computer
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public long ComputerTypeId { get; set; }
    public bool IsUnderMaintenance { get; set; }
}

public class GamingSession
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public long ComputerId { get; set; }
    public decimal HourlyRateSnapshot { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset LastChargedAt { get; set; }
    public decimal ChargedAmount { get; set; }
    public GamingSessionStatus Status { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public GamingSessionEndReason? EndReason { get; set; }
    public long? EndedByAccountId { get; set; }
}

public class ProductCategory
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public class Product
{
    public long Id { get; set; }
    public long CategoryId { get; set; }
    public string Name { get; set; } = "";
    public decimal CurrentPrice { get; set; }
}

public class Order
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public long GamingSessionId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
}

public class OrderItem
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public OrderItemStatus Status { get; set; }
    public long? ConfirmedByAccountId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public long? CancelledByAccountId { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}

public class OrderItemCancelRequest
{
    public long Id { get; set; }
    public long OrderItemId { get; set; }
    public long RequestedByCustomerId { get; set; }
    public CancelRequestStatus Status { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public long? HandledByAccountId { get; set; }
    public DateTimeOffset? HandledAt { get; set; }
}

public class Tournament
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset RegistrationClosesAt { get; set; }
    public TournamentStatus Status { get; set; }
    public long CreatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset? TeamsFinalizedAt { get; set; }
    public long? CancelledByAccountId { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}

public class TournamentRegistration
{
    public long Id { get; set; }
    public long TournamentId { get; set; }
    public long CustomerId { get; set; }
    public string RiotGameName { get; set; } = "";
    public string RiotTagline { get; set; } = "";
    public decimal FeeAmountSnapshot { get; set; } = 50000;
    public RegistrationFeeStatus FeeStatus { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }
    public RegistrationRefundReason? RefundReason { get; set; }
}

public class PlayerStatsSnapshot
{
    public long Id { get; set; }
    public long TournamentRegistrationId { get; set; }
    public StatsStatus Status { get; set; }
    public string? Source { get; set; }
    public string? Scope { get; set; }
    public int? Matches { get; set; }
    public decimal? WinRate { get; set; }
    public decimal? HeadshotRate { get; set; }
    public decimal? DamagePerRound { get; set; }
    public decimal? Kda { get; set; }
    public DateTimeOffset? FetchedAt { get; set; }
    public DateTimeOffset? SourceUpdatedAt { get; set; }
}

public class TeamGenerationRun
{
    public long Id { get; set; }
    public long TournamentId { get; set; }
    public string ModelName { get; set; } = "";
    public string ModelVersion { get; set; } = "";
    public long GeneratedByAccountId { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
}

public class TeamProposal
{
    public long Id { get; set; }
    public long GenerationRunId { get; set; }
    public long TournamentRegistrationId { get; set; }
    public int ProposedTeamNumber { get; set; }
    public decimal? PredictedSkillScore { get; set; }
}

public class Team
{
    public long Id { get; set; }
    public long TournamentId { get; set; }
    public int TeamNumber { get; set; }
    public long FinalizedByAccountId { get; set; }
    public DateTimeOffset FinalizedAt { get; set; }
}

public class TeamMember
{
    public long Id { get; set; }
    public long TeamId { get; set; }
    public long TournamentRegistrationId { get; set; }
}
