namespace Application.Tests.Common.Mapping;

public class OrderMappingExtensions_Tests
{
    // - - - - - - - - - Order.ToDto - - - - - - - - -

    [Fact]
    public void Order_ToDto_ShouldMapAllScalarFields()
    {
        var order = MakeOrder();

        var dto = order.ToDto();

        dto.Id.Should().Be(order.Id);
        dto.BuyerEmail.Should().Be("buyer@test.com");
        dto.OrderDate.Should().Be(order.OrderDate);
        dto.Subtotal.Should().Be(100m);
        dto.Discount.Should().Be(10m);
        dto.PaymentIntentId.Should().Be("pi_test");
    }

    [Theory]
    [InlineData(OrderStatus.Pending, "Pending")]
    [InlineData(OrderStatus.PaymentReceived, "PaymentReceived")]
    [InlineData(OrderStatus.Refunded, "Refunded")]
    public void Order_ToDto_ShouldMapStatusAsString(OrderStatus status, string expected)
    {
        var order = MakeOrder(status: status);

        var dto = order.ToDto();

        dto.Status.Should().Be(expected);
    }

    [Fact]
    public void Order_ToDto_Total_ShouldEqualGetTotal()
    {
        var order = MakeOrder();

        var dto = order.ToDto();

        // Total must equal GetTotal(), not Subtotal — Discount and ShippingPrice are included
        dto.Total.Should().Be(order.GetTotal());
        dto.Total.Should().Be(95m); // 100 subtotal - 10 discount + 5 shipping
    }

    [Fact]
    public void Order_ToDto_ShouldMapDeliveryMethodFields()
    {
        var order = MakeOrder();

        var dto = order.ToDto();

        dto.DeliveryMethod.Should().Be("Standard delivery");
        dto.ShippingPrice.Should().Be(5m);
    }

    [Fact]
    public void Order_ToDto_ShouldMapShippingAddress()
    {
        var order = MakeOrder();

        var dto = order.ToDto();

        dto.ShippingAddress.Should().NotBeNull();
        dto.ShippingAddress.Name.Should().Be("John Doe");
        dto.ShippingAddress.Line1.Should().Be("123 Main St");
        dto.ShippingAddress.City.Should().Be("London");
    }

    [Fact]
    public void Order_ToDto_ShouldMapOrderItems()
    {
        var order = MakeOrder();

        var dto = order.ToDto();

        dto.OrderItems.Should().HaveCount(1);
        dto.OrderItems[0].ProductId.Should().Be(42);
        dto.OrderItems[0].ProductName.Should().Be("Test Board");
        dto.OrderItems[0].Quantity.Should().Be(2);
        dto.OrderItems[0].Price.Should().Be(50m);
    }

    [Fact]
    public void Order_ToDto_WithNoDiscount_TotalShouldBeSubtotalPlusShipping()
    {
        var order = MakeOrder(subtotal: 200m, discount: 0m, shippingPrice: 10m);

        var dto = order.ToDto();

        dto.Total.Should().Be(210m);
        dto.Discount.Should().Be(0m);
    }

    // - - - - - - - - - OrderItem.ToDto - - - - - - - - -

    [Fact]
    public void OrderItem_ToDto_ShouldMapAllFields()
    {
        var orderItem = new OrderItem
        {
            ItemOrdered = new ProductItemOrdered
            {
                ProductId = 7,
                ProductName = "Angular Hat",
                PictureUrl = "/images/hat.png"
            },
            Price = 25m,
            Quantity = 3
        };

        var dto = orderItem.ToDto();

        dto.ProductId.Should().Be(7);
        dto.ProductName.Should().Be("Angular Hat");
        dto.PictureUrl.Should().Be("/images/hat.png");
        dto.Price.Should().Be(25m);
        dto.Quantity.Should().Be(3);
    }

    // - - - - - - - - - Helpers - - - - - - - - -

    private static Order MakeOrder(
        OrderStatus status = OrderStatus.Pending,
        decimal subtotal = 100m,
        decimal discount = 10m,
        decimal shippingPrice = 5m)
    => new Order
    {
        Id = 1,
        BuyerEmail = "buyer@test.com",
        OrderDate = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc),
        PaymentIntentId = "pi_test",
        Status = status,
        Subtotal = subtotal,
        Discount = discount,
        ShippingAddress = new ShippingAddress
        {
            Name = "John Doe",
            Line1 = "123 Main St",
            City = "London",
            State = "England",
            PostalCode = "SW1A 1AA",
            Country = "GB"
        },
        DeliveryMethod = new DeliveryMethod
        {
            ShortName = "UPS1",
            Description = "Standard delivery",
            DeliveryTime = "3-5 Days",
            Price = shippingPrice
        },
        PaymentSummary = new PaymentSummary
        {
            Last4 = 4242,
            Brand = "visa",
            ExpMonth = 12,
            ExpYear = 2027
        },
        OrderItems =
        [
            new OrderItem
            {
                ItemOrdered = new ProductItemOrdered
                {
                    ProductId = 42,
                    ProductName = "Test Board",
                    PictureUrl = "/images/board.png"
                },
                Price = 50m,
                Quantity = 2
            }
        ]
    };
}
