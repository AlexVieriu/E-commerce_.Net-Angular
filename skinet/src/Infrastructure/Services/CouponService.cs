namespace Infrastructure.Services;

public class CouponService : ICouponService
{
    public async Task<AppCoupon?> GetCouponFromPromoCode(string code)
    {
        // From here - Stripe code
        var promotionService = new PromotionCodeService();
        var options = new PromotionCodeListOptions { Code = code };

        var promotionCodes = await promotionService.ListAsync(options);

        var promotionCode = promotionCodes.FirstOrDefault();

        // To here - Stripe code

        if (promotionCode != null && promotionCode?.Promotion.Coupon != null)
        {
            return new AppCoupon
            {
                Name = promotionCode.Promotion.Coupon.Name,
                AmountOff = promotionCode.Promotion.Coupon.AmountOff / 100,  // Stripe uses cents, so we divide it by 100
                PercentOff = promotionCode.Promotion.Coupon.PercentOff,
                PromotionCode = promotionCode.Code,
                Id = promotionCode.Promotion.Coupon.Id
            };
        }
        return null;
    }
}
