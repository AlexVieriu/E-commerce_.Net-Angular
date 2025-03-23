var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Chaining of methods - static methods [Name of Method](this IServiceCollection services)
builder.Services.AddControllers();
builder.Services.AddOpenApi();      // new with .net 9: https://aka.ms/aspnet/openapi
builder.Services.AddDbContext<StoreContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddCors();
builder.Services.AddSingleton<IConnectionMultiplexer>(config =>
{
    var conStr = builder.Configuration.GetConnectionString("Redis") ??
            throw new Exception("Redis connection string not found");
    var conf = ConfigurationOptions.Parse(conStr, true);
    return ConnectionMultiplexer.Connect(conf);
});
builder.Services.AddSingleton<ICartService, CartService>();
builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<AppUser>()
                .AddEntityFrameworkStores<StoreContext>();
builder.Services.AddScoped<IPaymentService, PaymentService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // now u can add any UI for testing: swagger, scalar, etc
    app.MapOpenApi();

    // URL: http://localhost:5150/swagger/index.html
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Demo Api"));

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

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionMiddleware>();
app.UseCors(options => options.AllowAnyHeader()
                              .AllowAnyMethod()
                              .AllowCredentials()
                              .WithOrigins("http://localhost:4200", "https://localhost:4200"));
app.MapControllers();

// https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0
app.MapGroup("api").MapIdentityApi<AppUser>();

try // Database Migration
{
    // when we use services inside or outside DI, we need to create a Scoped
    // once this is executed the framework will dispose the scope    
    using var scoped = app.Services.CreateScope();
    var services = scoped.ServiceProvider;
    var context = services.GetRequiredService<StoreContext>();
    await context.Database.MigrateAsync();

    await StoreContentSeed.SeedAsync(context);
}
catch (Exception ex)
{
    WriteLine(ex.Message);
    throw;
}

app.Run();
