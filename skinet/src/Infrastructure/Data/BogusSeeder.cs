using Bogus;

namespace Infrastructure.Data;

// Bulk demo data for the admin panel (T24): 50+ products, 10+ customers, 20+ orders.
// Dev-only, opt-in via `dotnet run -- --seed`. Idempotent: re-running is a no-op once orders exist.
public static class BogusSeeder
{
    // Standard demo password for every generated customer (matches the project's test login).
    private const string CustomerPassword = "Pa$$w0rd12!@";

    // Product images are a fixed local set under wwwroot — fake URLs would render broken, so we
    // only ever assign a real file, grouped by type so the picture roughly matches the category.
    private static readonly Dictionary<string, string[]> ImagesByType = new()
    {
        ["Boards"] = ["sb-ang1.png", "sb-ang2.png", "sb-core1.png", "sb-core2.png", "sb-react1.png", "sb-ts1.png"],
        ["Boots"] = ["boot-ang1.png", "boot-ang2.png", "boot-core1.png", "boot-core2.png", "boot-redis1.png"],
        ["Gloves"] = ["glove-code1.png", "glove-code2.png", "glove-react1.png", "glove-react2.png"],
        ["Hats"] = ["hat-core1.png", "hat-react1.png", "hat-react2.png"]
    };

    // Exact casing of the baseline JSON brands — avoids case-only duplicates in the brand filter.
    private static readonly string[] Brands = ["Angular", "NetCore", "React", "Typescript", "Redis", "VS Code"];
    private static readonly string[] CardBrands = ["visa", "mastercard", "amex"];

    // Weighted toward PaymentReceived so the dashboard looks like a real shop, with a few edge states.
    private static readonly OrderStatus[] Statuses =
    [
        OrderStatus.Pending,
        OrderStatus.PaymentReceived, OrderStatus.PaymentReceived, OrderStatus.PaymentReceived,
        OrderStatus.Refunded,
        OrderStatus.PaymentFailed
    ];

    public static async Task SeedAsync(StoreContext context, UserManager<AppUser> userManager)
    {
        // Orders only ever come from this seeder — their presence means we've already bulk-seeded.
        if (await context.Orders.AnyAsync())
            return;

        // Fixed seed → the same dataset every run (reproducible screenshots / demos).
        Randomizer.Seed = new Random(24_2026);
        var f = new Faker();

        var products = await SeedProductsAsync(context);
        var customerEmails = await SeedCustomersAsync(userManager, f);
        await SeedOrdersAsync(context, f, products, customerEmails);
    }

    private static async Task<List<Product>> SeedProductsAsync(StoreContext context)
    {
        var types = ImagesByType.Keys.ToArray();

        var productFaker = new Faker<Product>()
            .RuleFor(p => p.Type, f => f.PickRandom(types))
            .RuleFor(p => p.Brand, f => f.PickRandom(Brands))
            // Type[..^1] singularizes "Boards" -> "Board" etc. for a natural name.
            .RuleFor(p => p.Name, (f, p) => $"{p.Brand} {f.Commerce.ProductAdjective()} {p.Type[..^1]}")
            .RuleFor(p => p.Description, f => f.Lorem.Sentences(2))
            .RuleFor(p => p.Price, f => Math.Round(f.Random.Decimal(10, 300), 2))
            // 0-150 so some land low/out of stock for the dashboard's low-stock view.
            .RuleFor(p => p.QuantityInStock, f => f.Random.Int(0, 150))
            .RuleFor(p => p.PictureUrl, (f, p) => $"/images/products/{f.PickRandom(ImagesByType[p.Type])}");

        var products = productFaker.Generate(52);
        context.Products.AddRange(products);
        await context.SaveChangesAsync(); // populates Ids for the order references below

        return products;
    }

    private static async Task<List<string>> SeedCustomersAsync(UserManager<AppUser> userManager, Faker f)
    {
        var addressFaker = new Faker<Address>()
            .RuleFor(a => a.Line1, f => f.Address.StreetAddress())
            .RuleFor(a => a.Line2, f => f.Random.Bool(0.4f) ? f.Address.SecondaryAddress() : null)
            .RuleFor(a => a.City, f => f.Address.City())
            .RuleFor(a => a.State, f => f.Address.StateAbbr())
            .RuleFor(a => a.PostalCode, f => f.Address.ZipCode())
            .RuleFor(a => a.Country, f => f.Address.CountryCode());

        var emails = new List<string>();

        for (var i = 0; i < 12; i++)
        {
            var firstName = f.Name.FirstName();
            var lastName = f.Name.LastName();
            // Index keeps the address unique; Clean() strips chars Identity's username rules reject.
            var email = $"{Clean(firstName)}.{Clean(lastName)}{i}@example.com";

            var user = new AppUser
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                Address = addressFaker.Generate()
            };

            var result = await userManager.CreateAsync(user, CustomerPassword);
            if (result.Succeeded)
                emails.Add(email);
        }

        return emails;
    }

    private static async Task SeedOrdersAsync(StoreContext context, Faker f,
        List<Product> products, List<string> customerEmails)
    {
        var deliveryMethods = await context.DeliveryMethods.ToListAsync();
        if (deliveryMethods.Count == 0 || customerEmails.Count == 0)
            return; // nothing valid to reference — skip rather than throw

        var shippingFaker = new Faker<ShippingAddress>()
            .RuleFor(a => a.Name, f => f.Name.FullName())
            .RuleFor(a => a.Line1, f => f.Address.StreetAddress())
            .RuleFor(a => a.Line2, f => f.Random.Bool(0.4f) ? f.Address.SecondaryAddress() : null)
            .RuleFor(a => a.City, f => f.Address.City())
            .RuleFor(a => a.State, f => f.Address.StateAbbr())
            .RuleFor(a => a.PostalCode, f => f.Address.ZipCode())
            .RuleFor(a => a.Country, f => f.Address.CountryCode());

        var orders = new List<Order>();

        for (var i = 0; i < 25; i++)
        {
            // PickRandom(count) draws distinct products so an order never lists the same item twice.
            var orderItems = f.PickRandom(products, f.Random.Int(1, 4))
                .Select(p => new OrderItem
                {
                    ItemOrdered = new ProductItemOrdered
                    {
                        ProductId = p.Id,
                        ProductName = p.Name,
                        PictureUrl = p.PictureUrl
                    },
                    Price = p.Price,
                    Quantity = f.Random.Int(1, 3)
                })
                .ToList();

            var subtotal = orderItems.Sum(x => x.Price * x.Quantity);
            var discount = f.Random.Bool(0.3f) ? Math.Round(subtotal * 0.1m, 2) : 0m;

            orders.Add(new Order
            {
                BuyerEmail = f.PickRandom(customerEmails),
                OrderDate = f.Date.RecentOffset(90).UtcDateTime,
                ShippingAddress = shippingFaker.Generate(),
                DeliveryMethod = f.PickRandom(deliveryMethods), // tracked entity -> EF sets the FK, no insert
                PaymentSummary = new PaymentSummary
                {
                    Last4 = f.Random.Int(1000, 9999),
                    Brand = f.PickRandom(CardBrands),
                    ExpMonth = f.Random.Int(1, 12),
                    ExpYear = f.Random.Int(2026, 2030)
                },
                OrderItems = orderItems,
                Subtotal = subtotal,
                Discount = discount,
                Status = f.PickRandom(Statuses),
                // Index-based -> deterministic AND unique, satisfying the PaymentIntentId unique index (T18).
                PaymentIntentId = $"pi_seed_{i:D6}"
            });
        }

        context.Orders.AddRange(orders);
        await context.SaveChangesAsync();
    }

    // Keep only letters/digits — Identity's default AllowedUserNameCharacters rejects apostrophes etc.
    private static string Clean(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
