# Task 14 — Transaction Isolation Levels

## Why this approach

Task 14 asked to wrap `CreateOrderAsync` in `BeginTransactionAsync()` via `IUnitOfWork`.

After analysis, this was **intentionally not implemented**. The reasoning is the core learning here.

## What confused me

The task description implied the code was unsafe without an explicit transaction. It isn't.
The confusion was between "I don't see a transaction" and "there is no transaction".

## Explanation

### EF Core's implicit transaction guarantee

EF Core wraps every `SaveChangesAsync()` call in an **implicit transaction automatically**.
From the EF Core source (`SaveChanges` internals): if no ambient transaction exists, EF starts one,
executes all pending SQL statements (INSERTs, UPDATEs), then commits. If any statement fails,
it rolls back — all or nothing.

In `CreateOrderAsync`, all of the following are batched into a **single** `SaveChangesAsync()` call
via `unitOfWork.Complete()`:
- `UPDATE Products SET QuantityInStock = QuantityInStock - @qty WHERE Id = @id` (per item)
- `INSERT INTO Orders (...)`
- `INSERT INTO OrderItems (...)`

Because it's one `SaveChangesAsync()`, EF issues one transaction. Stock deduction and order
creation are **already atomic**. If the INSERT fails, the UPDATE is rolled back automatically.

### When you DO need an explicit transaction

An explicit `BeginTransactionAsync()` is justified in exactly these cases:

1. **Multiple `SaveChangesAsync()` calls that must be atomic together.**
   Example: you save an Order in one call, then update an Inventory ledger in a second call.
   If the second call fails, the first is already committed — you have an orphaned order.
   Fix: wrap both in one explicit transaction.

2. **Mixing EF with raw SQL (`ExecuteSqlRawAsync`).**
   Raw SQL runs outside EF's change tracker. EF's implicit transaction does not cover it.
   You must use an explicit transaction to include it.

3. **Cross-context operations.**
   Two `DbContext` instances = two separate connections = two separate implicit transactions.
   To make them atomic you need a distributed transaction or restructure to one context.

4. **Explicit isolation level control.**
   EF's implicit transaction uses the database default (`READ COMMITTED` in SQL Server).
   If you need `SERIALIZABLE` or `REPEATABLE READ`, you must call `BeginTransactionAsync`
   with the desired `IsolationLevel`.

### Why adding it here would be a code smell

Adding an explicit transaction to this method would:
- Add 4 lines of ceremony with zero functional benefit
- Signal to reviewers that the author doesn't trust EF's guarantees (or doesn't know about them)
- Create a false impression that without it the code was unsafe

The **correct senior move** is to understand the implicit guarantee and document why you're
NOT adding it — exactly what this file is.

### What about task 15 — cart delete after commit?

Cart deletion (`cartService.DeleteCartAsync`) is intentionally outside any transaction boundary.
Redis and SQL Server do not share a transaction. If cart deletion fails, the order is already
committed — that is the correct behaviour. Fire-and-forget on a non-critical cleanup operation.
This is not a bug, it is a deliberate design choice.

## Exercise

Read `UnitOfWork.Complete()` and `OrderService.CreateOrderAsync()`. Then answer:

1. How many SQL statements does `Complete()` emit for a 3-item order?
2. Are they in the same transaction? How do you know?
3. Write a scenario where you WOULD need `BeginTransactionAsync()` in this codebase.
4. What isolation level does SQL Server use by default, and what does it mean?

## Solution

1. For a 3-item order: 3 `UPDATE Products` + 1 `INSERT Orders` + 3 `INSERT OrderItems` = **7 statements**, all in one batch inside one implicit transaction.

2. Yes — same transaction. EF Core guarantees this. You can verify by enabling SQL logging
   (`optionsBuilder.LogTo(Console.WriteLine)`) and observing `BEGIN TRANSACTION` /
   `COMMIT TRANSACTION` wrapping all 7 statements in one block.

3. A scenario requiring explicit transaction in this codebase:
   If we add an `InventoryLedger` entity that must be saved in a **separate** `SaveChangesAsync`
   call (e.g., from a different service), we'd need to wrap both calls in one explicit transaction.
   Or if we call `context.Database.ExecuteSqlRawAsync("UPDATE ...")` for a bulk stock operation
   alongside EF-tracked changes.

4. SQL Server default: `READ COMMITTED`. Means: a transaction only reads rows that have been
   committed by other transactions. It prevents dirty reads (reading uncommitted data) but allows
   non-repeatable reads (a row you read once may change if you read it again within the same transaction).

## Interview Q&A

**Q: Why didn't you add an explicit transaction to `CreateOrderAsync`?**

A: Because EF Core wraps every `SaveChangesAsync()` in an implicit transaction automatically.
All my writes — stock updates and order insert — go through one `Complete()` call, which is one
`SaveChangesAsync()`. EF batches them into a single SQL transaction, so atomicity is already
guaranteed. Adding `BeginTransactionAsync()` on top would be redundant ceremony that adds
complexity without benefit. I only reach for explicit transactions when I have multiple
`SaveChangesAsync()` calls that must be atomic, or when mixing EF with raw SQL, or when I need
a non-default isolation level.

---

**Q: When would you use `SERIALIZABLE` isolation in an e-commerce context?**

A: Almost never in a high-traffic system — it causes full table locks and kills throughput.
The right tool for stock race conditions is optimistic concurrency (`[ConcurrencyCheck]` / row
versioning), which is what Task 13 implemented. `SERIALIZABLE` would be appropriate for a
low-volume financial audit table where you need absolute consistency over performance.

---

**Q: What's the difference between `READ COMMITTED` and `REPEATABLE READ`?**

A: `READ COMMITTED`: you can only read committed rows, but if you read the same row twice in
one transaction, it may return different values (another transaction committed between your reads).
`REPEATABLE READ`: once you read a row, that value is locked for the duration of your transaction —
no other transaction can update it. More consistent, but more contention.

---

**Q: Cart deletion happens after the order is saved. What if it fails?**

A: That's intentional. Redis and SQL Server don't share a transaction — you can't roll back
a SQL commit because a Redis delete failed. The correct design is: commit the order first
(the critical operation), then clean up the cart (non-critical). If cart deletion fails,
the user has a stale cart in Redis — a minor UX issue, not a data integrity problem.
I'd handle it with a background cleanup job rather than blocking the checkout flow.

---

claude --resume 855033c5-43ce-41cf-afc0-7ed0bd5ba0b5
