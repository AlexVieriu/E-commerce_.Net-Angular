namespace Core.Entities;

// No Id: the cart is its Redis key, and CartService derives that from the caller.
// An id here would be one the client could send — that was BUG-18.
public class ShoppingCart
{
    public List<CartItem> Items { get; set; } = [];

    // properties for integration with Stripe
    public int? DeliveryMethodId { get; set; }
    public string? ClientSecret { get; set; }
    public string? PaymentIntentId { get; set; }
    public AppCoupon? Coupon { get; set; }
}