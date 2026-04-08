# Task 18 — Order Idempotency (Unique Constraint on PaymentIntentId)

> Big picture: see [sprint-2-checkout-flow.md](sprint-2-checkout-flow.md) — Task 18 failure zone is at "Pay $341".

## Why this approach

Task 16 prevents duplicate PaymentIntents at the Stripe level.
Task 18 prevents duplicate Orders at the database level.

Stripe knows nothing about your `Orders` table. Even with one PaymentIntent, the user can
double-click "Pay $341" — two requests hit `POST /api/orders` with the same cart and same
`PaymentIntentId`. Without a constraint, you get two orders for one payment.

Two layers of defense:
- **App level**: check for an existing order by `PaymentIntentId` before inserting — return it
- **DB level**: unique index as last resort if two concurrent requests both pass the app check

## What confused me

If Task 16 already prevents duplicate PaymentIntents, why can we still get duplicate orders?

Because Task 16 guards `POST /api/payments` (Stripe call). Task 18 guards `POST /api/orders`
(DB insert). They protect different endpoints. Stripe has no idea your order exists.

## Explanation

### The double-click bug (without Task 18)

```
User double-clicks "Pay $341"
  Request A → reads cart → PaymentIntentId = pi_abc123
  Request B → reads cart → PaymentIntentId = pi_abc123
  Request A: no order found → inserts Order #1
  Request B: no order found → inserts Order #2
  SQLite: 2 orders, both with pi_abc123

  Stripe webhook fires for pi_abc123 → finds 2 rows
  → updates Order #1 to PaymentReceived
  → Order #2 stays Pending forever — orphaned
```

### The fix (with Task 18)

```
Request A → inserts Order #1 [OK]
Request B → app check finds Order #1 → returns it [OK]

True race (both hit DB simultaneously):
  → unique index rejects 2nd insert → DbUpdateException → 500
  → still better than 2 orders in DB
```

### App-level check (`OrderService.cs`)

```csharp
var existingOrder = await unitOfWork.Repository<Order>()
    .GetEntityWithSpec(OrderSpecification.ByPaymentIntent(cart.PaymentIntentId));
if (existingOrder != null) return existingOrder;
```

### DB-level constraint (`OrderConfiguration.cs`)

```csharp
builder.HasIndex(o => o.PaymentIntentId).IsUnique();
```

### Why `nvarchar(450)` on SqlServer

SQL Server can't index `nvarchar(max)` — unbounded columns can't build a B-tree.
EF narrows it to `nvarchar(450)` (900-byte limit ÷ 2 bytes/char). A Stripe ID is ~27 chars.

## Exercise

1. Why is the app-level check not enough without the DB constraint?
2. Why is the DB constraint not enough without the app-level check?
3. The webhook finds an order by `PaymentIntentId`. Why does the unique index make this safer?

## Solution

1. Two truly concurrent requests can both read "no order" before either commits → both inserts
   succeed → duplicates. The DB constraint catches this case.

2. The DB constraint throws `DbUpdateException` → 500. The app-level check returns the existing
   order cleanly. Defense in depth: best UX path first, safety net second.

3. Before the index: `WHERE PaymentIntentId = 'pi_abc123'` could return multiple rows — webhook
   updates the wrong one. After the index: always exactly 1 row — deterministic and correct.

## Known Limitation — Order Created After Payment

Currently in `checkout.component.ts`:

```
1. Stripe.js confirms payment  ← money taken
2. POST /api/orders            ← order created in SQLite
3. navigate to /checkout/success
```

The webhook can fire between steps 1 and 2 — before the order exists. Webhook finds nothing,
order stays `Pending` forever. User also sees the success page before the webhook, so
"View My Order" may show `Pending` on a paid order (user panic).

**The correct fix** — create order first, then confirm payment, then wait for SignalR:

```
1. POST /api/orders            ← Status: Pending
2. Stripe.js confirms payment  ← money taken
3. Webhook → order exists → Status: PaymentReceived
4. SignalR → navigate to /checkout/success
```

**Why not implemented:** Requires not deleting the cart until after payment (currently
`CreateOrderAsync` deletes cart from Redis immediately — a retry after a failed payment
would find no cart and fail). Also needs a SignalR timeout fallback. Sprint 5 territory
when Task 41 (order cancellation + stock reversal) is in place.

Full flow + analysis: [sprint-2-checkout-flow.md](sprint-2-checkout-flow.md)

## Interview Q&A

**Q: How did you prevent duplicate orders on double-click?**

A: Two layers. App level: before inserting, I query for an existing order by `PaymentIntentId`
and return it if found — handles the common case cleanly. DB level: unique index on
`PaymentIntentId` rejects duplicates that slip through a true race. The index also makes
the webhook lookup deterministic — guaranteed exactly one row.

---

**Q: Why not just rely on the DB constraint and catch the exception?**

A: You could, but the second request gets a 500 instead of a clean response. The app-level
check handles 99% of cases gracefully. The constraint is the safety net for the 1% race.

---

**Q: Is there a remaining vulnerability in the checkout flow?**

A: Yes — the order is created *after* Stripe confirms payment. If the webhook fires in that
window, it finds no order. The correct fix is order-first: create the order before confirming
payment, then wait for SignalR before navigating to the success page. I documented it as a
known limitation — it requires reworking cart deletion and adding a SignalR timeout, which
is planned for Sprint 5 alongside order cancellation logic.

---

**Q: Why did `PaymentIntentId` change from `nvarchar(max)` to `nvarchar(450)` on SQL Server?**

A: SQL Server can't index unbounded columns. EF narrows it to `nvarchar(450)` automatically
when you add a unique index. Stripe IDs are ~27 chars so 450 is more than sufficient.

---

claude --resume 855033c5-43ce-41cf-afc0-7ed0bd5ba0b5
