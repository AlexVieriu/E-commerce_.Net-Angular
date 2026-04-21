## SECTION I — CHECKOUT FLOW SYSTEM DESIGN

> **Source:** `sprint-2-checkout-flow.md` — full Redis + SQL + Stripe + SignalR interaction.

---

### EASY

**I1.** [HIGH] Describe the checkout flow from "click Pay $341" to "success page."

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

```
1. Client: stripe.confirmPayment(clientSecret)
   → Stripe.js sends card + confirmation token to Stripe directly
   → Stripe charges card, returns paymentIntent.status = "succeeded"

2. Client: POST /api/orders { cartId }
   → OrderService reads cart from Redis → has PaymentIntentId pi_abc
   → Checks SQL for existing order by pi_abc (Task 18 app-level check)
   → Decrements stock (Task 13 optimistic concurrency)
   → Inserts Order (status=Pending)
   → Deletes cart from Redis

3. Client: navigate to /checkout/success
   → Success page subscribes to SignalR orderSignal
   → Currently renders spinner "Processing..."

4. [Async, seconds later] Stripe → POST /api/payments/webhook
   → HandlePaymentIntentSucceeded runs
   → Finds order by PaymentIntentId (unique index guarantees 1 row)
   → Updates status = PaymentReceived
   → Pushes SignalR event to buyer's connection

5. Client: SignalR fires → orderSignal.set(order)
   → Spinner resolves, order details display
```


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Describing it as "client → server → DB." Missing the multi-party choreography (Stripe, webhook, SignalR).


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Naming the **known limitation** upfront — step 1 and step 2 are in the wrong order (order created AFTER payment), and describing the correct order-first fix. Shows you know the current state is a deferred tradeoff.


</details>

---

### MEDIUM

**I2.** [HIGH] Three state stores — Redis, SQL, Stripe. Who owns what, and what happens if one fails?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

| Store | Owns | Failure mode |
|---|---|---|
| **Redis** | Active cart (items, prices, delivery method, PaymentIntentId, ClientSecret). TTL: 30 days. | Cart lost — user rebuilds. Not fatal. |
| **SQL Server** | Orders, OrderItems, Products (prices, stock). The source of truth for "what did the business commit to." | **Critical failure** — checkout blocked. |
| **Stripe** | PaymentIntents, charges, refunds. The source of truth for "did money move." | Payment blocked. Graceful fallback: defer the payment step, keep cart alive. |

**Interactions:**
- Redis holds ephemeral session state. If Redis drops, the user loses in-flight carts but completed orders are safe.
- SQL holds authoritative business state. If SQL drops, no orders can be created; Redis carts are untouched.
- Stripe holds money state. If Stripe is down, no payments process; SQL orders never transition to PaymentReceived.

**Failure mode matrix:**

| Redis down | SQL down | Stripe down | Result |
|---|---|---|---|
| ✓ | up | up | Carts fail → user loses session, can rebuild |
| up | ✓ | up | Orders fail → checkout blocked, carts safe |
| up | up | ✓ | Payments fail → user sees error, cart persists, orders never created |
| up | up | up | Happy path |


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"All three must be up." True but not a design — a principal engineer wants to see degraded-mode thinking.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **Redis as a cache, not a system of record** — cart data should be persistable to SQL (e.g. `AbandonedCarts` table) as a backup, so a Redis outage doesn't lose hours of user state. This project's current model makes carts ephemeral; a production system would add durability.


</details>

---

**I3.** [HIGH] [OUT-OF-SCOPE] Why is card data never sent to your API? What are the compliance implications?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**How it works:**
Stripe.js (client-side JavaScript) handles card input. When the user enters their card in the Stripe Payment Element, the data goes **directly from the browser to Stripe's servers** over TLS, bypassing your API entirely. Your client receives back a `paymentMethodId` or `confirmationToken` — a non-sensitive reference.

**Why this design:**
- **PCI-DSS scope reduction** — your servers never touch PAN (Primary Account Number), so you fall into **SAQ-A** (the simplest PCI compliance questionnaire). If card data flowed through your API, you'd need SAQ-D (full audit, expensive).
- **Smaller attack surface** — your logs, DB backups, memory dumps never contain card data. No "breached DB exposes millions of cards" headline.
- **Stripe owns tokenisation** — card → token mapping lives in Stripe's vault. You store tokens, not cards.

**What your API DOES see:**
- PaymentIntent IDs (`pi_abc123`) — references, not cards.
- Last-4 and brand (if you explicitly attach them to the Order model).
- Charge amounts, statuses, refund states.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Because Stripe is secure." Doesn't explain *how* the architecture reduces your compliance burden.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the `frame-src https://js.stripe.com https://hooks.stripe.com` CSP directive is *required* for this architecture — the Stripe Payment Element renders in an iframe hosted by Stripe. Missing that directive breaks card input silently (card fields disabled). Connects Task 9 (CSP) to Task 16 (Stripe) architecturally.


</details>

---

**I4.** [HIGH] Webhook vs client-side confirmation — what's the role of each?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Client-side `confirmPayment`:**
- **Purpose:** initiate the charge. Stripe.js sends card + intent to Stripe, Stripe processes it, returns a status.
- **What it proves:** the card was submitted and Stripe's response was `succeeded`.
- **What it does NOT prove:** the charge is final. Stripe may reverse it (fraud rules, 3DS failure later), or the browser may report `succeeded` but drop the response.

**Server-side webhook (`payment_intent.succeeded`):**
- **Purpose:** authoritative notification from Stripe to your backend. Signed (HMAC with webhook secret).
- **What it proves:** the charge is definitively recorded in Stripe and will settle.
- **Why you need it:** the client's `succeeded` response can be lost (network blip), spoofed (malicious client), or delayed. The webhook is the only authoritative signal.

**Practical rule:**
- Use client-side confirmation to drive UX (show "Processing..." spinner).
- Use the webhook to drive business state (mark Order as PaymentReceived, grant access, ship products).

**Never trust the client to confirm payment state to your business logic.**


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"The webhook is for backup." No — it's the source of truth. The client is for UX.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **webhook signature verification** is non-negotiable: `EventUtility.ConstructEvent(payload, signature, _whSecret)` — without it, anyone can POST a fake "succeeded" event to your webhook URL and trigger the state change. The signature ties the event to Stripe's private key.


</details>

---

### HARD

**I5.** [HIGH] Design the "order-first" checkout pattern. Show code diffs, where cart deletion moves, and how you handle the SignalR timeout.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**New flow:**
```
1. Client: POST /api/orders { cartId }
   Server: creates order with status=Pending
           does NOT delete cart (needed for retry on payment failure)
           returns { orderId, clientSecret }

2. Client: stripe.confirmPayment(clientSecret)
   Stripe charges card

3. Stripe → POST /api/payments/webhook (payment_intent.succeeded)
   Server: finds order by PaymentIntentId (unique index)
           status = PaymentReceived
           deletes cart from Redis
           pushes SignalR "order-updated" to buyer

4. Client (on success page):
   - subscribes to SignalR orderSignal
   - starts a 30s timeout timer
   - if SignalR arrives → render order
   - if timeout → fall back to polling GET /api/orders/{id} every 3s for 2 min
   - if still no PaymentReceived → show "Still processing, we'll email you" + contact info
```

**Code diffs — backend:**

```diff
// OrderService.cs
public async Task<Order?> CreateOrderAsync(string email, CreateOrderDto dto)
{
    // ... app-level idempotency check, price validation, stock decrement ...
    
    _unitOfWork.Repository<Order>().Add(order);
    if (!await _unitOfWork.Complete()) return null;
    
-   await _cartService.DeleteCartAsync(dto.CartId);
    return order;
}
```

```diff
// PaymentsController.cs — webhook handler
private async Task HandlePaymentIntentSucceeded(PaymentIntent intent)
{
    var order = await _unitOfWork.Repository<Order>()
        .GetEntityWithSpec(OrderSpecification.ByPaymentIntent(intent.Id));
    
    order.Status = OrderStatus.PaymentReceived;
    await _unitOfWork.Complete();

+   await _cartService.DeleteCartAsync(order.CartId);
+   await _hub.Clients.User(order.BuyerEmail)
+       .SendAsync("order-updated", order.Id);
}
```

**Code diffs — frontend:**

```diff
// checkout-success.component.ts
ngOnInit() {
    this.orderSignal$ = this.signalr.orderSignal;

+   timer(30_000).pipe(take(1)).subscribe(() => {
+       if (!this.orderSignal()) this.startPolling();
+   });
}

+ private startPolling() {
+   interval(3_000).pipe(
+       take(40),   // 2 min max
+       switchMap(() => this.orderService.getOrder(this.orderId)),
+       takeWhile(o => o.status === 'Pending', true)
+   ).subscribe(order => {
+       if (order.status === 'PaymentReceived') this.orderSignal.set(order);
+   });
+ }
```

**What changes in the existing codebase:**
- `CreateOrderAsync` no longer deletes the cart.
- Webhook handler now deletes cart + pushes SignalR.
- Success page has timeout + polling fallback.
- Need a background job to cancel orders older than 30 min with `status=Pending` + restore stock.

**Production consequence:** without the polling fallback, webhook delivery failures (network, Stripe outage) leave the user staring at a spinner forever. The fallback takes the system from "must-be-perfect" to "graceful-degradation."


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Moving cart deletion without handling retry or abandonment. It's a multi-step change with compensating actions.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that this is a **Saga pattern** — multi-step business process with compensating transactions (cancel on abandonment, restore stock on cancel). For more complex flows, adopt a saga framework (MassTransit); for this scale, ad-hoc is fine. Identifying the pattern shows architectural vocabulary.


</details>

---

**I6.** [MEDIUM] The SignalR connection drops mid-checkout. What happens to the order, and how do you design the recovery?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**What happens:**
1. User reaches success page, connection drops (mobile network, VPN hiccup).
2. Stripe webhook fires → server updates order to PaymentReceived.
3. Server attempts `hub.Clients.User(buyer).SendAsync("order-updated", ...)` → SignalR finds no active connection.
4. By default, **the notification is dropped silently**. SignalR doesn't queue messages for disconnected users.
5. User's browser reconnects → SignalR re-establishes connection — but the missed event is gone.
6. Success page stuck on spinner indefinitely.

**Recovery design:**

**Layer 1 — server-side tracking:**
- Record SignalR delivery state in SQL: `SentAt`, `AcknowledgedAt`.
- Webhook handler updates order AND inserts a `NotificationOutbox` row.
- Background worker reads outbox, retries delivery until acknowledged or timeout.

**Layer 2 — client-side reconnection sync:**
- On SignalR reconnect, client calls `GET /api/orders/{id}` for the most recent order to sync state.
- Server returns current order status; client updates signal.

**Layer 3 — client-side polling fallback (from I5):**
- If SignalR never arrives within 30s, start polling.
- Eventually reveals the PaymentReceived state even if SignalR is permanently broken.

**Layer 4 — email fallback:**
- After 5 min without resolution, send an order confirmation email regardless.
- User sees their order in their inbox even if the web UI is broken.

**Production consequence:** without these layers, users on spotty networks lose confidence ("Did my payment go through?") and call support. The fallback chain is pure customer service cost reduction.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Reconnect and retry." SignalR's auto-reconnect doesn't replay missed server-pushed messages. You need explicit state sync on reconnect.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **SignalR backplane (Redis or Azure SignalR Service)** is needed when you scale to multiple API replicas — without it, a user connected to replica A won't receive messages sent by replica B's webhook handler. Fundamental gotcha at scale.


</details>

---

**I7.** [HIGH] [OUT-OF-SCOPE] At 10,000 concurrent checkouts, where does this architecture break? List the top 3 bottlenecks and the fix for each.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Bottleneck 1 — EF optimistic concurrency conflict storm.**
On a hot product, 10k concurrent checkouts all read stock=5 and race to decrement. Optimistic concurrency throws for 9,995 of them. Retries compound the load.

*Fix:* pre-gate with Redis atomic `DECR stock:{productId}`. Only requests that pre-acquire stock proceed to SQL. SQL becomes serial per-SKU, not concurrent.

**Bottleneck 2 — SQL connection pool exhaustion.**
Default pool is ~100 connections. 10k concurrent requests queue up. Timeouts cascade.

*Fix:* 
- Increase pool (more RAM on DB).
- Asynchronous query patterns (`await` properly, don't block threads).
- Read-only queries route to read replicas.
- Cache hot reads (product details, delivery methods) in Redis with short TTL.

**Bottleneck 3 — Stripe rate limits.**
Stripe limits PaymentIntent creation to ~100/sec per account (default). 10k bursts trigger 429s.

*Fix:*
- Request rate-limit increase from Stripe (they'll raise for legitimate business).
- Token bucket / leaky bucket in your `PaymentService` to smooth out bursts.
- Queue-based processing: put checkout requests on a queue, workers drain at Stripe's rate.

**Bottleneck 4 (bonus) — SignalR backplane.**
Without a backplane, SignalR messages broadcast only to the replica's connections. 10k users are spread across 10 replicas; each replica sees 10% of notifications.

*Fix:* Azure SignalR Service or Redis backplane. Messages route across replicas.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Scale horizontally." True but vacuous — the interviewer wants the specific failure modes.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **circuit breakers** (Polly library) for downstream dependencies — when Stripe rate-limits, the circuit opens, fails fast, user sees clean error instead of 30s timeouts. Protects the whole system from cascading failure.


</details>

---

**I8.** [HIGH] The webhook fires BEFORE `POST /api/orders` completes. Trace the failure and walk through the fix with code.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Current broken flow — race:**

```
T=0    Client: stripe.confirmPayment() → Stripe charges
T=50   Stripe internal: charge succeeded, queues webhook
T=60   Client: starts POST /api/orders (network RTT)
T=80   Stripe → POST /api/payments/webhook
T=80   Server: findOne(PaymentIntentId = pi_abc) → 0 rows (order not inserted yet)
T=80   Server: returns 200, takes no action
T=100  Server: order inserted with status=Pending (too late)
T=end  Order stuck in Pending forever
```

**Why 200 without action:** the webhook handler returns 200 even when it can't find the order — otherwise Stripe retries for hours, spamming your server.

**Fix — order-first (from I5) + idempotent webhook:**

```csharp
public async Task<IActionResult> StripeWebhook()
{
    var signature = Request.Headers["Stripe-Signature"];
    var payload = await new StreamReader(Request.Body).ReadToEndAsync();
    var stripeEvent = EventUtility.ConstructEvent(payload, signature, _whSecret);

    if (stripeEvent.Type == "payment_intent.succeeded")
    {
        var intent = (PaymentIntent)stripeEvent.Data.Object;
        var order = await _unitOfWork.Repository<Order>()
            .GetEntityWithSpec(OrderSpecification.ByPaymentIntent(intent.Id));
        
        if (order == null)
        {
            // Order-first is NOT YET implemented → defensive: enqueue for retry
            _logger.LogWarning("Webhook fired before order exists: {IntentId}", intent.Id);
            await _webhookRetry.EnqueueAsync(stripeEvent);  // retry after 10s
            return Ok();
        }

        order.Status = OrderStatus.PaymentReceived;
        await _unitOfWork.Complete();
        await _cartService.DeleteCartAsync(order.CartId);
        await _hub.Clients.User(order.BuyerEmail).SendAsync("order-updated", order.Id);
    }

    return Ok();
}
```

**With order-first implemented:** the order always exists before payment is confirmed. The webhook always finds it. The `if (order == null)` branch should never fire — if it does, it's a genuine bug (order deleted?), and the retry queue catches it.

**Production consequence:** without the retry queue, even rare races become permanently-stuck orders. With it, most races self-heal within 10-30 seconds.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Ignoring the race and adding more retries on the client. That's treating the symptom, not the cause.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **Stripe's webhook retries** — Stripe itself retries failed webhooks with exponential backoff over 3 days. If your webhook returns a 5xx, Stripe retries. If it returns 200 (as above), Stripe considers it delivered. So your own retry queue is needed for 200-but-not-processed cases. Knowing Stripe's retry semantics is senior-level.


</details>

---

**I9.** [HIGH] [OUT-OF-SCOPE] Design a **transactional outbox** pattern so that SignalR notifications survive webhook handler crashes, server restarts, and transient SignalR-hub failures.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**The problem:**
The webhook handler updates the order AND pushes SignalR in the same method. If the SignalR push fails (hub down, no connection, network), the order is updated but the user never gets notified. If the handler crashes between the two, same outcome. SignalR is fire-and-forget; no retry.

**The outbox pattern — write notifications to SQL in the same transaction as the business change, and have a separate worker drain them:**

```csharp
// 1. Add an Outbox table
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;          // "OrderUpdated"
    public string Payload { get; set; } = null!;       // JSON
    public string Target { get; set; } = null!;        // buyer email
    public DateTime CreatedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public int Attempts { get; set; }
}

// 2. Webhook handler writes order + outbox row in ONE SaveChanges
public async Task HandlePaymentIntentSucceeded(PaymentIntent intent)
{
    var order = await _uow.Repository<Order>()
        .GetEntityWithSpec(OrderSpecification.ByPaymentIntent(intent.Id));
    
    order.Status = OrderStatus.PaymentReceived;
    
    _uow.Repository<OutboxMessage>().Add(new OutboxMessage
    {
        Id = Guid.NewGuid(),
        Type = "OrderUpdated",
        Payload = JsonSerializer.Serialize(new { order.Id, order.Status }),
        Target = order.BuyerEmail,
        CreatedAt = DateTime.UtcNow
    });

    await _uow.Complete();   // atomic: order + outbox in one tx
}

// 3. Background worker drains the outbox
public class OutboxDispatcher : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pending = await _db.Outbox
                .Where(o => o.DispatchedAt == null && o.Attempts < 5)
                .OrderBy(o => o.CreatedAt)
                .Take(50)
                .ToListAsync(ct);

            foreach (var msg in pending)
            {
                try
                {
                    await _hub.Clients.User(msg.Target)
                        .SendAsync(msg.Type, JsonDocument.Parse(msg.Payload), ct);
                    msg.DispatchedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    msg.Attempts++;
                    _logger.LogWarning(ex, "Outbox dispatch failed, attempt {N}", msg.Attempts);
                }
            }

            await _db.SaveChangesAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }
}
```

**Why this works:**
- **Atomicity:** the order update and the outbox row are in the same SQL transaction. You can't have one without the other.
- **Durability:** if the process crashes between SaveChanges and SignalR, the outbox row survives. On restart, the dispatcher drains it.
- **Retry:** failed dispatches increment attempts; bounded retries prevent runaway.
- **Decoupling:** the webhook is free to return 200 fast; dispatch is async.

**The catch — at-least-once delivery:**
The user may receive the notification multiple times if the dispatcher crashes after SignalR send but before `DispatchedAt` is set. The client must be idempotent — re-receiving "order updated" with the same order.Id should be a no-op. Use the order.Status as the source of truth; ignore redundant notifications.

**When NOT to use outbox:**
- If notifications are best-effort (analytics pings) — cost of SQL write exceeds the value.
- If you have a native messaging platform (Azure Service Bus) with durable subscriptions — use that instead of rolling your own.

**Production consequence:** without outbox, a 2% SignalR failure rate means 2% of paid orders silently never transition in the UI. Support tickets spike. With outbox, the dispatcher heals stragglers in seconds, and a monitoring alert on `attempts >= 5` catches genuinely stuck messages.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Put SignalR send in a try/catch with retries inside the webhook handler." Doesn't survive a crash. Doesn't survive the process dying. The outbox's value is specifically that the notification state is in SQL, not in memory.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **MassTransit** or **NServiceBus** have outbox built-in with automatic deduplication. For a production system, adopting a messaging library beats hand-rolling. Hand-rolled outbox is fine for a CV project or a simple case; acknowledge the mature alternative exists.


</details>

---

**I10.** [MEDIUM] [OUT-OF-SCOPE] Where does **3DS / SCA (PSD2)** fit into this checkout flow, and how does it change the client state machine?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**What is 3DS / SCA:**
- **3DS (3-D Secure)**: card-network protocol for authenticating the cardholder via a challenge (SMS code, banking app approval).
- **SCA (Strong Customer Authentication)**: the **EU regulation (PSD2)** that mandates 3DS on most card-not-present payments above ~€30 from EU customers. US/UK cards have similar pressure but are less uniformly enforced.

**Trigger:** Stripe applies 3DS based on card-issuer rules, amount, user location, and merchant history. The merchant doesn't choose — the issuer does.

**Where it fires in the flow:**

```
Client: stripe.confirmPayment(clientSecret)
  ↓
Stripe sees issuer requires 3DS
  ↓
Stripe returns { status: "requires_action", next_action: { ... } }
  ↓
Stripe.js auto-opens 3DS challenge iframe/modal
  ↓
User completes challenge (SMS code, banking app tap)
  ↓
Stripe confirms → status: "succeeded" OR "requires_payment_method" (failed)
```

**Critical client-side change — the state machine has a new state:**

```typescript
// Before 3DS (happy path only):
'idle' → 'confirming' → 'succeeded' | 'failed'

// With 3DS:
'idle' → 'confirming' → 'requires_action' → 'authenticating' → 'succeeded' | 'failed'
```

**What your code needs:**

1. **Call `stripe.confirmPayment` with `redirect: "if_required"`** — lets Stripe.js handle the challenge in-app without forcing a full page redirect.
2. **Handle the intermediate status:** Stripe.js resolves the promise with `paymentIntent.status = "requires_action"` if 3DS is pending. You must NOT treat this as failure — it's intermediate.
3. **Await the second confirm:** Stripe.js returns control after the challenge. Your promise resolves with final status.
4. **Timeout handling:** users abandon challenges. Default Stripe timeout is a few minutes; your UI should show "waiting for authentication" with a cancel option.

**Simplified code:**

```typescript
const { paymentIntent, error } = await this.stripe.confirmPayment({
    elements: this.elements,
    confirmParams: { return_url: window.location.origin + '/checkout/success' },
    redirect: 'if_required'
});

if (error) {
    // Card declined, 3DS failed, etc.
    this.state.set('failed');
    return;
}

if (paymentIntent.status === 'succeeded') {
    this.state.set('succeeded');
    return;
}

if (paymentIntent.status === 'processing') {
    // Async method — bank approval in progress. SignalR will update via webhook.
    this.state.set('pending_webhook');
    return;
}
```

**Server-side — no extra work:**
3DS happens entirely client-side between Stripe.js and the card issuer. Your webhook still fires on `payment_intent.succeeded` or `payment_intent.payment_failed`. No new endpoint, no new state in your DB beyond the existing status transitions.

**What breaks if you ignore 3DS:**
- EU customers hit a `requires_action` status they don't understand → UI treats it as "failure" → user retries → Stripe refuses (same idempotency key, different params — see D9/K6 pattern).
- Support tickets: "my payment worked in France but not on your site." The card works; your frontend rejected the intermediate state.

**Production consequence:** a merchant that doesn't handle 3DS gracefully loses ~20% of EU conversions. For a CV project this is out of scope; for a real shop, PSD2 support is day-one.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"3DS is a backend concern." False — the challenge is a client-side flow. Your backend sees only the final success/fail event via webhook.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **off-session vs on-session** payments — the rules differ. On-session (user present, filling out a form) commonly needs 3DS. Off-session (saved card, subscription renewal) uses `setup_future_usage` to pre-authenticate; the challenge happens once when the card is saved, not on every charge. Knowing the distinction signals real Stripe experience.


</details>

---

### ⚡ Syntax Quick-Fire

**IQ1.** [HIGH] Angular `toSignal(obs$, { initialValue: undefined })` — what does the signal return before `obs$` emits?

<details>
<summary>The 5% Answer</summary>

- Returns **`undefined`** (the `initialValue` you supplied) before the first emission.
- Without `initialValue`, the signal type is `T | undefined` and starts as `undefined`.
- With `{ requireSync: true }`: the observable **must emit synchronously** on subscription (e.g. `BehaviorSubject`) — otherwise `toSignal` throws `NG0601`. No `initialValue` is needed because the sync emission sets the value immediately.

```typescript
// BehaviorSubject — emits synchronously, requireSync is safe
orderSignal = toSignal(this.signalr.order$, { requireSync: true });

// HTTP — async, needs initialValue
products = toSignal(this.http.get<Product[]>('/api/products'), { initialValue: [] });
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "It returns null before the first emission." Returns `undefined`, not `null`, unless your type is explicitly nullable. Template `@if` checks matter.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** `manualCleanup: true` option — the signal doesn't auto-unsubscribe when the component destroys. Useful for signals created outside an injection context (e.g. in a service). Without it, `toSignal` auto-unsubscribes via `DestroyRef`.

</details>

---

**IQ2.** [HIGH] C# `IActionResult` vs `ActionResult<T>` — what does the generic give you?

<details>
<summary>The 5% Answer</summary>

- **`ActionResult<T>`**: the response body type is known to the framework — Swagger/OpenAPI generates correct response schemas, and client SDK generators produce strongly-typed clients.
- **`IActionResult`**: opaque to tooling — the body type is unknown, so generated clients use `dynamic` or `object`.
- Implicit conversion: with `ActionResult<T>` you can `return order;` directly instead of `return Ok(order);` — the framework wraps it automatically.

```csharp
// Typed — Swagger shows "200: Order schema"
public async Task<ActionResult<Order>> GetOrder(int id) {
    var order = await _service.GetOrderAsync(id);
    if (order == null) return NotFound();
    return order;  // implicit conversion to ActionResult<Order>
}
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "`IActionResult` is fine — it's more flexible." Flexibility comes at the cost of API discoverability and type safety for consumers.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Using `TypedResults` (minimal APIs / .NET 7+) — `Results.Ok<Order>(order)` — which provides even more precise type information without needing `ActionResult<T>`. Shows awareness of the evolving API surface.

</details>

---

**IQ3.** [HIGH] RxJS: `switchMap` vs `mergeMap` vs `concatMap` — one sentence each.

<details>
<summary>The 5% Answer</summary>

- **`switchMap`**: cancels the previous inner observable on each new source emission — use for autocomplete / search (latest query wins, old results discarded).
- **`mergeMap`**: subscribes to all inner observables concurrently — use for parallel independent requests where order doesn't matter.
- **`concatMap`**: queues inner observables, runs one at a time in emission order — use for sequential operations where order matters (save, then notify).

```typescript
// Autocomplete — only latest search matters
searchTerm$.pipe(
    switchMap(term => this.http.get(`/api/search?q=${term}`))
).subscribe(results => ...);

// Parallel file uploads — all start immediately
files$.pipe(
    mergeMap(file => this.upload(file))
).subscribe(result => ...);

// Sequential steps — step 2 waits for step 1
steps$.pipe(
    concatMap(step => this.process(step))
).subscribe();
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "`switchMap` for everything." Using `switchMap` for form submission or payment — if the user clicks twice, the first submission is cancelled, potentially leaving the server half-processed.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** `exhaustMap` — a fourth operator: ignores new source emissions while the inner observable is still running. Ideal for form submit buttons where you want to prevent double-submit without cancelling the in-flight request.

</details>

---
