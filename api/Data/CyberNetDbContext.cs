using CyberNet.Models;
using Microsoft.EntityFrameworkCore;

namespace CyberNet.Data;

public class CyberNetDbContext : DbContext
{
    public CyberNetDbContext(DbContextOptions<CyberNetDbContext> options) : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Computer> Computers => Set<Computer>();
    public DbSet<ComputerMaintenance> ComputerMaintenances => Set<ComputerMaintenance>();
    public DbSet<ComputerRoom> ComputerRooms => Set<ComputerRoom>();
    public DbSet<ComputerType> ComputerTypes => Set<ComputerType>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerStatusHistory> CustomerStatusHistories => Set<CustomerStatusHistory>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeSession> EmployeeSessions => Set<EmployeeSession>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameRank> GameRanks => Set<GameRank>();
    public DbSet<GamingSession> GamingSessions => Set<GamingSession>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceDetail> InvoiceDetails => Set<InvoiceDetail>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<SessionCharge> SessionCharges => Set<SessionCharge>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();
    public DbSet<StockReceiptDetail> StockReceiptDetails => Set<StockReceiptDetail>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentRegistration> TournamentRegistrations => Set<TournamentRegistration>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasCharSet("utf8mb4");

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.Employee).WithMany(x => x.AuditLogs).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.HasIndex(x => x.CategoryName).IsUnique();
        });

        modelBuilder.Entity<Computer>(e =>
        {
            e.HasIndex(x => x.ComputerName).IsUnique();
            e.HasOne(x => x.Room).WithMany(x => x.Computers).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ComputerType).WithMany(x => x.Computers).HasForeignKey(x => x.ComputerTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ComputerMaintenance>(e =>
        {
            e.HasOne(x => x.Computer).WithMany(x => x.ComputerMaintenances).HasForeignKey(x => x.ComputerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Employee).WithMany(x => x.ComputerMaintenances).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ComputerRoom>(e =>
        {
            e.HasIndex(x => x.RoomName).IsUnique();
        });

        modelBuilder.Entity<ComputerType>(e =>
        {
            e.HasIndex(x => x.TypeName).IsUnique();
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.WalletBalance).HasDefaultValue(0m);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<CustomerStatusHistory>(e =>
        {
            e.Property(x => x.ChangedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.Customer).WithMany(x => x.CustomerStatusHistories).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ChangedByEmployee).WithMany(x => x.ChangedCustomerStatusHistories).HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Employee>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.Role).WithMany(x => x.Employees).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmployeeSession>(e =>
        {
            e.Property(x => x.LoginTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.Employee).WithMany(x => x.EmployeeSessions).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Game>(e =>
        {
            e.HasIndex(x => x.GameName).IsUnique();
        });

        modelBuilder.Entity<GameRank>(e =>
        {
            e.HasIndex(x => new { x.GameId, x.RankOrder }).IsUnique();
            e.HasOne(x => x.Game).WithMany(x => x.GameRanks).HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GamingSession>(e =>
        {
            e.Property(x => x.TotalCost).HasDefaultValue(0m);
            e.HasOne(x => x.Computer).WithMany(x => x.GamingSessions).HasForeignKey(x => x.ComputerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Customer).WithMany(x => x.GamingSessions).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryTransaction>(e =>
        {
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.Product).WithMany(x => x.InventoryTransactions).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Employee).WithMany(x => x.InventoryTransactions).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invoice>(e =>
        {
            e.Property(x => x.InvoiceDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.Discount).HasDefaultValue(0m);
            e.HasOne(x => x.Customer).WithMany(x => x.Invoices).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.GamingSession).WithMany(x => x.Invoices).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Employee).WithMany(x => x.Invoices).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Promotion).WithMany(x => x.Invoices).HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceDetail>(e =>
        {
            e.HasOne(x => x.Invoice).WithMany(x => x.InvoiceDetails).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.Property(x => x.OrderTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.Customer).WithMany(x => x.Orders).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.GamingSession).WithMany(x => x.Orders).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Employee).WithMany(x => x.Orders).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Invoice).WithMany(x => x.Orders).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderDetail>(e =>
        {
            e.HasOne(x => x.Order).WithMany(x => x.OrderDetails).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany(x => x.OrderDetails).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderStatusHistory>(e =>
        {
            e.Property(x => x.ChangedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.Order).WithMany(x => x.OrderStatusHistories).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ChangedByEmployee).WithMany(x => x.ChangedOrderStatusHistories).HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.Property(x => x.PaymentTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.WalletTransaction).WithMany(x => x.Payments).HasForeignKey(x => x.WalletTransactionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.HasIndex(x => x.PermissionName).IsUnique();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.Property(x => x.Stock).HasDefaultValue(0);
            e.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.HasIndex(x => x.RoleName).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SessionCharge>(e =>
        {
            e.HasOne(x => x.GamingSession).WithMany(x => x.SessionCharges).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Shift>(e =>
        {
            e.HasOne(x => x.Employee).WithMany(x => x.Shifts).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockReceipt>(e =>
        {
            e.Property(x => x.ReceiptDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.Supplier).WithMany(x => x.StockReceipts).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Employee).WithMany(x => x.StockReceipts).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockReceiptDetail>(e =>
        {
            e.HasOne(x => x.Receipt).WithMany(x => x.StockReceiptDetails).HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany(x => x.StockReceiptDetails).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Team>(e =>
        {
            e.HasIndex(x => new { x.TournamentId, x.TeamName }).IsUnique();
            e.HasOne(x => x.Tournament).WithMany(x => x.Teams).HasForeignKey(x => x.TournamentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TeamMember>(e =>
        {
            e.HasKey(x => new { x.TeamId, x.RegistrationId });
            e.HasOne(x => x.Team).WithMany(x => x.TeamMembers).HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Registration).WithMany(x => x.TeamMembers).HasForeignKey(x => x.RegistrationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Tournament>(e =>
        {
            e.Property(x => x.EntryFee).HasDefaultValue(0m);
            e.HasOne(x => x.Game).WithMany(x => x.Tournaments).HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByEmployee).WithMany(x => x.CreatedTournaments).HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TournamentRegistration>(e =>
        {
            e.HasIndex(x => new { x.TournamentId, x.CustomerId }).IsUnique();
            e.Property(x => x.RegisteredAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.Tournament).WithMany(x => x.TournamentRegistrations).HasForeignKey(x => x.TournamentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Customer).WithMany(x => x.TournamentRegistrations).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Rank).WithMany(x => x.TournamentRegistrations).HasForeignKey(x => x.RankId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WalletTransaction>(e =>
        {
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.Customer).WithMany(x => x.WalletTransactions).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
