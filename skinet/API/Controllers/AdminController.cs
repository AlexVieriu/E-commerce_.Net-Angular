namespace API.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(IUnitOfWork unit) : BaseApiController
{
    [HttpGet("orders")]
    public async Task<ActionResult<Order>> GetOrders([FromQuery] OrderSpecParams specParams)
    {
        var spec = new OrderSpecification(specParams);

        return await CreatePagedResult(
            unit.Repository<Order>(), spec, specParams.PageIndex, specParams.PageSize);
    }
}
