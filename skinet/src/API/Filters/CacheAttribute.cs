namespace API.Filters;

[AttributeUsage(AttributeTargets.Method)]
public class CacheAttribute(int timeToLiveInMinutes) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // An attribute gets no constructor injection, so the service comes from the request.
        var cacheService = context.HttpContext.RequestServices.GetRequiredService<IResponseCacheService>();

        var cacheKey = GenerateCacheKeyFromRequest(context.HttpContext.Request);

        var cachedResponse = await cacheService.GetCachedResponseAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedResponse))
        {
            var contentResult = new ContentResult
            {
                Content = cachedResponse,
                ContentType = "application/json",
                StatusCode = 200
            };

            context.Result = contentResult;
            return; // Cache hit: the action and the database are skipped.
        }

        var executedContext = await next();

        if (executedContext.Result is OkObjectResult okObjectResult)
        {
            if (okObjectResult.Value != null) 
                await cacheService.CreateCacheResponseAsync(
                    cacheKey, okObjectResult.Value, TimeSpan.FromMinutes(timeToLiveInMinutes));
        }
    }

    // Key e.g. "/api/products|brandId-1|typeId-2"; query keys are sorted, so their order doesn't matter.
    // Path lowercased: routing ignores case, but InvalidateCache's Redis pattern doesn't.
    // Invariant culture: the key must not depend on the server's locale.
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
