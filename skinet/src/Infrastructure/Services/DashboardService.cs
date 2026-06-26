namespace Infrastructure.Services;

public class DashboardService(IUnitOfWork unitOfWork) : IDashboardService
{
    // Products at or below this quantity show up as "low stock".
    private const int LowStockThreshold = 5;
    // Widget caps — the counts above stay exact, these lists are previews.
    private const int LowStockListSize = 10;
    private const int RecentOrdersSize = 5;

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        // Admin-only screen over a small dataset — load and aggregate in memory.
        // At larger scale, push COUNT/SUM into SQL (repo aggregate methods) instead.
        var products = await unitOfWork.Repository<Product>().GetAllAsync(cancellationToken);
        var orders = await unitOfWork.Repository<Order>()
            .GetAllAsync(OrderSpecification.AllForDashboard(), cancellationToken);

        var lowStock = products.Where(p => p.QuantityInStock <= LowStockThreshold).ToList();

        return new DashboardDto
        {
            TotalProducts = products.Count,
            TotalOrders = orders.Count,
            PendingOrders = orders.Count(o => o.Status == OrderStatus.Pending),
            // Revenue = captured payments only, net of discount, incl. shipping (GetTotal()).
            // Pending/Failed/Mismatch aren't paid; Refunded is money returned — all excluded.
            TotalRevenue = orders
                .Where(o => o.Status == OrderStatus.PaymentReceived)
                .Sum(o => o.GetTotal()),
            LowStockCount = lowStock.Count,
            LowStockProducts = lowStock
                .OrderBy(p => p.QuantityInStock)
                .Take(LowStockListSize)
                .Select(p => new LowStockProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    QuantityInStock = p.QuantityInStock
                })
                .ToList(),
            // Spec already orders by OrderDate desc, so Take gives the newest.
            RecentOrders = orders
                .Take(RecentOrdersSize)
                .Select(o => new RecentOrderDto
                {
                    Id = o.Id,
                    BuyerEmail = o.BuyerEmail,
                    OrderDate = o.OrderDate,
                    Total = o.GetTotal(),
                    Status = o.Status.ToString()
                })
                .ToList()
        };
    }
}
