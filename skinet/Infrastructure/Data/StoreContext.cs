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

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderConfiguration).Assembly);

        // Apply common configurations
        ApplyCommonConfigurations(modelBuilder);

        // Apply database-specific configurations (to be overridden in derived classes)
        ApplyDatabaseSpecificConfigurations(modelBuilder);

        // -- Old code --
        // .ApplyConfigurationsFromAssembly : applies configuration from all IEntityTypeConfiguration instances that are defined in provided assembly 
        // .Assembly : gets the assembly that contains the type of the current instance
        // modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductConfiguration).Assembly);

    }

    protected virtual void ApplyCommonConfigurations(ModelBuilder modelBuilder)
    {
        // Common configurations are now handled by IEntityTypeConfiguration classes
        // loaded via ApplyConfigurationsFromAssembly (OrderConfiguration, ProductConfiguration, etc.)
        // This method can be used for any additional common configurations if needed
    }

    protected virtual void ApplyDatabaseSpecificConfigurations(ModelBuilder modelBuilder)
    {
        // Default implementation - can be overridden in derived classes
    }
}
