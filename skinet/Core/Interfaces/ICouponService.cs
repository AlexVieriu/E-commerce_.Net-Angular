namespace Core.Interfaces;

public interface ICouponService
{
    public Task<AppCoupon?> GetCouponFromPromoCode(string code); 
}
