namespace API.Controllers;

public class PaymentsController(
    IPaymentService payService,
    IUnitOfWork unitOfWork,
    ILogger<PaymentsController> logger,
    IOptions<StripeSettings> stripeOptions,
    IHubContext<NotificationHub> hubContext) : BaseApiController
{
    private readonly string _whSecret = stripeOptions.Value.WhSecret;

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
    public async Task<ActionResult<IReadOnlyList<DeliveryMethod>>> GetDeliveryMethods(CancellationToken token)
    {
        return Ok(await unitOfWork.Repository<DeliveryMethod>().GetAllAsync(token));
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> StripeWebhook()
    {
        // StreamReader to read the event
        var json = await new StreamReader(Request.Body).ReadToEndAsync();

        try
        {
            var stripeEvent = ConstructStripeEvent(json);

            if (stripeEvent.Type != EventTypes.PaymentIntentSucceeded)
                return Ok();

            if (stripeEvent.Data.Object is not PaymentIntent intent)
                return BadRequest("Invalid event data");

            await HandlePaymentIntentSucceeded(intent);

            return Ok();
        }
        catch (StripeException ex)
        {
            // This is sent back to Stripe
            logger.LogError(ex, "Stripe webhook error");
            return StatusCode(StatusCodes.Status500InternalServerError, "Stripe webhook error");
        }
        catch (Exception ex)
        {
            // This is sent back to Stripe
            logger.LogError(ex, "Failed to handle stripe event");
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to handle stripe event");
        }
    }

    private Event ConstructStripeEvent(string json)
    {
        try
        {
            return EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"],
                _whSecret, throwOnApiVersionMismatch: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to construct stripe event");
            throw new StripeException("Invalid signature");
        }
    }

    private async Task HandlePaymentIntentSucceeded(PaymentIntent intent)
    {
        if (intent.Status != "succeeded")
            return;

        var spec = OrderSpecification.ByPaymentIntent(intent.Id);

        var order = await unitOfWork.Repository<Order>().GetEntityWithSpec(spec);

        if (order == null)
        {
            logger.LogWarning("Order not found for PaymentIntent {PaymentIntentId}", intent.Id);
            return;
        }

        await payService.ReconcileOrderPayment(order, intent.Amount);

        // SignalR
        var connectionId = NotificationHub.GetConnectionId(order.BuyerEmail);

        if (!string.IsNullOrEmpty(connectionId))
            await hubContext.Clients.Client(connectionId)
                .SendAsync("OrderCompleteNotification", order.ToDto());
    }
}
