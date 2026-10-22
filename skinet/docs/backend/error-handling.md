# Error Handling

Global exception handling via middleware. All unhandled exceptions return a consistent JSON shape.

## ExceptionMiddleware

Registered in `Program.cs` as the first middleware — wraps the entire pipeline.

```csharp
app.UseMiddleware<ExceptionMiddleware>();
```

Catches any unhandled exception and returns:
- **Development:** `{ statusCode, message, stackTrace }`
- **Production:** `{ statusCode, message, "Internal Server Error" }`

Returns `500` by default. Maps specific exception types to other status codes (see below). Always sets `Content-Type: application/json`.

## ApiErrorResponse

```csharp
public class ApiErrorResponse(int statusCode, string message, string? details)
```

Used by `ExceptionMiddleware`. Also used directly in controllers via `BadRequest(...)`, `NotFound(...)` etc. — ASP.NET wraps those in its own problem details format automatically via `[ApiController]`.

## Exception → Status Code Mapping

| Exception | Status Code |
|-----------|-------------|
| `PriceChangedException` | 409 Conflict |
| `DbUpdateConcurrencyException` | 409 Conflict |
| `InvalidOperationException` | 400 Bad Request |
| Everything else | 500 Internal Server Error |

`PriceChangedException` lives in `Core/Exceptions/` — thrown by `OrderService` when cart prices differ from DB prices at order creation, and by both services when a coupon has been retired (`OrderService` at order creation, `PaymentService` at intent creation). Every throw site saves the re-priced cart first.

`DbUpdateConcurrencyException` is EF's — raised when a token-guarded `UPDATE` affects 0 rows (`QuantityInStock` is `[ConcurrencyCheck]`). Reaches the middleware only from the **product edit** path; `OrderService` catches its own and converts to `InvalidOperationException` → 400, so checkout tells the buyer "try again" instead of "someone else edited this". Both 409s get the same client message; only the checkout path also reloads the cart, because that recovery
lives in the services that own `POST /orders` and `POST /payments` — see `frontend/interceptors.md`.

⚠️ **Known gap:** the 409 body has no machine-readable type, so the client separates the two conflicts by
*which service made the call*. `PriceChangedException` also saves the re-priced cart before throwing, then
doesn't return it — the client fetches it again. Both tracked in *Pull-in candidates* (`docs/archive/execution-plan.md`),
to ride with the next change in this middleware.

## Error Flow

```
Unhandled exception → ExceptionMiddleware → ApiErrorResponse (JSON) → client
401/403/404/400     → [ApiController] automatic response              → client
```

## BuggyController

Dev-only controller at `api/buggy/*` for testing error responses.
Excluded from routing in non-Development via `RemoveControllerConvention` in `Program.cs`.

## Rules / Constraints

- Never catch and swallow exceptions silently — let them bubble to `ExceptionMiddleware`.
- Never return raw exception messages to the client in production.
- `ExceptionMiddleware` must stay the first middleware registered.

## Common Mistakes

- Registering `ExceptionMiddleware` after other middleware — exceptions thrown before it won't be caught.
