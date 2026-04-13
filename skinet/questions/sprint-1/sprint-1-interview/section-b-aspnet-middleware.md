## SECTION B — ASP.NET CORE & MIDDLEWARE

### EASY

**B1.** What is middleware in ASP.NET Core? How is it different from a filter?

<details><summary>Answer</summary>

- **Middleware** — handles every HTTP request/response (logging, auth, CORS, exceptions); runs before routing; broader scope.
- **Filters** — run only around controller actions (action/result/exception filters); MVC-specific.

Order: `Middleware pipeline → Routing → Filters → Action`.
</details>

---

**B2.** What is the difference between `AddScoped`, `AddTransient`, and `AddSingleton`? Give a real-world example for each.

<details><summary>Answer</summary>

- **`Singleton`** — one instance for the entire app lifetime (e.g. `IMemoryCache`, config service).
- **`Scoped`** — one instance per HTTP request (e.g. `DbContext`, `ICurrentUser`).
- **`Transient`** — new instance every injection (e.g. lightweight stateless services, validators).

Rule: `DbContext` is almost always `Scoped`.
</details>

---

**B3.** What does `app.UseAuthentication()` do? What happens if you put it AFTER `app.UseAuthorization()` in the pipeline?

<details><summary>Answer</summary>

- **`UseAuthentication`** — reads the token/cookie and populates `HttpContext.User`.
- **`UseAuthorization`** — checks `[Authorize]` attributes against that user.

If auth comes **after** authorization, the user identity isn't set yet — every `[Authorize]` endpoint returns 401. Order matters: Authentication first, then Authorization.
</details>

---

**B4.** What does `[ApiController]` attribute do on a controller? Name at least three behaviors it enables.

<details><summary>Answer</summary>

1. Automatic model validation — returns 400 if `ModelState` is invalid (no manual check needed).
2. Binding source inference — complex types from `[FromBody]`, simple types from `[FromQuery]`/`[FromRoute]`.
3. Problem details for errors (RFC 7807).
Also: requires `[Route]` attribute, disables `IActionResult` null returns.
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

---

**B6.** What is the request pipeline order for: `UseRouting`, `UseCors`, `UseAuthentication`, `UseAuthorization`, `UseEndpoints`? What breaks if you swap `UseCors` and `UseAuthentication`?

<details><summary>Answer</summary>

Correct order: `UseRouting` → `UseCors` → `UseAuthentication` → `UseAuthorization` → `UseEndpoints`.

- `UseRouting` first — selects the endpoint (CORS needs this to match a policy).
- `UseCors` before auth — otherwise preflight `OPTIONS` requests hit auth and return 401, blocking the browser.
- `UseAuthentication` before `UseAuthorization` — must establish WHO before checking WHAT they can access.
</details>

---

**B7.** Explain `IOptions<T>`, `IOptionsSnapshot<T>`, and `IOptionsMonitor<T>`. Which reloads on config change? Which is safe for Singleton services? Which would you use for secrets that rotate?

<details><summary>Answer</summary>

`IOptions<T>`: Singleton, reads config once at startup, never reloads. Safe in Singletons.
`IOptionsSnapshot<T>`: Scoped, reloads per request from config source — NOT safe in Singletons.
`IOptionsMonitor<T>`: Singleton-safe AND reloads — uses change tokens and `OnChange` callback.
Use `IOptionsMonitor<T>` for rotating secrets (Key Vault with `ReloadInterval`).
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

---

