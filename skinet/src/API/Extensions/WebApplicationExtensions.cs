namespace API.Extensions;

public static class WebApplicationExtensions
{
    // runBulkSeed = the `--seed` flag was passed: also generate the Bogus demo dataset (T24).
    public static async Task SeedDatabaseAsync(this WebApplication app, bool runBulkSeed = false)
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

            // Baseline (admin + JSON products + delivery) runs on every startup, all environments.
            await StoreContextSeed.SeedAsync(baseContext, userManager, adminPassword);

            // Bulk demo data is opt-in (--seed) and Development-only, so a deploy never auto-pollutes prod.
            if (runBulkSeed)
            {
                if (app.Environment.IsDevelopment())
                {
                    await BogusSeeder.SeedAsync(baseContext, userManager);
                    WriteLine($"Bulk demo data seeded (Bogus). DB now holds "
                        + $"{await baseContext.Products.CountAsync()} products, "
                        + $"{userManager.Users.Count()} users, "
                        + $"{await baseContext.Orders.CountAsync()} orders.");
                }
                else
                {
                    WriteLine("--seed ignored: bulk demo data only seeds in Development.");
                }
            }
        }
        catch (Exception ex)
        {
            WriteLine(ex.Message);
            throw;
        }
    }
}
