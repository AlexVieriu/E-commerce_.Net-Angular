# Testing

One file for all verification. **Top = the checklists** you run (per task / before deploy /
after deploy). **Bottom = reference** for writing tests. Automated *and* manual both live here.

---

## ✅ After each task (before opening the PR)

- [ ] `dotnet test` green locally — **needs Redis up** (`docker compose up -d redis`); `API.Tests` runs
      against the real thing, so without it those tests fail on a connection error, not an assertion
- [ ] `dotnet build -c Release` clean — **Release is where analyzer warnings become errors** (T54)
- [ ] `npm run lint` + `npm run format` clean (frontend)
- [ ] `npm test -- --watch=false` green (frontend) — the bare `--` ends npm's own arguments
- [ ] Smoke the one behavior the task changed — happy path **and** the failure path it touches.
      **Write it down only if it earns a file** (see below); most tasks don't
- [ ] Pass the gate (EXPLAIN / REBUILD / BREAK) → tick `progress.md`

CI re-runs all of the above on the task PR — the backstop, not the first check. Setup: `build-deployment.md`.
It runs Redis as a `services:` container on the `.NET Build & Test` job. A test that needs a new external
service needs a new service block there too, or it goes green locally and red on the PR.

## ✅ Before `development` → `main` (deploy gate)

**Automated**
- [ ] Full suite green (`dotnet test`)

**Core smoke** — at `https://localhost:7096` (the prod artifact, single-origin). The Stripe step
needs `stripe listen --forward-to https://localhost:7096/api/payments/webhook` running.
- [ ] App loads, no console errors, seed data shows
- [ ] register → login → cart → checkout (Stripe `4242 4242 4242 4242`) → admin → refund → logout
- [ ] Logout → cookie cleared (DevTools → Application → Cookies)
- [ ] **Apply a coupon** — `gimme10` (percent-off) and `gimme5` (amount-off) both resolve; a nonsense code
      is still rejected. No test can reach `CouponService` (no DI seam, `[ExcludeFromCodeCoverage]`), which
      is why BUG-20 hid for two months

**This sprint's behaviors** — `smoke-tests/sprint-<n>/`. Run the current sprint's files here. Past sprints
are locked by the test suite; the entries below predate the folder and stay until their sprint file exists.

**A task earns a file only when both hold:** no automated test can reach the check, *and* you'd re-derive
the steps at the gate. Expect ~1 per sprint. Anything the Core smoke above already walks through — the page
loads, the filter filters, the sort sorts — does not earn one. A folder of files nobody runs is worse than
no folder.
> _Sprint D:_ see [`smoke-tests/sprint-d/`](smoke-tests/sprint-d/) — T43 index measurement + the one new
> check (an over-long product name must be a 400).
>
> _Sprint C:_ see [`smoke-tests/sprint-c/`](smoke-tests/sprint-c/README.md) — one file per task or bug.
> BUG-19 coupon trust is 4 tests; needs `stripe listen` and a throwaway Stripe campaign. Test D needs a
> coupon with **cents** — `gimme5` and `gimme10` are whole amounts and cannot catch a truncated conversion.
>
> _Sprint B:_ see [`smoke-tests/sprint-b.md`](smoke-tests/sprint-b.md) — BUG-18 cart ownership (12 steps,
> in order; needs `stripe listen` and two accounts).
>
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

xUnit (.NET), v3 + FluentAssertions + Moq. Angular unit tests run on **Vitest** (T52) — see below.
UI *flows* are covered by Playwright E2E (`skinet/e2e/`, Task 37), which is local-only.

## Angular unit tests (`client/src/**/*.spec.ts`)

```bash
cd skinet/client
npm test                      # watch mode in a terminal
npm test -- --watch=false     # one run — what CI does
```

- **Runner: Vitest**, set in `angular.json`'s `test` target (`builder: @angular/build:unit-test`,
  `runner: vitest`). Karma cannot run here — root `overrides` force `glob 10` / `rimraf 5` on the whole
  tree and `karma@6.4.4` needs 7 / 3.
- **Environment: jsdom, no browser.** `browsers` is deliberately absent; per the builder schema, *"When
  not specified, tests are run in a Node.js environment using jsdom."* So there is no address bar, no
  navigation and nothing rendered — anything needing those belongs in Playwright.
- **What they're for:** pure logic reachable from a component's public surface. `isLocalUrl`'s decision
  is the first example (`login/login.component.spec.ts`, 14 cases).
- Each case builds its own `TestBed`, because the value under test is read in the constructor.

## Playwright E2E (`skinet/e2e/`)

Local-only (never CI — see `mindset/spikes.md` T37 Decision 1). Needs Docker's `redis` container
running first (`docker compose up -d redis`); `global-setup.ts` starts it automatically if not.

```bash
cd skinet/e2e
npx playwright test              # all 3 tests: checkout, auth+orders, admin pagination
npx playwright test --ui         # interactive UI mode
npx playwright show-report       # last run's HTML report
```

`webServer` starts `dotnet run` + `ng serve` itself and `global-setup.ts` seeds demo data
(`dotnet run -- --seed`) before the suite runs — no manual setup beyond Redis.

### Layout

```
skinet/e2e/                    own npm project (own package.json + node_modules)
├── playwright.config.ts       the wiring: baseURL, webServer, projects -- read this first
├── global-setup.ts            runs once, before any browser opens
├── tests/                     WHAT to test -- the story
│   ├── *.spec.ts              one file per flow
│   └── setup/*.setup.ts       not tests: log in once, save the session
├── pages/                     HOW to reach it -- the only place selectors live
│   └── *.ts                   one class per page or widget (page object)
├── support/                   shared constants (credentials, API URL) -- no selectors
└── generated, git-ignored:
    playwright/.auth/*.json    saved logins, written by tests/setup/
    test-results/              traces + screenshots of failures
    playwright-report/         the HTML report
```

### What runs when

```
npx playwright test
  |
  +-- global-setup.ts    docker compose up -d redis; dotnet run -- --seed (seeds, then exits)
  |
  +-- webServer[]        starts API :7096 + Angular :4200, waits until both answer,
  |                      stops both when the run ends
  |
  +-- projects           a dependency graph, not a list:

        setup-customer --+-------------------> checkout ---------+
                         |                                       v
                         +-----------------------------> customer-orders

        setup-admin ---------------------------------> admin
```

- `tests/setup/customer.setup.ts` logs in once and writes `playwright/.auth/customer.json`.
- `checkout` and `customer-orders` name that file as their `storageState` — they open already logged in.
- `dependencies` also forces order: a project runs after everything it lists, even when you run it
  alone with `--project=`.

⚠️ `testMatch` in the config maps a **file name** to a project. Renaming a spec file unhooks it
from its project and its `dependencies`. Smallest page object to copy: `pages/CartPage.ts`.

## API integration tests (`tests/API.Tests/`)

The real API in-process — real pipeline, real cookie auth, real controllers, **real Redis**. Sits between
unit tests (everything mocked) and Playwright (real browser). Use it to prove an **endpoint contract** —
who can reach what — without paying for a browser.

Needs Redis running (`docker compose up -d redis`). Nothing else.

```
CustomWebApplicationFactory
├── real       pipeline · auth cookies · controllers · DI · Redis
└── swapped    throwaway SQLite file per run   (migrations apply on boot, file deleted after)
               fake Stripe + admin settings    (StripeSettings is ValidateOnStart — no boot without them,
                                                and no dependency on your user-secrets)
```

`CreateAuthenticatedClientAsync()` registers a fresh GUID-email user, logs in, and returns a client with
**its own cookie jar**. Call it twice and one test acts as two people:

```csharp
var alice   = await factory.CreateAuthenticatedClientAsync();
var mallory = await factory.CreateAuthenticatedClientAsync();
```

Three traps, all already handled in the factory — don't undo them:

- **Clients must use `https://localhost`.** The Identity cookie is `SecurePolicy.Always`, so a cookie
  container won't replay it over `http` and every call 401s *after a successful login*. TestServer
  ignores the scheme; the client still has to ask for it. Use `CreateWebClient()`, not `CreateClient()`.
- **`SqliteConnection.ClearAllPools()` before deleting the temp DB.** EF pools connections, so the file
  stays locked and teardown throws on every test.
- **Settings read *eagerly* must be environment variables, not `ConfigureAppConfiguration`.**
  `AddApplicationServices` reads `StripeSettings:SecretKey` while `Program.cs` is still running, before
  the factory has added any source. Locally your user-secrets fill the gap and it looks fine; on CI there
  are none and the host won't boot. Env vars are in place that early and outrank user-secrets. Keep
  per-instance values (the SQLite path) out of them — parallel test classes would share one.

`API.csproj` carries `<InternalsVisibleTo Include="API.Tests" />` — top-level statements make `Program`
internal, and `WebApplicationFactory<Program>` has to name it.

## Test projects

```
tests/
├── Core.Tests/           # Entity + domain logic
├── Application.Tests/    # DTOs, specs, mapping, validators
├── Infrastructure.Tests/ # Services (mocked Redis, InMemory EF)
└── API.Tests/            # Endpoint contracts end-to-end (real host, real Redis)
```

Shared helpers in `tests/Tests.Shared/`. Frontend tests live beside their component as
`*.spec.ts` under `client/src/`, not in a separate project.

## Coverage

Target ~80% backend. What's intentionally **not** covered:

- API controllers → `API.Tests` (endpoint contracts) + Playwright E2E (Task 37)
- `PaymentService` / `CouponService` → `[ExcludeFromCodeCoverage]`: they call Stripe SDK classes directly with no DI seam. Architectural fix: inject an `IStripeClient` abstraction (deferred).

Snapshot percentages live in `progress.md` (Task 19 log), not here — they go stale.

## Running

```bash
# All tests
dotnet test

# Specific project
dotnet test tests/Core.Tests

# Integration only (needs Redis up)
dotnet test tests/API.Tests

# With coverage (requires dotnet-coverage tool)
dotnet-coverage collect "dotnet test" --output coverage.xml --output-format cobertura
reportgenerator -reports:coverage.xml -targetdir:coverage-report -reporttypes:TextSummary "-assemblyfilters:+Core;+Application;+Infrastructure"

# Frontend (from skinet/client)
npm test -- --watch=false
```

## Naming

`MethodName_Scenario_ExpectedBehavior`

## What gets tests

- Every new service with business logic → tests in the same PR
- Every new spec or mapping → tests in the same PR
- Infrastructure bootstrapping (factories, seed, config) → no unit tests
- Controllers → no unit tests. Ownership/auth contracts → `API.Tests`; user flows → Playwright
- Stripe services → no unit tests (no DI seam)

## Rules

- Use FluentAssertions (`.Should().Be(...)`) — not raw `Assert.Equal`.
- Infrastructure tests use InMemory EF for OrderService, mock Redis for CartService.
- Never call real external APIs (Stripe, Redis) in unit tests. `API.Tests` is the exception by design:
  it uses real Redis. Stripe stays faked there too — nothing in it reaches Stripe.
