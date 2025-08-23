namespace Infrastructure.Data.Factory;

public class SqlServerStoreContext : StoreContext
{
    public SqlServerStoreContext(DbContextOptions<SqlServerStoreContext> options) : base(options) { }

    protected override void ApplyDatabaseSpecificConfigurations(ModelBuilder modelBuilder)
    {
        // SQL Server-specific configurations

        // Configure Product price for SQL Server with precision
        modelBuilder.Entity<Product>()
            .Property(x => x.Price)
            .HasColumnType("decimal(18,2)");

        // Configure AppCoupon decimal properties for SQL Server
        var couponEntity = modelBuilder.Entity<AppCoupon>();

        // AmountOff in cents, so no decimal places needed
        couponEntity.Property(x => x.AmountOff)
            .HasColumnType("decimal(18,0)");

        // PercentOff with 2 decimal places for precision (e.g., 25.50%)
        couponEntity.Property(x => x.PercentOff)
            .HasColumnType("decimal(5,2)");

        // Fix the warnings by adding missing decimal configurations
        modelBuilder.Entity<DeliveryMethod>()
            .Property(e => e.Price)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Order>()
            .Property(e => e.Discount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Order>()
            .Property(e => e.Subtotal)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<OrderItem>()
            .Property(e => e.Price)
            .HasColumnType("decimal(18,2)");
    }
}
