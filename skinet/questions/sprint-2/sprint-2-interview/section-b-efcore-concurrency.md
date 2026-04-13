## SECTION B — EF CORE & CONCURRENCY

### EASY

**B1.** What does `[ConcurrencyCheck]` do in EF Core?

<details><summary>Answer</summary>

`[ConcurrencyCheck]` marks a property as a **concurrency token**. When EF Core generates an `UPDATE` or `DELETE`, it adds the original value of that property to the `WHERE` clause. If no rows are matched (because another request already changed that value), EF throws `DbUpdateConcurrencyException`. It's optimistic concurrency — no locks taken, conflict detected at save time.
</details>

---

**B2.** What are the three value states in `DbUpdateConcurrencyException`?

<details><summary>Answer</summary>

When `DbUpdateConcurrencyException` is thrown, `exception.Entries` contains the conflicting entities. Each entry exposes three value sets:

- **`OriginalValues`** — what you read from the DB at the start (the values EF used in the WHERE clause)
- **`CurrentValues`** — what you tried to write (your in-memory changes)
- **`DatabaseValues`** — what's actually in the DB right now (fetched on demand, requires a DB round-trip)

These three sets let you implement merge strategies: who wins, how to combine, or whether to reject.
</details>

---

**B3.** What is the difference between `FirstOrDefault()` and `SingleOrDefault()` in EF Core? When would you use each?

<details><summary>Answer</summary>

- **`FirstOrDefault()`** returns the first matching row. SQL uses `TOP 1` (SQL Server) or `LIMIT 1`. No error if multiple rows match.
- **`SingleOrDefault()`** asserts exactly one row matches. SQL uses `TOP 2` (fetches two rows to detect duplicates). Throws `InvalidOperationException` if more than one row matches.

Use `FirstOrDefault()` for sorted lists (e.g., latest order). Use `SingleOrDefault()` when business logic requires uniqueness — looking up by email, username, or any column that *should* be unique.
</details>

---

**B4.** What does `Include()` do in EF Core? What SQL does it generate?

<details><summary>Answer</summary>

`Include()` performs **eager loading** — it tells EF to load a navigation property in the same query as the parent entity, using a SQL `JOIN`. Without it, navigation properties are `null` (in a non-tracking context) or are lazy-loaded only when accessed (requires proxies). `Include()` generates a `LEFT JOIN` between the parent and the related table, returning all data in one round-trip.
</details>

---

**B5.** What is a migration in EF Core? What happens if you modify an entity but forget to create a migration?

<details><summary>Answer</summary>

A migration is a C# class EF generates that represents the delta between your current entity model and the last applied schema state. It contains `Up()` (apply the change) and `Down()` (reverse it) methods that translate to DDL SQL. If you modify an entity but don't create a migration, the in-memory model is out of sync with the database schema. Depending on your configuration: runtime `SqlException` on queries that reference the missing column, or silent data loss if columns are dropped from the model without a migration removing them from the DB.
</details>

---

### MEDIUM

**B6.** You have `[ConcurrencyCheck]` on `QuantityInStock`. Two requests read `stock = 5` simultaneously. Request A saves `stock = 4`. Request B tries to save `stock = 4`. What SQL does EF generate for Request B? What happens? What exception is thrown?

<details><summary>Answer</summary>

EF generates:
```sql
UPDATE Products SET QuantityInStock = 4
WHERE Id = 7 AND QuantityInStock = 5
```
By the time Request B executes this, the DB has `stock = 4` (Request A already changed it). The `WHERE` clause matches 0 rows. EF checks rows affected — expects 1, got 0 — and throws `DbUpdateConcurrencyException`. Request B's transaction is rolled back.
</details>

---

**B7.** What does `AsNoTracking()` do? When should you use it? What breaks if you use `AsNoTracking()` and then try to update the entity? What happens to `[ConcurrencyCheck]` if you read with `AsNoTracking()`?

<details><summary>Answer</summary>

- **What it does** — tells EF not to add returned entities to the change tracker. Faster reads (no snapshot, no identity map lookup), less memory. Use for read-only queries (DTOs, projections, display data).
- **If you try to update** — EF treats the entity as detached and throws `InvalidOperationException` unless you manually `Attach()` it and set the state.
- **`[ConcurrencyCheck]` + no-tracking** — EF can still read the token value, but doesn't store the original value in its tracking snapshot. If you Attach and update, you must manually set `OriginalValues` for the concurrency check to work correctly.
</details>

---

**B8.** Compare `[ConcurrencyCheck]` and `[Timestamp]` (rowversion). When would you choose each? Which databases support `[Timestamp]`? What SQL does each generate?

<details><summary>Answer</summary>

- **`[ConcurrencyCheck]`** — uses an existing business column as the token; original value included in the WHERE clause. Portable across all databases, but requires the column to change detectably with every update.
- **`[Timestamp]`** (or `[RowVersion]`) — dedicated `rowversion`/`timestamp` binary column the database automatically increments on every update. More reliable (DB handles it), smaller WHERE clause (8-byte binary). SQL Server native — requires workarounds for PostgreSQL (`xmin`) or SQLite.
</details>

---

**B9.** You call `SaveChangesAsync()` and it throws `DbUpdateConcurrencyException`. You want to implement "client wins" resolution. Write the code to reload the database values and force your changes through.

<details><summary>Answer</summary>

"Client wins" means: your in-memory changes override whatever is in the DB. You reload the DB values into `OriginalValues` (so EF's WHERE clause matches the current DB state), keeping your `CurrentValues` intact, then retry `SaveChangesAsync()`.

```csharp
catch (DbUpdateConcurrencyException ex)
{
    foreach (var entry in ex.Entries)
    {
        var dbValues = await entry.GetDatabaseValuesAsync();
        if (dbValues == null)
            throw; // row was deleted — client wins has no meaning
        entry.OriginalValues.SetValues(dbValues); // WHERE now uses current DB state
    }
    await context.SaveChangesAsync(); // retry — client's CurrentValues win
}
```
</details>

---

**B10.** What is the N+1 query problem? Give an example with Orders and OrderItems. How does `Include()` fix it? What's the downside of over-using `Include()`?

<details><summary>Answer</summary>

The N+1 problem is when you execute 1 query to fetch N parent entities, then execute N additional queries (one per parent) to load a related collection. With 100 orders, that's 101 queries. `Include()` collapses this into a single JOIN query. The downside of over-using `Include()` is fetching large amounts of data you don't need (over-fetching), and for multiple collections, a cartesian product that multiplies rows.
</details>

---

**B11.** What does this code do? Is there a performance problem?

```csharp
var products = await context.Products.ToListAsync();
var expensive = products.Where(p => p.Price > 100).ToList();
```

<details><summary>Answer</summary>

Yes, there is a serious performance problem. `ToListAsync()` materializes the entire `Products` table into memory first — all rows, all columns. The `.Where(p => p.Price > 100)` then filters in-memory (LINQ to Objects), not in the database. If there are 100,000 products, all 100,000 rows are loaded over the network before the filter runs.

The fix: apply the filter before materializing.

```csharp
var expensive = await context.Products
    .Where(p => p.Price > 100)
    .ToListAsync();
```

This generates `SELECT * FROM Products WHERE Price > 100` — only matching rows transfer.
</details>

---

**B12.** What is the Specification pattern? Why use it instead of putting query logic directly in the repository? What are the trade-offs?

<details><summary>Answer</summary>

The Specification pattern encapsulates a query's criteria (filter, sort, pagination, includes) in a reusable object. Instead of adding a new method to the repository for every query variation, callers build a specification and pass it to a generic `GetAsync(spec)` method. It keeps repositories thin, makes queries composable and testable in isolation, and avoids repository method explosion. Trade-off: adds abstraction overhead — for simple CRUD it's overkill.
</details>

---

**B13.** What is the difference between `IQueryable<T>` and `IEnumerable<T>` when used with EF Core? If your repository returns `IEnumerable<Product>` and the caller does `.Where(p => p.Price > 100)`, where does the filtering happen? Why is this a performance disaster?

<details><summary>Answer</summary>

`IQueryable<T>` represents an unexecuted query — EF translates further LINQ operators into SQL. `IEnumerable<T>` is already materialized data in memory — any further LINQ is C# iteration, not SQL.

If the repository returns `IEnumerable<Product>`, the entire `Products` table is loaded into memory first. The caller's `.Where(p => p.Price > 100)` then runs as LINQ to Objects — filtering 1 million rows in memory instead of asking the database to return only matching rows. It's a full-table scan in application memory over the network.
</details>

---

### HARD

**B14.** You have a Unit of Work with `BeginTransactionAsync()`. Inside the transaction, you decrement stock (with concurrency check), create an order, and insert a payment record. The stock update succeeds, the order insert succeeds, but a subsequent write to Redis fails with a network timeout. What happens to the SQL transaction? What happens to the stock? Design the correct rollback behavior.

<details><summary>Answer</summary>

The SQL transaction is independent of Redis. If you don't explicitly call `transaction.RollbackAsync()`, the SQL changes are either committed or left open depending on your code flow. If you catch the Redis exception and throw without rolling back, SQL Server eventually rolls back when the connection is closed — but this is non-deterministic. The correct design: catch all exceptions inside the transaction block, call `RollbackAsync()` explicitly, and handle the Redis failure as a separate concern (outbox pattern or fire-and-forget with retry). The SQL database is the source of truth; Redis is a cache.
</details>

---

**B15.** EF Core's `InMemoryDatabase` doesn't enforce foreign keys, unique constraints, or concurrency tokens. You wrote a concurrency test using InMemory and it passed — but the same scenario fails in production. Explain why InMemory gave a false positive. What testing database should you use instead? What are the trade-offs?

<details><summary>Answer</summary>

`InMemoryDatabase` is a simple in-memory dictionary — it simulates basic entity storage but doesn't implement SQL semantics. Concurrency tokens (`[ConcurrencyCheck]`, `[Timestamp]`) are not checked during saves. Two simultaneous saves to the same entity both succeed without `DbUpdateConcurrencyException`. In production (SQL Server), the WHERE clause with the original value fails to match and the exception fires. Use **SQLite in-process with WAL mode** for tests — it's a real SQL engine, enforces constraints, and supports concurrency tokens, while still running in-memory or from a temp file.
</details>

---

**B16.** You have 10 million products. Your query filters by `Category`, sorts by `Price`, and paginates (`Skip(100).Take(20)`). The query takes 3 seconds. Explain the execution plan, identify the bottleneck, and design the correct index. What's the difference between a covering index and a filtered index here?

<details><summary>Answer</summary>

Without an index, SQL Server does a full table scan of 10 million rows, then sorts all matching rows by price in a temporary work area (sort spill), then offsets 100 and returns 20 — processing millions of rows for a 20-row result.

The bottleneck is the full scan + sort. The fix is a **composite index on `(CategoryId, Price)`** — SQL Server can use it to seek directly to the category, already sorted by price, then skip 100 and take 20. A **covering index** adds all projected columns to the index itself (using `INCLUDE`) so no table lookup is needed. A **filtered index** is an index with a WHERE clause, useful if most queries target a specific subset (e.g., only active products).
</details>

---

**B17.** Compare three EF Core concurrency strategies. For each, give the generated SQL and a use case:
1. No concurrency control (default)
2. `[ConcurrencyCheck]` on a specific column
3. `[Timestamp]` (rowversion)

<details><summary>Answer</summary>

**1. No concurrency control (last-write wins)**
```sql
UPDATE Products SET QuantityInStock = @p0 WHERE Id = @p1
```
Use: non-critical data where overwrites are acceptable (e.g., user preference settings, display order).

**2. `[ConcurrencyCheck]` on a column**
```sql
UPDATE Products SET QuantityInStock = @p0 WHERE Id = @p1 AND QuantityInStock = @p2
```
Use: business-meaningful conflict detection on a specific field (stock, balance, version counter). Portable across all databases.

**3. `[Timestamp]` / rowversion**
```sql
UPDATE Products SET Name = @p0 WHERE Id = @p1 AND RowVersion = @p2
```
Use: general-purpose "was this entity touched by anyone" check. SQL Server native, detects all changes regardless of which column changed.
</details>

---

**B18.** Design a retry strategy for `DbUpdateConcurrencyException` in a background order processor. Include: max retries, backoff, what to reload, when to give up. Write the code.

<details><summary>Answer</summary>

For a background processor, use exponential backoff with jitter (to avoid thundering herd), a max retry limit (3–5), reload both the entity and fresh DB state before each retry, and treat deletion (null `DatabaseValues`) as a terminal failure. Give up after max retries and either dead-letter the message or emit a structured failure event.
</details>

---
