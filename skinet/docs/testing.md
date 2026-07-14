# Testing

One file for all verification. **Top = the checklists** you run (per task / before deploy /
after deploy). **Bottom = reference** for writing tests. Automated *and* manual both live here.

---

## ✅ After each task (before opening the PR)

- [ ] `dotnet test` green locally
- [ ] `dotnet build -c Release` clean — **Release is where analyzer warnings become errors** (T54)
- [ ] `npm run lint` + `npm run format` clean (frontend)
- [ ] Smoke the one behavior the task changed — happy path **and** the failure path it touches
- [ ] Pass the gate (EXPLAIN / REBUILD / BREAK) → tick `progress.md`

CI re-runs all of the above on the task PR — the backstop, not the first check. Setup: `build-deployment.md`.

## ✅ Before `development` → `main` (deploy gate)

**Automated**
- [ ] Full suite green (`dotnet test`)

**Core smoke** — at `https://localhost:7096` (the prod artifact, single-origin). The Stripe step
needs `stripe listen --forward-to https://localhost:7096/api/payments/webhook` running.
- [ ] App loads, no console errors, seed data shows
- [ ] register → login → cart → checkout (Stripe `4242 4242 4242 4242`) → admin → refund → logout
- [ ] Logout → cookie cleared (DevTools → Application → Cookies)

**This sprint's behaviors** — swap to the tasks just completed; past sprints are locked by the test suite.
> _Sprint 2:_ Price change T17 — reach the **Payment** step, then edit that product's `Price` in
> `data/store.db` → expect **409 + redirect to cart, no charge**. Idempotency (T16/T18) + concurrency
> (T13/T14) have no clean manual check → just confirm `dotnet test` is green.
>
> _Sprint 3:_ Payment sync T26 — skip `stripe listen` for one checkout (webhook never fires) → order
> stuck **Pending** → admin orders table → "sync" icon → expect **PaymentReceived** + SignalR
> notification (if that session is still connected). Then run one checkout **with** `stripe listen`
> running to confirm the webhook path still works unchanged.
>
> _BUG-11 (stateless-country checkout):_ check out with a **Romanian** address (Stripe's Address Element
> shows no state field) → order must be **created**, and render without a dangling comma. Then a **US**
> address **with** a state → must still succeed. State is required only for US/CA/AU; a regression here
> is a 400 on the money path, so it belongs in the deploy gate, not just the unit tests.
>
> _BUG-12 (admin paging):_ `/admin/orders` → click page 2 → the rows must **change**. The wire param is
> `pageIndex`, not `pageNumber` — model binding drops an unknown name silently, so this fails with no
> error anywhere.

**Infra** — confirm against Azure *before* the push (app boots without them but fails on first cart/cache use or payment):
- [ ] App Service **Redis** connection string set (not `localhost`)
- [ ] Key Vault `kv-skinet-test` has Stripe `SecretKey` + `WhSecret`; managed identity can read it

## ✅ After deploying to `main` (Azure)

- [ ] GitHub Actions all checks green
- [ ] Live URL loads, no 500
- [ ] Smoke: register → login → cart → Stripe payment → admin order visible

Both local and Azure green → sprint is closed. See `git-workflow.md` for the merge step.

---

# Reference — writing tests

xUnit (.NET), v3 + FluentAssertions + Moq. No Angular unit tests — UI flows covered by Playwright E2E (Task 37).

## Test projects

```
tests/
├── Core.Tests/           # Entity + domain logic
├── Application.Tests/    # DTOs, specs, mapping, validators
├── Infrastructure.Tests/ # Services (mocked Redis, InMemory EF)
└── API.Tests/            # (placeholder — controllers covered by Playwright)
```

Shared helpers in `tests/Tests.Shared/`.

## Coverage

Target ~80% backend. What's intentionally **not** covered:

- API controllers → Playwright E2E (Task 37)
- `PaymentService` / `CouponService` → `[ExcludeFromCodeCoverage]`: they call Stripe SDK classes directly with no DI seam. Architectural fix: inject an `IStripeClient` abstraction (deferred).

Snapshot percentages live in `progress.md` (Task 19 log), not here — they go stale.

## Running

```bash
# All tests
dotnet test

# Specific project
dotnet test tests/Core.Tests

# With coverage (requires dotnet-coverage tool)
dotnet-coverage collect "dotnet test" --output coverage.xml --output-format cobertura
reportgenerator -reports:coverage.xml -targetdir:coverage-report -reporttypes:TextSummary "-assemblyfilters:+Core;+Application;+Infrastructure"
```

## Naming

`MethodName_Scenario_ExpectedBehavior`

## What gets tests

- Every new service with business logic → tests in the same PR
- Every new spec or mapping → tests in the same PR
- Infrastructure bootstrapping (factories, seed, config) → no unit tests
- Controllers → no unit tests (Playwright)
- Stripe services → no unit tests (no DI seam)

## Rules

- Use FluentAssertions (`.Should().Be(...)`) — not raw `Assert.Equal`.
- Infrastructure tests use InMemory EF for OrderService, mock Redis for CartService.
- Never call real external APIs (Stripe, Redis) in unit tests.
