namespace Application.Common.Interfaces;

public interface ICouponService
{
    // Used in the CouponsController to validate the CouponCode
    public Task<AppCoupon?> GetCouponFromPromoCode(string code); 
}
