namespace API.Middleware;

// `partial` so the [LoggerMessage] source generator can emit the body of LogUnhandledException.
public partial class ExceptionMiddleware(IHostEnvironment env, RequestDelegate next,
    ILogger<ExceptionMiddleware> logger)
{
    // Cached: a new JsonSerializerOptions per call discards the serializer's internal metadata cache.
    private static readonly JsonSerializerOptions ErrorJsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Source-generated logging (CA1848) — template parsed once, no boxing of the args.
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception ex, string method, string path);

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
            LogUnhandledException(logger, ex, context.Request.Method, context.Request.Path);

            response = env.IsDevelopment()
                ? new ApiErrorResponse(context.Response.StatusCode, ex.Message, ex.StackTrace)
                : new ApiErrorResponse(context.Response.StatusCode, "An unexpected error occurred", null);
        }

        var json = JsonSerializer.Serialize(response, ErrorJsonOptions);

        return context.Response.WriteAsync(json);
    }
}
