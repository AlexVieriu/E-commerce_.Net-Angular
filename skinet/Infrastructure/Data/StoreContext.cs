namespace Infrastructure.Data;

public class StoreContext(DbContextOptions options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Product> Products { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<DeliveryMethod> DeliveryMethods { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // .ApplyConfigurationsFromAssembly : applies configuration from all IEntityTypeConfiguration instances that are defined in provided assembly 
        // .Assembly : gets the assembly that contains the type of the current instance
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductConfiguration).Assembly);

    }
}
