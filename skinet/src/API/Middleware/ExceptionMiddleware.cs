namespace API.Middleware;

// `partial` so the [LoggerMessage] source generator can emit the body of LogUnhandledException.
public partial class ExceptionMiddleware(IHostEnvironment env, RequestDelegate next,
    ILogger<ExceptionMiddleware> logger)
{
    // Reused: a new JsonSerializerOptions per call rebuilds its metadata cache (CA1869).
    private static readonly JsonSerializerOptions ErrorJsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Source-generated logging (CA1848) — template parsed once, no boxing of the args.
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception ex, string method, string path);

    // Convention-based middleware: the method must be named Invoke or InvokeAsync.
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
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
        else if (ex is DbUpdateConcurrencyException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            // EF's own message quotes affected-row counts — write one the admin can act on.
            response = new ApiErrorResponse(context.Response.StatusCode,
                "This product was changed by someone else. Reload and try again.", null);
        }
        else if (ex is InvalidOperationException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            // Our services throw this with user-facing messages; a framework one would show its message too.
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
