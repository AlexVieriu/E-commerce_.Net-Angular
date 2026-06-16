## SECTION B — OPTIMISTIC CONCURRENCY WITH EF CORE

> **Source:** Task 13 — `[ConcurrencyCheck]` on `Product.QuantityInStock` + Fail-and-Notify strategy.

---

### EASY

**B1.** [HIGH] What is optimistic concurrency and how does it differ from pessimistic?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**
- **Pessimistic** — lock the row on read (`SELECT ... FOR UPDATE`), block other writers until you release. Good when conflicts are frequent, catastrophic for read-heavy workloads because readers queue.
- **Optimistic** — no lock; on save, verify the row hasn't changed since you read it. If it has, throw. Good when conflicts are rare (typical e-commerce).

In EF Core, `[ConcurrencyCheck]` / `[Timestamp]` implements optimistic concurrency via the WHERE clause of the UPDATE.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Optimistic doesn't lock, pessimistic does." True but trivial — you need to say when to choose each.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that optimistic concurrency is **useless if you never validate the result** — if code ignores the `DbUpdateConcurrencyException`, the row was never protected. Also noting that pessimistic locks create distributed-system deadlocks at scale.


</details>

---

**B2.** [MEDIUM] What does `[ConcurrencyCheck]` actually change in the generated SQL?

<details>
<summary>The 5% Answer</summary>

- Injects the property's **OriginalValue** (the snapshot EF captured at load time) into the UPDATE's WHERE clause
- Without `[ConcurrencyCheck]`:
```sql
UPDATE Products SET QuantityInStock = 0 WHERE Id = 1
```

With `[ConcurrencyCheck] QuantityInStock`:
```sql
UPDATE Products SET QuantityInStock = 0
WHERE Id = 1 AND QuantityInStock = 1
```

If zero rows are affected, EF Core throws `DbUpdateConcurrencyException`.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"It locks the row." Wrong — no lock at all. It's purely a WHERE-clause check.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `[ConcurrencyCheck]` adds **no schema change** — it's pure model metadata. `[Timestamp]`, in contrast, requires a new `rowversion` column.


</details>

---

### MEDIUM

**B3.** [HIGH] Walk me through the race scenario with two concurrent checkouts on `QuantityInStock = 1`. Show state at each step.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

```
T=0  stock in DB = 1
T=1  Ctx1.FindAsync(1) → Original=1, Current=1
T=1  Ctx2.FindAsync(1) → Original=1, Current=1
T=2  Ctx1: stock-=1 → Current=0, Original=1 (unchanged)
T=2  Ctx2: stock-=1 → Current=0, Original=1 (unchanged)
T=3  Ctx1.SaveChangesAsync():
       UPDATE Products SET stock=0 WHERE Id=1 AND stock=1 → 1 row ✓
       DB is now stock=0
T=4  Ctx2.SaveChangesAsync():
       UPDATE Products SET stock=0 WHERE Id=1 AND stock=1 → 0 rows ✗
       EF throws DbUpdateConcurrencyException
```

In `OrderService`, we catch it and rethrow `InvalidOperationException("Stock changed during checkout...")`, which `ExceptionMiddleware` maps to 400. The Angular error interceptor shows a snackbar.

**Production consequence:** without `[ConcurrencyCheck]`, both UPDATEs succeed, stock drops to -1, two orders exist for one item — you oversold.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Explaining only step 3 without the snapshot mechanism. Interviewers want to see you understand OriginalValue vs CurrentValue.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the race window is the time between `FindAsync` and `SaveChangesAsync` — minimising that window reduces conflict rate. Also that EF's InMemory provider enforces this check, which is why the unit test works without SQL.


</details>

---

**B4.** [MEDIUM] [OUT-OF-SCOPE] Compare `[ConcurrencyCheck]` vs `[Timestamp]` / `rowversion`. When should you use each?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

| Attribute | Granularity | Database | Use when |
|---|---|---|---|
| `[ConcurrencyCheck]` | Per-column | Any (SQL Server, SQLite, Postgres, InMemory) | You care about one specific column (stock, balance). Portable. No schema changes. |
| `[Timestamp]` (`rowversion`) | Entire row | SQL Server only | You want conflict detection on *any* column change. More robust but not portable. Requires a new binary column. |

For this project, `[ConcurrencyCheck]` is right: we only care about stock conflicts, and we target both SQL Server (Azure) and SQLite (local dev). `[Timestamp]` would break SQLite.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"`[Timestamp]` is better." Not universally — the SQL Server dependency breaks SQLite/Postgres compatibility. Always match the tool to the project constraints.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that Postgres has `xmin` as a system column that EF Core can use as a concurrency token without a schema change. Also that `[Timestamp]` is really a SQL Server `rowversion` which auto-increments on *every* write including untracked raw SQL.


</details>

---

**B5.** [MEDIUM] What three value states does `DbUpdateConcurrencyException.Entries[0]` expose and what can you do with them?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

```csharp
var entry = ex.Entries.First();
var proposed  = entry.CurrentValues;                    // what you tried to write
var original  = entry.OriginalValues;                   // what you read at load time
var inDb      = await entry.GetDatabaseValuesAsync();   // what's actually in DB now
```

Four resolution strategies flow from these three states:

| Strategy | Action | Use when |
|---|---|---|
| Client Wins | Overwrite DB with `proposed` | Low-impact fields (e.g. profile bio) |
| Store Wins | Reload from `inDb`, discard `proposed` | Idempotent background jobs |
| Merge | Combine `proposed` and `inDb` field-by-field | Collaborative editing of independent fields |
| Fail-and-Notify | Rethrow, let the user retry | Checkout, payments — accuracy > smoothness |


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Only mentioning try-catch. The interviewer wants you to show you understand EF exposes all three snapshots explicitly.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `GetDatabaseValuesAsync` is a **fresh round-trip to the DB** — don't call it in a hot loop. Also, in a retry policy, use the fresh value to decide whether to retry or escalate.


</details>

---

**B6.** [HIGH] Why Fail-and-Notify and not Store Wins for checkout? Give a concrete failure scenario.

<details>
<summary>The 5% Answer</summary>

- **Store Wins** = "accept the current DB value and continue" → **silent wrong-value acceptance** for checkout

- Scenario: Alice sees stock=1 at $50. Bob pays first → stock=0. Alice's save hits the concurrency exception. With Store Wins, code reloads stock=0… but can't create the order (no stock). The user thinks they bought the item but gets nothing.

- **Why Fail-and-Notify is the only safe path:**
  1. Throw → caught → rethrown as domain exception
  2. Return 400 with a clear message
  3. User sees snackbar: "Stock changed, please try again"
  4. User decides whether to retry with refreshed cart

- You cannot merge a sale. You cannot silently accept the server's state. You stop and ask the human.

- **Production consequence:** Store Wins for checkout means money taken but no order, or inventory goes negative


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Fail-and-Notify is safer." Correct but lazy — you need to describe why each other strategy is *wrong* for this specific flow.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Tying it to the real Stripe flow: if Stripe already confirmed payment, Store Wins would leave money taken without an order. That's a refund/support ticket waiting to happen. Fail-and-Notify combined with Task 17's `PaymentIntentId` clearing is the only coherent approach.


</details>

---

### HARD

**B7.** [HIGH] Find the race condition in this `PlaceOrder` method and fix it with the minimal diff:

```csharp
public async Task<Order> PlaceOrderAsync(int productId, int qty, string email)
{
    var product = await _context.Products.FindAsync(productId);
    if (product.QuantityInStock < qty)
        throw new InsufficientStockException();

    product.QuantityInStock -= qty;
    var order = new Order(email, product, qty);
    _context.Orders.Add(order);
    await _context.SaveChangesAsync();
    return order;
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**The race:** Two concurrent calls both pass the `QuantityInStock < qty` check (they read the same value), both decrement, both save. Final stock is wrong by one order; an item is sold twice.

**Why the check doesn't save us:** the check is logical, the save is physical. Between them, another transaction can commit.

**Minimal fix — mark the column `[ConcurrencyCheck]` and catch:**

```csharp
public class Product
{
    [ConcurrencyCheck]
    public int QuantityInStock { get; set; }
}

public async Task<Order> PlaceOrderAsync(int productId, int qty, string email)
{
    var product = await _context.Products.FindAsync(productId);
    if (product.QuantityInStock < qty)
        throw new InsufficientStockException();

    product.QuantityInStock -= qty;
    var order = new Order(email, product, qty);
    _context.Orders.Add(order);

    try
    {
        await _context.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        throw new InvalidOperationException(
            "Stock changed during checkout. Please try again.");
    }
    return order;
}
```

EF generates `UPDATE Products SET QuantityInStock = @new WHERE Id = @id AND QuantityInStock = @original`. The loser's UPDATE affects 0 rows → throws.

**Production consequence:** without this fix, at 100 concurrent checkouts on a popular item, you'll see ~10-20 oversells per flash-sale event.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Adding `lock(...)` in C# or wrapping in `BeginTransactionAsync`. The lock is in-process only (useless across replicas); a transaction alone doesn't prevent the race unless you raise isolation to SERIALIZABLE, which kills throughput.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `[ConcurrencyCheck]` protects **only the decrement**, not the business rule. If you also need to guard "don't accept orders for out-of-stock items", you still need the `< qty` check. Defense in depth.


</details>

---

**B8.** [HIGH] [OUT-OF-SCOPE] Your inventory service handles 10,000 concurrent checkouts per second during a flash sale. Optimistic concurrency causes 30% of requests to throw. How does the architecture fail, and what do you change?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Failure modes:**
1. **Conflict storm** — at 10k RPS on a single hot product, optimistic concurrency is mostly conflicts, not saves. Retry logic amplifies the load.
2. **Stripe orphans** — every conflict means a PaymentIntent was created (Task 16 scope) but no order. Stripe fills with orphaned intents.
3. **User experience** — users see "try again" spam on a flash sale they can't win.

**Changes:**
1. **Queue-based serialisation** — put checkout requests on a per-product queue (Azure Service Bus, one queue per SKU). Workers process serially. Conflicts disappear because there's one writer.
2. **Redis atomic decrement as pre-gate** — `DECR stock:123` atomic operation as a cheap gate before the EF flow. Returns the new value instantly; if < 0, reject before ever reaching SQL.
3. **Reserved stock with TTL** — on add-to-cart, reserve stock in Redis for 5 minutes with a sliding expiry. Checkout completes the reservation. Avoids conflicts entirely because the reservation is the serialisation point.

**Tradeoffs:** Redis pre-gate is eventually consistent — stock count in SQL lags Redis briefly. For inventory, that's acceptable because the authoritative count is whatever completes the order, not the display number.

**Production consequence:** retry-loop architecture at flash-sale scale creates retry avalanches and DB connection pool exhaustion. Pre-gating in Redis flattens the load curve.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Add a retry policy with exponential backoff." That's first-aid — it doesn't solve conflict storms, only spreads them over time.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **eventual consistency tradeoffs**: the Redis count is the source of truth for "can I buy?", SQL is the source of truth for "did I buy?". Also mentioning Shopify's published architecture — they use a similar Redis/queue gate for flash-sale products.


</details>

---

**B9.** [MEDIUM] `CreateOrderAsync_ShouldThrow_WhenStockChangedDuringConcurrentCheckout` — your unit test uses the EF Core **InMemory** provider. Is that correct for this test? What would SQLite in-memory give you that InMemory doesn't?

<details>
<summary>The 5% Answer</summary>

- **InMemory is correct for this specific test** — we're testing EF Core's concurrency token behaviour, not SQL behaviour
- Since EF Core 5, InMemory enforces concurrency tokens: context2's save compares the original tracked value against the store value → throws `DbUpdateConcurrencyException` — exactly what we assert

- **What SQLite in-memory gives you that InMemory doesn't:**
  - Real SQL execution (catches generated-query bugs)
  - Real FK constraints (catches cascade delete bugs)
  - Real transaction rollback (catches "I assumed rollback worked" bugs)
  - Real `UNIQUE` / `CHECK` constraints (catches schema-mismatch bugs)

**When to pick which:**

| InMemory | SQLite in-memory |
|---|---|
| Testing EF Core behaviour (concurrency, change tracker) | Testing FK / cascade / unique constraints |
| Speed > SQL fidelity | SQL fidelity > speed |
| Narrow, isolated test | Would silently pass on InMemory but fail in prod |

For the concurrency test, InMemory is right. For Task 18's unique-index test, **SQLite in-memory would be better** — InMemory doesn't enforce unique indexes, so that test might pass on a provider that doesn't actually exercise the constraint.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Always use SQLite." More fidelity isn't always better — it adds schema setup cost for tests that don't need it.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the real production DB is SQL Server — so the test pyramid should include **integration tests on real SQL Server** (via Testcontainers or LocalDB) for the high-confidence layer, with InMemory/SQLite as fast inner-loop tests.


</details>

---

**B10.** [MEDIUM] Walk me through the error chain from `DbUpdateConcurrencyException` all the way to a red snackbar in the user's browser. Include every exception mapping and middleware.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

```
1. EF Core detects 0 rows affected → throws DbUpdateConcurrencyException in SaveChangesAsync

2. OrderService.CreateOrderAsync catch block:
   throw new InvalidOperationException("Stock changed during checkout. Please try again.");

3. Request hits ExceptionMiddleware (API/Middleware/ExceptionMiddleware.cs):
   switch (ex)
   {
       case InvalidOperationException: status = 400; message = ex.Message; break;
       ...
   }
   Response: 400 Bad Request, body: { message: "Stock changed..." }

4. Browser receives 400 → Angular HttpClient emits HttpErrorResponse

5. error.interceptor.ts catches:
   if (err.status === 400 && !err.error.errors) snackbar.error(err.error.message);

6. SnackbarService calls MatSnackBar.open(message, 'Close', { duration: 5000, panelClass: 'snack-error' });

7. User sees: red snackbar with "Stock changed during checkout. Please try again."
```

**Why the translation at the service layer matters:**
- Clients shouldn't know EF Core exists (infrastructure leak)
- The message should be user-friendly, not `DbUpdateConcurrencyException: The database operation...`
- Different callers may want different status codes (background job vs HTTP request)


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Saying "EF throws, middleware catches, user sees an error." Missing the translation step and why it exists.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **`InvalidOperationException` is a poor choice** — it's too generic. A better design: a typed `StockConflictException` in `Core/Exceptions/` that `ExceptionMiddleware` maps explicitly to 409 (not 400) — 409 Conflict is semantically correct here, same as Task 17's `PriceChangedException`. Shows you'd refactor for precision.


</details>

---

**B11.** [MEDIUM] [OUT-OF-SCOPE] Is `[ConcurrencyCheck]` sufficient to prevent stock oversell in a two-replica deployment behind a load balancer?

<details>
<summary>The 5% Answer</summary>

- **Yes — the check happens in the database, not in the app**
- The WHERE clause `AND QuantityInStock = @original` is evaluated by SQL Server's storage engine, which serialises writes to the same row via latches
- No matter how many app replicas, only one UPDATE can succeed against the same `QuantityInStock = 1` WHERE condition

- **What `[ConcurrencyCheck]` does NOT protect against:**
  - **Stale reads across replicas** — both replicas load stock=1 and try to save; optimistic concurrency is designed exactly for this — one wins, one throws
  - **Business-logic races** — if the stock check is done in app code and the decrement column isn't marked, you oversell (see B7)
  - **Distributed transactions** — mixing SQL and Redis writes still isn't atomic


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"It's not safe across replicas because there's no shared lock." Wrong — the protection lives in the DB, which *is* the shared state. Replicas compete for the same row.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **read replicas** (read-only SQL replicas for scale) can return stale data briefly, so if your app reads from a replica and writes to the primary, the OriginalValue may be newer than what the replica returned. Usually fine (concurrency check on the primary catches it), but worth noting.


</details>

---

### ⚡ Syntax Quick-Fire

**BQ1.** [HIGH] EF Core: `FirstOrDefault()` vs `SingleOrDefault()` — which is correct when a unique index guarantees at most one result?

<details>
<summary>The 5% Answer</summary>

- **`SingleOrDefault()`** — validates the uniqueness invariant at runtime. Throws `InvalidOperationException` if more than one row is returned. With a unique index, this is the semantic contract: "exactly 0 or 1."
- **`FirstOrDefault()`** silently accepts duplicates — takes the first row, hides violations. If the index is ever missing from a migration, `First*` masks the bug.
- Use `Single*` whenever a DB constraint guarantees uniqueness. The exception on violation is a signal, not noise.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Always `FirstOrDefault` — it's safer." Lenient ≠ safer. It hides the very condition `Single*` is designed to catch.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing `SingleOrDefault` generates `TOP 2` in SQL — reads one extra row to verify uniqueness. For a unique-indexed column the extra read costs zero (index returns exactly 1 row). So correctness has negligible cost here.

</details>

---

**BQ2.** [HIGH] What does `AsNoTracking()` do and when should you use it?

<details>
<summary>The 5% Answer</summary>

- Skips the **change tracker** — EF doesn't snapshot entities for `SaveChanges` detection.
- **Benefit:** no `OriginalValues` allocation, no identity-map overhead per fetch.
- **Use:** read-only queries (catalogue listing, reporting, API reads).
- **Don't use:** entities you'll later modify — changes are invisible to `SaveChanges`.

```csharp
var products = await _context.Products
    .AsNoTracking()
    .Where(p => p.CategoryId == catId)
    .ToListAsync();
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Add it everywhere for performance." If you then modify and call `SaveChanges`, nothing is saved — silent data loss.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** `UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)` on `DbContextOptions` sets no-tracking as the global default with opt-in via `AsTracking()`. Cleaner for read-heavy contexts.

</details>

---

**BQ3.** [HIGH] C# `async Task` vs `async ValueTask` — when does `ValueTask` eliminate a heap allocation?

<details>
<summary>The 5% Answer</summary>

- **`Task`** always allocates on the heap, even for synchronous completion.
- **`ValueTask`** is a struct — no allocation when the method completes synchronously (cache hit, memoised result).
- Use `ValueTask` for **high-frequency paths** where most calls complete synchronously.
- **Footgun:** `ValueTask` is single-await only. Awaiting it twice, storing it in a field, or accessing it after the first await causes undefined behaviour. Use `.AsTask()` to convert if multiple awaits are needed.

```csharp
public ValueTask<Product?> GetCachedAsync(int id)
    => _cache.TryGetValue(id, out var p) ? new(p) : new(FetchFromDbAsync(id));
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Use `ValueTask` everywhere." For non-hot paths it adds complexity with no benefit.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Naming the three `ValueTask` misuse patterns: double-await, post-completion access, and field storage. These are real production async bugs that Task avoids entirely.

</details>

---
