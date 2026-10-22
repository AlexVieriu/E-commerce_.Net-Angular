namespace Infrastructure.Services;

// Creates PromotionCodeService with `new`, so there is nothing to mock (same as PaymentService).
[ExcludeFromCodeCoverage]
public class CouponService : ICouponService
{
    public async Task<AppCoupon?> GetCouponFromPromoCode(string code)
    {
        // A missing `code` filter matches everything, not nothing — Stripe would return every live code. (BUG-19)
        if (string.IsNullOrWhiteSpace(code)) return null;

        var promotionService = new PromotionCodeService();
        var options = new PromotionCodeListOptions
        {
            Code = code,
            // Only a live code may be redeemed; two codes can share the same string.
            Active = true,
            // Unexpanded, `promotion.coupon` is only an id and Coupon stays null; list calls need `data.` (BUG-20).
            Expand = ["data.promotion.coupon"]
        };

        var promotionCodes = await promotionService.ListAsync(options);

        var promotionCode = promotionCodes.FirstOrDefault();

        if (promotionCode != null && promotionCode?.Promotion.Coupon != null)
        {
            return new AppCoupon
            {
                Name = promotionCode.Promotion.Coupon.Name,
                // `100m`, not 100: AmountOff is a long, so integer division would drop the cents.
                AmountOff = promotionCode.Promotion.Coupon.AmountOff / 100m,
                PercentOff = promotionCode.Promotion.Coupon.PercentOff,
                PromotionCode = promotionCode.Code,
                Id = promotionCode.Promotion.Coupon.Id
            };
        }
        return null;
    }
}
