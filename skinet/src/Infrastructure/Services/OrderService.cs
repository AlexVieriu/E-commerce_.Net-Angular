namespace Infrastructure.Services;

public class OrderService(IUnitOfWork unitOfWork, ICartService cartService) : IOrderService
{
    public async Task<Order?> CreateOrderAsync(string email, CreateOrderDto orderDto)
    {
        var cart = await cartService.GetCartAsync(orderDto.CartId);
        if (cart == null) return null;
        if (cart.PaymentIntentId == null) return null;

        if (!cart.Items.Any())
            throw new InvalidOperationException("Cannot create order from an empty cart.");

        var items = new List<OrderItem>();

        foreach (var item in cart.Items)
        {
            if (item.Quantity <= 0)
                throw new InvalidOperationException(
                    $"Invalid quantity ({item.Quantity}) for product ID {item.ProductId}.");

            var productItem = await unitOfWork.Repository<Product>().GetByIdAsync(item.ProductId);
            if (productItem == null) return null;

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

        var subtotal = items.Sum(x => x.Price * x.Quantity);
        var discount = CalculateDiscount(cart.Coupon, subtotal);

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

        if (!await unitOfWork.Complete()) return null;

        // Cart is no longer needed — delete it from Redis.
        // Fire-and-forget: cart deletion failure doesn't invalidate an already-saved order.
        await cartService.DeleteCartAsync(orderDto.CartId);

        return order;
    }

    private static decimal CalculateDiscount(AppCoupon? coupon, decimal subtotal)
    {
        if (coupon == null) return 0;

        if (coupon.AmountOff.HasValue)
            return coupon.AmountOff.Value;

        if (coupon.PercentOff.HasValue)
            return subtotal * (coupon.PercentOff.Value / 100);

        return 0;
    }
}
