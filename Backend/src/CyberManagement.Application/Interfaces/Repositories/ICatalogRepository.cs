using CyberManagement.Application.DTOs.Catalog;

namespace CyberManagement.Application.Interfaces.Repositories;

public interface ICatalogRepository
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(bool menuOnly, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(bool menuOnly, CancellationToken cancellationToken);
    Task<CategoryDto> SaveCategoryAsync(int? id, SaveCategoryCommand command, CancellationToken cancellationToken);
    Task<ProductDto> SaveProductAsync(int? id, SaveProductCommand command, CancellationToken cancellationToken);
    Task DisableCategoryAsync(int id, CancellationToken cancellationToken);
    Task DisableProductAsync(int id, CancellationToken cancellationToken);
}
