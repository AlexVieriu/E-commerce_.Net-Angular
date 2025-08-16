namespace API.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(IUnitOfWork unit) : BaseApiController
{
    [HttpGet("orders")]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetOrders(
        [FromQuery] OrderSpecParams specParams)
    {
        var spec = new OrderSpecification(specParams);

        return await CreatePagedResult(
            unit.Repository<Order>(), spec, specParams.PageIndex, specParams.PageSize, x => x.ToDto());
    }

    [HttpGet("orders/{id}")]
    public async Task<ActionResult<OrderDto>> GetOrderById(int id)
    {
        var spec = new OrderSpecification(id);
        var order = await unit.Repository<Order>().GetEntityWithSpec(spec);

        return order != null ? Ok(order.ToDto()) : BadRequest("No order found with this id");
    }
}
