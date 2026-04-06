namespace Infrastructure.Data;

public class StoreContextSeed
{
    public static async Task SeedAsync(StoreContext context, UserManager<AppUser> userManager,
        string adminPassword)
    {
        if (!userManager.Users.Any(x => x.UserName == "admin@test.com"))
        {
            var user = new AppUser
            {
                UserName = "admin@test.com",
                Email = "admin@test.com"
            };

            var result = await userManager.CreateAsync(user, adminPassword);
            if (!result.Succeeded)
                throw new Exception($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            await userManager.AddToRoleAsync(user, "Admin");
        }

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
