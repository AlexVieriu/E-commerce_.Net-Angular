## SECTION E — ERROR HANDLING & MIDDLEWARE

> **Source:** Architecture docs — `ExceptionMiddleware`, `ApiErrorResponse`, `[ApiController]` automatic responses, `BuggyController`.

---

### EASY

**E1.** [HIGH] Why must `ExceptionMiddleware` be registered as the FIRST middleware in the pipeline?

<details>
<summary>The 5% Answer</summary>

Middleware executes in registration order on the request path. The response flows back in **reverse** order.

**If registered after other middleware:**
```
Request → [AuthMiddleware throws] → never reaches ExceptionMiddleware
                                   → unhandled exception bubbles to ASP.NET's default handler
                                   → HTML error page, not JSON — client gets an unparseable response
```

**Registered first:**
```
Request → [ExceptionMiddleware wraps everything] → [Auth] → [Routing] → [Controllers]
         ←── catches any exception from Auth, Routing, or Controllers ───────────────
```

The middleware wraps the entire pipeline. Any exception thrown anywhere downstream is caught, logged, and returned as a consistent JSON `ApiErrorResponse`. Without this, a crash in routing middleware returns HTML — Angular's error interceptor gets `err.error` as an HTML string, not a parseable object.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Register it after `UseRouting()` so it has access to route data." The route data isn't needed to format an error. The requirement is to wrap the entire pipeline — positioning after routing leaves auth/routing exceptions uncaught.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `app.UseDeveloperExceptionPage()` and `app.UseExceptionHandler()` as ASP.NET's built-in equivalents. `ExceptionMiddleware` is a custom version that provides more control: JSON-only responses, environment-aware detail exposure, and typed exception mapping. `UseExceptionHandler("/error")` redirects to an error endpoint — adding a round-trip. Custom middleware handles it in-place.

</details>

---

### MEDIUM

**E2.** [HIGH] [OUT-OF-SCOPE] Your `ExceptionMiddleware` returns stack traces in development. A developer accidentally deploys a development build to production (`ASPNETCORE_ENVIRONMENT=Development`). What is exposed and what is the OWASP classification?

<details>
<summary>The 5% Answer</summary>

**What is exposed in error responses:**
- Full stack trace with file paths: `at OrderService.CreateOrderAsync(CreateOrderDto dto) in /home/runner/work/skinet/src/Infrastructure/Services/OrderService.cs:line 47`
- Internal class names and namespaces: exposes the exact layer structure
- Framework version embedded in the stack: attackers scan for CVEs against that version
- EF Core exception details: table names, column names, SQL fragments
- Connection string fragments in `SqlException` messages: partial connection info including server names

**OWASP classification:**
OWASP Top 10 A05:2021 — **Security Misconfiguration**. Specifically: "Detailed error messages (stack traces, application errors) [that] expose information useful to attackers."

**Prevention:**
1. CI/CD pipeline sets `ASPNETCORE_ENVIRONMENT=Production` as an Azure App Setting — never hardcoded in the build artifact
2. The middleware check `env.IsDevelopment()` ensures production builds never hit the stack-trace branch even if misconfigured
3. Smoke test after every deploy: call a known-error endpoint and confirm the response contains no `stackTrace` field

**Production consequence:** an attacker probing the API with malformed requests gets a complete map of the codebase, server paths, EF Core version, and SQL schema — all useful for targeting known CVEs or crafting injection attacks.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Stack traces are in logs, not responses." Logs and responses are separate. The middleware explicitly writes `exception.StackTrace` into the JSON response body in dev mode. An attacker doesn't need log access — the response body is returned to their HTTP client.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning automated security header scanning (tools like `SecurityHeaders.com`, OWASP ZAP, or GitHub Advanced Security's code scanning) as part of CI to catch misconfigurations before they reach production. A simple grep for `"stackTrace"` in integration test responses catches the problem automatically.

</details>

---

**E3.** [HIGH] [OUT-OF-SCOPE] What's the difference between how `[ApiController]` handles 400 errors vs how `ExceptionMiddleware` handles them? Do they share the same response format?

<details>
<summary>The 5% Answer</summary>

**`[ApiController]` (automatic model validation):**
- Handles `ModelState.IsValid == false` before the action method runs — a filter, not an exception
- Returns RFC 7807 `ProblemDetails` format:
```json
{ "type": "...", "title": "One or more validation errors occurred.", "status": 400, "errors": { "Email": ["'Email' is required."] } }
```
- No exception is thrown — the pipeline short-circuits before reaching the controller action

**`ExceptionMiddleware` (thrown exceptions):**
- Catches exceptions that propagate up through the pipeline
- Returns `ApiErrorResponse` format:
```json
{ "statusCode": 400, "message": "Stock changed during checkout. Please try again.", "details": null }
```
- Two different shapes: `ProblemDetails` vs `ApiErrorResponse`

**Angular's error interceptor handles both:**
```typescript
// Checks for ProblemDetails shape (validation)
if (err.error.errors) handleValidationErrors(err.error.errors);
// Falls back to ApiErrorResponse shape (domain error)
else snackbar.error(err.error.message);
```

Without this dual-check, validation errors would fall through to the generic snackbar handler, showing "undefined" instead of field-level error messages.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"ExceptionMiddleware catches model validation errors." Model validation failures from `[ApiController]` don't become exceptions. They're handled by the filter pipeline and return 400 directly — `ExceptionMiddleware` never sees them.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `IProblemDetailsService` (ASP.NET 7+) as the modern approach to unifying response shapes. If you implement `IExceptionHandler` and return `ProblemDetails` for all errors, both validation failures and domain exceptions share the same JSON shape — Angular can use one parsing path. The tradeoff: you lose the ability to include custom fields (like `stackTrace` in dev) without extending `ProblemDetails`.

</details>

---

**E4.** [MEDIUM] The `BuggyController` exists for dev-only error testing. How is it excluded from production routing without using `#if DEBUG` preprocessor directives?

<details>
<summary>The 5% Answer</summary>

Using an `IApplicationModelConvention` — a code-based mechanism that removes the controller from the routing model before requests can reach it:

```csharp
// Registered at startup — controller is removed from routing model entirely
services.AddControllers(options =>
{
    if (!env.IsDevelopment())
        options.Conventions.Add(new RemoveControllerConvention(typeof(BuggyController)));
});

public class RemoveControllerConvention : IApplicationModelConvention
{
    private readonly Type _controllerType;
    public RemoveControllerConvention(Type type) => _controllerType = type;

    public void Apply(ApplicationModel application)
    {
        var controller = application.Controllers
            .FirstOrDefault(c => c.ControllerType == _controllerType.GetTypeInfo());
        if (controller != null) application.Controllers.Remove(controller);
    }
}
```

In production: `GET /api/buggy/notfound` returns `404` — the route doesn't exist.

**Why not `#if DEBUG`:**
`DEBUG` symbol depends on build configuration — it must be correctly set for every build. A production deployment built in `Debug` mode (common CI misconfiguration) would still include the controller. The runtime `IsDevelopment()` check is environment-aware and independent of build configuration.

**Why not `[Authorize]`:**
`[Authorize]` means the routes still exist — only access is restricted. A security misconfiguration (e.g., wrong auth scheme) could expose them. Removing from routing is a stronger guarantee.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Add `[ApiExplorerSettings(IgnoreApi = true)]` to hide it." That hides the controller from Swagger documentation but the routes are still registered and accessible. Anyone who guesses `/api/buggy/servererror` still gets a 500 with a stack trace.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `IApplicationModelConvention` runs once at startup — zero runtime overhead for each request. The route is simply not registered, so ASP.NET's routing table has no entry to match. This is strictly better than runtime checks on every request to the buggy endpoint.

</details>

---

### HARD

**E5.** [HIGH] Find all 3 bugs in this `ExceptionMiddleware` and write the corrected version:

```csharp
public async Task InvokeAsync(HttpContext context)
{
    try
    {
        await _next(context);
    }
    catch (Exception ex)
    {
        var response = new ApiErrorResponse(
            statusCode: 500,
            message: ex.Message,
            details: ex.StackTrace
        );
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(response);
    }
}
```

<details>
<summary>The 5% Answer</summary>

**Bug 1 — `ex.StackTrace` always included:**
No environment check. In production, every unhandled exception leaks the full stack trace to the client — file paths, line numbers, class structure.

**Bug 2 — `ex.Message` always exposed:**
`ex.Message` can contain sensitive details: EF Core includes table/column names in `DbUpdateException.Message`, SQL Server includes query fragments in `SqlException.Message`. In production, the message should be `"Internal Server Error"` (generic) while the real message is logged server-side only.

**Bug 3 — No logging and no status code mapping:**
All exceptions return 500 regardless of type. `PriceChangedException` should be 409, `InvalidOperationException` should be 400. And critically: no `ILogger.LogError(ex, ...)` call — exceptions are swallowed silently with no server-side record.

**Corrected version:**
```csharp
public async Task InvokeAsync(HttpContext context)
{
    try
    {
        await _next(context);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);

        var (statusCode, message) = ex switch
        {
            PriceChangedException pce => (409, pce.Message),
            InvalidOperationException ioe => (400, ioe.Message),
            _ => (500, "Internal Server Error")
        };

        var details = _env.IsDevelopment() ? ex.StackTrace : null;
        var response = new ApiErrorResponse(statusCode, message, details);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(response);
    }
}
```

**Production consequence:** the original silently exposes full application internals on every exception, logs nothing, and maps everything to 500 — making all errors look identical in monitoring.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Only identifying one or two bugs. The logging omission is the hardest to spot but arguably the most important in production — without it, a 3am production outage produces zero diagnostic information.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `IExceptionHandler` (ASP.NET Core 8+) as the modern, structured replacement for custom exception middleware. It provides a `TryHandleAsync` pattern, supports chaining multiple handlers, integrates with `ProblemDetailsFactory`, and handles response already started edge cases automatically. For new projects, prefer `IExceptionHandler`; custom middleware is still valid but requires manual handling of these edge cases.

</details>

---

**E6.** [HIGH] A 404 response navigates Angular to `/not-found`. But some 404s (empty search results) should stay on the current page. How do you fix the interceptor, and what is the correct HTTP semantic for "search returned nothing"?

<details>
<summary>The 5% Answer</summary>

**Current problem:**
```typescript
case 404:
    this.router.navigate(['/not-found']);
    break;
```
`GET /api/products?search=nonexistentterm` → 404 → user gets navigated away from the shop page during a search.

**Differentiation approach — by URL pattern:**
```typescript
case 404:
    // Only navigate for resource-not-found 404s, not empty collections
    if (!req.url.includes('/api/products')) {
        this.router.navigate(['/not-found']);
    }
    return throwError(() => err);
```

**But the correct fix is the API design:**
A search returning 0 results is **not** a 404. The `/api/products` endpoint *exists* — it's functioning correctly and returned an empty set. The correct response is `200 OK` with an empty array:
```json
{ "data": [], "count": 0, "pageIndex": 1, "pageSize": 10 }
```

Reserve 404 for "this specific resource does not exist" — e.g., `GET /api/products/99999` for a product ID that doesn't exist. 404 means "I looked for a thing and there is no such thing." An empty search result means "I looked and found nothing matching your criteria" — valid, successful operation.

Fixing the API semantic eliminates the ambiguity entirely. The interceptor never needs to distinguish — no product 404 should ever be "empty list."

**Production consequence:** an API that returns 404 for empty collections forces every client to implement fragile URL-pattern checks to distinguish "no results" from "endpoint missing." A third-party integration or mobile client that treats all 404s as "not found" displays an error page for a normal empty search — a client bug caused by a wrong API contract.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Check `err.error.message` to decide whether to navigate." Message strings are not a stable API contract. They change with code changes and internationalisation. Status codes are the contract.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning RFC 9110 (HTTP Semantics): "The 404 (Not Found) status code indicates that the origin server did not find a current representation for the target resource." A collection endpoint with no matching items is a resource that *exists* — it just has no members. `200 []` is correct. Sending 404 for empty collections is a common API design mistake that forces clients to differentiate between "endpoint doesn't exist" and "no results."

</details>

---

**E7.** [HIGH] What is the exact JSON shape `ExceptionMiddleware` returns in development vs production? Why does Angular's error interceptor break if the server returns HTML instead?

<details>
<summary>The 5% Answer</summary>

**Development response (`env.IsDevelopment() == true`):**
```json
{
  "statusCode": 500,
  "message": "Object reference not set to an instance of an object.",
  "details": "   at OrderService.CreateOrderAsync(...) in /src/Infrastructure/Services/OrderService.cs:line 47\n   at API.Controllers.OrdersController..."
}
```

**Production response:**
```json
{
  "statusCode": 500,
  "message": "Internal Server Error",
  "details": null
}
```

**Why Angular breaks on HTML:**
If `ExceptionMiddleware` is not registered (or registered too late), ASP.NET falls back to its built-in `Developer Exception Page` or IIS error pages — both return `text/html`. Angular's `errorInterceptor` does:
```typescript
snackbar.error(err.error.message ?? 'An error occurred');
```
`err.error` is an HTML string, not an object. `.message` is `undefined` — the snackbar shows "An error occurred" for every unhandled exception, with no debugging information. In dev, this masks the actual error entirely.

The `Content-Type: application/json` header set by `ExceptionMiddleware` ensures Angular's `HttpClient` parses the response as JSON automatically.

**Production consequence:** if `ExceptionMiddleware` is removed or registered too late, every unhandled exception returns an HTML error page to Angular. `err.error.message` is `undefined`, the snackbar shows "An error occurred" for every 500, and useful stack traces are lost even in development — all errors become indistinguishable to the developer.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Angular can parse any response format — just check `err.status`." Angular's `HttpClient` parses `err.error` as JSON only when the `Content-Type` is `application/json`. An HTML response with status 500 gives you `err.error` as a raw HTML string — calling `.message` on it returns `undefined`.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Adding a smoke test after every deploy: `GET /api/buggy/servererror` in dev, assert the response body is valid JSON with a `statusCode` field and `Content-Type: application/json`. If `ExceptionMiddleware` registration ever regresses, this test fails immediately rather than silently returning HTML to Angular.

</details>

---

**E8.** [HIGH] [OUT-OF-SCOPE] This project sets `script-src: 'self' 'unsafe-inline'` in the CSP header. Explain exactly what threat `unsafe-inline` reintroduces, why the project has no choice with the current Angular setup, and how Angular 22's nonce-based CSP eliminates it.

<details>
<summary>The 5% Answer</summary>

**What `unsafe-inline` reintroduces:**
Content Security Policy's primary defence against XSS is blocking inline scripts — any `<script>` tag injected by an attacker is blocked because the source is `inline`, not an allow-listed origin. `unsafe-inline` disables this protection. A successful XSS payload (`<script>alert(document.cookie)</script>` injected into a comment field) executes despite the CSP.

**Why the project has no choice today:**
Angular's bootstrap process and `@angular/material` inject inline `<style>` and `<script>` blocks at runtime. The Angular build tool doesn't (by default) add `nonce` attributes to these. Without `unsafe-inline`, the Angular app fails to boot:
```
Refused to apply inline style because it violates Content-Security-Policy directive: "style-src 'self'"
```

**Angular 22 nonce-based CSP (`script-src 'nonce-{random}'`):**
Angular 19+ supports `--csp-nonce` in the build, and the dev server injects a random nonce per request:
```html
<script nonce="r4nd0m8yt3s">/* Angular bootstrap */</script>
```
Server CSP header:
```
Content-Security-Policy: script-src 'self' 'nonce-r4nd0m8yt3s'
```
Only scripts with the matching nonce execute. An attacker's injected `<script>` has no nonce — it's blocked. `unsafe-inline` is no longer needed.

**Migration path for this project:**
1. Add `--csp-nonce` to the Angular build command
2. Generate a cryptographically random nonce per HTTP request in ASP.NET
3. Inject it into `index.html` via `IHtmlDocumentTagHelperComponent`
4. Add `nonce-{value}` to `script-src` in the CSP header
5. Remove `unsafe-inline` from both `script-src` and `style-src`

**Production consequence:** `unsafe-inline` means a single stored XSS vulnerability (e.g., a product review with malicious HTML) bypasses the entire CSP layer. All the hardened cookie settings (`HttpOnly`, `Secure`, `SameSite=Strict`) protect the token — but `unsafe-inline` XSS can still call authenticated API endpoints directly from the victim's browser tab.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "CSP replaces `HttpOnly` — if CSP blocks the script the cookie is safe." CSP and `HttpOnly` are independent layers. CSP prevents **execution** of injected scripts. `HttpOnly` prevents **reading** `document.cookie` even if a script does execute. Both are needed — `unsafe-inline` weakens CSP, not `HttpOnly`.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing **Trusted Types** (a CSP extension) as the companion to nonce-based CSP — it prevents DOM-based XSS by requiring that all DOM sinks (`innerHTML`, `document.write`) receive sanitized `TrustedHTML` objects instead of raw strings. Angular's `DomSanitizer` already produces `TrustedHTML` when Trusted Types is enabled — zero code change for Angular apps. Chrome enforced Trusted Types in Google.com since 2020.

</details>

---

## ⚡ Syntax Quick-Fire

**E-QF1.** [HIGH] In C#, what is the difference between `throw` and `throw ex` inside a `catch` block?

<details>
<summary>The 5% Answer</summary>

- **`throw`** — rethrows the current exception preserving the **original stack trace**. The call site where the exception was first thrown remains visible in logs.
- **`throw ex`** — resets the stack trace to the current line. The original call site is lost — all you see in the log is the `catch` block location, not where the error actually occurred.

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "...");
    throw;        // ✓ original stack trace preserved
    // throw ex;  // ✗ stack trace reset to this line
}
```

Use `throw new WrapperException("context", ex)` only when genuinely adding diagnostic context — the inner exception chain still preserves the original trace.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Using `throw ex` in a logging catch block. Every on-call incident becomes harder to diagnose — the stack trace points to the `catch` block, not the actual failure site. This is one of the most common ways to lose production debugging information.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing `ExceptionDispatchInfo.Capture(ex).Throw()` — preserves the original stack trace even when rethrowing from a different thread or async context. Used internally by `Task.WhenAll` and useful when propagating exceptions across thread boundaries manually.

</details>

---

**E-QF2.** [HIGH] What HTTP status code is correct for "the request conflicts with current server state"? Name two examples from this project.

<details>
<summary>The 5% Answer</summary>

**`409 Conflict`** — the request is valid but conflicts with the current state of the resource.

Examples from this project:
1. **`PriceChangedException`** — price changed between cart creation and order submission. The request (place order at old price) conflicts with current server state (new price). Client must refresh and re-confirm.
2. **Duplicate order for the same `PaymentIntentId`** — a PaymentIntent can only be fulfilled once. A second order attempt with the same intent ID conflicts with the already-fulfilled state.

Both return 409 so Angular can handle them specifically (show "price changed" UI) rather than treating them as generic 500 errors.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Returning 400 for conflict scenarios. 400 means "the request is malformed." A conflict request is syntactically valid — the client did everything right but the server state changed underneath them. 409 signals "retry with updated data," 400 signals "fix your request."

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing RFC 9110's distinction — 409 is idempotent-safe for retry after state refresh. Angular's error interceptor can pattern-match on 409 specifically to trigger a cart reload and re-display the updated price, guiding the user to reconfirm rather than showing a generic error.

</details>

---

**E-QF3.** [HIGH] Does `ExceptionMiddleware` catch the 400 returned by `[ApiController]` model validation?

<details>
<summary>The 5% Answer</summary>

**No.** `[ApiController]` handles model validation in the **filter pipeline** — it short-circuits before the action method runs and returns a `ProblemDetails` 400 directly. No exception is thrown, so `ExceptionMiddleware` (which only catches thrown exceptions) never sees it.

```
Request → [ExceptionMiddleware wraps] → Routing → [ApiController filter: ModelState invalid?]
                                                    → yes: return 400 ProblemDetails immediately
                                                           ExceptionMiddleware never fires
                                                    → no: action runs → exceptions caught by middleware
```

Angular's error interceptor must handle both shapes: `ProblemDetails` (from `[ApiController]`) and `ApiErrorResponse` (from `ExceptionMiddleware`).

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "I'll catch all 400 errors in `ExceptionMiddleware` by checking `ModelState.IsValid`." `ExceptionMiddleware` is middleware — it wraps `_next(context)` and catches **thrown exceptions**. It has no access to `ModelState` and is never invoked for automatic validation responses.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing `IProblemDetailsService` (ASP.NET 7+) unifies the shapes — `IExceptionHandler` returns `ProblemDetails` for all exceptions, so Angular's interceptor parses one consistent format. The tradeoff: you lose the ability to include custom fields (like `stackTrace` in dev) without extending `ProblemDetails`.

</details>
