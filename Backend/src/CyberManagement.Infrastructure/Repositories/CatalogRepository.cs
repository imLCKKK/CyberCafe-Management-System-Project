using CyberManagement.Application.DTOs.Catalog;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Repositories;

public sealed class CatalogRepository(ApplicationDbContext database) : ICatalogRepository
{
    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(bool menuOnly, CancellationToken ct) =>
        await database.Categories.AsNoTracking().Where(c => !menuOnly || c.IsActive)
            .OrderBy(c => c.CategoryId).Select(c => new CategoryDto(c.CategoryId, c.CategoryName, c.IsActive)).ToListAsync(ct);

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(bool menuOnly, CancellationToken ct) =>
        await database.Products.AsNoTracking()
            .Where(p => !menuOnly || (p.IsActive && p.Category.IsActive))
            .OrderBy(p => p.ProductId)
            .Select(p => new ProductDto(p.ProductId, p.ProductName, p.CategoryId, p.Price, p.Stock, p.IsActive)).ToListAsync(ct);

    public async Task<CategoryDto> SaveCategoryAsync(int? id, SaveCategoryCommand command, CancellationToken ct)
    {
        var category = id is null ? new Category() : await CategoryAsync(id.Value, ct);
        category.CategoryName = command.Name;
        category.IsActive = command.IsActive;
        if (id is null) database.Categories.Add(category);
        await database.SaveChangesAsync(ct);
        return new(category.CategoryId, category.CategoryName, category.IsActive);
    }

    public async Task<ProductDto> SaveProductAsync(int? id, SaveProductCommand command, CancellationToken ct)
    {
        var product = id is null ? new Product() : await ProductAsync(id.Value, ct);
        if (!await database.Categories.AnyAsync(c => c.CategoryId == command.CategoryId && c.IsActive, ct))
            throw new BusinessRuleException("Danh mục không tồn tại hoặc đã ngừng sử dụng.");
        product.ProductName = command.Name;
        product.CategoryId = command.CategoryId;
        product.Price = command.CurrentPrice;
        product.IsActive = command.IsActive;
        // Stock belongs to inventory. A catalog edit never writes a quantity or historical order price.
        if (id is null) database.Products.Add(product);
        await database.SaveChangesAsync(ct);
        return new(product.ProductId, product.ProductName, product.CategoryId, product.Price, product.Stock, product.IsActive);
    }

    public async Task DisableCategoryAsync(int id, CancellationToken ct)
    {
        var category = await CategoryAsync(id, ct);
        category.IsActive = false;
        await database.SaveChangesAsync(ct);
    }

    public async Task DisableProductAsync(int id, CancellationToken ct)
    {
        var product = await ProductAsync(id, ct);
        product.IsActive = false;
        await database.SaveChangesAsync(ct);
    }

    private async Task<Category> CategoryAsync(int id, CancellationToken ct) =>
        await database.Categories.SingleOrDefaultAsync(c => c.CategoryId == id, ct)
        ?? throw new EntityNotFoundException("Không tìm thấy danh mục.");

    private async Task<Product> ProductAsync(int id, CancellationToken ct) =>
        await database.Products.SingleOrDefaultAsync(p => p.ProductId == id, ct)
        ?? throw new EntityNotFoundException("Không tìm thấy sản phẩm.");
}
