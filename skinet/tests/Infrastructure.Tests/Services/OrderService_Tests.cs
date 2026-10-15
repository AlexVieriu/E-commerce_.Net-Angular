namespace Infrastructure.Tests.Services;

public class OrderService_HappyPathTests
{
    private static DbContextOptions<StoreContext> InMemoryOptions()
        => new DbContextOptionsBuilder<StoreContext>()
            .UseInMemoryDatabase("OrderService_" + Guid.NewGuid())
            .Options;

    private static ShoppingCart MakeCart(
        int productId = 1,
        int deliveryMethodId = 1,
        decimal price = 100m,
        int quantity = 2,
        string paymentIntentId = "pi_happy",
        AppCoupon? coupon = null)
    => new()
    {
        PaymentIntentId = paymentIntentId,
        DeliveryMethodId = deliveryMethodId,
        Coupon = coupon,
        Items =
        [
            new CartItem
            {
                ProductId = productId,
                ProductName = "Test Board",
                Price = price,
                Quantity = quantity,
                PictureUrl = "/images/board.png",
                Brand = "TestBrand",
                Type = "Boards"
            }
        ]
    };

    private static ICouponService NoCoupons() => Mock.Of<ICouponService>();

    private static CreateOrderDto MakeOrderDto(int deliveryMethodId = 1) => new()
    {
        DeliveryMethodId = deliveryMethodId,
        ShippingAddress = new ShippingAddress
        {
            Name = "Jane Doe",
            Line1 = "1 Test St",
            City = "London",
            State = "England",
            PostalCode = "EC1A 1BB",
            Country = "GB"
        },
        PaymentSummary = new PaymentSummary
        {
            Last4 = 1234,
            Brand = "visa",
            ExpMonth = 6,
            ExpYear = 2028
        }
    };

    [Fact]
    public async Task CreateOrderAsync_WithValidCart_ShouldCreateOrderWithCorrectSubtotal()
    {
        var options = InMemoryOptions();

        using (var seed = new StoreContext(options))
        {
            seed.Products.Add(new Product
            {
                Id = 1, Name = "Test Board", Description = "desc",
                Price = 100m, PictureUrl = "/img.png",
                Brand = "Test", Type = "Boards", QuantityInStock = 10
            });
            seed.DeliveryMethods.Add(new DeliveryMethod
            {
                Id = 1, ShortName = "UPS1", DeliveryTime = "1-2 Days",
                Price = 5m, Description = "Standard"
            });
            await seed.SaveChangesAsync();
        }

        using var ctx = new StoreContext(options);
        var cartMock = new Mock<ICartService>();
        cartMock.Setup(x => x.GetCartAsync()).ReturnsAsync(MakeCart());

        var service = new OrderService(new UnitOfWork(ctx), cartMock.Object, NoCoupons());

        var order = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto());

        order.Should().NotBeNull();
        order!.Subtotal.Should().Be(200m);      // price 100 * qty 2
        order.Discount.Should().Be(0m);          // no coupon
        order.BuyerEmail.Should().Be("buyer@test.com");
        order.OrderItems.Should().HaveCount(1);
        order.DeliveryMethod.Price.Should().Be(5m);
    }

    // The order is created before the card is charged, so a decline has to be able to walk back
    // through the cart. Deleting it here ended the checkout one step before the money moved. (BUG-2)
    [Fact]
    public async Task CreateOrderAsync_ShouldKeepTheCart_SoADeclinedCardCanBeRetried()
    {
        var options = InMemoryOptions();

        using (var seed = new StoreContext(options))
        {
            seed.Products.Add(new Product
            {
                Id = 1, Name = "Test Board", Description = "desc",
                Price = 100m, PictureUrl = "/img.png",
                Brand = "Test", Type = "Boards", QuantityInStock = 10
            });
            seed.DeliveryMethods.Add(new DeliveryMethod
            {
                Id = 1, ShortName = "UPS1", DeliveryTime = "1-2 Days",
                Price = 5m, Description = "Standard"
            });
            await seed.SaveChangesAsync();
        }

        using var ctx = new StoreContext(options);
        var cartMock = new Mock<ICartService>();
        cartMock.Setup(x => x.GetCartAsync()).ReturnsAsync(MakeCart());

        var service = new OrderService(new UnitOfWork(ctx), cartMock.Object, NoCoupons());

        var order = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto());

        order.Should().NotBeNull();
        cartMock.Verify(x => x.DeleteCartAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenCartNotFound_ShouldReturnNull()
    {
        var options = InMemoryOptions();

        using var ctx = new StoreContext(options);
        var cartMock = new Mock<ICartService>();
        cartMock.Setup(x => x.GetCartAsync()).ReturnsAsync((ShoppingCart?)null);

        var service = new OrderService(new UnitOfWork(ctx), cartMock.Object, NoCoupons());

        var result = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto());

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateOrderAsync_WhenCartHasNoPaymentIntentId_ShouldReturnNull()
    {
        var options = InMemoryOptions();

        using var ctx = new StoreContext(options);
        var cartMock = new Mock<ICartService>();
        cartMock.Setup(x => x.GetCartAsync())
            .ReturnsAsync(MakeCart(paymentIntentId: null!));

        var service = new OrderService(new UnitOfWork(ctx), cartMock.Object, NoCoupons());

        var result = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto());

        result.Should().BeNull();
    }

    // Seeds the $100 board and the $5 delivery both coupon tests below price against.
    private static async Task SeedProductAndDeliveryAsync(DbContextOptions<StoreContext> options)
    {
        using var seed = new StoreContext(options);

        seed.Products.Add(new Product
        {
            Id = 1, Name = "Test Board", Description = "desc",
            Price = 100m, PictureUrl = "/img.png",
            Brand = "Test", Type = "Boards", QuantityInStock = 10
        });
        seed.DeliveryMethods.Add(new DeliveryMethod
        {
            Id = 1, ShortName = "UPS1", DeliveryTime = "1-2 Days",
            Price = 5m, Description = "Standard"
        });

        await seed.SaveChangesAsync();
    }

    private static ICouponService CouponsReturning(AppCoupon? coupon)
    {
        var mock = new Mock<ICouponService>();
        mock.Setup(x => x.GetCouponFromPromoCode(It.IsAny<string>())).ReturnsAsync(coupon);

        return mock.Object;
    }

    [Fact]
    public async Task CreateOrderAsync_UsesTheLookedUpAmountOff_NotTheCartsOwn()
    {
        var options = InMemoryOptions();
        await SeedProductAndDeliveryAsync(options);

        using var ctx = new StoreContext(options);

        // The cart claims $999 off; the live campaign is $20. (BUG-19)
        var forged = new AppCoupon
        {
            Id = "coup1", Name = "$20 Off", PromotionCode = "SAVE20", AmountOff = 999m
        };
        var live = new AppCoupon
        {
            Id = "coup1", Name = "$20 Off", PromotionCode = "SAVE20", AmountOff = 20m
        };

        var cartMock = new Mock<ICartService>();
        cartMock.Setup(x => x.GetCartAsync()).ReturnsAsync(MakeCart(coupon: forged));

        var service = new OrderService(new UnitOfWork(ctx), cartMock.Object, CouponsReturning(live));

        var order = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto());

        order.Should().NotBeNull();
        order!.Discount.Should().Be(20m);
        order.GetTotal().Should().Be(185m); // 200 - 20 + 5 shipping
    }

    [Fact]
    public async Task CreateOrderAsync_UsesTheLookedUpPercentOff_NotTheCartsOwn()
    {
        var options = InMemoryOptions();
        await SeedProductAndDeliveryAsync(options);

        using var ctx = new StoreContext(options);

        // The cart claims 90% off; the live campaign is 10%. (BUG-19)
        var forged = new AppCoupon
        {
            Id = "coup2", Name = "10% Off", PromotionCode = "SAVE10", PercentOff = 90m
        };
        var live = new AppCoupon
        {
            Id = "coup2", Name = "10% Off", PromotionCode = "SAVE10", PercentOff = 10m
        };

        var cartMock = new Mock<ICartService>();
        cartMock.Setup(x => x.GetCartAsync()).ReturnsAsync(MakeCart(coupon: forged));

        var service = new OrderService(new UnitOfWork(ctx), cartMock.Object, CouponsReturning(live));

        var order = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto());

        order.Should().NotBeNull();
        order!.Discount.Should().Be(20m); // 10% of subtotal 200
        order.GetTotal().Should().Be(185m);
    }

    [Fact]
    public async Task CreateOrderAsync_LooksTheCouponUpByThePromotionCode()
    {
        var options = InMemoryOptions();
        await SeedProductAndDeliveryAsync(options);

        using var ctx = new StoreContext(options);

        var coupon = new AppCoupon
        {
            Id = "coup1", Name = "$20 Off", PromotionCode = "SAVE20", AmountOff = 20m
        };

        var cartMock = new Mock<ICartService>();
        cartMock.Setup(x => x.GetCartAsync()).ReturnsAsync(MakeCart(coupon: coupon));

        var couponMock = new Mock<ICouponService>();
        couponMock.Setup(x => x.GetCouponFromPromoCode(It.IsAny<string>())).ReturnsAsync(coupon);

        var service = new OrderService(new UnitOfWork(ctx), cartMock.Object, couponMock.Object);

        await service.CreateOrderAsync("buyer@test.com", MakeOrderDto());

        // The typed code, never the id — that is the field test B forges.
        couponMock.Verify(x => x.GetCouponFromPromoCode("SAVE20"), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_ThrowsAndDropsTheCoupon_WhenItNoLongerResolves()
    {
        var options = InMemoryOptions();
        await SeedProductAndDeliveryAsync(options);

        using var ctx = new StoreContext(options);

        var coupon = new AppCoupon
        {
            Id = "coup1", Name = "$20 Off", PromotionCode = "SAVE20", AmountOff = 20m
        };
        var cart = MakeCart(coupon: coupon);

        var cartMock = new Mock<ICartService>();
        cartMock.Setup(x => x.GetCartAsync()).ReturnsAsync(cart);
        cartMock.Setup(x => x.SetCartAsync(It.IsAny<ShoppingCart>())).ReturnsAsync(cart);

        // The campaign was retired while the cart sat in Redis.
        var service = new OrderService(new UnitOfWork(ctx), cartMock.Object, CouponsReturning(null));

        var act = () => service.CreateOrderAsync("buyer@test.com", MakeOrderDto());

        await act.Should().ThrowAsync<PriceChangedException>();

        // Re-priced and saved, so the retry does not hit the same 409.
        cart.Coupon.Should().BeNull();
        cart.PaymentIntentId.Should().BeNull();
        cartMock.Verify(x => x.SetCartAsync(cart), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_WithSamePaymentIntentId_ShouldReturnExistingOrder()
    {
        // Task 18: idempotency — calling twice with same cart/PaymentIntentId returns
        // the first order without creating a duplicate or decrementing stock twice.
        var options = InMemoryOptions();

        using (var seed = new StoreContext(options))
        {
            seed.Products.Add(new Product
            {
                Id = 1, Name = "Test Board", Description = "desc",
                Price = 100m, PictureUrl = "/img.png",
                Brand = "Test", Type = "Boards", QuantityInStock = 10
            });
            seed.DeliveryMethods.Add(new DeliveryMethod
            {
                Id = 1, ShortName = "UPS1", DeliveryTime = "1-2 Days",
                Price = 5m, Description = "Standard"
            });
            await seed.SaveChangesAsync();
        }

        using var ctx = new StoreContext(options);
        var cartMock = new Mock<ICartService>();
        cartMock.Setup(x => x.GetCartAsync()).ReturnsAsync(MakeCart());

        var service = new OrderService(new UnitOfWork(ctx), cartMock.Object, NoCoupons());

        // First call — creates the order, decrements stock
        var first = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto());

        // Second call — same PaymentIntentId, should return the existing order
        var second = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto());

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        second!.Id.Should().Be(first!.Id); // same order returned, no duplicate

        // Stock should only have been decremented once (qty=2 from QuantityInStock=10)
        using var verify = new StoreContext(options);
        var product = await verify.Products.FindAsync(1);
        product!.QuantityInStock.Should().Be(8); // 10 - 2, not 10 - 4
    }
}

public class OrderService_ConcurrencyTests
{
    private static ICouponService NoCoupons() => Mock.Of<ICouponService>();

    private static DbContextOptions<StoreContext> InMemoryOptions()
        => new DbContextOptionsBuilder<StoreContext>()
            .UseInMemoryDatabase("ConcurrencyTest_" + Guid.NewGuid())
            .Options;

    private static ShoppingCart MakeCart(int productId, int deliveryMethodId, string paymentIntentId = "pi_test_123") => new()
    {
        PaymentIntentId = paymentIntentId,
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
        cart1.Setup(x => x.GetCartAsync()).ReturnsAsync(MakeCart(1, 1));

        var cart2 = new Mock<ICartService>();
        cart2.Setup(x => x.GetCartAsync()).ReturnsAsync(MakeCart(1, 1, "pi_test_456"));

        var service1 = new OrderService(new UnitOfWork(ctx1), cart1.Object, NoCoupons());
        var service2 = new OrderService(new UnitOfWork(ctx2), cart2.Object, NoCoupons());

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
