using CyberNet.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberNet.Data;

public class CyberNetDbContext : DbContext
{
    public CyberNetDbContext(DbContextOptions<CyberNetDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Computer> Computers => Set<Computer>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<GamingSession> GamingSessions => Set<GamingSession>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tự động nạp tất cả class cấu hình trong thư mục Configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CyberNetDbContext).Assembly);
    }
}
