namespace API.HealthChecks;

// Readiness only — one connection wakes the serverless database for a full hour.
// Base StoreContext: SQLite in Development and the tests, SqlServer in production.
public class SqlHealthCheck(StoreContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Against a paused database EF retries for minutes, so the registration timeout bounds this.
        var reachable = await db.Database.CanConnectAsync(cancellationToken);

        return reachable
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The database did not answer.");
    }
}
