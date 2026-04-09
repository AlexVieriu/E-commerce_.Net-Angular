namespace API.Middleware;

public class ExceptionMiddleware(IHostEnvironment env, RequestDelegate next,
    ILogger<ExceptionMiddleware> logger)
{
    // We need to use this spelling "InvokeAsync" to work
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // this method is in the .net documentation
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, env);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception ex, IHostEnvironment env)
    {
        context.Response.ContentType = "application/json";

        ApiErrorResponse response;

        if (ex is PriceChangedException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            response = new ApiErrorResponse(context.Response.StatusCode, ex.Message, null);
        }
        else if (ex is InvalidOperationException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            // Safe to expose — we wrote this message intentionally
            response = new ApiErrorResponse(context.Response.StatusCode, ex.Message, null);
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            response = env.IsDevelopment()
                ? new ApiErrorResponse(context.Response.StatusCode, ex.Message, ex.StackTrace)
                : new ApiErrorResponse(context.Response.StatusCode, "An unexpected error occurred", null);
        }

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var json = JsonSerializer.Serialize(response, options);

        return context.Response.WriteAsync(json);
    }
}
