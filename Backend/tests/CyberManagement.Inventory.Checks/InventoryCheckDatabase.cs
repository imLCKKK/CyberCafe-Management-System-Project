using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal sealed class InventoryCheckDatabase(string? serverConnection)
{
    // Chỉ DB có tên sinh trong lượt kiểm tra này mới được tạo/xóa.
    private readonly string name = $"CyberManagement_SCRUM13_Checks_{Guid.NewGuid():N}";
    private bool created;
    public bool IsSql => !string.IsNullOrWhiteSpace(serverConnection);
    public string ConnectionString => new SqlConnectionStringBuilder(serverConnection ??
        "Server=127.0.0.1,1;Integrated Security=True;TrustServerCertificate=True")
        { InitialCatalog = name }.ConnectionString;

    public void Configure(DbContextOptionsBuilder options)
    {
        if (IsSql) options.UseSqlServer(ConnectionString);
        else options.UseInMemoryDatabase(name);
    }

    public async Task InitializeAsync(ApplicationDbContext database)
    {
        if (!IsSql) { await database.Database.EnsureCreatedAsync(); return; }
        await using var connection = Master();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{name}') IS NOT NULL THROW 51000, 'Check DB already exists.', 1; CREATE DATABASE [{name}];";
        await command.ExecuteNonQueryAsync();
        created = true;
        await database.Database.MigrateAsync();
        if ((await database.Database.GetAppliedMigrationsAsync()).Count() != 4)
            throw new InvalidOperationException("Unexpected migration count; review integration order.");
        await using var count = connection.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM [{name}].sys.tables WHERE is_ms_shipped=0 AND name <> '__EFMigrationsHistory';";
        if (Convert.ToInt32(await count.ExecuteScalarAsync()) != 35)
            throw new InvalidOperationException("Expected 35 business tables.");
        Console.WriteLine("PASS fresh SQL database: 4 migrations, 35 business tables.");
    }

    public async Task SeedInventoryAsync(ApplicationDbContext database)
    {
        if (IsSql)
            await database.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "InventoryReadSeed.sql")));
        else if (!await database.Products.AnyAsync())
        {
            var category = new Category { CategoryName = "SCRUM13_TEST_INVENTORY", IsActive = true };
            database.Products.AddRange(
                new Product { ProductName = "TEST_Nước suối", Category = category, Stock = 20, IsActive = true, Price = 10000 },
                new Product { ProductName = "TEST_Mì ly hết hàng", Category = category, Stock = 0, IsActive = true, Price = 20000 },
                new Product { ProductName = "TEST_Coca ngừng bán", Category = category, Stock = 5, IsActive = false, Price = 15000 });
            await database.SaveChangesAsync();
        }
    }

    public async Task CleanupAsync()
    {
        if (!created) return;
        SqlConnection.ClearAllPools();
        await using var connection = Master();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END;";
        await command.ExecuteNonQueryAsync();
        created = false;
        Console.WriteLine($"Removed isolated check database: {name}");
    }

    private SqlConnection Master() => new(new SqlConnectionStringBuilder(ConnectionString)
        { InitialCatalog = "master" }.ConnectionString);
}
