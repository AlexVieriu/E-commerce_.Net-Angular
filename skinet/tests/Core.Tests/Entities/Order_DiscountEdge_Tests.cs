namespace Core.Tests.Entities;

public class Order_DiscountEdge_Tests
{
    [Fact]
    public void GetTotal_WhenNoDiscount_ShouldReturnSubtotalPlusShipping()
    {
        var order = MakeOrder(subtotal: 100m, discount: 0m, shipping: 10m);

        order.GetTotal().Should().Be(110m);
    }

    [Fact]
    public void GetTotal_WhenDiscountEqualsSubtotal_ShouldReturnShippingOnly()
    {
        // 100% coupon applied — customer pays only shipping
        var order = MakeOrder(subtotal: 100m, discount: 100m, shipping: 10m);

        order.GetTotal().Should().Be(10m);
    }

    [Fact]
    public void GetTotal_WhenFreeShipping_ShouldReturnSubtotalMinusDiscount()
    {
        var order = MakeOrder(subtotal: 150m, discount: 20m, shipping: 0m);

        order.GetTotal().Should().Be(130m);
    }

    [Fact]
    public void GetTotal_WhenDiscountExceedsSubtotal_TotalIsNegative()
    {
        // Documents current behaviour: no floor at zero.
        // If this becomes a bug in production, this test will catch the regression
        // when a floor is added (change Should().Be(-5m) to Should().Be(0m)).
        var order = MakeOrder(subtotal: 50m, discount: 60m, shipping: 5m);

        order.GetTotal().Should().Be(-5m);
    }

    [Fact]
    public void GetTotal_WhenEverythingIsZero_ShouldReturnZero()
    {
        var order = MakeOrder(subtotal: 0m, discount: 0m, shipping: 0m);

        order.GetTotal().Should().Be(0m);
    }

    [Theory]
    [InlineData(200, 25, 15, 190)]   // 25% off $200 = $50, + $15 shipping = $165... wait no, discount is flat $
    [InlineData(200, 50, 15, 165)]   // $50 off + $15 shipping
    [InlineData(99.99, 9.99, 5, 95)] // decimal rounding
    public void GetTotal_Theory_ShouldCalculateCorrectly(
        decimal subtotal, decimal discount, decimal shipping, decimal expected)
    {
        var order = MakeOrder(subtotal, discount, shipping);

        order.GetTotal().Should().Be(expected);
    }

    private static Order MakeOrder(decimal subtotal, decimal discount, decimal shipping)
    => new Order
    {
        BuyerEmail = "test@test.com",
        PaymentIntentId = "pi_test",
        Subtotal = subtotal,
        Discount = discount,
        DeliveryMethod = new DeliveryMethod
        {
            ShortName = "UPS1",
            DeliveryTime = "1-2 Days",
            Description = "Fast",
            Price = shipping
        }
    };
}
