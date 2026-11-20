namespace CyberManagement.Application.DTOs.Computers;

public sealed record ComputerMaintenanceDto(
    int MaintenanceId, int ComputerId, int EmployeeId, string? Description,
    DateTime StartTime, DateTime? EndTime, string Status);

public sealed record MaintenanceUpdateResult(ComputerMaintenanceDto Maintenance, bool Changed);
