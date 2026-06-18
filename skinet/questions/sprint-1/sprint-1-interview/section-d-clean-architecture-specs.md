## SECTION D — CLEAN ARCHITECTURE & SPECIFICATION PATTERN

> **Source:** Architecture docs — 4-layer Clean Architecture, GenericRepository, UnitOfWork, Specification pattern, SQLite/SQL Server dual-provider setup.

---

### EASY

**D1.** [HIGH] What are the 4 layers of Clean Architecture and what is the dependency direction rule?

<details>
<summary>The 5% Answer</summary>

| Layer | Location | Responsibilities | Dependencies |
|---|---|---|---|
| **Core** | `src/Core/` | Entities, repository interfaces, domain exceptions | None |
| **Application** | `src/Application/` | Service interfaces, DTOs, specs, pagination | Core only |
| **Infrastructure** | `src/Infrastructure/` | EF Core, Redis, Stripe implementations | Core + Application |
| **API** | `src/API/` | Controllers, middleware, DI wiring, SignalR | All layers |

**Dependency direction: always inward.** API → Application → Core. Infrastructure → Core + Application. Core has zero outward dependencies.

**Why this matters:**
- Core compiles standalone with no EF Core, no Redis, no HTTP dependencies — it's pure domain
- Swapping Infrastructure (e.g., EF Core → Dapper) only requires changing Infrastructure, never Core or Application
- Controllers test against service interfaces (Application layer), never against concrete implementations

- **Red rule:** no outer layer may import an inner layer — Infrastructure → Core is valid; Core → API is a circular dependency and a compile error.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Infrastructure depends on API for logging or config." This creates a circular dependency — API depends on Infrastructure (via DI) and Infrastructure depends on API. Clean Architecture breaks. Logging goes through `ILogger<T>` (injected anywhere), config through `IOptions<T>` (both defined in Microsoft.Extensions — not in any of your layers).

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning the **Dependency Inversion Principle** (the 'D' in SOLID) is the formal basis: high-level modules (Core) define abstractions (`IGenericRepository<T>`); low-level modules (Infrastructure) implement them (`GenericRepository<T>`). The DI container wires concrete → abstract at runtime. Core never references EF Core; Infrastructure implements Core's contracts.

</details>

<details>
<summary>Explanation</summary>

```
┌─────────────┐     ┌───────────────────┐
│     API     │────▶│   Application     │
│  (Web/HTTP) │     │ (Services/UseCases)│
└──────┬──────┘     └────────┬──────────┘
       │                     │
       │            ┌────────▼──────────┐
       └───────────▶│      Core         │
                    │(Entities/Interfaces)
                    └────────▲──────────┘
                             │
                    ┌────────┴──────────┐
                    │  Infrastructure   │
                    │ (DB/Stripe/Email) │
                    └───────────────────┘
  ──▶ = "depends on"
```

| Layer | Knows about | Blind to |
|---|---|---|
| **Core** | Nothing | Everyone |
| **Application** | Core | API, Infra |
| **Infrastructure** | Core + Application | API |
| **API** | Application | Infrastructure directly |

**Concrete flow — placing an order:**
`Controller` → `IOrderService` (Application) → `IOrderRepository` (Core interface) → `OrderRepository` (Infrastructure implements it).

The controller never touches `OrderRepository` directly. Core defines the contract; Infrastructure fulfills it; the DI container wires them at startup.

</details>

---

**D2.** [HIGH] What is the Specification pattern and why does this project use it instead of writing query methods on the repository?

<details>
<summary>The 5% Answer</summary>

A **specification** encapsulates a query's WHERE, ORDER BY, INCLUDE, SKIP/TAKE logic into a reusable C# object. The repository executes specs against any EF provider.

```csharp
// Custom repo method — works, but one method per filter combo
public Task<List<Product>> GetByBrandAsync(string brand) => ...
public Task<List<Product>> GetByBrandAndSearchAsync(string brand, string search) => ...
public Task<List<Product>> GetByBrandAndSearchSortedAsync(string brand, string search, string sort) => ...
// 3 filters = already 3 methods. 5 filters = up to 25 methods.

// With spec — all filter combinations in one class
var spec = new ProductSpecification(new ProductSpecParams { Search = search, Brands = brands, Sort = sort });
var products = await _repo.GetAllAsync(spec);
```

**Why specs:**
1. **Testable in isolation** — `ProductSpecification` is a plain C# object; `new()` it in a test with no EF context needed. A custom repo method requires mocking the entire repository to test the same filter logic.
2. **Single source of truth** — one spec class, not 5 copies of the same LINQ across different services
3. **Repository stays generic** — `IGenericRepository<T>` has ~6 methods covering all entities. Without specs, you'd need `GetProductsByBrand`, `GetProductsByBrandAndSearch`, etc. — combinatorial explosion.
4. **Projection support** — `BaseSpecification<T, TResult>` centralises the `Select` projection inside the spec, keeping it testable and reusable. Custom repo methods can also project to DTOs, but the projection is buried inside the method and can't be composed or reused across different queries.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Add `GetProductsByBrand(string brand)` to `IProductRepository`." That approach requires a new repository method for every query variation. N entity types × M query variations = an unmanageable interface that changes with every business requirement.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that specifications can be **composed** — `ProductSpecification` can accept multiple optional filters in one constructor. The alternative (separate methods per filter combination) grows exponentially and creates N×M method counts for N filters and M sort options. Specs keep the combinatorial complexity in one place.

</details>

<details>
<summary>Explanation</summary>

### Big Picture Flow

User opens `/products?brand=Nike&sort=priceAsc&page=2`:

```
HTTP GET /products?brand=Nike&sort=priceAsc&page=2
         │
         ▼
┌─────────────────────────────────┐
│         ProductsController      │  ← builds ProductSpecParams only
│  var spec = new                 │
│    ProductSpecification(params) │
│  _repo.GetAllAsync(spec)        │
└─────────────┬───────────────────┘
              │
              ▼
┌─────────────────────────────────┐
│       ProductSpecification      │  ← ALL query logic lives here
│  AddCriteria(x =>               │
│    x.Brand == params.Brand &&   │
│    x.Name.Contains(params.Search│
│  )                              │
│  AddOrderByAsc(x => x.Price)    │
│  AddPaging(skip, take)          │
│  AddInclude(x => x.ProductType) │
└─────────────┬───────────────────┘
              │  (plain C# object — no DB yet)
              ▼
┌─────────────────────────────────┐
│     IGenericRepository<T>       │  ← same 6 methods for ALL entities
│  GetAllAsync(spec)              │
└─────────────┬───────────────────┘
              │
              ▼
┌─────────────────────────────────┐
│      SpecificationEvaluator     │  ← translates spec → IQueryable chain
│  query = query.Where(criteria)  │
│  query = query.Include(...)     │
│  query = query.OrderBy(...)     │
│  query = query.Skip().Take()    │
└─────────────┬───────────────────┘
              │
              ▼
┌─────────────────────────────────┐
│     EF Core → SQL Server        │
│  SELECT p.*, pt.*               │
│  FROM Products p                │
│  JOIN ProductTypes pt ON ...    │
│  WHERE p.Brand = 'Nike'         │
│  ORDER BY p.Price ASC           │
│  OFFSET 20 ROWS FETCH NEXT 20   │
└─────────────────────────────────┘
```

---

### When to use each approach

| Approach | Use when |
|---|---|
| **Query in service** | Never in production. Prototypes only. |
| **Custom repo methods** | Small project, < 5 entities, queries are few and stable. Simple CRUD. |
| **Specification (this project)** | Medium+ project with many filter combinations. Queries need testing in isolation. |
| **CQRS + MediatR** | Large team, complex domain, reads/writes scale independently, or event sourcing. Overkill for 90% of projects. |

---

### When NOT to use specs

```csharp
// Overkill — just use FindAsync
var product = await _repo.FindAsync(id);

// Overkill — one-time query, never reused
var count = await _context.Products.CountAsync();
```

Specs are worth it when the query has **optional filters**, is **reused across services**, or needs **testing in isolation**.

---

**Key mental model:** a spec is a query stored as a value. Build it in the service layer, hand it to the generic repo, and the evaluator applies it to EF's `IQueryable<T>`. The DB never sees the spec — only the resulting SQL.

</details>

---

### MEDIUM

**D3.** [HIGH] [OUT-OF-SCOPE] This spec crashes on SQL Server but works fine in SQLite. Find the bug:

```csharp
public class ProductByPriceSpec : BaseSpecification<Product>
{
    public ProductByPriceSpec()
    {
        AddOrderByAsc(x => (double)x.Price);
    }
}
```

<details>
<summary>The 5% Answer</summary>

**Bug:** casting `decimal` to `double` inside a LINQ expression that EF Core translates to SQL.

**What happens per provider:**
- **SQLite (dev):** stores decimals as `REAL` (float). EF's SQLite provider handles the cast silently. Tests pass, sort order appears correct.
- **SQL Server (prod):** EF generates `CAST(Price AS float)`. SQL Server executes the cast: `9.995` becomes `9.994999...` in floating point. Sort order can change — products with prices that round differently as floats swap positions.

**Fix:**
```csharp
AddOrderByAsc(x => (object)x.Price);
```

`(object)` satisfies the `Expression<Func<T, object>>` signature without any type conversion. EF Core translates this to `ORDER BY Price` with no `CAST` — the decimal column sorts precisely.
- **Note:** the project's `specifications.md` mandates this pattern. Always verify the generated SQL via EF Core query logging (`Microsoft.EntityFrameworkCore.Database.Command` at `Information`) when switching providers.

**Production consequence:** product price ordering silently changes in production for items with prices like `$9.995` and `$10.004`. Subtle, hard to reproduce, causes customer complaints about "wrong" sort order.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Cast to `decimal` explicitly instead." `decimal` is already the column type — an explicit `(decimal)` cast is a no-op in C# but still generates a `CAST` in some providers. `(object)` is the correct pattern precisely because it avoids any SQL-level cast.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning this is explicitly documented in the project's Common Mistakes. The broader lesson: any cast or method call inside a LINQ `Expression<...>` is translated to SQL — C# semantics don't apply. Always verify expression translation on the production DB provider, not just SQLite.

</details>

<details>
<summary>Explanation</summary>

### How LINQ works — two modes

LINQ has two completely different modes depending on the target type:

```
Func<Product, bool>              f = p => p.Price > 10;
// ↑ compiled delegate — black box, runs in C# memory
// EF cannot look inside — already machine code

Expression<Func<Product, bool>> e = p => p.Price > 10;
// ↑ data structure — a tree describing what the code would do
// EF can walk the tree and translate each node to SQL
```

Same lambda syntax — the C# compiler picks which one based on the target type.

**The expression tree for `p => p.Price > 10`:**
```
BinaryExpression (GreaterThan)
  ├── MemberExpression  →  p.Price   →  SQL: Price
  └── ConstantExpression →  10       →  SQL: 10

Result: WHERE Price > 10
```

**The expression tree for `p => (double)p.Price`:**
```
UnaryExpression (Convert decimal → double)
  └── MemberExpression  →  p.Price   →  SQL: Price

EF sees: numeric type conversion decimal → double
Result: CAST(Price AS float)
```

**The expression tree for `p => (object)p.Price`:**
```
UnaryExpression (Convert decimal → object)  ← boxing
  └── MemberExpression  →  p.Price   →  SQL: Price

EF sees: boxing — no SQL equivalent, drop the node
Result: Price
```

This is why `.Compile()` in D9 was a bug — it converted the expression tree into a compiled delegate, making it a black box EF could no longer translate.

---

### Why the cast direction matters

`x.Price` is `decimal` in C# and stored as `decimal(18,2)` in SQL Server. When you write `(double)x.Price` in a LINQ expression, you are telling EF: "I want this value as a `double`." EF reads the expression tree, sees the source type (`decimal`) and the target type (`double`), looks up its type map, and emits a SQL-level cast:

```
C# Entity            DB Column (SQL Server)      LINQ Expression
decimal Price   →    decimal(18,2) Price    →    (double)x.Price
                                                  ↓
                                            EF sees:
                                            source = decimal
                                            target = double → SQL float
                                                  ↓
                                       ORDER BY CAST(Price AS float)
                                       -- sorts on the converted float
                                       -- not the original decimal
```

You never wrote `CAST` — EF generated it because the expression tree contained a numeric type conversion.

### Why `(object)` fixes it

Boxing (`decimal → object`) has no SQL equivalent. EF cannot map it to a `CAST`, so it drops the conversion node entirely and uses the column's native type:

```
(object)x.Price
↓
EF sees: boxing — no SQL type conversion possible
↓
ORDER BY Price    -- sorts on the exact decimal column, no CAST
```

### Why SQLite hides the bug

Your `SqliteStoreContext` maps all `decimal` columns to `REAL` (float) via `HasColumnType("REAL")`. SQLite is already storing `$9.995` as a float. Casting float → float is a no-op — same approximate value in, same out. Sort order doesn't change in dev.

SQL Server stores `decimal` exactly. The `CAST(Price AS float)` does a real conversion with precision loss. For prices near a binary representability boundary, the float approximation shifts the value slightly — `$9.995` becomes `9.994999999999999...` — and two prices that were correctly ordered as decimals can swap positions as floats.

### Why it's hard to reproduce

- No exception, no 500 error, no log entry — just silently wrong sort order
- Only affects prices near a binary representability boundary
- Looks correct in every environment you can debug in (SQLite dev)
- A customer complains, you check locally, everything looks fine

</details>

---

**D4.** [HIGH] [OUT-OF-SCOPE] Why does this project use SQLite in development and SQL Server in production? How is `StoreContext` registered so service code never changes between environments?

<details>
<summary>The 5% Answer</summary>

**Why two providers:**
- **SQLite:** zero-install, file-based. Any developer can run the project without installing SQL Server, Azure SQL, or Docker. Fast local startup.
- **SQL Server:** full ANSI compliance, proper `nvarchar(450)` indexes, decimal precision matching production, transaction semantics that match what users experience.

**DI registration — factory pattern (dev):**
```csharp
// Program.cs — Development
services.AddDbContext<SqliteStoreContext>(o =>
    o.UseSqlite(config.GetConnectionString("DefaultConnection")));
services.AddScoped<StoreContext>(p =>
    p.GetRequiredService<SqliteStoreContext>());
```

**Production:**
```csharp
// Program.cs — Production
services.AddDbContext<SqlServerStoreContext>(o =>
    o.UseSqlServer(config.GetConnectionString("DefaultConnection")));
services.AddScoped<StoreContext>(p =>
    p.GetRequiredService<SqlServerStoreContext>());
```

All services inject `StoreContext` — the abstract base. In dev, the DI container resolves `SqliteStoreContext`; in prod, `SqlServerStoreContext`. Service code is identical in both environments. The provider detail is entirely in `Program.cs`.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Inject `SqliteStoreContext` in services during development." Services would need `#if DEBUG` blocks or environment checks everywhere. Impossible to test against the prod provider without code changes.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that despite SQLite working for dev, **integration tests should run against SQL Server** (via Testcontainers or SQL Server LocalDB) to catch provider-specific bugs like the `decimal → double` cast, `nvarchar(max)` indexing, and unique constraint enforcement differences. SQLite dev speeds up the inner loop; SQL Server CI catches production divergences.

</details>

---

**D5.** [MEDIUM] You add `global using Application.Specifications` to `GlobalUsings.cs`. Three months later a teammate complains their new `ShippingSpecification` class isn't found anywhere in the project — but all your existing specs work fine. Same namespace, same folder. What are the three things you check first?

<details>
<summary>The 5% Answer</summary>

**Check 1 — Is the namespace in the file correct?**
```csharp
// ShippingSpecification.cs
namespace Application.Specifications;   // ✅ matches the global using
namespace Application.Specifications.Shipping;  // ❌ sub-namespace, not covered
```
The most common mistake: the IDE auto-generates a nested namespace based on the subfolder. `global using Application.Specifications` only covers that exact namespace — not children.

**Check 2 — Is `GlobalUsings.cs` in the right project?**
`global using` is scoped to the project it lives in. If the teammate is writing specs in a different project (e.g., they added `ShippingSpecification` to `Infrastructure` instead of `Application`), the `global using` in `Application` doesn't cover it.

**Check 3 — Is the file included in the build?**
Check the `.csproj` for explicit `<Compile Remove="..." />` entries, or verify the file wasn't accidentally created outside the project folder. A file on disk that isn't part of the project compiles to nothing — no error, just silently missing.

**The rule:** one `global using` per namespace covers every class in that namespace forever — you never add per-class usings. If a class isn't found, the problem is always the namespace declaration inside the file, the project scope, or the build inclusion — never the `global using` itself.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Add `global using Application.Specifications.Shipping` for the new class." That treats `global using` like a per-class import (Python-style). One namespace `global using` covers all classes inside it — adding per-class or per-subfolder usings bloats `GlobalUsings.cs` and signals a misunderstanding of how C# namespaces work.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Knowing that `global using` generates no "unused using" warning even if the namespace is wrong — it partially resolves and the error only surfaces when a type from that namespace is actually referenced. Mentioning that Roslyn analysers (ReSharper, or `dotnet_diagnostic` rules) can surface "namespace not found" warnings earlier than compile time.

</details>

---

### HARD

**D6.** [HIGH] [OUT-OF-SCOPE] The `UnitOfWork` caches `GenericRepository<T>` instances in a `ConcurrentDictionary`. What problem does this solve, and what subtle bug does it introduce if `UnitOfWork` is accidentally registered as a Singleton?

<details>
<summary>The 5% Answer</summary>

**Problem it solves:**
Multiple calls to `unitOfWork.Repository<Product>()` within a single HTTP request return the **same repository instance** — same underlying `DbContext`. This ensures:
1. All tracked entities across repositories share one change tracker
2. `unitOfWork.Complete()` (`SaveChangesAsync`) atomically commits all pending changes from all repositories
3. No "entity already tracked by another context" `InvalidOperationException`

**The ConcurrentDictionary pattern:**
```csharp
private ConcurrentDictionary<string, object> _repos = new();

public IGenericRepository<T> Repository<T>() where T : BaseEntity
    => (IGenericRepository<T>)_repos.GetOrAdd(typeof(T).Name,
        _ => new GenericRepository<T>(_context));
```

**Singleton bug:**
`DbContext` is **not thread-safe**. If `UnitOfWork` is `Singleton`, all concurrent HTTP requests share one `DbContext` instance:
- Request A reads User #1 → tracked
- Request B reads User #1 simultaneously → `InvalidOperationException: "An item with the same key has already been added"`
- Or: Request A's pending `Order` gets saved with Request B's `Product` update — cross-request data corruption

**Correct lifetime:** `AddScoped<IUnitOfWork, UnitOfWork>()` — one `UnitOfWork` (and one `DbContext`) per HTTP request.

**Production consequence:** singleton `DbContext` causes intermittent 500 errors under concurrent load — impossible to reproduce under single-threaded testing, appears only in production or load tests.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Register as Singleton for performance — creating a new context is expensive." `DbContext` creation is cheap (it's just object allocation, not a DB connection). The connection pool manages actual SQL connections. Singleton DbContext is a data corruption bug, not a performance optimisation.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `ConcurrentDictionary` is used because `UnitOfWork` could theoretically be accessed from multiple threads in async code paths (e.g., background services using `IServiceScopeFactory`). Within a web request, there's typically only one thread, but defensive thread-safety in a dictionary that's cheap to make thread-safe is a reasonable choice.

</details>

<details>
<summary>Explanation</summary>

### What the ConcurrentDictionary actually holds

```
ConcurrentDictionary<string, object>

KEY (string)        VALUE (object — common base type to hold any GenericRepository<T>)
────────────────────────────────────────────────────────
"Product"        →  GenericRepository<Product>        { _context = [0xA1B2] }
"Order"          →  GenericRepository<Order>          { _context = [0xA1B2] }
"Coupon"         →  GenericRepository<Coupon>         { _context = [0xA1B2] }
"DeliveryMethod" →  GenericRepository<DeliveryMethod> { _context = [0xA1B2] }
```

The dictionary stores **repo instances only** — not entities. Entities live in the DbContext:

```
DbContext [0xA1B2]
  └── ChangeTracker
        ├── Product #3  (Modified)   ← Stock changed
        ├── Product #7  (Modified)   ← Stock changed
        └── Order       (Added)      ← new order
```

`[0xA1B2]` is just a memory address showing all repos point to the **same** context — not part of the dictionary. The dictionary is purely `"TypeName" → RepoInstance`.

---

### Real checkout flow — 4 repos, 1 transaction

```csharp
// HTTP POST /checkout — DI creates StoreContext [0xA1B2] → UnitOfWork(_context)

var productRepo        = _unitOfWork.Repository<Product>();        // dict: { Product → Repo[0xA1B2] }
var orderRepo          = _unitOfWork.Repository<Order>();          // dict: { Product, Order → Repo[0xA1B2] }
var couponRepo         = _unitOfWork.Repository<Coupon>();         // dict: { Product, Order, Coupon → ... }
var deliveryMethodRepo = _unitOfWork.Repository<DeliveryMethod>(); // dict: { ..., DeliveryMethod → ... }
// All 4 repos share context [0xA1B2] — one ChangeTracker

var basket         = await _basketService.GetBasketAsync(basketId); // Redis — no SQL
var deliveryMethod = await deliveryMethodRepo.GetByIdAsync(id);     // SELECT DeliveryMethods
var coupon         = await couponRepo.GetByIdAsync(basket.CouponId); // SELECT Coupons

foreach (var item in basket.Items)
{
    var product = await productRepo.GetByIdAsync(item.ProductId); // SELECT Products
    product.Stock -= item.Quantity; // ChangeTracker: Product (Modified) — RAM only
}

orderRepo.Add(new Order { ... }); // ChangeTracker: Order (Added) — RAM only

// Second call to Repository<Product>() in the loop:
// key "Product" already IN dict → same Repo[0xA1B2] returned, no new instance

await _unitOfWork.Complete(); // → SaveChangesAsync()
// BEGIN TRANSACTION
//   UPDATE Products SET Stock = 8  WHERE Id = 3
//   UPDATE Products SET Stock = 14 WHERE Id = 7
//   INSERT INTO Orders   VALUES (...)
//   INSERT INTO OrderItems VALUES (...)   ← EF cascades automatically
// COMMIT  ← all or nothing
```

---

### Why shared context matters — without ConcurrentDictionary

```
WITH shared context (correct)         WITHOUT (new repo each call)
──────────────────────────────        ──────────────────────────────
productRepo  → context [0xA1B2]       productRepo → context [0xA1B2]
orderRepo    → context [0xA1B2]       orderRepo   → context [0xC3D4]  ← different!

Complete() → one SaveChangesAsync()   Complete() → saves [0xA1B2] only
  Products ✓                            Products saved ✓
  Orders   ✓  ← same ChangeTracker     Orders   ✗  ← on a different context
  atomic COMMIT                         stock decremented, no order record
```

---

### Singleton bug

```
CORRECT (Scoped)                      WRONG (Singleton)
────────────────────                  ──────────────────────────────
Request A → UnitOfWork A              Request A ──┐
              └── context A                        ├──► ONE UnitOfWork
Request B → UnitOfWork B              Request B ──┘      └── ONE context
              └── context B                                     │
                                                     concurrent ChangeTracker writes
                                                          ↓
                                               InvalidOperationException
                                               or cross-request data corruption
```

</details>

---

**D7.** [HIGH] A spec passes `PageIndex = 0` to `ApplyPaging`. Describe the exact failure in SQL Server vs SQLite and write the defensive fix.

<details>
<summary>The 5% Answer</summary>

**How `Skip` is calculated:**
```csharp
var skip = pageSize * (pageIndex - 1);
// pageIndex = 0 → skip = pageSize * (0 - 1) = pageSize * -1 → NEGATIVE
```

**SQL Server:**
EF generates `OFFSET -N ROWS FETCH NEXT M ROWS ONLY`. SQL Server throws:
```
The offset specified in a OFFSET clause may not be negative.
```
Request fails with a 500.

**SQLite:**
SQLite silently treats negative `OFFSET` as 0. Query runs, returns the first page. No error — looks like `PageIndex = 1` behaviour. Masks the bug entirely in development.

**Defensive fix in `BaseSpecification`:**
```csharp
protected void ApplyPaging(int skip, int take)
{
    if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip),
        $"Skip must be ≥ 0 (got {skip}). PageIndex must be ≥ 1.");
    if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take),
        $"Take must be > 0 (got {take}).");
    Skip = skip;
    Take = take;
    IsPagingEnabled = true;
}
```

**In `PagingParams`:**
```csharp
private int _pageIndex = 1;
public int PageIndex
{
    get => _pageIndex;
    set => _pageIndex = value < 1 ? 1 : value;  // clamp to minimum 1
}
public int Skip => PageSize * (PageIndex - 1);   // always ≥ 0
```

**Production consequence:** `PageIndex = 0` from a client query string crashes the SQL Server query with an unhandled 500. SQLite dev hides the bug.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Validate `PageIndex ≥ 1` in the controller." Controllers shouldn't know the inner details of paging calculation — validation belongs at the boundary where the invalid state is created. The spec or `PagingParams` model binding is the right place.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that this is the exact CI failure: `PagingParams` test expected `50` but production code used `60` as default page size. The test existed to catch exactly this kind of silent default change — if the page size changes, the test fails and forces a deliberate decision. Tests that "just check current behaviour" earn their keep when behaviour drifts.

</details>

---

**D8.** [HIGH] [OUT-OF-SCOPE] A service loads 200 orders then fetches each order's items in a loop. Count the SQL queries, name the pattern, and write two EF Core fixes — one using the Specification pattern already in this project, one using `AsSplitQuery()`.

<details>
<summary>The 5% Answer</summary>

**Anti-pattern:**
```csharp
var orders = await _repo.GetAllAsync(new OrdersForUserSpec(email)); // 1 query
foreach (var order in orders)
    var items = await _repo.GetAllAsync(new ItemsForOrderSpec(order.Id)); // N queries
// 200 orders = 201 SQL queries
```
- **Count:** 201 — 1 query for the list, 1 per order for its items.
- **Pattern:** **N+1** — one query fetches the parent set, then N separate round-trips fetch each child.

**Fix 1 — `AddInclude` in the Specification (correct for this project):**
```csharp
public OrdersForUserSpec(string email) : base(o => o.BuyerEmail == email)
{
    AddInclude(o => o.OrderItems);
    AddInclude(o => o.DeliveryMethod);
    AddOrderByDescending(o => o.OrderDate);
}
```
- EF generates a single `SELECT` with `LEFT JOIN OrderItems` — all data in one round-trip.

**Fix 2 — `AsSplitQuery()` (multiple collections):**
```csharp
// GenericRepository<T>.GetAllAsync
return await SpecificationEvaluator<T>
    .GetQuery(_context.Set<T>().AsQueryable(), spec)
    .AsSplitQuery()
    .ToListAsync();
// EF issues 2 queries:
// Query 1: SELECT * FROM Orders WHERE BuyerEmail = ...
// Query 2: SELECT * FROM OrderItems WHERE OrderId IN (1, 2, 3 ...)
```
- Use when an entity has **more than one collection navigation** included — a single JOIN causes cartesian explosion (`OrderItems` × `Coupons` per order multiplies result rows; EF deduplicates in memory but the wire payload can be 100× larger).
- Default is single-query mode; opt in globally with `UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)` in `DbContext` options.

**Production consequence:** N+1 on 50 orders per page under 100 concurrent users = 5,100 queries/second — database thread pool exhausted, P99 latency spikes to seconds.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Add `AsNoTracking()` to fix N+1." `AsNoTracking()` removes change-tracking overhead but still fires 201 queries — the root cause is the missing `AddInclude`, not tracking.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing that `AsSplitQuery()` does not run inside a transaction — if a concurrent write lands between the two queries, the second result can reflect a different DB state than the first. For order or inventory data, either use a transaction scope or accept the larger single-query payload. Selective use per query is safer than enabling split globally.

</details>

<details>
<summary>Explanation</summary>

### Fix 1 — `AddInclude` (single query)

`DeliveryMethod = null!` is a required single nav → **INNER JOIN**.
`List<OrderItem>` is a collection nav → **LEFT JOIN**.

```sql
FROM "Orders" AS "o"
INNER JOIN "DeliveryMethods" AS "d" ON "o"."DeliveryMethodId" = "d"."Id"
LEFT JOIN "OrderItems" AS "o0" ON "o"."Id" = "o0"."OrderId"
WHERE "o"."BuyerEmail" = @email
ORDER BY "o"."OrderDate" DESC
```

### Fix 2 — `AsSplitQuery()` (first query only)

`OrderItems` disappears from the first query — EF fires a second `IN (...)` query after.

```sql
-- Query 1
FROM "Orders" AS "o"
INNER JOIN "DeliveryMethods" AS "d" ON "o"."DeliveryMethodId" = "d"."Id"
WHERE "o"."BuyerEmail" = @email
ORDER BY "o"."OrderDate" DESC

-- Query 2 (EF fires after, not shown by ToQueryString)
SELECT "o0".* FROM "OrderItems" AS "o0"
WHERE "o0"."OrderId" IN (1, 2, 3 ...)
```

### Paging — EF wraps in a subquery

EF pages the `Orders` rows first inside a subquery, then JOINs — avoids cartesian explosion on paginated results.

```sql
FROM (
    SELECT "o".* FROM "Orders" AS "o"
    WHERE "o"."Status" = @status_Value
    ORDER BY "o"."OrderDate" DESC
    LIMIT @p OFFSET @p
) AS "o0"
INNER JOIN "DeliveryMethods" AS "d" ON "o0"."DeliveryMethodId" = "d"."Id"
LEFT JOIN "OrderItems" AS "o1" ON "o0"."Id" = "o1"."OrderId"
```

</details>

---

**D9.** [HIGH] Find all 3 bugs in this `GenericRepository` method and write the corrected version:

```csharp
public async Task<IReadOnlyList<T>> GetAllWithSpecAsync(ISpecification<T> spec)
{
    IEnumerable<T> query = _context.Set<T>();
    if (spec.Criteria != null) query = query.Where(spec.Criteria.Compile());
    return await Task.FromResult(query.ToList());
}
```

<details>
<summary>The 5% Answer</summary>

**Bug 1 — `IEnumerable<T>` instead of `IQueryable<T>`:**
Assigning `_context.Set<T>()` to `IEnumerable<T>` switches LINQ to **LINQ-to-objects**. EF enumerates the entire table into memory, then filters in C#. Full table scan, every row across the wire, no WHERE clause in SQL.

**Bug 2 — `.Compile()` on the criteria expression:**
`spec.Criteria` is an `Expression<Func<T, bool>>` — a tree EF translates to SQL. `.Compile()` converts it to a delegate (`Func<T, bool>`), which EF cannot translate. Even if bug 1 were fixed (`IQueryable<T>`), the compiled delegate forces client-side evaluation of the predicate.

**Bug 3 — `SpecificationEvaluator` never called + fake async:**
`spec.Includes`, `OrderBy`, `Skip`, `Take`, projection — **none of these are applied**. Only `Criteria` is used. The spec's whole purpose (composable eager loading, paging, ordering) is silently dropped. `Task.FromResult(query.ToList())` also blocks the thread synchronously and fakes the async signature — no I/O ever suspends.

**Corrected version:**
```csharp
public async Task<IReadOnlyList<T>> GetAllWithSpecAsync(ISpecification<T> spec)
{
    return await ApplySpecification(spec).ToListAsync();
}

private IQueryable<T> ApplySpecification(ISpecification<T> spec)
    => SpecificationEvaluator<T>.GetQuery(_context.Set<T>().AsQueryable(), spec);
```

`SpecificationEvaluator` composes `Criteria`, `Includes`, `OrderBy/Desc`, `Skip/Take`, and projection onto the `IQueryable<T>`, then `ToListAsync()` issues a single SQL round-trip with genuine async I/O.

**Production consequence:** an `OrdersForUserSpec` with `AddInclude(o => o.OrderItems)` + `ApplyPaging(0, 20)` on a 500k-row Orders table fetches **every order** across all users into memory, filters `BuyerEmail` in C#, then returns page 1 — no JOIN on `OrderItems` (second N+1 wave when items are touched later). Memory pressure, connection pool starvation, P99 latency in seconds. SQLite dev barely notices; SQL Server under load melts.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Just add `.AsQueryable()` after `_context.Set<T>()`." That fixes bug 1 but leaves `.Compile()` forcing client-side evaluation and ignores all `Includes`/`OrderBy`/paging — 2 of 3 bugs remain.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `SpecificationEvaluator<T>` is the single source of truth for how specs translate to EF — repositories should never hand-roll spec application. The evaluator is also where `AsSplitQuery()` or `AsNoTracking()` gets centralised. Fixing this method by delegating to the evaluator means the bug can never reappear per-repository. Also: enabling EF Core's `QueryTrackingBehavior` + logging `Microsoft.EntityFrameworkCore.Database.Command` at `Information` would have surfaced `SELECT * FROM Orders` with no `WHERE` during code review.

</details>

---

## ⚡ Syntax Quick-Fire

**D-QF1.** [HIGH] `IEnumerable<T>` vs `IQueryable<T>` in EF Core — where does filtering happen for each?

<details>
<summary>The 5% Answer</summary>

- **`IQueryable<T>`** — LINQ expression tree is translated to SQL. Filtering, ordering, and paging happen **in the database**. Only matching rows are sent over the wire.
- **`IEnumerable<T>`** — LINQ-to-objects. If a `DbSet` is cast to `IEnumerable` before `Where`, EF loads **all rows** into memory first, then filters in C#.

```csharp
// WRONG — full table scan, all rows loaded into memory
IEnumerable<Product> products = _context.Products;
var cheap = products.Where(p => p.Price < 10);

// CORRECT — WHERE clause in SQL
IQueryable<Product> products = _context.Products;
var cheap = products.Where(p => p.Price < 10);
```

**Production consequence:** `IEnumerable` on a 1M-row table fetches 1M rows into app memory before filtering — out-of-memory crash or extreme latency under load.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Returning `IEnumerable<T>` from a repository method and calling `.Where()` on the result in the service layer. The query has already executed — the entire table is in memory. This is the single most common EF Core performance anti-pattern.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing that `IQueryable` composes — you can chain `.Where().OrderBy().Skip().Take()` and EF Core generates a single SQL query. The query only executes when enumerated (`ToListAsync`, `FirstOrDefaultAsync`, etc.). The Specification pattern leverages this: all predicates are added to the `IQueryable` before any SQL is sent.

</details>

---

**D-QF2.** [HIGH] What does `AsNoTracking()` do and when should you use it?

<details>
<summary>The 5% Answer</summary>

Skips adding returned entities to the EF **Change Tracker**. EF won't watch them for mutations, so `SaveChangesAsync` ignores them entirely.

**Use for:** read-only queries — list pages, search results, reports, API responses that map directly to DTOs.

**Benefits:**
- Lower memory: no snapshot of each entity's original state
- Faster: no identity resolution or proxy generation overhead
- Eliminates "entity already tracked" `InvalidOperationException` when loading the same entity in two separate queries

**Don't use for:** entities you intend to update — without tracking, EF can't detect what changed and `SaveChangesAsync` does nothing.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Using `AsNoTracking()` globally for all queries including update flows. Updating an untracked entity requires re-attaching it with `_context.Entry(entity).State = EntityState.Modified` — this marks ALL properties as modified, generating an `UPDATE` for every column instead of only changed ones.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Setting `UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)` on the `DbContext` options as the default and opting **in** to tracking with `.AsTracking()` only where needed — makes read-heavy apps faster by default without per-query annotations.

</details>

---

**D-QF3.** [HIGH] `FirstOrDefaultAsync` vs `SingleOrDefaultAsync` — when does `Single` throw?

<details>
<summary>The 5% Answer</summary>

- **`SingleOrDefaultAsync`** — throws `InvalidOperationException` if **more than one** row matches. Returns `null` if zero rows match. Use when querying by a unique key — multiple matches indicate a data integrity violation you want to surface immediately.
- **`FirstOrDefaultAsync`** — returns the first row, never throws for multiple matches. Use when "first of many" is intentional (e.g., most recent order).

```csharp
// Correct — product ID should be unique; multiple matches = data corruption
var product = await _context.Products.SingleOrDefaultAsync(p => p.Id == id);

// Correct — most recent order; multiple rows is expected
var latest = await _context.Orders.OrderByDescending(o => o.OrderDate).FirstOrDefaultAsync();
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Always using `FirstOrDefaultAsync` "to be safe." On a unique-key query, multiple matches means something is wrong with your data or your query. `First` silently returns one row and hides the corruption — `Single` surfaces it immediately.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Using `FindAsync(id)` for primary key lookups instead of either — it checks the Change Tracker first (returns the tracked entity without a DB round-trip if already loaded), then queries the DB. Faster than `FirstOrDefault` for PK lookups in update-heavy flows.

</details>
