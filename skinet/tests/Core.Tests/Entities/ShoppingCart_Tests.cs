namespace Core.Tests.Entities;

public class ShoppingCart_Tests
{
    // BEGINNER EXAMPLE 6: Testing Collections
    [Fact]
    public void ShoppingCart_Items_ShouldInitializeAsEmptyList()
    {
        // Arrange & Act
        var cart = new ShoppingCart() { Id = "test-cart" };

        // Assert
        cart.Items.Should().NotBeNull();
        cart.Items.Should().BeEmpty();
    }

    [Fact]
    public void ShoppingCart_ShouldAddItems()
    {
        // Arrange
        var cart = new ShoppingCart { Id = "test-cart" };
        var item = new CartItem
        {
            ProductId = 1,
            ProductName = "Test Product",
            Price = 100,
            Quantity = 2,
            PictureUrl = "/test.png",
            Brand = "Test",
            Type = "Test"
        };

        // Act
        cart.Items.Add(item);

        // Assert
        cart.Items.Should().HaveCount(1);
        cart.Items[0].ProductId.Should().Be(1);
    }

    [Fact]
    public void ShoppingCart_ShouldUpdateQuantity_WhenSameProductAddedAgain()
    {
        // Arrange
        var cart = new ShoppingCart { Id = "test-cart" };
        cart.Items.Add(new CartItem
        {
            ProductId = 1,
            ProductName = "Test Product",
            Price = 100,
            Quantity = 2,
            PictureUrl = "/test.png",
            Brand = "Test",
            Type = "Test"
        });

        // Act        
        var existingItem = cart.Items.FirstOrDefault(x => x.ProductId == 1);
        existingItem?.Quantity += 3;

        // Assert
        cart.Items.Should().HaveCount(1);
        cart.Items[0].Quantity.Should().Be(5);

    }

    [Fact]
    public void ShoppingCart_ShouldRemoveItem()
    {
        // Arrange
        var cart = new ShoppingCart { Id = "test-cart" };
        var item = new CartItem
        {
            ProductId = 1,
            ProductName = "Test Product",
            Price = 100,
            Quantity = 2,
            PictureUrl = "/test.png",
            Brand = "Test",
            Type = "Test"
        };

        // Act
        cart.Items.Add(item);
        cart.Items.Remove(item);

        // Assert
        cart.Items.Should().BeEmpty();
    }
}
