namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController(IPaymentService payService, IGenericRepository<DeliveryMethod> dmRepo)
        : ControllerBase
{
    [Authorize]
    [HttpPost("{cartId}")]
    public async Task<ActionResult<ShoppingCart>> CreateOrUpdatePaymentIntent(string cartId)
    {
        var cart = await payService.CreateOrUpdatePaymentIntent(cartId);

        if (cart == null)
            return BadRequest("Problem with your cart");

        return Ok(cart);
    }

    [HttpGet("delivery-methods")]
    public async Task<ActionResult<IReadOnlyList<DeliveryMethod>>> GetDeliveryMethods()
    {
        return Ok(await dmRepo.GetAllAsync());
    }
}
