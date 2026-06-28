# Specification Pattern

Encapsulates query logic into reusable objects. Lives in `Application/Specifications/`.

## Base Classes

```csharp
BaseSpecification<T>           // returns T entities
BaseSpecification<T, TResult>  // projects to TResult
```

## Builder Methods

| Method | SQL equivalent |
|---|---|
| `AddCriteria(x => ...)` | WHERE (multiple calls = AND) |
| `AddInclude(x => x.Nav)` | LEFT JOIN |
| `AddOrderByAsc/Descending` | ORDER BY |
| `ApplyPaging(skip, take)` | OFFSET / FETCH |
| `ApplyDistinct()` | DISTINCT |
| `AddSelect(x => ...)` | SELECT projection (`<T,TResult>` only) |

## Examples

```csharp
// Filter + sort + paging
public class ProductSpecification : BaseSpecification<Product>
{
    public ProductSpecification(ProductSpecParams p) : base(x =>
        (string.IsNullOrEmpty(p.Search) || x.Name.ToLower().Contains(p.Search)) &&
        (p.Brands.Count == 0 || p.Brands.Contains(x.Brand)))
    {
        ApplyPaging(p.PageSize * (p.PageIndex - 1), p.PageSize);
        AddOrderByAsc(x => (object)x.Price);
    }
}

// Static factory (named query)
public static OrderSpecification ByEmail(string email) => new(o => o.BuyerEmail == email);
```

## Request flow (how a spec runs end-to-end)

A spec only *describes* a query — a bag of properties (`Criteria`, `Includes`, `OrderByDesc`, `Skip/Take`…). It runs nothing. Three roles:

- **Recipe** (`*Specification`) — builder methods (`AddCriteria`/`AddInclude`/`AddOrderBy…`) *set* properties. No DB.
- **Cook** (`GenericRepository` + `SpecificationEvaluator`) — *read* those properties, build the EF query. Still no DB.
- **Kitchen** (EF Core) — `ToListAsync`/`ToList`/`Count` translate to SQL and execute. ([EF: queries run only when enumerated](https://learn.microsoft.com/en-us/ef/core/querying/how-query-works))

Road map (e.g. dashboard `GetAllAsync(spec)`):

```
Controller → Service → [build spec] → UnitOfWork.Repository → GenericRepository.GetAllAsync
→ ApplySpecification → SpecificationEvaluator.GetQuery → ApplyCommon → ToListAsync → SQL → List<T> → back to Service
```

The spec's properties are read in exactly **one** place — `SpecificationEvaluator.ApplyCommon` + `GetQuery` (the `Criteria` / `Includes` / `OrderBy` / `IsDistinct` / `IsPagingEnabled` `if`-checks). Those `if`s are the only "checking points"; nothing else inspects a spec.

## Rules / Constraints

- Namespace must be `Application.Specifications` — not nested under the class name.
- Never put query logic in controllers or services.
- Cast `decimal` to `(object)` for ordering — never `(double)` (precision loss).

## Common Mistakes

- `(double)` cast on decimal ordering — use `(object)`.
- Spec namespace matching the class name (e.g., `Application.Specifications.ProductSpecification`).
- Passing `skip < 0` or `take <= 0` to `ApplyPaging` — throws `ArgumentOutOfRangeException`.
