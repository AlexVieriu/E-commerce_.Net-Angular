namespace Infrastructure.Services;

public class ResponseCacheService(IConnectionMultiplexer redis) : IResponseCacheService
{
    private readonly IDatabase _redisDB = redis.GetDatabase(1); // 0 is for our cart

    // Reused: a new JsonSerializerOptions per call rebuilds its metadata cache (CA1869).
    private static readonly JsonSerializerOptions CacheJsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task CreateCacheResponseAsync(string cacheKey, object response, TimeSpan timeToLive)
    {
        var serializedResponse = JsonSerializer.Serialize(response, CacheJsonOptions);

        await _redisDB.StringSetAsync(cacheKey, serializedResponse, timeToLive);
    }

    public async Task<string?> GetCachedResponseAsync(string cacheKey)
    {
        var cachedResponse = await _redisDB.StringGetAsync(cacheKey);

        return cachedResponse.IsNullOrEmpty ? string.Empty : cachedResponse;
    }

    public async Task RemoveCacheByPattern(string pattern)
    {
        var server = redis.GetServer(redis.GetEndPoints().First());
        var keys = server.Keys(database: _redisDB.Database, pattern: $"*{pattern}*").ToArray();

        if (keys.Length != 0)
            await _redisDB.KeyDeleteAsync(keys);
    }
}
