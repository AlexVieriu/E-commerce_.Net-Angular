
namespace API.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(
    IUnitOfWork unit,
    IPaymentService paymentService,
    IDashboardService dashboardService,
    IHubContext<NotificationHub> hubContext) : BaseApiController
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> GetDashboard(CancellationToken cancellationToken)
        => Ok(await dashboardService.GetDashboardAsync(cancellationToken));

    [HttpGet("orders")]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetOrders(
        [FromQuery] OrderSpecParams specParams)
    {
        var spec = OrderSpecification.WithFilters(specParams);

        return await CreatePagedResult(
            unit.Repository<Order>(), spec, specParams.PageIndex, specParams.PageSize, x => x.ToDto());
    }

    [HttpGet("orders/{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrderById(int id)
    {
        var order = await GetOrderByIdAsync(id);

        if (order == null)
            return BadRequest("No order found with this id");

        return order.ToDto();
    }

    [HttpPost("orders/refund/{id:int}")]
    public async Task<ActionResult<OrderDto>> RefundOrder(int id)
    {
        var order = await GetOrderByIdAsync(id);

        if (order == null)
            return BadRequest("No order found with this id");

        if (order.Status == OrderStatus.Pending)
            return BadRequest("Pending orders can't be refunded");

        var refundStatus = await paymentService.RefundPayment(order.PaymentIntentId);
        if (refundStatus != "succeeded")
            return BadRequest("Refund failed");

        order.Status = OrderStatus.Refunded;
        await unit.Complete();

        return order.ToDto();
    }

    [HttpPost("orders/{id:int}/sync-payment-status")]
    public async Task<ActionResult<OrderDto>> SyncPaymentStatus(int id)
    {
        var order = await GetOrderByIdAsync(id);

        if (order == null)
            return BadRequest("No order found with this id");

        // Refunded is money-truth from the refund endpoint — never let a stale Stripe read overwrite it.
        if (order.Status == OrderStatus.Refunded)
            return BadRequest("Refunded orders can't be re-synced");

        await paymentService.SyncOrderPaymentStatus(order);

        var connectionId = NotificationHub.GetConnectionId(order.BuyerEmail);
        if (!string.IsNullOrEmpty(connectionId))
            await hubContext.Clients.Client(connectionId).SendAsync("OrderCompleteNotification", order.ToDto());

        return order.ToDto();
    }

    private async Task<Order?> GetOrderByIdAsync(int id)
        => await unit.Repository<Order>().GetEntityWithSpec(OrderSpecification.ById(id));
}
