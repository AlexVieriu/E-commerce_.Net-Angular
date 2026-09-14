namespace Infrastructure.Services;

public class OrderService(IUnitOfWork unitOfWork, ICartService cartService,
    ICouponService couponService) : IOrderService
{
    public async Task<Order?> CreateOrderAsync(string email, CreateOrderDto orderDto)
    {
        var cart = await cartService.GetCartAsync();
        if (cart == null) return null;
        if (cart.PaymentIntentId == null) return null;

        var existingOrder = await unitOfWork.Repository<Order>()
            .GetEntityWithSpec(OrderSpecification.ByPaymentIntent(cart.PaymentIntentId));
        if (existingOrder != null) return existingOrder;

        if (cart.Items.Count == 0)
            throw new InvalidOperationException("Cannot create order from an empty cart.");

        var items = new List<OrderItem>();
        var priceChanges = new List<string>();

        foreach (var item in cart.Items)
        {
            if (item.Quantity <= 0)
                throw new InvalidOperationException(
                    $"Invalid quantity ({item.Quantity}) for product ID {item.ProductId}.");

            var productItem = await unitOfWork.Repository<Product>().GetByIdAsync(item.ProductId);
            if (productItem == null) return null;

            if (item.Price != productItem.Price)
            {
                priceChanges.Add($"{productItem.Name} (${item.Price:F2} → ${productItem.Price:F2})");
                item.Price = productItem.Price;
            }

            if (productItem.QuantityInStock < item.Quantity)
                throw new InvalidOperationException(
                    $"Insufficient stock for '{productItem.Name}'. Available: {productItem.QuantityInStock}, requested: {item.Quantity}.");

            productItem.QuantityInStock -= item.Quantity;
            unitOfWork.Repository<Product>().Update(productItem);

            var itemOrdered = new ProductItemOrdered
            {
                ProductId = productItem.Id,
                ProductName = productItem.Name,
                PictureUrl = productItem.PictureUrl
            };

            items.Add(new OrderItem
            {
                ItemOrdered = itemOrdered,
                Price = productItem.Price,
                Quantity = item.Quantity
            });
        }

        var deliveryMethod = await unitOfWork.Repository<DeliveryMethod>()
                                             .GetByIdAsync(orderDto.DeliveryMethodId);
        if (deliveryMethod == null) return null;

        if (cart.DeliveryMethodId.HasValue && cart.DeliveryMethodId != deliveryMethod.Id)
            priceChanges.Add($"Delivery method changed");

        if (priceChanges.Count > 0)
        {
            cart.PaymentIntentId = null;
            cart.ClientSecret = null;
            await cartService.SetCartAsync(cart);
            throw new PriceChangedException(
                $"Prices have changed: {string.Join(", ", priceChanges)}. Your cart has been updated.");
        }

        var subtotal = items.Sum(x => x.Price * x.Quantity);
        var discount = await ResolveDiscountAsync(cart, subtotal);

        var order = new Order
        {
            OrderItems = items,
            DeliveryMethod = deliveryMethod,
            ShippingAddress = orderDto.ShippingAddress,
            Subtotal = subtotal,
            PaymentSummary = orderDto.PaymentSummary,
            PaymentIntentId = cart.PaymentIntentId,
            BuyerEmail = email,
            Discount = discount
        };

        unitOfWork.Repository<Order>().Add(order);

        try
        {
            if (!await unitOfWork.Complete()) return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "Stock changed during checkout due to a concurrent order. Please try again.");
        }

        // The cart stays: a declined card leaves the intent payable, and the retry needs the cart.
        // The client deletes it once the payment succeeds. (BUG-2)
        return order;
    }

    // Only the promotion code is trusted — the amounts on the cart are client-supplied. (BUG-19)
    private async Task<decimal> ResolveDiscountAsync(ShoppingCart cart, decimal subtotal)
    {
        if (cart.Coupon == null) return 0;

        var coupon = await couponService.GetCouponFromPromoCode(cart.Coupon.PromotionCode);
        if (coupon != null) return CalculateDiscount(coupon, subtotal);

        // The campaign ended while the cart sat in Redis. Re-price without it, like a price change.
        cart.Coupon = null;
        cart.PaymentIntentId = null;
        cart.ClientSecret = null;
        await cartService.SetCartAsync(cart);

        throw new PriceChangedException(
            "Your coupon is no longer valid. Your cart has been updated.");
    }

    private static decimal CalculateDiscount(AppCoupon coupon, decimal subtotal)
    {
        if (coupon.AmountOff.HasValue)
            return coupon.AmountOff.Value;

        if (coupon.PercentOff.HasValue)
            return subtotal * (coupon.PercentOff.Value / 100);

        return 0;
    }
}
