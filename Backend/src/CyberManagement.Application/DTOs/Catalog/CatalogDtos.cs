using System.Text.Json.Serialization;

namespace CyberManagement.Application.DTOs.Catalog;

public sealed record CategoryDto(int Id, string Name, bool IsActive);
public sealed record ProductDto(int Id, string Name, int CategoryId, decimal CurrentPrice, int StockQuantity, bool IsActive);
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
    public required string Name { get; init; }
    public required int CategoryId { get; init; }
    public required decimal CurrentPrice { get; init; }
    public required bool IsActive { get; init; }
}
