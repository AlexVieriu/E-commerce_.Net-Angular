namespace Infrastructure.Config;

public class RoleConfiguration : IEntityTypeConfiguration<IdentityRole>
{
    public void Configure(EntityTypeBuilder<IdentityRole> builder)
    {
        builder.HasData(
            new IdentityRole { Id = "admin-id", Name = "Admin", NormalizedName = "ADMIN", ConcurrencyStamp = "admin-concurrency-stamp" },           
            new IdentityRole { Id = "customer-id", Name = "Customer", NormalizedName = "CUSTOMER", ConcurrencyStamp = "admin-concurrency-stamp" }            
        );
    }
}