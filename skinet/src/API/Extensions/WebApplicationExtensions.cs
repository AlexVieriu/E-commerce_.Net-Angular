namespace API.Extensions;

public static class WebApplicationExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        try
        {
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
            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var adminPassword = app.Configuration["AdminSettings:Password"]
                ?? throw new InvalidOperationException("AdminSettings:Password is not configured.");

            await StoreContextSeed.SeedAsync(baseContext, userManager, adminPassword);
        }
        catch (Exception ex)
        {
            WriteLine(ex.Message);
            throw;
        }
    }
}
