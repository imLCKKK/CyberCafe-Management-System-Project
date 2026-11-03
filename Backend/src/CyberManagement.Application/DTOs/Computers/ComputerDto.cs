namespace CyberManagement.Application.DTOs.Computers;

public sealed record ComputerDto(
    int ComputerId,
    string ComputerName,
    int RoomId,
    int ComputerTypeId,
    string Status,
    bool HasOpenSession,
    bool HasOpenMaintenance);
