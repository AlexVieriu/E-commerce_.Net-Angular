namespace Infrastructure.Services;

public class CartService(IConnectionMultiplexer redis, ICurrentUser currentUser) : ICartService
{
    // IDatabase is from Redis
    private readonly IDatabase _redisDB = redis.GetDatabase();

    // The one place a cart key is built. Prefixed to keep carts out of the Redis root keyspace.
    private string CartKey => $"cart:{currentUser.Id}";

    public async Task<bool> DeleteCartAsync()
        => await _redisDB.KeyDeleteAsync(CartKey);

    public async Task<ShoppingCart?> GetCartAsync()
    {
        var data = await _redisDB.StringGetAsync(CartKey);

        return data.IsNullOrEmpty ? null : JsonSerializer.Deserialize<ShoppingCart>(data.ToString());
    }

    public async Task<ShoppingCart?> SetCartAsync(ShoppingCart cart)
    {
        // An `id` in the request body is an unknown property and is dropped on binding.
        // CartKey is the cart's only identity.
        var created = await _redisDB.StringSetAsync(
          CartKey, JsonSerializer.Serialize(cart), TimeSpan.FromDays(30));

        return created ? await GetCartAsync() : null;
    }
}
