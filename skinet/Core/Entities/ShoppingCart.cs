namespace Core.Entities;

public class ShoppingCart
{
    public required string Id { get; set; }
    public List<CartItem> Items { get; set; } = [];

    // properties for integration with Stripe
    public int? DeliveryMethodId { get; set; }
    public string? ClientSecret { get; set; }
    public string? PaymentIntentId { get; set; }
    public AppCoupon? Coupon { get; set; }
}