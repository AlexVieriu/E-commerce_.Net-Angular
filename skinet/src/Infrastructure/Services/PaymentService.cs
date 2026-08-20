namespace Infrastructure.Services;

// PaymentService and its private helpers call Stripe SDK classes (PaymentIntentService,
// RefundService) directly via `new` — there is no DI seam to mock.
// Unit testing would require a full Stripe test-mode sandbox (real HTTP calls).
// Architectural fix: inject an IStripeClient abstraction
[ExcludeFromCodeCoverage]
public class PaymentService(ICartService cartService, IUnitOfWork unitOfWork,
    ICouponService couponService) : IPaymentService
{
    private readonly ICartService _cartService = cartService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICouponService _couponService = couponService;

    public async Task<ShoppingCart?> CreateOrUpdatePaymentIntent()
    {
        // get the caller's cart from Redis
        var cart = await _cartService.GetCartAsync() ?? throw new InvalidOperationException("Cart unavailable");

        var shippingPrice = await GetShippingPriceAsync(cart) ?? 0;

        await ValidateCartItemsInCartAsync(cart);

        var subtotal = CalculateSubtotal(cart);

        if (cart.Coupon != null)
            subtotal = await ApplyDiscountAsync(cart, subtotal);

        var total = subtotal + shippingPrice;

        await CreateUpdatePaymentIntentAsync(cart, total);

        await _cartService.SetCartAsync(cart);

        return cart;
    }

    public async Task<string> RefundPayment(string paymentIntentId)
    {
        var refundOptions = new RefundCreateOptions { PaymentIntent = paymentIntentId };
        var refundService = new RefundService();
        var refund = await refundService.CreateAsync(refundOptions);

        return refund.Status;
    }

    public async Task ReconcileOrderPayment(Order order, long paidAmountInCents)
    {
        var orderTotalInCents = (long)Math.Round(order.GetTotal() * 100, MidpointRounding.AwayFromZero);

        order.Status = orderTotalInCents != paidAmountInCents
            ? OrderStatus.PaymentMismatch
            : OrderStatus.PaymentReceived;

        await _unitOfWork.Complete();
    }

    public async Task SyncOrderPaymentStatus(Order order)
    {
        var intentService = new PaymentIntentService();
        var intent = await intentService.GetAsync(order.PaymentIntentId);

        await ReconcileOrderPayment(order, intent.Amount);
    }

    // Private helpers below, in the order CreateOrUpdatePaymentIntent calls them

    private async Task<long?> GetShippingPriceAsync(ShoppingCart cart)
    {
        if (cart.DeliveryMethodId.HasValue)
        {
            var deliveryMethod = await _unitOfWork.Repository<DeliveryMethod>()
                .GetByIdAsync((int)cart.DeliveryMethodId)
                ?? throw new InvalidOperationException("Problem getting delivery method");

            return (long)deliveryMethod.Price * 100;
        }
        return null;
    }

    private async Task ValidateCartItemsInCartAsync(ShoppingCart cart)
    {
        foreach (var item in cart.Items)
        {
            var productItem = await _unitOfWork.Repository<Product>()
                .GetByIdAsync(item.ProductId) ?? throw new InvalidOperationException("Problem getting product in cart");

            if (item.Price != productItem.Price)
                item.Price = productItem.Price;
        }
    }

    private static long CalculateSubtotal(ShoppingCart cart)
    {
        var itemTotal = cart.Items.Sum(i => i.Quantity * i.Price * 100);
        return (long)itemTotal;
    }

    // The charge follows the same rule as the order: re-read by promotion code, never by the
    // cart's id, which the client can forge. (BUG-19)
    private async Task<long> ApplyDiscountAsync(ShoppingCart cart, long amount)
    {
        var coupon = await _couponService.GetCouponFromPromoCode(cart.Coupon!.PromotionCode);

        if (coupon == null)
        {
            // Retired campaign. Drop it and re-price, so no intent is created at the old total.
            cart.Coupon = null;
            cart.PaymentIntentId = null;
            cart.ClientSecret = null;
            await _cartService.SetCartAsync(cart);

            throw new PriceChangedException(
                "Your coupon is no longer valid. Your cart has been updated.");
        }

        // The lookup returns dollars; this method works in cents.
        if (coupon.AmountOff.HasValue)
            amount -= (long)(coupon.AmountOff.Value * 100);
        if (coupon.PercentOff.HasValue)
            amount -= (long)(amount * (coupon.PercentOff.Value / 100));

        return Math.Max(amount, 0); // Ensure amount does not go negative
    }

    private static async Task CreateUpdatePaymentIntentAsync(ShoppingCart cart, long amount)
    {
        var serviceIntent = new PaymentIntentService();

        if (string.IsNullOrEmpty(cart.PaymentIntentId))
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = amount,
                Currency = "usd",
                PaymentMethodTypes = ["card"]
            };

            // Was prefixed with the cart id, which no longer exists. The GUID was already what made
            // this unique per call, so the key is unchanged in effect.
            var intent = await serviceIntent.CreateAsync(options, new RequestOptions { IdempotencyKey = Guid.NewGuid().ToString() });
            cart.PaymentIntentId = intent.Id;
            cart.ClientSecret = intent.ClientSecret;
        }
        else
        {
            var options = new PaymentIntentUpdateOptions
            {
                Amount = amount,
            };

            await serviceIntent.UpdateAsync(cart.PaymentIntentId, options);
        }
    }
}