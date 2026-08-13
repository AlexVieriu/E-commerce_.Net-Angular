namespace Application.Common.Interfaces;

// No method takes a cart key — CartService derives it from the caller,
// so there is no parameter for a client to forge (BUG-18).
public interface ICartService
{
    Task<ShoppingCart?> GetCartAsync();
    Task<ShoppingCart?> SetCartAsync(ShoppingCart cart);
    Task<bool> DeleteCartAsync();
}
