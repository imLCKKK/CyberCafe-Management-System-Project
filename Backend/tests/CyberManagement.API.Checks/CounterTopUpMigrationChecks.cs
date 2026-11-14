using CyberManagement.Application.DTOs.Customers;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using CyberManagement.Infrastructure.Repositories;
using CyberManagement.Infrastructure.Wallets;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

internal static class CounterTopUpMigrationChecks
{
    public static async Task<int> RunAsync(string serverConnection)
    {
        if (string.IsNullOrWhiteSpace(serverConnection))
            throw new ArgumentException("Set AUTH_CHECKS_SQL_CONNECTION to a SQL Server instance. A new isolated database is always created.");
        const string initialCounterMigration = "20261114112809_WalletLedgerCounterTopUps";
        var name = "CyberManagement_CounterMigrationChecks_" + Guid.NewGuid().ToString("N");
        var masterString = new SqlConnectionStringBuilder(serverConnection) { InitialCatalog = "master" }.ConnectionString;
        var connectionString = new SqlConnectionStringBuilder(serverConnection) { InitialCatalog = name }.ConnectionString;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connectionString).Options;
        var created = false;
        var checks = 0;
        try
        {
            await using (var master = new SqlConnection(masterString))
            {
                await master.OpenAsync();
                await using var create = master.CreateCommand();
                create.CommandText = $"IF DB_ID(N'{name}') IS NOT NULL THROW 51000, 'Migration check database already exists.', 1; CREATE DATABASE [{name}];";
                await create.ExecuteNonQueryAsync();
                created = true;
            }
            int customerId;
            int employeeId;
            int ledgerId;
            var pendingKey = Guid.NewGuid();
            var approvedKey = Guid.NewGuid();
            var now = DateTime.UtcNow;
            await using (var db = new ApplicationDbContext(options))
            {
                await db.GetService<IMigrator>().MigrateAsync(initialCounterMigration);
                Check("Intermediate counter migration applies independently", (await db.Database.GetAppliedMigrationsAsync()).Last() == initialCounterMigration);
                var employee = new Employee { FullName = "Migration cashier", Username = "migration.cashier", PasswordHash = "fixture-only-unused-hash",
                    Role = new Role { RoleName = "MigrationChecks" }, IsActive = true };
                var customer = new Customer { FullName = "Migration cash owner", Phone = "0770000101", WalletBalance = 50000, Status = "Active" };
                var ledger = new WalletTransaction { Customer = customer, Amount = 50000, BalanceAfter = 50000, Type = "TopUp", CreatedAt = now,
                    Description = "Approved before the gift policy" };
                db.Employees.Add(employee);
                db.WalletTransactions.Add(ledger);
                await db.SaveChangesAsync();
                customerId = customer.CustomerId;
                employeeId = employee.EmployeeId;
                ledgerId = ledger.TransactionId;
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [CounterTopUpRequests] ([CustomerId],[RequestKey],[Amount],[Status],[CreatedAt]) VALUES ({customerId},{pendingKey},{50000m},N'Pending',{now})");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [CounterTopUpRequests] ([CustomerId],[RequestKey],[Amount],[Status],[CreatedAt],[ApprovedAt],[ApprovedByEmployeeId],[CashAmountReceived],[WalletTransactionId]) VALUES ({customerId},{approvedKey},{50000m},N'Approved',{now},{now},{employeeId},{50000m},{ledgerId})");
            }
            await using (var db = new ApplicationDbContext(options))
            {
                await db.Database.MigrateAsync();
                var pending = await db.CounterTopUpRequests.AsNoTracking().SingleAsync(row => row.RequestKey == pendingKey);
                var approved = await db.CounterTopUpRequests.AsNoTracking().SingleAsync(row => row.RequestKey == approvedKey);
                Check("Gift migration updates an existing Pending request", pending.Amount == 50000 && pending.BonusAmount == 5000
                    && pending.TotalAmount == 55000 && pending.Status == "Pending" && pending.WalletTransactionId is null);
                Check("Gift migration preserves an old Approved request without a retroactive gift", approved.Amount == 50000
                    && approved.BonusAmount == 0 && approved.TotalAmount == 50000 && approved.Status == "Approved"
                    && approved.WalletTransactionId == ledgerId && approved.CashAmountReceived == 50000
                    && approved.ApprovedByEmployeeId == employeeId);
                Check("Migration never changes the old wallet or ledger", await db.Customers.Where(row => row.CustomerId == customerId)
                    .Select(row => row.WalletBalance).SingleAsync() == 50000
                    && await db.WalletTransactions.CountAsync(row => row.CustomerId == customerId) == 1
                    && await db.WalletTransactions.Where(row => row.TransactionId == ledgerId).Select(row => row.Amount).SingleAsync() == 50000);
            }
            await using (var db = new ApplicationDbContext(options))
            {
                var repository = new CounterTopUpRepository(db, new WalletLedgerWriter(db));
                var quote = new CounterTopUpOptionDto(50000, 5000, 55000);
                var approvedReplay = await repository.CreateAsync(customerId, approvedKey, quote, CancellationToken.None);
                Check("Old request-key retry returns the Approved historical snapshot", approvedReplay.Replayed
                    && approvedReplay.Request.Status == "Approved" && approvedReplay.Request.BonusAmount == 0
                    && approvedReplay.Request.TotalAmount == 50000 && approvedReplay.Request.TransactionId == ledgerId);
                var approvalReplay = await repository.ApproveAsync(approvedReplay.Request.RequestId, employeeId, 50000, CancellationToken.None);
                Check("Old approval retry cannot reapply the new gift policy", approvalReplay.Replayed
                    && approvalReplay.Request.TotalAmount == 50000 && approvalReplay.Request.BalanceAfter == 50000);
                var pendingReplay = await repository.CreateAsync(customerId, pendingKey, quote, CancellationToken.None);
                Check("Pending request-key retry uses the migrated gift snapshot", pendingReplay.Replayed
                    && pendingReplay.Request.BonusAmount == 5000 && pendingReplay.Request.TotalAmount == 55000);
                Check("Historical retry adds no new request, ledger or money", await db.CounterTopUpRequests.CountAsync() == 2
                    && await db.WalletTransactions.CountAsync(row => row.CustomerId == customerId) == 1
                    && await db.Customers.Where(row => row.CustomerId == customerId).Select(row => row.WalletBalance).SingleAsync() == 50000);
            }
            await using (var db = new ApplicationDbContext(options))
            {
                var badApprovedKey = Guid.NewGuid();
                await SqlRefused("SQL rejects Approved cash without a receipt/actor/ledger", [547], () => db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO [CounterTopUpRequests] ([CustomerId],[RequestKey],[Amount],[BonusAmount],[TotalAmount],[Status],[CreatedAt]) VALUES ({customerId},{badApprovedKey},{10000m},{0m},{10000m},N'Approved',{now})"));
                var duplicatePendingKey = Guid.NewGuid();
                await SqlRefused("SQL independently permits only one Pending request per customer", [2601, 2627], () => db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO [CounterTopUpRequests] ([CustomerId],[RequestKey],[Amount],[BonusAmount],[TotalAmount],[Status],[CreatedAt]) VALUES ({customerId},{duplicatePendingKey},{10000m},{0m},{10000m},N'Pending',{now})"));
                await SqlRefused("SQL rejects a gift/total mismatch", [547], () => db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE [CounterTopUpRequests] SET [TotalAmount]={999999m} WHERE [RequestKey]={pendingKey}"));
                Check("Constraint failures preserve both migrated snapshots", await db.CounterTopUpRequests.CountAsync() == 2
                    && await db.CounterTopUpRequests.Where(row => row.RequestKey == pendingKey).Select(row => row.TotalAmount).SingleAsync() == 55000
                    && await db.CounterTopUpRequests.Where(row => row.RequestKey == approvedKey).Select(row => row.TotalAmount).SingleAsync() == 50000);
            }
            Console.WriteLine($"{checks} SQL counter migration/upgrade checks passed.");
            return checks;
        }
        finally
        {
            // Never use the caller's database name: only this GUID database, checked absent
            // and created above, is eligible for cleanup.
            if (created)
            {
                await using var master = new SqlConnection(masterString);
                await master.OpenAsync();
                await using var remove = master.CreateCommand();
                remove.CommandText = $"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END;";
                await remove.ExecuteNonQueryAsync();
                Console.WriteLine("Removed isolated counter migration-check database: " + name);
            }
        }

        async Task SqlRefused(string description, int[] expectedNumbers, Func<Task> action)
        {
            try { await action(); }
            catch (SqlException exception) when (expectedNumbers.Contains(exception.Number)) { Check(description, true); return; }
            throw new InvalidOperationException(description + ": expected a SQL constraint rejection.");
        }
        void Check(string description, bool condition)
        {
            if (!condition) throw new InvalidOperationException(description);
            checks++;
            Console.WriteLine("PASS " + description);
        }
    }
}
