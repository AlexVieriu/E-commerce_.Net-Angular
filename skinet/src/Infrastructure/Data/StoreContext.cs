namespace Infrastructure.Data;

public class StoreContext : IdentityDbContext<AppUser>
{
    public StoreContext()
    {

    }

    public StoreContext(DbContextOptions options) : base(options)
    {

    }

    public DbSet<Product> Products { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<DeliveryMethod> DeliveryMethods { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    // Named `builder` to match IdentityDbContext.OnModelCreating(ModelBuilder builder) — a mismatched
    // override parameter name breaks callers who pass it by name.
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(OrderConfiguration).Assembly);

        // Provider-specific settings, e.g. SQL Server's decimal column types.
        ApplyDatabaseSpecificConfigurations(builder);
    }

    protected virtual void ApplyDatabaseSpecificConfigurations(ModelBuilder modelBuilder)
    {
    }
}
