# Task 27 — Dashboard Aggregation & the Specification Flow

## Why this approach

The admin dashboard is one screen answering "how's the shop doing?" — counts, revenue, low-stock
alerts, recent orders — in a single call. The work lives behind a dedicated `IDashboardService`
(controller stays thin, dependency inversion keeps it modular), and the numbers are computed
**in memory**: load the rows, aggregate with LINQ. Deliberate "fine at this scale" call — the
admin dataset is small, and the revenue total uses `Order.GetTotal()` (a C# method EF can't
translate to SQL), so in-memory sidesteps that entirely. The escape hatch is documented in a
comment: at larger scale, push `COUNT`/`SUM` into SQL via repo aggregate methods.

## What confused me — actual gaps from the LEARN + cold test

| Gap | Location | Tier |
|---|---|---|
| When/where does a spec actually hit the DB? (deferred until `ToListAsync`) | `GenericRepository.cs · GetAllAsync` → `SpecificationEvaluator` | **OWN** |
| In-memory aggregate vs SQL `COUNT`/`SUM` — and why `GetTotal()` blocks the SQL version | `DashboardService.cs · TotalRevenue` | **OWN** |
| "Too many methods" — sort spec/repo by role: recipe / cook / kitchen | `BaseSpecification.cs`, `GenericRepository.cs` | **RECOGNIZE** |
| Where is the spec "checked"? Only in `ApplyCommon` + `GetQuery` `if`s | `SpecificationEvaluator.cs · ApplyCommon` | **RECOGNIZE** |
| Recent orders sorted by the **spec**; low-stock sorted in the **service** | `OrderSpecification.AllForDashboard` vs `DashboardService` | **RECOGNIZE** |
| `Where` filters (keeps rows); `Select` projects (can return `List<bool>`) | `DashboardService.cs · lowStock` | **LET GO** |
| Low-stock threshold is `<=` not `<` ("at or below") — off-by-one in round 2 | `DashboardService.cs · lowStock` | **LET GO** |

## Explanation

### A spec describes; the repo cooks; EF runs it

A specification is a **bag of properties** (`Criteria`, `Includes`, `OrderByDesc`, `Skip/Take`,
`IsDistinct`) — it runs nothing. Three roles:

- **Recipe** (`*Specification`) — builder methods *set* the properties. No DB.
- **Cook** (`GenericRepository` + `SpecificationEvaluator`) — *read* the properties, build the EF query. Still no DB.
- **Kitchen** (EF Core) — `ToListAsync` translates to SQL and executes.

LINQ is **deferred**: building `IQueryable` runs nothing; the query is sent only when enumerated
(`ToList`, `Count`, `foreach`, async overloads). Full road map in
[`docs/backend/specifications.md → Request flow`](../../docs/backend/specifications.md).

The spec's properties are read in exactly **one** place — `SpecificationEvaluator.ApplyCommon` +
`GetQuery` (`if (spec.Criteria != null)`, `Includes`, `OrderBy`, `IsDistinct`, `IsPagingEnabled`).
Those `if`s are the only "checking points."

### Revenue rule

```csharp
TotalRevenue = orders
    .Where(o => o.Status == OrderStatus.PaymentReceived)
    .Sum(o => o.GetTotal());
```
Revenue counts `PaymentReceived` orders only — net of discount, incl. shipping (`GetTotal()`).

### In-memory vs SQL aggregate

Current code pulls all rows then counts/sums in C# — trivial for 25 orders, wasteful for 5M.
The SQL alternative (`SELECT COUNT(*)`, `SELECT SUM(...)`) returns one number, no rows loaded — but
`Sum(o => o.GetTotal())` can't be pushed down: `GetTotal()` is a **C# method**, not a column, so EF
can't translate it. You'd have to re-express the total as column arithmetic first. That awkwardness
is part of why in-memory was the pragmatic pick at this scale.

### Who sorts what

- **Recent orders** — sorted by the **spec** (`AddOrderByDescending(o => o.OrderDate)` → SQL `ORDER BY ... DESC`). The service just `Take`s the top 5.
- **Low stock** — sorted in the **service** (`.OrderBy(p => p.QuantityInStock)`), because that's a *different* key than the spec applied. Rule: if the list is already in the order you want, don't re-sort — just `Take`.

### Count vs cap

`LowStockCount` / `TotalOrders` stay **exact**; `LowStockProducts` / `RecentOrders` are **capped
previews** (`Take(10)` / `Take(5)`). The admin sees "30 low" but only the 10 most urgent rows.

## Exercise

Without looking at the code:
1. Write the `TotalRevenue` LINQ chain (which status it sums).
2. Write the low-stock block: exact count + a capped, most-urgent-first preview of `LowStockProductDto`.
3. Explain where the "recent" ordering comes from (it's not in this method).
4. Explain what stops you from moving `TotalRevenue` into a SQL `SUM`.

## Solution

```csharp
var lowStock = products.Where(p => p.QuantityInStock <= LowStockThreshold).ToList(); // <= not <

return new DashboardDto
{
    TotalProducts = products.Count,
    TotalOrders   = orders.Count,
    PendingOrders = orders.Count(o => o.Status == OrderStatus.Pending),

    TotalRevenue  = orders.Where(o => o.Status == OrderStatus.PaymentReceived)
                          .Sum(o => o.GetTotal()),

    LowStockCount = lowStock.Count,                       // exact
    LowStockProducts = lowStock.OrderBy(p => p.QuantityInStock) // most urgent first
                               .Take(LowStockListSize)           // capped preview
                               .Select(p => new LowStockProductDto { /* Id, Name, QuantityInStock */ })
                               .ToList(),

    RecentOrders = orders.Take(RecentOrdersSize)         // spec already ordered desc
                         .Select(o => new RecentOrderDto { /* lean fields */ })
                         .ToList()
};
```
3. The spec orders it: `OrderSpecification.AllForDashboard()` calls `AddOrderByDescending(o => o.OrderDate)`.
4. `GetTotal()` is a C# method, not a SQL column — EF can't translate it into `SUM`.

## Tests

`DashboardService_Tests.cs` — EF InMemory provider + real `UnitOfWork` (matches `OrderService_Tests`):
- `GetDashboardAsync_ComputesCounts_RevenueRule_AndLowStock` — counts correct, revenue sums only `PaymentReceived`, low-stock set right.
- `GetDashboardAsync_CapsPreviewLists_ButKeepsCountsExact` — preview lists capped at 10/5 while `LowStockCount`/`TotalOrders` stay exact.

> Note: tests use the EF Core **InMemory provider** (a test-only fake DB) — distinct from the
> production **in-memory aggregation** (loading real rows then computing in C#). Same words, different things.

## Interview Q&A

**When does a LINQ/EF query actually run?**
- It's deferred — building `IQueryable` runs nothing
- Executes only when enumerated: `ToList`/`ToArray`/`Count`/`First`/`foreach` (and async overloads)

**What is a specification and what does it do at runtime?**
- An object that describes a query as data (criteria, includes, ordering, paging)
- It runs nothing; the repository + `SpecificationEvaluator` read it and build the EF query; `ToListAsync` executes it

**Why aggregate in memory instead of SQL here? When would you switch?**
- Small admin dataset → loading + LINQ is simple and fine
- `GetTotal()` is a C# method EF can't translate to SQL `SUM`, so in-memory avoids that
- Switch to SQL `COUNT`/`SUM` (repo aggregate methods) when row counts grow large enough to hurt — confirm with a benchmark

**Why is the recent-orders list correct without an `OrderBy` in the service?**
- The spec (`AllForDashboard`) already applied `ORDER BY OrderDate DESC`
- The list arrives newest-first, so `Take(5)` gives the 5 most recent

**`Where` vs `Select`?**
- `Where` filters — keeps matching items, same type, fewer of them
- `Select` projects — transforms each item; `Select(p => p.Stock <= 5)` would give `List<bool>`

**Count vs preview cap?**
- Counts (`LowStockCount`, `TotalOrders`) are exact
- List fields are capped previews (`Take(10)`/`Take(5)`) so the payload stays small
