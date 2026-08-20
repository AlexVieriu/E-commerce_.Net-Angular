namespace Infrastructure.Tests.Services;

// The blank-code guard returns before the first Stripe call, so it is the only branch in
// CouponService a unit test can reach — the rest has no DI seam (see the class comment).
public class CouponService_Tests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCouponFromPromoCode_ShouldReturnNull_WhenCodeIsBlank(string? code)
    {
        var service = new CouponService();

        // Left unguarded this drops Stripe's `Code` filter, and the list returns every live
        // promotion code for FirstOrDefault to pick one. (BUG-19)
        var coupon = await service.GetCouponFromPromoCode(code!);

        coupon.Should().BeNull();
    }
}
