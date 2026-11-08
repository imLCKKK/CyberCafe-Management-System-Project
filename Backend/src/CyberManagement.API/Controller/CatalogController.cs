using CyberManagement.Application.DTOs.Catalog;
using CyberManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin,Manager,Cashier,Waiter,Technician")]
public sealed class CatalogController(CatalogService catalog) : ControllerBase
{
    [HttpGet("categories")]
    [Authorize(Policy = "permission:product.view")]
    public async Task<IActionResult> GetCategories(CancellationToken ct) => Ok(await catalog.GetCategoriesAsync(ct));

    [HttpGet("products")]
    [Authorize(Policy = "permission:product.view")]
    public async Task<IActionResult> GetProducts(CancellationToken ct) => Ok(await catalog.GetProductsAsync(ct));

    [HttpPost("categories")]
    [Authorize(Policy = "permission:product.manage")]
    public async Task<IActionResult> CreateCategory(SaveCategoryCommand command, CancellationToken ct)
    {
        var result = await catalog.SaveCategoryAsync(null, command, ct);
        return Created("/api/admin/categories", result);
    }

    [HttpPut("categories/{id:int}")]
    [Authorize(Policy = "permission:product.manage")]
    public async Task<IActionResult> UpdateCategory(int id, SaveCategoryCommand command, CancellationToken ct) =>
        Ok(await catalog.SaveCategoryAsync(id, command, ct));

    [HttpDelete("categories/{id:int}")]
    [Authorize(Policy = "permission:product.manage")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        await catalog.DisableCategoryAsync(id, ct);
        return NoContent();
    }

    [HttpPost("products")]
    [Authorize(Policy = "permission:product.manage")]
    public async Task<IActionResult> CreateProduct(SaveProductCommand command, CancellationToken ct)
    {
        var result = await catalog.SaveProductAsync(null, command, ct);
        return Created("/api/admin/products", result);
    }

    [HttpPut("products/{id:int}")]
    [Authorize(Policy = "permission:product.manage")]
    public async Task<IActionResult> UpdateProduct(int id, SaveProductCommand command, CancellationToken ct) =>
        Ok(await catalog.SaveProductAsync(id, command, ct));

    [HttpDelete("products/{id:int}")]
    [Authorize(Policy = "permission:product.manage")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken ct)
    {
        await catalog.DisableProductAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/menu")]
[Authorize(Roles = "Customer")]
public sealed class MenuController(CatalogService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MenuDto>> GetMenu(CancellationToken ct) => Ok(await catalog.GetMenuAsync(ct));
}
