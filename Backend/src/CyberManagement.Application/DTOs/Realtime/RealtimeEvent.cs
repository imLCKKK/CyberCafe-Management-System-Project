namespace CyberManagement.Application.DTOs.Realtime;

public sealed record RealtimeEvent(Guid EventId, string Type, int EntityId, DateTimeOffset OccurredAtUtc)
{
    public const string ComputerTypesChanged = "ComputerTypesChanged";
    public const string MaintenanceChanged = "MaintenanceChanged";
    public const string ComputerChanged = "ComputerChanged";
    public const string TournamentChanged = "TournamentChanged";
    public const string RegistrationChanged = "RegistrationChanged";
    public const string PromotionChanged = "PromotionChanged";
}

public enum RealtimeAccountType
{
    Staff,
    Customer
}
