241. Introduction
-> API performance(caching) 
-> Angular Lazy loading


242. Setting up caching on the API

-- Core.Interfaces -> IResponseCacheService.cs --
Task CacheResponseAsync(string cacheKey, object response, TimeSpan timeToLive);
Task<string?> GetCachedResponseAsync(string cacheKey);
Task RemoveCacheByPattern(string pattern);


-- Infrastructure.Services -> ResponseCacheService.cs --
CacheResponseAsync
    -> add json option for CamelCase
    -> serialize the response
    -> await to set the cache

GetCachedResponseAsync
    -> await to get the cache
    -> return the cached response

RemoveCacheByPattern


-- Program.cs --
builder.Services.AddSingleton<IResponseCacheService, ResponseCacheService>();

