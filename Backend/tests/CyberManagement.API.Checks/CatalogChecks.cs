using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CyberManagement.Application.DTOs.Catalog;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class CatalogChecks
{
    public static async Task<int> RunAsync(IServiceProvider services, HttpClient client,
        IReadOnlyDictionary<string, string> tokens, string customerToken)
    {
        var checks = 0;
        var admin = tokens["Admin"];
        var categoryBody = new { name = "CATALOG_TEST", isActive = true };
        foreach (var path in new[] { "/api/admin/products", "/api/admin/categories", "/api/menu" })
            await Status("Anonymous read " + path, HttpStatusCode.Unauthorized, await Send(HttpMethod.Get, path, null));
        foreach (var path in new[] { "/api/admin/products", "/api/admin/categories" })
        {
            await Status("Customer admin read " + path, HttpStatusCode.Forbidden, await Send(HttpMethod.Get, path, customerToken));
            foreach (var (role, token) in tokens)
                await Status(role + " catalog read", role == "Technician" ? HttpStatusCode.Forbidden : HttpStatusCode.OK,
                    await Send(HttpMethod.Get, path, token));
        }
        foreach (var token in tokens.Values)
            await Status("Employee cannot use customer menu", HttpStatusCode.Forbidden, await Send(HttpMethod.Get, "/api/menu", token));

        using var created = await Send(HttpMethod.Post, "/api/admin/categories", admin, categoryBody);
        await Status("Create category", HttpStatusCode.Created, created);
        var category = (await created.Content.ReadFromJsonAsync<CategoryDto>())!;
        object ProductBody(decimal price = 30000, bool active = true, int? categoryId = null) =>
            new { name = "TEST_Mì bò", categoryId = categoryId ?? category.Id, currentPrice = price, isActive = active };
        using var productResponse = await Send(HttpMethod.Post, "/api/admin/products", admin, ProductBody());
        await Status("Create product", HttpStatusCode.Created, productResponse);
        var product = (await productResponse.Content.ReadFromJsonAsync<ProductDto>())!;
        Check("New product starts with zero stock", product.StockQuantity == 0);

        foreach (var path in new[] { "/api/admin/categories", "/api/admin/products" })
        {
            var body = path.EndsWith("products") ? ProductBody() : categoryBody;
            var id = path.EndsWith("products") ? product.Id : category.Id;
            foreach (var (role, token) in tokens.Where(p => p.Key is not ("Admin" or "Manager")))
            {
                await Status(role + " cannot create", HttpStatusCode.Forbidden, await Send(HttpMethod.Post, path, token, body));
                await Status(role + " cannot update", HttpStatusCode.Forbidden, await Send(HttpMethod.Put, $"{path}/{id}", token, body));
                await Status(role + " cannot delete", HttpStatusCode.Forbidden, await Send(HttpMethod.Delete, $"{path}/{id}", token));
            }
            await Status("Customer cannot write", HttpStatusCode.Forbidden, await Send(HttpMethod.Post, path, customerToken, body));
            await Status("Anonymous cannot write", HttpStatusCode.Unauthorized, await Send(HttpMethod.Post, path, null, body));
        }
        foreach (var price in new[] { -1m, 0.5m, 1_000_000_000m })
            await Status("Invalid VND price " + price, HttpStatusCode.BadRequest,
                await Send(HttpMethod.Post, "/api/admin/products", admin, ProductBody(price)));
        await Status("Unknown category", HttpStatusCode.Conflict, await Send(HttpMethod.Post, "/api/admin/products", admin, ProductBody(categoryId: int.MaxValue)));
        await Status("Invalid category ID", HttpStatusCode.BadRequest, await Send(HttpMethod.Post, "/api/admin/products", admin, ProductBody(categoryId: 0)));
        await Status("Missing required price", HttpStatusCode.BadRequest, await Send(HttpMethod.Post, "/api/admin/products", admin,
            new { name = "Missing price", categoryId = category.Id, isActive = true }));
        await Status("Price cannot be null", HttpStatusCode.BadRequest, await Send(HttpMethod.Post, "/api/admin/products", admin,
            new { name = "Null price", categoryId = category.Id, currentPrice = (decimal?)null, isActive = true }));
        await Status("Cannot inject stock", HttpStatusCode.BadRequest, await Send(HttpMethod.Put, $"/api/admin/products/{product.Id}", admin,
            new { name = "Injected", categoryId = category.Id, currentPrice = 1, isActive = true, stockQuantity = 999 }));
        foreach (var name in new[] { " ", new string('x', 151) })
            await Status("Invalid name", HttpStatusCode.BadRequest, await Send(HttpMethod.Post, "/api/admin/categories", admin, new { name, isActive = true }));
        await Status("Missing product", HttpStatusCode.NotFound, await Send(HttpMethod.Put, "/api/admin/products/2147483647", admin, ProductBody()));
        await Status("Missing category", HttpStatusCode.NotFound, await Send(HttpMethod.Delete, "/api/admin/categories/2147483647", admin));

        // Fresh request/scope, with the same product IDs used by inventory and menu.
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Products.SingleAsync(p => p.ProductId == product.Id)).Stock = 7;
            await db.SaveChangesAsync();
        }
        await Status("Manager edits price", HttpStatusCode.OK, await Send(HttpMethod.Put, $"/api/admin/products/{product.Id}", tokens["Manager"], ProductBody(35000)));
        var menu = await Menu();
        Check("Menu uses persisted price and inventory", menu.Products.Single(p => p.Id == product.Id) is { CurrentPrice: 35000, StockQuantity: 7 });
        using var adminReload = await Send(HttpMethod.Get, "/api/admin/products", admin);
        var reloaded = (await adminReload.Content.ReadFromJsonAsync<ProductDto[]>())!.Single(p => p.Id == product.Id);
        Check("Reload uses SQL price, name and stock", reloaded.CurrentPrice == 35000 && reloaded.StockQuantity == 7 && reloaded.Name == "TEST_Mì bò");
        await Status("Disable product", HttpStatusCode.NoContent, await Send(HttpMethod.Delete, $"/api/admin/products/{product.Id}", admin));
        Check("Disabled product is absent from menu", !(await Menu()).Products.Any(p => p.Id == product.Id));
        await Status("Re-enable zero-price product", HttpStatusCode.OK, await Send(HttpMethod.Put, $"/api/admin/products/{product.Id}", admin, ProductBody(0)));
        Check("Zero-price product remains selectable", (await Menu()).Products.Any(p => p.Id == product.Id && p.CurrentPrice == 0));
        await Status("Disable category", HttpStatusCode.NoContent, await Send(HttpMethod.Delete, $"/api/admin/categories/{category.Id}", admin));
        menu = await Menu();
        Check("Disabled category hides its products", !menu.Categories.Any(c => c.Id == category.Id) && !menu.Products.Any(p => p.Id == product.Id));
        await Status("Cannot assign inactive category", HttpStatusCode.Conflict, await Send(HttpMethod.Post, "/api/admin/products", admin, ProductBody()));
        await Status("Manager reactivates category", HttpStatusCode.OK, await Send(HttpMethod.Put, $"/api/admin/categories/{category.Id}", tokens["Manager"], categoryBody));
        Check("Category reactivation restores active products", (await Menu()).Products.Any(p => p.Id == product.Id));
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check("Catalog operations preserve stock and historical row", await db.Products.AnyAsync(p => p.ProductId == product.Id && p.Stock == 7));
            Check("Invalid requests did not insert products", await db.Products.CountAsync(p => p.CategoryId == category.Id) == 1);
        }
        return checks;

        async Task<MenuDto> Menu()
        {
            using var response = await Send(HttpMethod.Get, "/api/menu", customerToken);
            await Status("Customer reads menu", HttpStatusCode.OK, response);
            return (await response.Content.ReadFromJsonAsync<MenuDto>())!;
        }
        async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request);
        }
        async Task Status(string name, HttpStatusCode expected, HttpResponseMessage response)
        {
            if (response.StatusCode != expected) throw new Exception($"{name}: expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            Check(name, true);
        }
        void Check(string name, bool passed)
        {
            if (!passed) throw new Exception("Catalog check failed: " + name);
            checks++;
            Console.WriteLine("PASS Catalog: " + name);
        }
    }
}
