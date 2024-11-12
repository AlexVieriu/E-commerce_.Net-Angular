global using Core.Entities;

namespace Core.Interfaces;

public interface IProductRepository
{
    bool ProductExists(int id);
    Task<bool> SaveChangesAsync();
    Task<IReadOnlyList<string>> GetBrandsAsync();
    Task<IReadOnlyList<string>> GetTypesAsync();
    Task<IReadOnlyList<Product>> GetProductAsync();
    Task<Product?> GetProductByIdAsync(int id);
    void AddProduct(Product product);
    void DeleteProduct(Product product);
    void UpdateProduct(Product product);
}
