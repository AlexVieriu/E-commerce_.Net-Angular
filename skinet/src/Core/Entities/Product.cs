namespace Core.Entities;

public class Product : BaseEntity
{
    public decimal Price { get; set; }
    [ConcurrencyCheck]
    public int QuantityInStock { get; set; }
    public required string Brand { get; set; }
    public required string Description { get; set; }
    public required string Name { get; set; }
    public required string PictureUrl { get; set; }
    public required string Type { get; set; }
}