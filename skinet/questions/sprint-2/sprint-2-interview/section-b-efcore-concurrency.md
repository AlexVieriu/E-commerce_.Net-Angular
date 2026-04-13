## SECTION B — EF CORE & CONCURRENCY

### EASY

**B1.** What does `[ConcurrencyCheck]` do in EF Core?

<details><summary>Answer</summary>

`[ConcurrencyCheck]` marks a property as a **concurrency token**. When EF Core generates an `UPDATE` or `DELETE`, it adds the original value of that property to the `WHERE` clause. If no rows are matched (because another request already changed that value), EF throws `DbUpdateConcurrencyException`. It's optimistic concurrency — no locks taken, conflict detected at save time.
</details>

<details><summary>Deep Dive</summary>

**What EF Core generates without `[ConcurrencyCheck]`**

```sql
-- Standard update — only matches on primary key
UPDATE Products SET QuantityInStock = 4
WHERE Id = 7
```

If two requests both read `stock = 5` and both try to set it to `4`, both succeed silently. The second write wins. You have a lost update bug.

**What EF Core generates with `[ConcurrencyCheck]`**

```csharp
public class Product
{
    public int Id { get; set; }

    [ConcurrencyCheck]
    public int QuantityInStock { get; set; }
}
```

```sql
-- EF adds the original value to WHERE
UPDATE Products SET QuantityInStock = 4
WHERE Id = 7 AND QuantityInStock = 5
```

Request A executes this first → 1 row affected → success. Now `stock = 4` in the DB.

Request B executes the same SQL → 0 rows affected (because DB now has `4`, not `5`) → EF sees 0 affected rows → throws `DbUpdateConcurrencyException`.

**Fluent API equivalent**

```csharp
modelBuilder.Entity<Product>()
    .Property(p => p.QuantityInStock)
    .IsConcurrencyToken();
```

**What counts as a concurrency token**

Any property you mark becomes part of every UPDATE/DELETE's WHERE clause. You can have multiple concurrency tokens on one entity — EF ANDs them all. In practice, pick one: either a dedicated `[Timestamp]` (rowversion) column, or a specific business-meaningful column like `QuantityInStock` or `Version`.

**The key mental model: optimistic locking**

`[ConcurrencyCheck]` implements **optimistic concurrency** — it assumes conflicts are rare, takes no locks, and detects conflicts only at commit time. The alternative is pessimistic locking (`SELECT FOR UPDATE`) which holds a database lock for the duration of the operation. Optimistic is appropriate when conflicts are infrequent and the cost of retrying is low.
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

<details><summary>Deep Dive</summary>

**Visualizing the three states**

```
Time →

T1: You read stock = 10 → OriginalValues["QuantityInStock"] = 10
T2: Alice buys 2 → DB stock = 8
T3: You try to save stock = 9 (you also sold 1)

OriginalValues["QuantityInStock"] = 10   ← what you read
CurrentValues["QuantityInStock"]  = 9    ← what you want to write
DatabaseValues["QuantityInStock"] = 8    ← what Alice left behind
```

**Accessing them in code**

```csharp
catch (DbUpdateConcurrencyException ex)
{
    foreach (var entry in ex.Entries)
    {
        var original  = entry.OriginalValues;
        var current   = entry.CurrentValues;
        var db        = await entry.GetDatabaseValuesAsync(); // round-trip to DB

        // db is null if the entity was deleted by another user
        if (db == null)
        {
            // Entity deleted — decide: fail or recreate?
            throw new InvalidOperationException("Product was deleted by another user.");
        }

        // Example: three-way merge for stock
        var originalStock = (int)original["QuantityInStock"];
        var myStock       = (int)current["QuantityInStock"];
        var dbStock       = (int)db["QuantityInStock"];

        int sold = originalStock - myStock;           // I sold: 10 - 9 = 1
        int mergedStock = dbStock - sold;             // DB value minus my sale: 8 - 1 = 7

        entry.CurrentValues["QuantityInStock"] = mergedStock;
        entry.OriginalValues.SetValues(db);           // update origin so next save passes
    }

    await context.SaveChangesAsync(); // retry with merged values
}
```

**The three resolution strategies**

| Strategy | How | When |
|---|---|---|
| Client wins | Set `OriginalValues` = `DatabaseValues`, re-save | Admin override, authoritative source |
| Database wins | Set `CurrentValues` = `DatabaseValues`, discard changes | Read-heavy, last-write doesn't matter |
| Merge | Compute merged value, set `CurrentValues` | Stock, counters, commutative operations |

**`DatabaseValues` is lazy — it's a DB call**

`entry.GetDatabaseValuesAsync()` makes a fresh SELECT. Don't call it in a loop without thinking — each call is a round-trip. For an entity with many concurrency conflicts, this adds up. It can also return `null` if the row was deleted between your read and save.
</details>

---

**B3.** What is the difference between `FirstOrDefault()` and `SingleOrDefault()` in EF Core? When would you use each?

<details><summary>Answer</summary>

- **`FirstOrDefault()`** returns the first matching row. SQL uses `TOP 1` (SQL Server) or `LIMIT 1`. No error if multiple rows match.
- **`SingleOrDefault()`** asserts exactly one row matches. SQL uses `TOP 2` (fetches two rows to detect duplicates). Throws `InvalidOperationException` if more than one row matches.

Use `FirstOrDefault()` for sorted lists (e.g., latest order). Use `SingleOrDefault()` when business logic requires uniqueness — looking up by email, username, or any column that *should* be unique.
</details>

<details><summary>Deep Dive</summary>

**The SQL difference**

```csharp
// FirstOrDefault — SQL Server
context.Users.Where(u => u.Email == email).FirstOrDefault();
// SELECT TOP(1) * FROM Users WHERE Email = 'x@x.com'

// SingleOrDefault — SQL Server
context.Users.Where(u => u.Email == email).SingleOrDefault();
// SELECT TOP(2) * FROM Users WHERE Email = 'x@x.com'
```

`SingleOrDefault` fetches **two** rows intentionally — if it gets two back, it knows there are multiple matches and throws. If it gets zero, returns null. If it gets one, returns it.

**Why `TOP(2)` not `TOP(1)`**

To throw on duplicates, EF must know if more than one row exists. Fetching `TOP(1)` would never detect duplicates. Fetching `TOP(2)` is the cheapest way to distinguish "exactly one" from "more than one."

**When uniqueness matters for correctness**

```csharp
// Email lookup — there should be exactly one user per email
// If there are duplicates, something is wrong — you WANT the exception
var user = await context.Users
    .SingleOrDefaultAsync(u => u.Email == email);

// Latest order — multiple orders are expected, just want the newest
var latest = await context.Orders
    .Where(o => o.CustomerId == id)
    .OrderByDescending(o => o.CreatedAt)
    .FirstOrDefaultAsync();
```

**The null vs exception matrix**

| Method | 0 rows | 1 row | 2+ rows |
|---|---|---|---|
| `First()` | throws | returns it | returns first |
| `FirstOrDefault()` | returns null | returns it | returns first |
| `Single()` | throws | returns it | throws |
| `SingleOrDefault()` | returns null | returns it | throws |

**Practical rule**

- Primary key lookups: use `FindAsync(id)` (uses change tracker cache, then DB) — fastest option
- Unique column lookups (email, username): `SingleOrDefaultAsync` — detects data corruption
- Sorted/filtered lists where multiple matches are valid: `FirstOrDefaultAsync`
- Never use `Single/First` without `OrDefault` unless you're certain a row must exist and an exception on miss is correct behavior
</details>

---

**B4.** What does `Include()` do in EF Core? What SQL does it generate?

<details><summary>Answer</summary>

`Include()` performs **eager loading** — it tells EF to load a navigation property in the same query as the parent entity, using a SQL `JOIN`. Without it, navigation properties are `null` (in a non-tracking context) or are lazy-loaded only when accessed (requires proxies). `Include()` generates a `LEFT JOIN` between the parent and the related table, returning all data in one round-trip.
</details>

<details><summary>Deep Dive</summary>

**Without Include — navigation property is null**

```csharp
var order = await context.Orders.FirstOrDefaultAsync(o => o.Id == 1);
var items = order.OrderItems; // null — never loaded
```

**With Include**

```csharp
var order = await context.Orders
    .Include(o => o.OrderItems)
    .FirstOrDefaultAsync(o => o.Id == 1);

var items = order.OrderItems; // populated
```

**Generated SQL**

```sql
SELECT o.Id, o.CustomerId, o.CreatedAt,
       oi.Id, oi.OrderId, oi.ProductId, oi.Quantity, oi.Price
FROM Orders o
LEFT JOIN OrderItems oi ON oi.OrderId = o.Id
WHERE o.Id = 1
```

**ThenInclude — nested navigation**

```csharp
var orders = await context.Orders
    .Include(o => o.OrderItems)
        .ThenInclude(oi => oi.Product)  // load Product for each OrderItem
    .ToListAsync();
```

Generates:
```sql
SELECT o.*, oi.*, p.*
FROM Orders o
LEFT JOIN OrderItems oi ON oi.OrderId = o.Id
LEFT JOIN Products p ON p.Id = oi.ProductId
```

**When Include becomes a problem**

1. **Cartesian explosion**: include multiple collections on one entity — the JOIN multiplies rows
```csharp
.Include(o => o.OrderItems)
.Include(o => o.ShippingAddresses)  // each OrderItem × each Address
```
A `SplitQuery` can fix this by running separate queries per collection.

2. **Over-fetching**: you load 50 columns when you only need 3. Use `Select()` projections instead of `Include` when you don't need the full entity.

```csharp
// Better than Include for read-only views
var summaries = await context.Orders
    .Select(o => new OrderSummaryDto {
        Id = o.Id,
        ItemCount = o.OrderItems.Count,
        Total = o.OrderItems.Sum(i => i.Price * i.Quantity)
    })
    .ToListAsync();
```

**Split query option**

```csharp
var orders = await context.Orders
    .Include(o => o.OrderItems)
    .Include(o => o.Tags)
    .AsSplitQuery()   // runs separate SELECT per Include
    .ToListAsync();
```

EF runs: `SELECT * FROM Orders`, then `SELECT * FROM OrderItems WHERE OrderId IN (...)`, then `SELECT * FROM Tags WHERE OrderId IN (...)`. No row multiplication, but two extra round-trips.
</details>

---

**B5.** What is a migration in EF Core? What happens if you modify an entity but forget to create a migration?

<details><summary>Answer</summary>

A migration is a C# class EF generates that represents the delta between your current entity model and the last applied schema state. It contains `Up()` (apply the change) and `Down()` (reverse it) methods that translate to DDL SQL. If you modify an entity but don't create a migration, the in-memory model is out of sync with the database schema. Depending on your configuration: runtime `SqlException` on queries that reference the missing column, or silent data loss if columns are dropped from the model without a migration removing them from the DB.
</details>

<details><summary>Deep Dive</summary>

**The migration lifecycle**

```bash
# 1. Modify entity class
# 2. Generate migration
dotnet ef migrations add AddProductDescription

# 3. Review the generated file in Migrations/
# 4. Apply to DB
dotnet ef database update
```

**What a generated migration looks like**

```csharp
public partial class AddProductDescription : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "Products",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Description",
            table: "Products");
    }
}
```

**What happens if you skip the migration**

| Scenario | Result |
|---|---|
| Added a property, no migration | `SqlException: Invalid column name 'Description'` on first query touching it |
| Removed a property, no migration | Property silently ignored — column stays in DB (data preserved but invisible to EF) |
| Changed column type, no migration | Works until EF tries to materialize the wrong type → runtime cast exception |
| Added a required (non-nullable) property without default | `SqlException: Cannot insert NULL` on save |

**`EnsureCreated()` vs `Migrate()` — a common confusion**

```csharp
// EnsureCreated: creates DB from current model, NO migrations applied or tracked
// Useful only for tests with InMemory or throw-away SQLite
context.Database.EnsureCreated();

// Migrate: applies pending migrations, respects migration history
// Use this in production
context.Database.Migrate();
```

Never use `EnsureCreated()` in production — it creates the schema from scratch and doesn't interoperate with the migrations history table.

**Detecting model/DB drift at startup**

```csharp
// In startup — throws if migrations are pending
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<StoreContext>();
var pending = db.Database.GetPendingMigrations();
if (pending.Any())
    throw new InvalidOperationException($"Pending migrations: {string.Join(", ", pending)}");
```

Or auto-apply (acceptable in dev/staging, risky in production):
```csharp
db.Database.Migrate(); // applies all pending migrations on startup
```
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

<details><summary>Deep Dive</summary>

**Full timeline with SQL**

```
T1: Request A reads:  SELECT * FROM Products WHERE Id = 7  → stock = 5
T1: Request B reads:  SELECT * FROM Products WHERE Id = 7  → stock = 5

T2: Request A runs:   UPDATE Products SET QuantityInStock = 4
                      WHERE Id = 7 AND QuantityInStock = 5
                      → 1 row affected ✓  (DB now has stock = 4)

T3: Request B runs:   UPDATE Products SET QuantityInStock = 4
                      WHERE Id = 7 AND QuantityInStock = 5
                      → 0 rows affected ✗  (DB has 4, not 5)

T3: EF Core:          expected rowsAffected = 1, got 0
                      → throw DbUpdateConcurrencyException
```

**The exception's entry state**

```csharp
catch (DbUpdateConcurrencyException ex)
{
    var entry = ex.Entries.Single();

    // What Request B read at T1
    entry.OriginalValues["QuantityInStock"]  // = 5

    // What Request B tried to write
    entry.CurrentValues["QuantityInStock"]   // = 4

    // What's actually in the DB now (requires DB round-trip)
    var dbValues = await entry.GetDatabaseValuesAsync();
    dbValues["QuantityInStock"]              // = 4 (Request A's value)
}
```

**The lost update problem this prevents**

Without `[ConcurrencyCheck]`, both requests succeed:
- Stock should be `5 - 1 - 1 = 3`
- Actual result: `4` (Request B silently overwrote Request A)
- One sale was lost — inventory is wrong

With `[ConcurrencyCheck]`, Request B fails and must retry, eventually producing the correct `3`.

**Correct retry logic for stock decrement**

```csharp
private async Task DecrementStockWithRetry(int productId, int quantity, int maxRetries = 3)
{
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            var product = await context.Products.FindAsync(productId);
            if (product.QuantityInStock < quantity)
                throw new InvalidOperationException("Insufficient stock");

            product.QuantityInStock -= quantity;
            await context.SaveChangesAsync();
            return; // success
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (attempt == maxRetries - 1) throw; // give up

            // Reload from DB before retrying
            await ex.Entries.Single().ReloadAsync();
        }
    }
}
```

`entry.ReloadAsync()` refreshes both `OriginalValues` and `CurrentValues` from the DB, so the next attempt reads fresh stock before applying the decrement.
</details>

---

**B7.** What does `AsNoTracking()` do? When should you use it? What breaks if you use `AsNoTracking()` and then try to update the entity? What happens to `[ConcurrencyCheck]` if you read with `AsNoTracking()`?

<details><summary>Answer</summary>

- **What it does** — tells EF not to add returned entities to the change tracker. Faster reads (no snapshot, no identity map lookup), less memory. Use for read-only queries (DTOs, projections, display data).
- **If you try to update** — EF treats the entity as detached and throws `InvalidOperationException` unless you manually `Attach()` it and set the state.
- **`[ConcurrencyCheck]` + no-tracking** — EF can still read the token value, but doesn't store the original value in its tracking snapshot. If you Attach and update, you must manually set `OriginalValues` for the concurrency check to work correctly.
</details>

<details><summary>Deep Dive</summary>

**What the change tracker does normally**

When you load an entity, EF stores two copies in memory:
1. **Original values snapshot** — what came from the DB
2. **Current values** — the live object you're working with

On `SaveChanges()`, EF compares current vs original to generate the UPDATE. This costs memory and CPU — every tracked entity is stored twice.

**`AsNoTracking()` skips all of that**

```csharp
// Without AsNoTracking — entity is tracked, ~2x memory, snapshot stored
var products = await context.Products.ToListAsync();

// With AsNoTracking — entities returned but not stored in change tracker
var products = await context.Products.AsNoTracking().ToListAsync();
```

For read-heavy APIs (product listings, search results, admin reports), this is a significant optimization. A list of 1000 products uses ~half the memory, and the query executes slightly faster because EF skips identity resolution.

**What breaks when you try to update a no-tracking entity**

```csharp
var product = await context.Products.AsNoTracking()
    .FirstOrDefaultAsync(p => p.Id == 1);

product.Name = "Updated";
await context.SaveChangesAsync(); // no-op or exception — EF doesn't know about this entity
```

EF simply ignores the change — nothing was tracked, nothing is dirty. To update a no-tracking entity:

```csharp
context.Products.Update(product);  // marks ALL properties as modified
await context.SaveChangesAsync();
// Generates: UPDATE Products SET Name=..., Price=..., Stock=... WHERE Id=1
// Updates EVERY column, even unchanged ones — wasteful and potentially dangerous
```

Or attach and set state explicitly:
```csharp
context.Entry(product).State = EntityState.Modified;
await context.SaveChangesAsync(); // same result — all columns updated
```

**`[ConcurrencyCheck]` + `AsNoTracking()` — the trap**

```csharp
// No-tracking read
var product = await context.Products.AsNoTracking()
    .FirstOrDefaultAsync(p => p.Id == 1);
// product.QuantityInStock = 5

product.QuantityInStock = 4;
context.Products.Update(product); // attach, all properties modified

await context.SaveChangesAsync();
// EF generates WHERE: Id = 1 AND QuantityInStock = 4
//                                                    ↑
//                              WRONG — uses CURRENT value, not original!
//                              This is the value you just wrote, not what you read
```

Since EF has no snapshot, it uses the current value as the "original" — so the WHERE clause checks if `QuantityInStock = 4` (your new value), not `= 5` (what you read). This defeats the entire point of the concurrency check.

**Fix: manually set `OriginalValues`**

```csharp
var product = await context.Products.AsNoTracking()
    .FirstOrDefaultAsync(p => p.Id == 1);

int originalStock = product.QuantityInStock; // save before modifying
product.QuantityInStock = 4;

var entry = context.Products.Update(product);
entry.OriginalValues["QuantityInStock"] = originalStock; // restore original

await context.SaveChangesAsync();
// Now WHERE: Id = 1 AND QuantityInStock = 5 ← correct
```

**Rule of thumb**

- Use `AsNoTracking()` for GETs that return DTOs, never for entities you'll save
- If you need concurrency checks, either use tracked queries OR manage `OriginalValues` manually
</details>

---

**B8.** Compare `[ConcurrencyCheck]` and `[Timestamp]` (rowversion). When would you choose each? Which databases support `[Timestamp]`? What SQL does each generate?

<details><summary>Answer</summary>

- **`[ConcurrencyCheck]`** — uses an existing business column as the token; original value included in the WHERE clause. Portable across all databases, but requires the column to change detectably with every update.
- **`[Timestamp]`** (or `[RowVersion]`) — dedicated `rowversion`/`timestamp` binary column the database automatically increments on every update. More reliable (DB handles it), smaller WHERE clause (8-byte binary). SQL Server native — requires workarounds for PostgreSQL (`xmin`) or SQLite.
</details>

<details><summary>Deep Dive</summary>

**`[ConcurrencyCheck]` — SQL generated**

```csharp
public class Product
{
    public int Id { get; set; }

    [ConcurrencyCheck]
    public int QuantityInStock { get; set; }
}
```

```sql
UPDATE Products
SET QuantityInStock = @p0
WHERE Id = @p1 AND QuantityInStock = @p2
--                 ↑ original value included
```

Problems:
- If you update a column *other than* the checked one (e.g., Name), the stock value is still included in WHERE — but the DB stock may have changed for a valid reason. Can cause false concurrency conflicts.
- You must track the original value yourself (change tracker does this for you, but `AsNoTracking()` breaks it).

**`[Timestamp]` — SQL generated**

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }  // 8-byte opaque value
}
```

```sql
UPDATE Products
SET Name = @p0
WHERE Id = @p1 AND RowVersion = @p2
--                 ↑ 8-byte binary blob
```

The DB updates `RowVersion` automatically with every write — you never set it in code. It changes even if you update only one column.

**Database support**

| Database | Support | Type |
|---|---|---|
| SQL Server | Native | `rowversion` / `timestamp` |
| PostgreSQL | Via `xmin` system column | `uint` — needs Npgsql config |
| MySQL | Manual trigger or version column | No native equivalent |
| SQLite | No native support | Manual version column needed |

**PostgreSQL `xmin` approach**

```csharp
// Npgsql EF Core provider
modelBuilder.Entity<Product>()
    .Property(p => p.xmin)
    .HasColumnName("xmin")
    .HasColumnType("xid")
    .ValueGeneratedOnAddOrUpdate()
    .IsConcurrencyToken();
```

**When to choose which**

| Scenario | Use |
|---|---|
| SQL Server, simplest solution | `[Timestamp]` — DB handles everything |
| PostgreSQL | `xmin` via Npgsql, or a manual `Version int` with `[ConcurrencyCheck]` |
| Conflict detection on a specific field (stock, balance) | `[ConcurrencyCheck]` on that field — precise semantic |
| General "was this entity changed by anyone" | `[Timestamp]` — catches all changes |
| Portable across DBs (multi-provider) | `[ConcurrencyCheck]` on a `Version` int you increment manually |

**Manual version with `[ConcurrencyCheck]`**

```csharp
public class Product
{
    public int Id { get; set; }

    [ConcurrencyCheck]
    public int Version { get; set; }  // increment in service layer on every update
}
```

```csharp
product.Version++;
await context.SaveChangesAsync();
// WHERE Id = 1 AND Version = 3  (original was 3)
```

Works across all databases. More verbose but fully portable.
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

<details><summary>Deep Dive</summary>

**Why updating `OriginalValues` makes client win**

EF generates: `UPDATE ... WHERE Id = @id AND ConcurrencyToken = @original`

After the conflict, the DB has a new value — call it V2. Your `OriginalValues` still hold V1 (what you read), which no longer matches. The update fails again.

By calling `entry.OriginalValues.SetValues(dbValues)`, you update your snapshot to V2. Now EF generates:
`WHERE Id = @id AND ConcurrencyToken = V2` — which matches. Your `CurrentValues` (the change you want) remain unchanged. Your values overwrite V2. Client wins.

**Full robust implementation**

```csharp
private async Task SaveWithClientWinsAsync(int maxRetries = 3)
{
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            await context.SaveChangesAsync();
            return;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (attempt == maxRetries - 1)
                throw new Exception("Concurrency conflict unresolvable after max retries", ex);

            foreach (var entry in ex.Entries)
            {
                var dbValues = await entry.GetDatabaseValuesAsync();

                if (dbValues == null)
                {
                    // Another user deleted the entity — re-throw, client can't win against deletion
                    throw new InvalidOperationException(
                        $"Entity {entry.Metadata.Name} was deleted by another user.");
                }

                // Client wins: overwrite OriginalValues with what's in DB now
                // CurrentValues (our changes) remain intact
                entry.OriginalValues.SetValues(dbValues);
            }
            // Loop → retry SaveChangesAsync with updated OriginalValues
        }
    }
}
```

**"Database wins" for comparison**

```csharp
// Database wins: discard client changes, use DB values
foreach (var entry in ex.Entries)
{
    var dbValues = await entry.GetDatabaseValuesAsync();
    entry.CurrentValues.SetValues(dbValues);  // overwrite in-memory with DB
    entry.OriginalValues.SetValues(dbValues);
}
// Nothing changed → SaveChanges is a no-op or updates to same values
```

**When client wins is appropriate**

- Admin override: admin explicitly overrides a concurrent edit
- Authoritative source: a service has computed a value that should always win (e.g., fraud score update)
- Explicit user action: user clicks "Force Save" after being warned of conflict

**When client wins is dangerous**

- Stock management: silently overwriting another request's stock update causes lost sales
- Financial balances: client wins would ignore concurrent transactions
- For these, use merge strategies or reject with a user-visible conflict message
</details>

---

**B10.** What is the N+1 query problem? Give an example with Orders and OrderItems. How does `Include()` fix it? What's the downside of over-using `Include()`?

<details><summary>Answer</summary>

The N+1 problem is when you execute 1 query to fetch N parent entities, then execute N additional queries (one per parent) to load a related collection. With 100 orders, that's 101 queries. `Include()` collapses this into a single JOIN query. The downside of over-using `Include()` is fetching large amounts of data you don't need (over-fetching), and for multiple collections, a cartesian product that multiplies rows.
</details>

<details><summary>Deep Dive</summary>

**The N+1 in action**

```csharp
// N+1 — DO NOT DO THIS
var orders = await context.Orders.ToListAsync(); // 1 query → returns 100 orders

foreach (var order in orders)
{
    // EF lazy-loads OrderItems for EACH order — 100 separate queries!
    var total = order.OrderItems.Sum(i => i.Price * i.Quantity);
    Console.WriteLine($"Order {order.Id}: ${total}");
}
// Total: 101 queries for 100 orders
```

Each `order.OrderItems` access triggers:
```sql
SELECT * FROM OrderItems WHERE OrderId = 1
SELECT * FROM OrderItems WHERE OrderId = 2
...
SELECT * FROM OrderItems WHERE OrderId = 100
```

**Fixing with `Include()`**

```csharp
// 1 query with JOIN
var orders = await context.Orders
    .Include(o => o.OrderItems)
    .ToListAsync();

// OrderItems already in memory — no additional queries
foreach (var order in orders)
{
    var total = order.OrderItems.Sum(i => i.Price * i.Quantity);
}
```

```sql
SELECT o.*, oi.*
FROM Orders o
LEFT JOIN OrderItems oi ON oi.OrderId = o.Id
```

**Lazy loading — the silent N+1 enabler**

EF Core doesn't lazy-load by default (unlike EF6). To enable it:
```csharp
// Install: Microsoft.EntityFrameworkCore.Proxies
optionsBuilder.UseLazyLoadingProxies();
```

With lazy loading enabled, `order.OrderItems` auto-fetches on access — convenient but silently creates N+1 patterns. Most teams disable lazy loading in production and use explicit `Include()`.

**Downside 1: over-fetching**

```csharp
// Loading 1000 orders with all their items just to count them
var orders = await context.Orders
    .Include(o => o.OrderItems)  // fetches ALL item data
    .ToListAsync();

var total = orders.Sum(o => o.OrderItems.Count); // only needed the count!
```

Better:
```csharp
var total = await context.Orders.SumAsync(o => o.OrderItems.Count);
// SELECT SUM((SELECT COUNT(*) FROM OrderItems WHERE OrderId = o.Id)) FROM Orders
```

**Downside 2: cartesian explosion with multiple Includes**

```csharp
var orders = await context.Orders
    .Include(o => o.OrderItems)   // 5 items per order
    .Include(o => o.Tags)         // 3 tags per order
    .ToListAsync();
// JOIN produces 5 × 3 = 15 rows per order — massive duplication
```

For 100 orders: expected 100 × (5 + 3) = 800 data points, actual: 100 × 15 = 1500 rows transferred.

**Fix: `AsSplitQuery()`**

```csharp
var orders = await context.Orders
    .Include(o => o.OrderItems)
    .Include(o => o.Tags)
    .AsSplitQuery()  // 3 separate queries, no row multiplication
    .ToListAsync();
```

Runs:
```sql
SELECT * FROM Orders;
SELECT * FROM OrderItems WHERE OrderId IN (1, 2, ...);
SELECT * FROM Tags WHERE OrderId IN (1, 2, ...);
```

Three queries, no duplication. Trade-off: 3 round-trips instead of 1, and they're not atomic (data could change between queries).
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

<details><summary>Deep Dive</summary>

**Why this happens — IQueryable vs IEnumerable**

`context.Products` is `IQueryable<Product>`. `IQueryable` builds an expression tree — the query hasn't run yet. Calling `ToListAsync()` executes the query and returns `List<Product>` — now it's `IEnumerable<Product>` in memory.

Once it's `IEnumerable`, `.Where()` is LINQ to Objects — it runs a C# predicate, not a SQL filter. The database has already done its job.

**What SQL each version generates**

```csharp
// Version 1 (broken):
var products = await context.Products.ToListAsync();
// SQL: SELECT * FROM Products  ← all rows!
var expensive = products.Where(p => p.Price > 100).ToList();
// No SQL — pure C# filtering in memory

// Version 2 (correct):
var expensive = await context.Products.Where(p => p.Price > 100).ToListAsync();
// SQL: SELECT * FROM Products WHERE Price > 100
```

**Scale of the problem**

| Product count | V1 (all rows) | V2 (filtered) |
|---|---|---|
| 1,000 | ~200KB transferred | ~5KB (50 matches) |
| 100,000 | ~20MB transferred | ~500KB |
| 1,000,000 | ~2GB transferred | ~5MB |

**Common variations of the same bug**

```csharp
// Bug — filter after AsEnumerable()
var result = context.Products.AsEnumerable().Where(p => p.Price > 100);

// Bug — LINQ method that breaks IQueryable chain
var result = context.Products
    .ToList()          // ← materializes here
    .OrderBy(p => p.Price)
    .Where(p => p.Price > 100);

// Bug — calling a C# method EF can't translate (it silently switches to client eval)
var result = context.Products
    .Where(p => MyLocalMethod(p.Price))  // ← might cause client-side eval
    .ToList();
```

**How to detect it**

Enable EF query logging in development:

```csharp
optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information)
              .EnableSensitiveDataLogging();
```

Watch for `SELECT * FROM Products` with no WHERE clause when you expected filtering. Or use `ToQueryString()`:

```csharp
var query = context.Products.Where(p => p.Price > 100);
Console.WriteLine(query.ToQueryString()); // prints the SQL before executing
```
</details>

---

**B12.** What is the Specification pattern? Why use it instead of putting query logic directly in the repository? What are the trade-offs?

<details><summary>Answer</summary>

The Specification pattern encapsulates a query's criteria (filter, sort, pagination, includes) in a reusable object. Instead of adding a new method to the repository for every query variation, callers build a specification and pass it to a generic `GetAsync(spec)` method. It keeps repositories thin, makes queries composable and testable in isolation, and avoids repository method explosion. Trade-off: adds abstraction overhead — for simple CRUD it's overkill.
</details>

<details><summary>Deep Dive</summary>

**The repository method explosion problem**

```csharp
// Without Specification — repository grows unbounded
interface IProductRepository
{
    Task<IEnumerable<Product>> GetAllAsync();
    Task<IEnumerable<Product>> GetByCategoryAsync(string category);
    Task<IEnumerable<Product>> GetByCategoryWithBrandAsync(string category);
    Task<IEnumerable<Product>> GetByCategoryWithBrandSortedByPriceAsync(string category);
    Task<IEnumerable<Product>> GetPagedByCategoryAsync(string category, int skip, int take);
    // ... grows forever
}
```

Every new query need requires a new repository method, often with near-duplicate logic.

**Specification base class**

```csharp
public abstract class BaseSpecification<T>
{
    public Expression<Func<T, bool>>? Criteria { get; protected set; }
    public List<Expression<Func<T, object>>> Includes { get; } = new();
    public Expression<Func<T, object>>? OrderBy { get; protected set; }
    public Expression<Func<T, object>>? OrderByDescending { get; protected set; }
    public int Take { get; protected set; }
    public int Skip { get; protected set; }
    public bool IsPagingEnabled { get; protected set; }

    protected void AddInclude(Expression<Func<T, object>> include)
        => Includes.Add(include);

    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip; Take = take; IsPagingEnabled = true;
    }
}
```

**A concrete specification**

```csharp
public class ProductsWithFiltersAndPagingSpec : BaseSpecification<Product>
{
    public ProductsWithFiltersAndPagingSpec(ProductSpecParams p)
    {
        AddInclude(x => x.ProductBrand);
        AddInclude(x => x.ProductType);

        if (!string.IsNullOrEmpty(p.Search))
            Criteria = x => x.Name.ToLower().Contains(p.Search.ToLower());

        if (p.BrandId.HasValue)
            Criteria = Criteria is null
                ? x => x.ProductBrandId == p.BrandId
                : Criteria.And(x => x.ProductBrandId == p.BrandId);

        OrderBy = p.Sort == "priceAsc"
            ? x => x.Price
            : null;

        ApplyPaging((p.PageIndex - 1) * p.PageSize, p.PageSize);
    }
}
```

**The generic repository evaluator**

```csharp
public static class SpecificationEvaluator<T> where T : class
{
    public static IQueryable<T> GetQuery(IQueryable<T> query, BaseSpecification<T> spec)
    {
        if (spec.Criteria != null)
            query = query.Where(spec.Criteria);

        query = spec.Includes.Aggregate(query, (q, i) => q.Include(i));

        if (spec.OrderBy != null)
            query = query.OrderBy(spec.OrderBy);
        else if (spec.OrderByDescending != null)
            query = query.OrderByDescending(spec.OrderByDescending);

        if (spec.IsPagingEnabled)
            query = query.Skip(spec.Skip).Take(spec.Take);

        return query;
    }
}
```

**The thin repository**

```csharp
public class GenericRepository<T> where T : class
{
    public async Task<IEnumerable<T>> GetAllWithSpecAsync(BaseSpecification<T> spec)
    {
        return await SpecificationEvaluator<T>
            .GetQuery(context.Set<T>().AsQueryable(), spec)
            .ToListAsync();
    }
}
```

**Caller usage**

```csharp
var spec = new ProductsWithFiltersAndPagingSpec(queryParams);
var products = await repo.GetAllWithSpecAsync(spec);
```

**Trade-offs**

| Pro | Con |
|---|---|
| Thin repositories, no method explosion | Extra abstractions to learn and maintain |
| Specifications are unit-testable in isolation | Over-engineering for simple CRUD |
| Composable — combine specs via `.And()`, `.Or()` | Specification evaluator becomes a bottleneck for complex queries |
| Single `GetAsync(spec)` covers all query shapes | Complex specs can hide performance issues |
</details>

---

**B13.** What is the difference between `IQueryable<T>` and `IEnumerable<T>` when used with EF Core? If your repository returns `IEnumerable<Product>` and the caller does `.Where(p => p.Price > 100)`, where does the filtering happen? Why is this a performance disaster?

<details><summary>Answer</summary>

`IQueryable<T>` represents an unexecuted query — EF translates further LINQ operators into SQL. `IEnumerable<T>` is already materialized data in memory — any further LINQ is C# iteration, not SQL.

If the repository returns `IEnumerable<Product>`, the entire `Products` table is loaded into memory first. The caller's `.Where(p => p.Price > 100)` then runs as LINQ to Objects — filtering 1 million rows in memory instead of asking the database to return only matching rows. It's a full-table scan in application memory over the network.
</details>

<details><summary>Deep Dive</summary>

**How EF defers query execution**

```csharp
IQueryable<Product> query = context.Products; // no SQL yet

query = query.Where(p => p.Price > 100);      // adds to expression tree, no SQL
query = query.OrderBy(p => p.Name);           // still no SQL
query = query.Skip(0).Take(20);               // still no SQL

var result = await query.ToListAsync();        // NOW SQL executes:
// SELECT * FROM Products WHERE Price > 100 ORDER BY Name OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
```

The key: `IQueryable` builds an expression tree. Every LINQ method call appends to that tree. SQL is only generated when you materialize (`ToList`, `ToArray`, `FirstOrDefault`, etc.).

**The `IEnumerable` repository leak**

```csharp
// Repository — returns IEnumerable (wrong)
public IEnumerable<Product> GetAll()
{
    return context.Products; // implicit cast to IEnumerable — EXECUTES the query here
}

// Caller
var products = repo.GetAll();            // all products in memory
var filtered = products.Where(p => p.Price > 100); // too late — C# iteration
```

When `context.Products` is assigned to `IEnumerable<Product>`, the implicit conversion from `IQueryable` to `IEnumerable` calls `GetEnumerator()`, which executes the SQL. Everything after that is in-process.

**What SQL is generated in each case**

```csharp
// IQueryable returned from repo — SQL has WHERE
IQueryable<Product> query = repo.GetAll(); // returns IQueryable
var result = query.Where(p => p.Price > 100).ToList();
// SQL: SELECT * FROM Products WHERE Price > 100

// IEnumerable returned from repo — SQL has no WHERE
IEnumerable<Product> products = repo.GetAll(); // query executed, full table in memory
var result = products.Where(p => p.Price > 100).ToList();
// SQL: SELECT * FROM Products   ← full table scan!
```

**The correct pattern — return `IQueryable` for composable queries**

```csharp
// Repository returns IQueryable
public IQueryable<Product> GetAll()
{
    return context.Products.AsNoTracking(); // still deferred
}

// Caller composes freely
var result = await repo.GetAll()
    .Where(p => p.Price > 100)
    .OrderBy(p => p.Name)
    .Skip(page * pageSize)
    .Take(pageSize)
    .ToListAsync();
// All operators translated to a single efficient SQL query
```

**Trade-off of exposing `IQueryable`**

Returning `IQueryable` from a repository leaks the data access concern into the calling layer — callers can add arbitrary `.Include()`, `.Join()`, or even `.FromSqlRaw()`. This breaks the repository abstraction.

The Specification pattern (B12) solves this tension: keep repositories returning `IEnumerable`/`IReadOnlyList` (clean abstraction), but accept a `Specification` object that builds the `IQueryable` internally before materializing. Best of both worlds.

**Quick reference table**

| | `IQueryable<T>` | `IEnumerable<T>` |
|---|---|---|
| Where is LINQ executed? | SQL (database) | C# (memory) |
| When does query run? | On materialization | On enumeration |
| Further filtering | Added to SQL | In-process iteration |
| Good for | Composable repos | Final in-memory operations |
| Memory cost | Low (only results) | High (full table) |
</details>

---

### HARD

**B14.** You have a Unit of Work with `BeginTransactionAsync()`. Inside the transaction, you decrement stock (with concurrency check), create an order, and insert a payment record. The stock update succeeds, the order insert succeeds, but a subsequent write to Redis fails with a network timeout. What happens to the SQL transaction? What happens to the stock? Design the correct rollback behavior.

<details><summary>Answer</summary>

The SQL transaction is independent of Redis. If you don't explicitly call `transaction.RollbackAsync()`, the SQL changes are either committed or left open depending on your code flow. If you catch the Redis exception and throw without rolling back, SQL Server eventually rolls back when the connection is closed — but this is non-deterministic. The correct design: catch all exceptions inside the transaction block, call `RollbackAsync()` explicitly, and handle the Redis failure as a separate concern (outbox pattern or fire-and-forget with retry). The SQL database is the source of truth; Redis is a cache.
</details>

<details><summary>Deep Dive</summary>

**The typical broken pattern**

```csharp
await using var transaction = await context.Database.BeginTransactionAsync();

try
{
    // SQL operations — all succeed
    product.QuantityInStock -= quantity;
    context.Orders.Add(newOrder);
    await context.SaveChangesAsync();

    // Redis — fails after SQL committed
    await redis.HashSetAsync($"cart:{userId}", ...); // throws SocketException

    await transaction.CommitAsync(); // never reached
}
catch (Exception ex)
{
    // Transaction not explicitly rolled back
    // SQL state: SaveChanges ran but CommitAsync didn't
    // ← stock decremented in the DB? Depends on isolation level and connection handling
    throw;
}
```

In SQL Server with the default transaction isolation, `SaveChangesAsync()` writes to the transaction's in-memory buffer — not committed to disk yet. If `CommitAsync()` is never called and the connection closes, SQL Server rolls back automatically. But relying on this is fragile — connection pooling can delay closure.

**The correct transaction block**

```csharp
await using var transaction = await context.Database.BeginTransactionAsync();

try
{
    // All SQL work
    product.QuantityInStock -= quantity;
    context.Orders.Add(newOrder);
    await context.SaveChangesAsync();

    await transaction.CommitAsync(); // commit SQL atomically
}
catch (Exception)
{
    await transaction.RollbackAsync(); // explicit rollback on ANY failure
    throw;
}

// Redis OUTSIDE the transaction — it's not transactional anyway
try
{
    await redis.DeleteAsync($"cart:{userId}");
}
catch (Exception ex)
{
    // Redis failure is non-fatal — log and move on
    // Cart will expire naturally or be cleaned up by a background job
    logger.LogWarning(ex, "Failed to delete cart from Redis for user {UserId}", userId);
}
```

**Why Redis doesn't belong inside the SQL transaction**

Redis has no concept of SQL transactions. `BeginTransactionAsync()` is a SQL Server construct. Including Redis operations "inside" the transaction block gives a false impression of atomicity — Redis and SQL can't commit or roll back together.

**The Outbox Pattern — guaranteed eventual consistency**

For operations that must survive failures (sending order confirmation email, notifying inventory service), use the outbox pattern:

```csharp
// Inside the SQL transaction — write an "outbox event" to the DB
context.OutboxMessages.Add(new OutboxMessage {
    Type = "OrderCreated",
    Payload = JsonSerializer.Serialize(orderCreatedEvent),
    CreatedAt = DateTime.UtcNow
});
await context.SaveChangesAsync(); // outbox event commits atomically with the order

await transaction.CommitAsync();
// Order + outbox event are in the DB — atomically

// A background worker reads OutboxMessages and processes them
// If Redis call fails, the worker retries — it re-reads from OutboxMessages
```

This guarantees that even if the process crashes after the SQL commit, the event will eventually be processed. The outbox row acts as a durable task queue.

**What actually happens to stock without explicit rollback**

| Scenario | Stock outcome |
|---|---|
| Redis fails → explicit `RollbackAsync()` | Stock unchanged ✓ |
| Redis fails → no rollback → connection returned to pool | SQL Server detects orphaned transaction → auto-rollback. Stock unchanged, but non-deterministic timing |
| Redis fails → `CommitAsync()` already called | Stock decremented, order created, cart not deleted. Need compensating transaction or retry for Redis |
| Full crash (power off) mid-commit | SQL Server WAL (write-ahead log) ensures atomicity → stock unchanged if commit didn't complete |
</details>

---

**B15.** EF Core's `InMemoryDatabase` doesn't enforce foreign keys, unique constraints, or concurrency tokens. You wrote a concurrency test using InMemory and it passed — but the same scenario fails in production. Explain why InMemory gave a false positive. What testing database should you use instead? What are the trade-offs?

<details><summary>Answer</summary>

`InMemoryDatabase` is a simple in-memory dictionary — it simulates basic entity storage but doesn't implement SQL semantics. Concurrency tokens (`[ConcurrencyCheck]`, `[Timestamp]`) are not checked during saves. Two simultaneous saves to the same entity both succeed without `DbUpdateConcurrencyException`. In production (SQL Server), the WHERE clause with the original value fails to match and the exception fires. Use **SQLite in-process with WAL mode** for tests — it's a real SQL engine, enforces constraints, and supports concurrency tokens, while still running in-memory or from a temp file.
</details>

<details><summary>Deep Dive</summary>

**Why InMemory doesn't enforce concurrency**

EF Core's `InMemoryDatabase` stores entities as dictionaries of property values. When `SaveChanges` runs, it:
1. Finds the entity by primary key in the dictionary
2. Overwrites its values
3. Returns success

There is no concept of "check the original value in a WHERE clause." Concurrency tokens exist in the model metadata, but InMemory simply ignores them during save. Both concurrent saves succeed — the second silently overwrites the first.

**The false positive test**

```csharp
// Tests pass with InMemory — concurrency "conflict" never fires
[Fact]
public async Task TwoRequestsShouldConflict()
{
    var options = new DbContextOptionsBuilder<StoreContext>()
        .UseInMemoryDatabase("TestDb")
        .Options;

    using var ctx1 = new StoreContext(options);
    using var ctx2 = new StoreContext(options);

    var p1 = await ctx1.Products.FindAsync(1);
    var p2 = await ctx2.Products.FindAsync(1);

    p1.QuantityInStock = 4;
    p2.QuantityInStock = 3;

    await ctx1.SaveChangesAsync(); // succeeds
    await ctx2.SaveChangesAsync(); // ALSO succeeds — no exception! false negative

    // Test expects DbUpdateConcurrencyException, never gets it
    // Test passes but production fails
}
```

**SQLite in-process — the right choice**

```csharp
// NuGet: Microsoft.EntityFrameworkCore.Sqlite

private static DbContextOptions<StoreContext> CreateSqliteOptions()
{
    var connection = new SqliteConnection("DataSource=:memory:");
    connection.Open(); // keep connection open — in-memory DB lives as long as connection

    return new DbContextOptionsBuilder<StoreContext>()
        .UseSqlite(connection)
        .Options;
}

[Fact]
public async Task TwoRequestsShouldConflict()
{
    var options = CreateSqliteOptions();

    using var ctx = new StoreContext(options);
    ctx.Database.EnsureCreated(); // creates schema in SQLite

    // SQLite enforces concurrency tokens — test now behaves like production
    // ...
}
```

**What SQLite supports vs InMemory**

| Feature | InMemory | SQLite |
|---|---|---|
| Foreign key constraints | No | Yes (with `PRAGMA foreign_keys = ON`) |
| Unique constraints | No | Yes |
| Concurrency tokens | No | Yes |
| CHECK constraints | No | Yes |
| Real SQL translation | No (LINQ only) | Yes |
| Transactions | Limited | Yes |
| Speed | Fastest | Fast (in-memory mode) |
| Portability | Only for unit tests | Tests + lightweight dev DB |

**SQLite limitations vs SQL Server**

| SQL Server feature | SQLite equivalent |
|---|---|
| `rowversion` / `[Timestamp]` | Not supported — use `[ConcurrencyCheck]` with a version int |
| Full-text search | Basic LIKE only |
| Stored procedures | Not supported |
| Window functions | Supported in SQLite 3.25+ |
| `NEWID()`, `GETDATE()` | Different functions |

**Practical test strategy**

- **Unit tests**: InMemory for pure logic tests (no SQL validation needed)
- **Integration tests**: SQLite in-memory for constraint/concurrency testing
- **End-to-end tests**: SQL Server via Testcontainers (Docker) — identical to production
- **CI pipeline**: SQLite for fast integration tests, Testcontainers for full E2E suite

```csharp
// Testcontainers — SQL Server in Docker for true production parity
var container = new MsSqlBuilder().Build();
await container.StartAsync();
var connStr = container.GetConnectionString();
```
</details>

---

**B16.** You have 10 million products. Your query filters by `Category`, sorts by `Price`, and paginates (`Skip(100).Take(20)`). The query takes 3 seconds. Explain the execution plan, identify the bottleneck, and design the correct index. What's the difference between a covering index and a filtered index here?

<details><summary>Answer</summary>

Without an index, SQL Server does a full table scan of 10 million rows, then sorts all matching rows by price in a temporary work area (sort spill), then offsets 100 and returns 20 — processing millions of rows for a 20-row result.

The bottleneck is the full scan + sort. The fix is a **composite index on `(CategoryId, Price)`** — SQL Server can use it to seek directly to the category, already sorted by price, then skip 100 and take 20. A **covering index** adds all projected columns to the index itself (using `INCLUDE`) so no table lookup is needed. A **filtered index** is an index with a WHERE clause, useful if most queries target a specific subset (e.g., only active products).
</details>

<details><summary>Deep Dive</summary>

**What SQL EF generates**

```sql
SELECT p.Id, p.Name, p.Price, p.Description, p.ImageUrl
FROM Products p
WHERE p.CategoryId = 5
ORDER BY p.Price
OFFSET 100 ROWS FETCH NEXT 20 ROWS ONLY
```

**Without an index — the 3-second execution plan**

```
Table Scan (10M rows)           ← scans every row
    → Filter (CategoryId = 5)   ← keeps ~50k matching rows in memory
        → Sort (by Price)       ← sorts 50k rows in TempDB (sort spill to disk if >25MB)
            → Top (skip 100, take 20)  ← discards first 100, returns 20
```

Every query scans 10 million rows. Sorting 50k rows in TempDB spills to disk. For high concurrency, this creates I/O contention.

**Composite index on `(CategoryId, Price)`**

```sql
CREATE INDEX IX_Products_Category_Price
ON Products (CategoryId, Price);
```

Now the execution plan:

```
Index Seek (CategoryId = 5)     ← jumps directly to category 5 rows
    → already sorted by Price   ← no sort step needed — index order is used
        → RID Lookup (20 rows)  ← fetches remaining columns for only 20 rows
```

Time: microseconds. The index stores rows pre-sorted by CategoryId then Price. `OFFSET 100 FETCH NEXT 20` walks the index leaf pages.

**Why column order in the composite index matters**

`(CategoryId, Price)` — correct for this query: filter by category first, then sort within category.

`(Price, CategoryId)` — wrong: SQL Server can't seek by CategoryId efficiently since it's the second column. It would scan all prices and filter on CategoryId.

Rule: **equality columns first, range/sort columns last.**

**Covering index — eliminates table lookups**

After the index seek finds the right 20 rows by `(CategoryId, Price)`, SQL Server must fetch `Name`, `Description`, `ImageUrl` from the actual table row (RID lookup or Key lookup). With 20 rows this is fast, but under high concurrency, these random I/O lookups add up.

A covering index includes all needed columns:

```sql
CREATE INDEX IX_Products_Category_Price_Covering
ON Products (CategoryId, Price)
INCLUDE (Id, Name, ImageUrl);  -- added to leaf pages, not key
```

Now SQL Server satisfies the entire query from the index alone — no table lookup at all.

**`INCLUDE` vs key column — what's the difference?**

- **Key columns** (`CategoryId`, `Price`) are part of the B-tree — used for seeking and ordering
- **INCLUDE columns** (`Name`, `ImageUrl`) are stored only in leaf pages — available after seek, not searchable

You don't put `Name` in the key because queries don't filter/sort by it. Putting all columns in the key would create a massive index that hurts write performance.

**Filtered index — for partial table queries**

If 90% of your queries only show active products:

```sql
CREATE INDEX IX_Products_Active_Category_Price
ON Products (CategoryId, Price)
INCLUDE (Name, ImageUrl)
WHERE IsDeleted = 0 AND IsActive = 1;
```

This index contains only ~1 million rows instead of 10 million (assuming 10% active). Smaller index = faster seeks, less memory, faster inserts/updates for inactive products (no index maintenance needed).

**When EF creates indexes automatically**

EF Core auto-creates an index for:
- Every foreign key column (e.g., `CategoryId`)
- Unique constraints

But it doesn't auto-create composite or covering indexes — you configure these explicitly:

```csharp
modelBuilder.Entity<Product>()
    .HasIndex(p => new { p.CategoryId, p.Price })
    .HasDatabaseName("IX_Products_Category_Price");
```

**EF pagination performance note: keyset vs offset**

`Skip(10000).Take(20)` still scans 10,020 rows even with an index — just index rows. For deep pagination, keyset pagination is faster:

```csharp
// Instead of Skip(pageIndex * pageSize)
// Use: WHERE Price > @lastSeenPrice AND CategoryId = @cat ORDER BY Price LIMIT 20
// → always reads exactly 20 index entries, regardless of page depth
```
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

<details><summary>Deep Dive</summary>

**1. No concurrency control — last write wins**

```csharp
public class UserPreference
{
    public int Id { get; set; }
    public string Theme { get; set; }  // no token
}
```

```sql
-- Simple: only WHERE on PK
UPDATE UserPreferences SET Theme = 'dark' WHERE Id = 42
```

Two concurrent updates both succeed. Second writer overwrites first. For low-stakes data where the latest value is always the "correct" one, this is fine. For anything financial or inventory-related, it's a bug.

**2. `[ConcurrencyCheck]` — semantic token**

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }

    [ConcurrencyCheck]
    public int QuantityInStock { get; set; }
}
```

```sql
-- Read:
SELECT Id, Name, QuantityInStock FROM Products WHERE Id = 7
-- Returns: QuantityInStock = 5

-- Update attempt:
UPDATE Products
SET QuantityInStock = 4
WHERE Id = 7
  AND QuantityInStock = 5    ← concurrency token

-- If another writer changed stock to 3 first:
-- → 0 rows affected → DbUpdateConcurrencyException
```

**Key nuance**: the token only triggers if the checked column changed. If someone updated `Name` concurrently, `QuantityInStock` is unchanged → no conflict detected. This is by design — `[ConcurrencyCheck]` detects conflict on that specific field only.

**3. `[Timestamp]` — universal token**

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int QuantityInStock { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }
}
```

SQL Server schema:
```sql
CREATE TABLE Products (
    Id int PRIMARY KEY,
    Name nvarchar(200),
    QuantityInStock int,
    RowVersion rowversion NOT NULL  -- auto-incremented by SQL Server on every UPDATE
);
```

```sql
-- Read:
SELECT Id, Name, QuantityInStock, RowVersion FROM Products WHERE Id = 7
-- Returns: RowVersion = 0x0000000000000003

-- Update attempt:
UPDATE Products
SET Name = 'Updated Name'
WHERE Id = 7
  AND RowVersion = 0x0000000000000003   ← checks opaque binary token

-- If ANY column was changed by another writer (RowVersion is now 0x0000000000000004):
-- → 0 rows affected → DbUpdateConcurrencyException
```

**`[Timestamp]` catches what `[ConcurrencyCheck]` misses**

```
User A reads: Name = "Widget", Stock = 5, RowVersion = 3
User B reads: Name = "Widget", Stock = 5, RowVersion = 3

User B changes Name to "Super Widget", saves → RowVersion becomes 4

User A changes Stock to 4, tries to save:
  - With [ConcurrencyCheck] on Stock: WHERE Stock = 5 → matches (B didn't touch Stock) → saves → Name reverted to "Widget" ← LOST UPDATE
  - With [Timestamp]: WHERE RowVersion = 3 → doesn't match (B changed it to 4) → DbUpdateConcurrencyException ← DETECTED
```

**Side-by-side comparison**

| | No control | `[ConcurrencyCheck]` | `[Timestamp]` |
|---|---|---|---|
| Conflict detection | None | On checked column only | On any column change |
| SQL WHERE | PK only | PK + original column value | PK + 8-byte rowversion |
| DB support | All | All | SQL Server native |
| Write overhead | Lowest | Low | Low (DB handles token) |
| False negatives | Many | On unchecked columns | None |
| Use case | Low-stakes overwrites | Specific field semantics | General entity versioning |
</details>

---

**B18.** Design a retry strategy for `DbUpdateConcurrencyException` in a background order processor. Include: max retries, backoff, what to reload, when to give up. Write the code.

<details><summary>Answer</summary>

For a background processor, use exponential backoff with jitter (to avoid thundering herd), a max retry limit (3–5), reload both the entity and fresh DB state before each retry, and treat deletion (null `DatabaseValues`) as a terminal failure. Give up after max retries and either dead-letter the message or emit a structured failure event.
</details>

<details><summary>Deep Dive</summary>

**Why backoff matters in background processors**

A background processor handling orders competes with web requests for the same products. If 10 processor threads all retry immediately after a conflict, they generate a spike of competing writes. Exponential backoff with jitter spreads retries over time, reducing contention.

**The full retry implementation**

```csharp
public class OrderProcessor
{
    private readonly StoreContext _context;
    private readonly ILogger<OrderProcessor> _logger;

    private const int MaxRetries = 5;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(100);

    public async Task ProcessOrderAsync(int orderId, CancellationToken ct)
    {
        int attempt = 0;

        while (true)
        {
            try
            {
                await ExecuteOrderAsync(orderId, ct);
                return; // success
            }
            catch (DbUpdateConcurrencyException ex)
            {
                attempt++;

                if (attempt >= MaxRetries)
                {
                    _logger.LogError(ex,
                        "Concurrency conflict unresolvable after {Attempts} retries for order {OrderId}",
                        attempt, orderId);

                    // Dead-letter: persist failure for manual review or compensating action
                    await DeadLetterAsync(orderId, ex, ct);
                    return;
                }

                // Check if entity was deleted (can't retry)
                foreach (var entry in ex.Entries)
                {
                    var dbValues = await entry.GetDatabaseValuesAsync(ct);
                    if (dbValues == null)
                    {
                        _logger.LogWarning(
                            "Entity {Entity} deleted by another process — aborting order {OrderId}",
                            entry.Metadata.Name, orderId);
                        await DeadLetterAsync(orderId, ex, ct);
                        return;
                    }
                }

                // Exponential backoff with jitter: 100ms, 200ms, 400ms, 800ms, ...
                // Jitter ±25% to spread concurrent retries
                var delay = TimeSpan.FromMilliseconds(
                    BaseDelay.TotalMilliseconds
                    * Math.Pow(2, attempt - 1)
                    * (0.75 + Random.Shared.NextDouble() * 0.5));

                _logger.LogWarning(
                    "Concurrency conflict on attempt {Attempt}/{Max} for order {OrderId}. " +
                    "Retrying in {Delay}ms",
                    attempt, MaxRetries, orderId, (int)delay.TotalMilliseconds);

                await Task.Delay(delay, ct);

                // Detach all tracked entities — reload from DB on next attempt
                _context.ChangeTracker.Clear();
            }
        }
    }

    private async Task ExecuteOrderAsync(int orderId, CancellationToken ct)
    {
        // Reload entities fresh — critical after ChangeTracker.Clear()
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found");

        foreach (var item in order.OrderItems)
        {
            var product = await _context.Products.FindAsync(
                new object[] { item.ProductId }, ct)
                ?? throw new InvalidOperationException($"Product {item.ProductId} not found");

            if (product.QuantityInStock < item.Quantity)
                throw new InvalidOperationException(
                    $"Insufficient stock for product {item.ProductId}");

            product.QuantityInStock -= item.Quantity;
        }

        order.Status = OrderStatus.Confirmed;
        order.ProcessedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct); // may throw DbUpdateConcurrencyException
    }

    private async Task DeadLetterAsync(int orderId, Exception ex, CancellationToken ct)
    {
        // Persist failed order for manual review / alerting
        using var scope = /* create new scope to get a fresh context */ null!;
        // ... insert into FailedOrders table with error details
        _logger.LogError("Order {OrderId} dead-lettered: {Error}", orderId, ex.Message);
    }
}
```

**Why `ChangeTracker.Clear()` before retry**

After a `DbUpdateConcurrencyException`, the change tracker still holds the conflicting entity with stale `OriginalValues`. If you just reload the product with `FindAsync`, EF returns the cached (stale) tracked instance — you don't get fresh DB values.

`ChangeTracker.Clear()` detaches everything, forcing a real DB round-trip on the next read. This is the safest approach for a retry loop.

**Backoff values for 5 retries (with 0% jitter for clarity)**

| Attempt | Delay |
|---|---|
| 1 | 100ms |
| 2 | 200ms |
| 3 | 400ms |
| 4 | 800ms |
| 5 → give up | — |

With ±25% jitter, concurrent processors will spread their retries across a ~50ms window at each level, dramatically reducing thundering herd.

**When to give up vs when to keep retrying**

| Scenario | Action |
|---|---|
| Entity deleted | Give up immediately — retrying is meaningless |
| Persistent high contention (>3 failures) | Dead-letter — likely a data or logic issue |
| `OperationCanceledException` | Give up — host is shutting down |
| `DbUpdateException` (not concurrency) | Don't retry — it's a constraint violation, not a race |
| Max retries reached | Dead-letter + alert + manual review |
</details>

---
