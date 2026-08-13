using Microsoft.Data.Sqlite;

namespace API.Tests;

// Boots the real API in-process: real pipeline, real Redis, real cookie auth.
// Swapped: a throwaway SQLite file per run, and fake Stripe/admin settings so the host starts
// without a developer's user-secrets. Needs Redis on localhost:6379.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Meets the app's own password policy: 12+ chars, upper, digit, non-alphanumeric.
    private const string TestPassword = "Pa$$w0rd12!@";

    private readonly string _dbFile =
        Path.Combine(Path.GetTempPath(), $"skinet-tests-{Guid.NewGuid():N}.db");

    // AddApplicationServices reads StripeSettings:SecretKey EAGERLY, while Program.cs is still
    // running — before ConfigureAppConfiguration below has added a thing. Environment variables are
    // the only source already in place that early, and they outrank user secrets, which is also what
    // stops this harness from silently borrowing a developer's real Stripe keys.
    // Both values are constants, so setting them once per process is safe under parallel test classes.
    static CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("StripeSettings__SecretKey", "sk_test_fake");
        Environment.SetEnvironmentVariable("StripeSettings__WhSecret", "whsec_fake");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development selects the SQLite branch in AddApplicationServices and skips Key Vault.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Everything here is read lazily — the Redis multiplexer and the DbContext options
            // resolve on first use, and the admin password during seeding, all after the host is
            // built and this source is in place. The db path is per-instance, so unlike the Stripe
            // keys above it must never become an environment variable: parallel classes would
            // overwrite each other's database.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbFile}",
                ["ConnectionStrings:Redis"] = "localhost",
                ["AdminSettings:Password"] = TestPassword
            });
        });
    }

    // Identity's cookie is issued with SecurePolicy.Always, so a cookie container only replays it
    // over https. TestServer ignores the scheme, but the client still has to ask for one.
    public HttpClient CreateWebClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    // Registers a brand-new user and returns a client already holding their auth cookie.
    // Each caller gets an independent cookie jar, which is what lets one test act as two people.
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateWebClient();
        var email = $"bug18-{Guid.NewGuid():N}@test.local";

        var register = await client.PostAsJsonAsync("api/account/register", new RegisterDTO
        {
            FirstName = "Test",
            LastName = "User",
            Email = email,
            Password = TestPassword
        });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("api/login?useCookies=true", new
        {
            email,
            password = TestPassword
        });
        login.EnsureSuccessStatusCode();

        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing) return;

        // EF pools SQLite connections, so the file stays locked until the pool is cleared.
        SqliteConnection.ClearAllPools();

        // A leftover temp file must never fail a run — Windows can hold the handle a moment longer.
        try
        {
            if (File.Exists(_dbFile)) File.Delete(_dbFile);
        }
        catch (IOException)
        {
        }
    }
}
