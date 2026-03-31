namespace Application.Common.Interfaces;

public interface IResponseCacheService
{
    Task CreateCacheResponseAsync(string cacheKey, object response, TimeSpan timeToLive);
    Task<string?> GetCachedResponseAsync(string cacheKey);
    Task RemoveCacheByPattern(string pattern);
}
