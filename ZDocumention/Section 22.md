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


243. Creating an attribute on the API

-- API.RequestHelpers -> CacheAttribute.cs --
-> derived from Attribute, IAsyncActionFilter
-> override OnActionExecutionAsync()
    -> called asynchronously before the action, after model biding is complete
    -> get cache service from DI container
    -> generate the key
    -> if we have a cached response with a key, return it
    -> otherwise, call the next filter in the pipeline (goes to the controller where the cache attribute is applied)
    -> if there is OkObjectResult, cache the response(cacheKey, object(ex: products), timeToLive)


Dictionary:

-- CacheAttribute.cs --

AttributeTargets.All
Pros: 
-> very flexible - you can put your attributes anywhere
-> helpful when experimenting or not sure yet where it will be needed

Cons:
-> to permissive - attributes might be applied on places 
they don't make sense(caching property or method) 
-> can cause confusion to other developers (cache to class and property)
-> harder to enforce correct usage - you might introduce 
bugs if the attribute does nothing in some contexts


IAsyncActionFilter:
-> a filter that asynchronously surrounds execution of the action, 
after the model biding is complete

