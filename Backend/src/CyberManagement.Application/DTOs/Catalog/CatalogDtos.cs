using System.Text.Json.Serialization;

namespace CyberManagement.Application.DTOs.Catalog;

public sealed record CategoryDto(int Id, string Name, bool IsActive);
public sealed record ProductDto(int Id, string Name, int CategoryId, decimal CurrentPrice, int StockQuantity, bool IsActive, string? ImageUrl = null);
public sealed record MenuDto(IReadOnlyList<CategoryDto> Categories, IReadOnlyList<ProductDto> Products);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveCategoryCommand
{
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveProductCommand
{
    private string? _imageUrl;

    public required string Name { get; init; }
    public required int CategoryId { get; init; }
    public required decimal CurrentPrice { get; init; }
    public required bool IsActive { get; init; }

    public string? ImageUrl
    {
        get => _imageUrl;
        init { _imageUrl = value; ImageUrlSpecified = true; }
    }

    // Older callers can edit price/name without clearing an existing photo.
    [JsonIgnore]
    public bool ImageUrlSpecified { get; private init; }
}
