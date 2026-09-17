namespace API.Middleware;

// Guards /health/ready in front of the endpoint, so a caller without the key spends no database wake.
// Not Identity — that reads the user table, which is what may be down.
public class HealthKeyMiddleware(RequestDelegate next, IConfiguration config)
{
    private const string HeaderName = "X-Health-Key";
    private const string QueryName = "key";

    public async Task InvokeAsync(HttpContext context)
    {
        var expected = config["HealthSettings:ReadyKey"];

        // No key configured means locked, not open.
        if (string.IsNullOrEmpty(expected) || !Matches(context.Request, expected))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    private static bool Matches(HttpRequest request, string expected)
    {
        // Header for CI, query string for a phone. The query form lands in logs, so rotate the key.
        var supplied = request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(supplied))
            supplied = request.Query[QueryName].ToString();

        // Fixed-time so timing can't leak the key one character at a time.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected));
    }
}
