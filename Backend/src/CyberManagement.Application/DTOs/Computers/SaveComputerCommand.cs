using System.ComponentModel.DataAnnotations;

namespace CyberManagement.Application.DTOs.Computers;

public sealed class SaveComputerCommand
{
    [Required, StringLength(100)]
    public string ComputerName { get; init; } = "";
    [Range(1, int.MaxValue)]
    public int RoomId { get; init; }
    [Range(1, int.MaxValue)]
    public int ComputerTypeId { get; init; }
}
