using Core.Entities.OrderAggregate;

namespace Core.Tests.Entities;

public class Order_Tests
{
    // Testing Methods with Return Values
    [Fact]
    public void Order_GetTotal_ShouldCalculateCorrectly()
    {
        // Arrange
        var order = new Order
        {
            BuyerEmail = "test@test.com",
            PaymentIntentId = "pi_test",
            Subtotal = 100m,
            Discount = 10m,
            DeliveryMethod = new DeliveryMethod
            {
                ShortName = "UPS1",
                DeliveryTime = "1-2 Days",
                Price = 5m,
                Description = "Fast"
            }
        };

        // Act
        var total = order.GetTotal();

        // Assert
        total.Should().Be(95m);
    }

    [Theory]
    [InlineData(100, 0, 10, 110)]      // Subtotal 100, no discount, shipping 10 = 110
    [InlineData(100, 20, 5, 85)]       // Subtotal 100, discount 20, shipping 5 = 85
    [InlineData(50, 10, 0, 40)]        // Subtotal 50, discount 10, no shipping = 40
    [InlineData(200, 50, 15, 165)]     // Subtotal 200, discount 50, shipping 15 = 165
    public void Order_GetTotal_ShouldHandleDifferentScenarios(decimal subtotal,
                                                              decimal discount,
                                                              decimal shipping,
                                                              decimal expectedTotal)
    {
        // Arrange
        var order = new Order
        {
            BuyerEmail = "test@test.com",
            PaymentIntentId = "pi_test",
            Subtotal = subtotal,
            Discount = discount,
            DeliveryMethod = new DeliveryMethod
            {
                ShortName = "Test",
                DeliveryTime = "Test",
                Price = shipping,
                Description = "Test"
            }
        };

        // Act 
        var total = order.GetTotal();

        // Assert
        total.Should().Be(expectedTotal);
    }
}

// Testing Enums
public class OrderStatus_Tests
{
    [Fact]
    public void OrderStatus_ShouldHaveCorrectValues()
    {
        // Assert
        OrderStatus.Pending.Should().Be(OrderStatus.Pending);
        OrderStatus.PaymentReceived.Should().Be(OrderStatus.PaymentReceived);

        var statusValue = (int)OrderStatus.Pending;
        statusValue.Should().Be(0);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, "Pending")]
    [InlineData(OrderStatus.PaymentReceived, "PaymentReceived")]
    [InlineData(OrderStatus.Refunded, "Refunded")]
    public void OrderStatus_ToString_ShouldReturnCorrectString(
        OrderStatus status, string expected)
    {
        // Act
        var result = status.ToString();

        // Assert
        result.Should().Be(expected);
    }
}
