## SECTION B — ASP.NET CORE & MIDDLEWARE

### EASY

**B1.** What is middleware in ASP.NET Core? How is it different from a filter?

<details><summary>Answer</summary>

- **Middleware** — handles every HTTP request/response (logging, auth, CORS, exceptions); runs before routing; broader scope.
- **Filters** — run only around controller actions (action/result/exception filters); MVC-specific.

Order: `Middleware pipeline → Routing → Filters → Action`.
</details>

<details><summary>Deep Dive</summary>

**The middleware pipeline is a chain of delegates**

Each middleware is literally a `Func<RequestDelegate, RequestDelegate>`. A `RequestDelegate` is `Task Invoke(HttpContext ctx)`. When you call `app.Use(...)`, you're adding a link to a chain. At startup, the chain is compiled into a single nested delegate.

```
Request ──► Middleware1.Invoke()
                 │
                 ▼ next()
            Middleware2.Invoke()
                 │
                 ▼ next()
            Middleware3.Invoke()
                 │
                 ▼ next()
            [Endpoint / Action]
                 │
               Response flows back up through the same chain
```

The response flows backwards through the same chain (after `await next()`), which is why exception-handling middleware is placed first — it wraps everything.

**Middleware anatomy**

```csharp
// Inline (lambda) — for simple cases
app.Use(async (context, next) =>
{
    // Before: request going in
    context.Response.Headers["X-Request-Id"] = Guid.NewGuid().ToString();

    await next(context);  // call the rest of the pipeline

    // After: response going out
    _logger.LogInformation("Responded with {Status}", context.Response.StatusCode);
});

// Class-based — for testable, reusable middleware
public class TimingMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx)
    {
        var sw = Stopwatch.StartNew();
        await next(ctx);
        sw.Stop();
        ctx.Response.Headers["X-Elapsed-Ms"] = sw.ElapsedMilliseconds.ToString();
    }
}
```

Short-circuiting: if middleware does NOT call `next()`, the rest of the pipeline is skipped entirely. Rate-limiting and auth middleware use this to return 429/401 without ever reaching the controller.

**Filters are MVC-specific and run in a separate sub-pipeline**

Filters have access to MVC constructs: `ActionContext`, `ModelState`, `ActionDescriptor`, action arguments. They run after the endpoint is matched but around the action execution. The filter pipeline is:

```
Authorization Filter → Resource Filter → Model Binding → Action Filter → Action → Action Filter → Result Filter → Result → Exception Filter (if threw)
```

**When to use middleware vs filters**

| Use middleware for... | Use filters for... |
|---|---|
| Every request (even non-MVC: static files, health checks) | Around controller actions only |
| Exception handling (all layers) | Action-level exception handling |
| Auth/CORS/headers (run before routing) | Response modification after action |
| Logging/timing the full pipeline | Pre/post action validation |
| Short-circuit before routing | Injecting DI services into the filter |

**Important**: exception filters do NOT catch exceptions from middleware (e.g., an exception thrown in CORS middleware). Only exception-handling middleware (outermost) catches those. Exception filters only catch exceptions thrown by action methods and action filters.
</details>

---

**B2.** What is the difference between `AddScoped`, `AddTransient`, and `AddSingleton`? Give a real-world example for each.

<details><summary>Answer</summary>

- **`Singleton`** — one instance for the entire app lifetime (e.g. `IMemoryCache`, config service).
- **`Scoped`** — one instance per HTTP request (e.g. `DbContext`, `ICurrentUser`).
- **`Transient`** — new instance every injection (e.g. lightweight stateless services, validators).

Rule: `DbContext` is almost always `Scoped`.
</details>

<details><summary>Deep Dive</summary>

**Singleton — one instance, shared across all threads forever**

The container creates the instance once (on first resolve) and returns the same object every time, for the entire app lifetime. Because requests are processed on different threads, Singletons **must be thread-safe**. Use:
- `ConcurrentDictionary<K,V>` instead of `Dictionary<K,V>`
- `Interlocked` for counters
- `ImmutableList<T>` for read-heavy shared state
- Or design the singleton to be stateless

If a Singleton holds mutable state without synchronization, you get race conditions that are nearly impossible to reproduce in tests but happen constantly in production.

**Scoped — one instance per logical "scope"**

In ASP.NET Core, a scope is created at the start of each HTTP request and disposed at the end. This maps perfectly to "unit of work" — `DbContext` tracks entity changes for exactly one request. Two simultaneous requests get two completely separate `DbContext` instances.

The DI container uses `IServiceScopeFactory` to create and manage scopes. In background services (`IHostedService`), there is NO automatic scope — you must create one manually:

```csharp
public class MyBackgroundService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Orders.ToListAsync(ct); // Safe: scoped DbContext
            await Task.Delay(5000, ct);
        }
    }
}
```

Without the scope, resolving a Scoped service from a Singleton throws `InvalidOperationException` (when `ValidateScopes = true`, which is default in Development).

**Transient — new instance every injection**

The container creates a new object every single time `GetService<T>()` is called. This means if `ServiceA` and `ServiceB` both inject `IValidator`, they each get their own instance. Transient objects are NOT tracked by the container — they are created and immediately "forgotten". You are responsible for disposing them.

If a Transient service is `IDisposable`, it will be disposed when the enclosing scope ends (the container tracks it for cleanup). But if you resolve a Transient from the root scope (e.g., in a Singleton), it lives forever — a memory leak.

**The Captive Dependency problem visualized**

```
Singleton (lives forever)
  └── injects Scoped DbContext (should live per-request)
        = DbContext is now captured and lives forever too
        = stale entity tracking, connection leaks, thread-safety bugs
```

ASP.NET Core throws `InvalidOperationException` at startup when `ValidateScopes = true`. But this check only works for direct injection. If a Singleton holds a `Func<IDbContext>` factory and resolves from the root scope inside the factory, it bypasses the check — a common gotcha.

**Choosing the right lifetime — decision tree**

1. Does it share state across requests? → Singleton (and make it thread-safe)
2. Does it track per-request state (DB connection, current user, transaction)? → Scoped
3. Is it lightweight, stateless, or cheap to create? → Transient (but watch for IDisposable leaks)
</details>

---

**B3.** What does `app.UseAuthentication()` do? What happens if you put it AFTER `app.UseAuthorization()` in the pipeline?

<details><summary>Answer</summary>

- **`UseAuthentication`** — reads the token/cookie and populates `HttpContext.User`.
- **`UseAuthorization`** — checks `[Authorize]` attributes against that user.

If auth comes **after** authorization, the user identity isn't set yet — every `[Authorize]` endpoint returns 401. Order matters: Authentication first, then Authorization.
</details>

<details><summary>Deep Dive</summary>

**What `UseAuthentication` actually does internally**

`UseAuthentication` calls `IAuthenticationService.AuthenticateAsync()` on every request. This service tries each registered authentication scheme in order (cookie scheme, JWT bearer scheme, etc.). For each scheme, it calls the corresponding `IAuthenticationHandler.AuthenticateAsync()`:

- **Cookie scheme**: reads the cookie, decrypts and validates the `DataProtection`-encrypted payload, verifies the security stamp (on the validation interval), constructs a `ClaimsPrincipal`, and stores it in `HttpContext.User`.
- **JWT bearer scheme**: reads the `Authorization: Bearer <token>` header, validates the signature against the configured signing key, checks `exp`/`nbf`/`iss`/`aud` claims, and constructs `HttpContext.User` from the JWT claims.

If no scheme authenticates successfully, `HttpContext.User` is set to an anonymous `ClaimsPrincipal` (not null — always a principal, but `IsAuthenticated = false`).

**What `UseAuthorization` actually does**

`UseAuthorization` reads `HttpContext.User` (already populated by authentication) and the current endpoint's metadata (gathered by `UseRouting`). It looks for `[Authorize]` attributes on the endpoint and evaluates:
1. Is the user authenticated (`User.Identity.IsAuthenticated`)?
2. Does the user have the required roles (`[Authorize(Roles = "Admin")]`)?
3. Does the user satisfy the policy (`[Authorize(Policy = "MinAge18")]`)?

If any check fails, it calls `IAuthorizationService.ChallengeAsync()` (→ 401) or `ForbidAsync()` (→ 403).

**Why the order matters — in detail**

```
Request arrives
    ↓
UseAuthentication() ← populates HttpContext.User
    ↓
UseAuthorization()  ← reads HttpContext.User, checks [Authorize]
```

If swapped:
```
Request arrives
    ↓
UseAuthorization()  ← HttpContext.User is anonymous → ALL [Authorize] → 401
    ↓
UseAuthentication() ← never reached for protected endpoints (pipeline short-circuited)
```

**`[AllowAnonymous]` — important nuance**

`[AllowAnonymous]` bypasses the authorization check, but authentication STILL runs. This means even on anonymous endpoints, `HttpContext.User` is populated if a valid cookie/token is present. This is useful for endpoints that behave differently based on whether the user is logged in (e.g., showing a personalized greeting).

**Multiple authentication schemes**

```csharp
builder.Services.AddAuthentication()
    .AddCookie("Cookies")
    .AddJwtBearer("Bearer");

// Endpoint can specify which scheme to use:
[Authorize(AuthenticationSchemes = "Bearer")]
[HttpGet("api/orders")]
```

Or use a policy that requires a specific scheme. This is common in apps serving both browser clients (cookies) and API clients (JWT).
</details>

---

**B4.** What does `[ApiController]` attribute do on a controller? Name at least three behaviors it enables.

<details><summary>Answer</summary>

1. Automatic model validation — returns 400 if `ModelState` is invalid (no manual check needed).
2. Binding source inference — complex types from `[FromBody]`, simple types from `[FromQuery]`/`[FromRoute]`.
3. Problem details for errors (RFC 7807).
Also: requires `[Route]` attribute, disables `IActionResult` null returns.
</details>

<details><summary>Deep Dive</summary>

**Behavior 1: Automatic model validation in depth**

Without `[ApiController]`, after model binding you must write:
```csharp
if (!ModelState.IsValid) return BadRequest(ModelState);
```

With `[ApiController]`, an implicit `ModelStateInvalidFilter` runs before your action body. If `ModelState.IsValid` is false, it short-circuits with a `ValidationProblemDetails` (HTTP 400) that contains all validation errors grouped by field:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["The Email field is required.", "Not a valid email address."],
    "Price": ["Price must be greater than 0."]
  }
}
```

You can customize this via `ApiBehaviorOptions.InvalidModelStateResponseFactory`.

**Behavior 2: Binding source inference rules (precise)**

The inference algorithm checks in this order:
1. If the parameter type is `IFormFile` or `IFormFileCollection` → `[FromForm]`
2. If the parameter has `[FromServices]` attribute → DI injection
3. If it's a complex type (class/record) → `[FromBody]`
4. If a route template parameter with matching name exists → `[FromRoute]`
5. Otherwise → `[FromQuery]`

Nuance: if a complex type is used as a query parameter (e.g., `GET /products?filter.minPrice=10&filter.maxPrice=100`), you need an explicit `[FromQuery]` attribute — inference puts it in `[FromBody]` by default.

**Behavior 3: RFC 7807 Problem Details**

All error responses use a structured JSON format:
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Not Found",
  "status": 404,
  "traceId": "00-abc123-def456-00"
}
```

The `traceId` is automatically set from `Activity.Current.Id` (OpenTelemetry trace correlation). This is extremely useful — you can search your logs by traceId to find exactly what went wrong for a specific request.

**The `[Route]` requirement**

`[ApiController]` requires an explicit `[Route]` on the controller (not on individual actions). Convention-based routing (like MVC's `{controller}/{action}`) doesn't work. This enforces attribute routing which is more explicit, predictable, and RESTful.

**What `[ApiController]` does NOT do**

- It does NOT enable authentication or authorization.
- It does NOT add Swagger/OpenAPI — that's `Swashbuckle`/`NSwag`.
- It does NOT prevent you from returning arbitrary status codes via `StatusCode(418)`.
- It does NOT affect the serialization (that's `System.Text.Json` or `Newtonsoft.Json` config).
</details>

---

### MEDIUM

**B5.** Your API returns 500 on every request. In production, how do you handle exceptions without leaking internals? Design an exception handling middleware.

<details><summary>Answer</summary>

- Wrap `await next()` in try/catch.
- Catch exceptions, log full details (Serilog/AppInsights).
- Return a generic `ProblemDetails` with status 500 and a correlation ID — never expose stack traces.
- Map known domain exceptions (`NotFoundException` → 404, `ValidationException` → 400).
- Register **first** in the pipeline so it catches everything downstream.
</details>

<details><summary>Deep Dive</summary>

**Exception handling middleware — the only place that catches middleware exceptions**

Middleware deeper in the pipeline (auth, authorization, routing) may throw exceptions. Only exception-handling middleware wrapping them can catch these. If a CORS exception is thrown in the CORS middleware and there's no outer exception handler, the request returns a 500 to the client with no logging.

```csharp
app.UseExceptionHandler("/error");  // Outer exception handler — catches everything below

app.UseRouting();
app.UseCors();
app.UseAuthentication(); // If an exception throws here, the outer handler catches it
```

**Mapping known exceptions to HTTP status codes**

Rather than swallowing exceptions, the middleware should map domain exceptions:

```csharp
public class ExceptionHandlingMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new ProblemDetails 
            { 
                Title = "Not Found",
                Detail = ex.Message 
            });
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new ValidationProblemDetails
            {
                Errors = ex.Errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "An error occurred",
                Detail = "Please contact support.",
                Instance = Activity.Current?.Id ?? context.TraceIdentifier
            });
        }
    }
}
```

**Correlation IDs for request tracing**

Every error response should include a correlation ID (trace ID). This links the error response to the server logs so support can find what went wrong:

```
Client error response: {"traceId": "00-abc123def456-def789ghi012-00"}
Client reports: "Error 00-abc123def456-def789ghi012-00"
Support searches logs for that ID → finds detailed stack trace
```

ASP.NET Core automatically sets `Activity.Current.Id` from the `traceparent` header. The middleware includes it in the response so clients can report it.

**Logging the full exception detail before stripping it for the client**

The middleware logs the full exception (stack trace, inner exceptions) to your logging system (Serilog, AppInsights), then returns a generic message to the client. This prevents leaking sensitive details via stack traces (file paths, SQL queries, internal APIs) while still capturing the error for debugging.
</details>

---

**B6.** What is the request pipeline order for: `UseRouting`, `UseCors`, `UseAuthentication`, `UseAuthorization`, `UseEndpoints`? What breaks if you swap `UseCors` and `UseAuthentication`?

<details><summary>Answer</summary>

Correct order: `UseRouting` → `UseCors` → `UseAuthentication` → `UseAuthorization` → `UseEndpoints`.

- `UseRouting` first — selects the endpoint (CORS needs this to match a policy).
- `UseCors` before auth — otherwise preflight `OPTIONS` requests hit auth and return 401, blocking the browser.
- `UseAuthentication` before `UseAuthorization` — must establish WHO before checking WHAT they can access.
</details>

<details><summary>Deep Dive</summary>

**CORS preflight — why routing must come first**

A browser makes a CORS preflight `OPTIONS` request before the actual request if the request is cross-origin and uses complex headers/methods. The CORS middleware must see the request metadata (method, headers) and match it against the CORS policy. To do this, it needs to know which endpoint the request targets — thus `UseRouting` must run first.

If authentication runs before CORS sees the endpoint, the preflight `OPTIONS` request hits the auth middleware, fails (no credentials), returns 401. Browser sees 401 on preflight and blocks the actual request entirely. The real endpoint never gets called.

```
Client (domain A) → Server (domain B)
  CORS preflight OPTIONS /api/data
    ↓
  [UseAuthentication] — no creds, returns 401
    ↓
  Browser sees 401, blocks the actual request
  ↓
  XHR fails
```

Correct order:
```
  CORS preflight OPTIONS /api/data
    ↓
  [UseRouting] — identifies /api/data endpoint
    ↓
  [UseCors] — checks CORS policy, returns 200 OK (allows the preflight to succeed)
    ↓
  Actual request
```

**AllowCredentials() constraint**

When you use `AllowCredentials()` in CORS policy, you cannot use a wildcard origin:

```csharp
// BAD — throws at startup
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()           // wildcard origin
              .AllowCredentials();        // CONFLICT — credentials require specific origins
    });
});

// GOOD
options.AddPolicy("AllowSpecific", policy =>
{
    policy.WithOrigins("https://trusted.example.com")
          .AllowCredentials();
});
```

This is a security constraint: if wildcard origin is allowed with credentials, any cross-origin site could steal credentials.
</details>

---

**B7.** Explain `IOptions<T>`, `IOptionsSnapshot<T>`, and `IOptionsMonitor<T>`. Which reloads on config change? Which is safe for Singleton services? Which would you use for secrets that rotate?

<details><summary>Answer</summary>

`IOptions<T>`: Singleton, reads config once at startup, never reloads. Safe in Singletons.
`IOptionsSnapshot<T>`: Scoped, reloads per request from config source — NOT safe in Singletons.
`IOptionsMonitor<T>`: Singleton-safe AND reloads — uses change tokens and `OnChange` callback.
Use `IOptionsMonitor<T>` for rotating secrets (Key Vault with `ReloadInterval`).
</details>

<details><summary>Deep Dive</summary>

**IOptions — the simplest, one-time read**

`IOptions<T>` is the default choice for most settings:

```csharp
public class PaymentService
{
    private readonly StripeSettings _settings;
    
    public PaymentService(IOptions<StripeSettings> options)
    {
        _settings = options.Value;  // read once, store in field
    }
}
```

The value is read from the config provider chain once at DI container build time. It never changes. If you rotate a secret in Key Vault, the app doesn't pick it up until restart. Good for settings that don't change (API URLs, feature flags).

**IOptionsSnapshot — scoped reload**

`IOptionsSnapshot<T>` is Scoped, so each request re-reads the config:

```csharp
services.AddScoped<MyService>();

public class MyService
{
    public MyService(IOptionsSnapshot<AppSettings> options)
    {
        var settings = options.Value; // re-read from config providers each request
    }
}
```

This lets you pick up config changes per-request. BUT it can't be injected into Singletons (Captive Dependency). If a Singleton injects `IOptionsSnapshot`, it reads the value once in the constructor and never re-reads.

**IOptionsMonitor — Singleton-safe with callbacks**

`IOptionsMonitor<T>` is Singleton and reloads automatically:

```csharp
public class CacheService
{
    private readonly IOptionsMonitor<CacheSettings> _monitor;
    
    public CacheService(IOptionsMonitor<CacheSettings> monitor)
    {
        _monitor = monitor;
        // Subscribe to changes
        monitor.OnChange(newSettings =>
        {
            _logger.LogInformation("Cache settings updated");
            ReconfigureCache(newSettings);
        });
    }
    
    public void GetCacheValue(string key)
    {
        var settings = _monitor.CurrentValue;  // always current
    }
}
```

The provider uses change tokens (from Azure App Configuration or Key Vault) to notify subscribers when config changes. The `CurrentValue` property always reflects the latest config.

**Performance: when does each refresh?**

- `IOptions`: startup only
- `IOptionsSnapshot`: per request (scoped)
- `IOptionsMonitor`: on change events (can be minutes apart, depending on `ReloadInterval`)

For secrets that rotate frequently (API keys, tokens), use `IOptionsMonitor` with a reasonable reload interval (5–10 minutes). The callback can re-initialize dependent services (like refreshing auth headers).
</details>

---

**B8.** What does `[FromBody]`, `[FromQuery]`, `[FromRoute]`, and `[FromHeader]` do? When does `[ApiController]` infer the source automatically?

<details><summary>Answer</summary>

- **`[FromBody]`** — deserialize request body (JSON).
- **`[FromQuery]`** — bind from the query string.
- **`[FromRoute]`** — bind from the URL route template.
- **`[FromHeader]`** — bind from an HTTP header.

With `[ApiController]` auto-inference: complex types → `[FromBody]`; `IFormFile` → `[FromForm]`; name matches route template → `[FromRoute]`; otherwise → `[FromQuery]`.
</details>

<details><summary>Deep Dive</summary>

**Binding source inference — the exact algorithm**

The model binder runs BEFORE action filters. It checks the parameter type in this order:

1. Is parameter explicitly attributed (`[FromBody]`, `[FromRoute]`, etc.)? → Use that source.
2. Is it `IFormFile` or `IFormFileCollection`? → Infer `[FromForm]`.
3. Is it a "complex type" (class, record, struct with properties)? → Infer `[FromBody]`.
4. Is there a route template parameter matching the name (case-insensitive)? → Infer `[FromRoute]`.
5. Otherwise → Infer `[FromQuery]`.

Complex types are determined by whether the type has a public parameterless constructor and writable properties, OR is a primitive wrapper (like `Guid`).

**The query string binding edge case**

```csharp
[HttpGet("products")]
public IActionResult GetProducts([FromQuery] FilterOptions filter)
{
    // filter is a complex type, but [FromQuery] is explicit
    // The binder deconstructs query string into filter properties:
    // GET /products?filter.minPrice=10&filter.maxPrice=100
}

public record FilterOptions(decimal MinPrice, decimal MaxPrice);
```

Query string binding complex types by flattening properties. This is why prefix is important: `filter.minPrice` → `FilterOptions.MinPrice`.

**[FromServices] — DI injection in action parameters**

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetOrder(
    int id,  // [FromRoute]
    [FromServices] IOrderRepository repo)  // injected from DI
{
    var order = await repo.GetByIdAsync(id);
    return Ok(order);
}
```

This injects services directly into action parameters. Useful for optional dependencies or when you want explicit clarity that a parameter comes from DI, not the request.

**Body binding subtlety: only one [FromBody] per action**

```csharp
// INVALID — two [FromBody] parameters
public IActionResult Create(
    [FromBody] Order order,
    [FromBody] Address address)  // ERROR at runtime
{ }

// VALID — combine into one complex type
public IActionResult Create([FromBody] CreateOrderRequest request)
{
    var order = request.Order;
    var address = request.Address;
}
```

The model binder can only read the request body once. If you need multiple values, combine them into a wrapper DTO.
</details>

---

**B9.** You deploy to Azure App Service. The app starts but returns 500 on all endpoints. The log says "Unable to resolve service for type 'IOrderService'." What happened? How do you fix it?

<details><summary>Answer</summary>

The service wasn't registered in the DI container. Common causes:
- Forgot to register it in `Program.cs`.
- Registered the concrete type (`AddScoped<OrderService>()`) but injecting by interface (`IOrderService`).
- Typo in namespace — wrong type resolved.
- The service has an unresolvable transitive dependency.

Prevention: use `ValidateOnBuild = true` (.NET 8+), or add an integration test that resolves all registered services.
</details>

<details><summary>Deep Dive</summary>

**Common DI resolution errors — diagnosis**

Error: "Unable to resolve service for type 'IOrderService' while activating..."

Causes:
1. **Not registered**: `IOrderService` is never added to `services`.
2. **Registered the wrong type**: `services.AddScoped<OrderService>()` (concrete type) but injecting `IOrderService` (interface).
3. **Transitive dependency missing**: `OrderService` depends on `IRepository<Order>`, which is registered, but `Repository<T>` depends on `DbContext`, which isn't.
4. **Circular dependency**: `ServiceA` depends on `ServiceB`, which depends on `ServiceA`.
5. **Scope mismatch**: A Singleton depends on a Scoped service (Captive Dependency).

**Debugging DI registration issues**

Approach 1: integration test that validates all services can be resolved:

```csharp
[Fact]
public void AllServicesCanBeResolved()
{
    var provider = new ServiceCollection()
        .AddApplicationServices()
        .AddInfrastructureServices()
        .BuildServiceProvider();
    
    // Try to resolve every registered service
    foreach (var descriptor in provider.GetRequiredService<IEnumerable<ServiceDescriptor>>())
    {
        var instance = provider.GetService(descriptor.ServiceType);
        Assert.NotNull(instance);
    }
}
```

Approach 2: use `ValidateOnBuild` (.NET 8+):

```csharp
var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// ... register all services ...

// Build with validation
services.AddApplicationServices();
var app = builder.Build(validateScopes: true);  // throws if DI is broken
```

This validates the entire service graph at startup.

**The "Unable to resolve" error includes context**

The error message tells you the resolution chain:
```
InvalidOperationException: Unable to resolve service for type 'IOrderService' 
while activating 'OrderController'.
```

This tells you:
- The direct missing service: `IOrderService`
- The consumer trying to use it: `OrderController`

If it's a chain: "while activating `ServiceB` while activating `ServiceA`..."

**Extension methods for batch registration**

To avoid forgetting services, use extension methods:

```csharp
// In ApplicationServicesExtensions.cs
public static IServiceCollection AddApplicationServices(this IServiceCollection services)
{
    services.AddScoped<IOrderService, OrderService>();
    services.AddScoped<IProductService, ProductService>();
    services.AddScoped<IRepository<Order>, OrderRepository>();
    // ... all app layer services ...
    return services;
}

// In Program.cs — one line
builder.Services.AddApplicationServices();
```

This centralizes registration and makes it easy to see what's registered.
</details>

---

### HARD

**B10.** Design the middleware pipeline for an e-commerce API that needs: exception handling, CORS, CSP headers, authentication, authorization, rate limiting, and request logging. Draw the order and explain why each piece goes where it does.

<details><summary>Answer</summary>

Order:
1. `ExceptionHandler` — outermost, catches everything downstream.
2. `RequestLogging` — captures timing for all requests (including failed ones).
3. `SecurityHeaders`/CSP — adds headers before response.
4. `UseRouting` — selects endpoint (needed by CORS).
5. `UseCors` — needs endpoint info, must be before auth.
6. `RateLimiting` — reject before expensive auth work.
7. `UseAuthentication` — identity must be established.
8. `UseAuthorization` — check permissions after identity.
9. `UseEndpoints` — execute the action.

Reasoning: each layer depends on the one before it, and security layers run outside business logic.
</details>

<details><summary>Deep Dive</summary>

**Exception handler outermost — the safety net**

If any middleware below throws an exception, the outer exception handler catches it. This is the only place exceptions from middleware (auth, CORS, routing) can be caught and logged.

**Rate limiting before auth — efficiency**

Rate limiting rejects requests before expensive operations. If you're rate-limited, why run auth? Reverse: auth before authorization (you must know WHO before checking WHAT they can do).

**Security headers after routing**

Security headers (CSP, X-Frame-Options) are response headers. They must run after routing but before the endpoint executes (so the endpoint's response gets the headers).

**The response flows backward**

Request flows down: ExceptionHandler → RequestLogging → Routing → CORS → Auth → Endpoint.
Response flows back up: Endpoint → Auth → CORS → Routing → RequestLogging → ExceptionHandler (adds final headers/logging).
</details>

---

**B11.** You're building a multi-tenant SaaS app. Each tenant has its own database. How do you resolve a `DbContext` with a connection string that depends on the current request's tenant header? Which DI lifetime do you use and why?

<details><summary>Answer</summary>

Use Scoped lifetime. Create middleware that reads the tenant header and stores the tenant ID in `HttpContext.Items` or a scoped `ITenantContext`. Register `DbContext` as Scoped with a factory:

```csharp
AddDbContext<AppDb>((sp, opt) => {
    var tenant = sp.GetRequiredService<ITenantContext>();
    opt.UseSqlServer(GetConnString(tenant.Id));
});
```

Scoped ensures one `DbContext` per request with the correct tenant. Cache connection strings. Singleton would be dangerous (shared DB across tenants). Transient would create a new context on every injection.
</details>

<details><summary>Deep Dive</summary>

**Tenant resolution from the request**

Middleware runs before the request reaches the endpoint, so it's the perfect place to identify the tenant:

```csharp
app.Use(async (context, next) =>
{
    var tenantId = context.Request.Headers["X-Tenant-ID"].ToString();
    if (string.IsNullOrEmpty(tenantId))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("X-Tenant-ID header required");
        return;
    }
    
    var tenantContext = new TenantContext { Id = tenantId };
    context.Items["TenantContext"] = tenantContext;
    
    await next();
});
```

The tenant context is stored in `HttpContext.Items` — a request-scoped dictionary. The scoped DbContext can resolve it:

```csharp
services.AddScoped<ITenantContext>(sp =>
    (ITenantContext)sp.GetRequiredService<IHttpContextAccessor>()
        .HttpContext!.Items["TenantContext"]!);
```

**Connection string caching**

Don't query a connection string vault on every request. Cache tenant→connection string mappings with an expiry:

```csharp
private readonly IMemoryCache _cache;

private string GetConnectionString(string tenantId)
{
    return _cache.GetOrCreate($"tenant:{tenantId}", entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
        return _keyVault.GetSecretAsync($"connection-{tenantId}").Result;
    });
}
```

This avoids repeated vault calls while allowing connection string updates to propagate within an hour.

**Isolation — the critical requirement**

Each tenant's DbContext must connect to their database only. Data leakage (querying another tenant's data) is a security breach. Use integration tests to verify:

```csharp
[Fact]
public async Task TenantA_CannotSee_TenantB_Data()
{
    var contextA = CreateDbContext(tenantId: "A");
    var contextB = CreateDbContext(tenantId: "B");
    
    await contextA.Users.AddAsync(new User { Name = "Alice" });
    await contextA.SaveChangesAsync();
    
    var usersInB = await contextB.Users.ToListAsync();
    Assert.Empty(usersInB);  // Tenant B sees nothing
}
```
</details>

---

