
namespace Infrastructure.Services;

public class ResponseCacheService(IConnectionMultiplexer redis) : IResponseCacheService
{
    private readonly IDatabase _redisDB = redis.GetDatabase(1); // 0 is for our cart

    public async Task CacheResponseAsync(string cacheKey, object response, TimeSpan timeToLive)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var serializedResponse = JsonSerializer.Serialize(response, options);

        await _redisDB.StringSetAsync(cacheKey, serializedResponse, timeToLive);
    }

    public async Task<string?> GetCachedResponseAsync(string cacheKey)
    {
        var cachedResponse = await _redisDB.StringGetAsync(cacheKey);

        return cachedResponse.IsNullOrEmpty ? string.Empty : cachedResponse;
    }

    public Task RemoveCacheByPattern(string pattern)
    {
        throw new NotImplementedException();
    }
}
