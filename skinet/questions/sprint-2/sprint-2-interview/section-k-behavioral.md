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

---

**Total: ~120 questions across 11 sections**
**Distribution: ~24 Easy (20%) | ~48 Medium (40%) | ~48 Hard (40%)**

---

> "A test that can't fail is worse than no test — it gives you false confidence."
