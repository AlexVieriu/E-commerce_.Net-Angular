namespace Application.Common.Interfaces;

public interface IOrderService
{
    Task<Order?> CreateOrderAsync(string email, CreateOrderDto orderDto);
}
