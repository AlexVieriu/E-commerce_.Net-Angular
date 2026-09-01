namespace Infrastructure.Config;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // To constrain an address column, switch to the lambda form:
        // OwnsOne(o => o.ShippingAddress, a => a.Property(x => x.City).HasMaxLength(100));
        builder.OwnsOne(o => o.ShippingAddress);
        builder.OwnsOne(o => o.PaymentSummary);

        builder.Property(o => o.Status)
               .HasConversion(a => a.ToString(),                // C# → DB  (writing)
                              a => Enum.Parse<OrderStatus>(a)); // DB → C#  (reading)

        builder.Property(o => o.BuyerEmail)
               .HasMaxLength(256);

        builder.HasMany(x => x.OrderItems)        // an Order has many OrderItems
               .WithOne()                         // each OrderItem belongs to exactly one Order
               .OnDelete(DeleteBehavior.Cascade); // delete the Order -> delete its items

        builder.HasIndex(o => o.PaymentIntentId)
               .IsUnique();

        // Order list: WHERE BuyerEmail, ORDER BY OrderDate DESC — the DESC must match.
        // Leftmost prefix covers BuyerEmail alone, so it needs no index of its own.
        builder.HasIndex(o => new { o.BuyerEmail, o.OrderDate })
               .IsDescending(false, true);

        builder.Property(x => x.OrderDate)
               .HasConversion(d => d.ToUniversalTime(), // C# → DB   (writing)
                              d => DateTime.SpecifyKind(d, DateTimeKind.Utc)); // DB → C#  (reading)
    }
}
