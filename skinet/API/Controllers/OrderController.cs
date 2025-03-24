namespace API.Controllers;

[Authorize]
public class OrderController(ICartService cartService, IUnitOfWork unitOfWork) : BaseApiController
{
    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder(CreateOrderDto orderDto)
    {
        var email = User.GetEmail();
        var cart = await cartService.GetCartAsync(orderDto.CartId);

        if (cart == null)
            return BadRequest("Cart not found");

        if (cart.PaymentIntentId == null)
            return BadRequest("Payment Intent not found");

        var items = new List<OrderItem>();

        foreach (var item in cart.Items)
        {
            var productItem = await unitOfWork.Repository<Product>().GetByIdAsync(item.ProductId);
            if (productItem == null)
                return BadRequest("Product not found");

            var itemOrdered = new ProductItemOrdered
            {
                ProductId = productItem.Id,
                ProductName = productItem.Name,
                PictureUrl = productItem.PictureUrl
            };

            var OrderItem = new OrderItem
            {
                ItemOrdered = itemOrdered,
                Price = productItem.Price,
                Quantity = item.Quantity
            };
            items.Add(OrderItem);
        }
        var deliveryMethod = await unitOfWork.Repository<DeliveryMethod>()
                                             .GetByIdAsync(orderDto.DeliveryMethodId);

        if (deliveryMethod == null)
            return BadRequest("Not delivery method selected");

        var order = new Order
        {
            OrderItems = items,
            DeliveryMethod = deliveryMethod,
            ShippingAddress = orderDto.ShippingAddress,
            Subtotal = items.Sum(x => x.Price * x.Quantity),
            PaymentSummary = orderDto.PaymentSummary,
            PaymentIntentId = cart.PaymentIntentId,
            BuyerEmail = email
        };

        unitOfWork.Repository<Order>().Add(order);

        if (await unitOfWork.Complete())
            return Created();
        else
            return BadRequest("Problem creating order");
    }

    [HttpGet("/api/orders")]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetOrdersForUser()
    {
        var spec = new OrderSpecification(User.GetEmail());
        var orders = await unitOfWork.Repository<Order>().GetAllAsync(spec);
        var ordersToReturn = orders.Select(x => x.ToDto()).ToList();
        return Ok(ordersToReturn);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrderById(int id)
    {
        var spec = new OrderSpecification(User.GetEmail(), id);
        var order = await unitOfWork.Repository<Order>().GetEntityWithSpec(spec);

        if (order == null)
            return NotFound();

        return Ok(order.ToDto());
    }
}
