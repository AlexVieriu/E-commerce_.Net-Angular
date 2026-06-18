## SECTION K — BEHAVIORAL & DECISION-MAKING

> **Source:** Grounded scenarios tied to the actual Sprint 2 tasks. Technical + decision-making.
> For each question: the 1% answer walks through (1) what you noticed (2) how you confirmed the cause (3) what you changed (4) what you'd do differently.

---

### BEHAVIORAL

**K1.** [HIGH] Tell me about a race condition you solved in production.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer (Task 13 grounded):**

**What I noticed:**
In our checkout flow, we were occasionally oversellling popular products — stock counts going negative in the DB. Low-traffic days were fine; flash-sale style promos produced 2-5 negative-stock incidents.

**How I confirmed the cause:**
I read the logs around the incidents and found that two `CreateOrderAsync` calls for the same product had `SaveChangesAsync` completing within 50ms of each other. Both had passed the `QuantityInStock >= qty` check against a stock of 1. The check was correct in isolation — the race was between the read and the save.

I reproduced it in a test by creating two `DbContext` instances, calling `FindAsync` on both before either saved, then racing the saves.

**What I changed:**
Added `[ConcurrencyCheck]` on `Product.QuantityInStock`. EF now emits `UPDATE ... WHERE Id = @id AND QuantityInStock = @original`. The loser's UPDATE affects 0 rows → `DbUpdateConcurrencyException`.

I caught the exception in `OrderService`, rethrew as `InvalidOperationException` with a user-friendly message, and verified the frontend interceptor showed the snackbar properly.

I considered the other three resolution strategies (Client Wins, Store Wins, Merge) and explicitly chose Fail-and-Notify — you can't silently accept server state for a payment the user is about to confirm. Documented this in the task notes.

**What I'd do differently:**
For a flash-sale scale, optimistic concurrency would thrash (30%+ conflict rate). I'd pre-gate with a Redis atomic DECR before hitting SQL, so SQL sees only pre-approved requests. For this project's scale, optimistic is the right fit.


</details>

---

<details>
<summary>Red Flag</summary>

**Red Flag:**
"I added a transaction." Doesn't fix the race (see J6). Shows shallow understanding.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Naming the four resolution strategies and explicitly justifying why Fail-and-Notify is the only one that preserves inventory integrity for a paid-flow.


</details>

---

**K2.** [HIGH] You were asked to add a transaction boundary but chose not to. Walk me through how you pushed back.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer (Task 14 grounded):**

**The request:** a reviewer asked me to wrap `CreateOrderAsync` in `BeginTransactionAsync()` for atomicity.

**My response:**
I read EF Core's source for `SaveChanges` internals and confirmed it wraps every call in an implicit transaction. All my writes — stock decrement, order insert, order items insert — go through one `unitOfWork.Complete()` call, which is one `SaveChangesAsync`. EF batches them into a single SQL transaction. Atomicity is already guaranteed.

I wrote up the reasoning in a short decision doc:
- What implicit transactions guarantee.
- When explicit transactions are actually needed (multiple SaveChanges, raw SQL, cross-context, non-default isolation).
- Why adding it here is redundant ceremony — it would mislead future readers into thinking the method is unsafe without it.

I sent that doc in the PR conversation and asked: "do any of the four explicit-transaction cases apply here?" The reviewer agreed they didn't.

**What I'd do differently:**
I'd add a short comment in the method pointing to the decision doc, so the next reviewer doesn't re-raise the same concern. Documentation that pre-empts questions is cheap; repeating conversations is expensive.


</details>

---

<details>
<summary>Red Flag</summary>

**Red Flag:**
"I added it because they asked." Senior-level work requires pushing back with data when the request is wrong. Capitulation to "looks safer" adds long-term debt.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the implicit transaction guarantee is load-bearing — future changes that introduce a second `SaveChangesAsync` MUST be caught, because they'd silently break atomicity. I'd add a lint rule or test that flags multiple `SaveChangesAsync` calls in one service method.


</details>

---

**K3.** [HIGH] A payment system had duplicate charges. Walk me through how you'd debug.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer (Tasks 16 + 18 grounded):**

**Step 1 — confirm the scope.**
How many duplicates? Same user or different users? Same product or broad? Any pattern in timing (burst vs steady)?

For this project's design, "duplicate charges" would show as:
- Two PaymentIntents for one cart on Stripe (Task 16 failure mode).
- Two Orders for one PaymentIntent in SQL (Task 18 failure mode).

**Step 2 — identify which layer.**

**If duplicate PaymentIntents (Stripe):**
- Check `IdempotencyKey` usage on `CreateAsync`. Missing? Changing between retries? Both are red flags.
- Look for retry wrappers that increment the key per attempt (classic bug, see D9).
- Correlate with network blip logs on `POST /api/payments/{cartId}`.

**If duplicate Orders (SQL):**
- Check for the Task 18 app-level check. Missing?
- Check for a unique index on `PaymentIntentId`. Missing? Name `IX_Orders_PaymentIntentId`.
- If both present, investigate the TOCTOU window — are there truly-concurrent double-clicks?

**Step 3 — reproduce.**
Write a test that double-invokes `CreateOrderAsync` with the same cart. Assert `second.Id == first.Id` and no duplicate row in DB.

**Step 4 — fix + backfill.**
- Deploy the fix (idempotency key or app-check or unique index).
- Run a one-off query to find existing duplicates: `SELECT PaymentIntentId, COUNT(*) FROM Orders GROUP BY PaymentIntentId HAVING COUNT(*) > 1`.
- Reconcile: keep the first, cancel + refund the duplicate, notify affected users.
- Backfill data migration: the reconciliation query should be idempotent (re-runnable safely).

**Step 5 — add monitoring.**
Alert on any future occurrence of `COUNT(*) > 1` grouped by PaymentIntentId. Catch regressions fast.

**What I'd do differently:**
I'd set up the monitoring BEFORE the bug — duplicate detection is a basic invariant to guard. Adding it reactively is fine but preventive is better.


</details>

---

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Add a unique index." Half the answer. Without the app-level check, the unique index turns duplicates into 500s, which is a worse UX than deduplication.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning reconciliation with Stripe — if SQL and Stripe disagree about the state of `pi_abc`, the reconciliation is a **scripted daily job**, not a firefight. Production payment systems run these continuously.


</details>

---

**K4.** [HIGH] You discovered a test was passing but proved nothing. What did you do?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer (Task 19 grounded):**

**What I noticed:**
Running code coverage, I saw `SetCartAsync` in `CartService` was marked covered — meaning a test exercised it. But when I deleted the `SetCartAsync` body and ran tests, nothing failed. Coverage showed green; behaviour wasn't actually tested.

**How I confirmed:**
Read the test. The setup used `It.IsAny<TimeSpan?>()` and `It.IsAny<When>()` — old StackExchange.Redis overload signatures. Production code used the new `Expiration` / `ValueCondition` overloads.

Moq couldn't match the setup; it returned `false` as the default. The method returned null; the assertion `result.Should().BeNull()` passed.

**What I changed:**
1. Updated the setup to match the new overload signatures.
2. Added `Verify(..., Times.Once)` so the test would fail if the mock was never invoked.
3. Wrote a positive-path test for `SetCartAsync` that captures the serialised payload via `Callback` and asserts both the key and TTL are correct.
4. Audited the other `CartService` tests for the same pattern — found and fixed one more.

**What I'd do differently:**
I'd propose a **CI rule: every test must include at least one `Verify` for each mock it sets up**, or it fails. This is unusual but would prevent an entire class of silent-pass bugs. Short of that, code review checklist item.

I also started using **mutation testing (Stryker)** on the critical services. It runs intentional mutations and reports tests that don't detect them. That's the gold standard for test-quality confidence.


</details>

---

<details>
<summary>Red Flag</summary>

**Red Flag:**
"I added a test." Adding more tests with the same broken pattern is worse than having no tests — it's comforting wrongness.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning mutation testing specifically, and acknowledging that coverage % is a proxy, not a goal. Senior-level framing.


</details>

---

**K5.** [MEDIUM] A senior reviewer asked why your CSP includes `unsafe-inline`. How did you defend it?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer (Task 9 grounded):**

**The pushback:** "unsafe-inline defeats CSP. Remove it."

**My response:**
I walked them through the Angular Material pipeline:
1. Material uses `ViewEncapsulation.None` — dumps its styles as plain `<style>` tags into `<head>` at runtime via JavaScript.
2. The original HTML has no `<style>` tags to hash at build time.
3. There's no server-side nonce because this is a client-side SPA — no server to generate per-request nonces.
4. Without `'unsafe-inline'` on `style-src`, Material-rendered UI breaks silently — buttons, inputs, cards render unstyled.

I showed them the alternatives:
- **Nonce-based CSP via Angular SSR** — real solution, but requires SSR infra and a renderer patch. Non-trivial for a CV project.
- **Hash-based CSP** — precompute hashes of every injected style, rebuild on every Material update. Massive maintenance cost.
- **Strict-dynamic (script-src only)** — doesn't apply to styles.

I explained `unsafe-inline` on `style-src` is a **different risk class** than on `script-src`. Style-based XSS can exfiltrate data via CSS attribute selectors but can't execute code. For an e-commerce site without high-sensitivity data beyond session cookies (already protected by HttpOnly/SameSite), the risk-adjusted cost of eliminating `unsafe-inline` on styles isn't justified.

I documented this tradeoff in the task notes, so anyone revisiting the CSP knows:
- It's intentional, not oversight.
- The production-grade fix is nonce-based via SSR.
- Reconsider when SSR becomes part of the roadmap.

**The reviewer accepted it.**

**What I'd do differently:**
I'd have included the documented tradeoff in the PR description BEFORE the reviewer raised it. Pre-empting the predictable objection builds trust.


</details>

---

<details>
<summary>Red Flag</summary>

**Red Flag:**
"I just used `unsafe-inline` because Material needs it." Parroting without understanding. Real seniors want the mechanism (runtime injection), the alternatives, and the risk-adjusted decision.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Distinguishing between `unsafe-inline` on `script-src` (major hole) vs `style-src` (smaller hole). Risk stratification is senior thinking.


</details>

---

**K6.** [HIGH] Walk me through a time you designed for an edge case that seemed unlikely but turned out to matter.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer (Task 17 grounded):**

**The situation:**
During Sprint 2, I was implementing price-change detection. The obvious path: detect, throw 409, redirect. Done.

A subtler case: what happens when the user retries after the 409?

Most engineers would assume "same as a fresh checkout — create payment intent, pay." But I traced through the Stripe SDK:
1. `createOrUpdatePaymentIntent` uses `cart.Id` as the idempotency key.
2. After a price-change rejection, the cart is updated in Redis at the new price.
3. The retry calls `CreateAsync` with `cart.Id` as the key — **but the amount is now different**.
4. Stripe returns: "Keys for idempotent requests can only be used with the same parameters they were first used with."
5. 500 error. User stuck.

**What I did:**
1. Reproduced it manually — changed DB price between checkout steps, clicked Pay, saw 409, clicked checkout again, got 500.
2. Changed the idempotency key from `cart.Id` to `cart.Id + Guid.NewGuid()`. The GUID ensures uniqueness per creation attempt; the cart.Id prefix preserves traceability in Stripe logs.
3. Also cleared `PaymentIntentId` and `ClientSecret` from the cart in the 409 path, so `createOrUpdatePaymentIntent` takes the CREATE branch, not UPDATE (updating a succeeded intent also fails).

**Why it mattered:**
The flow between 409 and retry is invisible in happy-path testing. Without this fix, every price-change rejection locks the user into a 500 error — worse than the original bug it was meant to solve.

**What I'd do differently:**
I'd write the retry-after-409 test **before** the 409 detection feature. TDD'd edge cases surface these traps early. My task 17 doc notes the retry test as a Sprint 5 follow-up — next sprint, I'd promote it to "must-have."


</details>

---

<details>
<summary>Red Flag</summary>

**Red Flag:**
"I added retry support." Doesn't name the specific Stripe error, the idempotency key interaction, or the PaymentIntent status constraint.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Showing that Task 17's fix forced a change to Task 16's idempotency key design. Recognising coupling between tasks shows architectural awareness, not feature-by-feature thinking.


</details>

---

**K7.** [HIGH] Tell me about a bug that only appeared under a specific runtime mode (prod vs dev, zone vs zoneless, AOT vs JIT, server vs client). How did you find and fix it?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer (Bug 1 grounded):**

**What I noticed:**
On the checkout page, the "Confirm" button stayed disabled even after the user filled in their address. The `completionStatus` signal was supposed to enable it when `address === true`. Clicking another random field — a voucher input — "fixed" it.

**The first trap I avoided:**
I didn't trust the "clicking another field fixed it" observation. That's **confirmation bias territory** — the bug isn't gone, something unrelated happened to trigger the fix. I resisted shipping a workaround (adding a dummy field tap) and dug for root cause.

**How I confirmed the cause:**

1. I added `effect(() => console.log('status:', completionStatus()))` in the component. The effect fired on the first signal set (initial state) but NOT on subsequent updates. Conclusion: signal's equality check was returning "no change."

2. I looked at the update code:
   ```typescript
   this.completionStatus.update(state => {
       state.address = true;
       return state;
   });
   ```
   The mutation returns the same object reference. Signal's `===` check sees no change → no notification → no re-render.

3. I confirmed by testing the fix — swap to `({ ...state, address: true })` — and the effect fired, button enabled immediately. Root cause confirmed.

**Why this bug is mode-specific:**

This code would have worked in **zone-based Angular** (pre-v18 default). Zone.js monkey-patches every async event handler; after the click that triggered the update, the zone forced change detection, and the template re-evaluated `completionStatus().address`. The mutation was picked up incidentally by the zone sweep, not by the signal system.

This project is **zoneless** (`provideZonelessChangeDetection()`). Zoneless relies entirely on signal notifications. The same code that "worked" in zone silently breaks in zoneless.

**What I changed:**

1. **The immediate fix:** replace the three `state.x = true; return state;` patterns in the checkout component with `({ ...state, x: true })`.
2. **A nested-state audit:** searched the codebase for `signal.update(s => { s... = ...; return s; })` patterns — found two more in unrelated components. Fixed all.
3. **A lint rule:** proposed an ESLint rule to flag `update` callbacks that return the same parameter name they received (`update(s => ... return s)`). Catches the pattern mechanically.
4. **A doc note:** added a short "zoneless gotchas" page to the frontend README, with the memory diagram and the fix pattern. Future devs hit the same issue with context.

**What I'd do differently:**

I'd have written a **test that specifically verifies template re-rendering on signal update** — not just "state is correct," which the broken code passes. In zoneless, tests need to exercise change propagation explicitly. Our existing component tests were all "state correctness" tests; they'd have missed this bug forever.

The broader lesson: **runtime-mode bugs are invisible until you migrate.** Zone → zoneless is the current one; AOT vs JIT exposed template-binding issues historically; SSR vs CSR exposes hydration mismatches. Any time the team makes a runtime-mode change, expect a wave of latent bugs — budget for a stabilisation sprint.


</details>

---

<details>
<summary>Red Flag</summary>

**Red Flag:**
"I just cloned the object." Doesn't explain why zoneless differs from zone, doesn't explain why the fix works, doesn't explain how you found it. The fix is trivial; the diagnosis is the senior-level part.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Naming **zone.js vs zoneless** explicitly as the runtime-mode distinction, and connecting this bug to a **class of bugs that migration surfaces**. Seniors think in categories, not incidents.


</details>

---
