claude --resume 855033c5-43ce-41cf-afc0-7ed0bd5ba0b5

# Task 16 — Stripe Idempotency Key

## Why this approach

Stripe's `PaymentIntent` creation is not idempotent by default. If the HTTP call to Stripe succeeds but your server never receives the response (network blip, timeout), a retry creates a second PaymentIntent for the same cart — potential duplicate charge.

Passing `IdempotencyKey = cart.Id` tells Stripe: "if you've already processed a request with this key, return the same response — don't create a new resource."

## What confused me

Stripe creates unique PaymentIntent IDs automatically (`pi_abc123`). So why would you get duplicates?

The answer: uniqueness and idempotency are different things. Stripe will happily create two *unique* PaymentIntents for the same cart if you call `CreateAsync` twice — they each get a unique ID. The idempotency key prevents the second call from creating a new intent at all.

## Explanation

### The full Stripe payment flow (3 legs)

```
Leg 1 — Your server → Stripe
  POST /api/payments/{cartId}
    └── PaymentService.CreateOrUpdatePaymentIntent()
          └── Stripe CreateAsync() ← IdempotencyKey = cart.Id
                → returns PaymentIntentId + ClientSecret
                → stored in Redis cart

Leg 2 — User → Stripe directly
  Angular uses ClientSecret + Stripe.js to collect card details
  Card data never touches your server
  Stripe processes the payment

Leg 3 — Stripe → Your server (webhook)
  POST /api/payments/webhook
    └── Finds order by PaymentIntentId
          └── Sets OrderStatus.PaymentReceived
                └── SignalR push to buyer
```

### Where IdempotencyKey fits

Only Leg 1. Once you have a `PaymentIntentId`, Legs 2 and 3 reference it by ID — already idempotent by nature.

### What happens without the key on a retry

```
Call 1 → Stripe creates pi_abc123 → network blip → your server gets no response
Call 2 → Stripe creates pi_xyz456 → your server stores this one
Result: pi_abc123 is an orphaned open PaymentIntent
```

With `IdempotencyKey = cart.Id`:
```
Call 1 → Stripe creates pi_abc123, stores response under key "cart-id-999" → network blip
Call 2 → Stripe sees "cart-id-999" already processed → returns pi_abc123 again
Result: one PaymentIntent, correct behaviour
```

### Why cart.Id is the right key

One cart should produce exactly one PaymentIntent. Cart ID is stable, unique per user session, and already in scope. It's the natural idempotency boundary.

### Implementation

```csharp
var intent = await serviceIntent.CreateAsync(
    options,
    new RequestOptions { IdempotencyKey = cart.Id }
);
```

Stripe stores the idempotency key + response for 24 hours. Requests with the same key within that window return the cached response.

## Exercise

1. A user opens checkout on two browser tabs simultaneously. Both tabs call `POST /api/payments/{cartId}`. What happens without the idempotency key? What happens with it?
2. Why is the idempotency key only on `CreateAsync` and not on `UpdateAsync`?
3. Stripe stores idempotency keys for 24 hours. What happens if a cart is abandoned for 25 hours and the user returns?

## Solution

1. **Without key:** two calls to Stripe create two PaymentIntents. Both tabs store different `PaymentIntentId`s — one gets orphaned. Potential confusion at webhook time. **With key:** second call returns the same PaymentIntent. Both tabs end up with the same `pi_abc123`. Safe.

2. `UpdateAsync` takes an existing `PaymentIntentId` as its first argument — it targets a specific resource. Calling it twice with the same ID and same amount is already safe (idempotent by design — updating a value to itself is a no-op). `CreateAsync` has no target resource yet, so without a key Stripe can't know it's a duplicate request.

3. After 24 hours Stripe's cached response expires. A new `CreateAsync` call with the same `cart.Id` creates a fresh PaymentIntent. This is correct behaviour — the old intent would have expired or been cancelled anyway.

## Interview Q&A

**Q: Why did you add an idempotency key to the Stripe PaymentIntent creation?**

A: Without it, a network failure between my server and Stripe on a retry creates a second PaymentIntent for the same cart — a potential duplicate charge. The idempotency key tells Stripe to return the same response if it's seen that key before. I used `cart.Id` as the key because one cart should map to exactly one PaymentIntent, and it's already in scope at that point in the code.

---

**Q: Stripe already assigns unique IDs to PaymentIntents. Why do you still need idempotency?**

A: Uniqueness and idempotency solve different problems. Stripe's unique IDs prevent two intents from having the same ID. But without an idempotency key, Stripe will create two *different* unique intents if you call `CreateAsync` twice — it has no way to know they're for the same logical operation. The idempotency key provides that business-level identity: "this is the same request, give me the same result."

---

**Q: What's the scope of a Stripe idempotency key?**

A: Per API key, per endpoint. Stripe stores it for 24 hours. Within that window, any request with the same key returns the cached response without creating a new resource. After 24 hours the key expires and a new call creates a fresh resource.
