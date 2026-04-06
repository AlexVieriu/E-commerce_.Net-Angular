---
name: Task 13 — Optimistic Concurrency
description: Why ConcurrencyCheck beats locks for stock decrement, and how EF Core detects stale writes
type: user
---

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

This gives you three resolution strategies:

| Strategy | What you do | When to use |
|---|---|---|
| **Client wins** | Overwrite DB with your values anyway | Admin force-update, data migration |
| **Store wins** | Discard your changes, reload from DB | Background job that can silently retry |
| **Merge** | Combine both — e.g. sum the differences | Complex inventory reconciliation |

For checkout, none of these fit. We don't want to silently retry or blindly overwrite. We want to **tell the user** and let them try again. So we rethrow as `InvalidOperationException`.

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

### 6. Why InMemory works for the test

EF Core's InMemory provider (v5+) simulates concurrency tokens using an in-memory dictionary. When ctx2 tries to save:

```
ctx2 says:   "I read QuantityInStock=1, trying to write 0"
InMemory:    "Current value in store is 0, not 1" → DbUpdateConcurrencyException
```

No real SQL involved, but EF Core's concurrency detection logic is identical.

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

## Implementation

**Branch:** `feature/13-stock-race-condition`

### 1. `Core/Entities/Product.cs`
Added the attribute — one line:
```csharp
[ConcurrencyCheck]
public int QuantityInStock { get; set; }
```

### 2. `Infrastructure/Services/OrderService.cs`
Wrapped `unitOfWork.Complete()` in a try/catch:
```csharp
try
{
    if (!await unitOfWork.Complete()) return null;
}
catch (DbUpdateConcurrencyException)
{
    throw new InvalidOperationException(
        "Stock changed during checkout due to a concurrent order. Please try again.");
}
```
`InvalidOperationException` is caught by `ExceptionMiddleware` → 400 with the message → snackbar on the client.

### 3. Migrations
Empty body (no DDL) — `[ConcurrencyCheck]` only updates the EF model snapshot, not the column.
The snapshot now has `.IsConcurrencyToken()` on `QuantityInStock`, which is what EF reads to generate the `WHERE stock = @original` clause.

Generated for both providers:
- `Migrations/SqlServer/..._AddConcurrencyTokenToProduct.cs`
- `Migrations/SQLite/..._AddConcurrencyTokenToProduct.cs`

### 4. `Infrastructure.Tests/Services/OrderService_Tests.cs`
TDD test — was RED before the fix, GREEN after:
```csharp
// Pre-load into both contexts before either saves — simulates the race
await ctx1.Products.FindAsync(1);
await ctx2.Products.FindAsync(1);

var order1 = await service1.CreateOrderAsync(...);   // succeeds
var act2   = async () => await service2.CreateOrderAsync(...);  // must throw

await act2.Should().ThrowAsync<InvalidOperationException>().WithMessage("*concurrent*");
product!.QuantityInStock.Should().Be(0);  // only decremented once
```

### 5. Housekeeping
- `Core/GlobalUsing.cs` — added `System.ComponentModel.DataAnnotations`
- `Infrastructure.Tests/GlobalUsing.cs` — added DTOs, EF Core, Infrastructure.Data namespaces
- `Infrastructure/Data/Factory/SqlServerContextFactory.cs` — added `AddEnvironmentVariables()` so migrations work locally without Azure
- `Infrastructure/Infrastructure.csproj` — added `Microsoft.Extensions.Configuration.EnvironmentVariables`

## Interview Q&A

**Q: What's the difference between optimistic and pessimistic concurrency?**
Pessimistic: lock the row on read, block all other writers until you release. Good when conflicts are frequent. Bad for high-read scenarios.
Optimistic: no lock. On save, verify the row hasn't changed. Throw on conflict. Good when conflicts are rare (most e-commerce scenarios).

**Q: How does EF Core implement [ConcurrencyCheck]?**
It adds the original property value to the UPDATE's WHERE clause. If 0 rows are affected, EF throws `DbUpdateConcurrencyException`. No extra column needed.

**Q: What's the alternative to [ConcurrencyCheck] in SQL Server?**
`rowversion` / `timestamp` — a binary column auto-incremented by SQL Server on every write. More robust (covers all columns, not just the one you annotate), but requires a column addition. Use `[Timestamp]` attribute in EF Core.

**Q: What should you do when you catch DbUpdateConcurrencyException?**
Options: (1) retry by re-reading the entity and reapplying changes — good for background jobs; (2) return a user-facing error and let them retry — better for user-driven writes where intent may have changed.
