namespace API.Controllers;

// No endpoint takes a cart id — CartService resolves the caller's own cart (BUG-18).
[Authorize]
public class CartController(ICartService cartService) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ShoppingCart>> GetCart()
    {
        var cart = await cartService.GetCartAsync();

        return Ok(cart ?? new ShoppingCart());
    }

    [HttpPost]
    public async Task<ActionResult<ShoppingCart>> UpdateCart(ShoppingCart cart)
    {
        var updatedCart = await cartService.SetCartAsync(cart);

        return updatedCart == null ?
            BadRequest("Problem updating the cart") : Ok(updatedCart);
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteCart()
    {
        await cartService.DeleteCartAsync();
        return NoContent();
    }
}
