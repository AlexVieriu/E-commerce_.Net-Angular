namespace Core.Tests.Entities;

// BEGINNER EXAMPLE 1: Testing Simple Entity Properties
public class Product_Tests
{
    // MethodName_Scenario_ExpectedBehavior
    [Fact]
    public void Product_SetProperties_ReturnCorrectProperties()
    {
        // Act & Assert
        var product = new Product
        {
            Name = "Test Board",
            Description = "Test Description",
            Price = 100m,
            PictureUrl = "/images/test.png",
            Type = "Boards",
            Brand = "TestBrand",
            QuantityInStock = 10
        };

        // Assert 
        product.Name.Should().Be("Test Board");
        product.Price.Should().Be(100m);
        product.QuantityInStock.Should().Be(10);
    }
}


// BEGINNER EXAMPLE 2: Testing Business Logic - Stock Availability
public class ProductStock_Tests
{
    [Theory]
    [InlineData(10, 5, true)]   // 10 in stock, ordering 5 -> Available
    [InlineData(10, 10, true)]  // 10 in stock, ordering 10 -> Available
    [InlineData(10, 11, false)] // 10 in stock, ordering 11 -> NOT Available
    [InlineData(0, 1, false)]   // 0 in stock, ordering 1 -> NOT Available
    public void Product_IsInStock_ShouldReturnCorrectAvailability(
        int quantityInStock, int requestedQuantity, bool expectedIsAvailable)
    {
        // Arrange
        var product = new Product
        {
            Name = "Test Product",
            Description = "Test",
            Price = 100m,
            PictureUrl = "/test.png",
            Type = "Boards",
            Brand = "Test",
            QuantityInStock = quantityInStock
        };

        // Act 
        var isAvailable = product.QuantityInStock >= requestedQuantity;

        // Assert
        isAvailable.Should().Be(expectedIsAvailable);
    }
}


// BEGINNER EXAMPLE 3: Testing Entity Inheritance 
public class BaseEntity_Tests
{
    [Fact]
    public void Product_ShouldInheritFromBaseEntity()
    {
        // Arrange
        var product = new Product
        {
            Name = "Test",
            Description = "Test",
            Price = 100,
            PictureUrl = "/test.png",
            Type = "Boards",
            Brand = "Test",
            QuantityInStock = 10
        };

        // Act & Assert
        product.Should().BeAssignableTo<BaseEntity>();
        product.Id.Should().Be(0);
    }
}

// BEGINNER EXAMPLE 10: Testing String Properties
public class ProductName_Tests
{
    [Theory]
    [InlineData("Angular Speedster Board 2000")]
    [InlineData("Core Blue Hat")]
    [InlineData("Redis Red Boots")]
    public void Product_Name_ShouldAcceptValidNames(string name)
    {
        // Arrange & Act
        var product = new Product
        {
            Name = name,
            Description = "Test",
            Price = 100,
            PictureUrl = "/test.png",
            Type = "Test",
            Brand = "Test",
            QuantityInStock = 10
        };

        // Assert
        product.Name.Should().Be(name);
        product.Name.Should().NotBeNullOrEmpty();
    }
}