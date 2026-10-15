namespace API.Tests.Integration;

// BUG-19 at the API layer: POST /api/cart stores the body verbatim, so a customer can forge
// their own coupon. The discount must come from the lookup, not the cart.
public class CouponTrustTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    // xUnit v3 wants every cancellable call to take the test's token (xUnit1051).
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // What Stripe would return for gimme5.
    private static AppCoupon LiveCoupon() => new()
    {
        Name = "GIMME5",
        PromotionCode = "gimme5",
        Id = "hFVUDrfn",
        AmountOff = 5m
    };

    // Anonymous, not ShoppingCart, so the coupon fields can hold anything.
    private static object CartWithCoupon(Product product, int deliveryMethodId, string paymentIntentId,
        decimal? amountOff, decimal? percentOff = null) => new
        {
            items = new[]
            {
                new
                {
                    productId = product.Id,
                    productName = product.Name,
                    price = product.Price,
                    quantity = 1,
                    pictureUrl = product.PictureUrl,
                    brand = product.Brand,
                    type = product.Type
                }
            },
            deliveryMethodId,
            paymentIntentId,
            clientSecret = "cs_test_fake",
            coupon = new
            {
                name = "GIMME5",
                amountOff,
                percentOff,
                promotionCode = "gimme5",
                id = "hFVUDrfn"
            }
        };

    private static CreateOrderDto OrderDtoFor(int deliveryMethodId) => new()
    {
        DeliveryMethodId = deliveryMethodId,
        ShippingAddress = new ShippingAddress
        {
            Name = "Jane Doe",
            Line1 = "1 Test St",
            City = "London",
            PostalCode = "EC1A 1BB",
            Country = "GB"
        },
        PaymentSummary = new PaymentSummary
        {
            Last4 = 4242,
            Brand = "visa",
            ExpMonth = 6,
            ExpYear = 2030
        }
    };

    // Checkout re-reads prices from the DB — a wrong one 409s before the discount is reached.
    private static async Task<(Product Product, DeliveryMethod Delivery)> SeedRefsAsync(HttpClient client)
    {
        var product = await client.GetFromJsonAsync<Product>("api/products/1", Ct);
        var deliveries = await client.GetFromJsonAsync<List<DeliveryMethod>>(
            "api/payments/delivery-methods", Ct);

        return (product!, deliveries!.First());
    }

    [Fact]
    public async Task CreateOrder_TakesTheDiscountFromTheLookup_NotTheForgedCartValue()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        factory.Coupons.Coupon = LiveCoupon();

        var (product, delivery) = await SeedRefsAsync(client);
        var intentId = $"pi_{Guid.NewGuid():N}";

        // Live campaign is $5; the cart claims $999.
        var forged = CartWithCoupon(product, delivery.Id, intentId, amountOff: 999m);
        (await client.PostAsJsonAsync("api/cart", forged, Ct)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("api/orders", OrderDtoFor(delivery.Id), Ct);
        response.EnsureSuccessStatusCode();

        var order = await response.Content.ReadFromJsonAsync<OrderDto>(Ct);

        order.Should().NotBeNull();
        order!.Discount.Should().Be(5m);
        order.Total.Should().Be(product.Price - 5m + delivery.Price);

        // The typed code is the only field the lookup may key on.
        factory.Coupons.LastCode.Should().Be("gimme5");
    }

    [Fact]
    public async Task CreateOrder_Returns409_AndDropsTheCoupon_WhenItNoLongerResolves()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        // Campaign retired after the cart was built.
        factory.Coupons.Coupon = null;

        var (product, delivery) = await SeedRefsAsync(client);
        var intentId = $"pi_{Guid.NewGuid():N}";

        var cart = CartWithCoupon(product, delivery.Id, intentId, amountOff: 5m);
        (await client.PostAsJsonAsync("api/cart", cart, Ct)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("api/orders", OrderDtoFor(delivery.Id), Ct);

        // Same 409 the client already handles for a price change.
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);

        // If the dead coupon survived, the retry would hit the same 409 forever.
        var reloaded = await client.GetFromJsonAsync<ShoppingCart>("api/cart", Ct);

        reloaded.Should().NotBeNull();
        reloaded!.Coupon.Should().BeNull();
        reloaded.PaymentIntentId.Should().BeNull();
    }
}
