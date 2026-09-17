namespace API.HealthChecks;

// Status and timing only — a failed check's exception text can name the database server.
public static class HealthResponseWriter
{
    // Cached: a new JsonSerializerOptions per call discards the serializer's metadata cache.
    private static readonly JsonSerializerOptions ReportJsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            // Says how long a sleeping database took to resume, or that the check timed out.
            totalMs = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    ms = (int)entry.Value.Duration.TotalMilliseconds
                })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, ReportJsonOptions));
    }
}
