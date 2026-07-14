namespace API.Filters;
// Named ...Attribute per convention; C# strips the suffix, so usage stays [InvalidateCache("...")].
[AttributeUsage(AttributeTargets.Method)]
public class InvalidateCacheAttribute(string pattern) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var resultContext = await next();

        if (resultContext.Exception == null || resultContext.ExceptionHandled)
        {
            var cacheService = context.HttpContext.RequestServices.GetRequiredService<IResponseCacheService>();
            await cacheService.RemoveCacheByPattern(pattern);
        }
    }
}
