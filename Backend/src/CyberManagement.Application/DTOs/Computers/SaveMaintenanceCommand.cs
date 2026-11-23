using System.ComponentModel.DataAnnotations;

namespace CyberManagement.Application.DTOs.Computers;

public sealed class SaveMaintenanceCommand
{
    [StringLength(255)]
    public string? Description { get; init; }
}
