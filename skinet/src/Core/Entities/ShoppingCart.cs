namespace Core.Entities;

// No Id: the cart is identified by its Redis key, which CartService derives from the authenticated
// caller. Carrying an id here would mean the client could send one, which is exactly BUG-18.
public class ShoppingCart
{
    public List<CartItem> Items { get; set; } = [];

    // properties for integration with Stripe
    public int? DeliveryMethodId { get; set; }
    public string? ClientSecret { get; set; }
    public string? PaymentIntentId { get; set; }
    public AppCoupon? Coupon { get; set; }
}