namespace API.RequestHelpers;

[AttributeUsage(AttributeTargets.All)]
public class CacheAttribute(int timeToLiveInSeconds) : Attribute, IAsyncActionFilter
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
                    cacheKey, okObjectResult.Value, TimeSpan.FromMinutes(timeToLiveInSeconds));
        }
    }

    // cache key ex: "/api/products?brandId=1&typeId=2"
    private string GenerateCacheKeyFromRequest(HttpRequest request)
    {
        var keyBuilder = new StringBuilder();

        keyBuilder.Append($"{request.Path}");

        foreach (var (key, value) in request.Query.OrderBy(x => x.Key))
        {
            keyBuilder.Append($"|{key}-{value}");
        }

        return keyBuilder.ToString();
    }
}
