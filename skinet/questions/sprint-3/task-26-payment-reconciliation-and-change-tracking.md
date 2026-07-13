# Task 26 — Payment Reconciliation & EF Change Tracking

## Why this approach

- Original framing ("admin sets any `OrderStatus` via a dropdown") rejected on review — payment status
  must be Stripe-sourced only, never admin-entered. Rebuilt as a reconciliation endpoint instead.
- One rule, two triggers: the webhook already has the paid amount (Stripe pushes it); the admin sync
  endpoint doesn't, so it pulls the `PaymentIntent` from Stripe first, then reuses the exact same rule.
- Match/mismatch/save logic extracted from `PaymentsController.HandlePaymentIntentSucceeded` into
  `IPaymentService.ReconcileOrderPayment` — one place, no risk of the webhook and the admin path
  silently disagreeing on the rule.
- SignalR stayed in the API layer (both controllers), not moved into `Infrastructure` alongside the
  shared method — `API` already references `Infrastructure`; the reverse would be circular.
- `Refunded` orders excluded from sync — a stale Stripe read must never overwrite money that already
  moved via the dedicated refund endpoint.

## What confused me — actual gaps from the cold test

| Gap | Location | Tier |
|---|---|---|
| Why one shared method instead of two independent ones that "check each other" | `PaymentService.cs · ReconcileOrderPayment` | **OWN** |
| Why the SignalR push couldn't move into `Infrastructure` with the rest of the logic | `PaymentsController.cs` / `AdminController.cs` vs `PaymentService.cs` | **OWN** |
| How does `order.Status = X` get saved with no explicit "mark as modified" call | `GenericRepository.cs · GetEntityWithSpec`, `UnitOfWork.cs · Complete` | **OWN** |
| Why passing `order` through method calls is enough — no `return order;` needed | reference-type semantics (C# fundamental, not EF-specific) | **RECOGNIZE** |
| When `GenericRepository.Update()` (`Attach` + `State = Modified`) is actually needed | `GenericRepository.cs · Update` | **RECOGNIZE** |
| Trace the missing-`Refunded`-guard failure mode to the actual wrong DB state (took 3 attempts — kept conflating this with the separate refund-money-movement endpoint) | `AdminController.cs · SyncPaymentStatus` | **OWN** |

## Explanation

### The design: one rule, two triggers

```csharp
// PaymentService.cs — the one shared rule
public async Task ReconcileOrderPayment(Order order, long paidAmountInCents)
{
    var orderTotalInCents = (long)Math.Round(order.GetTotal() * 100, MidpointRounding.AwayFromZero);

    order.Status = orderTotalInCents != paidAmountInCents
        ? OrderStatus.PaymentMismatch
        : OrderStatus.PaymentReceived;

    await _unitOfWork.Complete();
}

// Admin-triggered recovery path — pulls the amount, then reuses the rule above
public async Task SyncOrderPaymentStatus(Order order)
{
    var intentService = new PaymentIntentService();
    var intent = await intentService.GetAsync(order.PaymentIntentId);

    await ReconcileOrderPayment(order, intent.Amount);
}
```

- **Webhook** = push trigger. Stripe hands the `PaymentIntent` straight to `HandlePaymentIntentSucceeded`
  — it already has `intent.Amount`, so it calls `ReconcileOrderPayment` directly.
- **Admin sync** = pull trigger. It has no amount yet, so its entire job is: go fetch it
  (`PaymentIntentService.GetAsync`), then hand it to the same shared method.
- Merging these into one method would mean branching on "do I already have the amount or not" inside a
  single method, or forcing the webhook to make a pointless extra Stripe call just so both callers look
  alike. Splitting separates *how you obtain the number* (differs per caller) from *what you do once you
  have it* (identical rule, one place).

### Why SignalR had to stay in the API layer

Project reference direction: `API` → `Infrastructure` (API already depends on Infrastructure).
`NotificationHub` and `IHubContext<NotificationHub>` live in `API`. Moving the SignalR push into
`PaymentService` (which lives in `Infrastructure`) would require `Infrastructure` to reference `API` —
the reverse direction — which is a circular project reference and simply won't compile. So the shared
method only owns the match/mismatch/save decision; the SignalR push stays duplicated (3 trivial lines)
in each controller, calling the shared method for the part that actually needed sharing.

### EF Core change tracking — where the "wiring" actually is

```csharp
// GenericRepository.cs
public async Task<T?> GetEntityWithSpec(ISpecification<T> spec)
    => await ApplySpecification(spec).FirstOrDefaultAsync();   // context.Set<T>() — tracked by default
```

- Querying through `context.Set<T>()` (no `.AsNoTracking()` anywhere in this codebase's read path) is
  **tracked by default**. The moment EF materializes the row, it registers that exact object in the
  `ChangeTracker` and snapshots its property values as they were at load time.
- `UnitOfWork(StoreContext context)` passes that *same* `context` into every repository it creates
  (`Activator.CreateInstance(repositoryType, context)`), and `StoreContext` is registered `AddScoped` —
  one instance per HTTP request. So the repository that fetched `order` and the `UnitOfWork` that later
  saves it share the identical `ChangeTracker`.
- `unitOfWork.Complete()` → `context.SaveChangesAsync()`. The first thing `SaveChangesAsync` does is
  call `ChangeTracker.DetectChanges()`: it walks every tracked entity and diffs its **current** property
  values (read live off the object, right now) against the **original snapshot** from load time. Any
  difference marks that property (and the entity) `Modified`; EF generates an `UPDATE` for only the
  changed columns.
- **Gotcha worth knowing:** if you set a property and then set it back to its original value before
  `SaveChanges()`, `DetectChanges()` sees no net difference — no `UPDATE` for that column at all. It's a
  before/after diff computed lazily on demand, not an event log of every assignment.
- `GenericRepository.Update()` (`Attach` + `State = Modified`) exists for the opposite case: a
  **detached** entity EF has never seen (e.g. built from a deserialized request body). `order` here is
  never detached — it came from a tracked query on the live context — so calling `.Update()` on it would
  be redundant, which is why neither `ReconcileOrderPayment` nor `RefundOrder` ever calls it.

### Reference semantics — why no `return order;` is needed

`Order` is a reference type. `AdminController` → `PaymentService.SyncOrderPaymentStatus(order)` →
`ReconcileOrderPayment(order, amount)` passes the *same* object at every step, not a copy — mutating
`.Status` inside `ReconcileOrderPayment` is visible to every caller already holding that reference, with
no reassignment needed. The real failure mode isn't "forgetting to pass the object back" — it's **two
separate fetches of the same row producing two different tracked objects with no shared reference**:
change one, save it, and the other's in-memory `.Status` is stale and disconnected. That's the actual EF
bug class this pattern avoids, as long as one fetched reference is threaded through the whole call.

### The missing-guard failure mode (BREAK)

```csharp
if (order.Status == OrderStatus.Refunded)
    return BadRequest("Refunded orders can't be re-synced");   // ← this line
```

If this guard didn't exist and `SyncPaymentStatus` ran against an already-`Refunded` order: `GetAsync`
returns the *original* `PaymentIntent` — refunding it earlier doesn't change its `.Amount` field, that
field is "what was originally charged," not "what remains." `order.GetTotal()` hasn't changed either.
Same number compared to itself → match → `order.Status` silently flips from `Refunded` back to
`PaymentReceived`. **No money moves anywhere in this path** — `SyncPaymentStatus` never calls Stripe's
refund API (`RefundService.CreateAsync`), only a read (`PaymentIntentService.GetAsync`). The bug is
entirely local: the DB now claims the order still needs handling as revenue, and
`DashboardService`'s revenue rule (`PaymentReceived` only) would count it again — a data-integrity bug,
not a money-movement one.

## Exercise

Without looking at the code:
1. Write `ReconcileOrderPayment`'s signature and body.
2. Write `SyncOrderPaymentStatus`'s signature and body.
3. In one paragraph, explain why `order.Status = X` gets persisted with no explicit "mark this
   modified" call anywhere in `ReconcileOrderPayment`.
4. Trace what happens if the `Refunded` guard is removed from `SyncPaymentStatus` — name the actual
   wrong `order.Status` value it ends up at, not just "it's bad."
5. Name the specific, concrete reason the SignalR push couldn't move into `PaymentService`.

## Solution

```csharp
public async Task ReconcileOrderPayment(Order order, long paidAmountInCents)
{
    var orderTotalInCents = (long)Math.Round(order.GetTotal() * 100, MidpointRounding.AwayFromZero);

    order.Status = orderTotalInCents != paidAmountInCents
        ? OrderStatus.PaymentMismatch
        : OrderStatus.PaymentReceived;

    await _unitOfWork.Complete();
}

public async Task SyncOrderPaymentStatus(Order order)
{
    var intentService = new PaymentIntentService();
    var intent = await intentService.GetAsync(order.PaymentIntentId);

    await ReconcileOrderPayment(order, intent.Amount);
}
```

3. `order` was fetched via `GetEntityWithSpec`, a tracked query on the request's scoped `StoreContext`.
   EF snapshotted its original values at load time. `SaveChangesAsync()` runs `DetectChanges()` first,
   which diffs the object's *current* live values against that snapshot and generates an `UPDATE` for
   whatever changed — no manual "mark modified" call needed because the object was already tracked.
4. `order.Status` ends up back at `OrderStatus.PaymentReceived` — the original charge amount and the
   order's own total are unchanged, so they still match, and the reconcile rule concludes "paid," erasing
   the fact that it was already refunded.
5. `API` already references `Infrastructure`; `NotificationHub`/`IHubContext<NotificationHub>` live in
   `API`. Moving the SignalR call into `PaymentService` (in `Infrastructure`) would need `Infrastructure`
   to reference `API` back — a circular project reference, which doesn't compile.

## Tests

`PaymentService_Tests.cs` — `ReconcileOrderPayment_AmountMatches_SetsPaymentReceived`,
`ReconcileOrderPayment_AmountMismatches_SetsPaymentMismatch`. Both mock only `IUnitOfWork`/`ICartService`
— no Stripe sandbox needed, because unlike the rest of `PaymentService` (`[ExcludeFromCodeCoverage]` at
the class level for exactly this reason), `ReconcileOrderPayment` makes no direct Stripe SDK call itself.

## Interview Q&A

**Why extract the match/mismatch rule into a shared method instead of two independent implementations?**
- Two independent copies of "how do we decide `PaymentReceived` vs `PaymentMismatch`" can silently drift
  if one gets edited (e.g. a rounding tolerance added) and the other doesn't
- One shared method means the webhook and the admin recovery path can never disagree on the rule

**Why couldn't the SignalR notification move into the `Infrastructure` project alongside the shared method?**
- `API` already has a project reference to `Infrastructure`
- `NotificationHub`/`IHubContext<NotificationHub>` live in `API`
- `Infrastructure` referencing `API` back would be a circular project reference — doesn't compile

**How does EF Core know to `UPDATE` a row when you just set a property on a C# object?**
- Entities loaded via a tracked query (no `.AsNoTracking()`) are registered in the `ChangeTracker` with
  an original-value snapshot at load time
- Setting a property is a plain in-memory assignment — no EF code runs on that line
- `SaveChangesAsync()` calls `ChangeTracker.DetectChanges()` first, which diffs current vs. original
  values and generates an `UPDATE` for whatever changed

**When is `GenericRepository.Update()` (`Attach` + `State = Modified`) actually necessary?**
- Only for a *detached* entity EF has never tracked — e.g. one built from a deserialized request body
- An entity fetched via `GetEntityWithSpec` on the same scoped `DbContext` is already tracked, so calling
  `.Update()` on it is redundant

**What's the actual bug if `SyncPaymentStatus` is called on an already-`Refunded` order with no guard?**
- No money moves — `SyncPaymentStatus` only calls `PaymentIntentService.GetAsync` (a read), never
  `RefundService.CreateAsync`
- The original `PaymentIntent.Amount` and `order.GetTotal()` are both unchanged since the refund, so they
  still match
- `order.Status` silently flips from `Refunded` back to `PaymentReceived` — a data-integrity bug that
  would also corrupt the dashboard's revenue total, which counts `PaymentReceived` orders

## Sources

- [Change Tracking - EF Core | Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/change-tracking/) — tracked vs. no-tracking queries, snapshot-based tracking
- [Change Detection and Notifications - EF Core | Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/change-tracking/change-detection) — `DetectChanges()` runs automatically before `SaveChanges()`; snapshot vs. live-value diffing
