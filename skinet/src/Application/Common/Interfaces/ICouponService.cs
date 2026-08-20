namespace Application.Common.Interfaces;

public interface ICouponService
{
    // The only way to a coupon's values: the validate endpoint, the order and the charge. (BUG-19)
    public Task<AppCoupon?> GetCouponFromPromoCode(string code); 
}
