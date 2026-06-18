# Task 15 — Cart Delete Must Be After Commit

## Why this approach

Task 15 asked to verify that `DeleteCartAsync` is called **after** `unitOfWork.Complete()`, not inside the same transaction scope.

This was already correct — the task was a verification, not a fix. The core learning is *why* this ordering matters.

## What confused me

It might seem safer to delete the cart inside the transaction so everything rolls back together if something fails. The confusion is assuming Redis and SQL Server can share a transaction — they can't.

## Explanation

### The ordering in code

`OrderService.CreateOrderAsync` (`Infrastructure/Services/OrderService.cs`):

```csharp
// 1. Commit order + stock updates to SQL Server
if (!await unitOfWork.Complete()) return null;

// 2. Delete cart from Redis — AFTER commit
await cartService.DeleteCartAsync(orderDto.CartId);
```

### Why Redis and SQL can't share a transaction

SQL Server transactions are managed by the database engine. Redis has no concept of a SQL transaction. There is no distributed transaction coordinator between them in this app.

This means:
- You **cannot** roll back a Redis delete if the SQL commit fails — they are independent systems
- You **cannot** include Redis in `BeginTransactionAsync()` — only SQL statements go there

### Why cart delete comes after commit (and not before)

| Scenario | Cart deleted before commit | Cart deleted after commit |
|---|---|---|
| SQL commit succeeds | Cart gone, order exists ✅ | Cart gone, order exists ✅ |
| SQL commit fails | **Cart gone, no order** ❌ | Cart still exists, user can retry ✅ |

Deleting before commit means a failed order leaves the user with no cart and no order — data loss.

### What if cart delete fails after commit?

The order is already in SQL Server — committed and safe. The user has a stale cart in Redis. This is a minor UX issue (cart shows old items), not a data integrity problem. The correct response is:
- Accept the failure silently (fire-and-forget)
- Optionally: a background cleanup job to purge orphaned carts

Blocking the checkout response on Redis cleanup would hurt UX for a non-critical operation.

## Exercise

Read `OrderService.CreateOrderAsync` and answer:

1. On which line does the SQL commit happen?
2. On which line does the Redis delete happen?
3. What would go wrong if you moved the Redis delete to *before* `unitOfWork.Complete()`?
4. Could you wrap the Redis delete in the same `BeginTransactionAsync()` as the SQL operations? Why or why not?

## Solution

1. SQL commit: `if (!await unitOfWork.Complete()) return null;` — line 70.
2. Redis delete: `await cartService.DeleteCartAsync(orderDto.CartId);` — line 80.
3. If Redis delete came first: a failed SQL commit would leave the user with no cart and no order. The user loses their cart and has to rebuild it. Data is in an inconsistent state.
4. No. `BeginTransactionAsync()` creates a SQL Server transaction. Redis is a completely separate system with no SQL transaction support. Redis commands cannot participate in a SQL transaction.

## Interview Q&A

**Q: Why is cart deletion after the SQL commit and not inside the transaction?**

A: Redis and SQL Server don't share a transaction — there's no distributed transaction coordinator. If I deleted the cart before committing the order and the commit failed, the user would lose their cart with no order to show for it. The correct ordering is: commit the critical operation first (order + stock), then clean up the non-critical one (cart). If cart deletion fails after a successful commit, the user has a stale cart — a minor issue I'd handle with background cleanup, not a checkout failure.

---

**Q: Is "fire-and-forget" on the cart deletion safe here?**

A: Yes. The cart is non-critical after the order commits. The worst case of a failed delete is a stale Redis key that expires naturally or gets overwritten next time the user creates a cart. I wouldn't retry it synchronously in the checkout path — that would degrade checkout reliability for a cleanup operation.

---

claude --resume 855033c5-43ce-41cf-afc0-7ed0bd5ba0b5
