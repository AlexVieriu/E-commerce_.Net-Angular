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

// Kestrel localhost config for dev only — Azure handles HTTPS termination in prod
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
    // Stripe Elements iframes (js.stripe.com, hooks.stripe.com) don't send CORP/COEP headers,
    // so COEP 'credentialless' from the defaults blocks them. We don't use SharedArrayBuffer /
    // cross-origin isolation, so turn COEP off. COOP same-origin from the defaults stays.
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

// Static files served before rate limiter so Angular chunks are never throttled
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();

// For SignalR
app.UseAuthentication();
app.UseAuthorization();

app.MapFallbackToController("Index", "Fallback");
app.MapControllers();
var identityGroup = app.MapGroup("api").MapIdentityApi<AppUser>();
if (!app.Environment.IsDevelopment())
    identityGroup.RequireRateLimiting("strict");
app.MapHub<NotificationHub>("/hub/notifications");

// `dotnet run -- --seed` is a one-shot: seed (incl. the Bogus demo dataset) then exit, don't serve.
var runSeed = args.Contains("--seed");

await app.SeedDatabaseAsync(runSeed);

if (runSeed) return;

app.Run();
