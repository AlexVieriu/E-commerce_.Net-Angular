## SECTION C — TRANSACTION BOUNDARIES & ISOLATION LEVELS

> **Source:** Task 14 (implicit transactions, intentionally NOT adding `BeginTransactionAsync`) + Task 15 (cart delete must happen after commit).

---

### EASY

**C1.** [HIGH] Does EF Core wrap `SaveChangesAsync` in a transaction automatically?

<details>
<summary>The 5% Answer</summary>

- **Yes** — every `SaveChangesAsync` call automatically starts an implicit transaction, executes all pending SQL statements, then commits
- If any statement fails, the entire batch rolls back
- You only need `BeginTransactionAsync` when:
  - Multiple `SaveChangesAsync` calls must be atomic together
  - You need a non-default isolation level


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"You have to wrap it in a transaction manually." A senior would correct this immediately — adding a redundant explicit transaction is a junior code smell.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the implicit transaction also covers cascade-delete SQL that EF generates, so you don't need to wrap related-entity deletes manually.


</details>

---

**C2.** [HIGH] [OUT-OF-SCOPE] What's the default transaction isolation level in SQL Server?

<details>
<summary>The 5% Answer</summary>

- Default is **`READ COMMITTED`**
- Prevents **dirty reads** — you only see committed data
- Allows **non-repeatable reads** — the same SELECT within the same transaction may return different values if another transaction commits in between
- Azure SQL often has **`READ COMMITTED SNAPSHOT`** on by default — gives readers non-blocking row-version reads instead of shared locks


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"SERIALIZABLE." That's the strictest, not the default. Confusing these in a live interview signals no real SQL experience.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `READ COMMITTED SNAPSHOT` (RCSI) is the modern Azure SQL default and that it eliminates reader-writer blocking by serving readers from the version store — a common gotcha when migrating from on-prem to Azure SQL.


</details>

---

### MEDIUM

**C3.** [HIGH] Your `CreateOrderAsync` has no `BeginTransactionAsync`. A code reviewer flags it as a bug. How do you push back?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

Show that all writes go through one `SaveChangesAsync` call (via `unitOfWork.Complete()`):
- `UPDATE Products SET QuantityInStock = ...` (per item)
- `INSERT INTO Orders (...)`
- `INSERT INTO OrderItems (...)`

EF Core wraps the whole batch in one implicit transaction. If the `INSERT Orders` fails, the `UPDATE Products` is rolled back automatically. Atomicity is already guaranteed.

Adding `BeginTransactionAsync` would:
- Add 4 lines of ceremony with zero functional benefit
- Signal to future readers that the code is unsafe without it (misleading)
- Hide the fact that the author didn't understand EF's guarantees

The correct move: **document why you're NOT adding it**. That's what the task-14 decision doc does.

**When you WOULD add it:**
- Multiple `SaveChangesAsync` calls that must be atomic together
- Mixing EF with `ExecuteSqlRawAsync` (raw SQL is outside EF's implicit transaction)
- Cross-context operations (two `DbContext` instances = two connections = two transactions)
- Non-default isolation level needed (SERIALIZABLE, REPEATABLE READ)

**Production consequence:** the reviewer is right that atomicity matters — but they're wrong about the mechanism. If you capitulate and add a redundant transaction, the codebase grows with ceremony that later readers copy-paste elsewhere.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"I'll add it just to be safe." That's cargo-culting. Senior reviewers want a reason, not ceremony.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Pointing to the EF Core source (`SaveChanges` internals) or the [MS docs: "Saving changes in a transaction"](https://learn.microsoft.com/en-us/ef/core/saving/transactions) — knowing the authoritative reference shows confidence.


</details>

---

**C4.** [HIGH] What are the four cases where you MUST reach for `BeginTransactionAsync`?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

1. **Multiple `SaveChangesAsync` calls that must be atomic.**
   Example: save an Order, then update an InventoryLedger in a second call. If the second fails, you already committed the first → orphan.

2. **Mixing EF with raw SQL (`ExecuteSqlRawAsync`).**
   Raw SQL runs outside EF's change tracker. EF's implicit transaction doesn't cover it. Wrap both in an explicit transaction.

3. **Cross-context operations.**
   Two `DbContext` instances = two connections = two implicit transactions. To atomify: restructure to one context, or use a distributed transaction (`TransactionScope` with MSDTC).

4. **Explicit isolation level control.**
   EF's implicit transaction uses the DB default (`READ COMMITTED`). If you need `SERIALIZABLE`:
   ```csharp
   using var tx = await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable);
   // ... writes ...
   await ctx.SaveChangesAsync();
   await tx.CommitAsync();
   ```


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Always use explicit transactions for safety." Ceremony-thinking. Shows you don't trust the framework.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **`TransactionScope` is deprecated on .NET Core/5+** for cross-DB transactions (MSDTC is Windows-only and not supported on Linux containers). The modern answer is **outbox/inbox patterns** or Saga — explicit transactions across systems are an anti-pattern in cloud deployments.


</details>

---

**C5.** [HIGH] Cart deletion happens AFTER `SaveChangesAsync`. If I move it before, what breaks? Walk through both scenarios.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

Current order:
```csharp
if (!await unitOfWork.Complete()) return null;  // commits order + stock to SQL
await cartService.DeleteCartAsync(orderDto.CartId);  // Redis, post-commit
```

**Scenario A — cart deleted before commit, SQL commit fails:**
- Redis: cart gone ✗
- SQL: no order (rolled back)
- User: "Where's my cart? Where's my order?" → data loss, support ticket.

**Scenario B — cart deleted after commit (current), cart delete fails:**
- Redis: cart still there (stale) ✗
- SQL: order exists ✓
- User: sees success page, has a leftover cart next time they shop. Minor UX issue, not a data integrity problem.

Post-commit is correct because the SQL commit is the **critical** operation; the cart is **non-critical** cleanup. If cleanup fails, the critical op is safe.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Transactions should roll everything back together." Impossible — Redis and SQL Server don't share a transaction coordinator.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the correct long-term solution is an **outbox pattern**: on commit, insert a row into an `Outbox` table within the same SQL transaction. A background worker reads the outbox and performs the Redis cleanup with retries. That guarantees eventual consistency without blocking checkout.


</details>

---

**C6.** [HIGH] [OUT-OF-SCOPE] Compare `READ COMMITTED` vs `REPEATABLE READ` vs `SERIALIZABLE`. When would you reach for each in an e-commerce context?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

| Level | Dirty reads | Non-repeatable reads | Phantom reads | Use case |
|---|---|---|---|---|
| READ UNCOMMITTED | ✗ possible | ✗ possible | ✗ possible | Never (only for reporting where accuracy doesn't matter) |
| READ COMMITTED | ✓ blocked | ✗ possible | ✗ possible | Default. Most e-commerce reads/writes. |
| REPEATABLE READ | ✓ | ✓ | ✗ possible | You need the same row value to stay stable across multiple reads in one transaction (rare). |
| SERIALIZABLE | ✓ | ✓ | ✓ | Financial audit log, absolute consistency over throughput. |

**E-commerce reality:** stock races are solved with optimistic concurrency (`[ConcurrencyCheck]`), not higher isolation. SERIALIZABLE would serialize the entire hot-row, killing throughput on a flash sale — exactly the wrong tradeoff.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Use SERIALIZABLE for safety." It destroys throughput and causes deadlocks at scale. Shows no production experience.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **SNAPSHOT isolation** as the SQL Server pragmatic middle ground — gives you repeatable-read semantics without blocking writers, by reading from the version store. Azure SQL's default (`READ COMMITTED SNAPSHOT`) is a lite version of this.


</details>

---

### HARD

**C7.** [HIGH] [OUT-OF-SCOPE] Can Redis and SQL Server share a transaction? Explain exactly why or why not and give the correct pattern.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**No**, they cannot share a transaction. Two reasons:

1. **No shared transaction coordinator.** SQL Server has its own transaction manager; Redis has no concept of a SQL transaction. For cross-system atomicity you'd need a distributed transaction coordinator (DTC / XA) — neither system supports it cleanly, and MSDTC isn't available on Linux.

2. **Redis doesn't support rollback.** Redis has `MULTI`/`EXEC` for atomic batches, but they're atomic *within Redis* only, and don't roll back on external failure. Once `EXEC` completes, the changes are live.

**Correct pattern — Outbox:**

```
Step 1: within SQL transaction, write to Orders + Outbox (pending cart-delete message)
Step 2: SQL commits atomically
Step 3: background worker polls Outbox, calls Redis.DeleteCart, marks row processed
Step 4: retries on failure (idempotent delete: Redis.DEL on missing key returns 0, no error)
```

This gives you **eventual consistency** without blocking checkout, and survives Redis downtime.

**Production consequence:** trying to fake a cross-system transaction leads to half-committed states — the classic "money taken, no order" bug.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Use TransactionScope with distributed transactions." Deprecated on modern .NET and Linux-unsupported.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **idempotent consumers** are the backbone of outbox patterns — the worker must handle re-delivering the same message without duplicate side effects. Redis deletes are naturally idempotent (`DEL` on a missing key is a no-op).


</details>

---

**C8.** [MEDIUM] A junior author wraps `CreateOrderAsync` in an explicit transaction to "be safe." Review the diff:

```csharp
public async Task<Order?> CreateOrderAsync(string email, CreateOrderDto dto)
{
    using var tx = await _context.Database.BeginTransactionAsync();
    try
    {
        // ... build order, decrement stock ...
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return order;
    }
    catch
    {
        await tx.RollbackAsync();
        throw;
    }
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Critique:**

1. **Redundant transaction** — `SaveChangesAsync` already wraps all writes in an implicit transaction. The explicit `BeginTransactionAsync` just swaps the implicit one for an identical explicit one. No functional change.

2. **Masks intent** — future readers will assume the transaction is necessary for correctness and copy the pattern, adding ceremony everywhere.

3. **`RollbackAsync` is unnecessary** — when the transaction is disposed without `CommitAsync`, it rolls back automatically. The `using` block already guarantees rollback on exception.

4. **Doesn't fix the real concurrency risk** — wrapping doesn't add a row lock. The stock race is solved by `[ConcurrencyCheck]`, not by a transaction.

**Acceptable rewrite:**
```csharp
public async Task<Order?> CreateOrderAsync(string email, CreateOrderDto dto)
{
    // ... build order, decrement stock ...
    await _context.SaveChangesAsync();  // implicit transaction, covers all writes
    return order;
}
```

**When explicit IS justified here** — only if the method grew to have multiple `SaveChangesAsync` calls, or needed `IsolationLevel.Serializable`.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Approving the PR. This is a common junior pattern that senior reviewers should catch.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Suggesting the PR author remove the transaction AND add a comment like `// implicit transaction — see Task 14` to pre-empt future reviewers asking the same question. Documenting intent is a senior move.


</details>

---

**C9.** [MEDIUM] Design: add an `InventoryLedger` entity that must save atomically with `Order`. The ledger is a separate aggregate with its own service. Show the correct transaction structure.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Setup:** `InventoryLedger` is a new entity tracking every stock movement (for audit/accounting). It must save atomically with the order — if the order commits, the ledger entry must commit; if either fails, both roll back.

**Wrong approach:**
```csharp
await _orderService.CreateOrderAsync(...);   // SaveChangesAsync #1
await _ledgerService.RecordMovementAsync(...); // SaveChangesAsync #2
// ❌ if #2 fails, #1 already committed
```

**Correct approach — explicit transaction spanning both:**

```csharp
using var tx = await _context.Database.BeginTransactionAsync();
try
{
    var order = await _orderService.CreateOrderAsync(email, dto, saveImmediately: false);
    _ledgerService.AddMovement(order, MovementType.Sale);
    await _context.SaveChangesAsync();  // one save, both entities
    await tx.CommitAsync();
    return order;
}
catch
{
    // automatic rollback on dispose
    throw;
}
```

Or better: **share the `DbContext`** so both services operate on the same change tracker, and call `SaveChangesAsync` once. Then the implicit transaction covers both.

```csharp
var order = _orderService.BuildOrder(email, dto);
_ledgerService.QueueLedgerEntry(order);
await _unitOfWork.Complete();  // one SaveChangesAsync → one implicit transaction
```

The UnitOfWork pattern exists exactly for this: one transaction, multiple aggregates.

**Production consequence:** saving aggregates separately without a shared UnitOfWork creates half-committed states under failure. This is the classic "ledger drift" bug in accounting systems.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Using `TransactionScope` across services. Doesn't work across connections without MSDTC; anti-pattern in cloud deployments.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that if `InventoryLedger` lived in a **separate bounded context** (eventually: a microservice), the correct answer is **NOT** a distributed transaction but the **outbox pattern** with eventual consistency. Explicit transactions only make sense within one bounded context.


</details>

---

### ⚡ Syntax Quick-Fire

**CQ1.** [HIGH] `using var tx = await ctx.Database.BeginTransactionAsync();` vs `using (var tx = ...) { }` — any behavioral difference?

<details>
<summary>The 5% Answer</summary>

- **No behavioral difference** — both call `Dispose()` at end of scope.
- `using var` is C# 8+ syntactic sugar (declaration-style). Scope ends at the enclosing block close.
- Critical: `IDbContextTransaction.Dispose()` on an uncommitted transaction **automatically rolls it back**. The `using` pattern guarantees rollback on exception without an explicit `catch`.

```csharp
// Both are equivalent:
using var tx = await ctx.Database.BeginTransactionAsync();
// ... on exception: Dispose() → rollback

using (var tx = await ctx.Database.BeginTransactionAsync())
{
    // ... on exception: Dispose() → rollback
}
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Adding an explicit `catch { await tx.RollbackAsync(); throw; }`. Redundant — `Dispose()` already rolls back. Adds ceremony and a second failure point if `RollbackAsync` throws.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing that `using var` scope ends at the *block* close, not the *statement* close. Useful for controlling rollback scope precisely — e.g., `using var` inside an `if` block disposes at the `}` of that `if`.

</details>

---

**CQ2.** [HIGH] `await Task.WhenAll(taskA, taskB)` vs `await taskA; await taskB;` — what's the concurrency difference?

<details>
<summary>The 5% Answer</summary>

- **`Task.WhenAll`**: both tasks start before either is awaited — they run **concurrently**.
- **Sequential awaits**: `taskB` doesn't start until `taskA` completes — strictly sequential.
- For independent I/O (two DB reads, an HTTP call + a cache read), `WhenAll` halves the latency.
- **Failure mode**: `WhenAll` waits for ALL tasks; if taskA throws, taskB still runs to completion (and may also throw). Use `Task.WhenAll` and inspect `AggregateException.InnerExceptions` to get all errors.

```csharp
// Concurrent — total time ≈ max(taskA, taskB)
await Task.WhenAll(fetchProductsAsync(), fetchDeliveryMethodsAsync());

// Sequential — total time ≈ taskA + taskB
await fetchProductsAsync();
await fetchDeliveryMethodsAsync();
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Sequential is safer." There's no safety difference for independent operations. Sequential just wastes wall-clock time.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Noting that `WhenAll` with EF Core on the same `DbContext` is **not safe** — `DbContext` is not thread-safe. Use separate contexts (or separate services with their own contexts) for concurrent DB operations.

</details>

---

**CQ3.** [HIGH] LINQ: `Where(p => ids.Contains(p.Id))` — what SQL does EF Core generate, and when does it break?

<details>
<summary>The 5% Answer</summary>

- Generates `WHERE Id IN (@p0, @p1, @p2, ...)` — a single parameterised `IN` clause.
- **Breaks at scale**: SQL Server limits `IN` lists to ~2,100 parameters (sp_executesql parameter cap). EF Core with large collections will throw at the SQL layer.
- **Fix for large sets**: chunk the IDs and issue multiple queries, or use a temp table join.

```csharp
// Safe for small sets (< 1000 ids)
var products = await _context.Products
    .Where(p => ids.Contains(p.Id))
    .ToListAsync();

// For large sets: chunk
foreach (var chunk in ids.Chunk(500))
{
    var batch = await _context.Products
        .Where(p => chunk.Contains(p.Id))
        .ToListAsync();
}
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "It works fine." Works for small IDs lists; silently breaks at 2100+ parameters in production.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Mentioning that `EF.Functions.Contains` / a temp table join via `ExecuteSqlRaw` is the production pattern for bulk ID lookups — it avoids the parameter limit entirely and the DB's query planner makes better decisions on large sets.

</details>

---
