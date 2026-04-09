namespace Application.Tests.Specification;

public class ProductSpecification_SearchSort_Tests
{
    // - - - - - - - - - Search - - - - - - - - -

    [Fact]
    public void ProductSpecification_Search_ShouldMatchPartialName()
    {
        var products = GetTestProducts();
        var spec = new ProductSpecification(new ProductSpecParams { Search = "board" });

        var result = ApplySpec(spec, products);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(p => p.Name.ToLower().Contains("board"));
    }

    [Fact]
    public void ProductSpecification_Search_ShouldBeCaseInsensitive()
    {
        var products = GetTestProducts();
        var specLower = new ProductSpecification(new ProductSpecParams { Search = "angular" });
        var specUpper = new ProductSpecification(new ProductSpecParams { Search = "ANGULAR" });
        var specMixed = new ProductSpecification(new ProductSpecParams { Search = "AnGuLaR" });

        var lower = ApplySpec(specLower, products);
        var upper = ApplySpec(specUpper, products);
        var mixed = ApplySpec(specMixed, products);

        lower.Should().HaveCount(2);
        upper.Should().HaveCount(2);
        mixed.Should().HaveCount(2);
    }

    [Fact]
    public void ProductSpecification_Search_ShouldReturnEmpty_WhenNoMatch()
    {
        var products = GetTestProducts();
        var spec = new ProductSpecification(new ProductSpecParams { Search = "xyz_no_match" });

        var result = ApplySpec(spec, products);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ProductSpecification_Search_AndBrand_ShouldCombineWithAndLogic()
    {
        var products = GetTestProducts();
        var spec = new ProductSpecification(new ProductSpecParams
        {
            Search = "board",
            Brands = ["Angular"]
        });

        var result = ApplySpec(spec, products);

        // "Angular Speedster Board 2000" matches both; "React Super Board" has wrong brand
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Angular Speedster Board 2000");
    }

    // - - - - - - - - - Sort - - - - - - - - -

    [Fact]
    public void ProductSpecification_Sort_PriceAsc_ShouldReturnCheapestFirst()
    {
        var products = GetTestProducts();
        var spec = new ProductSpecification(new ProductSpecParams { Sort = "priceAsc" });

        var result = ApplyFullSpec(spec, products);

        result.Should().BeInAscendingOrder(p => p.Price);
    }

    [Fact]
    public void ProductSpecification_Sort_PriceDesc_ShouldReturnMostExpensiveFirst()
    {
        var products = GetTestProducts();
        var spec = new ProductSpecification(new ProductSpecParams { Sort = "priceDesc" });

        var result = ApplyFullSpec(spec, products);

        result.Should().BeInDescendingOrder(p => p.Price);
    }

    [Fact]
    public void ProductSpecification_Sort_Default_ShouldReturnAlphabeticalByName()
    {
        var products = GetTestProducts();
        var spec = new ProductSpecification(new ProductSpecParams()); // no sort

        var result = ApplyFullSpec(spec, products);

        result.Should().BeInAscendingOrder(p => p.Name);
    }

    // - - - - - - - - - ProductSpecParams parsing - - - - - - - - -

    [Fact]
    public void ProductSpecParams_Brands_ShouldSplitCommaSeparatedValues()
    {
        var specParams = new ProductSpecParams
        {
            Brands = ["Angular,React"]
        };

        specParams.Brands.Should().HaveCount(2);
        specParams.Brands.Should().Contain("Angular");
        specParams.Brands.Should().Contain("React");
    }

    [Fact]
    public void ProductSpecParams_Brands_ShouldHandleMixedListAndCommaValues()
    {
        var specParams = new ProductSpecParams
        {
            Brands = ["Angular,React", "NetCore"]
        };

        specParams.Brands.Should().HaveCount(3);
        specParams.Brands.Should().Contain("Angular");
        specParams.Brands.Should().Contain("React");
        specParams.Brands.Should().Contain("NetCore");
    }

    [Fact]
    public void ProductSpecParams_Search_ShouldLowercaseInput()
    {
        var specParams = new ProductSpecParams { Search = "ANGULAR BOARD" };

        specParams.Search.Should().Be("angular board");
    }

    // - - - - - - - - - Helpers - - - - - - - - -

    private static List<Product> GetTestProducts() =>
    [
        new() { Name = "Angular Speedster Board 2000", Description = "Fast board", Price = 200, PictureUrl = "/1.png", Type = "Boards", Brand = "Angular", QuantityInStock = 10 },
        new() { Name = "React Super Board",            Description = "Super board", Price = 250, PictureUrl = "/2.png", Type = "Boards", Brand = "React",   QuantityInStock = 5  },
        new() { Name = "Core Blue Hat",                Description = "Blue hat",   Price = 10,  PictureUrl = "/3.png", Type = "Hats",   Brand = "NetCore", QuantityInStock = 20 },
        new() { Name = "Angular Gloves",               Description = "Warm gloves",Price = 50,  PictureUrl = "/4.png", Type = "Gloves", Brand = "Angular", QuantityInStock = 15 },
    ];

    private static List<Product> ApplySpec(ProductSpecification spec, List<Product> products)
        => spec.ApplyCriteria(products.AsQueryable()).ToList();

    private static List<Product> ApplyFullSpec(ProductSpecification spec, List<Product> products)
    {
        var query = products.AsQueryable();

        if (spec.Criteria != null)
            query = query.Where(spec.Criteria);

        if (spec.OrderByDesc != null)
            query = query.OrderByDescending(spec.OrderByDesc);
        else if (spec.OrderByAsc != null)
            query = query.OrderBy(spec.OrderByAsc);

        if (spec.IsPagingEnabled)
            query = query.Skip(spec.Skip).Take(spec.Take);

        return query.ToList();
    }
}
