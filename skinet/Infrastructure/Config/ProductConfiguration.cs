namespace Infrastructure.Config;

// -- IEntityTypeConfiguration --
// Allows configuration for an entity type to be factored into a separate class,
// rather than in-line in Microsoft.EntityFrameworkCore.DbContext.OnModelCreating(...)
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        // builder.Property(x => x.Price).HasColumnType("REAL");
    }
}