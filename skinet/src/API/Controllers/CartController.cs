namespace API.Controllers;

// No endpoint takes a cart id. The cart is whichever one belongs to the authenticated caller,
// resolved inside CartService — so there is nothing for a client to forge (BUG-18).
[Authorize]
public class CartController(ICartService cartService) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ShoppingCart>> GetCart()
    {
        var cart = await cartService.GetCartAsync();

        return Ok(cart ?? new ShoppingCart { Id = string.Empty });
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
