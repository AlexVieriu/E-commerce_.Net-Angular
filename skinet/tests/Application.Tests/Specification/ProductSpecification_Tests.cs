namespace Application.Tests.Specification;

// INTERMEDIATE EXAMPLE 15: Testing Specifications Pattern
public class ProductSpecification_Tests
{
    [Fact]
    public void ProductSpecification_ShouldFilterByMultipleBrands()
    {
        // Arrange
        var products = GetTestProducts();
        var specParams = new ProductSpecParams
        {
            Brands = new List<string> { "Angular", "React" }
        };
        var spec = new ProductSpecification(specParams);

        // Act
        var filtered = ApplySpec(spec, products);

        // Assert
        filtered.Should().HaveCount(3); // Angular Board + Angular Gloves + React Board
        filtered.Should().OnlyContain(p => p.Brand == "Angular" || p.Brand == "React");
    }

    [Fact]
    public void ProductSpecification_ShouldFilterByType()
    {
        // Arrange
        var products = GetTestProducts();
        var specParams = new ProductSpecParams
        {
            Types = new List<string> { "Boards" }
        };
        var spec = new ProductSpecification(specParams);

        // Act
        var filtered = ApplySpec(spec, products);

        // Assert
        filtered.Should().HaveCount(2); // Angular Board + React Board
        filtered.Should().OnlyContain(p => p.Type == "Boards");
    }

    [Fact]
    public void ProductSpecification_ShouldCombineFilters_WithAndLogic()
    {
        // Arrange
        var products = GetTestProducts();
        var specParams = new ProductSpecParams
        {
            Brands = new List<string> { "Angular" },
            Types = new List<string> { "Boards" }
        };
        var spec = new ProductSpecification(specParams);

        // Act
        var filtered = ApplySpec(spec, products);

        // Assert
        filtered.Should().HaveCount(1); // Only Angular Board matches both filters
        filtered[0].Name.Should().Be("Angular Speedster Board 2000");
        filtered[0].Brand.Should().Be("Angular");
        filtered[0].Type.Should().Be("Boards");
    }

    [Fact]
    public void ProductSpecification_ShouldReturnAll_WhenNoFiltersApplied()
    {
        // Arrange
        var products = GetTestProducts();
        var specParams = new ProductSpecParams
        {
            // No filters set
        };
        var spec = new ProductSpecification(specParams);

        // Act
        var filtered = ApplySpec(spec, products);

        // Assert
        filtered.Should().HaveCount(4); // All products
    }


    private List<Product> GetTestProducts()
    {
        return new List<Product>
        {
            new() {
                Name = "Angular Speedster Board 2000",
                Description = "Fast board",
                Price = 200,
                PictureUrl = "/test1.png",
                Type = "Boards",
                Brand = "Angular",
                QuantityInStock = 10
            },
            new() {
                Name = "React Super Board",
                Description = "Super board",
                Price = 250,
                PictureUrl = "/test2.png",
                Type = "Boards",
                Brand = "React",
                QuantityInStock = 5
            },
            new() {
                Name = "Core Blue Hat",
                Description = "Blue hat",
                Price = 10,
                PictureUrl = "/test3.png",
                Type = "Hats",
                Brand = "NetCore",
                QuantityInStock = 20
            }
        };
    }

    private List<Product> ApplySpec(ProductSpecification spec, List<Product> products)
    {
        // .ApplyCriteria method is the key bridge that allows you to test your specification
        // logic without a database
        var query = products.AsQueryable();
        return spec.ApplyCriteria(query).ToList();
    }
}
