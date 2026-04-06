namespace Infrastructure.Tests.Services;

public class OrderService_ConcurrencyTests
{
    private static DbContextOptions<StoreContext> InMemoryOptions()
        => new DbContextOptionsBuilder<StoreContext>()
            .UseInMemoryDatabase("ConcurrencyTest_" + Guid.NewGuid())
            .Options;

    private static ShoppingCart MakeCart(int productId, int deliveryMethodId) => new()
    {
        Id = "test-cart",
        PaymentIntentId = "pi_test_123",
        DeliveryMethodId = deliveryMethodId,
        Items =
        [
            new CartItem
            {
                ProductId = productId,
                ProductName = "Test Board",
                Price = 199m,
                Quantity = 1,
                PictureUrl = "/images/test.png",
                Brand = "TestBrand",
                Type = "Boards"
            }
        ]
    };

    private static CreateOrderDto MakeOrderDto(int deliveryMethodId) => new()
    {
        CartId = "test-cart",
        DeliveryMethodId = deliveryMethodId,
        ShippingAddress = new ShippingAddress
        {
            Name = "John Doe",
            Line1 = "123 Main St",
            City = "London",
            State = "England",
            PostalCode = "SW1A 1AA",
            Country = "GB"
        },
        PaymentSummary = new PaymentSummary
        {
            Last4 = 4242,
            Brand = "visa",
            ExpMonth = 12,
            ExpYear = 2027
        }
    };

    [Fact]
    public async Task CreateOrderAsync_WhenStockModifiedConcurrently_SecondOrderFails()
    {
        // Arrange
        var options = InMemoryOptions();

        using (var seed = new StoreContext(options))
        {
            seed.Products.Add(new Product
            {
                Id = 1, Name = "Test Board", Description = "desc",
                Price = 199m, PictureUrl = "/img.png",
                Brand = "Test", Type = "Boards", QuantityInStock = 1
            });
            seed.DeliveryMethods.Add(new DeliveryMethod
            {
                Id = 1, ShortName = "UPS1", DeliveryTime = "1-2 Days",
                Price = 5m, Description = "Standard"
            });
            await seed.SaveChangesAsync();
        }

        var ctx1 = new StoreContext(options);
        var ctx2 = new StoreContext(options);

        // Pre-load into both contexts before either saves — simulates the race
        await ctx1.Products.FindAsync(1);
        await ctx2.Products.FindAsync(1);

        var cart1 = new Mock<ICartService>();
        cart1.Setup(x => x.GetCartAsync("test-cart")).ReturnsAsync(MakeCart(1, 1));

        var cart2 = new Mock<ICartService>();
        cart2.Setup(x => x.GetCartAsync("test-cart")).ReturnsAsync(MakeCart(1, 1));

        var service1 = new OrderService(new UnitOfWork(ctx1), cart1.Object);
        var service2 = new OrderService(new UnitOfWork(ctx2), cart2.Object);

        // Act
        var order1 = await service1.CreateOrderAsync("user1@test.com", MakeOrderDto(1));
        var act2 = async () => await service2.CreateOrderAsync("user2@test.com", MakeOrderDto(1));

        // Assert
        order1.Should().NotBeNull();
        await act2.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*concurrent*");

        using var verify = new StoreContext(options);
        var product = await verify.Products.FindAsync(1);
        product!.QuantityInStock.Should().Be(0);
    }
}
