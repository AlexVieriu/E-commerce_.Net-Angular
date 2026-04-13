## SECTION F — STRIPE, SIGNALR & CHECKOUT FLOW

### MEDIUM

**F1.** Draw the full Stripe payment flow from button click to order creation:
```
Browser → Stripe.js → Stripe API → Your API → OrderService → DB → Stripe webhook → SignalR → Angular
```
Include: what tokens are passed, where the SecretKey is used, what the webhook verifies.

<details><summary>Answer</summary>

```
1. Angular → POST /api/payments/create-intent
   Your API uses SecretKey to call Stripe → creates PaymentIntent
   Returns { clientSecret } to Angular

2. Angular → Stripe.js.confirmPayment({ clientSecret, payment_element })
   Stripe.js sends card details directly to Stripe API (SecretKey never leaves server)
   Stripe returns: { paymentIntent.status = "succeeded" }

3. Angular → GET /api/orders (or poll) — waits for order confirmation via SignalR

4. Stripe → POST /api/payments/webhook  (async, minutes later or seconds)
   Header: Stripe-Signature (HMAC with WebhookSecret)
   Your API: ConstructEvent(payload, signature, WebhookSecret) → verifies authenticity
   Payload: { type: "payment_intent.succeeded", data.object.id: "pi_xxx" }

5. Your API → OrderService.CreateOrUpdateOrder(paymentIntentId)
   Finds or creates Order by PaymentIntentId → saves to DB → deletes cart

6. Your API → SignalR Hub → pushes "OrderCompleteNotification" to client group

7. Angular hub listener fires → hides spinner → shows order confirmation
```

**Token summary:**
- `SecretKey` — server-side only, never in browser, used to call Stripe REST API
- `clientSecret` — returned to browser, used by Stripe.js to confirm the payment
- `Stripe-Signature` — HMAC header on webhook; verified with `WebhookSecret` to prove Stripe sent it
</details>

---

**F2.** Two webhook callbacks arrive for the same `PaymentIntentId`. How do you prevent duplicate orders?

<details><summary>Answer</summary>

**Idempotency via unique constraint on `PaymentIntentId`.**

Before creating an order, check if one already exists for that `PaymentIntentId`. If it does, return early (or update status if needed) instead of inserting a new row.

```csharp
public async Task<Order?> UpdateOrderPaymentSucceeded(string paymentIntentId)
{
    var order = await _context.Orders
        .FirstOrDefaultAsync(o => o.PaymentIntentId == paymentIntentId);

    if (order == null) return null; // no matching order — ignore

    order.Status = OrderStatus.PaymentReceived;
    await _context.SaveChangesAsync();
    return order;
}
```

Add a unique index on `PaymentIntentId` in the DB schema as a belt-and-suspenders guard. Even if two concurrent webhook calls both pass the "does order exist?" check, the second `INSERT` will fail at the DB level with a unique constraint violation.
</details>

---

**F3.** Why is the cart deleted AFTER the transaction commits, not inside it? What happens if cart deletion fails after commit?

<details><summary>Answer</summary>

**Cart is in Redis; the order is in SQL. You cannot put them in the same transaction — they're different systems.**

The safe order is:
1. `SaveChangesAsync()` — commits order to SQL (durable)
2. `_cartService.DeleteCartAsync(cartId)` — removes from Redis (best-effort)

If you deleted the cart first and `SaveChangesAsync` then failed, the user loses their cart with no order created — unrecoverable data loss from the customer's perspective.

**If cart deletion fails after commit:** the order exists in the DB (good). The Redis cart is orphaned but harmless — it will expire via TTL, or the user's next login/cart fetch can clean it up. An orphaned cart is far less damaging than a lost order.
</details>

---

**F4.** What is `IdempotencyKey` in Stripe? Why do you use `cart.Id` as the key? What happens if two requests use the same key?

<details><summary>Answer</summary>

`IdempotencyKey` is a client-supplied string that tells Stripe: "if you've seen this key before, return the same result — don't create a new resource."

Using `cart.Id` means: if the user's browser retries the "create payment intent" call (network blip, double-click), Stripe returns the EXISTING PaymentIntent with the same `client_secret` — no second charge is created.

**If two requests use the same key:**
- Same endpoint + same key → Stripe returns the cached response (same PaymentIntent object)
- Different endpoint + same key → Stripe returns 400 Idempotency error

The key must be unique per cart, not per user session — if the user empties and refills their cart, a new cart ID generates a new PaymentIntent.
</details>

---

### HARD

**F5.** Your Angular app suddenly shows the checkout spinner forever. The payment succeeded (you see the charge in Stripe Dashboard). What's the debugging checklist? (Give at least 6 steps.)

<details><summary>Answer</summary>

1. **Stripe Dashboard → Webhooks → check delivery status.** Did the webhook fire? Did your endpoint return 200? If it shows "Failed" with a 500/timeout, the webhook never processed.
2. **Check server logs for the webhook endpoint.** Did the signature verification (`ConstructEvent`) throw? Did the order creation succeed? Did `SaveChangesAsync` throw a concurrency exception?
3. **Browser DevTools → Network tab → filter by WS.** Is the SignalR WebSocket connection established? Is it open at the time the webhook should fire?
4. **Check SignalR hub group membership.** Did the Angular client call `HubConnection.invoke("JoinGroup", email)` BEFORE the webhook fired? If not, the notification was sent to a group with no members.
5. **Check CORS for SignalR.** Does `app.UseCors(...)` allow the SignalR hub's origin and credentials (`AllowCredentials()`)?
6. **Check `hubConnection.on("OrderCompleteNotification", ...)` registration.** Is the listener registered before calling `hubConnection.start()`? If registered after, the first notification is missed.
7. **Stripe test mode timing.** In test mode, webhooks can fire in milliseconds — faster than Angular mounts the hub. Add a "join group then confirm payment" sequence to guarantee the listener is ready.
8. **Check if the webhook is hitting the right environment.** Dev webhook pointing at localhost? Stripe CLI proxy (`stripe listen --forward-to localhost:5001/...`) needed for local testing.
</details>

---

**F6.** `PaymentService` calls `new PaymentIntentService()` and `new RefundService()` directly — no DI seam. You want to unit test it. What are the three options, and which is right for this project?

<details><summary>Answer</summary>

1. **Wrapper interface** — create `IStripeService` wrapping `PaymentIntentService` and `RefundService`. Inject the interface. Mock it in tests. **Best option** — clean, testable, follows DI principles.

2. **Protected virtual factory method** — extract `new PaymentIntentService()` into a `protected virtual` method. Subclass in tests to inject a fake. Works without changing the public API. Acceptable if you truly can't change the constructor.

3. **Integration test against Stripe test API** — use real Stripe with `sk_test_...` keys. No mocks. Honest but slow, requires network, can be flaky, and leaks test keys unless handled carefully via environment variables.

**Right for this project:** Option 1. We control the code, DI is already set up, and the Stripe SDK classes aren't sealed — wrapping them is trivial. Write the wrapper, register it in DI, mock the interface in unit tests.
</details>

---

**F7.** Design a "stock reservation" system: when a user starts checkout, reserve their items for 15 minutes. If they don't complete, release the reservation. Design the data model, reserve/release flows, and the background job. How do you handle a reservation expiring exactly when checkout completes?

<details><summary>Answer</summary>

**Data model:**
```csharp
public class StockReservation
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string UserId { get; set; }
    public string CartId { get; set; }
    public int Quantity { get; set; }
    public DateTime ExpiresAt { get; set; }
    public ReservationStatus Status { get; set; } // Active, Fulfilled, Released
}
```

**Reserve flow:** on checkout start → insert reservation → decrement `AvailableStock` (separate from `QuantityInStock`) → if `AvailableStock < 0`, rollback and return 409.

**Release flow:** background job polls every 1 minute → finds `Status=Active AND ExpiresAt < UtcNow` → restores `AvailableStock` → marks `Status=Released`.

**Checkout completes at expiry moment:** use optimistic concurrency on the reservation. The checkout marks `Status=Fulfilled` before decrementing real stock. Background job skips `Status=Fulfilled` rows. If the background job releases first, checkout finds `AvailableStock=0`, detects no reservation → returns 409 (item gone).
</details>

---
