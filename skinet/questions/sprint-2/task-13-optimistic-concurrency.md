# Task 13 — Optimistic Concurrency

## Why this approach

Two simultaneous checkouts read `QuantityInStock = 1`. Both pass the stock check. Both decrement to 0. Both save. Result: 2 orders created for 1 item — oversell.

The fix must make the second write **fail if the value changed since it was read**. That's optimistic concurrency: don't lock, but verify on save.

Pessimistic alternative (row lock) works but blocks all readers for the duration of the transaction — too expensive for a high-read product catalogue.

## What confused me

- Why doesn't the existing `if (QuantityInStock < item.Quantity)` check prevent this?
  Because both requests pass the check **before** either saves. The check and the save are not atomic.

- Does `[ConcurrencyCheck]` add a new column to the DB?
  No. It just changes the generated UPDATE statement to include the original value in the WHERE clause. No DDL change to the column itself (just a model annotation in the snapshot).

- Why does the InMemory provider work for this test?
  Since EF Core 5, InMemory enforces concurrency tokens. When context2 tries to save, InMemory compares the original tracked value against the current store value — mismatch → `DbUpdateConcurrencyException`.

## Explanation

### 1. The Change Tracker — how EF Core remembers what you read

When you call `FindAsync(1)`, EF Core does two things:
1. Fetches the row from the database
2. Stores a **snapshot of the original values** in the `ChangeTracker`

```
ChangeTracker entry for Product Id=1 (just loaded):
  CurrentValue:  { QuantityInStock = 1 }   ← what you work with in code
  OriginalValue: { QuantityInStock = 1 }   ← frozen snapshot from DB at load time
```

When you do `product.QuantityInStock -= 1`, only `CurrentValue` moves:

```
  CurrentValue:  { QuantityInStock = 0 }   ← modified
  OriginalValue: { QuantityInStock = 1 }   ← unchanged
```

### 2. What SQL is generated — with and without [ConcurrencyCheck]

Without the attribute, `SaveChangesAsync` generates last-write-wins:
```sql
UPDATE Products SET QuantityInStock = 0 WHERE Id = 1
```

With `[ConcurrencyCheck]`, EF Core injects the OriginalValue into the WHERE clause:
```sql
UPDATE Products
SET QuantityInStock = 0         -- CurrentValue
WHERE Id = 1
  AND QuantityInStock = 1       -- OriginalValue ← the concurrency guard
```

If **0 rows** are affected, EF Core knows someone else already changed that row and throws `DbUpdateConcurrencyException`.

### 3. The race scenario, step by step

```
              Alex (ctx1)                    Bob (ctx2)
──────────────────────────────────────────────────────────
FindAsync(1)  → stock=1, Original=1     FindAsync(1) → stock=1, Original=1
stock -= 1    → Current=0, Original=1   stock -= 1   → Current=0, Original=1

SaveChanges:
  WHERE Id=1 AND stock=1 → 1 row ✓     SaveChanges:
  DB is now stock=0                       WHERE Id=1 AND stock=1 → 0 rows ✗
                                          → DbUpdateConcurrencyException
```

### 4. What DbUpdateConcurrencyException contains

It's not just a message — it carries all three value states for every conflicting entity:

```csharp
catch (DbUpdateConcurrencyException ex)
{
    var entry = ex.Entries.First();

    var proposed  = entry.CurrentValues;                     // what YOU tried to write (0)
    var original  = entry.OriginalValues;                    // what YOU read at load time (1)
    var inDatabase = await entry.GetDatabaseValuesAsync();   // what's ACTUALLY in DB now (0)
}
```

This gives you four resolution strategies:

| Strategy | What you do | When to use |
|---|---|---|
| **Client wins** | Overwrite DB with your values anyway | Admin force-update, low-impact data |
| **Store wins** | Discard your changes, reload from DB | Background jobs, idempotent operations |
| **Merge** | Combine both changes field by field | Collaborative editing of independent fields |
| **Fail-and-Notify** | Rethrow, tell the user to retry manually | User-driven writes where accuracy > smoothness |

---

**Client wins — "I'm the Boss"**

Your write always goes through, regardless of what changed in the DB.

The scenario: Admin A changes a user's bio to "I love .NET." Admin B changes it to "I love Angular." Admin A saves first. Admin B saves a second later — overwrites silently. Admin A's change is simply lost.

When to use: when the data isn't critical. If the bio is slightly wrong for a second, no one loses money.

> **Drawback:** The "Lost Update" problem. You silently overwrite someone else's work without them knowing. Fine for low-impact data, dangerous for inventory or financial data.

---

**Store wins — "First Come, First Served"**

Discard your changes, accept what's already in the DB, move on.

The scenario: Two background jobs both read Order status = "Pending" and try to update it to "Processing." Job A saves first. Job B catches the exception, sees the status is already "Processing," and just gives up. No duplicate work done.

When to use: when the operation is idempotent — if it's already done, no need to do it again. Great for background tasks where only the first worker should succeed.

---

**Merge — "Smart Collaboration"**

Combine both changes field by field — each user's edit survives on the fields they touched.

The scenario: Admin A changes Price to $12. Admin B changes Description to "Blue Shirt." Both click save at the same time. Result: Price $12 and Description "Blue Shirt" — both changes survive.

When to use: collaborative tools where users edit independent fields.

> **Drawback:** Can produce inconsistent state. If Admin A sets Category to "Electronics" and Admin B adds Property "Washable," you end up with a Washable Laptop. Logically linked fields should never be merged blindly.

---

**Fail-and-Notify — "Safe Abort" (what we do)**

Don't resolve the conflict — surface it to the user and let them decide.

The scenario: Alice and Bob both see stock = 1 and click Pay. Alice's transaction commits, stock = 0. Bob's save sees the stock is no longer 1 — EF Core throws `DbUpdateConcurrencyException`. We catch it and rethrow as `InvalidOperationException`. Bob sees: "Stock changed. Please try again."

When to use: when accuracy matters more than smoothness. You can't merge a sale. You can't let the client win (that's overselling). You stop and ask the human.

> **Interview answer for Task 13:** "I didn't just add a try-catch. I identified a race condition, evaluated all four strategies, and chose Fail-and-Notify because it's the only one that guarantees inventory integrity — you can't take a customer's money without them re-confirming the current state."

### 5. [ConcurrencyCheck] vs [Timestamp] — which to use when

**`[ConcurrencyCheck]`** — marks specific columns:
```csharp
[ConcurrencyCheck]
public int QuantityInStock { get; set; }
```
- Adds only that column's original value to the WHERE clause
- Works with any column type, any database (including SQLite)
- Only detects conflicts on that column — if `Price` changes concurrently, you won't catch it

**`[Timestamp]`** — SQL Server `rowversion`:
```csharp
[Timestamp]
public byte[] RowVersion { get; set; }
```
- SQL Server auto-increments this binary column on **every write**, regardless of which column changed
- Catches all conflicts, not just one column — more robust
- Requires adding a new column to the table
- SQL Server only — breaks SQLite

**For this project**: `[ConcurrencyCheck]` is correct. We only care about stock conflicts, and we target both SQL Server (Azure) and SQLite (local dev).

### 6. InMemory vs SQLite — which to use for tests

**InMemory** stores data in a plain .NET dictionary. No SQL engine, no schema, zero setup. EF Core handles everything directly in RAM. Since EF Core 5, it also simulates concurrency tokens — which is exactly why it works for this test:

```
ctx2 says:   "I read QuantityInStock=1, trying to write 0"
InMemory:    "Current value in store is 0, not 1" → DbUpdateConcurrencyException
```

The downside: InMemory doesn't enforce FK constraints, doesn't run real transactions, and ignores `UNIQUE`/`CHECK` constraints. Tests can pass even when your SQL would fail in production.

**SQLite in-memory** is a real SQL engine running in-process (`Data Source=:memory:`). It executes actual SQL, enforces constraints, and runs real transactions. The tradeoff: you need to open a persistent connection and call `EnsureCreated()` to build the schema before each test.

**When to pick which:**

| Use InMemory when... | Use SQLite in-memory when... |
|---|---|
| Testing EF Core behavior (concurrency, change tracking) | Testing FK constraints, cascade deletes |
| Speed matters and SQL accuracy doesn't | Testing that a transaction actually rolls back |
| Test is narrow and isolated | Your test could silently pass on InMemory but fail on a real DB |

**For this test:** InMemory is the right call. We're testing whether EF Core throws on a concurrency conflict — that's EF Core behavior, not SQL behavior. SQLite would also work but adds schema setup for no extra coverage.

### 7. Full error chain to the client

```
DbUpdateConcurrencyException (EF Core)
  → caught in OrderService → throws InvalidOperationException("Stock changed during checkout...")
    → ExceptionMiddleware catches InvalidOperationException → 400 + message in body
      → error.interceptor sees 400, no .errors array → snackbar.error(err.error.message)
        → user sees red snackbar: "Stock changed during checkout. Please try again."
```

## Exercise

Given `QuantityInStock = 2`, three concurrent requests each ordering 1 unit:
- Request A saves first. What happens?
- Request B saves second. What happens?
- Request C saves third. What happens?

Think through: who succeeds, who gets the exception, and what is the final stock value.

## Solution

- **A** → WHERE stock=2 → matches → saves stock=1 ✓
- **B** → WHERE stock=2 → no match (DB has 1) → `DbUpdateConcurrencyException` → user sees snackbar, must retry
- **C** → WHERE stock=2 → no match (DB has 1) → same exception

If B retries: reads fresh stock=1, saves WHERE stock=1 → matches → stock=0 ✓
If C retries after B: reads stock=0, fails "Insufficient stock" check → clear error

Final stock: 1 (only A succeeded without retry). Each retry has a fair chance.

## Tests

TDD — test written first (RED), then `[ConcurrencyCheck]` added (GREEN).

File: `Infrastructure.Tests/Services/OrderService_Tests.cs`

```csharp
// Pre-load into both contexts before either saves — simulates the race
await ctx1.Products.FindAsync(1);
await ctx2.Products.FindAsync(1);

var order1 = await service1.CreateOrderAsync(...);   // succeeds
var act2   = async () => await service2.CreateOrderAsync(...);  // must throw

await act2.Should().ThrowAsync<InvalidOperationException>().WithMessage("*concurrent*");
product!.QuantityInStock.Should().Be(0);  // only decremented once
```

Uses EF Core **InMemory** provider — since EF Core 5 it enforces concurrency tokens. When `ctx2` tries to save, InMemory compares the original tracked value against the current store value → mismatch → `DbUpdateConcurrencyException`.

## Interview Q&A

**1. What's the difference between optimistic and pessimistic concurrency?**

Pessimistic: lock the row on read, block all other writers until you release. Good when conflicts are frequent. Bad for high-read scenarios.

Optimistic: no lock. On save, verify the row hasn't changed. Throw on conflict. Good when conflicts are rare (most e-commerce scenarios).

**2. How does EF Core implement [ConcurrencyCheck]?**

It adds the original property value to the UPDATE's WHERE clause. If 0 rows are affected, EF throws `DbUpdateConcurrencyException`. No extra column needed.

**3. What's the alternative to [ConcurrencyCheck] in SQL Server?**
`[Timestamp]` / `rowversion` — a binary column SQL Server auto-increments on every write. Covers all columns, not just one. Requires adding a column. SQL Server only — breaks SQLite.

**4. What are the four strategies for handling DbUpdateConcurrencyException?**

- **Client wins** — overwrite DB anyway. Use for low-impact data (e.g. profile bio) where a lost update is acceptable.

- **Store wins** — discard your changes, accept what's in DB. Use for idempotent background jobs where the first writer wins.

- **Merge** — combine changes field by field. Use for collaborative editing of independent fields. Risk: inconsistent state if fields are logically linked.

- **Fail-and-Notify** — rethrow, tell the user. Use when accuracy matters more than smoothness (e.g. checkout — you can't merge a sale).

**5. Why did you choose Fail-and-Notify for checkout instead of retrying automatically?**

Retrying silently (Store Wins) would be bad UX. If stock dropped or price changed between the user's read and save, I shouldn't take their money without re-confirmation. Fail-and-Notify protects inventory integrity and customer trust.

---

claude --resume 04a3b7d6-357c-4e67-a341-9326dfff6552
