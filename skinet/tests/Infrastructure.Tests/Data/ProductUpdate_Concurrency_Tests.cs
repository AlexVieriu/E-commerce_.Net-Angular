namespace Infrastructure.Tests.Data;

// Covers the write path ProductsController.UpdateProduct uses (BUG-16): load the TRACKED entity,
// map the DTO onto it, save. The concurrency guard only works when EF holds a real original value.
public class ProductUpdate_Concurrency_Tests
{
    private static DbContextOptions<StoreContext> InMemoryOptions()
        => new DbContextOptionsBuilder<StoreContext>()
            .UseInMemoryDatabase("ProductUpdate_" + Guid.NewGuid())
            .Options;

    private static Product MakeProduct(int stock) => new()
    {
        Id = 1, Name = "Board", Description = "desc", Price = 10m,
        PictureUrl = "/img.png", Brand = "Test", Type = "Boards", QuantityInStock = stock
    };

    private static CreateProductDTO MakeDto(int stock) => new()
    {
        Name = "Board", Description = "desc", Price = 10m,
        PictureUrl = "/img.png", Brand = "Test", Type = "Boards", QuantityInStock = stock
    };

    // The bug itself: changing stock used to put the NEW value in the concurrency WHERE clause,
    // so it matched 0 rows and threw even though nobody else had touched the row.
    [Fact]
    public async Task UpdateProduct_ChangingStockWithNoConflict_Saves()
    {
        var options = InMemoryOptions();
        using (var seed = new StoreContext(options))
        {
            seed.Products.Add(MakeProduct(10));
            await seed.SaveChangesAsync();
        }

        using (var context = new StoreContext(options))
        {
            var unitOfWork = new UnitOfWork(context);
            var product = await unitOfWork.Repository<Product>().GetByIdAsync(1);

            MakeDto(42).MapTo(product!);

            (await unitOfWork.Complete()).Should().BeTrue();
        }

        using var verify = new StoreContext(options);
        (await verify.Products.FindAsync(1))!.QuantityInStock.Should().Be(42);
    }

    // The guard still fires when the row really changes between the load and the save.
    // That window is tiny, so a second context changes the row in between.
    [Fact]
    public async Task UpdateProduct_StockChangedBetweenLoadAndSave_ThrowsConcurrencyException()
    {
        var options = InMemoryOptions();
        using (var seed = new StoreContext(options))
        {
            seed.Products.Add(MakeProduct(10));
            await seed.SaveChangesAsync();
        }

        using var context = new StoreContext(options);
        var unitOfWork = new UnitOfWork(context);

        // Load first — this is what captures the original value (stock 10).
        var product = await unitOfWork.Repository<Product>().GetByIdAsync(1);

        // Someone else sells the last units before we save.
        using (var other = new StoreContext(options))
        {
            var concurrent = await other.Products.FindAsync(1);
            concurrent!.QuantityInStock = 3;
            await other.SaveChangesAsync();
        }

        MakeDto(42).MapTo(product!);

        var act = async () => await unitOfWork.Complete();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
