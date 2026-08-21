namespace Infrastructure.Services;

// CouponService calls Stripe SDK (PromotionCodeService) directly via `new` — no DI seam.
// See PaymentService for the full explanation.
[ExcludeFromCodeCoverage]
public class CouponService : ICouponService
{
    public async Task<AppCoupon?> GetCouponFromPromoCode(string code)
    {
        // A missing `code` filter matches everything, not nothing — Stripe would return every live code. (BUG-19)
        if (string.IsNullOrWhiteSpace(code)) return null;

        // From here - Stripe code
        var promotionService = new PromotionCodeService();
        var options = new PromotionCodeListOptions
        {
            Code = code,
            // Only a live code may be redeemed; two codes can share the same string.
            Active = true,
            // `promotion.coupon` is an expandable reference — unexpanded it arrives as a bare id string,
            // leaving the Coupon object null. List endpoints need the `data.` prefix. (BUG-20)
            Expand = ["data.promotion.coupon"]
        };

        var promotionCodes = await promotionService.ListAsync(options);

        var promotionCode = promotionCodes.FirstOrDefault();

        // To here - Stripe code

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
