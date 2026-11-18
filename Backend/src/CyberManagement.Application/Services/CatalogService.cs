using System.ComponentModel.DataAnnotations;
using CyberManagement.Application.DTOs.Catalog;
using CyberManagement.Application.Interfaces.Repositories;

namespace CyberManagement.Application.Services;

public sealed class CatalogService(ICatalogRepository repository)
{
    public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct) => repository.GetCategoriesAsync(false, ct);
    public Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken ct) => repository.GetProductsAsync(false, ct);

    public async Task<MenuDto> GetMenuAsync(CancellationToken ct) =>
        new(await repository.GetCategoriesAsync(true, ct), await repository.GetProductsAsync(true, ct));

    public Task<CategoryDto> SaveCategoryAsync(int? id, SaveCategoryCommand command, CancellationToken ct)
    {
        ValidateId(id);
        var name = ValidateName(command.Name);
        return repository.SaveCategoryAsync(id, command with { Name = name }, ct);
    }

    public Task<ProductDto> SaveProductAsync(int? id, SaveProductCommand command, CancellationToken ct)
    {
        ValidateId(id);
        var name = ValidateName(command.Name);
        if (command.CategoryId <= 0)
            throw new ValidationException("Cần chọn danh mục hợp lệ.");
        if (command.CurrentPrice < 0 || command.CurrentPrice > 999_999_999 || decimal.Truncate(command.CurrentPrice) != command.CurrentPrice)
            throw new ValidationException("Giá bán phải là số nguyên VND từ 0 đến 999.999.999.");
        var validated = command with { Name = name };
        if (command.ImageUrlSpecified)
            validated = validated with { ImageUrl = NormalizeImageUrl(command.ImageUrl) };
        return repository.SaveProductAsync(id, validated, ct);
    }

    public Task DisableCategoryAsync(int id, CancellationToken ct) { ValidateId(id); return repository.DisableCategoryAsync(id, ct); }
    public Task DisableProductAsync(int id, CancellationToken ct) { ValidateId(id); return repository.DisableProductAsync(id, ct); }

    private static string ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
            throw new ValidationException("Tên phải có từ 1 đến 150 ký tự.");
        return name.Trim();
    }

    private static void ValidateId(int? id)
    {
        if (id is <= 0) throw new ValidationException("ID phải là số nguyên dương.");
    }

    private static string? NormalizeImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var url = value.Trim();
        if (url.Length > 500 || url.Any(char.IsControl) || url.Contains('\\'))
            throw new ValidationException("Đường dẫn ảnh tối đa 500 ký tự, không chứa ký tự điều khiển hoặc dấu gạch chéo ngược.");
        if (url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal)) return url;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(uri.UserInfo)) return url;
        throw new ValidationException("Ảnh phải dùng đường dẫn bắt đầu bằng / hoặc URL HTTP/HTTPS hợp lệ.");
    }
}
