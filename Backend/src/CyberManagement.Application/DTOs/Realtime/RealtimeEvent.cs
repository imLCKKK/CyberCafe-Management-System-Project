namespace CyberManagement.Application.DTOs.Realtime;

public sealed record RealtimeEvent(Guid EventId, string Type, int EntityId, DateTimeOffset OccurredAtUtc)
{
    public const string ComputerChanged = "ComputerChanged";
}

public enum RealtimeAccountType
{
    Staff,
    Customer
}
