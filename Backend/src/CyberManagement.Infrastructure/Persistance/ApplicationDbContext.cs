using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberManagement.Infrastructure.Persistance;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Identity
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Customers
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerStatusHistory> CustomerStatusHistories => Set<CustomerStatusHistory>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();

    // Computers
    public DbSet<ComputerRoom> ComputerRooms => Set<ComputerRoom>();
    public DbSet<ComputerType> ComputerTypes => Set<ComputerType>();
    public DbSet<Computer> Computers => Set<Computer>();
    public DbSet<ComputerMaintenance> ComputerMaintenances => Set<ComputerMaintenance>();
    public DbSet<GamingSession> GamingSessions => Set<GamingSession>();
    public DbSet<SessionCharge> SessionCharges => Set<SessionCharge>();

    // Inventory
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();
    public DbSet<StockReceiptDetail> StockReceiptDetails => Set<StockReceiptDetail>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    // Sales
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceOrder> InvoiceOrders => Set<InvoiceOrder>();
    public DbSet<InvoiceDetail> InvoiceDetails => Set<InvoiceDetail>();
    public DbSet<Payment> Payments => Set<Payment>();

    // Tournaments
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameRank> GameRanks => Set<GameRank>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentRegistration> TournamentRegistrations => Set<TournamentRegistration>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);
    }
}