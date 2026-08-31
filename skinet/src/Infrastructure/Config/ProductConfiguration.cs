namespace Infrastructure.Config;

// -- IEntityTypeConfiguration --
// Allows configuration for an entity type to be factored into a separate class,
// rather than in-line in Microsoft.EntityFrameworkCore.DbContext.OnModelCreating(...)
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        // builder.Property(x => x.Price).HasColumnType("REAL");

        // Length first: SQL Server can't index nvarchar(max). Skip these and EF picks 450.
        builder.Property(p => p.Brand).HasMaxLength(30);
        builder.Property(p => p.Type).HasMaxLength(30);
        builder.Property(p => p.Name).HasMaxLength(100);

        // Shop grid filtered: WHERE Brand IN (...) AND Type = ...
        builder.HasIndex(p => new { p.Brand, p.Type });

        // Landing page: no WHERE at all, just ORDER BY Name.
        builder.HasIndex(p => p.Name);

        // Same page, sort flipped. Ascending covers both — the index reads backwards.
        builder.HasIndex(p => p.Price);
    }
}