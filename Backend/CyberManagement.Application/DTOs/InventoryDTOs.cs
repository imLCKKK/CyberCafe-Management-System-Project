using System.ComponentModel.DataAnnotations;

namespace CyberManagement.Application.DTOs;

// Hợp đồng GET /api/admin/inventory, JSON camelCase theo cấu hình ASP.NET Core.
public sealed class InventoryItemDto
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int StockQuantity { get; init; }
    public bool IsActive { get; init; }
    public decimal CurrentPrice { get; init; }
    public long CategoryId { get; init; }
}

public sealed class InventoryMovementDto
{
    public long Id { get; init; }
    public long ProductId { get; init; }
    public string Type { get; init; } = string.Empty;
    public int QuantityDelta { get; init; }
    public int StockAfter { get; init; }
    public string Reason { get; init; } = string.Empty;
    public long? OrderItemId { get; init; }
    public long? RecordedByAccountId { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

// Sale/Return chỉ được ghi bởi transaction đặt/hủy món, không nhận từ API kho.
public sealed class CreateInventoryMovementRequest : IValidatableObject
{
    [Range(typeof(long), "1", "9223372036854775807")]
    public long ProductId { get; init; }

    [Required]
    [RegularExpression("^(Receive|Adjust)$")]
    public string Type { get; init; } = string.Empty;

    [Range(-1000000, 1000000)]
    public int QuantityDelta { get; init; }

    [Required]
    [MaxLength(200)]
    public string Reason { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (QuantityDelta == 0)
            yield return new ValidationResult(
                "QuantityDelta must not be zero.", [nameof(QuantityDelta)]);

        if (Type == "Receive" && QuantityDelta < 0)
            yield return new ValidationResult(
                "Receive must increase stock.", [nameof(QuantityDelta)]);
    }
}

public sealed class UpdateProductStatusRequest
{
    // Nullable + Required phân biệt false hợp lệ với body thiếu trường isActive.
    [Required]
    public bool? IsActive { get; init; }
}