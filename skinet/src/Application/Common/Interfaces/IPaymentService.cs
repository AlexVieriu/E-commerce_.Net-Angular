namespace Application.Common.Interfaces;

public interface IPaymentService
{
    Task<ShoppingCart?> CreateOrUpdatePaymentIntent(string cartId);
    Task<string> RefundPayment(string paymentIntentId);

    // Shared by the webhook (has the paid amount already) and the admin sync endpoint
    // (fetches it from Stripe first) — one rule for match -> PaymentReceived / mismatch -> PaymentMismatch.
    Task ReconcileOrderPayment(Order order, long paidAmountInCents);

    // Re-fetches the PaymentIntent from Stripe by order.PaymentIntentId, then reconciles.
    // Recovery path for when the webhook never reached the server.
    Task SyncOrderPaymentStatus(Order order);
}