## SECTION E — ERROR HANDLING & MIDDLEWARE

### MEDIUM

**E1.** You have a custom `ExceptionMiddleware` that catches exceptions and maps them to HTTP status codes. An `InvalidOperationException` maps to 400, everything else to 500. A developer throws `ArgumentNullException` from a service. What status code does the client get? Is this correct?

<details><summary>Answer</summary>

The client gets **500**. `ArgumentNullException` inherits from `ArgumentException → SystemException → Exception`, not from `InvalidOperationException`. It falls through to the `catch (Exception)` block.

Is it correct? **It depends — but probably not.** `ArgumentNullException` usually indicates a programming mistake (null passed where it shouldn't be), which is an internal bug → 500 is defensible. But if the null came from invalid user input, 400 would be more appropriate. The real fix: throw a domain-specific exception (`InvalidOrderException`, `CartNotFoundException`) and map those deliberately rather than relying on .NET base types.
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

---
