namespace Infrastructure.Services;

public class PaymentService(IConfiguration config,
                            ICartService cartService,
                            IGenericRepository<Product> productRepo,
                            IGenericRepository<DeliveryMethod> dmRepo)
    : IPaymentService
{
    public async Task<ShoppingCart?> CreateOrUpdatePaymentIntent(string cartId)
    {
        StripeConfiguration.ApiKey = config["StripeSettings:SecretKey"];

        // get cart from Redis
        var cart = await cartService.GetCartAsync(cartId);

        if (cart == null) return null;

        var shippingPrice = 0m;

        if (cart.DeliveryMethodId.HasValue)
        {
            var deliveryMethod = await dmRepo.GetByIdAsync((int)cart.DeliveryMethodId);

            if (deliveryMethod == null) return null;

            shippingPrice = deliveryMethod.Price;
        }

        foreach (var item in cart.Items)
        {
            var productItem = await productRepo.GetByIdAsync(item.ProductId);

            if (productItem == null) return null;

            if (item.Price != productItem.Price)
            {
                item.Price = productItem.Price;
            }

            var serviceIntent = new PaymentIntentService();
            PaymentIntent? intent = null;

            if (string.IsNullOrEmpty(cart.PaymentIntentId))
            {
                var options = new PaymentIntentCreateOptions
                {
                    // stripe uses long type
                    Amount = (long)cart.Items.Sum(i => i.Quantity * (i.Price * 100)) +
                                                       (long)(shippingPrice * 100),
                    Currency = "usd",
                    PaymentMethodTypes = ["card"]
                };

                intent = await serviceIntent.CreateAsync(options);
                cart.PaymentIntentId = intent.Id;
                cart.ClientSecret = intent.ClientSecret;
            }
            else
            {
                var options = new PaymentIntentUpdateOptions
                {
                    Amount = (long)cart.Items.Sum(i => i.Quantity * (i.Price * 100)) +
                                                       (long)(shippingPrice * 100),
                };

                intent = await serviceIntent.UpdateAsync(cart.PaymentIntentId, options);
            }


        }
        await cartService.SetCartAsync(cart);

        return cart;
    }
}
