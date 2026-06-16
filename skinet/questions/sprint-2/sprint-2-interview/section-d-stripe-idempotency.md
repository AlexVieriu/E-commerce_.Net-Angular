## SECTION D — STRIPE IDEMPOTENCY KEYS

> **Source:** Task 16 — `IdempotencyKey = cart.Id` on `PaymentIntentService.CreateAsync`.

---

### EASY

**D1.** [HIGH] What is an idempotency key and what guarantee does Stripe give for it?

<details>
<summary>The 5% Answer</summary>

- A unique client-chosen string attached to an API request
- Stripe caches the **response** keyed by `(API key, idempotency key)` pair for **24 hours**
- A repeat request with the same key returns the cached response — no new resource is created
- Passed via the `Idempotency-Key` HTTP header (the Stripe SDK's `RequestOptions.IdempotencyKey` just sets this header)


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"It makes the request unique." No — Stripe already gives every PaymentIntent a unique ID (`pi_abc123`). The key is about **deduplication across retries**, not uniqueness.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the 24-hour window matters — after it expires, a request with the same key creates a new resource. That's usually fine because the old intent would have expired/succeeded by then, but worth knowing.


</details>

---

**D2.** [MEDIUM] Why is `cart.Id` the right key for this project?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**
- **Stable** — the cart ID doesn't change across retries within a session.
- **Unique per logical operation** — one cart should produce exactly one PaymentIntent.
- **Already in scope** — no extra state to manage.
- **Meaningful in Stripe logs** — searching Stripe's dashboard by cart ID maps directly to your app's cart.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"`Guid.NewGuid()`." A fresh GUID on every request defeats the purpose — Stripe can never deduplicate.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `cart.Id` breaks down after a **price-change rejection** (Task 17), where you need a unique key per *attempt*, not per cart — which is why Task 17 uses `cart.Id + Guid.NewGuid()`.


</details>

---

### MEDIUM

**D3.** [HIGH] Describe the "network blip" scenario without an idempotency key. What orphan state is created?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

```
Client → POST /api/payments/cart-999
  Server → Stripe.CreateAsync(amount=$341, no key)
    Stripe: creates pi_abc123, status="requires_payment_method"
    Stripe → server: 200 OK + pi_abc123
    [network drops the response on the way back]
  Server times out waiting
Client: "retry" button
Client → POST /api/payments/cart-999
  Server → Stripe.CreateAsync(amount=$341, no key)
    Stripe: sees new request → creates pi_xyz456
    Server stores pi_xyz456 in Redis cart
```

Result: pi_abc123 sits on Stripe forever, never referenced by any order. If the user somehow pays against it (cached client secret), you have a charge without an order. Support nightmare.

With `IdempotencyKey = "cart-999"`, the second request returns the cached `pi_abc123` — no orphan.

**Production consequence:** orphaned PaymentIntents accumulate as noise in Stripe's dashboard and can lead to reconciliation mismatches if they somehow get paid.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Stripe deduplicates automatically." False. Stripe only deduplicates when you tell it to, via the header.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that Stripe's documentation recommends idempotency keys on **all POST requests to resource-creation endpoints**, not just PaymentIntents. Refunds, Transfers, Customers — all should have keys in production.


</details>

---

**D4.** [MEDIUM] Why is the idempotency key only on `CreateAsync`, not `UpdateAsync`?

<details>
<summary>The 5% Answer</summary>

- **`UpdateAsync`** targets an **existing** resource by ID (`pi_abc123`)
  - Retrying an update just re-applies the same change — same resource, no duplicate creation risk
  - Already idempotent by nature; no key needed

- **`CreateAsync`** has no target yet — "give me a new resource"
  - Without a key, each call creates a fresh resource
  - Stripe needs the key to recognise "this is a retry, not a new creation"


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Updates don't need idempotency." Partially true, but the reason matters — it's because they target a specific resource ID, not because updates are magically safe.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **amount changes on an update** still need care — if you update the amount and retry, Stripe's idempotency of updates is scoped to the same parameters. Changing parameters mid-retry is a code smell.


</details>

---

**D5.** [MEDIUM] [OUT-OF-SCOPE] What is the scope and TTL of a Stripe idempotency key?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

- **Scope:** per `(API key, endpoint)` — a key used on `/v1/payment_intents` is independent from the same key on `/v1/refunds`.
- **TTL:** 24 hours after the first request. After that, the cache entry is purged; a request with the same key creates a fresh resource.
- **Same key, different body:** returns an error (`Keys for idempotent requests can only be used with the same parameters they were first used with`). This is exactly the conflict Task 17 hit when re-using `cart.Id` with a new amount.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Forever." Stripe's cache is 24h, and that's in the public docs.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the 24h TTL is a soft contract — in practice you should treat it as "the key is useless after a few minutes." Designing around the 24h window is fragile.


</details>

---

**D6.** [HIGH] Two browser tabs call `POST /api/payments/{cartId}` simultaneously for the same cart. Walk through what happens with and without the key.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Without key:**
- Tab A: server calls Stripe → creates `pi_abc123`, stores it in Redis under `cart-999`.
- Tab B: server calls Stripe → creates `pi_xyz456`, stores it in Redis under `cart-999` **overwriting** `pi_abc123`.
- Redis now points to `pi_xyz456`; `pi_abc123` is orphaned.
- If the user pays on Tab A's form, the ClientSecret points to `pi_abc123` — charge happens but the cart holds `pi_xyz456` → webhook can't find the order.

**With key (`cart.Id`):**
- Tab A: creates `pi_abc123`, stored in Redis.
- Tab B: Stripe sees key already processed → returns `pi_abc123` again.
- Both tabs hold the same PaymentIntent. User pays on either — webhook finds the one order. Consistent state.

**Production consequence:** without the key, multi-tab users create orphaned intents + Redis-Stripe state mismatches. Frustrating to diagnose because Redis "looks right."


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"The second tab fails." It doesn't — both succeed. Both write to Stripe. Both overwrite Redis. The silent failure is the hard part.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Pointing out that the right architectural response to multi-tab is **pessimistic locking on the cart** (a distributed lock with a short TTL) — but Task 16 chose the lighter-weight idempotency-key approach because it covers both multi-tab and network-blip with one mechanism.


</details>

---

### HARD

**D7.** [HIGH] After a Task 17 price-change rejection, you clear `PaymentIntentId` and retry with `cart.Id` as the key. Stripe returns an error. What error, why, and what's the fix?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Error:**
```
Keys for idempotent requests can only be used with the same parameters they were first used with.
Current: { amount: 7500 }  Original: { amount: 5000 }
```

**Why:** Stripe's idempotency cache for `cart-999` stores the **parameters** of the first request, not just the response. The first call used `amount=5000` (old price $50). The retry uses `amount=7500` (new price $75). Same key, different body → Stripe refuses.

**Fix:** make the key unique per *creation attempt*, not per cart:

```csharp
new RequestOptions
{
    IdempotencyKey = $"{cart.Id}-{Guid.NewGuid()}"
}
```

Why the composite:
- `cart.Id` prefix — preserves traceability in Stripe logs (search by cart ID still works).
- `Guid.NewGuid()` suffix — guarantees uniqueness per creation attempt.

This loses the network-blip protection **within a single attempt**, but the retry is the new attempt. The idempotency semantics move from "cart → one intent ever" to "creation attempt → one intent at most."

**Production consequence:** without this fix, every price-change rejection creates a 500 at Stripe and the user sees "Payment system unavailable" for a retry that should work cleanly.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Just remove the key." Now you have no protection against network blips on the retry itself.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that a stricter design would **store the idempotency key alongside the cart state** — key v1 for first attempt, key v2 after rejection. That way, within any single attempt you still get idempotency; across attempts you get freshness. More code, more guarantees.


</details>

---

**D8.** [MEDIUM] [OUT-OF-SCOPE] A colleague argues: "Stripe already gives PaymentIntents unique IDs. We don't need idempotency." Refute this rigorously.

<details>
<summary>The 5% Answer</summary>

- The argument conflates **uniqueness** with **idempotency** — they solve different problems

- **Uniqueness** (Stripe's default): two PaymentIntents never have the same ID — two calls produce `pi_abc123` and `pi_xyz456`, both unique, both real, both billable
- **Idempotency** (opt-in via key): the same logical operation doesn't produce two resources — the second call returns `pi_abc123` again, no duplicate

- Uniqueness protects **Stripe** from ID collisions. Idempotency protects **you** from duplicate work.

- **Concrete proof the argument fails:** the network-blip scenario creates two uniquely-IDed PaymentIntents for one cart — uniqueness is satisfied, but the business is broken: one cart has two payment attempts, one orphaned


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Just repeating "uniqueness and idempotency are different." Interviewers want the concrete scenario that shows why uniqueness fails without idempotency.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Extending the argument to your own DB: your `Orders` table has unique IDs (autoincrement or GUID), yet Task 18 adds a unique index on `PaymentIntentId` as a second guard. Uniqueness of primary keys doesn't protect against duplicate business entities. The principle generalises.


</details>

---

**D9.** [HIGH] [OUT-OF-SCOPE] Find the bug in this retry wrapper:

```csharp
public async Task<PaymentIntent> CreateIntentWithRetry(Cart cart, long amount)
{
    for (int attempt = 0; attempt < 3; attempt++)
    {
        try
        {
            var options = new PaymentIntentCreateOptions { Amount = amount };
            var requestOptions = new RequestOptions
            {
                IdempotencyKey = $"{cart.Id}-{attempt}"
            };
            return await _paymentIntentService.CreateAsync(options, requestOptions);
        }
        catch (StripeException ex) when (attempt < 2)
        {
            await Task.Delay(1000 * (attempt + 1));
        }
    }
    throw new Exception("Failed after 3 attempts");
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**The bug:** the idempotency key changes on every attempt (`{cart.Id}-0`, `{cart.Id}-1`, `{cart.Id}-2`). That **defeats the purpose of idempotency**:

- Attempt 0: key `cart-999-0`, network blip, Stripe created intent but response lost.
- Attempt 1: key `cart-999-1`, Stripe has no cached response for this key → creates a second intent.
- Attempt 2: same thing — third intent.

Result: up to 3 orphaned PaymentIntents for one cart, one of which may have been successfully created on Stripe's side.

**Fix:** use the **same key across all retries within a single logical attempt**:

```csharp
var key = $"{cart.Id}-{Guid.NewGuid()}";  // stable for this invocation
var requestOptions = new RequestOptions { IdempotencyKey = key };

for (int attempt = 0; attempt < 3; attempt++)
{
    try
    {
        return await _paymentIntentService.CreateAsync(options, requestOptions);
    }
    catch (StripeException ex) when (attempt < 2 && IsRetryable(ex))
    {
        await Task.Delay(1000 * (attempt + 1));
    }
}
```

Now the 3 retries all hit the same cached response → no orphans.

**Also:** `catch (StripeException ex)` with no filter retries on 4xx errors too (invalid card, insufficient funds) — those should NOT be retried. Add an `IsRetryable` check that only retries 5xx / network errors / rate-limit 429s.

**Production consequence:** the original code produces more orphans the worse the network is. Retries become the problem, not the solution.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Fixing only the 4xx retry filter without the key bug. The key bug is the headline issue.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that Stripe SDK's `StripeClient` has built-in retry logic (via `StripeClient.RetryConfiguration`) that already handles key-reuse correctly — custom retry code like this is usually unnecessary and error-prone.


</details>

---

### ⚡ Syntax Quick-Fire

**DQ1.** [HIGH] C# string: `$"{cart.Id}-{Guid.NewGuid()}"` — when would you switch to `StringBuilder`?

<details>
<summary>The 5% Answer</summary>

- Interpolation (`$"..."`) compiles to `DefaultInterpolatedStringHandler` in C# 10+ — zero allocation for short, known-count concatenations.
- For **a loop concatenating an unknown number of strings**, `StringBuilder` avoids O(n²) string copies:

```csharp
// Bad — O(n²) allocations
foreach (var item in items) result += item.Name + ", ";

// Good — O(n) with StringBuilder
var sb = new StringBuilder();
foreach (var item in items) sb.Append(item.Name).Append(", ");
var result = sb.ToString();
```

- For 1–5 known concatenations, interpolation is cleaner and equally fast.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Always use `StringBuilder`." For 2-3 strings it's 3x more verbose with no performance benefit.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** `string.Join(", ", items.Select(i => i.Name))` — idiomatic single-call join, internally uses `StringBuilder`, no manual loop needed.

</details>

---

**DQ2.** [HIGH] `Guid.NewGuid()` (v4, random) as a primary key — what DB problem does it cause?

<details>
<summary>The 5% Answer</summary>

- Random GUIDs cause **B-tree index fragmentation** — each INSERT lands at a random position, forcing page splits and leaving pages half-full.
- **Fix**: `Guid.CreateVersion7()` (.NET 9) generates sequential UUIDs — time-ordered, naturally append to the end of the B-tree. No page splits. Same uniqueness guarantees.
- SQL Server alternative: `NEWSEQUENTIALID()` for clustered primary keys.
- **Rule**: use sequential GUIDs for clustered/unique indexed columns. Random GUIDs are fine for non-indexed identifiers (cart IDs, session tokens).

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "GUIDs are fine." True for correctness; wrong for performance on indexed PK columns at scale.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing that EF Core 8+ uses `Guid.CreateVersion7()` by default for GUID PKs when configured with `UseGuidVersion7()`. Knowing the EF roadmap shows framework-level awareness.

</details>

---

**DQ3.** [HIGH] `await Task.Delay(1000)` vs `Thread.Sleep(1000)` — what happens to throughput under load?

<details>
<summary>The 5% Answer</summary>

- `Thread.Sleep` **blocks the thread** for 1 second — the thread pool loses one worker, all requests queued to it wait.
- `await Task.Delay` **yields the thread** back to the pool during the delay — the thread processes other requests; a continuation resumes after 1 second on any available thread.
- Under 100 concurrent requests with `Thread.Sleep(1000)`, you need 100 threads. With `Task.Delay`, a handful of threads handles all 100.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "They're the same — both wait 1 second." One starves the thread pool; the other doesn't.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Mentioning that `Thread.Sleep` in ASP.NET Core is a red flag in code review — it always means "someone doesn't understand async." The only legitimate use is in `Main()` or a console app keeping alive for testing.

</details>

---
