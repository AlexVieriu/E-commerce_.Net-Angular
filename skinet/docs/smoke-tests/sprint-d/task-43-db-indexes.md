# Task 43 — DB indexes: the measurement

Mostly a **measurement record**, not a checklist — the evidence that the four indexes changed the plans, not
just the schema. One new manual check at the bottom.

`Products` had **zero** indexes; `Orders` had nothing on `BuyerEmail`. Everything below ran against the
compose `sqlserver` container ([`../../build-deployment.md`](../../build-deployment.md)) — dev is SQLite, so
`dotnet run` never applies a SqlServer migration and it would otherwise debut on Azure.

## Method

Local SQL Server, seeded to volume, same query before and after. Azure was **not** used: the database is on
the free offer, and repeatedly scanning a 200k-row table is exactly the workload that burns the monthly
allowance and auto-pauses the demo.

| | |
|---|---|
| Volume | 200,000 products · 200,000 orders · 2,000 distinct buyer emails (~100 orders each) |
| Seed | throwaway T-SQL `INSERT…SELECT`, **not** `BogusSeeder` — `--seed` is Development-only and Development is SQLite, so it cannot reach SQL Server |
| Queries | copied verbatim from EF's `ToQueryString()`, so this measures the app's SQL, not a hand-written approximation |
| Numbers | `SET STATISTICS IO` / `TIME` for cost, `SET STATISTICS PROFILE` for plan shape |

**Why logical reads and not a stopwatch:** a logical read is a page fetched from the data cache — a count,
not a duration. It doesn't move when the machine is busy or the buffer pool is already warm, which is also
why BenchmarkDotNet is the wrong tool here: its own repeated iterations would time the cache.

## The numbers

| # | Query | Reads before | Reads after | CPU before | CPU after |
|---|---|---|---|---|---|
| 1 | order list — `ByEmail` | **7,722** | **320** | 313 ms | 2 ms |
| 2 | landing page — no filter, `ORDER BY Name` | **15,062** | **46** | 4,190 ms | 1 ms |
| 3 | shop grid filtered — `ORDER BY Price` | **15,062** | **191** | 555 ms | 2 ms |
| 4 | the paging `COUNT(*)` | **15,062** | **343** | 347 ms | 10 ms |
| 5 | `ByIdAndEmail` | 3 | 3 | 1 ms | 0 ms |

Reads are on the driving table (`Orders` for 1 and 5, `Products` for 2–4).

## The plans

Every "before" plan is the same three operators — scan the whole table, sort it, throw most of it away.

| # | Before | After |
|---|---|---|
| 1 | `Clustered Index Scan(PK_Orders)` → `Sort` → `Parallelism` | `Index Seek(IX_Orders_BuyerEmail_OrderDate)` — **no Sort** |
| 2 | `Clustered Index Scan(PK_Products)` → `Sort` → `Top` | `Index Scan(IX_Products_Name)` ordered → `Top` |
| 3 | `Clustered Index Scan(PK_Products)` → `Sort` → `Top` | `Index Scan(IX_Products_Price)` ordered → `Top` |
| 4 | `Clustered Index Scan(PK_Products)` → `Stream Aggregate` | `Index Seek(IX_Products_Brand_Type)` |
| 5 | `Clustered Index Seek(PK_Orders)` | unchanged |

Query 2 is the one worth reading twice. The landing page carries **no `WHERE` at all** — EF prunes the
`Count == 0 ||` guards at translation time — so it reduces to `ORDER BY Name OFFSET 0 FETCH 10`. Before, that
meant reading 200,000 rows and sorting all of them to keep ten. After, it walks ten index entries and stops:
**15,062 reads → 46**, and 4,190 ms of CPU → 1 ms.

## Three things that did not go to plan

- **The `(Brand, Type)` index does not serve the grid page.** Query 3 filters on Brand/Type but sorts on
  Price, and the optimizer picked `IX_Products_Price` — a sort-free ordered walk beat a filter seek followed
  by a sort. The composite still earns its place on the `COUNT(*)` (query 4), which has no `ORDER BY`.
- **Query 5 was never slow.** `ByIdAndEmail` filters on `Id`, so `PK_Orders` already answered it in 3 reads.
  The composite covers it by leftmost prefix, but that is not what makes it fast.
- **`OrderItems` was seeded empty**, so the `LEFT JOIN` contributes 0 reads. The order-list numbers are
  therefore a floor, not a ceiling.

**One free win worth noting:** parameters now declare as `nvarchar(30)` / `nvarchar(256)`, matching the
columns exactly. Against `nvarchar(max)` there is no width to match and no index to seek.

## The defect this task uncovered

Applying the existing migrations to a fresh container printed **four** of the five in the folder.
`20260407100224_AddUniqueIndexOnPaymentIntentId` was skipped — it has no `.Designer.cs`, and that file is
where the `[Migration]` attribute EF scans for lives. So T18's unique index on `PaymentIntentId` **had never
been created on SQL Server or on Azure**; only SQLite had it, which is why nothing ever looked wrong in dev.

Folded into this task's migration rather than left for later: the dead file is deleted, and its
`AlterColumn` + `CreateIndex` were hand-added to `20260831062145_AddQueryIndexes.cs`. The unique index built
cleanly against the 200,000 seeded orders, so the `CREATE UNIQUE INDEX` is proved at volume — but on Azure it
will still fail if production holds duplicate `PaymentIntentId` values. **Check that before deploying:**

```sql
SELECT PaymentIntentId, COUNT(*) FROM Orders GROUP BY PaymentIntentId HAVING COUNT(*) > 1;
```

## The one manual check

| Do | Expect |
|---|---|
| Admin → create a product with a 200-char name | **400** with a validation message — not a 500 |

`HasMaxLength(100)` without the matching `MaximumLength(100)` in `CreateProductDtoValidator.cs` lets the
value reach SQL Server and come back as `String or binary data would be truncated`.

Shop grid, filters, sorts and the order list are the **Core smoke** in
[`../../testing.md`](../../testing.md) — no separate steps here. On baseline data (52 products) every plan
is a scan anyway, so a browser run proves nothing broke, never that anything got faster.

## Resetting the container

The volume seed is throwaway and lives only in the container:

```bash
docker compose down -v sqlserver && docker compose up -d sqlserver
```
