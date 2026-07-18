# API Patterns

BaseApiController, controller conventions, and ApiErrorResponse shape.

## BaseApiController

```csharp
[ApiController]
[Route("api/[controller]")]
public class BaseApiController : ControllerBase
```

Two `CreatePagedResult` overloads — wraps results in `Pagination<T>`, returns `200 OK`.

## Controller Conventions

- Inherit `BaseApiController`.
- Inject `IUnitOfWork`. Never inject `IGenericRepository<T>` directly.
- Auth at class level: `[Authorize]`. Role restriction: `[Authorize(Roles = "Admin")]`.
- `[Cache(minutes)]` on GET endpoints. `[InvalidateCache("api/route")]` on mutations — no trailing `"|"`,
  it would skip the by-id keys (`caching.md`).

```csharp
public class ProductsController(IUnitOfWork unitOfWork) : BaseApiController
{
    [Cache(10)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts(...) { ... }

    [InvalidateCache("api/products")]
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(CreateProductDTO productDto) { ... }
}
```

## Validation

DTO validation uses **FluentValidation**, not DataAnnotations.

- Validators live in `Application/Common/Validators/` as `AbstractValidator<TDto>` (one per write DTO; nest with `SetValidator`). Portable, testable, DI-friendly.
- Registered by assembly scan: `services.AddValidatorsFromAssemblyContaining<CreateProductDtoValidator>()` (scoped).
- `ValidationFilter` (global action filter in `API/Filters/`) resolves `IValidator<T>` per action argument, runs it, and returns a **400 `ValidationProblemDetails`** — the same shape `[ApiController]` emits for DataAnnotations, so the Angular error interceptor is unaffected.
- `[ApiController]` does NOT auto-run FluentValidation — the filter is the trigger. Args with no registered validator pass straight through.
- `RegisterDTO` still uses DataAnnotations (validated by `[ApiController]`).
- Test validators with FluentValidation's `TestValidate(...)` (see `Application.Tests`).

## ApiErrorResponse

```csharp
public class ApiErrorResponse(int statusCode, string message, string? details)
```

Returned by `ExceptionMiddleware`. See `backend/error-handling.md`.

## Rate Limiting

Two policies — see `controllers.md` for which endpoints use each.

| Policy | Limit | Applied to |
|---|---|---|
| Global | 200 req/min per IP | All endpoints |
| `strict` | 30 req/min per IP | Identity + Coupons |

## Rules / Constraints

- Controllers reference only Core/Application interfaces — never Infrastructure types.
- Keep controllers thin — no business logic.
- `CreatePagedResult` for all paginated endpoints.
- Infrastructure usings in `Program.cs` only, not `GlobalUsing.cs`.

## Common Mistakes

- Injecting `IGenericRepository<T>` directly instead of going through `IUnitOfWork`.
- Putting business logic in controllers — use a service.
