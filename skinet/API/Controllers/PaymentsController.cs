namespace API.Controllers;

public class PaymentsController(IPaymentService payService,
                                IUnitOfWork unitOfWork,
                                ILogger<PaymentsController> logger,
                                IConfiguration config,
                                IHubContext<NotificationHub> hubContext) : BaseApiController
{
    private readonly string _whSecret = config["StripeSettings:WhSecret"]!;

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
        return Ok(await unitOfWork.Repository<DeliveryMethod>().GetAllAsync());
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> StripeWebhook()
    {
        // StreamReader to read the event
        var json = await new StreamReader(Request.Body).ReadToEndAsync();

        try
        {
            var stripeEvent = ConstructStripeEvent(json);

            if (stripeEvent.Data.Object is not PaymentIntent intent)
                return BadRequest("Invalid event date");

            await HandlePaymentIntentSucceeded(intent);

            return Ok();
        }
        catch (StripeException ex)
        {
            // This is send back to stripe
            logger.LogError(ex, "Stripe webhook error");
            return StatusCode(StatusCodes.Status500InternalServerError, "Stripe webhook error");
        }
        catch (Exception ex)
        {
            // This is send back to stripe
            logger.LogError(ex, "Failed to handle stripe event");
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to handle stripe event");
        }
    }

    private Event ConstructStripeEvent(string json)
    {
        try
        {
            return EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"],
            _whSecret);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to construct stripe event");
            throw new StripeException("Invalid signature");
        }
    }

    private async Task HandlePaymentIntentSucceeded(PaymentIntent intent)
    {
        if (intent.Status == "succeeded")
        {
            var spec = new OrderSpecification(intent.Id, true);

            var order = await unitOfWork.Repository<Order>().GetEntityWithSpec(spec)
                ?? throw new Exception("Order not found");


            var orderTotalInCents = (long)order.GetTotal();

            if (orderTotalInCents != intent.Amount)
                order.Status = OrderStatus.PaymentMismatch;

            else
                order.Status = OrderStatus.PaymentReceived;

            await unitOfWork.Complete();

            // SignalR
            var connectionId = NotificationHub.GetConnectionId(order.BuyerEmail);

            if (!string.IsNullOrEmpty(connectionId))
                await hubContext.Clients.Client(connectionId)
                    .SendAsync("OrderCompleteNotification", order.ToDto());
        }
    }
}
