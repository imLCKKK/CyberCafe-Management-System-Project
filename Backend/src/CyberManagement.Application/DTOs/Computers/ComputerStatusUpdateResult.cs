namespace CyberManagement.Application.DTOs.Computers;

public sealed record ComputerStatusUpdateResult(ComputerDto Computer, bool Changed);
