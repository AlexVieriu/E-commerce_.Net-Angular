namespace Infrastructure.Tests.Services;

public class CartService_Tests
{
    // INTERMEDIATE EXAMPLE 13: Basic Mocking with Moq
    [Fact]
    public async Task GetCartAsync_ShouldReturnCart_WhenCartExists()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();

        var expectedCart = new ShoppingCart() { Id = "test-cart" };
        var serializedCart = JsonSerializer.Serialize(expectedCart);

        var cartService = new CartService(mockRedis.Object);

        // IDatabase GetDatabase(int db = -1, object? asyncState = null);
        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
                 .Returns(mockDatabase.Object);

        mockDatabase.Setup(x => x.StringGetAsync("test-cart", It.IsAny<CommandFlags>()))
                    .ReturnsAsync(serializedCart);

        // Act
        // Use hardcoded Id in simple testing
        // Use variable param in [Theory], with multiple tests
        var result = await cartService.GetCartAsync("test-cart");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("test-cart");
    }

    [Fact]
    public async Task GetCartAsync_ShouldReturnNull_WhenCartDoesNotExist()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();

        mockDatabase.Setup(x => x.StringGetAsync("nonexistent", It.IsAny<CommandFlags>()))
                    .ReturnsAsync(RedisValue.Null);

        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                 .Returns(mockDatabase.Object);

        var cartService = new CartService(mockRedis.Object);

        // Act
        var result = await cartService.GetCartAsync("nonexistent");

        // Assert        
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetCartAsync_ShouldSaveAndReturnCart_WhenSuccessful()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();

        var cart = new ShoppingCart
        {
            Id = "test-cart",
            Items = new List<CartItem>
            {
                new() {
                    ProductId = 1,
                    ProductName = "Test Product",
                    Price = 100m,
                    Quantity = 2,
                    PictureUrl = "/test.png",
                    Brand = "Test",
                    Type = "Test"
                }
            }
        };

        var serializedCart = JsonSerializer.Serialize(cart);

        // Mock test to return true
        // Task<bool> StringSetAsync(RedisKey key, RedisValue value, TimeSpan? expiry = null, bool keepTtl = false, When when = When.Always, CommandFlags flags = CommandFlags.None);
        mockDatabase.Setup(x => x.StringSetAsync(cart.Id,
                                                 It.IsAny<RedisValue>(),
                                                 It.IsAny<TimeSpan>(),
                                                 It.IsAny<When>(),
                                                 It.IsAny<CommandFlags>()))
                    .ReturnsAsync(true);

        // Mock StringGetAsync for the GetCartAsync call inside SetCartAsync
        // Task<RedisValue> StringGetAsync(RedisKey key, CommandFlags flags = CommandFlags.None);
        mockDatabase.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                    .ReturnsAsync(serializedCart);

        // IDatabase GetDatabase(int db = -1, object? asyncState = null);
        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
                 .Returns(mockDatabase.Object);

        var cartService = new CartService(mockRedis.Object);

        // Act               
        var result = await cartService.GetCartAsync("test-cart");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("test-cart");
        result.Items.Count.Should().Be(1);

        // Verify that StringSetAsync was called with correct parameters\
        mockDatabase.Verify(x => x.StringSetAsync(cart.Id,
                                                  It.IsAny<RedisValue>(),
                                                  TimeSpan.FromDays(30), // Verify expiration time
                                                  It.IsAny<When>(),
                                                  It.IsAny<CommandFlags>()),
                                 Times.Once);
    }

    [Fact]
    public async Task SetCartAsync_ShouldReturnNull_WhenSaveFails()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();

        var shoppingCart = new ShoppingCart() { Id = "nonexistent" };
        // Mock StringSetAsync to return false (failure)
        mockDatabase
            .Setup(x => x.StringSetAsync(
                shoppingCart.Id,
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        var cartService = new CartService(mockRedis.Object);

        // Act
        var result = await cartService.SetCartAsync(shoppingCart);

        //Assert
        result.Should().BeNull();

        // Verify 1: StringSetAsync WAS called (we attempted to save)
        mockDatabase.Verify(x => x.StringSetAsync(shoppingCart.Id,
                                  It.IsAny<RedisValue>(),
                                  TimeSpan.FromDays(30),
                                  It.IsAny<When>(),
                                  It.IsAny<CommandFlags>()),
                            Times.Once);

        // Verify 2: StringGetAsync was NOT called (because save failed, so GetCartAsync was never invoked)
        mockDatabase.Verify(
            x => x.StringGetAsync(It.IsAny<string>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }


    [Fact]
    public async Task DeleteCartAsync_ShouldReturnTrue_WhenCartExists()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();

        // Mock: Cart exists, delete returns true
        mockDatabase
            .Setup(x => x.KeyDeleteAsync("existing-cart", It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        var cartService = new CartService(mockRedis.Object);

        // Act
        var result = await cartService.DeleteCartAsync("existing-cart");

        // Assert
        result.Should().BeTrue();

        // Verify the method was called with correct key
        mockDatabase.Verify(
            x => x.KeyDeleteAsync("existing-cart", It.IsAny<CommandFlags>()),
            Times.Once);
    }


    // https://claude.ai/chat/2165e008-bf1a-43be-b1ae-0387cf5c35f4
    [Theory]
    [InlineData("cart-1")]
    [InlineData("cart-2")]
    [InlineData("")]
    public async Task DeleteCartAsync_ShouldPassCorrectKeyToRedis(string cartKey)
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();

        mockDatabase
            .Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        var cartService = new CartService(mockRedis.Object);

        // Act
        await cartService.DeleteCartAsync(cartKey);

        // Assert - Verify the exact key was passed
        mockDatabase.Verify(
            x => x.KeyDeleteAsync(
                It.Is<RedisKey>(key => key == cartKey),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }
}
