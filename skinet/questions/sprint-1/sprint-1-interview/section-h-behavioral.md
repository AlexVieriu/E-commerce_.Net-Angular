## SECTION H — BEHAVIORAL & PROBLEM-SOLVING

**H1.** A senior developer insists on using JWT stored in localStorage for authentication. You believe cookies with HttpOnly are more secure. How do you make your case without being combative? What are the legitimate arguments for JWT that you should acknowledge?

<details><summary>Answer</summary>

- **Acknowledge JWT benefits** — stateless, no server-side session, works across origins, easy for mobile, self-contained claims.
- **Present the concern** — localStorage is accessible to any JS on the page; one XSS and the token is stolen.
- **Propose a middle ground** — store the JWT in an `HttpOnly; Secure; SameSite` cookie: stateless + claims, without the localStorage XSS surface.
- **Frame it diplomatically** — "both work, but cookies give us defense-in-depth." Back it with OWASP, not personal opinion.
</details>

<details><summary>Deep Dive</summary>

**The actual security comparison**

| | localStorage JWT | HttpOnly Cookie JWT |
|---|---|---|
| XSS access | Stolen immediately | Not accessible to JS |
| CSRF | Not vulnerable | Vulnerable (mitigate with SameSite=Strict/Lax) |
| Cross-origin | Flexible | Requires CORS + credentials config |
| Mobile | Easy | Requires cookie support in HTTP client |
| Stateless | Yes | Yes (JWT in cookie is still stateless) |

**The middle ground argument — concrete code**

```csharp
// Server sets cookie on login
Response.Cookies.Append("auth_token", jwtToken, new CookieOptions
{
    HttpOnly = true,       // no JS access
    Secure = true,         // HTTPS only
    SameSite = SameSiteMode.Strict, // CSRF protection
    Expires = DateTimeOffset.UtcNow.AddHours(1)
});
```

```typescript
// Angular — no need to manage tokens at all
// Browser automatically attaches the HttpOnly cookie to every same-origin request
this.http.get('/api/orders', { withCredentials: true });
```

**How to present it without being combative**

1. "I want to make sure I understand the tradeoffs — can you walk me through why localStorage is preferred here?" (Ask before arguing)
2. "The concern I have is XSS — if any third-party script is compromised, the JWT is gone. Does the HttpOnly cookie approach work for this use case?"
3. "OWASP recommends..." (cite authority, not your opinion)
4. "Happy to defer if there's a constraint I'm missing — do we need cross-origin auth?"

If they still prefer localStorage after the discussion, note the risk in writing (PR comment, ADR), then move on. Not every battle is worth fighting.

**When localStorage is actually correct**

- Native mobile app using a WebView — cookies may not work reliably
- Cross-domain SSO (auth.company.com → app.company.com) — cookies don't cross domains by default
- API consumed by non-browser clients (CLI tools, scripts) that can't handle cookies

Always ask "who is the client?" before deciding on token storage.
</details>

---

**H2.** Your PR has 47 files changed. The reviewer says "This is too large to review." How do you handle this? How do you prevent it next time?

<details><summary>Answer</summary>

**Handle** (don't be defensive):
- Offer a walkthrough call to guide the reviewer through the changes.
- Split into stacked PRs (e.g. entities/migrations first → service layer → API endpoints).
- Add a detailed PR description with a file-by-file summary of intent.

**Prevent next time**:
- Break work into vertical slices (one feature end-to-end per PR).
- Use feature flags to merge incomplete features safely.
- Commit and PR daily — rule of thumb: under 400 lines changed, 10–15 files max.
</details>

<details><summary>Deep Dive</summary>

**Why large PRs are genuinely a problem**

A reviewer can meaningfully review ~200-400 lines in one sitting. Beyond that, cognitive load increases, mistakes are missed, and reviewers start rubber-stamping to get through it. Studies of code review effectiveness show defect detection rate drops sharply above 400 LOC.

47 files often means the PR was held back too long — "I'll finish the whole feature, then PR." This defeats the purpose of code review.

**Stacking PRs — practical workflow**

```
feature/order-domain   ← PR 1: entities, domain logic, no external deps
    ↓ merged
feature/order-service  ← PR 2: service layer, mocked repos
    ↓ merged
feature/order-api      ← PR 3: controller, DTOs, integration test
    ↓ merged to development
```

Each PR is reviewable in 30 minutes. Context carries from one to the next. Merge conflicts are smaller.

**Feature flags — merge incomplete work safely**

```csharp
if (_featureFlags.IsEnabled("NewCheckoutFlow"))
{
    return await _newCheckoutService.ProcessAsync(dto);
}
return await _legacyCheckoutService.ProcessAsync(dto);
```

Deploy to development/staging with the flag OFF. Enable it for testing. Merge to main, enable for production when ready. The feature is in main before it's "done" — PR size stays manageable.

**PR template — force yourself to think about scope**

```markdown
## What changed
- [ ] Entities / domain models
- [ ] Service layer
- [ ] API controllers / DTOs
- [ ] Tests
- [ ] Infrastructure / migrations

## Why
<!-- The business reason, not the technical change -->

## How to review
<!-- Walk the reviewer through the change order -->
1. Start with `Core/Entities/Order.cs` — new state machine
2. Then `Application/Services/OrderService.cs` — uses the state machine
3. Controllers are thin — just check status codes
```

If filling this out takes you 30 minutes, the PR is too big.

**What to say when told a PR is too large**

```
"You're right — I should have split this up. Let me:
1. Move the migration + entity changes to a separate merged PR first
2. Rebase this PR on top
3. Then we can review the service changes in isolation
Can we schedule a 20-minute walkthrough to unblock this while I prepare the split?"
```

Don't be defensive. They're right.
</details>

---

**H3.** You find a bug in production that affects 0.1% of orders. The fix is simple but the root cause is unclear. Do you deploy the fix immediately or investigate the root cause first? Justify your answer.

<details><summary>Answer</summary>

Deploy the fix NOW. 0.1% sounds small but at scale it's real money and trust.

Steps:
1. Deploy the fix with extra logging around the affected code path.
2. Monitor to confirm it resolves the issue.
3. Open a follow-up ticket for root cause investigation with the new logs.
4. Time-box the investigation (don't let it drag).

The fix and the investigation are parallel tracks, not sequential. Document the decision: "Deployed fix at [time], root cause TBD, tracking in [ticket]."
</details>

<details><summary>Deep Dive</summary>

**Why "investigate first" is often the wrong call**

"Simple fix, unclear root cause" is common in production bugs. While you investigate:
- More orders are affected (0.1% × time = growing impact)
- Data may become inconsistent (orders in wrong state, payments not linked)
- Support tickets accumulate, trust erodes

The investigation happens FASTER with the fix deployed and extra logs running, because you can observe the problem in production — not reproduce it locally (which may be impossible).

**The deploy + investigate framework**

```
Hour 0: Alert fires — 0.1% order failure rate
Hour 0-1: Identify scope, apply fix, deploy to staging
Hour 1: Deploy fix to production (with added logging)
Hour 1-2: Confirm fix resolves the failure rate
Hour 2-5: Root cause investigation with new log data
Hour 5: Post-mortem drafted, follow-up tickets created
```

**When NOT to deploy immediately**

- The fix changes a database schema (migration risk) — test more carefully
- The fix is complex and might introduce NEW bugs — consider a feature flag instead of a direct hotfix
- The 0.1% is in a security-critical path — might need security review before deploy

**The post-mortem — what makes it useful**

```markdown
## Incident: Order creation failure (2024-03-15, 14:00-15:30)

**Impact:** 0.1% of orders (47 orders) failed during a 90-minute window
**Root cause:** Race condition in stock decrement — missing [ConcurrencyCheck]
**Fix deployed:** 15:30 — added [ConcurrencyCheck] to Product.QuantityInStock
**Follow-up tickets:**
- [ ] Retry logic for DbUpdateConcurrencyException
- [ ] Alert on stock going negative
- [ ] Test coverage for concurrent order placement

**Why wasn't this caught earlier?**
- Unit tests didn't test concurrent scenarios
- InMemory DB used in tests doesn't enforce concurrency tokens

**Prevention:**
- SQLite in-process tests for concurrency scenarios
- k6 load tests in CI
```

The goal is not to assign blame — it's to prevent the same category of bug from reaching production again.

**"Simple fix" — define it**

"Simple" means: change one or two lines, obviously correct, low risk of side effects. If it FEELS simple but you can't explain WHY it's correct in one sentence, it's not simple — slow down.
</details>

---

**Total: ~75 questions across 8 sections**
**Distribution: ~15 Easy (20%) | ~30 Medium (40%) | ~30 Hard (40%)**

---

> "The difference between a mid-level developer and a senior is not what they can build — it's what they can explain, defend, and debug under pressure."
