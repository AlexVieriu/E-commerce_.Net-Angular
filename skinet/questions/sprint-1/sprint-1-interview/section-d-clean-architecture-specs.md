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

Red rule: no project higher in the diagram imports a project lower in the diagram. If Infrastructure imported API, you'd have a circular dependency.

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

---

**D2.** [HIGH] What is the Specification pattern and why does this project use it instead of writing query methods on the repository?

<details>
<summary>The 5% Answer</summary>

A **specification** encapsulates a query's WHERE, ORDER BY, INCLUDE, SKIP/TAKE logic into a reusable C# object. The repository executes specs against any EF provider.

```csharp
// Without spec — query in service (anti-pattern)
var products = await _context.Products
    .Where(p => p.Name.Contains(search) && brands.Contains(p.Brand))
    .OrderBy(p => p.Price).Skip(skip).Take(take).ToListAsync();

// With spec — query encapsulated (correct)
var spec = new ProductSpecification(new ProductSpecParams { Search = search, Brands = brands });
var products = await _repo.GetAllAsync(spec);
```

**Why specs:**
1. **Testable in isolation** — `ProductSpecification` is a plain C# object; verify filter logic without EF
2. **Single source of truth** — one spec class, not 5 copies of the same LINQ across different services
3. **Repository stays generic** — `IGenericRepository<T>` has ~6 methods covering all entities. Without specs, you'd need `GetProductsByBrand`, `GetProductsByBrandAndSearch`, etc. — combinatorial explosion.
4. **Projection support** — `BaseSpecification<T, TResult>` maps entities to DTOs in the database, avoiding over-fetching

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

---

**D4.** [HIGH] [OUT-OF-SCOPE] Why does this project use SQLite in development and SQL Server in production? How is `StoreContext` registered so service code never changes between environments?

<details>
<summary>The 5% Answer</summary>

**Why two providers:**
- **SQLite:** zero-install, file-based. Any developer can run the project without installing SQL Server, Azure SQL, or Docker. Fast local startup.
- **SQL Server:** full ANSI compliance, proper `nvarchar(450)` indexes, decimal precision matching production, transaction semantics that match what users experience.

**DI registration — factory pattern:**
```csharp
// Program.cs
if (env.IsDevelopment())
{
    services.AddDbContext<SqliteStoreContext>(o =>
        o.UseSqlite(config.GetConnectionString("DefaultConnection")));
    services.AddScoped<StoreContext>(p =>
        p.GetRequiredService<SqliteStoreContext>());
}
else
{
    services.AddDbContext<SqlServerStoreContext>(o =>
        o.UseSqlServer(config.GetConnectionString("DefaultConnection")));
    services.AddScoped<StoreContext>(p =>
        p.GetRequiredService<SqlServerStoreContext>());
}
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

**D5.** [MEDIUM] The namespace `Application.Specifications.ProductSpecification` causes a compile error — but it was working yesterday. What happened and what should the namespace be?

<details>
<summary>The 5% Answer</summary>

**What happened:** the `GlobalUsings.cs` file (or a `using` statement) was incorrectly written to include the class name as a sub-namespace:
```csharp
// WRONG — implies a subfolder named ProductSpecification inside Specifications/
global using Application.Specifications.ProductSpecification;

// CORRECT — specs are flat in the Specifications/ folder
global using Application.Specifications;
```

**Why it worked before:** a previous developer may have had an older version where specs were in per-class subdirectories, or the class was previously defined inside a nested namespace. After a folder restructure, the namespace changed but `GlobalUsings.cs` wasn't updated.

**The correct namespace:**
All specification files live directly in `Application/Specifications/` (no subdirectories). The namespace is `Application.Specifications` — never nested by class name.

This was one of the 12 CI failures found in Task 1 (first pipeline run) — it existed silently on dev machines because their local namespace matched the file structure, but CI picked up the mismatch from a clean build.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Create a subfolder `ProductSpecification` to match the namespace." That technically compiles but creates one-file-per-folder organisation for no benefit — hundreds of single-file directories.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `GlobalUsings.cs` with a wrong namespace generates no "unused using" warning — the namespace partially resolves. The error only surfaces when a type from that namespace is used and the compiler can't find it. IDE tooling (Resharper, Roslyn analyser) can catch this as a "namespace not found" warning if configured, but by default it's invisible until compile time.

</details>

---

### HARD

**D6.** [HIGH] The `UnitOfWork` caches `GenericRepository<T>` instances in a `ConcurrentDictionary`. What problem does this solve, and what subtle bug does it introduce if `UnitOfWork` is accidentally registered as a Singleton?

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

**D8.** [HIGH] A service loads 200 orders then fetches each order's items in a loop. Count the SQL queries, name the pattern, and write two EF Core fixes — one using the Specification pattern already in this project, one using EF Core 10's `AsSplitQuery()`.

<details>
<summary>The 5% Answer</summary>

**The anti-pattern:**
```csharp
// In OrderService
var orders = await _unitOfWork.Repository<Order>()
    .GetAllAsync(new OrdersForUserSpec(email));       // 1 query

foreach (var order in orders)
{
    var items = await _unitOfWork.Repository<OrderItem>()
        .GetAllAsync(new ItemsForOrderSpec(order.Id)); // N queries — one per order
}
// 200 orders = 201 SQL queries
```

**Pattern name:** **N+1** — 1 query to fetch the parent list, then N queries for each child collection.

**At scale:** 1,000 active users, each with 50 orders → 50,001 queries per page load. Database thread pool exhausted, latency spikes to seconds, connection pool timeouts under concurrent load.

**Fix 1 — `AddInclude` in the Specification (correct for this project):**
```csharp
public class OrdersForUserSpec : BaseSpecification<Order>
{
    public OrdersForUserSpec(string email) : base(o => o.BuyerEmail == email)
    {
        AddInclude(o => o.OrderItems);         // LEFT JOIN — 1 query total
        AddInclude(o => o.DeliveryMethod);     // chain includes as needed
        AddOrderByDescending(o => o.OrderDate);
    }
}
```
EF Core generates a single SQL query with `LEFT JOIN OrderItems`. All order items are loaded in one round-trip.

**Fix 2 — `AsSplitQuery()` for EF Core 9/10 (avoids cartesian explosion on multiple collections):**
```csharp
// In SpecificationEvaluator (or directly in the spec):
query = query.AsSplitQuery();
// EF Core issues 2 separate SQL queries instead of 1 giant JOIN:
// SELECT * FROM Orders WHERE BuyerEmail = ...
// SELECT * FROM OrderItems WHERE OrderId IN (...)
// Avoids row multiplication when including multiple collections simultaneously
```

**When to use split queries:** loading `Order` with both `OrderItems` and `Coupons` as includes — a JOIN would multiply rows (cartesian product). With EF Core 10, `AsSplitQuery()` is the default for multi-collection includes when configured globally.

**Production consequence:** N+1 in a paginated order history endpoint with 50 orders per page → 51 queries. Under 100 concurrent users = 5,100 queries per second. Database CPU spikes, connection pool exhausted, P99 latency exceeds 5s.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Add `AsNoTracking()` to fix N+1." `AsNoTracking()` improves read performance but doesn't reduce query count — N+1 still fires N+1 queries, just without change tracking overhead. The problem is missing eager loading (`AddInclude`), not tracking.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing EF Core's **cartesian explosion warning** — loading `Orders` with both `OrderItems` (100 items) and `Coupons` (3 per order) via JOIN produces 300 rows per order in the result set. EF deduplicates in memory, but the wire transfer is 300x larger than necessary. `AsSplitQuery()` solves this at the cost of a second round-trip — the right trade-off for wide result sets.

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
"Just add `.AsQueryable()` after `_context.Set<T>()`." That fixes bug 1 but leaves `.Compile()` forcing client-side predicate evaluation and still ignores `Includes`/`OrderBy`/paging. Repositories that only use `Criteria` defeat the entire Specification pattern — also, any `ToListAsync()` replacement for `Task.FromResult(query.ToList())` belongs on an `IQueryable`, not an `IEnumerable` (it compiles but is the same synchronous `ToList` + `Task.FromResult` under the hood on `IEnumerable`).

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
