namespace API.Tests.Integration;

// wwwroot is an ng build output, so it does not exist on CI — this factory serves a stub index.html instead.
public class SpaWebRootFactory : CustomWebApplicationFactory
{
    private readonly string _webRoot =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"skinet-webroot-{Guid.NewGuid():N}")).FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><app-root></app-root>");
        File.WriteAllText(Path.Combine(_webRoot, "main-ABC123.js"), "console.log('chunk');");
        builder.UseWebRoot(_webRoot);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        try
        {
            Directory.Delete(_webRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

// A cached index.html names the previous build's chunks, which a deploy deletes — so it must always revalidate.
public class IndexHtmlCachingTests(SpaWebRootFactory factory) : IClassFixture<SpaWebRootFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("")]               // UseDefaultFiles -> static files
    [InlineData("index.html")]     // static files, by name
    [InlineData("shop/42")]        // SPA deep link -> FallbackController
    public async Task IndexHtml_IsServedWithNoCache(string path)
    {
        var client = factory.CreateWebClient();

        var response = await client.GetAsync(path, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        body.Should().Contain("<app-root>");
        response.Headers.CacheControl?.NoCache.Should().BeTrue();
    }

    // Only index.html changes: other files keep their current headers until the rest of T59.
    [Fact]
    public async Task OtherStaticFiles_AreNotMarkedNoCache()
    {
        var client = factory.CreateWebClient();

        var response = await client.GetAsync("main-ABC123.js", Ct);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (response.Headers.CacheControl?.NoCache ?? false).Should().BeFalse();
    }
}
