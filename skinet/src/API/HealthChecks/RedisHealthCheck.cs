namespace API.HealthChecks;

// abortConnect=False hands back a connection even when Upstash is down, so only a PING proves it.
public class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // PingAsync takes no token, so this is where the registration timeout lands.
        cancellationToken.ThrowIfCancellationRequested();

        _ = await redis.GetDatabase().PingAsync();

        return HealthCheckResult.Healthy();
    }
}
