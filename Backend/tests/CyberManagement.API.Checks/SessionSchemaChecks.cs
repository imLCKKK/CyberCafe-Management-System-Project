using System.Data;
using CyberManagement.Domain.Entities;
using CyberManagement.Infrastructure.Persistance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

internal static class SessionSchemaChecks
{
    public static int RunModel()
    {
        var checks = 0;
        using var db = new ApplicationDbContextFactory().CreateDbContext([]);
        var model = db.GetService<IDesignTimeModel>().Model;
        var session = model.FindEntityType(typeof(GamingSession))!;
        var charge = model.FindEntityType(typeof(SessionCharge))!;
        Check("Migration snapshot matches the current SQL Server model", !db.Database.HasPendingModelChanges());
        Check("Billing schema preserves the existing table count", model.GetRelationalModel().Tables.Count() == 40);
        Check("Legacy sessions retain unknown rate and actor as null",
            session.FindProperty(nameof(GamingSession.HourlyRateSnapshot))!.IsNullable
            && session.FindProperty(nameof(GamingSession.OpenedByEmployeeId))!.IsNullable
            && session.FindProperty(nameof(GamingSession.ClosedByEmployeeId))!.IsNullable);
        Check("Rate snapshot uses exact integer VND precision", session.FindProperty(nameof(GamingSession.HourlyRateSnapshot))
            is { } rate && rate.GetPrecision() == 18 && rate.GetScale() == 0);
        Check("Open-session uniqueness covers malformed Active/end-time combinations",
            session.FindProperty("OpenSessionSlot") is { } slot && slot.IsShadowProperty() && slot.GetIsStored() == true
            && slot.GetComputedColumnSql() == "CASE WHEN [Status] = N'Active' OR [EndTime] IS NULL THEN 0 ELSE [GamingSessionId] END");
        foreach (var (name, field) in new[]
        {
            ("UX_GamingSessions_OpenComputer", nameof(GamingSession.ComputerId)),
            ("UX_GamingSessions_OpenCustomer", nameof(GamingSession.CustomerId))
        })
            Check($"SQL uniquely reserves an open slot for {field}", session.GetIndexes().Any(index =>
                index.GetDatabaseName() == name && index.IsUnique && index.GetFilter() is null
                && index.Properties.Select(property => property.Name).SequenceEqual([field, "OpenSessionSlot"])));
        foreach (var (entity, property) in new[]
        {
            (session, nameof(GamingSession.OpenOperationKey)), (session, nameof(GamingSession.CloseOperationKey)),
            (charge, nameof(SessionCharge.OperationKey)), (charge, nameof(SessionCharge.WalletTransactionId))
        })
            Check($"{entity.ClrType.Name}.{property} is unique only when present", entity.GetIndexes().Any(index =>
                index.IsUnique && index.Properties.Select(item => item.Name).SequenceEqual([property])
                && index.GetFilter() == $"[{property}] IS NOT NULL"));
        Check("Different operation keys cannot settle a session twice at the same time", charge.GetIndexes().Any(index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(SessionCharge.GamingSessionId), nameof(SessionCharge.EndTime)])));
        Check("Ledger deletion cannot remove or detach historical charges", charge.GetForeignKeys().Any(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(WalletTransaction) && foreignKey.DeleteBehavior == DeleteBehavior.NoAction));
        const string migration = "20261114104346_PrepareSessionBilling";
        var prior = db.Database.GetMigrations().TakeWhile(id => id != migration).Last();
        var migrator = db.GetService<IMigrator>();
        var up = migrator.GenerateScript(prior, migration, MigrationsSqlGenerationOptions.Idempotent);
        var down = migrator.GenerateScript(migration, prior);
        Check("Upgrade script refuses duplicate legacy sessions and settlements",
            up.Contains("THROW 51024") && up.Contains("GROUP BY [ComputerId]") && up.Contains("GROUP BY [CustomerId]")
            && up.Contains("GROUP BY [GamingSessionId], [EndTime]"));
        Check("Upgrade contains no history reset or fabricated backfill", !up.Contains("DELETE FROM [GamingSessions]")
            && !up.Contains("UPDATE [GamingSessions]") && !up.Contains("UPDATE [SessionCharges]")
            && !up.Contains("DROP TABLE"));
        Check("Downgrade refuses to discard recorded billing metadata", down.Contains("THROW 51025"));
        Check("Schema verification never opens a database connection", db.Database.GetDbConnection().State == ConnectionState.Closed);
        Console.WriteLine($"{checks} session schema checks passed (offline SQL Server model/scripts; not SQL execution).");
        return checks;

        void Check(string name, bool passed)
        {
            if (!passed) throw new InvalidOperationException(name);
            checks++;
            Console.WriteLine($"PASS {name}");
        }
    }

    // Called only inside the isolated SQL database owned by the API checks fixture.
    public static async Task<int> RunSqlAsync(ApplicationDbContext db)
    {
        if (!db.Database.IsSqlServer())
        {
            Console.WriteLine("SKIP session uniqueness/FK/check constraints: a real isolated SQL Server database is required.");
            return 0;
        }
        var checks = 0;
        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var customers = new[]
        {
            new Customer { FullName = "Session schema A", Phone = "schema-a" + suffix },
            new Customer { FullName = "Session schema B", Phone = "schema-b" + suffix }
        };
        var room = new ComputerRoom { RoomName = "Session schema " + suffix };
        var type = new ComputerType { TypeName = "Session schema " + suffix, PricePerHour = 10000 };
        var computers = new[]
        {
            new Computer { ComputerName = "Schema A", Room = room, ComputerType = type },
            new Computer { ComputerName = "Schema B", Room = room, ComputerType = type }
        };
        db.AddRange(customers); db.AddRange(computers);
        await db.SaveChangesAsync();
        var active = Session(computers[0], customers[0]);
        db.GamingSessions.Add(active); await db.SaveChangesAsync();
        Check("Legacy session can persist without fabricated billing metadata", active.HourlyRateSnapshot is null && active.OpenedByEmployeeId is null);
        await Reject("SQL prevents a second open session on the same computer", Session(computers[0], customers[1]), "UX_GamingSessions_OpenComputer");
        await Reject("SQL prevents the same customer on two computers", Session(computers[1], customers[0]), "UX_GamingSessions_OpenCustomer");
        await db.GamingSessions.Where(row => row.GamingSessionId == active.GamingSessionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.EndTime, now.AddSeconds(1)));
        var malformed = Session(computers[0], customers[1]); malformed.Status = "Ended";
        await Reject("SQL open slot covers Active-with-end versus Ended-without-end", malformed, "UX_GamingSessions_OpenComputer");
        await db.GamingSessions.Where(row => row.GamingSessionId == active.GamingSessionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, "Ended"));
        var reopened = Session(computers[0], customers[0]);
        db.GamingSessions.Add(reopened); await db.SaveChangesAsync();
        Check("Closing a session releases its slot while retaining history", reopened.GamingSessionId != active.GamingSessionId);
        db.ChangeTracker.Clear();
        var invalidRate = Closed(); invalidRate.HourlyRateSnapshot = -1;
        await Reject("SQL rejects nonpositive billing snapshots", invalidRate, "CK_GamingSessions_RateSnapshot");
        var invalidActor = Closed(); invalidActor.OpenedByEmployeeId = int.MaxValue;
        await Reject("SQL rejects fictitious staff actors", invalidActor, "FK_GamingSessions_Employees_OpenedByEmployeeId");
        var keyed = Closed(); keyed.EndTime = now.AddMinutes(2);
        keyed.OpenOperationKey = "schema:open:" + suffix; keyed.CloseOperationKey = "schema:close:" + suffix;
        db.GamingSessions.Add(keyed); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var duplicateOpen = Closed(); duplicateOpen.OpenOperationKey = keyed.OpenOperationKey;
        await Reject("SQL open operation key is durable and unique", duplicateOpen, "IX_GamingSessions_OpenOperationKey");
        var duplicateClose = Closed(); duplicateClose.CloseOperationKey = keyed.CloseOperationKey;
        await Reject("SQL close operation key is durable and unique", duplicateClose, "IX_GamingSessions_CloseOperationKey");
        var emptyKey = Closed(); emptyKey.OpenOperationKey = " ";
        await Reject("SQL rejects empty session operation keys", emptyKey, "CK_GamingSessions_OperationKeys");
        var ledger = new WalletTransaction { CustomerId = customers[0].CustomerId, Amount = -100, BalanceAfter = 0, Type = "GamingCharge" };
        db.WalletTransactions.Add(ledger); await db.SaveChangesAsync();
        var charge = Charge(10, "schema:charge:" + suffix); charge.WalletTransactionId = ledger.TransactionId;
        db.SessionCharges.Add(charge); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var duplicateCharge = Charge(20, charge.OperationKey!);
        await Reject("SQL charge operation key is unique", duplicateCharge, "IX_SessionCharges_OperationKey");
        var duplicateSettlement = Charge(10, "schema:other-settlement:" + suffix);
        await Reject("SQL settlement time cannot be charged twice", duplicateSettlement, "IX_SessionCharges_GamingSessionId_EndTime");
        var duplicateLedger = Charge(30, "schema:other-ledger:" + suffix); duplicateLedger.WalletTransactionId = ledger.TransactionId;
        await Reject("SQL ledger cannot belong to two charges", duplicateLedger, "IX_SessionCharges_WalletTransactionId");
        var invalidLedger = Charge(40, "schema:bad-ledger:" + suffix); invalidLedger.WalletTransactionId = int.MaxValue;
        await Reject("SQL rejects a nonexistent ledger link", invalidLedger, "FK_SessionCharges_WalletTransactions_WalletTransactionId");
        var invalidAmount = Charge(50, "schema:zero-charge:" + suffix); invalidAmount.Amount = 0;
        await Reject("SQL rejects a keyed zero-value charge", invalidAmount, "CK_SessionCharges_BillingMetadata");
        var invalidInterval = Charge(60, "schema:bad-interval:" + suffix); invalidInterval.StartTime = invalidInterval.EndTime;
        await Reject("SQL rejects a keyed empty billing interval", invalidInterval, "CK_SessionCharges_BillingMetadata");
        return checks;

        GamingSession Session(Computer computer, Customer customer) => new()
        { ComputerId = computer.ComputerId, CustomerId = customer.CustomerId, StartTime = now, Status = "Active" };
        GamingSession Closed()
        {
            var session = Session(computers[1], customers[1]); session.Status = "Ended"; session.EndTime = now.AddSeconds(1);
            return session;
        }
        SessionCharge Charge(int seconds, string key) => new()
        { GamingSessionId = keyed.GamingSessionId, StartTime = now, EndTime = now.AddSeconds(seconds), Amount = 100, OperationKey = key };
        async Task Reject(string name, object row, string constraint)
        {
            db.ChangeTracker.Clear(); db.Add(row);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Message.Contains(constraint))
            { db.ChangeTracker.Clear(); Check(name, true); return; }
            throw new InvalidOperationException(name + ": SQL did not reject the row with " + constraint);
        }
        void Check(string name, bool passed)
        {
            if (!passed) throw new InvalidOperationException(name);
            checks++; Console.WriteLine($"PASS {name}");
        }
    }
}
