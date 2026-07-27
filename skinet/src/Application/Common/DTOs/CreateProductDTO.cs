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

    // Maps onto an entity the context is already TRACKING, so EF keeps the original values it loaded.
    // Handing EF a detached entity instead makes [ConcurrencyCheck] compare the new stock to itself (BUG-16).
    public void MapTo(Product product)
    {
        product.Name = Name;
        product.Description = Description;
        product.Price = Price;
        product.PictureUrl = PictureUrl;
        product.Type = Type;
        product.Brand = Brand;
        product.QuantityInStock = QuantityInStock;
    }
}
