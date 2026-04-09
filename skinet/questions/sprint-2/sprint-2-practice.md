# Sprint 2 — Practice Exam

> CORS/CSP, Angular signals, optimistic concurrency, unit test quality
> 20% easy | 40% medium | 40% hard
> Covers tasks 9, BUG-1, 13, 14, 15, 16, 17, 18, 19

---

## Manual Testing Checklist

- [ ] Open DevTools → Network → any API response → verify `Content-Security-Policy`, `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy` headers
- [ ] Temporarily remove `.UnsafeInline()` from `AddStyleSrc()` → run app → watch Material break → understand WHY → revert
- [ ] Temporarily remove `AllowCredentials()` from CORS → try to log in → watch it fail → understand WHY → revert
- [ ] In Postman, send a request with `Origin: http://evil.com` → verify no `Access-Control-Allow-Origin` in response
- [ ] Open two browser tabs on checkout. Place a breakpoint on `OrderService.CreateOrderAsync` line 29. Let both hit the breakpoint. Release one, then the other. Observe the concurrency exception on the second.
- [ ] Open DevTools → Console → type `document.cookie` → verify it returns empty string (HttpOnly blocks it)
- [ ] Create a signal in Angular with an object value. Mutate it with `update(s => { s.x = 1; return s; })`. Verify the template does NOT update. Fix with spread. Verify it updates.
- [ ] Check the EF Core migration snapshot: open `SqlServerStoreContextModelSnapshot.cs`, search for `IsConcurrencyToken` on `QuantityInStock`
- [ ] Run `dotnet test` — all green before moving on
- [ ] Change the `StringSetAsync` setup in `CartService_Tests.cs` to use `TimeSpan?/When` (old overload). Run the test. It still passes — understand why (Moq silently ignores unmatched setups, returns default). Add `Verify(Times.Once)`. Run again. Does it fail?
- [ ] Open `PaymentService.cs` and `CouponService.cs` — verify `[ExcludeFromCodeCoverage]` is present with a comment explaining why

---

## Knowledge Beyond Scope

**Nonce-based CSP** — You used `unsafe-inline` as a tradeoff. Know the better alternative:
- Server generates a random nonce per request, injects it into `<script nonce="abc123">` and `Content-Security-Policy: script-src 'nonce-abc123'`
- Only scripts with the matching nonce execute — injected scripts without the nonce are blocked
- Angular doesn't support this out of the box; requires a custom server-side renderer or middleware
- Your answer: "We used `unsafe-inline` because Angular Material injects styles at runtime without nonce support. In a high-security app, we'd implement nonce-based CSP via server-side middleware."

**Pessimistic concurrency (SELECT FOR UPDATE)** — You implemented optimistic. Know the alternative:
- `SELECT ... FOR UPDATE` locks the row when reading. No other transaction can read or write it until you commit.
- Pros: guaranteed no conflicts. Cons: locks block all concurrent readers, causing contention and potential deadlocks.
- Your answer: "We chose optimistic because stock conflicts are rare in e-commerce — most products aren't being bought simultaneously. Pessimistic would block all readers of that product row, hurting read performance for everyone."

**EF Core change tracking modes** — Your test relies on the default tracking behavior. Know:
- `AsNoTracking()`: doesn't store original values, can't detect changes. Used for read-only queries (better performance).
- `AsTracking()` (default): stores original values, enables `[ConcurrencyCheck]` comparison.
- If `OrderService` used `AsNoTracking()` for the product read, the concurrency check would NOT work — EF wouldn't have the original value to compare.

**Zone.js deprecation** — Your project uses zoneless Angular. Know:
- Zone.js monkey-patches all async APIs (setTimeout, Promise, XHR). Expensive, causes phantom change detection cycles.
- Zoneless Angular (v18+) relies on signals and explicit `markForCheck()`. More performant, but requires immutable updates.
- Interviewers will ask: "Why go zoneless?" Answer: "Better performance — no unnecessary change detection cycles. Signals give us fine-grained reactivity."

---

## EASY (recall) — 20%

### E1. Question
What's the difference between CORS and CSP?

<details><summary>Answer</summary>
CORS controls which origins can make requests TO your server. CSP controls which sources your browser can load resources FROM. CORS is about outgoing requests; CSP is about incoming content.
</details>

### E2. Question
What does `[ConcurrencyCheck]` do in EF Core?

<details><summary>Answer</summary>
Adds the original property value to the UPDATE's WHERE clause. If 0 rows are affected (value changed since read), EF throws `DbUpdateConcurrencyException`.
</details>

### E3. Question
Why does mutating a signal object not trigger re-render in Angular?

<details><summary>Answer</summary>
Signals use reference equality (`===`). Mutating returns the same object reference — Angular sees `old === new` and skips re-render. Fix: return a new object with spread.
</details>

### E4. Question
What does `X-Frame-Options: DENY` protect against?

<details><summary>Answer</summary>
Clickjacking. Prevents your page from being embedded in an iframe on another site.
</details>

### E5. Question
What are the three value states in `DbUpdateConcurrencyException`?

<details><summary>Answer</summary>
`CurrentValues` (what you tried to write), `OriginalValues` (what you read), `GetDatabaseValuesAsync()` (what's actually in DB now).
</details>

### E6. Practical
Write from memory the SQL that EF Core generates WITH `[ConcurrencyCheck]` on `QuantityInStock` when updating stock from 5 to 4.

<details><summary>Answer</summary>
`UPDATE Products SET QuantityInStock = 4 WHERE Id = @p0 AND QuantityInStock = 5`
</details>

### E7. Question
You have a Moq `Setup(...).ReturnsAsync(false)` but no `Verify`. You delete the production method. The test still passes. Why?

<details><summary>Answer</summary>
Moq silently ignores setups that don't match the actual call. When the setup doesn't match (wrong overload, wrong parameters), the mock returns the type's default value — `false` for `bool`, `null` for reference types. The test never exercises your production code. Without `Verify`, there's no assertion that the method was called at all.
</details>

### E8. Question
What three properties make a test valuable (as opposed to "noise")?

<details><summary>Answer</summary>
1. **It can fail** — delete the production code and the test breaks.
2. **It fails for the right reason** — the assertion is the behavior you care about, not a side effect.
3. **It tests behavior, not implementation** — cares about WHAT the code does, not HOW.
</details>

### E9. Question
When should you use `[ExcludeFromCodeCoverage]`?

<details><summary>Answer</summary>
When the class has no injectable seam for its external dependencies — for example, Stripe services that call `new PaymentIntentService()` directly. Testing would require real HTTP calls. Always document WHY and what the architectural fix would be. Never use it to hide laziness.
</details>

---

## MEDIUM (scenario + practical) — 40%

### M1. Scenario
You deploy your Angular app and all Material components lose their styling. The console shows CSP violations. What happened and how do you fix it?

<details><summary>Answer</summary>
CSP is blocking inline styles. Angular Material injects `<style>` blocks directly into the DOM. Fix: add `unsafe-inline` to `style-src` directive. The ideal fix is nonce-based CSP, but Material doesn't support it without a custom renderer.
</details>

### M2. Scenario
Two users checkout simultaneously. User A's order succeeds. User B sees "Stock changed during checkout. Please try again." User B refreshes and retries — now they see "Insufficient stock." Trace the full chain of events.

<details><summary>Answer</summary>
1. Both read `QuantityInStock = 1`
2. A saves: `WHERE stock=1` → 1 row → stock becomes 0 ✓
3. B saves: `WHERE stock=1` → 0 rows → `DbUpdateConcurrencyException` → caught → `InvalidOperationException` → ExceptionMiddleware → 400 → snackbar
4. B retries: fresh read → `QuantityInStock = 0` → `0 < 1` → "Insufficient stock" thrown → 400 → snackbar
</details>

### M3. Practical
Open `OrderService.cs`. Comment out the `try/catch` around `unitOfWork.Complete()`. Run the concurrency test. What exception does the test get now? What HTTP status would the client see?

<details><summary>Answer</summary>
The test gets `DbUpdateConcurrencyException` (not `InvalidOperationException`). The client would see 500 because `ExceptionMiddleware` only maps `InvalidOperationException` to 400 — all other exceptions become 500.
</details>

### M4. Scenario
Your Angular app works on `localhost:4200` but after deploying to Azure, API calls fail silently. No errors in the .NET logs. What's wrong?

<details><summary>Answer</summary>
CORS. The Azure URL isn't in the origin whitelist. The browser blocks the request before it reaches the server — that's why there's no .NET log. Fix: add the Azure URL to `WithOrigins(...)` in the CORS policy.
</details>

### M5. Practical
Create a new Angular component with a signal:
```typescript
const user = signal({ name: 'Alex', score: 0 });
```
Write three different ways to increment `score` that correctly trigger re-render. Write one way that DOESN'T trigger re-render.

<details><summary>Answer</summary>
Works:
1. `user.update(u => ({ ...u, score: u.score + 1 }))`
2. `user.set({ ...user(), score: user().score + 1 })`
3. `user.update(u => Object.assign({}, u, { score: u.score + 1 }))`

Broken: `user.update(u => { u.score++; return u; })` — mutates, same reference.
</details>

### M6. Question
Why does `AllowCredentials()` in CORS matter for cookie auth? What happens if you remove it?

<details><summary>Answer</summary>
Without it, the browser strips cookies from cross-origin requests. The API receives the request but without the Identity cookie — it thinks the user is anonymous. Login appears to work but every subsequent request is unauthorized.
</details>

### M7. Scenario
You have `QuantityInStock = 10`. A background job tries to decrement by 1, but gets `DbUpdateConcurrencyException`. Should it retry automatically or fail?

<details><summary>Answer</summary>
Retry. Background jobs have no user waiting — silently re-read the entity, check stock again, and retry the decrement. Use a retry loop with a max attempt count. This is the "store wins" resolution strategy: reload the DB value, reapply your change.
</details>

### M8. Practical
Open `ExceptionMiddleware.cs`. Trace the path for:
1. `InvalidOperationException("Insufficient stock")` — what status code?
2. `DbUpdateConcurrencyException` (if uncaught) — what status code?
3. `NullReferenceException` — what status code?

<details><summary>Answer</summary>
1. 400 — `InvalidOperationException` is mapped to `BadRequest`
2. 500 — not an `InvalidOperationException`, falls to the else branch
3. 500 — same, generic server error
</details>

### M9. Question
What's the difference between `[ConcurrencyCheck]` and `[Timestamp]`?

<details><summary>Answer</summary>
`[ConcurrencyCheck]` — marks specific columns, adds their original value to WHERE. Works on any DB.
`[Timestamp]` — SQL Server `rowversion`, auto-incremented on every write. Catches ALL column changes, not just the annotated one. SQL Server only.
</details>

### M10. Practical
Draw from memory the full error chain when a concurrency conflict happens during checkout:
```
[EF Core] → [OrderService] → [ExceptionMiddleware] → [HTTP Response] → [error.interceptor] → [user sees ???]
```

### M11. Scenario
A signal holds a nested object: `signal({ user: { name: '', address: { city: '' } } })`. You spread the outer object but mutate `address.city` directly. Does the template update?

<details><summary>Answer</summary>
The template updates for checks on the outer object (new reference). But `address` is still the same reference — components that read `address.city` directly may not re-render because the inner reference didn't change. Fix: deep spread all nested objects: `{ ...s, user: { ...s.user, address: { ...s.user.address, city: 'London' } } }`.
</details>

### M12. Practical
Write the `Permissions-Policy` header value that disables accelerometer, camera, and microphone. What does each of these APIs do and why disable them?

<details><summary>Answer</summary>
`accelerometer=(), camera=(), microphone=()`. 
- Accelerometer: reads device tilt/motion sensor. Disabled because a shop doesn't need it, and injected scripts could fingerprint users.
- Camera/Microphone: media capture APIs. Disabled to prevent injected scripts from silently activating them.
</details>

### M13. Practical
`StringSetAsync` on StackExchange.Redis has two overloads. Write the correct Moq setup for the one called by production code in .NET + Redis 2.6+.

<details><summary>Answer</summary>
```csharp
mockDatabase.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<Expiration>(),       // not TimeSpan?
        It.IsAny<ValueCondition>(),   // not When
        It.IsAny<CommandFlags>()))
    .ReturnsAsync(true);

// Always add Verify on failure-path tests
mockDatabase.As<IDatabaseAsync>()
    .Verify(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<Expiration>(),
        It.IsAny<ValueCondition>(),
        It.IsAny<CommandFlags>()),
        Times.Once);
```
If you use the old overload (`TimeSpan?/When`) Moq ignores the setup and returns the default — the test passes but proves nothing.
</details>

### M14. Question
Why do you need `mockDatabase.As<IDatabaseAsync>()` instead of `mockDatabase.Setup(...)` directly when mocking StackExchange.Redis?

<details><summary>Answer</summary>
`StringSetAsync` is an explicit interface implementation — it's only accessible through `IDatabaseAsync`, not through `IDatabase`. Moq's `Setup` on the concrete type can't reach explicit interface implementations. `As<IDatabaseAsync>()` casts the mock to the interface so Moq can intercept the call.
</details>

### M15. Question
What coverage target do you set for each layer in this project, and why?

<details><summary>Answer</summary>

| Layer | Target | Reason |
|---|---|---|
| Core | 90% | Pure logic, no dependencies — no excuse not to test |
| Application | ~90% | Specs and mappings are pure C# — easy to test |
| Infrastructure (services) | ~85% | CartService 100%, OrderService 80% |
| API controllers | 0% | Covered by Playwright E2E (Task 37) |

Infrastructure overall reports ~2.6% — misleading. Factories, seed scripts, and EF config classes (all 0%) drag down the average. The meaningful services are at 80-100%.
</details>

### M16. Scenario
`Order.GetTotal()` returns `-5` when discount is `60`, subtotal is `50`, shipping is `5`. You don't know if this is intentional. What do you do?

<details><summary>Answer</summary>
Write a test that documents the current behavior with a comment explaining it's documentation, not validation:

```csharp
[Fact]
public void GetTotal_WhenDiscountExceedsSubtotal_TotalIsNegative()
{
    // Documents current behaviour: no floor at zero.
    // If a floor is added later, change Should().Be(-5m) to Should().Be(0m).
    var order = MakeOrder(subtotal: 50m, discount: 60m, shipping: 5m);
    order.GetTotal().Should().Be(-5m);
}
```

This acts as a regression alarm: if someone adds a floor, the test breaks and forces a conscious decision. Don't silently "fix" undocumented behavior.
</details>

---

## HARD (debug + design + senior stretch) — 40%

### H1. Design
Your CSP blocks a third-party payment widget (e.g., Stripe Elements loads JS from `js.stripe.com`). How do you allow it without using `unsafe-inline`?

<details><summary>Answer</summary>
Add `https://js.stripe.com` to `script-src`:
```csharp
csp.AddScriptSrc().Self().UnsafeInline().From("https://js.stripe.com");
```
For styles from Stripe: add to `style-src`. For iframes: add to `frame-src`. Each resource type has its own CSP directive. You whitelist the specific domain — not `*` or `unsafe-eval`.
</details>

### H2. Senior Question
You use optimistic concurrency for stock but what about the order itself? Two webhook callbacks arrive for the same `PaymentIntentId`. How do you prevent duplicate orders?

<details><summary>Answer</summary>
Unique constraint on `PaymentIntentId` in the orders table. When the second webhook tries to insert a duplicate, the DB throws a unique constraint violation. Catch it and return the existing order. This is idempotency — same input, same result. (This is Task 18 in the execution plan.)
</details>

### H3. Practical
Remove `[ConcurrencyCheck]` from `Product.cs`. Run the concurrency test. It should FAIL (the second order succeeds — oversell). Observe the failure message. Re-add the attribute. Run the test again. Confirm it passes.

This proves you understand what the attribute does — not just that it's there.

### H4. Senior Design
Design a retry strategy for `DbUpdateConcurrencyException` in a background order processor. Include: max retries, backoff, what to reload, when to give up.

<details><summary>Answer</summary>
```csharp
for (int attempt = 0; attempt < 3; attempt++)
{
    try
    {
        var product = await repo.GetByIdAsync(productId); // fresh read
        if (product.QuantityInStock < quantity)
            throw new InvalidOperationException("Insufficient stock");
        product.QuantityInStock -= quantity;
        await unitOfWork.Complete();
        return; // success
    }
    catch (DbUpdateConcurrencyException)
    {
        if (attempt == 2) throw; // give up after 3 attempts
        await Task.Delay(50 * (attempt + 1)); // linear backoff: 50ms, 100ms
        // EF context must be refreshed — detach or create new context
    }
}
```
Key points: re-read the entity (not the stale tracked copy), re-check stock, backoff between retries, bounded attempts, give up and escalate if max reached.
</details>

### H5. Senior Question
Angular Material uses `ViewEncapsulation.None` which injects global `<style>` tags. Why does this conflict with CSP? What would a production-grade solution look like?

<details><summary>Answer</summary>
CSP `style-src` without `unsafe-inline` blocks any `<style>` tag not loaded from an allowed source. Material injects styles at runtime into the DOM — these are inline styles with no `nonce` attribute.

Production solution:
1. Server generates a random nonce per request
2. Middleware injects `nonce` attribute into all `<style>` tags in the rendered HTML
3. CSP header includes `style-src 'nonce-<value>'` instead of `unsafe-inline`
4. Requires Angular Universal (SSR) or custom middleware to modify the HTML response

This is complex and not worth it for most projects — `unsafe-inline` is the accepted tradeoff.
</details>

### H6. Practical — System Design
Draw the full Stripe payment flow from button click to order creation. Include:
```
Browser → Stripe.js → Stripe API → Your API → OrderService → DB → Stripe webhook → SignalR → Angular
```
Include: what tokens are passed, where the SecretKey is used, what the webhook verifies, what SignalR sends.

<details><summary>Answer</summary>

```
1. Angular calls POST /api/payments/intent → creates PaymentIntent with Stripe SDK (SecretKey)
   → returns clientSecret to browser
2. Browser → Stripe.js.confirmCardPayment(clientSecret, card) → card data goes to Stripe directly
   → never touches your server
3. Stripe charges the card → sends payment_intent.succeeded to POST /api/payments/webhook
4. Webhook handler:
   a. Verifies signature using WhSecret (proves it's from Stripe)
   b. Finds order by PaymentIntentId
   c. Updates order status to PaymentReceived
   d. Sends SignalR notification to buyer's connection group
5. Angular receives SignalR OrderCompleteNotification → sets orderSignal() → spinner resolves
```
</details>

### H7. Debug
Your Angular app suddenly shows the checkout spinner forever. The payment succeeded (you see the charge in Stripe Dashboard). What's the debugging checklist?

<details><summary>Answer</summary>
1. Check webhook endpoint: is Stripe sending the event? (`stripe listen` logs or Stripe Dashboard → Webhooks)
2. Check API logs: does the webhook return 200 or 500?
3. If 500 "Invalid signature" → `WhSecret` is stale. Copy from `stripe listen` output, set in user secrets, **restart API** (not hot reload)
4. Check SignalR: is the hub connected? DevTools → Network → WS tab → look for the SignalR connection
5. Check order status in DB: `SELECT Status FROM Orders WHERE PaymentIntentId = '...'` — is it `PaymentReceived`?
6. Check `orderSignal()` in Angular DevTools: is it being set?
</details>

### H8. Senior Question
You use `SameSite=Strict` for CSRF protection. A colleague argues you should add anti-forgery tokens (`[ValidateAntiForgeryToken]`) too. Are they right? When would one not be enough?

<details><summary>Answer</summary>
They have a point for defense-in-depth. `SameSite=Strict` relies on the browser enforcing the policy — older browsers (pre-2020) don't support it. Anti-forgery tokens are server-verified regardless of browser support.

When `SameSite` alone isn't enough:
- Subdomain attacks: `evil.yoursite.com` is same-site as `yoursite.com` — `SameSite` allows it
- Browser bugs or plugins that bypass SameSite
- Mixed SameSite policy: if you switch to `Lax` for usability, GET-based state changes need anti-forgery protection

For a new app targeting modern browsers, `SameSite=Strict` alone is usually sufficient. For a financial app or high-security context, add anti-forgery tokens as defense-in-depth.
</details>

### H9. Senior Design
Your e-commerce app grows to 1000 concurrent users. The optimistic concurrency approach means popular products get frequent `DbUpdateConcurrencyException` during flash sales. Design a better solution.

<details><summary>Answer</summary>
Options (progressive):
1. **Retry with jitter**: catch the exception, wait random 50-200ms, re-read, retry (up to 3x). Spreads contention.
2. **Atomic SQL decrement**: skip EF entirely for stock updates. `UPDATE Products SET QuantityInStock = QuantityInStock - @qty WHERE Id = @id AND QuantityInStock >= @qty`. Returns affected rows — 0 means out of stock. No exception, no retry.
3. **Redis-based stock reservation**: decrement stock in Redis (atomic `DECRBY`). If Redis says 0 → reject immediately. Write to SQL asynchronously. Redis is in-memory so it handles high concurrency naturally.
4. **Queue-based ordering**: instead of processing orders synchronously, enqueue them. A single consumer processes orders sequentially — no concurrency issue. Trade latency for correctness.

For flash sales: option 3 (Redis reservation) or option 2 (atomic SQL) are standard.
</details>

### H10. Practical
Write a minimal Angular signal example from scratch (no looking at docs):
- A `checkout` signal with `{ address: boolean, payment: boolean }`
- A `computed` signal that returns `true` only when both are `true`
- A function that correctly sets `address` to `true`
- Demonstrate what happens if you mutate instead of spread

<details><summary>Answer</summary>

```typescript
const checkout = signal({ address: false, payment: false });
const isComplete = computed(() => checkout().address && checkout().payment);

// Correct
function markAddressComplete() {
    checkout.update(s => ({ ...s, address: true }));
}

// Broken — same reference, computed won't re-evaluate
function markAddressBroken() {
    checkout.update(s => { s.address = true; return s; });
}
```
</details>

### H11. Senior Question
Compare three EF Core concurrency strategies. For each, give the generated SQL and a use case:
1. No concurrency control (default)
2. `[ConcurrencyCheck]` on a specific column
3. `[Timestamp]` (rowversion)

<details><summary>Answer</summary>

**1. Default (last-write-wins):**
```sql
UPDATE Products SET QuantityInStock = 4 WHERE Id = 1
```
Use case: non-critical fields like user preferences. Overwrites are acceptable.

**2. [ConcurrencyCheck] on QuantityInStock:**
```sql
UPDATE Products SET QuantityInStock = 4 WHERE Id = 1 AND QuantityInStock = 5
```
Use case: protecting a specific business-critical field (stock, balance). Works with any DB.

**3. [Timestamp]:**
```sql
UPDATE Products SET QuantityInStock = 4 WHERE Id = 1 AND RowVersion = 0x000000000000007B
```
Use case: protecting the entire row. Any change to any column updates the version. SQL Server only. Good for entities where any concurrent modification is a conflict (e.g., user profile, config records).
</details>

### H12. Debug
A colleague's PR has a `CartService_Tests.cs` failure-path test with no `Verify`. You run the tests. They pass. You delete `SetCartAsync` entirely and run the tests again. They still pass. Walk through why and what you'd say in code review.

<details><summary>Answer</summary>
Without `Verify`, there's no assertion that `SetCartAsync` was called. The mock has an unmatched setup (wrong overload or the method is gone), so Moq returns `false` by default. The test asserts `result.Should().BeNull()` — and since the method isn't called at all, the service returns `null` anyway. It passes by coincidence.

Code review comment: "This test proves nothing. Add `mockDatabase.As<IDatabaseAsync>().Verify(x => x.StringSetAsync(...), Times.Once)`. Delete the production method — the test must break. Then restore the method and confirm the test passes again. That's the red-green cycle that validates the test."
</details>

### H13. Senior Design
`PaymentService` calls `new PaymentIntentService()` and `new RefundService()` directly. You want to unit test it. What are the three options, and which is right for this project?

<details><summary>Answer</summary>
**Option 1: Mark `[ExcludeFromCodeCoverage]` with a documented reason** (chosen here).
- Fast. No architectural change required. The comment explains what the fix would be.
- Right for: CV project with time constraints.

**Option 2: Inject an `IStripeClient` abstraction**.
- Create `IStripeClient` with `CreatePaymentIntentAsync` / `CreateRefundAsync`. The Stripe SDK wrapper implements it. Tests inject a mock.
- Right for: production codebases with a payment team.

**Option 3: Use Stripe's test-mode sandbox in integration tests**.
- No mocking — real HTTP calls to `api.stripe.com/v1` with test API keys.
- Right for: end-to-end payment flow verification. Not unit tests.

For this project: Option 1 chosen. Comment in the source: "Calls Stripe SDK via `new` — no DI seam to mock. Fix: inject `IStripeClient` abstraction. Deferred."
</details>

### H14. Scenario — Idempotency Testing
You need to test that calling `CreateOrderAsync` twice with the same cart returns the existing order, not a duplicate. Your first approach: seed an order with Id=99 into EF InMemory, then call the service once. The test fails — a new order with Id=100 is created. Why does manual seeding fail, and what's the correct approach?

<details><summary>Answer</summary>
EF InMemory doesn't enforce required navigation properties the same way SQL Server does. When you seed `new Order { Id = 99, DeliveryMethod = null! }`, the `GetEntityWithSpec` with `.Include(o => o.DeliveryMethod)` doesn't find it — the Include filter or null nav causes a mismatch. So the pre-insert check misses the seeded order and creates Id=100.

Correct approach: call the real flow twice, not once:
```csharp
var first = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto());
var second = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto());

second!.Id.Should().Be(first!.Id);       // same order returned
product!.QuantityInStock.Should().Be(8); // decremented once, not twice
```
This tests user-visible behavior: place the same order twice, get one result, stock not double-charged. No seeding needed.
</details>

### H15. Senior Design
You need to implement a "stock reservation" system: when a user starts checkout, reserve their items for 15 minutes. If they don't complete, release the reservation. Design the backend.

<details><summary>Answer</summary>
Data model:
```csharp
class StockReservation {
    int ProductId;
    int Quantity;
    string CartId;
    DateTime ExpiresAt; // now + 15min
}
```

Reserve flow:
1. User enters checkout → POST /api/reservations
2. Atomically: `INSERT reservation + UPDATE Products SET QuantityInStock -= qty WHERE stock >= qty` (in a transaction)
3. If stock insufficient → 409

Release flow (two mechanisms):
1. **On order completion**: delete reservation (stock already decremented)
2. **Background job** (runs every 1 min): `SELECT WHERE ExpiresAt < now()` → restore stock → delete expired reservations

Checkout flow change: `OrderService` no longer decrements stock (already reserved). Instead, it marks the reservation as "fulfilled."

Tradeoffs: more complex, reservation table needs cleanup, but eliminates the "add to cart → someone else buys it" race entirely.
</details>
