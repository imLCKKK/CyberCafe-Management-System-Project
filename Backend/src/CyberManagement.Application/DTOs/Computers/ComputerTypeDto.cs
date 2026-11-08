namespace CyberManagement.Application.DTOs.Computers;

public sealed record ComputerTypeDto(
    int ComputerTypeId,
    string TypeName,
    string? CPU,
    string? RAM,
    string? GPU,
    decimal PricePerHour,
    string? Description);
