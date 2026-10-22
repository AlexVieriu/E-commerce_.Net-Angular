namespace API.Controllers;

// `partial` so the [LoggerMessage] source generator can emit the bodies of the log methods below.
public partial class PaymentsController(
    IPaymentService payService,
    IUnitOfWork unitOfWork,
    ILogger<PaymentsController> logger,
    IOptions<StripeSettings> stripeOptions,
    IHubContext<NotificationHub> hubContext) : BaseApiController
{
    private readonly string _whSecret = stripeOptions.Value.WhSecret;

    // Source-generated logging (CA1848): the template is parsed once, and the args aren't boxed.
    // Static, so the logger is passed in; since .NET 9 an instance method could use the primary-constructor logger.
    [LoggerMessage(Level = LogLevel.Error, Message = "Stripe webhook error")]
    private static partial void LogStripeWebhookError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to handle stripe event")]
    private static partial void LogStripeEventHandlingFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to construct stripe event")]
    private static partial void LogStripeEventConstructionFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Order not found for PaymentIntent {PaymentIntentId}")]
    private static partial void LogOrderNotFound(ILogger logger, string paymentIntentId);

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ShoppingCart>> CreateOrUpdatePaymentIntent()
    {
        var cart = await payService.CreateOrUpdatePaymentIntent();

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
        // Stripe signs the raw body, so it is read as-is.
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
            // A non-2xx answer makes Stripe retry the event.
            LogStripeWebhookError(logger, ex);
            return StatusCode(StatusCodes.Status500InternalServerError, "Stripe webhook error");
        }
        catch (Exception ex)
        {
            // A non-2xx answer makes Stripe retry the event.
            LogStripeEventHandlingFailed(logger, ex);
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
            LogStripeEventConstructionFailed(logger, ex);
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
            LogOrderNotFound(logger, intent.Id);
            return;
        }

        await payService.ReconcileOrderPayment(order, intent.Amount);

        // Push the result to the buyer's open tab, if any.
        var connectionId = NotificationHub.GetConnectionId(order.BuyerEmail);

        if (!string.IsNullOrEmpty(connectionId))
            await hubContext.Clients.Client(connectionId)
                .SendAsync("OrderCompleteNotification", order.ToDto());
    }
}
