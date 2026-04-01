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
            Console.WriteLine($"Key Vault unavailable, falling back to App Service settings: {ex.Message}");
        }
    }
}

// Chaining of methods - static methods [Name of Method](this IServiceCollection services)
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

builder.Services.AddControllers(options =>
{
    // In non-Development environments, remove BuggyController from routing entirely.
    // This means its endpoints don't exist in production — not just hidden, but never registered.
    if (!builder.Environment.IsDevelopment())
        options.Conventions.Add(new RemoveControllerConvention(typeof(BuggyController)));
});
builder.Services.AddOpenApi();      // new with .net 9: https://aka.ms/aspnet/openapi

builder.Services.AddSingleton<IConnectionMultiplexer>(config =>
{
    var conStr = builder.Configuration.GetConnectionString("Redis") ??
            throw new InvalidOperationException("Redis connection string not found");
    var conf = ConfigurationOptions.Parse(conStr, true);
    return ConnectionMultiplexer.Connect(conf);
});
builder.Services.AddSingleton<IResponseCacheService, ResponseCacheService>();

#region Database Configuration
if (builder.Environment.IsDevelopment())
{
    // Register the SQLite context
    builder.Services.AddDbContext<SqliteStoreContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Register the base StoreContext to resolve to SqliteStoreContext
    builder.Services.AddScoped<StoreContext>(provider =>
        provider.GetRequiredService<SqliteStoreContext>());
}
else
{
    // Register the SQL Server context
    builder.Services.AddDbContext<SqlServerStoreContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServerAzureConnection")));

    // Register the base StoreContext to resolve to SqlServerStoreContext
    builder.Services.AddScoped<StoreContext>(provider =>
        provider.GetRequiredService<SqlServerStoreContext>());
}
#endregion

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICouponService, CouponService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global fallback: 10 requests/min per IP for all endpoints
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Strict: 2 requests/min per IP — for auth and coupon endpoints
    options.AddPolicy("strict", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 2,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddCors();
builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<AppUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<StoreContext>();
builder.Services.AddSignalR();

StripeConfiguration.ApiKey = builder.Configuration["StripeSettings:SecretKey"]
    ?? throw new InvalidOperationException("StripeSettings:SecretKey is not configured.");



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // now u can add any UI for testing: swagger, scalar, etc
    app.MapOpenApi();

    // app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Demo Api"));

    // URL: http://localhost:5150/scalar/v1
    app.MapScalarApiReference(options =>
        {
            options
            .WithTitle("Demo Api")
            .WithTheme(ScalarTheme.Moon)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        }
    );
}

app.UseMiddleware<ExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors(options => options.AllowAnyHeader()
                              .AllowAnyMethod()
                              .AllowCredentials()
                              .WithOrigins("http://localhost:4200", "https://localhost:4200", "https://skinet-alex89.azurewebsites.net"));
app.UseRateLimiter();

// For SignalR
app.UseAuthentication();
app.UseAuthorization();

// For Angular app
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToController("Index", "Fallback"); // Idex(action name), Fallback(controller name)

app.MapControllers();

// https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0
app.MapGroup("api").MapIdentityApi<AppUser>().RequireRateLimiting("strict");

app.MapHub<NotificationHub>("/hub/notifications");

try
{
    using var scoped = app.Services.CreateScope();
    var services = scoped.ServiceProvider;

    if (app.Environment.IsDevelopment())
    {
        var context = services.GetRequiredService<SqliteStoreContext>();
        await context.Database.MigrateAsync();
    }
    else
    {
        var context = services.GetRequiredService<SqlServerStoreContext>();
        await context.Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        var baseContext = services.GetRequiredService<StoreContext>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        await StoreContextSeed.SeedAsync(baseContext, userManager);
    }
}
catch (Exception ex)
{
    WriteLine(ex.Message);
    throw;
}

app.Run();