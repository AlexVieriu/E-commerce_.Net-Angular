namespace Infrastructure.Config;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        // Length first: SQL Server can't index nvarchar(max). Skip these and EF picks 450.
        builder.Property(p => p.Brand)
               .HasMaxLength(30);

        builder.Property(p => p.Type)
               .HasMaxLength(30);

        builder.Property(p => p.Name)
               .HasMaxLength(100);

        // Shop grid filtered: WHERE Brand IN (...) AND Type = ...
        builder.HasIndex(p => new { p.Brand, p.Type });

        // Landing page: no WHERE at all, just ORDER BY Name.
        builder.HasIndex(p => p.Name);

        // Price sorts both ways. One ascending index serves both —
        // SQL Server reads it backwards for DESC.
        builder.HasIndex(p => p.Price);
    }
}
