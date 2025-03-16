using System;

namespace Infrastructure.Services;

public class PaymentService : IPaymentService
{
    public Task<ShoppingCart> CreateOrUpdatePaymentIntent(string cartId)
    {
        throw new NotImplementedException();
    }
}
