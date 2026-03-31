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

## Rules / Constraints

- Namespace must be `Application.Specifications` — not nested under the class name.
- Never put query logic in controllers or services.
- Cast `decimal` to `(object)` for ordering — never `(double)` (precision loss).

## Common Mistakes

- `(double)` cast on decimal ordering — use `(object)`.
- Spec namespace matching the class name (e.g., `Application.Specifications.ProductSpecification`).
- Passing `skip < 0` or `take <= 0` to `ApplyPaging` — throws `ArgumentOutOfRangeException`.
