var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddControllers();
builder.Services.AddOpenApi();      // new with .net 9: https://aka.ms/aspnet/openapi

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

builder.Services.AddSingleton<IConnectionMultiplexer>(config =>
{
    var conStr = builder.Configuration.GetConnectionString("Redis") ??
            throw new Exception("Redis connection string not found");
    var conf = ConfigurationOptions.Parse(conStr, true);
    return ConnectionMultiplexer.Connect(conf);
});

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICouponService, CouponService>();

builder.Services.AddCors();
builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<AppUser>()
                .AddEntityFrameworkStores<StoreContext>();
builder.Services.AddSignalR();

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
// For SignalR
app.UseAuthentication();
app.UseAuthorization();

// For Angular app
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToController("Index", "Fallback"); // Idex(action name), Fallback(controller name)

app.MapControllers();

// https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0
app.MapGroup("api").MapIdentityApi<AppUser>();

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

    var baseContext = services.GetRequiredService<StoreContext>();
    await StoreContextSeed.SeedAsync(baseContext);
}
catch (Exception ex)
{
    WriteLine(ex.Message);
    throw;
}

app.Run();


