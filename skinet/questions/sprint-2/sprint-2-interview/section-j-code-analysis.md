## SECTION J — CROSS-CUTTING CODE ANALYSIS & DEBUGGING

> **Source:** Multi-bug code snippets spanning Sprint 2 concerns.

---

### MEDIUM

**J1.** [HIGH] This EF query triggers N+1. Find it and fix with the minimal diff.

```csharp
public async Task<Order?> CreateOrderAsync(string email, CreateOrderDto dto)
{
    var cart = await _cartService.GetCartAsync(dto.CartId);
    var items = new List<OrderItem>();
    foreach (var cartItem in cart.Items)
    {
        var product = await _context.Products.FindAsync(cartItem.ProductId);
        items.Add(new OrderItem(product.Name, product.Price, cartItem.Quantity));
        product.QuantityInStock -= cartItem.Quantity;
    }
    // ... rest
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**The N+1:** one `FindAsync` per cart item. A 10-item cart → 10 separate `SELECT * FROM Products WHERE Id = @id` round-trips.

**Fix — batch load:**

```csharp
var productIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();
var products = await _context.Products
    .Where(p => productIds.Contains(p.Id))
    .ToDictionaryAsync(p => p.Id);

foreach (var cartItem in cart.Items)
{
    if (!products.TryGetValue(cartItem.ProductId, out var product))
        throw new ProductNotFoundException(cartItem.ProductId);
    
    items.Add(new OrderItem(product.Name, product.Price, cartItem.Quantity));
    product.QuantityInStock -= cartItem.Quantity;
}
```

One query, O(1) dict lookup per item.

**Important caveat:** `FindAsync` consults the change tracker first (returns a tracked entity if already loaded). `Where(...).ToDictionaryAsync` goes to the DB even if entities are tracked — could return stale data if something else modified them in-memory. For this flow, it's safe (fresh context).

**Production consequence:** at 10 items per cart and 1000 checkouts/min, N+1 is 10,000 DB round-trips/min of pure waste. DB CPU and connection pool suffer.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Use `.Include()`." `Include` is for navigation properties; it doesn't help with `FindAsync` inside a loop.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **EF Core query plan caching** — the batched `Where(p => productIds.Contains(p.Id))` generates a SQL IN clause that caches well. The `FindAsync`-in-loop pattern generates different SQL per Id (`WHERE Id = 1`, `WHERE Id = 2`...) with more parameter-sniffing variance. Plan cache is another reason to batch.


</details>

---

**J2.** [HIGH] This cart signal update breaks the UI. Fix it.

```typescript
onRemoveItem(itemId: number) {
    this.cart.update(c => {
        c.items = c.items.filter(i => i.id !== itemId);
        return c;
    });
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

- **Bug:** `c.items = c.items.filter(...)` mutates the original cart object and returns the **same reference**
- Signal `===` check: `c === c` → no re-render

**Fix:**
```typescript
onRemoveItem(itemId: number) {
    this.cart.update(c => ({
        ...c,
        items: c.items.filter(i => i.id !== itemId)
    }));
}
```

Spread creates a new outer object. `filter` already returns a new array. Both levels get new references.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Only fixing the outer spread. `filter` already creates a new array, so the inner reference was fine — but the outer object reference wasn't.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that this is exactly the Bug 1 pattern — the same reference-equality trap regardless of whether the mutation is a direct property set or an assignment. The lint rule should catch both.


</details>

---

### HARD

**J3.** [HIGH] Find the 5 bugs in this `CreateOrderAsync` method:

```csharp
public async Task<Order?> CreateOrderAsync(string email, CreateOrderDto dto)
{
    var cart = await _cartService.GetCartAsync(dto.CartId);
    var items = new List<OrderItem>();

    foreach (var cartItem in cart.Items)
    {
        var product = await _context.Products.FindAsync(cartItem.ProductId);
        product.QuantityInStock -= cartItem.Quantity;
        items.Add(new OrderItem(product.Name, product.Price, cartItem.Quantity));
    }

    var order = new Order(email, dto.ShippingAddress, dto.DeliveryMethodId, items,
        cart.PaymentIntentId);
    _context.Orders.Add(order);
    await _context.SaveChangesAsync();

    return order;
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Bug 1 — `cart` can be null (no check).**
If `GetCartAsync` returns null (expired / wrong ID), `cart.Items` throws `NullReferenceException`. Fix:
```csharp
if (cart == null) throw new CartNotFoundException(dto.CartId);
```

**Bug 2 — `product` can be null (no check).**
`FindAsync` returns null if the product was deleted. `product.QuantityInStock` throws. Fix:
```csharp
if (product == null) throw new ProductNotFoundException(cartItem.ProductId);
```

**Bug 3 — no stock validation before decrement.**
`product.QuantityInStock -= cartItem.Quantity` can go negative. Fix:
```csharp
if (product.QuantityInStock < cartItem.Quantity)
    throw new InsufficientStockException(product.Id);
```

**Bug 4 — no idempotency check (Task 18).**
Two concurrent calls with the same cart both insert. Fix:
```csharp
var existing = await _context.Orders.SingleOrDefaultAsync(o => o.PaymentIntentId == cart.PaymentIntentId);
if (existing != null) return existing;
```

**Bug 5 — no price-change detection (Task 17).**
Cart price vs DB price mismatch is silently accepted. User could pay old lower price if client tampered with Redis indirectly. Fix:
```csharp
if (cartItem.Price != product.Price)
{
    // accumulate, clear PaymentIntent, throw PriceChangedException
}
```

**Bonus bug 6 — N+1 query (see J1).**

**Corrected version brings in Tasks 13, 17, 18 simultaneously.**

**Production consequence:** any one of these bugs causes a clean failure in dev but compounds under real traffic — NREs on expired carts, negative stock, duplicate orders on double-click, silent price fraud, DB meltdown from N+1.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Finding only 2-3. Interviewers flag this as "misses the systemic gaps." The code looks fine until you stress-test it with null, concurrency, and drift.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Noting that **this is exactly what the Sprint 2 tasks addressed**. Each bug maps to a specific task. Walking through the mapping shows holistic understanding: Task 13 → bug 3, Task 17 → bug 5, Task 18 → bug 4.


</details>

---

**J4.** [HIGH] [OUT-OF-SCOPE] Find the infinite loop bug in this Angular error interceptor:

```typescript
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
    return next(req).pipe(
        catchError((err: HttpErrorResponse) => {
            if (err.status === 401) {
                const router = inject(Router);
                router.navigateByUrl('/account/login');
            }
            if (err.status === 400 && err.error.errors) {
                const validation = err.error.errors;
                /* ... */
            }
            if (err.status === 400 && !err.error.errors) {
                const snackbar = inject(SnackbarService);
                snackbar.error(err.error.message);
            }
            return throwError(() => err);
        })
    );
};
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**The infinite loop:**

1. User on `/account/login`. Form submits wrong password.
2. `POST /api/account/login` → 401 Unauthorised.
3. Interceptor catches 401 → `router.navigateByUrl('/account/login')`.
4. Angular navigates to `/account/login` again → `LoginComponent` reinitialises.
5. If `LoginComponent.ngOnInit` makes an API call that requires auth (e.g. "check token validity"), that call hits 401.
6. Back to step 3. Loop.

Even without an explicit auth call in `ngOnInit`, any initialisation-time HTTP request that returns 401 relaunches the cycle.

**Secondary issue — no guard for the login endpoint itself:**
If the user is logging in and gets 401 (wrong password), the interceptor redirects them to... the page they're already on. Confusing UX.

**Fix:**
```typescript
if (err.status === 401 && !req.url.includes('/account/login')) {
    const router = inject(Router);
    router.navigateByUrl('/account/login');
}
```

Skip the redirect if the failing request is already the login call. The login form component handles its own 401 (shows "wrong password" inline).

**Additional hardening — throttle the redirect:**
If any OTHER request hits 401 after already being on `/account/login`, still redirecting is pointless. Check the current URL too:
```typescript
const router = inject(Router);
if (err.status === 401 
    && !req.url.includes('/account/login')
    && !router.url.startsWith('/account/login')) {
    router.navigateByUrl('/account/login');
}
```

**Production consequence:** the loop hammers your auth endpoint with requests, eats browser CPU, may rate-limit the user's IP, and the user sees flickering/stuck login page.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Only adding the URL check without explaining the full cascade. Interviewer wants to see you traced the loop step-by-step.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that a better long-term design is an **auth state signal** — the app globally tracks login state; the interceptor updates it on 401 and the router guard redirects. The interceptor shouldn't navigate directly; it should mutate state and let the app react. Separation of concerns.


</details>

---

**J5.** [HIGH] This webhook handler has a security bug. Find it.

```csharp
public async Task<IActionResult> StripeWebhook()
{
    var json = await new StreamReader(Request.Body).ReadToEndAsync();
    var stripeEvent = JsonSerializer.Deserialize<Event>(json);
    
    var signature = Request.Headers["Stripe-Signature"];
    EventUtility.ValidateSignature(json, signature, _whSecret);

    if (stripeEvent.Type == "payment_intent.succeeded")
    {
        var intent = (PaymentIntent)stripeEvent.Data.Object;
        var order = await _context.Orders
            .SingleOrDefaultAsync(o => o.PaymentIntentId == intent.Id);
        order.Status = OrderStatus.PaymentReceived;
        await _context.SaveChangesAsync();
    }

    return Ok();
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

- **Security bug: signature validation happens AFTER deserialization**

- **Why order matters — consequences:**
  1. **DoS surface:** attacker sends malformed JSON → server allocates/parses memory before checking legitimacy → garbage requests waste resources
  2. **Deserialization exploits:** if `System.Text.Json` has a parsing vulnerability, untrusted input hits it *before* you verify the sender
  3. **Validation never fires on bad JSON:** `JsonSerializer.Deserialize` throws on malformed input → handler returns 500 before signature check runs → attacker learns your parser's shape from error responses

**Correct pattern — validate FIRST, then parse via Stripe SDK:**

```csharp
public async Task<IActionResult> StripeWebhook()
{
    var json = await new StreamReader(Request.Body).ReadToEndAsync();
    var signature = Request.Headers["Stripe-Signature"];

    // ConstructEvent validates signature AND parses atomically
    Event stripeEvent;
    try
    {
        stripeEvent = EventUtility.ConstructEvent(json, signature, _whSecret);
    }
    catch (StripeException)
    {
        return BadRequest();  // invalid signature or unparseable — reject
    }

    if (stripeEvent.Type == "payment_intent.succeeded")
    {
        var intent = (PaymentIntent)stripeEvent.Data.Object;
        var order = await _context.Orders
            .SingleOrDefaultAsync(o => o.PaymentIntentId == intent.Id);
        if (order == null) return Ok();  // idempotent: unknown intent, acknowledge
        
        order.Status = OrderStatus.PaymentReceived;
        await _context.SaveChangesAsync();
    }

    return Ok();
}
```

`EventUtility.ConstructEvent` is Stripe's SDK method that does HMAC verification and parsing in one step. No untrusted JSON is processed separately.

**Additional gap in original:**
- `order` could be null (unknown PaymentIntentId — could happen with webhook retries before order exists). Throws NRE on `order.Status = ...`.

**Production consequence:** without proper signature-first design, attackers can probe your webhook endpoint and waste server resources. Real exploits in the wild have used similar patterns (Magento webhook CVE-2022-xxxx category).


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Only spotting "validate signature." You need to explain *why order matters* — parse-then-validate vs validate-then-parse.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that Stripe's signature includes a **timestamp** that must be within a 5-minute window (replay protection). `ConstructEvent` enforces this automatically. An attacker who captures a legitimate signed webhook can't replay it 10 minutes later. Shows you know the signature scheme beyond "it's HMAC."


</details>

---

**J6.** [HIGH] This transaction isolation choice has a subtle concurrency bug. Explain.

```csharp
public async Task<Product?> GetProductWithStockCheckAsync(int id, int requestedQty)
{
    using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

    var product = await _context.Products.FindAsync(id);
    if (product == null || product.QuantityInStock < requestedQty)
    {
        await tx.RollbackAsync();
        return null;
    }

    product.QuantityInStock -= requestedQty;
    await _context.SaveChangesAsync();
    await tx.CommitAsync();
    return product;
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**The bug — READ COMMITTED allows non-repeatable reads, which this code assumes it doesn't.**

Scenario:
```
Ctx1 at T=0: FindAsync(1) → stock=5. Passes check >= 3.
Ctx2 at T=1: FindAsync(1) → stock=5. Passes check >= 3.
Ctx1 at T=2: UPDATE stock=2 (5-3). Commits.
Ctx2 at T=3: UPDATE stock=2 (5-3). Commits. <- BUG: stock should be -1 after Ctx2.
Result: two orders fulfilled for 6 units against 5 units of stock.
```

The transaction doesn't save you because:
- READ COMMITTED only prevents *dirty reads* (reading uncommitted data).
- Both reads happened against committed data (the original stock=5).
- The writes don't conflict at the SQL level — they're just two UPDATEs to the same row.

**Why the transaction gives a false sense of safety:** developers often assume transactions prevent races. They prevent **consistency violations across multiple writes** (atomicity) but not **logical races on read-modify-write patterns**.

**Fix options:**

**Option 1 — `[ConcurrencyCheck]` on the column (optimistic):**
Same as Task 13's fix. WHERE clause includes the original value; the second UPDATE matches 0 rows → throws.

**Option 2 — raise isolation to `SERIALIZABLE`:**
Blocks concurrent reads. Kills throughput on hot rows. Not recommended for e-commerce.

**Option 3 — `SELECT FOR UPDATE` (pessimistic row lock):**
EF Core equivalent: add `SELECT ... WITH (UPDLOCK, HOLDLOCK)` via raw SQL or a `Microsoft.EntityFrameworkCore.SqlServer` extension. Reader locks the row for the transaction duration; Ctx2 blocks until Ctx1 commits.

**Option 4 — atomic UPDATE with stock-check in WHERE:**
```sql
UPDATE Products SET QuantityInStock = QuantityInStock - @qty
WHERE Id = @id AND QuantityInStock >= @qty
```
One round-trip. Returns row count — if 0, rejection. No race window.

For most e-commerce: **Option 1** (what Task 13 chose). For truly hot inventory: **Option 4** is faster because it avoids the round-trip.

**Production consequence:** the original code passes low-traffic tests but produces oversells under real load. Classic "works on my machine" pattern.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Use SERIALIZABLE." Kills throughput. Shows no production experience.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **Option 4** (atomic UPDATE with conditional WHERE) is what high-traffic systems use — Shopify, Amazon inventory — because it eliminates the read-then-write round-trip entirely. The transaction becomes unnecessary. Senior-level micro-optimisation.


</details>

---

**J7.** [MEDIUM] [OUT-OF-SCOPE] This "fix" to a memory leak in a checkout component has a subtler leak. Find it.

```typescript
@Component({...})
export class CheckoutComponent implements OnDestroy {
    private cartSub = this.cartService.cart$.subscribe(c => {
        this.cart = c;
        this.recalculateTotals();
    });

    private signalrSub = this.signalr.orderSignal.subscribe?.(o => {
        this.order = o;
    });

    ngOnDestroy() {
        this.cartSub.unsubscribe();
    }
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**The subtler leak — `signalrSub` is never unsubscribed.**

The original fix unsubscribes `cartSub` but not `signalrSub`. On component destroy:
- The SignalR emissions continue.
- The callback still holds a reference to `this` (the destroyed component).
- `this.order = o` writes to a detached component.
- Garbage collection is blocked because SignalR's observer list holds the callback, which holds `this`.

Every navigation to `/checkout` and back creates a new orphaned subscription. Memory climbs.

**Also suspicious:** `this.signalr.orderSignal.subscribe?.()` — `?.` suggests `orderSignal` might not be a subscribable. If it's a signal (not an observable), `.subscribe` doesn't exist; the chain returns undefined → `subscribeSub` is undefined → `unsubscribe()` would throw. The `?.` hides the bug silently.

**Correct fixes — modern Angular:**

**Option 1 — takeUntilDestroyed() (Angular 16+):**
```typescript
this.cartService.cart$
    .pipe(takeUntilDestroyed())
    .subscribe(c => { ... });

this.signalr.orderSignal$
    .pipe(takeUntilDestroyed())
    .subscribe(o => { ... });
```
No `ngOnDestroy` needed. Declarative and impossible to forget.

**Option 2 — use signals, not subscriptions:**
```typescript
cart = this.cartService.cart;    // signal
order = this.signalr.orderSignal; // signal

// template: {{ cart() }}, {{ order() }}
```
Signals don't leak on component destroy — they clean up automatically.

**Option 3 — AsyncPipe in template:**
```html
<div>{{ cart$ | async }}</div>
```
Pipe auto-unsubscribes on destroy.

**Production consequence:** long-lived apps (SPAs) with untracked subscriptions leak memory linearly with navigation. Browsers eventually slow to a crawl.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Adding the missing `signalrSub.unsubscribe()`. Works but fragile — the next developer adds a new subscription and forgets again. Systemic fix beats per-component vigilance.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `takeUntilDestroyed()` depends on **injecting `DestroyRef`** — it works in component constructors but not arbitrary code. Knowing the injection context constraints signals you've actually used it in anger.


</details>

---

**J8.** [HIGH] Find the captive-dependency bug in this DI registration:

```csharp
// Program.cs
builder.Services.AddSingleton<ICartCache, CartCache>();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(config.GetConnectionString("Default")));

// CartCache.cs
public class CartCache : ICartCache
{
    private readonly AppDbContext _db;
    public CartCache(AppDbContext db) => _db = db;

    public async Task<Product?> GetProductAsync(int id)
        => await _db.Products.FindAsync(id);
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**The bug — a Singleton captures a Scoped dependency.**

- `AddDbContext` registers `AppDbContext` as **Scoped** (per HTTP request) by default.
- `AddSingleton<ICartCache>` registers `CartCache` as **Singleton** (one instance for the app lifetime).
- The Singleton resolves `AppDbContext` ONCE, at construction, and **pins that scoped instance forever**.

**Consequences:**

1. **DbContext is not thread-safe.** Two concurrent requests both hit `CartCache.GetProductAsync` → both touch the same DbContext → one of:
   - `InvalidOperationException: A second operation started on this context before a previous operation completed.`
   - Corrupted internal change-tracker state.
   - Connection errors.

2. **Memory leak.** The captured DbContext holds a DB connection and a change tracker. Every entity ever loaded through the cache stays tracked forever. Memory grows monotonically.

3. **Stale data.** The context caches entities. After the first load, every `FindAsync(1)` returns the same in-memory object — even if SQL changed. The cache never sees DB updates.

4. **Dev-time detection:** .NET's DI validator catches this in `Development` environment if you call:
   ```csharp
   builder.Host.UseDefaultServiceProvider(o => o.ValidateScopes = true);
   ```
   On first Singleton resolution it throws `InvalidOperationException: Cannot consume scoped service 'AppDbContext' from singleton 'CartCache'`. **Enable this for every new project.**

**Fixes — pick one based on the actual need:**

**Fix 1 — Singleton that explicitly opens a scope per operation (correct for a true cache):**
```csharp
public class CartCache : ICartCache
{
    private readonly IServiceScopeFactory _scopeFactory;
    public CartCache(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<Product?> GetProductAsync(int id)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Products.FindAsync(id);
    }
}
```

**Fix 2 — inject `IDbContextFactory<AppDbContext>` (EF Core's native solution):**
```csharp
builder.Services.AddDbContextFactory<AppDbContext>(opt => opt.UseSqlServer(...));

public class CartCache : ICartCache
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public CartCache(IDbContextFactory<AppDbContext> f) => _factory = f;

    public async Task<Product?> GetProductAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Products.FindAsync(id);
    }
}
```

**Fix 3 — if you don't actually need Singleton, demote it:**
```csharp
builder.Services.AddScoped<ICartCache, CartCache>();
```
Per-request instance. No captive issue. But now the "cache" is per-request → ineffective as a cache.

**Decision rule:**
- True in-memory cache that persists across requests → Singleton + `IServiceScopeFactory` or `IDbContextFactory`.
- Per-request helper → Scoped.
- Stateless utility → Singleton (no DbContext needed).

**Production consequence:** this bug ships without visible errors on low traffic. Once you hit 2+ concurrent users, random `InvalidOperationException`s appear with no clear pattern — the hardest kind of bug to triage. `ValidateScopes = true` would have caught it on first startup.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Add a lock around the DbContext usage." Serialises all cache access, kills throughput, and doesn't fix memory/staleness. Treats the symptom.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `IHttpContextAccessor` injected into a Singleton is a related trap — if you cache the `HttpContext` reference at construction, you get a null (or worse, the FIRST request's) context for every subsequent call. Both bugs share a root cause: **lifetime inversion**. Naming the general category shows architectural vocabulary.


</details>

---

**J9.** [MEDIUM] This code deadlocks in legacy ASP.NET but works in ASP.NET Core. Explain why — and where it still bites you in Core.

```csharp
public ActionResult<Order> GetOrder(int id)
{
    var order = _orderService.GetOrderAsync(id).Result;  // sync-over-async
    if (order == null) return NotFound();
    return order;
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Why legacy ASP.NET deadlocks:**

1. `GetOrderAsync(id)` starts running on the request thread.
2. It hits an `await` (e.g. `await _context.Orders.FindAsync(id)`).
3. The continuation (the code after `await`) is captured with the **current SynchronizationContext**. Legacy ASP.NET has `AspNetSynchronizationContext` — it schedules continuations back onto the **same request thread**.
4. Meanwhile, the caller has blocked that thread by calling `.Result`.
5. Continuation waits for the thread → thread waits for the Result → **deadlock**.

**Why ASP.NET Core doesn't deadlock:**

ASP.NET Core **has no SynchronizationContext**. Continuations after `await` post to the thread pool, not back to the original thread. The original thread is free to block on `.Result`; the continuation runs on a different thread pool thread; Result eventually completes.

**So the bug is gone in Core. Case closed?**

**No — three places it still bites:**

1. **Thread pool starvation at scale.**
   - Every request that does `.Result` consumes a thread pool thread while it waits.
   - Under load, all thread pool threads are blocked on `.Result` calls.
   - Incoming requests can't acquire a thread → requests queue → latencies spike → eventually HTTP 503.
   - Symptom: "the app works fine at 10 rps, falls over at 100 rps."

2. **Legacy library callbacks still bring sync contexts.**
   - Any third-party library running inside an ASP.NET Core request that installs a SyncContext (some Windows Forms, some WPF integrations) re-introduces the deadlock risk.
   - Blazor Server has a SyncContext for UI work — sync-over-async on the UI thread deadlocks again.

3. **WinForms/WPF desktop code.**
   - `button_Click` handlers call `SomeAsyncMethod().Result` → deadlock. The UI thread has a SyncContext that schedules continuations back to itself.
   - Developers moving between desktop and web see the rule "Core doesn't deadlock" and apply it universally. Wrong.

**The universal fix — don't block on async:**

```csharp
public async Task<ActionResult<Order>> GetOrder(int id)
{
    var order = await _orderService.GetOrderAsync(id);
    if (order == null) return NotFound();
    return order;
}
```

**ConfigureAwait(false) — does it help?**

```csharp
var order = _orderService.GetOrderAsync(id).ConfigureAwait(false).GetAwaiter().GetResult();
```

- **In legacy ASP.NET**: if the entire call chain uses `ConfigureAwait(false)`, continuations skip the SyncContext → no deadlock. But if ONE library forgets, deadlock returns. Fragile.
- **In ASP.NET Core**: no effect; there's no SyncContext to avoid.
- **Library authors** still add `ConfigureAwait(false)` defensively — their code might be called from WinForms/WPF. Application code (controllers, services) in Core doesn't need it.

**Production consequence:** "works on my dev machine, dies in load tests" is the classic fingerprint of sync-over-async. Always `await`. Never `.Result` or `.Wait()` in controllers.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"ASP.NET Core can't deadlock, so `.Result` is fine." Wrong — it doesn't deadlock, but it starves the thread pool under load and reintroduces deadlocks the moment a SyncContext-using library enters the chain.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `Task.Run(() => SomeAsync()).Result` is **different** from `.Result` directly — `Task.Run` moves the work to the thread pool immediately, so legacy-ASP.NET deadlocks don't form. Some library authors use this as a "sync wrapper" hack. It still costs a thread pool thread, so it doesn't solve the scaling problem, but it's not identical to naked `.Result`.


</details>

---

### ⚡ Syntax Quick-Fire

**JQ1.** [HIGH] EF Core: `ToDictionaryAsync(p => p.Id)` vs `ToListAsync()` — when does the dictionary pay off?

<details>
<summary>The 5% Answer</summary>

- `ToDictionaryAsync` pays off when you do **multiple O(1) lookups by key** after loading.
- `ToListAsync` → multiple `.First(p => p.Id == id)` calls = O(n) per lookup → O(n²) total for a loop.
- For a 10-item cart: 10 product lookups → `ToDictionary` is the correct tool.

```csharp
// Load once, lookup O(1) per item
var productMap = await _context.Products
    .Where(p => productIds.Contains(p.Id))
    .ToDictionaryAsync(p => p.Id);

foreach (var item in cart.Items)
{
    if (!productMap.TryGetValue(item.ProductId, out var product))
        throw new ProductNotFoundException(item.ProductId);
    // O(1) — constant time regardless of catalogue size
}
```

- For a single lookup or a sequential `foreach` with no multi-key access, `ToList` is simpler.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Always `ToList`." Fine for simple cases, but in a loop with n items × m lookups it becomes O(n×m).

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Noting that `ToDictionaryAsync` throws `ArgumentException` on duplicate keys — safe here because `Product.Id` is a PK, but worth remembering for non-PK projections.

</details>

---

**JQ2.** [HIGH] Angular `@for (item of items; track item.id)` — why is `track` required in Angular 17+?

<details>
<summary>The 5% Answer</summary>

- `track` provides a **unique identity key** for the DOM differ. Angular reuses existing DOM nodes for items whose key is unchanged, rather than destroying and recreating them.
- Without `track` (or with `track $index`), any list mutation (add/remove/reorder) destroys all DOM nodes and recreates from scratch — expensive and loses focus/animation state.
- `track item.id` is the standard for entity lists (PK as key).

```html
@for (item of cartItems(); track item.productId) {
  <app-cart-item [item]="item" />
}
```

- `track $index`: use only for static lists or when items have no stable identity.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Track is optional." In Angular 17+, omitting `track` causes a compile warning and falls back to `$index` — which thrashes the DOM on reorder.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing that `track` expression is evaluated for each item to produce a value compared by `===`. If `item.id` is a number, primitive equality is fine. If it's an object, you'd need to project a primitive (e.g., `track item.id.toString()` — but avoid this: use a proper PK).

</details>

---

**JQ3.** [HIGH] C# `IEnumerable<T>` vs `IQueryable<T>` — what happens when you call `.Where()` on each in an EF Core context?

<details>
<summary>The 5% Answer</summary>

- **`IQueryable<T>.Where()`**: builds an **expression tree** translated to SQL — filter runs in the database. Only matching rows are transferred.
- **`IEnumerable<T>.Where()`**: runs the predicate **in-memory** on every element — all rows are loaded from DB first.
- Accidentally calling `.AsEnumerable()` or `.ToList()` before `.Where()` is the classic "loads entire table" bug.

```csharp
// Correct — SQL: WHERE Status = 'Pending'
var pending = await _context.Orders
    .Where(o => o.Status == OrderStatus.Pending)
    .ToListAsync();

// WRONG — loads ALL orders, filters in .NET memory
var pending = await _context.Orders
    .ToListAsync()      // ← entire table loaded here
    .Where(o => o.Status == OrderStatus.Pending);
```

- The `IQueryable` chain must stay in LINQ-translatable form until `ToList`/`FirstOrDefault`/`CountAsync` materialises it.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "They're interchangeable." The performance difference on a 1M-row table is the difference between 1ms and 10s.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Mentioning that `EF.Functions.Like(o.BuyerEmail, "%@gmail.com")` and other `EF.Functions.*` calls are ONLY valid on `IQueryable` — they translate to DB functions and throw on `IEnumerable`. Knowing this boundary prevents runtime errors.

</details>

---
