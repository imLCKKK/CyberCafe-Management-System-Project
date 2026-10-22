using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CyberCafe.Api.Data;

public sealed class CyberCafeDbContext(DbContextOptions<CyberCafeDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<TopUp> TopUps => Set<TopUp>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<ComputerType> ComputerTypes => Set<ComputerType>();
    public DbSet<Computer> Computers => Set<Computer>();
    public DbSet<GamingSession> GamingSessions => Set<GamingSession>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemCancelRequest> OrderItemCancelRequests => Set<OrderItemCancelRequest>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentRegistration> TournamentRegistrations => Set<TournamentRegistration>();
    public DbSet<PlayerStatsSnapshot> PlayerStatsSnapshots => Set<PlayerStatsSnapshot>();
    public DbSet<TeamGenerationRun> TeamGenerationRuns => Set<TeamGenerationRun>();
    public DbSet<TeamProposal> TeamProposals => Set<TeamProposal>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.Property(x => x.Phone).HasColumnType("varchar(20)");
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.Property(x => x.PasswordHash).HasColumnType("varchar(255)");
            e.Property(x => x.MustChangePassword).HasDefaultValue(false);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.HasIndex(x => x.Phone).IsUnique();
        });

        model.Entity<Customer>(e =>
        {
            e.ToTable("customers", t => t.HasCheckConstraint("CK_customers_wallet_balance", "[wallet_balance] >= 0"));
            Money(e.Property(x => x.WalletBalance).HasDefaultValue(0m));
            e.Property(x => x.WalletVersion).HasDefaultValue(0L).IsConcurrencyToken();
            e.HasIndex(x => x.AccountId).IsUnique();
        });

        model.Entity<TopUp>(e =>
        {
            e.ToTable("top_ups", t => t.HasCheckConstraint("CK_top_ups_amount", "[amount] > 0"));
            Money(e.Property(x => x.Amount));
            e.Property(x => x.RequestedByCustomer).HasDefaultValue(false);
            e.HasIndex(x => x.CustomerId).IsUnique().HasFilter("[status] = 'CreditedUnpaid'")
                .HasDatabaseName("UX_top_ups_one_uncollected_per_customer");
        });

        model.Entity<WalletTransaction>(e =>
        {
            e.ToTable("wallet_transactions", t =>
            {
                t.HasCheckConstraint("CK_wallet_transactions_balance", "[balance_after] >= 0");
                t.HasCheckConstraint("CK_wallet_transactions_source", "(CASE WHEN [top_up_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [gaming_session_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [order_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [order_item_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [tournament_registration_id] IS NULL THEN 0 ELSE 1 END) = 1");
                t.HasCheckConstraint("CK_wallet_transactions_type_source", "([transaction_type] = 'TopUpCredit' AND [top_up_id] IS NOT NULL AND [amount] > 0) OR ([transaction_type] = 'GamingCharge' AND [gaming_session_id] IS NOT NULL AND [amount] < 0) OR ([transaction_type] = 'OrderDebit' AND [order_id] IS NOT NULL AND [amount] < 0) OR ([transaction_type] = 'OrderItemRefund' AND [order_item_id] IS NOT NULL AND [amount] > 0) OR ([transaction_type] = 'TournamentFee' AND [tournament_registration_id] IS NOT NULL AND [amount] < 0) OR ([transaction_type] = 'TournamentRefund' AND [tournament_registration_id] IS NOT NULL AND [amount] > 0)");
            });
            Money(e.Property(x => x.Amount));
            Money(e.Property(x => x.BalanceAfter));
            e.HasIndex(x => new { x.CustomerId, x.RecordedAt });
            e.HasIndex(x => x.TopUpId).IsUnique().HasFilter("[top_up_id] IS NOT NULL");
            e.HasIndex(x => x.OrderId).IsUnique().HasFilter("[order_id] IS NOT NULL");
            e.HasIndex(x => x.OrderItemId).IsUnique().HasFilter("[order_item_id] IS NOT NULL");
            e.HasIndex(x => x.TournamentRegistrationId, "UX_wallet_transactions_registration_fee").IsUnique().HasFilter("[tournament_registration_id] IS NOT NULL AND [transaction_type] = 'TournamentFee'")
                .HasDatabaseName("UX_wallet_transactions_registration_fee");
            e.HasIndex(x => x.TournamentRegistrationId, "UX_wallet_transactions_registration_refund").IsUnique().HasFilter("[tournament_registration_id] IS NOT NULL AND [transaction_type] = 'TournamentRefund'")
                .HasDatabaseName("UX_wallet_transactions_registration_refund");
        });

        model.Entity<ComputerType>(e =>
        {
            e.ToTable("computer_types", t => t.HasCheckConstraint("CK_computer_types_rate", "[hourly_rate] > 0"));
            e.Property(x => x.Name).HasColumnType("varchar(30)");
            Money(e.Property(x => x.HourlyRate));
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(
                new ComputerType { Id = 1, Name = "Normal", HourlyRate = 8000 },
                new ComputerType { Id = 2, Name = "FPS", HourlyRate = 12000 },
                new ComputerType { Id = 3, Name = "Couple", HourlyRate = 15000 },
                new ComputerType { Id = 4, Name = "FPS+", HourlyRate = 16000 },
                new ComputerType { Id = 5, Name = "Stream", HourlyRate = 22000 });
        });
        model.Entity<Computer>(e =>
        {
            e.ToTable("computers");
            e.Property(x => x.Code).HasColumnType("varchar(30)");
            e.Property(x => x.IsUnderMaintenance).HasDefaultValue(false);
            e.HasIndex(x => x.Code).IsUnique();
        });
        model.Entity<GamingSession>(e =>
        {
            e.ToTable("gaming_sessions", t =>
            {
                t.HasCheckConstraint("CK_gaming_sessions_amounts", "[hourly_rate_snapshot] > 0 AND [charged_amount] >= 0");
                t.HasCheckConstraint("CK_gaming_sessions_time", "[last_charged_at] >= [started_at] AND ([ended_at] IS NULL OR [ended_at] >= [started_at])");
            });
            Money(e.Property(x => x.HourlyRateSnapshot));
            Money(e.Property(x => x.ChargedAmount).HasDefaultValue(0m));
            e.HasIndex(x => new { x.CustomerId, x.Status });
            e.HasIndex(x => new { x.ComputerId, x.Status });
            e.HasIndex(x => x.CustomerId).IsUnique().HasFilter("[status] = 'Active'").HasDatabaseName("UX_gaming_sessions_active_customer");
            e.HasIndex(x => x.ComputerId).IsUnique().HasFilter("[status] = 'Active'").HasDatabaseName("UX_gaming_sessions_active_computer");
        });

        model.Entity<ProductCategory>(e =>
        {
            e.ToTable("product_categories");
            e.Property(x => x.Name).HasMaxLength(80);
            e.HasIndex(x => x.Name).IsUnique();
        });
        model.Entity<Product>(e =>
        {
            e.ToTable("products", t => t.HasCheckConstraint("CK_products_price", "[current_price] >= 0"));
            e.Property(x => x.Name).HasMaxLength(120);
            Money(e.Property(x => x.CurrentPrice));
        });
        model.Entity<Order>(e =>
        {
            e.ToTable("orders", t => t.HasCheckConstraint("CK_orders_total", "[total_amount] >= 0"));
            Money(e.Property(x => x.TotalAmount));
            e.HasIndex(x => x.GamingSessionId);
        });
        model.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items", t => t.HasCheckConstraint("CK_order_items_values", "[quantity] > 0 AND [unit_price_snapshot] >= 0"));
            Money(e.Property(x => x.UnitPriceSnapshot));
            e.Property(x => x.Status).HasDefaultValue(OrderItemStatus.Pending);
        });
        model.Entity<OrderItemCancelRequest>(e =>
        {
            e.ToTable("order_item_cancel_requests");
            e.Property(x => x.Status).HasDefaultValue(CancelRequestStatus.Pending);
            e.HasIndex(x => x.OrderItemId).IsUnique().HasFilter("[status] = 'Pending'").HasDatabaseName("UX_cancel_requests_pending_item");
        });

        model.Entity<Tournament>(e =>
        {
            e.ToTable("tournaments");
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Status).HasDefaultValue(TournamentStatus.Open);
        });
        model.Entity<TournamentRegistration>(e =>
        {
            e.ToTable("tournament_registrations", t => t.HasCheckConstraint("CK_tournament_registrations_fee", "[fee_amount_snapshot] > 0"));
            e.Property(x => x.RiotGameName).HasMaxLength(100);
            e.Property(x => x.RiotTagline).HasColumnType("varchar(20)");
            Money(e.Property(x => x.FeeAmountSnapshot).HasDefaultValue(50000m));
            e.Property(x => x.FeeStatus).HasDefaultValue(RegistrationFeeStatus.Paid);
            e.HasIndex(x => new { x.TournamentId, x.CustomerId }).IsUnique();
            e.HasIndex(x => new { x.TournamentId, x.RegisteredAt });
        });
        model.Entity<PlayerStatsSnapshot>(e =>
        {
            e.ToTable("player_stats_snapshots");
            e.Property(x => x.Source).HasColumnType("varchar(30)");
            e.Property(x => x.Scope).HasColumnType("varchar(80)");
            e.Property(x => x.WinRate).HasPrecision(5, 2);
            e.Property(x => x.HeadshotRate).HasPrecision(5, 2);
            e.Property(x => x.DamagePerRound).HasPrecision(10, 2);
            e.Property(x => x.Kda).HasPrecision(10, 2);
            e.HasIndex(x => x.TournamentRegistrationId).IsUnique();
        });
        model.Entity<TeamGenerationRun>(e =>
        {
            e.ToTable("team_generation_runs");
            e.Property(x => x.ModelName).HasColumnType("varchar(100)");
            e.Property(x => x.ModelVersion).HasColumnType("varchar(50)");
        });
        model.Entity<TeamProposal>(e =>
        {
            e.ToTable("team_proposals", t => t.HasCheckConstraint("CK_team_proposals_number", "[proposed_team_number] > 0"));
            e.Property(x => x.PredictedSkillScore).HasPrecision(10, 4);
            e.HasIndex(x => new { x.GenerationRunId, x.TournamentRegistrationId }).IsUnique();
        });
        model.Entity<Team>(e =>
        {
            e.ToTable("teams", t => t.HasCheckConstraint("CK_teams_number", "[team_number] > 0"));
            e.HasIndex(x => new { x.TournamentId, x.TeamNumber }).IsUnique();
        });
        model.Entity<TeamMember>(e =>
        {
            e.ToTable("team_members");
            e.HasIndex(x => x.TournamentRegistrationId).IsUnique();
        });

        // SQL Server has no automatic snake_case naming convention in EF Core.
        foreach (var entity in model.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(SnakeCase(property.Name));
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type.IsEnum)
                {
                    model.Entity(entity.ClrType).Property(property.Name).HasConversion(typeof(string)).HasColumnType("varchar(30)");
                }
            }
        }

        Reference<Account, Account>(model, nameof(Account.CreatedByAccountId));
        Reference<Customer, Account>(model, nameof(Customer.AccountId));
        Reference<TopUp, Customer>(model, nameof(TopUp.CustomerId));
        Reference<TopUp, Account>(model, nameof(TopUp.CreditedByAccountId));
        Reference<TopUp, Account>(model, nameof(TopUp.CollectedByAccountId));
        Reference<WalletTransaction, Customer>(model, nameof(WalletTransaction.CustomerId));
        Reference<WalletTransaction, TopUp>(model, nameof(WalletTransaction.TopUpId));
        Reference<WalletTransaction, GamingSession>(model, nameof(WalletTransaction.GamingSessionId));
        Reference<WalletTransaction, Order>(model, nameof(WalletTransaction.OrderId));
        Reference<WalletTransaction, OrderItem>(model, nameof(WalletTransaction.OrderItemId));
        Reference<WalletTransaction, TournamentRegistration>(model, nameof(WalletTransaction.TournamentRegistrationId));
        Reference<WalletTransaction, Account>(model, nameof(WalletTransaction.RecordedByAccountId));
        Reference<Computer, ComputerType>(model, nameof(Computer.ComputerTypeId));
        Reference<GamingSession, Customer>(model, nameof(GamingSession.CustomerId));
        Reference<GamingSession, Computer>(model, nameof(GamingSession.ComputerId));
        Reference<GamingSession, Account>(model, nameof(GamingSession.EndedByAccountId));
        Reference<Product, ProductCategory>(model, nameof(Product.CategoryId));
        Reference<Order, Customer>(model, nameof(Order.CustomerId));
        Reference<Order, GamingSession>(model, nameof(Order.GamingSessionId));
        Reference<OrderItem, Order>(model, nameof(OrderItem.OrderId));
        Reference<OrderItem, Product>(model, nameof(OrderItem.ProductId));
        Reference<OrderItem, Account>(model, nameof(OrderItem.ConfirmedByAccountId));
        Reference<OrderItem, Account>(model, nameof(OrderItem.CancelledByAccountId));
        Reference<OrderItemCancelRequest, OrderItem>(model, nameof(OrderItemCancelRequest.OrderItemId));
        Reference<OrderItemCancelRequest, Customer>(model, nameof(OrderItemCancelRequest.RequestedByCustomerId));
        Reference<OrderItemCancelRequest, Account>(model, nameof(OrderItemCancelRequest.HandledByAccountId));
        Reference<Tournament, Account>(model, nameof(Tournament.CreatedByAccountId));
        Reference<Tournament, Account>(model, nameof(Tournament.CancelledByAccountId));
        Reference<TournamentRegistration, Tournament>(model, nameof(TournamentRegistration.TournamentId));
        Reference<TournamentRegistration, Customer>(model, nameof(TournamentRegistration.CustomerId));
        Reference<PlayerStatsSnapshot, TournamentRegistration>(model, nameof(PlayerStatsSnapshot.TournamentRegistrationId));
        Reference<TeamGenerationRun, Tournament>(model, nameof(TeamGenerationRun.TournamentId));
        Reference<TeamGenerationRun, Account>(model, nameof(TeamGenerationRun.GeneratedByAccountId));
        Reference<TeamProposal, TeamGenerationRun>(model, nameof(TeamProposal.GenerationRunId));
        Reference<TeamProposal, TournamentRegistration>(model, nameof(TeamProposal.TournamentRegistrationId));
        Reference<Team, Tournament>(model, nameof(Team.TournamentId));
        Reference<Team, Account>(model, nameof(Team.FinalizedByAccountId));
        Reference<TeamMember, Team>(model, nameof(TeamMember.TeamId));
        Reference<TeamMember, TournamentRegistration>(model, nameof(TeamMember.TournamentRegistrationId));
    }

    private static void Money(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<decimal> property) => property.HasPrecision(18, 0);
    private static string SnakeCase(string name) => Regex.Replace(name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
    private static void Reference<TDependent, TPrincipal>(ModelBuilder model, string foreignKey)
        where TDependent : class where TPrincipal : class =>
        model.Entity<TDependent>().HasOne<TPrincipal>().WithMany().HasForeignKey(foreignKey).OnDelete(DeleteBehavior.NoAction);
}

// Design-time creation only builds the model and migration; it never opens a SQL connection.
public sealed class CyberCafeDesignTimeFactory : IDesignTimeDbContextFactory<CyberCafeDbContext>
{
    public CyberCafeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CyberCafe")
            ?? "Server=127.0.0.1,1;Database=CyperCafe;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<CyberCafeDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new CyberCafeDbContext(options);
    }
}
