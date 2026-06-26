namespace Application.Common.DTOs;

public class DashboardDto
{
    public int TotalProducts { get; set; }
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public int LowStockCount { get; set; }
    public List<LowStockProductDto> LowStockProducts { get; set; } = [];
    public List<RecentOrderDto> RecentOrders { get; set; } = [];
}
