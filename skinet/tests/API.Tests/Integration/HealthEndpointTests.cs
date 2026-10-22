namespace API.Tests.Integration;

// The SPA catch-all answers any unknown path with 200 + index.html, so a 200 proves nothing.
// The body and content type are what fail if the route is renamed or deleted.
public class HealthEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    // xUnit v3 wants every cancellable call to take the test's token (xUnit1051).
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_AnswersWithHealthText_NotTheAngularPage()
    {
        var client = factory.CreateWebClient();

        var response = await client.GetAsync("health", Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
        body.Should().Be("Healthy");
    }

    [Fact]
    public async Task Ready_WithoutTheKey_Is401AndSaysNothing()
    {
        var client = factory.CreateWebClient();

        var response = await client.GetAsync("health/ready", Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task Ready_WithTheKeyInTheHeader_ReportsEveryDependency()
    {
        var client = factory.CreateWebClient();
        client.DefaultRequestHeaders.Add("X-Health-Key", CustomWebApplicationFactory.HealthKey);

        var response = await client.GetAsync("health/ready", Ct);
        var report = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct);
        var checks = report.GetProperty("checks");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        report.GetProperty("status").GetString().Should().Be("Healthy");
        checks.GetProperty("sql").GetProperty("status").GetString().Should().Be("Healthy");
        checks.GetProperty("redis").GetProperty("status").GetString().Should().Be("Healthy");
    }

    // The query form is the one you can type on a phone.
    [Fact]
    public async Task Ready_AcceptsTheKeyInTheQueryString()
    {
        var client = factory.CreateWebClient();

        var response = await client.GetAsync(
            $"health/ready?key={CustomWebApplicationFactory.HealthKey}", Ct);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_WithAWrongKey_Is401()
    {
        var client = factory.CreateWebClient();
        client.DefaultRequestHeaders.Add("X-Health-Key", "not-the-key");

        var response = await client.GetAsync("health/ready", Ct);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}
