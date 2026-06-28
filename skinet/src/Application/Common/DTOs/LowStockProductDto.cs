namespace Application.Common.DTOs;

public class LowStockProductDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int QuantityInStock { get; set; }
}
