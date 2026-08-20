namespace API.Tests;

// Stands in for the Stripe lookup. Tests set the answer, then read back the code searched.
public class StubCouponService : ICouponService
{
    // Null = no live coupon for that code, i.e. a retired campaign.
    public AppCoupon? Coupon { get; set; }

    // Must be the code the customer typed, never a value off the cart. (BUG-19)
    public string? LastCode { get; private set; }

    public Task<AppCoupon?> GetCouponFromPromoCode(string code)
    {
        LastCode = code;

        return Task.FromResult(Coupon);
    }
}
