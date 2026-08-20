namespace Infrastructure.Tests.Services;

// The blank-code guard returns before Stripe is called — the only branch here a test can reach.
public class CouponService_Tests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCouponFromPromoCode_ShouldReturnNull_WhenCodeIsBlank(string? code)
    {
        var service = new CouponService();

        var coupon = await service.GetCouponFromPromoCode(code!);

        coupon.Should().BeNull();
    }
}
