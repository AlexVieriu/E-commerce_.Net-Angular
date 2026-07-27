using FluentValidation;
using Application.Common.Validators;

namespace API.Extensions;

public static class ApplicationServicesExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services,
        IConfiguration config, IWebHostEnvironment env)
    {
        services.AddControllers(options =>
        {
            // Runs FluentValidation on every action argument (see ValidationFilter).
            options.Filters.Add<ValidationFilter>();

            if (!env.IsDevelopment())
                options.Conventions.Add(new RemoveControllerConvention(typeof(BuggyController)));
        });

        // Scans the Application assembly for every AbstractValidator<T> and registers each as a
        // scoped IValidator<T>. Scoped is the safe default.
        services.AddValidatorsFromAssemblyContaining<CreateProductDtoValidator>();

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
                options.UseSqlServer(
                    config.GetConnectionString("SqlServerAzureConnection"),
                    // Serverless Azure SQL auto-pauses when idle; the first connection while it
                    // resumes throws transient error 40613. Retry so startup waits it out instead
                    // of crashing (500.30) — see BUG-8.
                    sql => sql.EnableRetryOnFailure(
                        maxRetryCount: 8,
                        maxRetryDelay: TimeSpan.FromSeconds(20),
                        errorNumbersToAdd: null)));
            services.AddScoped<StoreContext>(provider =>
                provider.GetRequiredService<SqlServerStoreContext>());
        }

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IDashboardService, DashboardService>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 200,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            options.AddPolicy("strict", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        services.AddCors(opt =>
        {
            opt.AddPolicy("CorsPolicy", policy =>
            {
                policy
                    .WithOrigins(
                        "http://localhost:4200",
                        "https://localhost:4200",
                        "https://skinet-2025-test-hra4dvcpa3hvfyeq.polandcentral-01.azurewebsites.net"
                    )
                    .AllowAnyHeader()
                    .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                    .AllowCredentials();
            });
        });
        services.AddAuthorization();
        services.AddIdentityApiEndpoints<AppUser>(options =>
                {
                    options.Password.RequiredLength = 12;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireDigit = true;
                    options.Password.RequireNonAlphanumeric = true;

                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    options.Lockout.AllowedForNewUsers = true;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<StoreContext>();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromHours(24);
            options.SlidingExpiration = false;
        });
        services.AddSignalR();

        services.AddOptions<StripeSettings>()
            .BindConfiguration("StripeSettings")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        StripeConfiguration.ApiKey = config["StripeSettings:SecretKey"]
            ?? throw new InvalidOperationException("StripeSettings:SecretKey is not configured.");

        return services;
    }
}
