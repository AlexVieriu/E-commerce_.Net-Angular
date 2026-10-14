// Test project globally imports StackExchange.Redis, which also defines an `Order` type.
using Order = Core.Entities.OrderAggregate.Order;

namespace Infrastructure.Tests.Services;

public class DashboardService_Tests
{
    private static DbContextOptions<StoreContext> InMemoryOptions()
        => new DbContextOptionsBuilder<StoreContext>()
            .UseInMemoryDatabase("Dashboard_" + Guid.NewGuid())
            .Options;

    private static Product MakeProduct(int id, string name, int qty) => new()
    {
        Id = id, Name = name, Description = "desc", Price = 10m,
        PictureUrl = "/img.png", Brand = "Test", Type = "Boards", QuantityInStock = qty
    };

    private static Order MakeOrder(
        int id, OrderStatus status, decimal subtotal, decimal discount,
        DeliveryMethod deliveryMethod, int daysAgo) => new()
    {
        Id = id,
        BuyerEmail = $"buyer{id}@test.com",
        PaymentIntentId = $"pi_{id}",
        OrderDate = DateTime.UtcNow.AddDays(-daysAgo),
        Status = status,
        Subtotal = subtotal,
        Discount = discount,
        DeliveryMethod = deliveryMethod,
        ShippingAddress = new ShippingAddress
        {
            Name = "N", Line1 = "L1", City = "C", State = "S", PostalCode = "P", Country = "GB"
        },
        PaymentSummary = new PaymentSummary { Last4 = 4242, Brand = "visa", ExpMonth = 1, ExpYear = 2030 },
        OrderItems = []
    };

    private static DeliveryMethod MakeDelivery() => new()
    {
        Id = 1, ShortName = "UPS1", DeliveryTime = "1-2 Days", Price = 5m, Description = "Standard"
    };

    [Fact]
    public async Task GetDashboardAsync_ComputesCounts_RevenueRule_AndLowStock()
    {
        var options = InMemoryOptions();
        using (var seed = new StoreContext(options))
        {
            seed.Products.AddRange(
                MakeProduct(1, "Zero Stock", 0),
                MakeProduct(2, "Low Three", 3),
                MakeProduct(3, "Edge Five", 5),   // == threshold counts as low
                MakeProduct(4, "Plenty", 50));     // not low

            var dm = MakeDelivery();
            seed.Orders.AddRange(
                MakeOrder(1, OrderStatus.PaymentReceived, 100m, 10m, dm, daysAgo: 0), // total 95
                MakeOrder(2, OrderStatus.PaymentReceived, 200m, 0m, dm, daysAgo: 1),  // total 205
                MakeOrder(3, OrderStatus.Pending, 50m, 0m, dm, daysAgo: 2),           // excluded
                MakeOrder(4, OrderStatus.Refunded, 300m, 0m, dm, daysAgo: 3),         // excluded
                MakeOrder(5, OrderStatus.PaymentFailed, 80m, 0m, dm, daysAgo: 4));    // excluded
            await seed.SaveChangesAsync();
        }

        using var ctx = new StoreContext(options);
        var service = new DashboardService(new UnitOfWork(ctx));

        var result = await service.GetDashboardAsync();

        result.TotalProducts.Should().Be(4);
        result.TotalOrders.Should().Be(5);
        result.PendingOrders.Should().Be(1);
        // Only the two PaymentReceived orders count: 95 + 205.
        result.TotalRevenue.Should().Be(300m);

        result.LowStockCount.Should().Be(3);
        result.LowStockProducts.Select(p => p.QuantityInStock).Should().Equal(0, 3, 5); // ascending

        // Newest first; total maps via GetTotal() (subtotal - discount + shipping).
        result.RecentOrders.Select(o => o.Id).Should().Equal(1, 2, 3, 4, 5);
        result.RecentOrders[0].Total.Should().Be(95m);
        result.RecentOrders[0].Status.Should().Be("PaymentReceived");
    }

    [Fact]
    public async Task GetDashboardAsync_CapsPreviewLists_ButKeepsCountsExact()
    {
        var options = InMemoryOptions();
        using (var seed = new StoreContext(options))
        {
            // 18 products all at/under the threshold -> all low.
            for (var i = 1; i <= 18; i++)
                seed.Products.Add(MakeProduct(i, $"Low {i}", i % 6)); // qty 0..5

            var dm = MakeDelivery();
            for (var i = 1; i <= 18; i++)
                seed.Orders.Add(MakeOrder(i, OrderStatus.PaymentReceived, 100m, 0m, dm, daysAgo: i));
            await seed.SaveChangesAsync();
        }

        using var ctx = new StoreContext(options);
        var service = new DashboardService(new UnitOfWork(ctx));

        var result = await service.GetDashboardAsync();

        result.LowStockCount.Should().Be(18);            // true count
        result.LowStockProducts.Should().HaveCount(15);  // preview capped
        result.TotalOrders.Should().Be(18);              // true count
        result.RecentOrders.Should().HaveCount(15);      // preview capped
    }
}
