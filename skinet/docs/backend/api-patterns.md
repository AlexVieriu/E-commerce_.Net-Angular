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
- `[Cache(minutes)]` on GET endpoints. `[InvalidateCache("api/route|")]` on mutations.

```csharp
public class ProductsController(IUnitOfWork unitOfWork) : BaseApiController
{
    [Cache(10)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts(...) { ... }

    [InvalidateCache("api/products|")]
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(Product product) { ... }
}
```

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
