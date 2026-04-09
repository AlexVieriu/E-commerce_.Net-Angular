namespace Core.Tests.Entities;

public class ShoppingCart_Coupon_Tests
{
    [Fact]
    public void ShoppingCart_Coupon_ShouldBeNullByDefault()
    {
        var cart = new ShoppingCart { Id = "cart-1" };

        cart.Coupon.Should().BeNull();
    }

    [Fact]
    public void ShoppingCart_ShouldAttachAmountOffCoupon()
    {
        var cart = new ShoppingCart { Id = "cart-1" };
        var coupon = new AppCoupon
        {
            Id = "coup1",
            Name = "$10 Off",
            PromotionCode = "SAVE10",
            AmountOff = 10m,
            PercentOff = null
        };

        cart.Coupon = coupon;

        cart.Coupon.Should().NotBeNull();
        cart.Coupon!.AmountOff.Should().Be(10m);
        cart.Coupon.PercentOff.Should().BeNull();
        cart.Coupon.PromotionCode.Should().Be("SAVE10");
    }

    [Fact]
    public void ShoppingCart_ShouldAttachPercentOffCoupon()
    {
        var cart = new ShoppingCart { Id = "cart-1" };
        var coupon = new AppCoupon
        {
            Id = "coup2",
            Name = "25% Off",
            PromotionCode = "SUMMER25",
            AmountOff = null,
            PercentOff = 25m
        };

        cart.Coupon = coupon;

        cart.Coupon!.PercentOff.Should().Be(25m);
        cart.Coupon.AmountOff.Should().BeNull();
    }

    [Fact]
    public void ShoppingCart_ShouldRemoveCoupon_WhenSetToNull()
    {
        var cart = new ShoppingCart
        {
            Id = "cart-1",
            Coupon = new AppCoupon
            {
                Id = "c1", Name = "10% Off", PromotionCode = "SAVE10", PercentOff = 10m
            }
        };

        cart.Coupon = null;

        cart.Coupon.Should().BeNull();
    }

    [Fact]
    public void ShoppingCart_ShouldStoreCheckoutProperties()
    {
        var cart = new ShoppingCart
        {
            Id = "cart-1",
            DeliveryMethodId = 2,
            PaymentIntentId = "pi_abc123",
            ClientSecret = "secret_xyz"
        };

        cart.DeliveryMethodId.Should().Be(2);
        cart.PaymentIntentId.Should().Be("pi_abc123");
        cart.ClientSecret.Should().Be("secret_xyz");
    }

    [Fact]
    public void ShoppingCart_ShouldReplaceCoupon_WhenNewCouponApplied()
    {
        var cart = new ShoppingCart
        {
            Id = "cart-1",
            Coupon = new AppCoupon
            {
                Id = "c1", Name = "$5 Off", PromotionCode = "FIVE", AmountOff = 5m
            }
        };

        cart.Coupon = new AppCoupon
        {
            Id = "c2", Name = "$20 Off", PromotionCode = "TWENTY", AmountOff = 20m
        };

        cart.Coupon!.AmountOff.Should().Be(20m);
        cart.Coupon.PromotionCode.Should().Be("TWENTY");
    }
}
