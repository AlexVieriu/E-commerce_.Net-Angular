namespace API.Filters;

[AttributeUsage(AttributeTargets.Method)]
public class CacheAttribute(int timeToLiveInMinutes) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // we can't inject services in attributes, so we get it from the HttpContext
        var cacheService = context.HttpContext.RequestServices.GetRequiredService<IResponseCacheService>();

        // if we have this implemented, when we have different order of query params, 
        // we will have different cache keys
        // var cacheKey2 = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;

        var cacheKey = GenerateCacheKeyFromRequest(context.HttpContext.Request);

        var cachedResponse = await cacheService.GetCachedResponseAsync(cacheKey);

        // if we have a cached response
        if (!string.IsNullOrEmpty(cachedResponse))
        {
            var contentResult = new ContentResult
            {
                Content = cachedResponse,
                ContentType = "application/json",
                StatusCode = 200
            };

            context.Result = contentResult;
            return; // return to the client; don't hit the controller and the database
        }

        // calls the GetProducts() action or other action that has cache attribute
        var executedContext = await next(); 

        if (executedContext.Result is OkObjectResult okObjectResult)
        {
            if (okObjectResult.Value != null) 
                await cacheService.CreateCacheResponseAsync(
                    cacheKey, okObjectResult.Value, TimeSpan.FromMinutes(timeToLiveInMinutes));
        }
    }

    // cache key ex: "/api/products|brandId-1|typeId-2"
    // Invariant culture: a cache key must be byte-identical regardless of the server's locale.
    // Path lowercased: routing is case-insensitive, so "/api/Products/72" and "/api/products/72"
    // are the same resource. Without this they'd cache under different keys and InvalidateCache
    // (a single-case Redis pattern, case-sensitive) would miss whichever case it wasn't written for.
    private static string GenerateCacheKeyFromRequest(HttpRequest request)
    {
        var keyBuilder = new StringBuilder();

        keyBuilder.Append(CultureInfo.InvariantCulture, $"{request.Path.ToString().ToLowerInvariant()}");

        foreach (var (key, value) in request.Query.OrderBy(x => x.Key))
        {
            keyBuilder.Append(CultureInfo.InvariantCulture, $"|{key}-{value}");
        }

        return keyBuilder.ToString();
    }
}
