namespace Infrastructure.Services;

public class CartService(IConnectionMultiplexer redis) : ICartService
{
    // IDatabase is from Redis
    private readonly IDatabase _redisDB = redis.GetDatabase();

    public async Task<bool> DeleteCartAsync(string key)
        => await _redisDB.KeyDeleteAsync(key);

    public async Task<ShoppingCart?> GetCartAsync(string key)
    {
        var data = await _redisDB.StringGetAsync(key);

        return data.IsNullOrEmpty ? null : JsonSerializer.Deserialize<ShoppingCart>(data.ToString());
    }

    public async Task<ShoppingCart?> SetCartAsync(ShoppingCart cart)
    {
        var created = await _redisDB.StringSetAsync(
          cart.Id, JsonSerializer.Serialize(cart), TimeSpan.FromDays(30));

        return created ? await GetCartAsync(cart.Id) : null;
    }
}