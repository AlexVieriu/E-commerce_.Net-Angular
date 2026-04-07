# Task 18 — Order Idempotency (Unique Constraint on PaymentIntentId)

## Why this approach

Task 16 prevents duplicate PaymentIntents at the Stripe level. Task 18 prevents duplicate Orders at the database level.

Even with an idempotency key on Stripe, `CreateOrderAsync` could be called twice with the same `PaymentIntentId` (e.g., double form submit, race condition). Without a DB constraint, you'd get two orders tied to the same payment — one legitimate, one phantom.

Two layers of defense:
- **App level**: check for an existing order by `PaymentIntentId` before inserting — return it if found
- **DB level**: unique index as the last line of defense if two concurrent requests both pass the app-level check

## What confused me

Why do we need this if Stripe already has an idempotency key? Stripe's key prevents duplicate PaymentIntents — one payment = one intent. But your server can still call `CreateOrderAsync` twice with the same cart/intent (double-click). Stripe knows nothing about your `Orders` table.

## Explanation

### The double-click bug (without Task 18)

1. User clicks "Place Order" twice fast
2. Two requests hit `POST /api/orders` simultaneously, both carrying the same cart (`PaymentIntentId = pi_abc123`)
3. Request A reads cart from Redis → gets `pi_abc123`
4. Request B reads cart from Redis → gets `pi_abc123`
5. Request A: no order found for `pi_abc123` → inserts `Order #1` with `pi_abc123`
6. Request B: no order found for `pi_abc123` → inserts `Order #2` with `pi_abc123`
7. SQLite now has two orders, both with `pi_abc123`
8. User pays with card → Stripe fires one webhook: `payment_intent.succeeded` for `pi_abc123`
9. Your server queries `Orders WHERE PaymentIntentId = pi_abc123` → finds two rows
10. Updates the first one → `Order #1` is `PaymentReceived`
11. `Order #2` stays `Pending` forever — orphaned

### The fix (with Task 18)

- Step 5: Request A inserts `Order #1` ✅
- Step 6: Request B hits the app-level check → finds `Order #1` → returns it immediately, no insert
- If two requests are truly simultaneous and both pass the app check: the unique DB index rejects the second insert at the DB level

### App-level check (`OrderService.cs`)

```csharp
var existingOrder = await unitOfWork.Repository<Order>()
    .GetEntityWithSpec(OrderSpecification.ByPaymentIntent(cart.PaymentIntentId));
if (existingOrder != null) return existingOrder;
```

Catches the common case (sequential double-click). Fast, no exception thrown.

### DB-level constraint (`OrderConfiguration.cs`)

```csharp
builder.HasIndex(o => o.PaymentIntentId).IsUnique();
```

Last line of defense for true concurrent race. Throws `DbUpdateException` if a duplicate slips through — which bubbles up as a 500. You could catch it and return the existing order, but that's an edge case not worth the complexity here.

### Why `nvarchar(450)` on SqlServer

SQL Server can't index `nvarchar(max)` columns. EF automatically narrows it to `nvarchar(450)` (the max indexable length) when you add a unique index. The migration handles this with `AlterColumn`.

## Exercise

1. Why is the app-level check not enough on its own without the DB constraint?
2. Why is the DB constraint not enough on its own without the app-level check?
3. What does `nvarchar(450)` mean and why can't SQL Server index `nvarchar(max)`?
4. The webhook handler finds an order by `PaymentIntentId`. Why does the unique index make this safer?

## Solution

1. Two truly concurrent requests can both read "no existing order" before either commits. Without the DB constraint, both inserts succeed — you still get duplicates.

2. The DB constraint would reject the second insert with a `DbUpdateException`. That becomes a 500 response to the user. Acceptable, but worse UX than returning the existing order cleanly. The app-level check handles the common case gracefully.

3. `nvarchar(450)` = max 450 Unicode characters. SQL Server has a limit of 900 bytes per index key. Since each Unicode char is 2 bytes, the max indexable `nvarchar` length is 450. `nvarchar(max)` can hold up to 2GB — SQL Server can't build a B-tree index over an unbounded column.

4. Before the index, `WHERE PaymentIntentId = 'pi_abc123'` could return multiple rows. The webhook might update the wrong one or update both. With the unique index, the query always returns exactly one row — deterministic, correct.

## Interview Q&A

**Q: How did you prevent duplicate orders on double-click?**

A: Two layers. First, at the app level: before inserting, I query for an existing order with the same `PaymentIntentId` and return it if found — covers the common case with no exception. Second, at the DB level: a unique index on `PaymentIntentId` rejects any duplicate that slips through a true race condition. The index also makes the webhook lookup deterministic — it's guaranteed to find exactly one order.

---

**Q: Why not just rely on the DB constraint and catch the exception?**

A: You could, but it's worse UX — the second request gets a 500 instead of a clean response with the existing order. The app-level check handles the 99% case gracefully. The DB constraint is the safety net for the 1% true race. Defense in depth with the best UX path as the primary.

---

**Q: Why did the `PaymentIntentId` column type change from `nvarchar(max)` to `nvarchar(450)` on SQL Server?**

A: SQL Server can't build indexes on `nvarchar(max)` columns — the column is unbounded and SQL Server needs a fixed size to build a B-tree. EF automatically narrows it to `nvarchar(450)`, which is the maximum indexable `nvarchar` length (900-byte index key limit ÷ 2 bytes per Unicode char). A Stripe PaymentIntent ID is around 27 characters, so 450 is more than sufficient.

---

claude --resume 855033c5-43ce-41cf-afc0-7ed0bd5ba0b5
