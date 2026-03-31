namespace API.Controllers;

[Authorize]
public class OrdersController(IUnitOfWork unitOfWork, IOrderService orderService) : BaseApiController
{
    [HttpGet()]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetOrdersForUser(CancellationToken token)
    {
        var spec = OrderSpecification.ByEmail(User.GetEmail());
        var orders = await unitOfWork.Repository<Order>().GetAllAsync(spec, token);
        var ordersToReturn = orders.Select(x => x.ToDto()).ToList();

        return Ok(ordersToReturn);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrderById(int id)
    {
        var spec = OrderSpecification.ByIdAndEmail(User.GetEmail(), id);
        var order = await unitOfWork.Repository<Order>().GetEntityWithSpec(spec);

        if (order == null)
            return NotFound();

        return Ok(order.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder(CreateOrderDto orderDto)
    {
        var email = User.GetEmail();
        var order = await orderService.CreateOrderAsync(email, orderDto);

        if (order == null)
            return BadRequest("Problem creating order");

        return Created($"/api/orders/{order.Id}", order.ToDto());
    }
}
