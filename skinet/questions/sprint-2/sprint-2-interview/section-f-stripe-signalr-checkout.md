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

<details><summary>Deep Dive</summary>

**Why Stripe.js handles card details directly**

Your server never sees raw card numbers. The browser uses Stripe.js to tokenise card data and send it over a direct TLS connection to Stripe's servers. This keeps you out of PCI DSS scope (or lowers it to SAQ A, the lightest tier).

```typescript
// Angular — confirm payment
const result = await this.stripe.confirmPayment({
  elements: this.elements,
  confirmParams: { return_url: 'https://yoursite.com/checkout/success' },
  redirect: 'if_required'
});
if (result.error) { /* show error */ }
// else: payment confirmed, wait for webhook
```

**Webhook verification — why it matters**

Anyone can POST to your webhook endpoint. Without signature verification, a bad actor could fake a "payment_intent.succeeded" event and get a free order. `ConstructEvent` computes the HMAC-SHA256 of the raw request body using your `WebhookSecret` and compares it to the `Stripe-Signature` header.

```csharp
[HttpPost("webhook")]
public async Task<IActionResult> Webhook()
{
    var json = await new StreamReader(Request.Body).ReadToEndAsync();
    var stripeEvent = EventUtility.ConstructEvent(
        json,
        Request.Headers["Stripe-Signature"],
        _config["StripeSettings:WhSecret"]
    ); // throws StripeException if signature invalid

    if (stripeEvent.Type == "payment_intent.succeeded")
    {
        var intent = (PaymentIntent)stripeEvent.Data.Object;
        await _paymentService.UpdateOrderPaymentSucceeded(intent.Id);
    }
    return Ok();
}
```

**SignalR group management**

The client joins a group keyed on their email when the hub connects. The webhook handler pushes to that group:

```csharp
// Client connects:
await _hubContext.Groups.AddToGroupAsync(connectionId, userEmail);

// Webhook fires:
await _hubContext.Clients.Group(userEmail)
    .SendAsync("OrderCompleteNotification", orderDto);
```

**Timing problem: webhook arrives before Angular connects to hub**

If the webhook fires in milliseconds (Stripe test mode) but Angular's SignalR handshake takes longer, the notification is missed. Solution: Angular polls the order status on connect, or the server buffers the last notification per user for 30 seconds.
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

<details><summary>Deep Dive</summary>

**Why Stripe sends duplicate webhooks**

Stripe delivers webhooks at least once and retries on non-2xx responses (network blip, your server restart, timeout). You WILL receive duplicates — design for it.

**The race condition**

```
Webhook 1 arrives → reads: order exists? No
Webhook 2 arrives → reads: order exists? No  (before 1 has committed)
Webhook 1 inserts order → succeeds
Webhook 2 inserts order → DB unique constraint violation → exception
```

Handle the constraint violation gracefully:

```csharp
try
{
    _context.Orders.Add(order);
    await _context.SaveChangesAsync();
}
catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true)
{
    // Already created by concurrent webhook — this is OK
    _logger.LogInformation("Duplicate webhook for PaymentIntentId {Id} — ignoring", paymentIntentId);
}
```

**Always return 200 to Stripe**

Even if you detect a duplicate, return HTTP 200 to Stripe. If you return 4xx/5xx, Stripe retries — causing more duplicates. The correct response to "this is a duplicate" is `200 OK` (success — we handled it, nothing to do).

```csharp
// Webhook endpoint
return Ok(); // always 200, even for duplicates
```

**Idempotency key on the EF entity**

```csharp
public class Order
{
    public int Id { get; set; }
    public string PaymentIntentId { get; set; } = string.Empty; // unique index
    public OrderStatus Status { get; set; }
    // ...
}

// In OnModelCreating:
modelBuilder.Entity<Order>()
    .HasIndex(o => o.PaymentIntentId)
    .IsUnique();
```
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

<details><summary>Deep Dive</summary>

**The distributed transaction problem**

SQL and Redis don't participate in the same two-phase commit. Any "transaction" spanning both is logically impossible. You have to choose which system is authoritative (SQL — it holds the orders) and treat the other (Redis — carts) as a cache that can be cleaned up.

**What "best-effort cleanup" looks like**

```csharp
public async Task<Order> CreateOrderAsync(string buyerEmail, string cartId, ...)
{
    // ... build and save order ...
    await _context.SaveChangesAsync(); // authoritative write — must succeed

    // Best-effort cleanup — failure is acceptable
    try
    {
        await _cartService.DeleteCartAsync(cartId);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Cart {CartId} deletion failed after order commit — will expire via TTL", cartId);
        // Don't rethrow — order was created successfully
    }

    return order;
}
```

**TTL as the safety net**

Configure Redis keys with an expiry (e.g. 30 days):

```csharp
await _db.StringSetAsync(key, json, TimeSpan.FromDays(30));
```

Orphaned carts self-destruct. No manual cleanup job needed for the happy path.

**What if cart deletion consistently fails?**

If `DeleteCartAsync` throws repeatedly, users see stale carts. Add monitoring:

```csharp
_metrics.Increment("cart.deletion.failure");
```

Alert when this counter spikes — it indicates a Redis connectivity issue, not a logic error.

**Saga pattern — for when consistency really matters**

If cart deletion must be guaranteed, use the outbox pattern: write a "delete cart" message to a SQL outbox table inside the same transaction as the order. A background worker reads the outbox and deletes from Redis. Atomicity guaranteed at the SQL level. This is over-engineering for most projects but worth knowing for the interview.
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

<details><summary>Deep Dive</summary>

**How Stripe implements idempotency**

Stripe stores `(ApiKey, IdempotencyKey, endpoint)` → response for 24 hours. Any duplicate within that window returns the cached response without re-processing.

```csharp
public async Task<PaymentIntent> CreateOrUpdatePaymentIntent(string cartId)
{
    var cart = await _cartService.GetCartAsync(cartId);
    // ...

    var options = new PaymentIntentCreateOptions
    {
        Amount = (long)cart.Total * 100, // pence/cents
        Currency = "gbp",
        PaymentMethodTypes = ["card"],
    };

    var requestOptions = new RequestOptions
    {
        IdempotencyKey = cart.Id // cart.Id is stable for the lifetime of this cart
    };

    if (cart.PaymentIntentId == null)
        return await _paymentIntentService.CreateAsync(options, requestOptions);
    else
    {
        var updateOptions = new PaymentIntentUpdateOptions { Amount = options.Amount };
        return await _paymentIntentService.UpdateAsync(cart.PaymentIntentId, updateOptions, requestOptions);
    }
}
```

**Why `cart.Id` not `Guid.NewGuid()`**

A new GUID per request defeats the purpose — every retry creates a new intent. `cart.Id` is stable: same cart, same key, same intent.

**Edge case: cart amount changes before payment**

If the user changes their cart (adds/removes items), the cart total changes. You update the existing PaymentIntent with the new amount — which also updates what Stripe charges. The `IdempotencyKey` stays the same (still `cart.Id`) but you're calling `UpdateAsync`, not `CreateAsync`, so there's no idempotency conflict.

**What happens if two DIFFERENT users have the same cart.Id?**

This shouldn't happen — cart IDs are GUIDs or scoped to users. But if it did, Stripe would return User A's PaymentIntent to User B. Defense: prefix the key with the user identifier: `$"{userId}:{cartId}"`.
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

<details><summary>Deep Dive</summary>

**The timing race — the most common cause**

```
t=0ms: User clicks Pay
t=50ms: Stripe.js confirms payment
t=60ms: Stripe sends webhook (test mode is fast)
t=80ms: Webhook processed → SignalR pushes "OrderCompleteNotification"
t=200ms: Angular finally connects to SignalR hub and registers listener
```

The notification was sent at t=80ms, but the listener didn't exist until t=200ms. Fix:

```typescript
// WRONG — start hub before payment, register listener after
ngOnInit() {
  this.startSignalRConnection(); // starts hub, but no listener yet
  this.confirmPayment();         // payment fires, notification arrives, listener not ready
  this.hub.on('OrderCompleteNotification', ...); // too late
}

// CORRECT — register listener before payment
ngOnInit() {
  this.hub.on('OrderCompleteNotification', handler); // listener ready
  this.startSignalRConnection().then(() => {
    this.joinGroup();            // join group
    this.confirmPayment();       // NOW confirm payment — listener is ready
  });
}
```

**Hub group membership**

```csharp
// OrderHub.cs
public override async Task OnConnectedAsync()
{
    var email = Context.User?.FindFirstValue(ClaimTypes.Email);
    if (email != null)
        await Groups.AddToGroupAsync(Context.ConnectionId, email);
    await base.OnConnectedAsync();
}
```

```typescript
// Angular — connection must be OPEN before webhook fires
await this.hubConnection.start();
// Only now is the client in the group
```

**Stripe CLI for local webhook testing**

```bash
stripe listen --forward-to https://localhost:5001/api/payments/webhook
# Stripe CLI creates a tunnel and prints a temporary WebhookSecret
# Use that secret in your dev appsettings.json
```

Without this, Stripe can't reach `localhost` — webhooks silently fail.

**What to add to the spinner component**

```typescript
// Show a timeout fallback after 30 seconds
setTimeout(() => {
  if (this.loading()) {
    this.loading.set(false);
    this.error.set('Payment confirmed but order setup is taking longer than expected. Check your email or orders page.');
  }
}, 30_000);
```
</details>

---

**F6.** `PaymentService` calls `new PaymentIntentService()` and `new RefundService()` directly — no DI seam. You want to unit test it. What are the three options, and which is right for this project?

<details><summary>Answer</summary>

1. **Wrapper interface** — create `IStripeService` wrapping `PaymentIntentService` and `RefundService`. Inject the interface. Mock it in tests. **Best option** — clean, testable, follows DI principles.

2. **Protected virtual factory method** — extract `new PaymentIntentService()` into a `protected virtual` method. Subclass in tests to inject a fake. Works without changing the public API. Acceptable if you truly can't change the constructor.

3. **Integration test against Stripe test API** — use real Stripe with `sk_test_...` keys. No mocks. Honest but slow, requires network, can be flaky, and leaks test keys unless handled carefully via environment variables.

**Right for this project:** Option 1. We control the code, DI is already set up, and the Stripe SDK classes aren't sealed — wrapping them is trivial. Write the wrapper, register it in DI, mock the interface in unit tests.
</details>

<details><summary>Deep Dive</summary>

**Option 1: Wrapper interface — full implementation**

```csharp
// The abstraction
public interface IStripeService
{
    Task<PaymentIntent> CreatePaymentIntentAsync(PaymentIntentCreateOptions options);
    Task<Refund> CreateRefundAsync(RefundCreateOptions options);
}

// The real implementation (registered in DI)
public class StripeService : IStripeService
{
    private readonly PaymentIntentService _intents = new();
    private readonly RefundService _refunds = new();

    public Task<PaymentIntent> CreatePaymentIntentAsync(PaymentIntentCreateOptions options)
        => _intents.CreateAsync(options);

    public Task<Refund> CreateRefundAsync(RefundCreateOptions options)
        => _refunds.CreateAsync(options);
}

// PaymentService — now testable
public class PaymentService
{
    private readonly IStripeService _stripe;
    public PaymentService(IStripeService stripe) { _stripe = stripe; }
}

// Test
var mockStripe = new Mock<IStripeService>();
mockStripe.Setup(s => s.CreatePaymentIntentAsync(It.IsAny<PaymentIntentCreateOptions>()))
    .ReturnsAsync(new PaymentIntent { Id = "pi_test_123", Status = "requires_payment_method" });
var service = new PaymentService(mockStripe.Object);
```

**Option 2: Virtual factory — when you can't change the constructor**

```csharp
public class PaymentService
{
    protected virtual PaymentIntentService CreateIntentService() => new();

    public async Task<PaymentIntent> CreateAsync(long amount)
    {
        var service = CreateIntentService(); // testable seam
        return await service.CreateAsync(new PaymentIntentCreateOptions { Amount = amount });
    }
}

// Test subclass — override the factory
public class TestablePaymentService : PaymentService
{
    private readonly PaymentIntentService _fake;
    public TestablePaymentService(PaymentIntentService fake) { _fake = fake; }
    protected override PaymentIntentService CreateIntentService() => _fake;
}
```

Problem: `PaymentIntentService` itself isn't an interface — you need a real instance or a subclass. If Stripe SDK classes are `sealed`, this approach fails.

**Option 3: When integration tests are the honest answer**

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task CreatePaymentIntent_WithValidAmount_ReturnsRequiresPaymentMethod()
{
    StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_TEST_KEY")
        ?? throw new InvalidOperationException("STRIPE_TEST_KEY not set");

    var service = new PaymentService(new StripeService());
    var intent = await service.CreatePaymentIntentAsync(new() { Amount = 1000, Currency = "gbp" });

    Assert.Equal("requires_payment_method", intent.Status);
}
```

Tag integration tests with `[Trait("Category", "Integration")]` so you can exclude them from the fast unit test run: `dotnet test --filter "Category!=Integration"`.
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

<details><summary>Deep Dive</summary>

**Two stock fields: why**

```csharp
public class Product
{
    public int QuantityInStock { get; set; }    // actual physical stock
    public int AvailableStock { get; set; }     // stock - active reservations
}
```

Users see `AvailableStock`. The checkout checks `AvailableStock`. `QuantityInStock` only decrements when the order actually commits. This prevents overselling without requiring immediate `QuantityInStock` changes (which would need concurrency handling at reservation time too).

**Reserve — atomic decrement**

```csharp
public async Task<StockReservation?> ReserveAsync(string cartId, string userId, int productId, int qty)
{
    // Atomic: read and decrement in one operation
    await using var tx = await _context.Database.BeginTransactionAsync();
    var product = await _context.Products
        .Where(p => p.Id == productId)
        .FirstOrDefaultAsync();

    if (product == null || product.AvailableStock < qty)
    {
        await tx.RollbackAsync();
        return null; // → 409 Conflict
    }

    product.AvailableStock -= qty;

    var reservation = new StockReservation
    {
        ProductId = productId, UserId = userId, CartId = cartId,
        Quantity = qty,
        ExpiresAt = DateTime.UtcNow.AddMinutes(15),
        Status = ReservationStatus.Active
    };
    _context.StockReservations.Add(reservation);

    await _context.SaveChangesAsync();
    await tx.CommitAsync();
    return reservation;
}
```

**Background job (IHostedService)**

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await ReleaseExpiredReservationsAsync();
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
    }
}

private async Task ReleaseExpiredReservationsAsync()
{
    var expired = await _context.StockReservations
        .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAt < DateTime.UtcNow)
        .ToListAsync();

    foreach (var r in expired)
    {
        r.Status = ReservationStatus.Released;
        var product = await _context.Products.FindAsync(r.ProductId);
        if (product != null) product.AvailableStock += r.Quantity;
    }
    await _context.SaveChangesAsync();
}
```

**The race: expiry exactly when checkout completes**

```
t=14:59: Checkout begins → finds reservation Active
t=15:00: Background job wakes → marks reservation Released → restores AvailableStock
t=15:01: Checkout tries to commit → reservation is Released → checkout detects lost reservation → returns 409
```

To prevent: checkout sets `Status = Fulfilled` in the SAME transaction as the order commit. Background job query: `Status = Active` only — never touches `Fulfilled`. The race becomes:

```
t=14:59: Checkout transaction begins: sets Status=Fulfilled + decrements QuantityInStock
t=15:00: Background job: finds no Active expired rows (it's now Fulfilled) → no action
t=15:00: Checkout commits → success
```

Use optimistic concurrency (`[ConcurrencyCheck]` on `Status`) to ensure the transition `Active → Fulfilled` only succeeds if the background job hasn't already set it to `Released`.
</details>

---
