namespace API.Extensions;

public static class ApplicationServicesExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services,
        IConfiguration config, IWebHostEnvironment env)
    {
        services.AddControllers(options =>
        {
            if (!env.IsDevelopment())
                options.Conventions.Add(new RemoveControllerConvention(typeof(BuggyController)));
        });

        services.AddOpenApi();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var conStr = config.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("Redis connection string not found");
            return ConnectionMultiplexer.Connect(ConfigurationOptions.Parse(conStr, true));
        });
        services.AddSingleton<IResponseCacheService, ResponseCacheService>();

        if (env.IsDevelopment())
        {
            services.AddDbContext<SqliteStoreContext>(options =>
                options.UseSqlite(config.GetConnectionString("DefaultConnection")));
            services.AddScoped<StoreContext>(provider =>
                provider.GetRequiredService<SqliteStoreContext>());
        }
        else
        {
            services.AddDbContext<SqlServerStoreContext>(options =>
                options.UseSqlServer(config.GetConnectionString("SqlServerAzureConnection")));
            services.AddScoped<StoreContext>(provider =>
                provider.GetRequiredService<SqlServerStoreContext>());
        }

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ICouponService, CouponService>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1)
                    }));

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

        services.AddCors();
        services.AddAuthorization();
        services.AddIdentityApiEndpoints<AppUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<StoreContext>();
        services.AddSignalR();

        StripeConfiguration.ApiKey = config["StripeSettings:SecretKey"]
            ?? throw new InvalidOperationException("StripeSettings:SecretKey is not configured.");

        return services;
    }
}
