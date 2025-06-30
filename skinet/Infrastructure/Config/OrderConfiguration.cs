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
            a => (OrderStatus)Enum.Parse(typeof(OrderStatus), a)
        );

        // builder.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");

        builder.HasMany(x => x.OrderItems).WithOne().OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.OrderDate).HasConversion(
            d => d.ToUniversalTime(),
            d => DateTime.SpecifyKind(d, DateTimeKind.Utc)
        );
    }
}