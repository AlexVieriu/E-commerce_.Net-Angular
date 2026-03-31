namespace Application.Common.DTOs;

public class CreateProductDTO
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    [Required]
    public string PictureUrl { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = string.Empty;

    [Required]
    public string Brand { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Quantity in stock must be at least 1")]
    public int QuantityInStock { get; set; }

    public Product ToEntity() => new Product
    {
        Name = Name, Description = Description, Price = Price,
        PictureUrl = PictureUrl, Type = Type, Brand = Brand,
        QuantityInStock = QuantityInStock
    };

    public Product ToEntity(int id) => new Product
    {
        Id = id, Name = Name, Description = Description, Price = Price,
        PictureUrl = PictureUrl, Type = Type, Brand = Brand,
        QuantityInStock = QuantityInStock
    };
}
