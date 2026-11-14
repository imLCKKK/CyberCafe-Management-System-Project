namespace CyberManagement.Application.DTOs.Computers;

public sealed record UpdateComputerStatusCommand(string Status, string ExpectedStatus);
