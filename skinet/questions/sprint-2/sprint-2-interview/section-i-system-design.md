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

<details><summary>Deep Dive</summary>

**The "payment before order" dilemma**

Creating the PaymentIntent before the order means you can charge the card even when stock runs out between payment confirmation and order creation. Two approaches:

**Approach A (simpler — used in most small apps):** Create PaymentIntent first, then create the order on webhook. If stock is gone on webhook: refund immediately.

```csharp
catch (InsufficientStockException ex)
{
    // Refund the charge
    await _stripeRefundService.CreateAsync(new RefundCreateOptions { PaymentIntent = paymentIntentId });
    _logger.LogWarning("Refunded {PaymentIntentId} due to insufficient stock", paymentIntentId);
    // Notify user: "We're sorry, item sold out — full refund issued"
    await _hubContext.Clients.Group(userEmail).SendAsync("OrderFailedNotification", "Sold out — refund issued");
}
```

**Approach B (complex — prevents charging when stock is gone):** Reserve stock before payment. Use the reservation system (see F7) — charge only if reservation succeeds.

**Webhook retry handling**

```csharp
[HttpPost("webhook")]
public async Task<IActionResult> Webhook()
{
    // Always return 200 unless signature verification fails
    try
    {
        var stripeEvent = ConstructEvent();
        await _paymentService.ProcessWebhookAsync(stripeEvent);
        return Ok();
    }
    catch (StripeException ex) // signature invalid
    {
        return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Webhook processing failed — Stripe will retry");
        return StatusCode(500); // Stripe retries on 5xx
    }
}
```

Return 200 for business logic failures (stock out, duplicate). Return 5xx only for infrastructure failures (DB down) — Stripe will retry, giving you a chance to recover.

**Dead-letter table for missed webhooks**

```csharp
public class FailedWebhookEvent
{
    public int Id { get; set; }
    public string PaymentIntentId { get; set; }
    public string RawPayload { get; set; }
    public int RetryCount { get; set; }
    public DateTime LastAttempt { get; set; }
    public string FailureReason { get; set; }
}
```

Background job retries failed events up to 5 times with exponential backoff. After 5 failures, alert on-call.

**Angular spinner with timeout fallback**

```typescript
ngOnInit() {
  this.hubConnection.on('OrderCompleteNotification', order => {
    this.loading.set(false);
    this.router.navigateByUrl('/checkout/success');
  });
  this.hubConnection.on('OrderFailedNotification', msg => {
    this.loading.set(false);
    this.snackbar.error(msg);
  });

  // Fallback: if SignalR is lost, poll after 10s
  setTimeout(() => {
    if (this.loading()) this.pollOrderStatus();
  }, 10_000);
}
```
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

<details><summary>Deep Dive</summary>

**Two race scenarios — which one hits depends on timing**

**Scenario 1: Sequential webhook processing (most common)**

Webhook A is processed and committed before B's webhook arrives. B reads updated stock = 0, fails the stock check immediately. Clean and simple.

**Scenario 2: True concurrent processing (rare)**

Both webhooks arrive within milliseconds:
```
A reads stock=1 → A decrements → A.SaveChangesAsync → succeeds (QuantityInStock was 1)
B reads stock=1 → B decrements → B.SaveChangesAsync → DbUpdateConcurrencyException
                                                        (QuantityInStock is now 0, not 1)
```

B's exception handler:

```csharp
catch (DbUpdateConcurrencyException ex)
{
    await ex.Entries.Single().ReloadAsync();
    var product = (Product)ex.Entries.Single().Entity;
    // Reload shows stock = 0 → cannot fulfill
    await _paymentService.RefundAsync(paymentIntentId);
    throw new InsufficientStockException(product.Id, requestedQty, product.QuantityInStock);
}
```

**Why both payments succeed**

Stripe charges the card before your server is involved in stock checking. This is the fundamental challenge of e-commerce at scale: payment and inventory are decoupled systems. The only way to prevent charging someone for an out-of-stock item is to reserve stock BEFORE payment (see F7 — reservation system).

**SignalR notification for the failure path**

```csharp
// After catching InsufficientStockException in the webhook handler
await _hubContext.Clients.Group(userEmail).SendAsync("OrderFailedNotification", new
{
    message = "The item sold out just before your order completed.",
    refundIssued = true,
    refundEta = "5-10 business days"
});
```

**Idempotency on the refund**

The refund call itself must be idempotent — if the webhook retries after the refund was issued, don't issue a second refund:

```csharp
// Check if refund already exists before creating one
var existingRefunds = await _refundService.ListAsync(new RefundListOptions { PaymentIntent = paymentIntentId });
if (!existingRefunds.Any())
    await _refundService.CreateAsync(new RefundCreateOptions { PaymentIntent = paymentIntentId });
```
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

<details><summary>Deep Dive</summary>

**Why scale the API first, not the DB**

Adding DB instances is expensive and complex (replication lag, connection routing). Scaling the API is cheap and immediate. Stateless APIs scale horizontally with zero coordination — just add instances.

Redis being fine is actually the key insight: sessions/cart work perfectly at scale. The DB is the bottleneck, not the application tier.

**The caching hierarchy**

```
Browser cache (static assets) → CDN (images, Angular build) → Redis (product catalog, 5min TTL) → Read Replica (less frequent reads) → Primary DB (writes only)
```

Each layer absorbs load before it reaches the next. Most traffic never hits the primary DB.

**Cache invalidation for product catalog**

```csharp
// When admin updates a product:
await _context.SaveChangesAsync();
await _cache.KeyDeleteAsync($"product:{productId}");  // invalidate
await _cache.KeyDeleteAsync("products:all");           // invalidate list cache
```

Use cache-aside pattern: read from cache, if miss fetch from DB and populate cache. Never write-through for data that changes infrequently — it's unnecessary complexity.

**Read replica routing with EF Core**

```csharp
// Use a different connection string for read-only queries
services.AddDbContext<StoreReadContext>(options =>
    options.UseSqlServer(config["ConnectionStrings:ReadReplica"]));

// Write operations: StoreContext (primary)
// Read operations: StoreReadContext (replica)
```

Or use an interceptor to route based on query type. Simpler for small teams: just use two registered DbContext types.

**Queue-based processing for webhooks**

```csharp
// Webhook endpoint — just enqueues, returns 200ms
[HttpPost("webhook")]
public async Task<IActionResult> Webhook()
{
    var json = await new StreamReader(Request.Body).ReadToEndAsync();
    VerifySignature(json, Request.Headers["Stripe-Signature"]);

    await _serviceBus.SendMessageAsync(new ServiceBusMessage(json));
    return Ok(); // Stripe happy, processing async
}

// Worker service — processes at its own pace
public class WebhookWorkerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _processor.ProcessMessagesAsync(stoppingToken))
        {
            await _paymentService.ProcessWebhookAsync(message.Body.ToString());
        }
    }
}
```

Benefit: Stripe gets its 200ms response, DB spikes don't cause webhook timeouts, messages queue up and drain at the DB's pace.

**When NOT to over-engineer**

A 50x traffic spike on launch day is often temporary. Don't immediately jump to CQRS + Kafka + microservices. Start with the simplest fix (index, scale out), measure the impact, move to the next layer only if needed. Premature optimization at the architecture level costs months of engineering time for problems you may never actually have.
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

<details><summary>Deep Dive</summary>

**Full transition service**

```csharp
public class OrderStatusService
{
    private readonly StoreContext _context;
    private readonly IHubContext<OrderHub> _hub;
    private readonly IEmailQueue _emailQueue;

    public async Task TransitionAsync(int orderId, OrderStatus newStatus, string changedBy)
    {
        var order = await _context.Orders.FindAsync(orderId)
            ?? throw new NotFoundException($"Order {orderId}");

        var fromStatus = order.Status;
        order.TransitionTo(newStatus); // throws if invalid

        var history = new OrderStatusHistory
        {
            OrderId = orderId,
            FromStatus = fromStatus,
            ToStatus = newStatus,
            Timestamp = DateTime.UtcNow,
            ChangedBy = changedBy
        };
        _context.OrderStatusHistory.Add(history);

        await _context.SaveChangesAsync();

        // Real-time notification
        await _hub.Clients.Group(order.BuyerEmail)
            .SendAsync("OrderStatusUpdated", new { orderId, status = newStatus.ToString() });

        // Email (async — don't block the response)
        _emailQueue.Enqueue(new OrderStatusEmail
        {
            To = order.BuyerEmail,
            OrderId = orderId,
            NewStatus = newStatus
        });
    }
}
```

**Why the state machine is on the entity, not the service**

Business rules (what transitions are legal) belong in the domain model. The service handles side effects (saving, notifying). If the rule is only in the service, it can be bypassed by setting `order.Status` directly. When the entity enforces the rule, it's impossible to bypass.

**Alternative: Stateless library**

```csharp
// Using Stateless NuGet package
var machine = new StateMachine<OrderStatus, Trigger>(order.Status);
machine.Configure(OrderStatus.Pending)
    .Permit(Trigger.PaymentReceived, OrderStatus.PaymentReceived);
machine.Configure(OrderStatus.PaymentReceived)
    .Permit(Trigger.StartProcessing, OrderStatus.Processing);
// ...
machine.Fire(Trigger.PaymentReceived); // throws InvalidOperationException if invalid
```

Stateless handles the transition graph, guards, entry/exit actions. Good for complex state machines with many transitions and guards. Overkill for 5 linear states.

**Querying the audit trail**

```csharp
// "When did this order ship?"
var shipEvent = await _context.OrderStatusHistory
    .Where(h => h.OrderId == orderId && h.ToStatus == OrderStatus.Shipped)
    .OrderByDescending(h => h.Timestamp)
    .FirstOrDefaultAsync();
```

**Idempotency on transitions**

If `TransitionAsync(Shipped)` is called twice (network retry), the second call should not throw — it should detect "already in this state" and return success without creating a duplicate history entry:

```csharp
if (order.Status == newStatus) return; // already transitioned — idempotent
```
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

<details><summary>Deep Dive</summary>

**Atomic SQL decrement — implementation**

```csharp
public async Task<bool> DecrementStockAsync(int productId, int quantity)
{
    var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
        @"UPDATE Products
          SET QuantityInStock = QuantityInStock - {0}
          WHERE Id = {1} AND QuantityInStock >= {0}",
        quantity, productId);

    return rowsAffected > 0; // false = insufficient stock
}
```

No read first. No EF change tracking. No `DbUpdateConcurrencyException`. The WHERE clause is the guard — the decrement only happens if stock is sufficient. DB executes this atomically under row-level locking.

**Retry with jitter — Polly**

```csharp
var retryPolicy = Policy
    .Handle<DbUpdateConcurrencyException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt =>
            TimeSpan.FromMilliseconds(50 * Math.Pow(2, attempt) + Random.Shared.Next(0, 100))
    );

await retryPolicy.ExecuteAsync(() => CreateOrderAsync(dto));
```

Exponential backoff + jitter. Works for low contention. Under a flash sale, 1000 requests × 3 retries = 3000 DB calls for what should be a few hundred.

**Redis reservation — the flow**

```
1. User clicks Add to Cart → DECR product:{id}:available in Redis
   If result < 0: INCR (rollback) → return "sold out"

2. Cart session: Redis has reservations mapped to cartId with 15-min TTL

3. Checkout: confirm reservation → decrement real DB stock atomically
   (reservation → committed stock)

4. Expiry: TTL fires → Lua script restores Redis available count
```

```lua
-- Atomic Redis reservation
local current = redis.call('GET', KEYS[1])
if tonumber(current) >= tonumber(ARGV[1]) then
    return redis.call('DECRBY', KEYS[1], ARGV[1])
else
    return -1  -- insufficient
end
```

**Queue-based — Azure Service Bus + virtual waiting room**

```csharp
// Checkout endpoint
public async Task<IActionResult> Checkout(CheckoutDto dto)
{
    var position = await _orderQueue.EnqueueAsync(dto);
    return Accepted(new { queuePosition = position, estimatedWait = "~30s" });
}

// Angular: poll /api/orders/status or wait for SignalR notification
```

Users see a "You're in queue: position 47" screen. Backend processes at sustainable rate. No DB explosions. Used by concert ticketing sites, PlayStation Store on game launch days.

**Decision matrix**

```
Concurrent users:   < 100      100-1000     > 1000 (flash sale)    > 10k (massive)
Solution:          Optimistic  Atomic SQL   Redis reservation      Queue + Redis
```
</details>

---
