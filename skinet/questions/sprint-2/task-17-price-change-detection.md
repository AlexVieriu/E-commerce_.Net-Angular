# Task 17 — Price Change Detection

## Why this approach

Cart prices are stored in Redis and can become stale — a product price can change in the DB between the time the user adds it to cart and the time they pay. We need to catch this **at order creation time** (the last gate before stock is decremented and the order is written to DB) and reject the order rather than silently charging the wrong amount.

Why 409 Conflict and not 400 Bad Request?
- 400 = the request itself is malformed (wrong format, missing fields)
- 409 = the request is valid but conflicts with current server state (prices changed since the request was built)

Why a custom `PriceChangedException` instead of `InvalidOperationException`?
- `InvalidOperationException` already maps to 400. We need a different status code.
- A typed exception makes the mapping unambiguous — no magic strings or special messages to check.
- `Core/Exceptions/` is the right layer: it's a domain rule, not infrastructure.

Why clear `PaymentIntentId` and `ClientSecret` from the cart before throwing?
- The Stripe PaymentIntent was already confirmed (succeeded) before `CreateOrderAsync` runs.
- If we don't clear it, the next checkout attempt tries to **update** a succeeded PaymentIntent → Stripe error.
- Clearing forces a fresh PaymentIntent to be created on the next attempt.

Why reload the cart in the Angular error interceptor?
- `OrderService` updates Redis with corrected prices before throwing.
- But the Angular `CartService` holds a signal — it doesn't re-fetch automatically.
- The interceptor calls `getCartAsync()` to sync the signal with the updated Redis data, so the user sees the correct prices immediately on `/cart`.

---

## What confused me

- **Timing of PaymentService vs OrderService**: `ValidateCartItemsInCartAsync()` in `PaymentService` runs when the user moves to the payment step — it silently corrects cart prices. So the only valid test window is changing the DB price **after** reaching the review step, then clicking Pay.
- **Idempotency key conflict**: After clearing `PaymentIntentId`, using `cart.Id` as the idempotency key for a new intent fails — Stripe already saw that key with a different amount. Fix: `cart.Id + Guid.NewGuid()` — keeps cart traceability in Stripe logs, Guid suffix ensures uniqueness per creation attempt.
- **`stepper.previous()` causing Stripe error**: The checkout `catch` block always called `stepper.previous()`, which triggered `onStepChange` → `createOrUpdatePaymentIntent()` on an already-succeeded intent. Fixed by checking `error instanceof HttpErrorResponse && error.status === 409` and returning early.

---

## Explanation

### Backend flow

```
POST /api/orders
  └── OrderService.CreateOrderAsync()
        ├── For each cart item:
        │     ├── Fetch product from DB
        │     ├── Compare item.Price (Redis) vs productItem.Price (DB)
        │     └── If different → add to priceChanges list, update item.Price
        ├── If priceChanges.Count > 0:
        │     ├── Clear cart.PaymentIntentId + cart.ClientSecret
        │     ├── SetCartAsync() → update Redis with corrected prices
        │     └── throw PriceChangedException("Prices have changed: ...")
        └── ExceptionMiddleware catches PriceChangedException → 409 Conflict
```

### Frontend flow

```
confirmPayment()
  ├── Stripe confirms payment (PaymentIntent = succeeded)
  ├── createOrder() → POST /api/orders
  │     └── 409 response
  ├── errorInterceptor catches 409:
  │     ├── snackbar.error(err.error.message)   ← 15s duration
  │     ├── cartService.getCartAsync(cartId)     ← reloads signal from Redis
  │     └── router.navigateByUrl('/cart')        ← user sees updated prices
  └── checkout catch block: if 409 → return early (no stepper.previous())
```

### Key files

| File | Change |
|------|--------|
| `Core/Exceptions/PriceChangedException.cs` | New — typed domain exception |
| `API/Middleware/ExceptionMiddleware.cs` | Maps `PriceChangedException` → 409 |
| `Infrastructure/Services/OrderService.cs` | Price comparison loop + cart update + throw |
| `Infrastructure/Services/PaymentService.cs` | Idempotency key: `cart.Id-{Guid}` |
| `client/error.interceptor.ts` | 409 handler: reload cart + navigate |
| `client/checkout.component.ts` | Skip `stepper.previous()` on 409 |

---

## Exercise

**Scenario:** A product costs $50. The user adds it to cart and reaches the review step. An admin changes the price to $75. The user clicks Pay.

1. What HTTP status does the API return?
2. What happens to the cart in Redis?
3. What does the user see in the UI?
4. Can the user retry checkout immediately? Why or why not?
5. What would happen if we did NOT clear `PaymentIntentId` from the cart?

---

## Solution

1. **409 Conflict** — `PriceChangedException` is thrown and mapped by `ExceptionMiddleware`.
2. The cart item price is updated to $75 in Redis, and `PaymentIntentId`/`ClientSecret` are cleared.
3. Snackbar: `"Prices have changed: <ProductName> ($50.00 → $75.00). Your cart has been updated."` Duration: 15s. Redirected to `/cart` showing $75.
4. **Yes** — `PaymentIntentId` was cleared, so `createOrUpdatePaymentIntent()` creates a fresh Stripe PaymentIntent on the next checkout attempt.
5. The next checkout attempt would call `createOrUpdatePaymentIntent()` which tries to **update** the existing (already-succeeded) PaymentIntent → Stripe throws: *"This PaymentIntent's amount could not be updated because it has a status of succeeded."* → 500 error.

---

## Tests

Manual tests — no unit test written for this task.

**Test 1 — Price change detected (happy path for the feature):**
1. Add item to cart, go through checkout steps 1 → 2 → 3 → reach step 4 (review)
2. Change product price in DB + commit
3. Click Pay
4. Expected: snackbar with `"Prices have changed: <Name> ($X → $Y). Your cart has been updated."` + redirect to `/cart` + cart shows updated price

**Test 2 — Happy path still works:**
1. Go through checkout with no DB price change
2. Expected: order completes, redirect to `/checkout/success`

**Test 3 — Retry after rejection works:**
1. After 409 rejection, go back to checkout
2. Expected: new PaymentIntent created (no Stripe "already succeeded" error), order completes

**Key timing note:** Changing the price BEFORE reaching step 3 won't trigger the 409 — `PaymentService.ValidateCartItemsInCartAsync()` silently corrects cart prices when `createOrUpdatePaymentIntent()` runs at step 3. The detection window is between step 3 entry and the Pay click.

## Interview Q&A

**Q: Why 409 instead of 400 for a price change?**
A: 400 means the request is malformed. 409 Conflict means the request is valid but conflicts with current server state — prices changed between cart creation and order submission. Semantically more accurate, and it lets the client handle it differently (reload + redirect vs show a validation error).

**Q: Why clear the PaymentIntentId when rejecting due to price change?**
A: The Stripe payment was already confirmed before `CreateOrderAsync` runs. If the user retries, `createOrUpdatePaymentIntent()` would try to update a succeeded PaymentIntent — Stripe rejects that. Clearing the ID forces a new PaymentIntent to be created with the correct amount.

**Q: Why is the idempotency key `cart.Id + Guid` instead of just `cart.Id`?**
A: `cart.Id` alone was the original key. After a price-change rejection, we clear the PaymentIntentId but keep the same cart. Using `cart.Id` again would hit Stripe's idempotency cache — same key, different amount = error. The Guid suffix guarantees a unique key per creation attempt. The `cart.Id` prefix preserves traceability in Stripe logs.

**Q: Where is the right place to put domain exceptions in Clean Architecture?**
A: `Core/Exceptions/` — the domain layer has zero dependencies, so exceptions that represent domain rules belong there. The API layer (`ExceptionMiddleware`) references Core to catch and map them. The dependency flows inward, not outward.

**Q: What's the risk of the PaymentService silently correcting prices vs the OrderService rejecting them?**
A: `PaymentService` corrects prices during `createOrUpdatePaymentIntent()` (step 3 of checkout). This keeps Stripe in sync but the user never sees the change — they could pay a higher price without knowing. `OrderService` is the explicit gate: it rejects the order and forces the user to acknowledge the price change before proceeding. Defence-in-depth: PaymentService handles Stripe correctness, OrderService handles user transparency.

--- 

claude --resume eff6bd99-a311-4263-a404-95747cc0eac5