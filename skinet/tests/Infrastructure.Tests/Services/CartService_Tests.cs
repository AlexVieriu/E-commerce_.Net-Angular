namespace Infrastructure.Tests.Services;

public class CartService_Tests
{
    // No test passes a cart key any more — CartService derives it from the caller (BUG-18),
    // so every Redis call in here must land on "cart:{TestUserId}".
    private const string TestUserId = "user-1";
    private const string TestUserKey = $"cart:{TestUserId}";

    private static Mock<ICurrentUser> MockCurrentUser(string userId = TestUserId)
    {
        var mock = new Mock<ICurrentUser>();
        mock.Setup(x => x.Id).Returns(userId);

        return mock;
    }

    // INTERMEDIATE EXAMPLE 13: Basic Mocking with Moq
    [Fact]
    public async Task GetCartAsync_ShouldReturnCart_WhenCartExists()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();

        var expectedCart = new ShoppingCart() { Id = string.Empty };
        var serializedCart = JsonSerializer.Serialize(expectedCart);

        // Setup must happen BEFORE new CartService() — constructor calls redis.GetDatabase()
        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
                 .Returns(mockDatabase.Object);

        mockDatabase.Setup(x => x.StringGetAsync(TestUserKey, It.IsAny<CommandFlags>()))
                    .ReturnsAsync(serializedCart);

        var cartService = new CartService(mockRedis.Object, MockCurrentUser().Object);

        // Act
        var result = await cartService.GetCartAsync();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCartAsync_ShouldReturnNull_WhenCartDoesNotExist()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();

        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
                 .Returns(mockDatabase.Object);

        mockDatabase.Setup(x => x.StringGetAsync(TestUserKey, It.IsAny<CommandFlags>()))
                    .ReturnsAsync(RedisValue.Null);

        var cartService = new CartService(mockRedis.Object, MockCurrentUser().Object);

        // Act
        var result = await cartService.GetCartAsync();

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

        // What Redis holds after the save: same cart, Id discarded.
        var storedCart = JsonSerializer.Serialize(new ShoppingCart { Id = string.Empty, Items = cart.Items });

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
                    .ReturnsAsync(storedCart);

        // Setup must happen BEFORE new CartService() — constructor calls redis.GetDatabase()
        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
                 .Returns(mockDatabase.Object);

        var cartService = new CartService(mockRedis.Object, MockCurrentUser().Object);

        // Act
        var result = await cartService.SetCartAsync(cart);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Count.Should().Be(1);

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

    // The BUG-18 regression test: a client can send any Id it likes and it changes nothing.
    [Fact]
    public async Task SetCartAsync_ShouldStoreUnderTheCallersKey_AndDiscardTheClientId()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();

        // A cart claiming to belong to somebody else.
        var cart = new ShoppingCart { Id = "cart:victim-42" };

        RedisKey savedKey = default;
        RedisValue savedValue = default;

        mockDatabase.As<IDatabaseAsync>()
                    .Setup(x => x.StringSetAsync(
                        It.IsAny<RedisKey>(),
                        It.IsAny<RedisValue>(),
                        It.IsAny<Expiration>(),
                        It.IsAny<ValueCondition>(),
                        It.IsAny<CommandFlags>()))
                    .Callback<RedisKey, RedisValue, Expiration, ValueCondition, CommandFlags>(
                        (key, value, _, _, _) => { savedKey = key; savedValue = value; })
                    .ReturnsAsync(true);

        mockDatabase.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                    .ReturnsAsync(RedisValue.Null);

        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
                 .Returns(mockDatabase.Object);

        var cartService = new CartService(mockRedis.Object, MockCurrentUser().Object);

        // Act
        await cartService.SetCartAsync(cart);

        // Assert: written under the caller's key, and the id the client sent is gone from the payload
        savedKey.ToString().Should().Be(TestUserKey);
        JsonSerializer.Deserialize<ShoppingCart>(savedValue.ToString())!.Id.Should().BeEmpty();
    }

    [Fact]
    public async Task SetCartAsync_ShouldReturnNull_WhenSaveFails()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();

        var shoppingCart = new ShoppingCart() { Id = string.Empty };

        // CartService calls the newer overload: StringSetAsync(RedisKey, RedisValue, Expiration, ValueCondition, CommandFlags)
        // Must use As<IDatabaseAsync>() because it is an explicit interface implementation
        mockDatabase.As<IDatabaseAsync>()
            .Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Setup must happen BEFORE new CartService() — constructor calls redis.GetDatabase()
        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(mockDatabase.Object);

        var cartService = new CartService(mockRedis.Object, MockCurrentUser().Object);

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
            .Setup(x => x.KeyDeleteAsync(TestUserKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var cartService = new CartService(mockRedis.Object, MockCurrentUser().Object);

        // Act
        var result = await cartService.DeleteCartAsync();

        // Assert
        result.Should().BeTrue();

        // Verify the method was called with correct key
        mockDatabase.Verify(
            x => x.KeyDeleteAsync(TestUserKey, It.IsAny<CommandFlags>()),
            Times.Once);
    }


    // https://claude.ai/chat/2165e008-bf1a-43be-b1ae-0387cf5c35f4
    // Was a Theory over client-supplied keys; now over callers, since the key follows the user.
    [Theory]
    [InlineData("user-1")]
    [InlineData("user-2")]
    [InlineData("6f9619ff-8b86-d011-b42d-00c04fc964ff")]
    public async Task DeleteCartAsync_ShouldAlwaysDeleteTheCallersKey(string userId)
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();

        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(mockDatabase.Object);

        mockDatabase
            .Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var cartService = new CartService(mockRedis.Object, MockCurrentUser(userId).Object);

        // Act
        await cartService.DeleteCartAsync();

        // Assert - Verify the exact key was passed
        mockDatabase.Verify(
            x => x.KeyDeleteAsync(
                It.Is<RedisKey>(key => key == $"cart:{userId}"),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }
}
