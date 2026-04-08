# Task 16 — Stripe Idempotency Key

> Big picture: see [sprint-2-checkout-flow.md](sprint-2-checkout-flow.md) — Task 16 failure zone is at "Tab 2 → Tab 3".

## Why this approach

When the user moves from Shipping to the Payment tab, Angular calls `POST /api/payments/{cartId}`.
Your server calls Stripe `CreateAsync` to create a PaymentIntent. If the network blips between
Stripe creating the intent and your server receiving the response, a retry creates a **second**
PaymentIntent for the same cart — potential orphaned payment.

`IdempotencyKey = cart.Id` tells Stripe: "if you've already processed a request with this key,
return the same response — don't create a new resource."

## What confused me

Stripe creates unique PaymentIntent IDs automatically (`pi_abc123`). So why would you get duplicates?

**Uniqueness and idempotency are different things.**
Stripe will happily create two *unique* PaymentIntents for the same cart if you call `CreateAsync`
twice. The idempotency key prevents the second call from creating anything new at all.

## Explanation

### Where this fits in the checkout flow

```
Tab 2 → Tab 3 click
  → POST /api/payments/cart-999
      → Stripe CreateAsync(IdempotencyKey = "cart-999")  ← HERE
          → returns PaymentIntentId + ClientSecret
          → stored in Redis cart
```

Card data never touches your server — that happens in Leg 2 (Stripe.js directly).
The webhook (Leg 3) fires after the user pays and updates order status.

### Without the key (network blip scenario)

"Network blip" = Stripe created pi_abc123 and sent the response back, but it died on the
way to the server. Server times out → client sees error → clicks "Next" again.
Stripe on retry has no memory of pi_abc123 — it just sees a new `CreateAsync` request.

```
Call 1 → server → Stripe: pi_abc123 created ✓ → response dies → server times out
Call 2 → server → Stripe: no key → Stripe creates pi_xyz456 (brand new)
         → server stores pi_xyz456 in Redis
         → pi_abc123 orphaned on Stripe forever
```

### With the key

```
Call 1 → Stripe creates pi_abc123, stores response under "cart-999" → network blip
Call 2 → Stripe sees "cart-999" already processed → returns pi_abc123 again
One PaymentIntent, correct behaviour
```

### Implementation

```csharp
var intent = await serviceIntent.CreateAsync(
    options,
    new RequestOptions { IdempotencyKey = cart.Id }
);
```

`RequestOptions` is a Stripe SDK class. `IdempotencyKey` maps to the `Idempotency-Key` HTTP
header Stripe reads on every request. Stripe caches the response for 24 hours.

### Why cart.Id is the right key

One cart = one PaymentIntent. Cart ID is stable, unique per session, already in scope.

### Why Stripe doesn't enforce this by default

Stripe can't know your business boundary. Two `CreateAsync` calls with different amounts are
legitimately different requests. Opt-in keeps the API simple for scripts and one-shot tools
where retries never happen.

## Exercise

1. User opens checkout on two browser tabs simultaneously. Both call `POST /api/payments/{cartId}`.
   What happens without the key? With it?
2. Why is the key only on `CreateAsync` and not `UpdateAsync`?

## Solution

1. **Without key:** Stripe creates two PaymentIntents. One gets orphaned. Webhook confusion.
   **With key:** Second call returns the same `pi_abc123`. One intent, both tabs in sync.

2. `UpdateAsync` targets a specific `PaymentIntentId` — already idempotent by nature.
   `CreateAsync` has no target resource yet, so Stripe needs the key to identify duplicates.

## Interview Q&A

**Q: Why did you add an idempotency key to Stripe PaymentIntent creation?**

A: Without it, a network failure on retry creates a second PaymentIntent for the same cart —
a potential duplicate charge. `IdempotencyKey = cart.Id` tells Stripe to return the same
response if it's seen that key before. One cart should always map to exactly one PaymentIntent.

---

**Q: Stripe already assigns unique IDs. Why do you still need idempotency?**

A: Uniqueness prevents two intents from having the same ID. Idempotency prevents two intents
from being created for the same logical operation. Without the key, Stripe creates two *different*
unique intents — it has no way to know the second call is a retry, not a new purchase.

---

**Q: What's the scope of a Stripe idempotency key?**

A: Per API key, per endpoint. Cached for 24 hours. After that, a new call creates a fresh resource —
correct behaviour since the old intent would have expired anyway.

---

claude --resume 855033c5-43ce-41cf-afc0-7ed0bd5ba0b5
