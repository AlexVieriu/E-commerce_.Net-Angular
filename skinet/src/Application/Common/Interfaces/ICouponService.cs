namespace Application.Common.Interfaces;

public interface ICouponService
{
    // The only source of a coupon's amounts — the validate endpoint, the order and the charge all use it (BUG-19).
    public Task<AppCoupon?> GetCouponFromPromoCode(string code); 
}
