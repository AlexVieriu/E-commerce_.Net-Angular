namespace Core.Tests.Entities;

// BEGINNER EXAMPLE 9: Testing with Multiple Assertions
public class CartItem_Tests
{
    [Fact]
    public void CartItem_ShouldCalculateLineTotal()
    {
        // Arrange
        var item = new CartItem
        {
            ProductId = 1,
            ProductName = "Test Product",
            Price = 50m,
            Quantity = 3,
            PictureUrl = "/test.png",
            Brand = "Test",
            Type = "Test"
        };

        // Act
        var lineTotal = item.Price * item.Quantity;

        // Assert - Multiple related assertions
        item.Price.Should().Be(50m);
        item.Quantity.Should().Be(3);
        lineTotal.Should().Be(150m);
    }

    [Theory]
    [InlineData(10, 2, 20)]
    [InlineData(25, 4, 100)]
    [InlineData(99.99, 1, 99.99)]
    public void CartItem_LineTotal_ShouldCalculateCorrectly(
        decimal price, int quantity, decimal expectedTotal)
    {
        // Arrange
        var item = new CartItem
        {
            ProductId = 1,
            ProductName = "Test Product",
            Price = price,
            Quantity = quantity,
            PictureUrl = "/test.png",
            Brand = "Test",
            Type = "Test"
        };

        // Act
        var lineTotal = item.Price * item.Quantity;

        // Assert
        lineTotal.Should().Be(expectedTotal);
    }
}
