## SECTION E — PRICE CHANGE DETECTION & 409 CONFLICT

> **Source:** Task 17 — `PriceChangedException`, cart clearing, 409 mapping, Angular error interceptor.

---

### EASY

**E1.** [HIGH] Why 409 Conflict and not 400 Bad Request for a price change?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**
- **400 Bad Request** — the request itself is malformed (missing fields, invalid format, validation errors). The client can fix it by correcting the payload.
- **409 Conflict** — the request is syntactically valid but conflicts with the current server state. The client can't fix it by resending the same payload; they need to reconcile with fresh state.

A price change is exactly 409: the cart is valid, the user submitted it correctly, but server state changed between cart creation and order submission.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"It doesn't matter, both are errors." Status codes are a contract with the client. The Angular interceptor switches on status code — 400 shows validation errors, 409 reloads the cart and redirects.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **RFC 9110** explicitly lists "resource version conflict" as a canonical 409 use case — shows you know the spec, not just convention.


</details>

---

**E2.** [HIGH] Where in Clean Architecture do you place `PriceChangedException`?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**
`Core/Exceptions/PriceChangedException.cs` — the **Core (domain) layer**.

Reasons:
- It represents a **domain rule**, not an infrastructure concern.
- Core has no dependencies, so exceptions defined here are reusable by any layer.
- API layer (`ExceptionMiddleware`) depends on Core (inward dependency), so it can catch and map.
- Infrastructure (`OrderService`) also depends on Core, so it can throw.

Dependency direction always flows **inward**: API → Application → Core, and Infrastructure → Core. Exceptions that cross layers live at the innermost layer they're used in.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Putting it in `API/Exceptions/` or `Infrastructure/Exceptions/`. Infrastructure would then leak out when the API layer catches it — it'd have to reference Infrastructure, violating Clean Architecture's inward flow.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that truly *infrastructure-specific* exceptions (e.g. `RedisConnectionException`) legitimately live in Infrastructure and should be **translated** at the service boundary into a Core exception before bubbling up. Keeps the layers clean.


</details>

---

### MEDIUM

**E3.** [HIGH] Walk through the full backend flow when a product's price changes between review and Pay.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

```
User clicks "Pay $341" on checkout step 4
  → Stripe.js confirms payment      [money already taken here — Task 18 known limitation]
  → POST /api/orders { cartId: cart-999 }

OrderService.CreateOrderAsync:
  1. Load cart from Redis → items with prices $50 each
  2. For each cart item:
     - Fetch product from DB
     - Compare item.Price ($50) vs productItem.Price ($75)
     - If different:
         - Add to priceChanges list (old=50, new=75)
         - Update item.Price = 75 in the in-memory cart
  3. If priceChanges.Count > 0:
     - cart.PaymentIntentId = null
     - cart.ClientSecret = null
     - await cartService.SetCartAsync(cart)   ← updates Redis with new prices
     - throw new PriceChangedException(message)

ExceptionMiddleware:
  case PriceChangedException: status = 409; body = { message: "Prices have changed: ..." }

Response: 409 Conflict, body with detailed message
```

Angular then handles 409 (see E4 for the frontend flow).


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Missing the cart update in Redis. The redirect to `/cart` depends on Redis having fresh prices — if you only throw without updating, the cart still shows old prices.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that this flow is **idempotent by design** — a second call would see the already-updated Redis cart, the price check would pass, and the order would proceed. No double-punishment for the user.


</details>

---

**E4.** [MEDIUM] Why clear `PaymentIntentId` and `ClientSecret` from the cart before throwing the price change exception?

<details>
<summary>The 5% Answer</summary>

- By the time `CreateOrderAsync` runs, the Stripe payment has already succeeded (frontend calls `confirmPayment` before `POST /api/orders`)
- The PaymentIntent is in `status = "succeeded"`

- **Without clearing**, on the user's retry `createOrUpdatePaymentIntent` tries to **update** the succeeded intent with the new amount:
  ```
  Stripe response: "This PaymentIntent's amount could not be updated because it has a status of succeeded."
  ```
  → User sees 500, checkout is permanently broken

- **With clearing** (`PaymentIntentId = null`, `ClientSecret = null`): `createOrUpdatePaymentIntent` takes the **CREATE** branch, creates a fresh intent at the new price — works cleanly

- **Production consequence:** without clearing, every price-change rejection permanently locks the user out until they manually rebuild their cart


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Clearing makes it fresh." True but not specific. You need to name the exact Stripe error.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that this is where Task 16's idempotency key design breaks down — the retry needs a new key (`cart.Id + Guid`, see section D) because Stripe's cache still has the old amount for `cart.Id` alone. Task 17 forced the Task 16 design change.


</details>

---

**E5.** [MEDIUM] The Angular error interceptor reloads the cart on 409. Why can't the signal just auto-refresh?

<details>
<summary>The 5% Answer</summary>

- Signals are **not reactive to external state** — they only fire when `.set()` or `.update()` is called from within Angular
- The Redis cart was updated **server-side** during the 409 path; Angular has no polling mechanism to detect that
- The interceptor explicitly reloads:
  1. Calls `cartService.getCartAsync(cartId)` → fetches fresh cart from API (which reads Redis)
  2. Calls `cart.set(newCart)` on the signal
  3. Angular sees the new object reference → re-renders cart displays
- Without the reload, the user would be redirected to `/cart` but see stale prices — worse than the original problem


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Signals are reactive, they should update." Signals react to `.set()` or `.update()` calls. They don't poll.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that a more sophisticated design uses **SignalR or WebSocket push** for price changes — the frontend receives real-time notifications when products in the user's cart change price, and can react before the user tries to pay. Push > polling > post-facto reconciliation.


</details>

---

**E6.** [HIGH] [OUT-OF-SCOPE] Your Angular checkout component fires `POST /api/orders` and the user navigates away before the response arrives. Describe the 3 problems this creates and show the correct fix using `takeUntilDestroyed()` combined with server-side idempotency.

<details>
<summary>The 5% Answer</summary>

**Problem 1 — Memory leak / write to destroyed component:**
The HTTP subscription stays alive after component destroy. When the response arrives, `this.order = result` writes to a detached view — Angular throws `ExpressionChangedAfterItHasBeenChecked` or silently corrupts state in zoneless mode.

**Problem 2 — Rogue navigation:**
If the response handler calls `this.router.navigate(['/checkout/success'])`, it fires after the user has already navigated elsewhere — unexpected redirect mid-browsing.

**Problem 3 — Orphaned server state:**
If the POST succeeded, the order exists in SQL but the client never received confirmation. The user has no receipt and no success page — they think checkout failed but money was taken.

**Fix — client side:**
```typescript
private destroyRef = inject(DestroyRef);

submitOrder() {
    this.checkoutService.createOrder(this.cart())
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
            next: (order) => this.router.navigate(['/checkout/success', order.id]),
            error: (err) => this.snackbar.error(err.error.message)
        });
}
```

**Why server-side idempotency is the other half:**
`takeUntilDestroyed` cancels the in-flight request but the server may have already started processing. Task 18's `PaymentIntentId` unique index ensures a retry creates no duplicate order. The cancel-doesn't-undo problem: always send a confirmation email from the server — the order may exist even if the client never saw the response.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Use `AbortController` to cancel the request on navigation." Cancelling the HTTP request doesn't undo server-side work. If the server already committed the order before the abort arrived, the order is real. `takeUntilDestroyed` + server idempotency is the correct two-layer defence.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `takeUntilDestroyed()` requires an **injection context** — it must be called inside `constructor`, a field initialiser, or via `inject(DestroyRef)`. Calling it inside a method without the injector throws `NG0203`. Also: `AsyncPipe` handles this automatically — subscribing in the template rather than manually is the zero-boilerplate equivalent.

</details>

---

### HARD

**E7.** [MEDIUM] There are TWO places where prices are reconciled — `PaymentService.ValidateCartItemsInCartAsync` (silently corrects) and `OrderService.CreateOrderAsync` (rejects with 409). Critique this split. Is it correct?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**It's defence-in-depth, and it's correct — but the naming is misleading.**

- `PaymentService` validates prices when the user **moves to the payment step** (step 3). It silently corrects so Stripe always holds the right amount. If we didn't do this, Stripe would charge the old price while the server has the new price — reconciliation nightmare.
- `OrderService` validates prices when the user **clicks Pay** (order creation). It rejects because the user is about to commit. They deserve to know.

**The split isn't arbitrary:**
- PaymentService's correction is **invisible to the user** because they haven't committed to the purchase yet.
- OrderService's rejection is **visible** because the user has reviewed and is about to pay.

**The critique:**
- **Naming is wrong** — `ValidateCartItemsInCartAsync` silently mutates. A better name: `SyncCartPricesBeforePaymentAsync`. The current name hides the side effect.
- **Edge case uncovered** — between step 3 (PaymentService runs) and step 4 click (OrderService runs), prices can change again. This is intentional (it's the 409 detection window), but the timing is invisible to anyone reading the code.
- **Should be a documented invariant** — a comment in `PaymentService` explaining "this sync is defence in depth for the happy path; the authoritative check is in OrderService at commit time."


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Duplicated logic, should be merged." Merging breaks the UX split — either every price change surfaces as an error (bad for common cases) or every change is silent (bad for commit-time changes).


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Proposing to extract a shared `PriceReconciler` service with two methods — `SyncQuietly` and `DetectAndThrow` — so the logic is DRY but the behaviour at each call site is explicit. Shows you understand DRY isn't "same code, must merge" but "same *intent*, must share."


</details>

---

**E8.** [HIGH] Find the 3 bugs in this price-change handler:

```csharp
public async Task<Order?> CreateOrderAsync(string email, CreateOrderDto dto)
{
    var cart = await _cartService.GetCartAsync(dto.CartId);
    foreach (var item in cart.Items)
    {
        var product = await _context.Products.FindAsync(item.ProductId);
        if (product.Price != item.Price)
        {
            item.Price = product.Price;
            throw new PriceChangedException($"Price changed for {product.Name}");
        }
    }
    // ... build and save order
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Bug 1 — throws inside the loop, loses multi-item changes.**
If items 1 and 2 both changed price, the user only sees item 1's change, fixes their cart, and is blindsided by item 2's change on retry. Fix: collect all changes into a list, throw once after the loop with a combined message.

**Bug 2 — cart not persisted to Redis before throw.**
`item.Price = product.Price` only updates the in-memory cart. The throw unwinds; Redis still holds the old prices. The redirect to `/cart` shows stale data. Fix: `await _cartService.SetCartAsync(cart)` before throw.

**Bug 3 — `PaymentIntentId` and `ClientSecret` not cleared.**
Without clearing, the retry's `UpdateAsync` fails on a succeeded PaymentIntent (see E4). Fix:
```csharp
cart.PaymentIntentId = null;
cart.ClientSecret = null;
```

**Also suspicious:** no null check on `product` (if the product was deleted, `FindAsync` returns null). Not strictly a price-change bug, but a robustness gap.

**Corrected:**
```csharp
var changes = new List<string>();
foreach (var item in cart.Items)
{
    var product = await _context.Products.FindAsync(item.ProductId);
    if (product == null) throw new ProductNotFoundException(item.ProductId);
    if (product.Price != item.Price)
    {
        changes.Add($"{product.Name} (${item.Price:F2} → ${product.Price:F2})");
        item.Price = product.Price;
    }
}
if (changes.Any())
{
    cart.PaymentIntentId = null;
    cart.ClientSecret = null;
    await _cartService.SetCartAsync(cart);
    throw new PriceChangedException($"Prices have changed: {string.Join(", ", changes)}. Your cart has been updated.");
}
```

**Production consequence:** each bug is invisible individually but together they produce a "user is stuck" state where every retry fails.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Only finding bug 1. The Redis persistence bug is subtle and requires understanding the frontend-backend coupling.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the SQL hot path has **N product lookups** — bug 4: N+1. Replace with a single `WHERE Id IN (...)` query. Matters on carts with many items.


</details>

---

**E9.** [MEDIUM] [OUT-OF-SCOPE] Attack scenario: can a user force checkout at an old lower price by tampering with the client? Describe the attack and why (or whether) Task 17 prevents it.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Attack attempt:**
1. User loads product at price $50, adds to cart (Redis: price=50).
2. Admin updates price to $75.
3. User modifies the Angular request to `POST /api/orders` to include a custom `price: 50` field.

**Why Task 17 prevents this:**
The server **doesn't trust client-supplied prices**. `OrderService.CreateOrderAsync` loads the cart from Redis (server-side state), then fetches each product's current price from the DB (server-side state). The comparison is Redis vs DB — the client isn't part of the decision.

Even if the user corrupts Redis somehow (they can't without a cart-ID collision), the DB check is the source of truth. Different Redis price from DB price → `PriceChangedException`.

**Remaining risk:**
Task 17 protects `POST /api/orders`. **It does NOT protect earlier steps.** If the user replayed an old valid checkout session where Redis still had the $50 cart, they could potentially pay $50 — but:
- Cart TTL is 30 days in Redis; after that the session is gone.
- `PaymentService.ValidateCartItemsInCartAsync` syncs prices at step 3, overwriting the old price before Stripe charges.

**The real gap:** there's a race window between step 3 (payment intent created at $50) and step 4 (order creation at $75). Stripe holds $50, server wants $75. Task 17's PaymentIntent clearing handles this — the user retries at the new price.

**Production consequence:** the only way to "cheat" is to race the sync step, and the order-creation rejection closes the race.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"The server validates the price" — too shallow. You need to identify *where* the authoritative comparison happens.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `POST /api/cart` also accepts a client-supplied price when items are added. That's the real attack surface — **the client-supplied cart prices are overwritten by DB prices at every payment-intent sync**, so the attack works only within the race window between step 3 and step 4, which Task 17 closes. Full defence-in-depth tour.


</details>

---

**E10.** [HIGH] [OUT-OF-SCOPE] Design the price-change UX from scratch for a principal-level review. What does the user see, what does the backend enforce, and where is the single source of truth?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Source of truth:** `Product.Price` in SQL Server. Always.

**Backend enforcements (ordered by trust):**
1. `PaymentService.ValidateCartItemsInCartAsync` on step-3 transition — syncs Redis cart to DB prices, silently updates Stripe intent amount. Invisible to user.
2. `OrderService.CreateOrderAsync` on Pay click — re-checks prices, rejects with 409 on any change. Source of truth for commit.
3. DB constraint (not implemented, could be added): `CHECK (OrderItem.Price > 0)` to catch silent corruption.

**Frontend UX:**
1. Product page — displays current DB price (fresh on load).
2. Cart page — displays Redis price (may lag; add a "last updated Xs ago" badge if price-volatile).
3. Checkout step 3 (payment) — shows the synced price (DB price), with a subtle "Prices verified ✓" indicator.
4. Checkout step 4 (review) — locks the displayed price; no background refresh.
5. On 409 — snackbar with specifics ("Item X now $75, was $50"), redirect to `/cart` showing new prices with a yellow highlight on changed items.

**Real-time improvements:**
- WebSocket/SignalR push: when any product in an active cart changes price, push a notification to that user. Rare but clean.
- Audit trail: record every price change with timestamp in `PriceHistory` table for forensic analysis.
- Admin gate: price changes > 20% require approval (prevents pricing bugs from flooding the 409 path).

**What to never do:**
- Trust client-supplied prices at any boundary.
- Silently auto-retry at the new price (user consent required).
- Use Redis as the source of truth for prices.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Focusing on the UI alone. The interviewer wants to hear the full SoT chain from DB → cart → Stripe → display.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **financial observability** — dashboards showing 409 rate by product, time-to-detect for price changes, Stripe/DB reconciliation checks. Price changes are a business event; treat them with monitoring, not just code.


</details>

---

### ⚡ Syntax Quick-Fire

**EQ1.** [HIGH] C# `throw` vs `throw ex` in a catch block — what's the key difference?

<details>
<summary>The 5% Answer</summary>

- **`throw`** (bare) rethrows the original exception, **preserving the full stack trace**.
- **`throw ex`** resets the stack trace to the current line — you lose the original call site, making debugging much harder.

```csharp
catch (DbUpdateConcurrencyException ex)
{
    throw new InvalidOperationException("Stock changed.", ex);  // wraps with inner — GOOD
    // throw ex;  // BAD — stack trace now starts here
    // throw;     // GOOD — original stack trace preserved
}
```

- Always prefer `throw` or wrapping in a new exception with the original as `innerException`. Never `throw ex` unless you intentionally want to reset the trace.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "They're the same — both rethrow." Interviewers catch this. The stack trace difference is a real debugging cost.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Noting that `ExceptionDispatchInfo.Capture(ex).Throw()` can rethrow a captured exception with its original stack trace from a different context (e.g., from an `async` callback). Advanced pattern for background-thread exception propagation.

</details>

---

**EQ2.** [HIGH] Angular `computed(() => cart().total)` — when does it recalculate?

<details>
<summary>The 5% Answer</summary>

- Only when a **signal dependency changes** (signals read inside the `computed` callback).
- **Lazy and memoised** — if no signal changed, multiple reads return the cached value without re-running the function.
- The `computed` itself is a signal — it only runs when something reads it AND a dependency has changed since the last read.

```typescript
const total = computed(() => cart().items.reduce((s, i) => s + i.price * i.qty, 0));

total(); // runs the function — caches result
total(); // returns cached result (no signal changed)

cart.update(c => ({ ...c, items: [...c.items, newItem] }));  // cart changed
total(); // runs the function again (cart dependency changed)
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Every time you read it." Computed is lazy — it doesn't recalculate on every read, only when dependencies changed.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing that `computed` with `{ equal: fn }` lets you control when the computed's output is considered "changed" — useful for derived arrays where you want reference equality on the same content.

</details>

---

**EQ3.** [HIGH] C# LINQ: `Any()` vs `Count() > 0` — which is preferred and why?

<details>
<summary>The 5% Answer</summary>

- **`Any()`** is preferred — it **short-circuits** on the first element; no need to enumerate the rest.
- `Count()` iterates the entire collection, even if only presence matters.
- For `IQueryable` (EF Core), `Any()` generates `SELECT CASE WHEN EXISTS(...)` — far cheaper than `SELECT COUNT(*)` for large tables.

```csharp
// LINQ to Objects — Any short-circuits, Count iterates all
var hasItems = cart.Items.Any();        // stops at first item
var hasItems2 = cart.Items.Count() > 0; // iterates all items

// EF Core — Any generates EXISTS, Count generates COUNT(*)
var hasOrders = await _context.Orders.AnyAsync(o => o.BuyerEmail == email);
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "`Count() > 0` is clearer." Clarity is a style argument — but the semantics of `Any()` are exact: "does at least one exist?" That's more expressive, not less.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Noting that `.Count` (property, not method) on `ICollection<T>` is O(1) — safe to use when you already have a materialised collection with a known count. The `Any()` preference applies to `IEnumerable<T>` / `IQueryable<T>`.

</details>

---
