namespace Infrastructure.Data;

public class StoreContextSeed
{
    public static async Task SeedAsync(StoreContext context, UserManager<AppUser> userManager,
        string adminPassword)
    {
        // Key Vault (AdminSettings:Password) is the source of truth for the admin password.
        // Create the admin if missing, otherwise RESET its password from config on every startup
        // so rotating the secret + restart actually updates the account (BUG-9). Without this the
        // seed only created-if-missing, leaving a stale password no one could recover.
        var admin = await userManager.FindByEmailAsync("admin@test.com");
        if (admin is null)
        {
            admin = new AppUser
            {
                UserName = "admin@test.com",
                Email = "admin@test.com",
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(admin, adminPassword);
            if (!createResult.Succeeded)
                throw new InvalidOperationException($"Failed to create admin user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }
        else
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(admin);
            var resetResult = await userManager.ResetPasswordAsync(admin, token, adminPassword);
            if (!resetResult.Succeeded)
                throw new InvalidOperationException($"Failed to reset admin password: {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");

            // Self-heal: a restart also clears any lockout / failed-attempt count on the admin.
            await userManager.SetLockoutEndDateAsync(admin, null);
            await userManager.ResetAccessFailedCountAsync(admin);
        }

        if (!await userManager.IsInRoleAsync(admin, "Admin"))
            await userManager.AddToRoleAsync(admin, "Admin");

        var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        if (!context.Products.Any())
        {
            var productsData = await File.ReadAllTextAsync(path + @"/Data/SeedData/products.json");
            var products = JsonSerializer.Deserialize<List<Product>>(productsData);

            if (products == null)
                return;

            context.Products.AddRange(products);

            await context.SaveChangesAsync();
        }

        if (!context.DeliveryMethods.Any())
        {
            var dmData = await File.ReadAllTextAsync(path + @"/Data/SeedData//delivery.json");
            var deliveryMethods = JsonSerializer.Deserialize<List<DeliveryMethod>>(dmData);

            if (deliveryMethods == null)
                return;

            context.DeliveryMethods.AddRange(deliveryMethods);

            await context.SaveChangesAsync();
        }
    }
}
