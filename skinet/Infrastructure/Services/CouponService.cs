
namespace Infrastructure.Services;

public class CouponService : ICouponService
{
    public CouponService(IConfiguration config)
    {
        StripeConfiguration.ApiKey = config["StripeSettings:SecretKey"];
    }

    public async Task<AppCoupon?> GetCouponFromPromoCode(string code)
    {
        // From here - Stripe code
        var promotionService = new Stripe.PromotionCodeService();
        var options = new Stripe.PromotionCodeListOptions { Code = code };

        var promotionCodes = await promotionService.ListAsync(options);

        var promotionCode = promotionCodes.FirstOrDefault();

        // To here - Stripe code

        if (promotionCode != null && promotionCode?.Coupon != null)
        {
            return new AppCoupon
            {
                Name = promotionCode.Coupon.Name,
                AmountOff = promotionCode.Coupon.AmountOff, // Stripe uses cents, so we divide it by 100
                PercentOff = promotionCode.Coupon.PercentOff,
                PromotionCode = promotionCode.Code,
                Id = promotionCode.Coupon.Id
            };
        }
        return null;
    }
}
