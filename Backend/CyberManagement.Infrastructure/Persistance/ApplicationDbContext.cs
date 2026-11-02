namespace CyberManagement.Infrastructure.Persistance;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceDetail> InvoiceDetails => Set<InvoiceDetail>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Customers> Customers => Set<Customers>();
    public DbSet<Employees> Employees => Set<Employees>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Promotions> Promotions => Set<Promotions>();
    public DbSet<Computers> Computers => Set<Computers>();
    public DbSet<ComputerTypes> ComputerTypes => Set<ComputerTypes>();
    public DbSet<ComputerRooms> ComputerRooms => Set<ComputerRooms>();
    public DbSet<GamingSessions> GamingSessions => Set<GamingSessions>();

    public DbSet<InventoryTransaction> InventoryTransactions
        => Set<InventoryTransaction>();

    public DbSet<WalletTransactions> WalletTransactions
        => Set<WalletTransactions>();

    public DbSet<OrderStatusHistory> OrderStatusHistories
        => Set<OrderStatusHistory>();

    public DbSet<OrderItemCancelRequest> OrderItemCancelRequests
        => Set<OrderItemCancelRequest>();
    public DbSet<PromotionUsage> PromotionUsages => Set<PromotionUsage>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);
    }
}