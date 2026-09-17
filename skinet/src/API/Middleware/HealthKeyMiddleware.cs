namespace API.Middleware;

// Guards /health/ready in front of the endpoint, so a caller without the key spends no database wake.
// Not Identity — that reads the user table, which is what may be down.
public class HealthKeyMiddleware(RequestDelegate next, IConfiguration config)
{
    private const string HeaderName = "X-Health-Key";
    private const string QueryName = "key";

    public async Task InvokeAsync(HttpContext context)
    {
        var configuredKey = config["HealthSettings:ReadyKey"];

        // No key configured means locked, not open.
        if (string.IsNullOrEmpty(configuredKey) || !Matches(context.Request, configuredKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    private static bool Matches(HttpRequest request, string configuredKey)
    {
        // Header for CI, query string for a phone. The query form lands in logs, so rotate the key.
        var suppliedKey = request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(suppliedKey))
            suppliedKey = request.Query[QueryName].ToString();

        // Fixed-time so timing can't leak the key one character at a time.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(suppliedKey), Encoding.UTF8.GetBytes(configuredKey));
    }
}
