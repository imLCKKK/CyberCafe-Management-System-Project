using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal sealed class AuthCheckDatabase
{
    private readonly string _databaseName = $"CyberManagement_AuthChecks_{Guid.NewGuid():N}";
    private readonly string? _sqlConnection;
    private bool _created;

    public AuthCheckDatabase(string? serverConnection)
    {
        if (!string.IsNullOrWhiteSpace(serverConnection))
        {
            // Always use a new database; never migrate or seed the database supplied by the caller.
            _sqlConnection = new SqlConnectionStringBuilder(serverConnection)
            {
                InitialCatalog = _databaseName
            }.ConnectionString;
        }
    }

    public string Provider => _sqlConnection is null ? "InMemory" : "SQL Server";

    public void Configure(DbContextOptionsBuilder options)
    {
        if (_sqlConnection is null)
            options.UseInMemoryDatabase(_databaseName);
        else
            options.UseSqlServer(_sqlConnection);
    }

    public async Task InitializeAsync(ApplicationDbContext database)
    {
        if (_sqlConnection is null)
        {
            await database.Database.EnsureCreatedAsync();
            return;
        }

        await using var connection = CreateMasterConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{_databaseName}') IS NOT NULL THROW 51000, 'Auth check database already exists.', 1; CREATE DATABASE [{_databaseName}];";
        await command.ExecuteNonQueryAsync();
        _created = true;

        await database.Database.MigrateAsync();
        await using var applicationConnection = new SqlConnection(_sqlConnection);
        await applicationConnection.OpenAsync();
        await using var tableCount = applicationConnection.CreateCommand();
        tableCount.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0 AND name <> '__EFMigrationsHistory';";
        Console.WriteLine($"SQL business tables: {await tableCount.ExecuteScalarAsync()}; applied migrations: {(await database.Database.GetAppliedMigrationsAsync()).Count()}.");
    }

    public async Task CleanupAsync()
    {
        if (!_created)
            return;

        // Only this randomly named database, checked absent and created above, can be removed.
        await using var connection = CreateMasterConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END;";
        await command.ExecuteNonQueryAsync();
        _created = false;
        Console.WriteLine($"Removed isolated auth-check database: {_databaseName}");
    }

    private SqlConnection CreateMasterConnection() => new(new SqlConnectionStringBuilder(_sqlConnection!)
    {
        InitialCatalog = "master"
    }.ConnectionString);
}
