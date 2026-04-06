namespace Application.Common.Interfaces;

public interface IPaymentService
{
    Task<ShoppingCart?> CreateOrUpdatePaymentIntent(string cartId);
    Task<string> RefundPayment(string paymentIntentId);
}