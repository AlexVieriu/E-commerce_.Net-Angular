## SECTION F — ORDER IDEMPOTENCY & UNIQUE INDEXES

> **Source:** Task 18 — app-level check for existing order + unique index on `Order.PaymentIntentId`.

---

### EASY

**F1.** [HIGH] [OUT-OF-SCOPE] What is a unique index and what does it protect against?

<details>
<summary>The 5% Answer</summary>

- A DB-level index declared `UNIQUE` that rejects any INSERT/UPDATE which would cause duplicate values in the indexed column(s)
- Built as a **B-tree** with uniqueness enforced at the storage layer in SQL Server
- Violations throw `DbUpdateException` in EF Core (wraps SQL Server's `DuplicateKeyException`)
- It's the **last line of defence** against duplicate business entities when app-level checks race


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"It makes the column unique." Doesn't describe the mechanism or the failure mode.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning the difference between a **unique constraint** (logical) and a **unique index** (physical) — SQL Server implements both with an index, but semantically the constraint is the declaration and the index is the enforcement mechanism. Other engines may differ.


</details>

---

**F2.** [MEDIUM] What HTTP response does a `DbUpdateException` from a unique-index violation become?

<details>
<summary>The 5% Answer</summary>

- By default: bubbles up to `ExceptionMiddleware` → **500 Internal Server Error** with a generic "An error occurred" message
- The raw EF exception message is never shown to the user
- This is **acceptable but not ideal** — the user whose double-click hit the true race gets a hard 500
- **Better design:** catch the `DbUpdateException`, re-query for the existing order by `PaymentIntentId`, return 200 with the existing order — same as the app-level check returns


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"409 Conflict." Only if you explicitly catch and map it. Default behaviour is 500.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that SQL Server's specific error code for unique-index violation is `2601` (duplicate key in unique index) or `2627` (unique constraint violation). Catching by code, not by string match, makes the handler portable and robust.


</details>

---

### MEDIUM

**F3.** [HIGH] Why do you need BOTH an app-level check AND a DB constraint? Describe a scenario where only one fails.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**App-level check only — fails under true concurrency:**
```
Request A at T=0: SELECT order WHERE pi='pi_abc' → none → INSERT → commit at T=50ms
Request B at T=10: SELECT order WHERE pi='pi_abc' → none (A not committed yet) → INSERT → commit at T=60ms
Result: TWO orders with pi='pi_abc' — the app check raced.
```

**DB constraint only — works but bad UX:**
```
Request A: INSERT ✓ (no prior order, returns 200 with new order)
Request B: INSERT ✗ → DbUpdateException → 500 to the user
User sees: "Something went wrong" despite the order actually existing.
```

**Both together — defence in depth:**
- 99% case (sequential double-click): app check finds existing order, returns it cleanly. 200 with existing order.
- 1% case (true race): app check misses, DB rejects, 500. Still better than duplicate orders; can be upgraded to a graceful handler.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Belt and braces." Lazy — a senior wants the concrete race scenario.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning the TOCTOU (Time-of-Check to Time-of-Use) pattern. The app-level check is classic TOCTOU; the DB constraint is the atomic "check and use in one step." Naming the pattern shows concurrency literacy.


</details>

---

**F4.** [HIGH] Describe the double-click bug without Task 18. What's the observable user impact?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

Without any guard:
```
User double-clicks "Pay $341"
  Request A → reads cart pi_abc → inserts Order #1 → commits → deletes cart
  Request B → reads cart pi_abc (cart still there, deletion is post-commit) → inserts Order #2 → commits
  SQLite: 2 orders with pi_abc, identical amount, same buyer

Stripe webhook fires for pi_abc:
  findOne(PaymentIntentId = 'pi_abc') → returns ONE row (first match)
  Updates Order #1 to PaymentReceived
  Order #2 stays Pending forever
```

**User impact:**
- Sees success page (after Request A completed — Angular doesn't wait for B).
- Visits "My Orders" — sees TWO orders, one Pending.
- Panic support ticket: "I paid once, why two orders? Why is one unpaid?"
- Inventory decremented twice. If the product had stock 1, the second decrement goes to -1 → negative stock bug.

**Task 18 prevents this:** app-level check on Request B finds Order #1 → returns it without inserting. Stock decrement only happens once (inside the app check conditional).

**Production consequence:** without the guard, a customer with a shaky trackpad doubles your inventory deduction and your support load.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Two orders." Shallow. Interviewer wants stock impact, webhook impact, user-visible state.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that this also breaks **revenue analytics** — `SUM(Order.Total) GROUP BY Day` now double-counts. Silent data corruption that might not surface for weeks.


</details>

---

**F5.** [MEDIUM] [OUT-OF-SCOPE] `PaymentIntentId` changed from `nvarchar(max)` to `nvarchar(450)` when the unique index was added. Why?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

SQL Server **cannot index `nvarchar(max)`** (or any unbounded column). B-tree index keys have a max size of 900 bytes in SQL Server. At 2 bytes per unicode char, that's 450 chars max. EF Core narrows the column to `nvarchar(450)` automatically when you add `HasIndex(...).IsUnique()` on an unbounded column.

Stripe PaymentIntent IDs are ~27 chars (`pi_` prefix + 24 chars). 450 is massively sufficient.

Migration snippet:
```csharp
migrationBuilder.AlterColumn<string>(
    name: "PaymentIntentId",
    table: "Orders",
    type: "nvarchar(450)",
    maxLength: 450,
    ...
);
migrationBuilder.CreateIndex(
    name: "IX_Orders_PaymentIntentId",
    table: "Orders",
    column: "PaymentIntentId",
    unique: true);
```


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Performance." Not the reason — it's a hard SQL Server limit, not an optimisation choice.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **SQLite has no such limit** — it can index text of any size. This is a SQL Server-specific constraint. If your project targets only SQLite, `nvarchar(max)` could be indexed.


</details>

---

**F6.** [HIGH] Find the TOCTOU race in this app-level idempotency check:

```csharp
public async Task<Order?> CreateOrderAsync(string email, CreateOrderDto dto)
{
    var cart = await _cartService.GetCartAsync(dto.CartId);
    var existing = await _unitOfWork.Repository<Order>()
        .GetEntityWithSpec(OrderSpecification.ByPaymentIntent(cart.PaymentIntentId));
    if (existing != null) return existing;

    // ... build order, decrement stock ...
    _unitOfWork.Repository<Order>().Add(order);
    await _unitOfWork.Complete();
    return order;
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**The race:** between `GetEntityWithSpec` returning null and `Complete()` committing, another request can insert a duplicate:

```
Time   Request A                        Request B
T=0    GetEntityWithSpec → null
T=1                                     GetEntityWithSpec → null
T=2    Add → Complete → Order #1 committed
T=3                                     Add → Complete → Order #2 committed
                                        (no app check caught it — A not visible at T=1)
```

Result: two orders, both passed the check, both committed.

**Why a transaction doesn't fix it alone:**
Default isolation is READ COMMITTED — reads don't see uncommitted data but also don't lock out other readers. Both requests' SELECT returns null. You'd need SERIALIZABLE isolation or a SELECT FOR UPDATE lock, which kills throughput.

**Fix — the unique index on `PaymentIntentId`:**
Request B's `Complete()` throws `DbUpdateException` due to the unique index. The app-level check handles 99% of cases (sequential double-clicks); the unique index handles the 1% true race.

**Improved handler:**
```csharp
try
{
    await _unitOfWork.Complete();
}
catch (DbUpdateException ex) when (IsUniqueViolation(ex))
{
    // Race: another request just created the same order. Fetch and return it.
    return await _unitOfWork.Repository<Order>()
        .GetEntityWithSpec(OrderSpecification.ByPaymentIntent(cart.PaymentIntentId));
}
```

Now the true race is handled gracefully: re-query, return existing, status 200.

**Production consequence:** without the unique index, the TOCTOU race produces real duplicate orders under load. With only the unique index (no graceful handler), the 1% race gets a 500 — better than duplicates, worse than ideal.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Add a lock in C#." In-process lock is useless across replicas. Must be a distributed lock (Redis) or a DB-level constraint.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **application-level locks via Redis (RedLock)** are a valid alternative to DB unique indexes — cheaper than a DB index for high-write scenarios, but adds Redis as a critical dependency in the write path. Tradeoff discussion is what interviewers want.


</details>

---

### HARD

**F7.** [HIGH] There's a known limitation: orders are created AFTER the Stripe payment is confirmed. Describe the race and the correct fix.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Current (buggy) flow:**
```
1. stripe.confirmPayment(token)         ← Stripe charges card, returns succeeded
2. POST /api/orders                     ← order inserted in SQLite
3. navigate to /checkout/success
[in parallel, on Stripe's timeline]
X. Stripe → POST /api/payments/webhook  ← fires after charge completes
```

**The race:**
- Webhook can fire **between step 1 and step 2** (Stripe's async infrastructure is faster than the user's network RTT to your API).
- Webhook handler runs: `findOne(PaymentIntentId = 'pi_abc')` → no order exists yet → nothing to update.
- Step 2 finishes: order inserted with status=Pending.
- **Order stays Pending forever.** Webhook has already fired and won't retry.

**User impact:** success page shows, "View My Order" shows `Pending`. User panics. Support ticket: "I paid, why is my order unpaid?"

**Correct fix — order-first pattern:**

```
1. POST /api/orders                     ← order inserted with status=Pending
2. stripe.confirmPayment(token)         ← Stripe charges card
3. Webhook fires → order EXISTS → update status=PaymentReceived
4. SignalR notifies client → navigate to /checkout/success
```

**Why not implemented in Sprint 2:**
- Requires not deleting the cart until after payment succeeds (currently `CreateOrderAsync` deletes the cart immediately — retry after failed payment would find no cart).
- Needs a SignalR timeout fallback (what if the SignalR notification never arrives?).
- Interacts with Task 41 (order cancellation + stock reversal in Sprint 5).

Documented as a known limitation. The interview answer: "I identified the race, designed the correct fix, and deferred it because it requires rework beyond Sprint 2's scope."

**Production consequence:** in the current flow, a user with fast Stripe infrastructure (enterprise customers on Stripe's expedited lanes) hits this race more than average. Small user base hides the bug; scale exposes it.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Create the order in the webhook." Circular — the webhook needs the order to exist to tie payment to it.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the correct fix also needs a **SignalR timeout fallback**: if the webhook doesn't arrive within 30s, fall back to polling `GET /api/orders/{id}` until status changes or timeout. Belt-and-braces for webhook delivery failures. Production payment systems always assume webhooks can be delayed.


</details>

---

**F8.** [MEDIUM] [OUT-OF-SCOPE] The Stripe webhook finds orders by `PaymentIntentId`. Why does the unique index make webhook logic simpler and safer?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Before the unique index:**
```csharp
var orders = await _context.Orders
    .Where(o => o.PaymentIntentId == paymentIntentId)
    .ToListAsync();

// What now? 0 rows = order not yet created. 1 row = normal. 2+ rows = ???
if (orders.Count > 1) { /* pick which? log? alert? */ }
```

The webhook author has to reason about "multiple matching orders" — which one do I update? What if they disagree on status? The code either:
- Arbitrarily picks the first (wrong, non-deterministic)
- Updates all (wrong, may corrupt already-processed ones)
- Logs and gives up (leaves the paid order in Pending)

**After the unique index:**
```csharp
var order = await _context.Orders
    .SingleOrDefaultAsync(o => o.PaymentIntentId == paymentIntentId);

// Exactly 0 (race with order creation) or exactly 1. No ambiguity.
if (order == null) { /* retry path or enqueue */ }
else { order.Status = OrderStatus.PaymentReceived; }
```

The unique index **changes the domain model**: "one PaymentIntent maps to at most one Order" is now enforced by the DB, not hoped for by the app. The webhook becomes a one-liner.

**Production consequence:** webhooks that handle "what if duplicates" are always fragile. Enforcing single-match at the DB level eliminates entire classes of bugs.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"The index makes lookups faster." True (B-tree vs table scan) but not the main benefit — the semantic guarantee is the win.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `SingleOrDefault` vs `FirstOrDefault` is the linguistic expression of this guarantee. Using `Single*` communicates "the DB guarantees 0 or 1." Using `First*` communicates "I'm just grabbing one of possibly many." Code should express domain invariants, not hide them.


</details>

---

**F9.** [HIGH] Design: implement the "create order first" pattern. What breaks with cart deletion, and how do you fix it?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Flow:**
```
1. User clicks Pay
2. Client → POST /api/orders { cartId }
   Server:
     - Create order with status=Pending
     - DO NOT delete cart
     - Return orderId + clientSecret
3. Client → stripe.confirmPayment(clientSecret)
4. Stripe → webhook /api/payments/webhook
   Server:
     - Find order by PaymentIntentId (unique index)
     - status = PaymentReceived
     - Delete cart from Redis
     - Push SignalR event "order-updated" to buyer
5. Client receives SignalR → navigate to /checkout/success
6. Timeout fallback: if no SignalR after 30s, poll GET /api/orders/{id}
```

**What breaks with naive cart deletion (moving it to post-payment):**

1. **Retry after payment failure** — user's card declines at step 3. They retry from step 2. Current flow: cart was deleted at order creation, retry finds no cart, fails. **Fix:** delete cart only in the webhook (after payment success), not in the order creation step.

2. **Stock reservation timing** — if order creation decrements stock and the payment fails, stock is silently lost. **Fix:** add a `Pending → Cancelled` transition in the webhook when Stripe sends `payment_intent.payment_failed`. Restore stock on cancel. This is Task 41 territory.

3. **User abandons after order creation** — they create the order, close the tab, don't pay. Order sits Pending forever. **Fix:** a background job to cancel orders older than 30 min with `status=Pending`, restore stock.

**Diff vs current flow:**
```diff
- public async Task<Order?> CreateOrderAsync(...) {
-     ... commit order ...
-     await _cartService.DeleteCartAsync(cartId);   // remove
-     return order;
- }
```

```diff
+ public async Task HandlePaymentIntentSucceeded(...) {
+     var order = await _context.Orders.SingleOrDefaultAsync(...);
+     order.Status = OrderStatus.PaymentReceived;
+     await _context.SaveChangesAsync();
+     await _cartService.DeleteCartAsync(cartId);   // moved here
+     await _hub.Clients.User(...).SendAsync("order-updated", order.Id);
+ }
```

**Production consequence:** without the cancel-and-restore job, abandoned carts leak inventory. For low-stock items this is business-critical.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Just moving the cart delete without handling retry, abandonment, or stock restore. It's not a one-line fix.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **saga orchestration** — this is a multi-step workflow with compensating actions. At scale, an explicit saga framework (MassTransit, NServiceBus) beats ad-hoc if/else. For this project, the manual approach is fine; knowing when to reach for saga frameworks is senior-level.


</details>

---

### ⚡ Syntax Quick-Fire

**FQ1.** [HIGH] EF Core: `dbSet.Add(entity)` vs `dbSet.Attach(entity)` — what entity state does each set?

<details>
<summary>The 5% Answer</summary>

- **`Add()`** marks the entity as `EntityState.Added` — EF generates an `INSERT` on `SaveChanges`.
- **`Attach()`** marks it as `EntityState.Unchanged` — EF tracks it but generates no SQL unless you modify properties (which then become `Modified` → `UPDATE`).
- Use `Attach` when you have an entity by ID from outside EF (e.g., a DTO) and want to update only certain fields without re-loading from DB.

```csharp
// Attach + selective update — no SELECT round-trip
var order = new Order { Id = 42 };
_context.Orders.Attach(order);
order.Status = OrderStatus.Cancelled;  // only Status UPDATE is generated
await _context.SaveChangesAsync();
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "`Attach` and `Add` are similar." One generates INSERT, the other generates nothing (until a property changes). Very different.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** `_context.Entry(entity).State = EntityState.Modified` — explicitly marks ALL properties as modified (generates UPDATE for every column). Use when you can't predict which fields changed. Less efficient than `Attach` + selective property change.

</details>

---

**FQ2.** [HIGH] C# `record with { }` — does it deep-copy nested reference objects?

<details>
<summary>The 5% Answer</summary>

- **No — `with` is shallow.** The new record gets a copy of each field value, but reference-type fields share the same object.

```csharp
record Address(string City, string Street);
record Order(int Id, Address Address, decimal Total);

var o1 = new Order(1, new Address("Bucharest", "Main St"), 100m);
var o2 = o1 with { Total = 200m };

// o2.Address is the SAME object as o1.Address
ReferenceEquals(o1.Address, o2.Address);  // true

// To deep-copy, explicitly clone the nested record:
var o3 = o1 with { Address = o1.Address with { City = "Cluj" } };
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Records are immutable so `with` is a deep copy." Immutability is about the record fields, not the objects they reference.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing this is intentional — `with` is efficient precisely because it doesn't deep-clone. Nested records follow the same pattern recursively, making the full deep-copy opt-in and explicit.

</details>

---

**FQ3.** [HIGH] EF Core: `SaveChanges` vs `SaveChangesAsync` — which should you use in an ASP.NET Core API?

<details>
<summary>The 5% Answer</summary>

- **Always `SaveChangesAsync`** in ASP.NET Core. The synchronous version blocks the calling thread during the DB operation.
- Under load, blocking threads starves the thread pool — requests queue up, timeouts cascade.
- `SaveChangesAsync` yields the thread back to the pool during the network round-trip.
- **Exception:** console apps or seeding scripts that don't serve HTTP traffic can use `SaveChanges` for simplicity.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Either is fine — they do the same thing." Same result, different thread behaviour under load.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Noting that `DbContext.SaveChanges()` in a production API is a code review flag — same category as `Thread.Sleep()`. Automated static analysis (Roslyn analyzer) can catch this.

</details>

---
