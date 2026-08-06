namespace Application.Common.Interfaces;

// No method takes a cart key. The key is derived from the authenticated caller inside CartService,
// so there is no parameter for a client to forge (BUG-18).
public interface ICartService
{
    Task<ShoppingCart?> GetCartAsync();
    Task<ShoppingCart?> SetCartAsync(ShoppingCart cart);
    Task<bool> DeleteCartAsync();
}
