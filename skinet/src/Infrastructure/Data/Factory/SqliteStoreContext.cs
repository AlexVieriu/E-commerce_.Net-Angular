namespace Infrastructure.Data.Factory;

public class SqliteStoreContext : StoreContext
{
    public SqliteStoreContext(DbContextOptions<SqliteStoreContext> options) : base(options) { }

    protected override void ApplyDatabaseSpecificConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .Property(x => x.Price)
            .HasColumnType("REAL");

        var couponEntity = modelBuilder.Entity<AppCoupon>();
        couponEntity.Property(x => x.AmountOff)
            .HasColumnType("REAL");
        couponEntity.Property(x => x.PercentOff)
            .HasColumnType("REAL");

        modelBuilder.Entity<DeliveryMethod>()
            .Property(e => e.Price)
            .HasColumnType("REAL");

        modelBuilder.Entity<Order>()
            .Property(e => e.Discount)
            .HasColumnType("REAL");

        modelBuilder.Entity<Order>()
            .Property(e => e.Subtotal)
            .HasColumnType("REAL");

        modelBuilder.Entity<OrderItem>()
            .Property(e => e.Price)
            .HasColumnType("REAL");
    }
}
