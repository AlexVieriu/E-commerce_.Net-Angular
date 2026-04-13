## SECTION I — SYSTEM DESIGN SCENARIOS

### HARD

**I1.** You're designing the checkout flow for an e-commerce app. Walk through every step from "user clicks Place Order" to "user sees confirmation." Include: client validation, API call, stock check, payment processing, order creation, stock decrement, cart cleanup, real-time notification, and error handling at each step. What happens if the payment succeeds but the order insert fails?

<details><summary>Answer</summary>

**Full flow:**

1. **Client validation** — Angular validates form (address filled, card element complete via Stripe Elements). If invalid, show inline errors. No API call.

2. **Create/confirm PaymentIntent** — POST `/api/payments/create-intent` → server creates PaymentIntent with Stripe (uses SecretKey) → returns `clientSecret`. Angular calls `stripe.confirmPayment({ clientSecret })` → Stripe charges the card. If declined → show Stripe error, stop.

3. **Wait for webhook** — Angular connects to SignalR hub and waits for `OrderCompleteNotification`. Spinner shown.

4. **Stripe webhook fires** → POST `/api/payments/webhook`. Server verifies signature, extracts `paymentIntentId`.

5. **Stock check** — `OrderService` loads cart, checks each product's `QuantityInStock >= cartItem.Quantity`. If not → throw `InsufficientStockException` → return 409 (but payment already charged — see below).

6. **Order creation** — insert `Order` record with `PaymentIntentId`, buyer, items, delivery method.

7. **Stock decrement** — `product.QuantityInStock -= qty` within same `SaveChangesAsync` (covered by optimistic concurrency).

8. **Cart cleanup** — `DeleteCartAsync(cartId)` called after `SaveChangesAsync`. Best-effort; failure logged but not re-thrown.

9. **SignalR notification** — server pushes `OrderCompleteNotification` to user's hub group. Angular hides spinner, shows confirmation.

**If payment succeeds but order insert fails:**

The money is charged but no order exists. This is a critical failure. The webhook should be idempotent — Stripe will retry the webhook. On retry: attempt order creation again. If it keeps failing: log a critical alert, trigger a Stripe refund automatically, email the user. Store failed webhook events in a dead-letter table for manual review.
</details>

---

**I2.** Two users add the last item to their carts. Both click "Place Order" at the same time. Trace the exact execution path for both requests through: stock read, stock check, payment intent, `SaveChangesAsync` with `[ConcurrencyCheck]`, exception handling, and user notification. What does each user see?

<details><summary>Answer</summary>

```
t=0ms   User A & B both have stock=1 in their carts

t=10ms  A: POST /api/payments/create-intent → PI_A created (idempotency key: cartA)
t=10ms  B: POST /api/payments/create-intent → PI_B created (idempotency key: cartB)

t=50ms  A: stripe.confirmPayment(PI_A) → Stripe charges card A → "succeeded"
t=50ms  B: stripe.confirmPayment(PI_B) → Stripe charges card B → "succeeded"
        (Both payments succeed — Stripe doesn't know about stock)

t=100ms Webhook for PI_A fires → OrderService:
        - reads product stock = 1
        - stock check: 1 >= 1 ✓
        - decrement: stock → 0
        - SaveChangesAsync succeeds (WHERE QuantityInStock = 1 → 1 row updated)
        - Order A created, cart A deleted
        - SignalR → User A sees: "Order confirmed!"

t=101ms Webhook for PI_B fires → OrderService:
        - reads product stock = 0 (already decremented by A)
        - stock check: 0 >= 1 ✗ → InsufficientStockException
        (OR if both read stock=1 simultaneously: SaveChangesAsync → DbUpdateConcurrencyException)
        - Catch exception → issue Stripe refund for PI_B
        - SignalR → User B sees: "Sorry, item sold out — full refund issued within 5-10 days"
```

**User A:** order confirmed, charged successfully.  
**User B:** payment refunded automatically, clear message explaining why.
</details>

---

**I3.** Your app gets featured on Reddit and traffic spikes 50x. The database starts timing out. Your Redis cart store is fine. Walk through your scaling strategy: what do you scale first? What changes in architecture? When do you introduce caching, read replicas, CDN, and queue-based processing?

<details><summary>Answer</summary>

**Immediate (day 1):**
1. **Scale out the API horizontally** — run multiple instances behind a load balancer (Azure App Service scale-out, or Kubernetes replicas). This costs nothing in architecture changes — stateless API, Redis handles sessions.
2. **Identify the bottleneck** — check slow query log, Application Insights, or Azure Monitor. Is it a missing index? A hot query? Fix the cheapest thing first.
3. **Connection pooling** — ensure `DbContext` is registered as `Scoped` (not Singleton). Check connection pool size.

**Short-term (week 1):**
4. **Add read replicas** — route all GET queries (product listing, search) to the replica. Writes stay on primary. This halves the primary DB load for read-heavy workloads.
5. **Cache the product catalog** — product data changes rarely. Cache in Redis with a 5-minute TTL. 90% of traffic is "browse products" — hitting the DB for that is wasteful.

**Medium-term (month 1):**
6. **CDN for static assets** — Angular build, images, product photos on Azure CDN. Removes all static file serving from the origin.
7. **Move webhook processing to a queue** — Stripe webhooks → Azure Service Bus → worker service processes orders asynchronously. API responds to Stripe in 200ms, processing happens in background.

**Longer-term (if needed):**
8. **CQRS / read models** — separate read and write databases. Write model is normalized EF Core; read model is denormalized for fast queries.
</details>

---

**I4.** Design an order status tracking system. An order goes through: Pending -> PaymentReceived -> Processing -> Shipped -> Delivered. Each transition should: validate the transition is legal, record the timestamp, notify the buyer via SignalR, and send an email. Design the state machine, the notification system, and the audit trail. How do you prevent an order from going from "Pending" directly to "Delivered"?

<details><summary>Answer</summary>

**State machine — valid transitions:**
```csharp
private static readonly Dictionary<OrderStatus, OrderStatus[]> ValidTransitions = new()
{
    [OrderStatus.Pending]         = [OrderStatus.PaymentReceived],
    [OrderStatus.PaymentReceived] = [OrderStatus.Processing],
    [OrderStatus.Processing]      = [OrderStatus.Shipped],
    [OrderStatus.Shipped]         = [OrderStatus.Delivered],
    [OrderStatus.Delivered]       = [],
};
```

**Transition method on the Order entity:**
```csharp
public void TransitionTo(OrderStatus newStatus)
{
    if (!ValidTransitions[Status].Contains(newStatus))
        throw new InvalidOperationException(
            $"Cannot transition from {Status} to {newStatus}");
    Status = newStatus;
    StatusUpdatedAt = DateTime.UtcNow;
}
```

**Audit trail:**
```csharp
public class OrderStatusHistory
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public OrderStatus FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public DateTime Timestamp { get; set; }
    public string ChangedBy { get; set; } // user or system
}
```

**Notification:** after each `SaveChangesAsync`, publish `OrderStatusChangedEvent` → handled by two consumers: SignalR hub (real-time) + email queue (async).

**Prevention of invalid transitions:** the dictionary acts as a whitelist. `Pending → Delivered` is not in `ValidTransitions[Pending]` → `Contains` returns false → `InvalidOperationException` thrown before any save.
</details>

---

**I5.** Your e-commerce app grows to 1000 concurrent users. The optimistic concurrency approach means popular products get frequent `DbUpdateConcurrencyException` during flash sales. Design a better solution. Compare: retry with jitter, atomic SQL decrement, Redis-based stock reservation, and queue-based ordering.

<details><summary>Answer</summary>

| Approach | Throughput | Complexity | Oversell risk | Best for |
|---|---|---|---|---|
| Retry with jitter | Low-medium | Low | None | Low contention |
| Atomic SQL decrement | High | Low | None | Medium contention |
| Redis reservation | Very high | Medium | None (if done right) | Flash sales |
| Queue-based | Unlimited (sequential) | High | None | Extreme load |

**Retry with jitter:** on `DbUpdateConcurrencyException`, wait `Random(50-300ms)` and retry up to 3 times. Simple, no infrastructure change. Fails under sustained high load — retries pile up, DB overwhelmed.

**Atomic SQL decrement:** `UPDATE Products SET Stock = Stock - @qty WHERE Id = @id AND Stock >= @qty`. One round-trip, no ORM overhead, rows-affected tells you success/fail. Best default upgrade from optimistic concurrency.

**Redis reservation:** reserve stock in Redis (sub-millisecond), process orders asynchronously. Near-zero DB contention at checkout. Requires two-phase protocol (reserve in Redis + confirm in DB). Complex to keep in sync.

**Queue-based:** all order requests go to a message queue. Single worker processes them sequentially — zero concurrency conflicts. Handles any throughput. Users wait in a "virtual queue." High infrastructure complexity.

**Recommendation for this project:** implement atomic SQL decrement now (one-line change, huge improvement). Add Redis reservation when flash sales are planned.
</details>

---
