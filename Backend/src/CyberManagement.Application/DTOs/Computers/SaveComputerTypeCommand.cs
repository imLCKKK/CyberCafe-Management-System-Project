using System.ComponentModel.DataAnnotations;

namespace CyberManagement.Application.DTOs.Computers;

public sealed class SaveComputerTypeCommand
{
    [Required, StringLength(100)]
    public string TypeName { get; init; } = "";
    [Range(typeof(decimal), "1", "999999999999999999")]
    public decimal PricePerHour { get; init; }
    [StringLength(100)]
    public string? CPU { get; init; }
    [StringLength(50)]
    public string? RAM { get; init; }
    [StringLength(100)]
    public string? GPU { get; init; }
    [StringLength(255)]
    public string? Description { get; init; }
}
