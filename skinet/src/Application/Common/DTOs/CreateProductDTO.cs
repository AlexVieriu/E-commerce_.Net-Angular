namespace Application.Common.DTOs;

// Validation lives in CreateProductDtoValidator (FluentValidation) — this stays a pure data contract.
public class CreateProductDTO
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string PictureUrl { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public int QuantityInStock { get; set; }

    public Product ToEntity() => new Product
    {
        Name = Name,
        Description = Description,
        Price = Price,
        PictureUrl = PictureUrl,
        Type = Type,
        Brand = Brand,
        QuantityInStock = QuantityInStock
    };

    public Product ToEntity(int id) => new Product
    {
        Id = id,
        Name = Name,
        Description = Description,
        Price = Price,
        PictureUrl = PictureUrl,
        Type = Type,
        Brand = Brand,
        QuantityInStock = QuantityInStock
    };
}
