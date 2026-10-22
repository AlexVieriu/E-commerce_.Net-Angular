var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    var keyVaultUrl = builder.Configuration["KeyVaultUrl"];
    if (!string.IsNullOrEmpty(keyVaultUrl))
    {
        try
        {
            builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), new DefaultAzureCredential());
        }
        catch (Exception ex)
        {
            WriteLine($"Key Vault unavailable, falling back to App Service settings: {ex.Message}");
        }
    }
}

// Dev only: Azure terminates HTTPS in production.
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ConfigureEndpointDefaults(listenOptions => { });

        options.ListenLocalhost(7096, listenOptions =>
        {
            listenOptions.UseHttps();
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2AndHttp3;
        });

        options.ListenLocalhost(5150, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
        });
    });
}

builder.Services.AddApplicationServices(builder.Configuration, builder.Environment);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
        options
            .WithTitle("Demo Api")
            .WithTheme(ScalarTheme.Moon)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
    );
}

app.UseMiddleware<ExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors("CorsPolicy");

app.UseSecurityHeaders(policies => policies
    .AddDefaultSecurityHeaders()
    // The default COEP (credentialless) blocks Stripe's iframes, which send no COEP header.
    // We don't need cross-origin isolation, so COEP is off; the default COOP stays.
    .AddCrossOriginEmbedderPolicy(builder => builder.UnsafeNone())
    .AddContentSecurityPolicy(csp =>
    {
        csp.AddDefaultSrc().Self();
        csp.AddStyleSrc().Self().UnsafeInline();
        csp.AddScriptSrc().Self().UnsafeInline().From("https://js.stripe.com");
        csp.AddImgSrc().Self().Data().OverHttps();
        csp.AddFontSrc().Self().Data();
        csp.AddFrameSrc().From("https://js.stripe.com").From("https://hooks.stripe.com");
        csp.AddConnectSrc().Self().From("wss:").From("https://api.stripe.com").OverHttps();
    })
    .AddPermissionsPolicy(p => p.AddAccelerometer().None())
);

// Static files come before the rate limiter, so Angular chunks are never throttled.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    // index.html names this build's hashed chunks, so a cached copy outlives a deploy and 404s them.
    OnPrepareResponse = context =>
    {
        if (context.File.Name == "index.html") context.Context.Response.SetNoCache();
    }
});

app.UseRateLimiter();

// Cookie auth for the controllers and the SignalR hub.
app.UseAuthentication();
app.UseAuthorization();

// The key is checked in front of the endpoint, so an unauthorised caller spends no database wake.
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/health/ready"),
    branch => branch.UseMiddleware<HealthKeyMiddleware>());

// Liveness — Azure pings this every 60 seconds. No checks run, so it opens no connection.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false })
    .DisableRateLimiting();

// Readiness — on demand only: the deploy gate, CI, or you when the site looks broken.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteJsonAsync
}).DisableRateLimiting();

app.MapFallbackToController("Index", "Fallback");

app.MapControllers();

var identityGroup = app.MapGroup("api").MapIdentityApi<AppUser>();
if (!app.Environment.IsDevelopment())
    identityGroup.RequireRateLimiting("strict");

app.MapHub<NotificationHub>("/hub/notifications");

// `dotnet run -- --seed` seeds (Bogus demo data included) and exits without serving.
var runSeed = args.Contains("--seed");

await app.SeedDatabaseAsync(runSeed);

if (runSeed) return;

app.Run();
