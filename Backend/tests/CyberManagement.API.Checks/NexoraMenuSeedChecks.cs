using System.Text.Json;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class NexoraMenuSeedChecks
{
    public static async Task RunAsync()
    {
        var connection = Environment.GetEnvironmentVariable("AUTH_CHECKS_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Menu seed checks require AUTH_CHECKS_SQL_CONNECTION (SQL Server).");
        var fixture = new AuthCheckDatabase(connection);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        fixture.Configure(options);
        await using var database = new ApplicationDbContext(options.Options);
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "NexoraMenuSeed.sql"));
        using var assets = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "nexora-menu-assets.json")));
        var expected = assets.RootElement.GetProperty("assets").EnumerateArray()
            .ToDictionary(a => a.GetProperty("name").GetString()!, a => a.GetProperty("imageUrl").GetString()!);
        var checks = 0;
        try
        {
            await fixture.InitializeAsync(database);
            await database.Database.ExecuteSqlRawAsync(sql);
            var initial = await database.Products.AsNoTracking().OrderBy(p => p.ProductId).ToListAsync();
            Check(initial.Count == 62 && await database.Categories.CountAsync() == 7, "62 products and 7 categories");
            Check(initial.All(p => p.Stock == 0 && p.IsActive && expected.GetValueOrDefault(p.ProductName) == p.ImageUrl), "Images match selected assets; no invented stock");
            Check(initial.Single(p => p.ProductName == "Tiếp Trận").Price == 40000
                && initial.Single(p => p.ProductName == "Đồng Đội — 2 người").Price == 110000
                && initial.Single(p => p.ProductName == "Trứng thêm").Price == 7000, "Approved combo/add-on prices");

            await database.Database.ExecuteSqlRawAsync(sql);
            Check(initial.Select(p => p.ProductId).SequenceEqual(await database.Products.OrderBy(p => p.ProductId).Select(p => p.ProductId).ToListAsync()), "Repeat run preserves product IDs");
            Check(await database.Categories.CountAsync() == 7, "Repeat run creates no categories");

            var edited = await database.Products.SingleAsync(p => p.ProductName == "Mì xào bò");
            edited.Stock = 9;
            edited.Price = 36000;
            edited.ImageUrl = "/food/custom-photo.jpg";
            edited.IsActive = false;
            await database.SaveChangesAsync();
            await database.Database.ExecuteSqlRawAsync(sql);
            await database.Entry(edited).ReloadAsync();
            Check(edited is { Stock: 9, Price: 36000, ImageUrl: "/food/custom-photo.jpg", IsActive: false }, "Repeat run preserves later catalog/stock edits");

            database.Categories.Add(new Category { CategoryName = "Danh mục riêng", IsActive = true });
            await database.SaveChangesAsync();
            await database.Database.ExecuteSqlRawAsync(sql);
            Check(await database.Categories.AnyAsync(c => c.CategoryName == "Danh mục riêng"), "Unrelated catalog data stays intact");

            database.Categories.Add(new Category { CategoryName = "Cơm", IsActive = true });
            await database.SaveChangesAsync();
            var count = await database.Products.CountAsync();
            await ExpectConflict("Duplicate category stops atomically");
            Check(await database.Products.CountAsync() == count, "Conflict leaves product rows intact");
            Console.WriteLine($"{checks} Nexora menu seed checks passed (SQL Server).");
        }
        finally { await fixture.CleanupAsync(); }

        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            checks++;
        }

        async Task ExpectConflict(string label)
        {
            try { await database.Database.ExecuteSqlRawAsync(sql); }
            catch (SqlException error) when (error.Number == 51000) { checks++; return; }
            throw new InvalidOperationException(label);
        }
    }
}
