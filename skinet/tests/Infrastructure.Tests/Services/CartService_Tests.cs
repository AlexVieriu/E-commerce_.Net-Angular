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

        // Setup must happen BEFORE new CartService() — constructor calls redis.GetDatabase()
        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
                 .Returns(mockDatabase.Object);

        mockDatabase.Setup(x => x.StringGetAsync("test-cart", It.IsAny<CommandFlags>()))
                    .ReturnsAsync(serializedCart);

        var cartService = new CartService(mockRedis.Object);

        // Act
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

        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
                 .Returns(mockDatabase.Object);

        mockDatabase.Setup(x => x.StringGetAsync("nonexistent", It.IsAny<CommandFlags>()))
                    .ReturnsAsync(RedisValue.Null);

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

        // CartService calls StringSetAsync(key, value, TimeSpan) — TimeSpan implicitly converts to Expiration,
        // resolving to the newer IDatabaseAsync overload: StringSetAsync(RedisKey, RedisValue, Expiration, ValueCondition, CommandFlags)
        mockDatabase.As<IDatabaseAsync>()
                    .Setup(x => x.StringSetAsync(
                        It.IsAny<RedisKey>(),
                        It.IsAny<RedisValue>(),
                        It.IsAny<Expiration>(),
                        It.IsAny<ValueCondition>(),
                        It.IsAny<CommandFlags>()))
                    .ReturnsAsync(true);

        // Mock StringGetAsync for the GetCartAsync call inside SetCartAsync
        mockDatabase.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                    .ReturnsAsync(serializedCart);

        // Setup must happen BEFORE new CartService() — constructor calls redis.GetDatabase()
        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
                 .Returns(mockDatabase.Object);

        var cartService = new CartService(mockRedis.Object);

        // Act
        var result = await cartService.SetCartAsync(cart);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("test-cart");
        result.Items.Count.Should().Be(1);

        // Verify that StringSetAsync was called
        mockDatabase.As<IDatabaseAsync>()
                    .Verify(x => x.StringSetAsync(
                        It.IsAny<RedisKey>(),
                        It.IsAny<RedisValue>(),
                        It.IsAny<Expiration>(),
                        It.IsAny<ValueCondition>(),
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
        // StringSetAsync is an explicit IDatabaseAsync implementation — must use As<IDatabaseAsync>()
        mockDatabase.As<IDatabaseAsync>()
            .Setup(x => x.StringSetAsync(
                shoppingCart.Id,
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Setup must happen BEFORE new CartService() — constructor calls redis.GetDatabase()
        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(mockDatabase.Object);

        var cartService = new CartService(mockRedis.Object);

        // Act
        var result = await cartService.SetCartAsync(shoppingCart);

        // Assert: result is null — StringSet returned false so GetCartAsync was never called
        result.Should().BeNull();

        // Verify: StringGetAsync was NOT called (save failed, so GetCartAsync was never invoked)
        mockDatabase.Verify(
            x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }


    [Fact]
    public async Task DeleteCartAsync_ShouldReturnTrue_WhenCartExists()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();

        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(mockDatabase.Object);

        // Mock: Cart exists, delete returns true
        mockDatabase
            .Setup(x => x.KeyDeleteAsync("existing-cart", It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

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

        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(mockDatabase.Object);

        mockDatabase
            .Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

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
