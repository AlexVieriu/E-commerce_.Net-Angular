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

    [HttpGet("orders/{id}")]
    public async Task<ActionResult<OrderDto>> GetOrderById(int id)
    {
        var spec = OrderSpecification.ById(id);
        var order = await unit.Repository<Order>().GetEntityWithSpec(spec);

        return order != null ? Ok(order.ToDto()) : BadRequest("No order found with this id");
    }

    [HttpPost("orders/refund/{id:int}")]
    public async Task<ActionResult<OrderDto>> RefundOrder(int id)
    {
        var spec = OrderSpecification.ById(id);
        var order = await unit.Repository<Order>().GetEntityWithSpec(spec);

        if (order == null)
            return BadRequest("No order found with this id");

        if (order.Status == OrderStatus.Pending)
            return BadRequest("Pending orders can't be refunded");

        var refundStatus = await paymentService.RefundPayment(order.PaymentIntentId);
        if (refundStatus != "succeeded")
            return BadRequest("Refund failed");

        order.Status = OrderStatus.Refunded;

        // w8 for Db to update
        // the await ensures that for this specific request, the return statement
        // only happens after the DB operation is completed
        await unit.Complete();

        return order.ToDto();
    }

    [HttpPost("orders/{id:int}/sync-payment-status")]
    public async Task<ActionResult<OrderDto>> SyncPaymentStatus(int id)
    {
        var spec = OrderSpecification.ById(id);
        var order = await unit.Repository<Order>().GetEntityWithSpec(spec);

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
}
