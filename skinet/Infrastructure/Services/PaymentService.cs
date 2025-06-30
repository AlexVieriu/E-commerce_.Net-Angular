namespace Infrastructure.Services;

public class PaymentService(IConfiguration config, ICartService cartService, IUnitOfWork unitOfWork)
    : IPaymentService
{
    public async Task<ShoppingCart?> CreateOrUpdatePaymentIntent(string cartId)
    {
        StripeConfiguration.ApiKey = config["StripeSettings:SecretKey"];

        // get cart from Redis
        var cart = await cartService.GetCartAsync(cartId) ?? throw new Exception("Cart unavailable");

        var shippingPrice = await GetShippingPriceAsync(cart) ?? 0;

        await ValidateCartItemsInCartAsync(cart);

        var subtotal = CalculateSubtotal(cart);

        if (cart.Coupon != null)
            subtotal = await ApplyDiscountAsync(cart.Coupon, subtotal);

        var total = subtotal + shippingPrice;

        await CreateUpdatePaymentIntentAsync(cart, total);

        await cartService.SetCartAsync(cart);

        return cart;
    }

    private async Task CreateUpdatePaymentIntentAsync(ShoppingCart cart, long amount)
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

            var intent = await serviceIntent.CreateAsync(options);
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

    private async Task<long> ApplyDiscountAsync(AppCoupon appCoupon, long amount)
    {
        var couponService = new Stripe.CouponService();

        var coupon = await couponService.GetAsync(appCoupon.Id);

        if (coupon.AmountOff.HasValue)
            amount -= (long)coupon.AmountOff * 100; // Stripe uses cents, so we multiply by 100
        if (coupon.PercentOff.HasValue)
            amount -= (long)(amount * (coupon.PercentOff.Value / 100));

        return Math.Max(amount, 0); // Ensure amount does not go negative
    }

    private long CalculateSubtotal(ShoppingCart cart)
    {
        var itemTotal = cart.Items.Sum(i => i.Quantity * (i.Price * 100));
        return (long)itemTotal;
    }

    private async Task<long?> GetShippingPriceAsync(ShoppingCart cart)
    {

        if (cart.DeliveryMethodId.HasValue)
        {
            var deliveryMethod = await unitOfWork.Repository<DeliveryMethod>()
                .GetByIdAsync((int)cart.DeliveryMethodId)
                ?? throw new Exception("Problem getting delivery method");

            return (long)deliveryMethod.Price * 100;
        }
        return null;
    }

    private async Task ValidateCartItemsInCartAsync(ShoppingCart cart)
    {
        foreach (var item in cart.Items)
        {
            var productItem = await unitOfWork.Repository<Product>()
                .GetByIdAsync(item.ProductId) ?? throw new Exception("Problem getting product in cart");

            if (item.Price != productItem.Price)
            {
                item.Price = productItem.Price;
            }
        }
    }
}

