using FluentValidation;
using Microsoft.OpenApi;
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

        // Registers every AbstractValidator<T> in the Application assembly as a scoped IValidator<T>.
        services.AddValidatorsFromAssemblyContaining<CreateProductDtoValidator>();

        // Declare Bearer in the OpenAPI document so Scalar shows an auth box.
        // Without a scheme Scalar sends no Authorization header and every secured call is a 401.
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, ct) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    In = ParameterLocation.Header,
                    Description = "Paste the accessToken from POST /api/login"
                };
                return Task.CompletedTask;
            });
        });

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
                    // A paused serverless database answers 40613 while it resumes.
                    // Retrying lets startup wait instead of crashing with 500.30 (BUG-8).
                    sql => sql.EnableRetryOnFailure(
                        maxRetryCount: 8,
                        maxRetryDelay: TimeSpan.FromSeconds(20),
                        errorNumbersToAdd: null)));
            services.AddScoped<StoreContext>(provider =>
                provider.GetRequiredService<SqlServerStoreContext>());
        }

        // Tagged "ready" so only /health/ready runs them — /health maps no checks at all.
        services.AddHealthChecks()
            .AddCheck<SqlHealthCheck>("sql", failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"], timeout: TimeSpan.FromSeconds(10))
            .AddCheck<RedisHealthCheck>("redis", failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"], timeout: TimeSpan.FromSeconds(5));

        // CartService keys Redis by the caller, so Infrastructure needs the current user.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

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
