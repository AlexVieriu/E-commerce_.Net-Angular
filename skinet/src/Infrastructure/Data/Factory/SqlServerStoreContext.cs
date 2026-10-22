namespace Infrastructure.Data.Factory;

public class SqlServerStoreContext : StoreContext
{
    public SqlServerStoreContext(DbContextOptions<SqlServerStoreContext> options) : base(options) { }

    protected override void ApplyDatabaseSpecificConfigurations(ModelBuilder modelBuilder)
    {
        // Every decimal gets a column type, or EF warns that SQL Server may silently truncate it.
        modelBuilder.Entity<Product>()
            .Property(x => x.Price)
            .HasColumnType("decimal(18,2)");

        var couponEntity = modelBuilder.Entity<AppCoupon>();

        couponEntity.Property(x => x.AmountOff)
            .HasColumnType("decimal(18,0)");

        // e.g. 25.50 (%)
        couponEntity.Property(x => x.PercentOff)
            .HasColumnType("decimal(5,2)");

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
