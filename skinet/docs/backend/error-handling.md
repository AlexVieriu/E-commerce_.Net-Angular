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

Always returns `500`. Always sets `Content-Type: application/json`.

## ApiErrorResponse

```csharp
public class ApiErrorResponse(int statusCode, string message, string? details)
```

Used by `ExceptionMiddleware`. Also used directly in controllers via `BadRequest(...)`, `NotFound(...)` etc. — ASP.NET wraps those in its own problem details format automatically via `[ApiController]`.

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
