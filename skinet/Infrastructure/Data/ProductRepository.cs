namespace Infrastructure.Data;

public class ProductRepository(StoreContext context) : IProductRepository
{
    public void AddProduct(Product product)
    {
        context.Products.Add(product);
    }

    public void DeleteProduct(Product product)
    {
        context.Products.Remove(product);
    }

    public async Task<IReadOnlyList<string>> GetBrandsAsync()
    {
        return await context.Products.Select(p => p.Brand).Distinct().ToListAsync();
    }

    public async Task<IReadOnlyList<Product>> GetProductAsync(
        string? brand = null, string? type = null, string? sort = null)
    {
        var query = context.Products
            .Where(p => (brand == null || p.Brand == brand) && (type == null || p.Type == type));

        query = sort switch
        {
            "priceAsc" => query.OrderBy(p => (double)p.Price),
            "priceDesc" => query.OrderByDescending(p => (double)p.Price),
            _ => query.OrderBy(p => p.Name)
        };

        return await query.ToListAsync();
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        return await context.Products.FindAsync(id);
    }

    public async Task<IReadOnlyList<string>> GetTypesAsync()
    {
        return await context.Products.Select(p => p.Type).Distinct().ToListAsync();
    }

    public bool ProductExists(int id)
    {
        return context.Products.Any(e => e.Id == id);
    }

    public async Task<bool> SaveChangesAsync()
    {
        return await context.SaveChangesAsync() > 0;
    }

    public void UpdateProduct(Product product)
    {
        context.Entry(product).State = EntityState.Modified;
    }
}
