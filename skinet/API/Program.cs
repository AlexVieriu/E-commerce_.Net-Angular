var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();      // new with .net 9: https://aka.ms/aspnet/openapi
builder.Services.AddDbContext<StoreContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IProductRepository, ProductRepository>();


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

app.MapControllers();

try
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
