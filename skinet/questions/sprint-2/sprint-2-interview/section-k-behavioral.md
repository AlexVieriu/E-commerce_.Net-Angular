## SECTION K — BEHAVIORAL & PROBLEM-SOLVING

**K1.** You join a project with 0% test coverage. You have one sprint to improve it. Where do you start? How do you decide what to test first? What coverage target is realistic?

<details><summary>Answer</summary>

**Where to start:** Core domain logic and the highest-risk business code — not utilities, DTOs, or CRUD. The code most likely to contain bugs and most expensive to break in production.

**Priority order:**
1. **Domain entities with logic** — `Order.GetTotal()`, `Order.CanBeCancelled()`, price calculations. Pure C#, no mocks, fastest to write.
2. **Critical service paths** — `OrderService.CreateOrderAsync` (stock check, price validation, order creation). Test happy path + stock out + price mismatch.
3. **Payment flow** — `PaymentService` (mock Stripe). Money bugs are catastrophic.
4. **Concurrency scenarios** — stock decrement race conditions. Use SQLite in-process, not InMemory.

**What NOT to test first:** controllers (thin mapping), migrations (generated code), DTOs, simple repositories with no logic.

**Realistic target in one sprint:**
- **Core entities:** 90%+ (they're trivial to test)
- **Application services (critical paths only):** 60-70%
- **Global coverage:** 40-50% is realistic and meaningful — better than 80% that includes test noise

Document what's NOT covered and why, so the next sprint has a clear backlog.
</details>

<details><summary>Deep Dive</summary>

**The "highest risk" heuristic**

Ask: "If this breaks in production, what's the cost?" 
- `Order.GetTotal()` is wrong → customer over/underpays → financial damage, trust loss
- `QuantityInStock` not decremented → overselling → fulfilment failures
- `DeleteCartAsync` after refund → stale cart data → minor annoyance

Start with financial and inventory code. Test the "boring" path through the UI last or not at all.

**The test-first discovery process**

Start writing tests and you'll immediately hit design issues that make testing hard:
- `new PaymentIntentService()` directly in `PaymentService` → no DI seam → wrap it
- `DateTime.Now` hardcoded → inject `TimeProvider`
- Methods doing 5 things → split them

0% coverage is often a symptom of design debt. Tests reveal it. One sprint of testing = one sprint of design improvement.

**Practical sprint plan**

```
Day 1-2: Setup (xUnit project, Moq, FluentAssertions, SQLite, CI integration)
Day 3-4: Core entity tests (Order, Product, DeliveryMethod)
Day 5-6: OrderService tests (mock repos, happy path + error paths)
Day 7-8: PaymentService tests (mock Stripe wrapper)
Day 9: Concurrency tests (SQLite in-process)
Day 10: Review, refactor tests, document gaps
```

**Coverage gate in CI**

```xml
<!-- Run coverlet in CI, fail build if coverage drops below threshold -->
<PackageReference Include="coverlet.collector" Version="6.0.0" />
```

```yaml
# GitHub Actions
- name: Run tests with coverage
  run: dotnet test --collect:"XPlat Code Coverage" --results-directory coverage

- name: Check coverage
  run: |
    dotnet tool install -g dotnet-reportgenerator-globaltool
    reportgenerator -reports:coverage/**/coverage.cobertura.xml \
      -targetdir:coverage/report -reporttypes:Html -assemblyfilters:"+Core;+Application"
```

Start with a low threshold (40%) and raise it each sprint. A threshold you can hit is better than an aspirational one that gets turned off.

**The "quick wins" antipattern**

Resist the urge to test `ProductController.GetProducts()` because it's easy. The coverage number goes up but the risk doesn't go down. Prioritise test value, not test count.
</details>

---

**K2.** You're debugging a race condition that only reproduces under high load. You can't reproduce it locally. What tools and strategies do you use?

<details><summary>Answer</summary>

1. **Add structured logging** around the critical section — log timestamp, thread ID, user ID, stock value before and after every read/write. Query logs (Seq, Application Insights) for two writes within milliseconds on the same product.

2. **Check if `[ConcurrencyCheck]` is correctly applied** — if stock goes negative without a `DbUpdateConcurrencyException`, the attribute is missing or on the wrong property.

3. **Write a load test** — use k6 or NBomber to reproduce the load locally. 100 concurrent requests for the same product should trigger the race within seconds.

4. **Add metrics** — count `DbUpdateConcurrencyException` occurrences and retry counts via Application Insights custom metrics. A spike tells you WHEN and HOW OFTEN the race happens.

5. **Use Azure Monitor / SQL Insights** — check for lock timeouts, deadlocks, long-running transactions in the production DB.

6. **Review the code for the read-modify-write pattern** — any `FindAsync` followed by mutation followed by `SaveChangesAsync` without a concurrency token IS a race condition waiting to happen.
</details>

<details><summary>Deep Dive</summary>

**Structured logging — what to capture**

```csharp
_logger.LogInformation(
    "StockDecrement: ProductId={ProductId} RequestedQty={Qty} CurrentStock={Stock} ThreadId={Thread} UserId={User}",
    productId, quantity, product.QuantityInStock,
    Environment.CurrentManagedThreadId, userId);
```

In Application Insights or Seq, query:
```
| where message contains "StockDecrement"
| where productId == "42"
| order by timestamp
```

If you see two "CurrentStock=1" entries within 5ms on the same productId, that's your race.

**k6 load test — reproduce in 30 lines**

```javascript
// k6 script — 100 users all buying the last item
import http from 'k6/http';

export const options = { vus: 100, duration: '10s' };

export default function() {
  const payload = JSON.stringify({ productId: 42, quantity: 1, ... });
  const res = http.post('https://localhost:5001/api/orders', payload, {
    headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${TOKEN}` }
  });

  // Expect: most get 409 (sold out), exactly ONE gets 200
  if (res.status === 200) console.log('Order created:', res.json().id);
  if (res.status === 409) console.log('Conflict:', res.json().error);
  if (res.status === 500) console.log('BUG — unhandled race:', res.body);
}
```

If you see 500s or `QuantityInStock < 0` in the DB, the race condition is confirmed.

**Deadlock vs race condition**

Two different problems, same symptom (timeout/error under load):
- **Race condition** — two transactions proceed simultaneously, one overwrites the other's data. Fix: `[ConcurrencyCheck]` or atomic SQL.
- **Deadlock** — two transactions each hold a lock the other needs. Fix: ensure consistent lock ordering, use `WITH (UPDLOCK)` hints, shorten transactions.

Check SQL Server deadlock graph (Extended Events or `sys.event_log`) to distinguish.

**When you can't reproduce locally at all**

The race may require >100 concurrent requests — impossible on a dev machine. Options:
1. Request a staging environment with a load balancer and multiple API instances
2. Use Azure Load Testing (managed k6 at scale) against a staging slot
3. Deploy extra logging to production for 24 hours, collect data, roll back

Never debug a race condition by adding `Thread.Sleep` — you're just shifting the timing, not fixing the root cause.

**The definitive fix verification**

After applying the fix (`[ConcurrencyCheck]` + exception handling or atomic SQL), run the same k6 script. Verify:
- 0 responses with `status=500`
- `QuantityInStock` in DB never goes below 0
- All concurrent requests either succeed (one) or get a clean 409 (all others)
</details>

---

**K3.** A colleague pushes a test with no `Verify` and says "it passes, ship it." How do you handle this in code review without being condescending?

<details><summary>Answer</summary>

**In code review — leave a specific, actionable comment:**

> "Quick question on this test — what code change would break it? I was trying to figure out what regression it would catch, but I think it might pass even if we remove the Redis call entirely (no `Verify` on `StringSetAsync`). Could be wrong — what's your read? If I'm right, adding `_mockDb.Verify(x => x.StringSetAsync(...), Times.Once)` would make it a real safety net."

**Key principles:**
1. **Ask a question, don't make an accusation** — "what code change would break this?" forces them to think it through without being told they're wrong
2. **Show you might be wrong** — "could be wrong" reduces defensiveness
3. **Offer the fix** — don't just point out the problem
4. **Use the concrete example** — "remove the Redis call" is specific and falsifiable, not vague criticism

If they push back: pair on it. Delete the Redis call together, run the test. Let the outcome speak.
</details>

<details><summary>Deep Dive</summary>

**Why code review tone matters for testing culture**

If you come across as condescending about a test, the colleague learns to avoid the topic with you — not to write better tests. The goal is a team that values test quality, not a team that's afraid of your code review comments.

**The "what would break this?" question**

This is the single most useful question for evaluating any test. A test is only as good as the bugs it would catch. If the answer is "nothing" or "I don't know," the test needs `Verify` or a stronger assertion.

```
"What code change would break this test?"
→ Colleague: "Um... if we change the return type?"
→ You: "Right, but what if we removed the Redis call and just returned `true` directly?"
→ Colleague: "Oh... it would still pass."
→ You: "Yeah, that's the gap. The test doesn't prove Redis was called. Adding Verify closes it."
```

**What to include in the comment — template**

```
This test currently asserts [what it actually tests].
What it doesn't prove: [the side effect or interaction that matters].
A code change that would NOT break this test: [concrete example of a regression that passes].
Suggested fix: [specific Verify or assertion to add].
```

Applied:
```
This test asserts that SetCartAsync returns a non-null cart.
What it doesn't prove: that StringSetAsync was called on Redis with the correct key.
A code change that would NOT break this test: removing the Redis call from SetCartAsync entirely.
Suggested fix: _mockDb.Verify(x => x.StringSetAsync(It.Is<RedisKey>(k => k == cart.Id), ...), Times.Once);
```

**When to escalate**

If the colleague repeatedly ships tests with no `Verify` and ignores review comments, raise it in a team retrospective as a testing standards discussion — not as a personal complaint. Frame it as "let's align on what makes a good test" and propose a team agreement. Document it in a `TESTING.md` or PR template checklist.

**The hardest part: passing tests feel like passing tests**

The colleague isn't malicious — they ran the test, it passed, everything looks green. The problem is invisible. Empathise with that, then show why the invisible problem matters: "a test that passes when the production code is broken is worse than no test — it gives false confidence."
</details>

---

**Total: ~120 questions across 11 sections**
**Distribution: ~24 Easy (20%) | ~48 Medium (40%) | ~48 Hard (40%)**

---

> "A test that can't fail is worse than no test — it gives you false confidence."
