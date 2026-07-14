# Interview Prep — Skinet

One-liner per concept. Click the link for full explanation + Q&A.

---

## Sprint 1

- [Task 1 — GitHub Actions CI/CD](sprint-1/task-1-github-actions.md) — 3 jobs: CI on PRs, deploy on merge to main via OIDC; fixed 12 pre-existing test failures in the process
- [Task 3 — Config providers & secrets](sprint-1/task-3-config-providers.md) — User Secrets (dev) + Azure Key Vault (prod) via AddAzureKeyVault + DefaultAzureCredential; last provider wins
- [Task 4 — Admin seed password → env var / Key Vault](sprint-1/task-4-admin-seed-password.md) — User Secrets (dev) + Key Vault (prod) via config provider chain; last provider wins
- [Tasks 5-6-7 — Identity password policy, lockout, cookie hardening](sprint-1/task-5-6-7-identity-cookie-security.md) — IdentityOptions for password/lockout + ConfigureApplicationCookie for HttpOnly/Secure/SameSite=Strict/24h absolute
- **[Sprint 1 Practice Exam](sprint-1/sprint-1-practice.md)** — 30 questions: recall + scenario + practical + senior stretch. Manual testing checklist included.

## Sprint 2

- [Task 9 — CORS explicit + CSP security headers](sprint-2/task-9-cors-csp-security-headers.md) — named CorsPolicy with origin whitelist + NetEscapades for CSP/X-Frame/X-Content-Type/Referrer-Policy; unsafe-inline tradeoff for Angular Material
- [BUG-1 — Angular signals + reference equality](sprint-2/bug-1-signal-reference-equality.md) — mutating a signal object returns same reference → no re-render; fix with spread operator to create new object
- [Task 13 — Optimistic concurrency](sprint-2/task-13-optimistic-concurrency.md) — [ConcurrencyCheck] adds original value to WHERE on UPDATE; DbUpdateConcurrencyException when another write beat you to it
- [Task 14 — Transaction isolation levels](sprint-2/task-14-transaction-isolation.md) — BeginTransactionAsync wraps order creation; explicit rollback on failure; isolation levels control what concurrent reads see
- [Task 15 — Cart delete post-commit](sprint-2/task-15-cart-delete-post-commit.md) — Redis and SQL don't share a transaction; delete cart after commit so a failed order doesn't lose the cart
- [Task 16 — Stripe idempotency key](sprint-2/task-16-stripe-idempotency.md) — IdempotencyKey = cart.Id prevents duplicate PaymentIntents on network retry; uniqueness ≠ idempotency
- [Task 18 — Order idempotency](sprint-2/task-18-order-idempotency.md) — unique DB constraint on PaymentIntentId + pre-insert check returns existing order instead of duplicate
- [Task 17 — Price change detection](sprint-2/task-17-price-change-detection.md) — custom PriceChangedException → 409; cart prices updated in Redis before throw; interceptor reloads cart signal on 409
- **[Sprint 2 Checkout Flow](sprint-2/sprint-2-checkout-flow.md)** — full Redis/SQLite/Stripe/Angular flow with Task 16 + 18 failure zones and known limitation marked
- [Task 19 — Mock overload resolution](sprint-2/task-19-mock-overload-resolution.md) — Moq silently ignores unmatched setups; wrong StringSetAsync overload (TimeSpan? vs Expiration) passes accidentally; fix with correct type + Verify
- **[Sprint 2 Practice Exam](sprint-2/sprint-2-practice.md)** — 39 questions: recall + scenario + practical + senior stretch. Covers tasks 9–19 + BUG-1.

## Sprint 3

- [Task 8 — FluentValidation](sprint-3/task-8-fluentvalidation.md) — validators as `AbstractValidator<T>` in Application; custom `ValidationFilter` resolves `IValidator<T>` and returns a 400 `ValidationProblemDetails`; `[ApiController]` doesn't auto-run FluentValidation, so the filter is the trigger
- [Task 24 — Bogus seed](sprint-3/task-24-bogus-seed.md) — `Randomizer.Seed` (static, set before any Faker) = determinism; `Orders.AnyAsync()` guard = idempotency; two-check gate (`runBulkSeed` + `IsDevelopment`) = prod safety boundary
- [Task 27 — Dashboard aggregation & spec flow](sprint-3/task-27-dashboard-aggregation-and-spec-flow.md) — specs are deferred (run on `ToListAsync`, read once in `ApplyCommon`); revenue = `PaymentReceived` only; in-memory aggregate because `GetTotal()` can't translate to SQL `SUM`; recent sorted by spec, low-stock sorted in service
- [Task 25 — Admin shell + child routes](sprint-3/task-25-admin-shell-child-routes.md) — `<router-outlet>` is a placeholder the Router fills, not parent-child communication; one guard on the parent protects the whole child subtree via activation, not inheritance; empty-path `redirectTo` needs `pathMatch:'full'` or it loops forever
- [Task 26 — Payment reconciliation & EF change tracking](sprint-3/task-26-payment-reconciliation-and-change-tracking.md) — one shared `ReconcileOrderPayment` rule, two triggers (webhook push vs admin pull); SignalR stayed in `API` — moving it into `Infrastructure` would be a circular project reference; EF `DetectChanges()` diffs live values against a load-time snapshot before `SaveChanges`, no manual "mark modified" needed for tracked entities

## Sprint A

- [Task 54 — ESLint, Prettier & Roslyn analyzers](sprint-A/task-54-lint-analyzers.md) — Roslyn analyzers already ship in the SDK and run on **every** build, so `AnalysisMode` is a severity **dial**, not an installer (368 warnings, zero packages); a rule matches **syntax**, not meaning — which is why `ToLower()` is a culture bug where it *executes* (`ProductSpecParams`) and correct where EF *translates* it to SQL (`ProductSpecification`), and why obeying CA1862 inside an expression tree would make EF **throw** on every product search
