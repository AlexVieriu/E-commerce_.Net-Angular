namespace Infrastructure.Config;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // OwnsOne: own entity types
        // WithOwner: establishes the relationship back to the owner
        builder.OwnsOne(o => o.ShippingAddress, a => a.WithOwner());
        builder.OwnsOne(o => o.PaymentSummary, a => a.WithOwner());

        // for Enum conversation
        builder.Property(o => o.Status).HasConversion(
            a => a.ToString(),
            a => Enum.Parse<OrderStatus>(a)
        );

        // Same value as AspNetUsers.Email — Identity declares that nvarchar(256).
        builder.Property(o => o.BuyerEmail).HasMaxLength(256);

        // builder.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");

        builder.HasMany(x => x.OrderItems).WithOne().OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => o.PaymentIntentId).IsUnique();

        // Order list: filters BuyerEmail, sorts OrderDate DESC. The DESC must match the ORDER BY.
        // Leftmost prefix answers ByIdAndEmail too, so BuyerEmail needs no index of its own.
        builder.HasIndex(o => new { o.BuyerEmail, o.OrderDate }).IsDescending(false, true);

        builder.Property(x => x.OrderDate).HasConversion(
            d => d.ToUniversalTime(),
            d => DateTime.SpecifyKind(d, DateTimeKind.Utc)
        );
    }
}