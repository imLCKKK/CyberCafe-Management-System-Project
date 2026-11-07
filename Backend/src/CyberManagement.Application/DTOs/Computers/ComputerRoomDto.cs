namespace CyberManagement.Application.DTOs.Computers;

public sealed record ComputerRoomDto(int RoomId, string RoomName, int Floor, string? Description);
