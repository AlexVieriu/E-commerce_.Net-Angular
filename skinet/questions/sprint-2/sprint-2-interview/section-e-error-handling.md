## SECTION E — ERROR HANDLING & MIDDLEWARE

### MEDIUM

**E1.** You have a custom `ExceptionMiddleware` that catches exceptions and maps them to HTTP status codes. An `InvalidOperationException` maps to 400, everything else to 500. A developer throws `ArgumentNullException` from a service. What status code does the client get? Is this correct?

<details><summary>Answer</summary>

The client gets **500**. `ArgumentNullException` inherits from `ArgumentException → SystemException → Exception`, not from `InvalidOperationException`. It falls through to the `catch (Exception)` block.

Is it correct? **It depends — but probably not.** `ArgumentNullException` usually indicates a programming mistake (null passed where it shouldn't be), which is an internal bug → 500 is defensible. But if the null came from invalid user input, 400 would be more appropriate. The real fix: throw a domain-specific exception (`InvalidOrderException`, `CartNotFoundException`) and map those deliberately rather than relying on .NET base types.
</details>

<details><summary>Deep Dive</summary>

**The exception hierarchy trap**

```
Exception
├── SystemException
│   ├── ArgumentException
│   │   └── ArgumentNullException   ← lands here, NOT InvalidOperationException
│   └── InvalidOperationException
└── ApplicationException
```

`ArgumentNullException` and `InvalidOperationException` are siblings, not parent/child. Your `catch (InvalidOperationException)` block never fires.

**Why mapping .NET system exceptions is fragile**

Different third-party libraries throw different exception types for similar problems:
- EF Core throws `DbUpdateException` for constraint violations
- StackExchange.Redis throws `RedisConnectionException` for connection failures
- `HttpClient` throws `HttpRequestException` for 4xx/5xx from downstream

Mapping `InvalidOperationException` globally catches ANY exception with that type — including ones thrown by libraries for internal reasons. You'll accidentally return 400 for things that should be 500.

**The correct pattern: domain exceptions**

```csharp
// Domain exceptions — your code owns these
public class InsufficientStockException : Exception
{
    public InsufficientStockException(int productId, int requested, int available)
        : base($"Product {productId}: requested {requested}, available {available}") { }
}

public class CartNotFoundException : Exception
{
    public CartNotFoundException(string cartId)
        : base($"Cart '{cartId}' not found") { }
}

// Middleware maps your exceptions, not framework ones
catch (InsufficientStockException ex)  { statusCode = 400; message = ex.Message; }
catch (CartNotFoundException ex)       { statusCode = 404; message = ex.Message; }
catch (DbUpdateConcurrencyException)   { statusCode = 409; message = "Conflict — retry"; }
catch (Exception)                      { statusCode = 500; message = "Internal error"; }
```

Now `ArgumentNullException` stays as 500 (it IS an internal bug), and your business failures become explicit 400s/409s.

**Problem Details (RFC 7807) — the standard format**

```csharp
// Instead of { "error": "..." }, return the standard format:
var problem = new ProblemDetails
{
    Status = statusCode,
    Title = "Insufficient stock",
    Detail = ex.Message,
    Type = "https://yourapi.com/errors/insufficient-stock",
    Extensions = { ["traceId"] = Activity.Current?.Id }
};
await context.Response.WriteAsJsonAsync(problem);
```

Angular's error interceptor can parse `problem.type` to route errors to specific handlers rather than treating all 400s identically.
</details>

---

**E2.** What does this middleware do? Trace the flow for a request that throws `InvalidOperationException("Insufficient stock")`.

```csharp
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (InvalidOperationException ex)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
        }
    }
}
```

<details><summary>Answer</summary>

**What it does:** wraps every subsequent middleware/controller in a try-catch. Maps `InvalidOperationException` to 400 Bad Request and everything else to 500.

**Trace for `InvalidOperationException("Insufficient stock")`:**

1. Request arrives → `InvokeAsync` called → `await _next(context)` hands control to the next middleware/controller
2. Controller calls `OrderService.CreateOrderAsync()`
3. `OrderService` detects stock = 0 → `throw new InvalidOperationException("Insufficient stock")`
4. Exception bubbles up through the pipeline (controller → routing → middleware chain)
5. `ExceptionMiddleware` catches it in `catch (InvalidOperationException ex)`
6. Sets `StatusCode = 400`
7. Writes `{"error":"Insufficient stock"}` as JSON to the response body
8. Client receives HTTP 400 with that JSON body
</details>

<details><summary>Deep Dive</summary>

**Middleware pipeline — the reverse stack**

```
Request →  [ExceptionMiddleware]  →  [Auth]  →  [Routing]  →  [Controller]
Response ← [ExceptionMiddleware]  ←  [Auth]  ←  [Routing]  ←  [Controller]
```

`await _next(context)` is the "pass forward" call. Everything AFTER it in the method body runs on the way BACK (response path). The try-catch wraps the entire downstream pipeline.

**What this middleware does NOT do**

- Logging — the `ex` variable is swallowed in the 500 branch. You'll never know what happened.
- `ex` in the `InvalidOperationException` branch isn't logged either. If you have 10 different InvalidOperationExceptions, you can't distinguish them in monitoring.

Fix — always log before responding:

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Unhandled exception for {Method} {Path}",
        context.Request.Method, context.Request.Path);
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
}
```

**Response already started**

If any downstream middleware starts writing the response body before throwing (e.g., a streaming response), `context.Response.StatusCode = 400` has no effect — headers are already sent. Guard against this:

```csharp
if (context.Response.HasStarted)
{
    _logger.LogWarning("Response already started — cannot write error response");
    return;
}
context.Response.StatusCode = 400;
```

**Order matters: `ExceptionMiddleware` must be registered first (outermost)**

```csharp
app.UseMiddleware<ExceptionMiddleware>(); // FIRST — wraps everything
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

If you register it after `UseAuthentication`, auth exceptions won't be caught.
</details>

---

**E3.** Trace the full error chain when a concurrency conflict happens during checkout:
```
[EF Core] → [OrderService] → [ExceptionMiddleware] → [HTTP Response] → [error.interceptor] → [user sees ???]
```

<details><summary>Answer</summary>

1. **EF Core**: `SaveChangesAsync()` sends `UPDATE Products SET Stock = 0 WHERE Id = 1 AND Stock = 1` → 0 rows affected → throws `DbUpdateConcurrencyException`
2. **OrderService**: does not catch `DbUpdateConcurrencyException` → exception propagates
3. **ExceptionMiddleware**: not `InvalidOperationException` → falls to `catch (Exception)` → sets status 500, writes `{"error":"Internal server error"}`
4. **HTTP Response**: `500 Internal Server Error` with generic message
5. **Angular error.interceptor**: catches 500 → shows generic snackbar: "Something went wrong"
6. **User sees**: "Something went wrong" — no useful information, no way to know it was a stock conflict

**What should happen instead**: catch `DbUpdateConcurrencyException` explicitly → return 409 Conflict → Angular shows "Item sold out — please refresh your cart"
</details>

<details><summary>Deep Dive</summary>

**Fixing the chain — each layer's responsibility**

```csharp
// ExceptionMiddleware — add 409 mapping
catch (DbUpdateConcurrencyException)
{
    context.Response.StatusCode = 409;
    await context.Response.WriteAsJsonAsync(new
    {
        error = "A conflict occurred. Please refresh and try again.",
        type = "ConcurrencyConflict"
    });
}
```

```typescript
// Angular error.interceptor — handle 409 specifically
if (error.status === 409) {
  inject(SnackbarService).error('Item sold out — please refresh your cart');
  inject(Router).navigateByUrl('/cart');
}
```

**Why the full chain matters**

Each layer should do ONE thing:
- EF Core: detect the conflict and signal it (throws `DbUpdateConcurrencyException`)
- OrderService: optionally retry (with Polly), or rethrow a domain exception
- ExceptionMiddleware: map to HTTP status code
- Angular interceptor: translate HTTP status to UX action

When any layer swallows or mis-translates, the user gets a generic error that looks like a bug, not a business condition.

**The retry decision**

`DbUpdateConcurrencyException` is sometimes transient (two simultaneous requests, one wins). OrderService can retry once before giving up:

```csharp
try
{
    await _context.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException ex)
{
    // Reload the entity and check if stock is still available
    await ex.Entries.Single().ReloadAsync();
    var product = (Product)ex.Entries.Single().Entity;
    if (product.QuantityInStock >= requestedQty)
        await _context.SaveChangesAsync(); // retry once
    else
        throw new InsufficientStockException(product.Id, requestedQty, product.QuantityInStock);
}
```

**Angular interceptor — the full picture**

```typescript
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError(error => {
      switch (error.status) {
        case 400: inject(SnackbarService).error(error.error?.message ?? 'Bad request'); break;
        case 401: inject(Router).navigateByUrl('/account/login'); break;
        case 404: inject(Router).navigateByUrl('/not-found'); break;
        case 409: inject(SnackbarService).error('Conflict — please refresh'); break;
        case 500: inject(Router).navigateByUrl('/server-error'); break;
      }
      return throwError(() => error);
    })
  );
};
```
</details>

---

### HARD

**E4.** Your exception middleware catches `DbUpdateConcurrencyException` and returns 409 Conflict. But in some cases, the caller should retry automatically (background job), and in others, the user should be told to refresh (API call). How do you design the error response to support both consumers?

<details><summary>Answer</summary>

Return a structured error response with machine-readable fields — not just a message string.

```json
{
  "type": "ConcurrencyConflict",
  "title": "Resource was modified by another request",
  "status": 409,
  "retryable": true,
  "retryAfterMs": 500,
  "detail": "Product stock was updated concurrently. Reload and retry."
}
```

- **Background job** reads `retryable: true` → waits `retryAfterMs` → retries automatically
- **Angular** reads `type: "ConcurrencyConflict"` → shows user-facing message with a "Refresh Cart" button
- Both consumers use the same response, interpreted differently by their error handling logic

Use **Problem Details (RFC 7807)** as the envelope — it's the standard, Angular's `HttpClient` handles it well, and ASP.NET Core has built-in support.
</details>

<details><summary>Deep Dive</summary>

**Problem Details with custom extensions**

```csharp
// In ExceptionMiddleware
catch (DbUpdateConcurrencyException ex)
{
    var problem = new ProblemDetails
    {
        Status = 409,
        Title = "Concurrency conflict",
        Type = "https://yourapi.com/errors/concurrency-conflict",
        Detail = "The resource was modified by another request. Please retry.",
    };
    problem.Extensions["retryable"] = true;
    problem.Extensions["retryAfterMs"] = 500;
    problem.Extensions["traceId"] = Activity.Current?.Id;

    context.Response.StatusCode = 409;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(problem);
}
```

**Background job consumer**

```csharp
public async Task ProcessOrderJobAsync(OrderRequest request)
{
    const int maxRetries = 3;
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/orders", request);
        if (response.IsSuccessStatusCode) return;

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        var retryable = problem?.Extensions.GetValueOrDefault("retryable") as bool? ?? false;

        if (!retryable || attempt == maxRetries - 1)
            throw new Exception($"Order failed after {attempt + 1} attempts: {problem?.Detail}");

        var retryAfter = problem?.Extensions.GetValueOrDefault("retryAfterMs") as int? ?? 500;
        await Task.Delay(retryAfter + Random.Shared.Next(0, 200)); // jitter
    }
}
```

**Angular consumer**

```typescript
// error.interceptor.ts
if (error.status === 409) {
  const problem = error.error as ProblemDetails;
  if (problem.type === 'https://yourapi.com/errors/concurrency-conflict') {
    inject(SnackbarService).error('Prices or stock changed. Please refresh your cart.');
    inject(Router).navigateByUrl('/cart');
  }
}
```

**Why `type` as a URI matters**

The `type` URI is the machine-readable discriminator. Multiple 409 responses can exist (stock conflict, price conflict, version conflict) — each gets a unique `type` URI. Consumers switch on `type`, not on the `detail` string (which is human-readable and can change).

**Alternative: separate endpoints for background vs UI**

If the retry logic is complex, route background jobs to a dedicated endpoint (`/api/internal/orders`) that accepts different retry semantics and returns different response shapes. Only do this if the shared endpoint becomes too noisy with branching logic.
</details>

---
