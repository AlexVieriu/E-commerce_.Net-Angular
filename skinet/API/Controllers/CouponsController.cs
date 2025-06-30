namespace API.Controllers;

public class CouponsController(ICouponService couponService) : BaseApiController
{
    [HttpGet("{code}")]
    public async Task<ActionResult<AppCoupon>> ValidateCoupon(string code)
    {
        var coupon = await couponService.GetCouponFromPromoCode(code);

        return coupon != null ? coupon : BadRequest("Invalid voucher code");
    }
}

