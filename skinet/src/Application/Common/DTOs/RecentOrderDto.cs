namespace Application.Common.DTOs;

public class RecentOrderDto
{
    public int Id { get; set; }
    public required string BuyerEmail { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
    public required string Status { get; set; }
}
