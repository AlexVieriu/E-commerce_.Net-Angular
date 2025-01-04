using Core.Interfaces;

namespace API.Controllers;

public class CartController(ICartService cartService) : BaseApiController
{

    [HttpGet("{id}")]
    public async Task<ActionResult<ShoppingCart>> GetCartById(string id)
    {
        var cart = await cartService.GetCartAsync(id);

        return Ok(cart ?? new ShoppingCart { Id = id });
        // id will be stored on the client side and when the client will update the cart
        // we will send the shopping cart with that id        
    }

    [HttpPost]
    public async Task<ActionResult<ShoppingCart>> UpdateCart(ShoppingCart cart)
    {
        var updatedCart = await cartService.SetCartAsync(cart);

        return updatedCart == null ?
            BadRequest("Problem updating the cart") : Ok(updatedCart);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteCart(string id)
    {
        return await cartService.DeleteCartAsync(id) ?
            NoContent() : BadRequest("Problem deleting the cart");
    }
}
