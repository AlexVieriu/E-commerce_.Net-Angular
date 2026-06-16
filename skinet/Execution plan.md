# Execution Plan

> .NET 10 + Angular 22 | Azure-only | Solo dev | B2C physical goods
> **50 tasks | 6 sprints (2 wk each) | Deadline: Aug 30, 2026**
> Mid .NET / junior Angular | CV project | Claude Code for speed, saved time for learning

**Estimates:** S (<1d), M (1-3d), L (1wk+) — without Claude Code.
**LEARN:** 12 marked tasks. Claude implements -> you read every line + do exercise -> write `skinet/questions/sprint-N/task-N-*.md` + one line in `interview-prep.md`.
**Git:** Branch per task -> development (squash); merge sprint -> main (`--no-ff`). Conventional commits. See `docs/git-workflow.md`.

**Deadline reality (restart Jun 16 → Aug 30 at 4 days/week):**
Sprints 1–2 done. Build window Jun 16 – Aug 16 (Sprints 3–6), then Aug 17–30 reserved for interview-deck/Anki prep.
~36 build-days vs ~32 days of remaining task work — thin but real slack. One slow sprint = one cut task somewhere.
CV priority if forced to cut: keep Sprint 3 (admin) + Sprint 6 (README); cut from S4/S5 first.
Never sacrifice working Azure deploy + README. That's what job hunters actually look at.

---

## What Already Exists

Full CRUD products/orders/cart/payments/coupons | Cookie Identity (Admin+Customer) | Stripe (PaymentIntent+webhook+refund+coupons) | SignalR notifications | Redis cart+cache | Angular 22 zoneless+signals+Tailwind+Material | 4-step checkout | Admin orders+refund | Guards (auth/admin/emptycart/orderComplete) | Rate limiting | ExceptionMiddleware+errorInterceptor | GitHub Actions CI/CD to Azure | 21 docs | 14 test files | Security audit done (stock race condition fixed in T13)

---

## Sprint 1 — Security + CI/CD *(done: Apr 2)*

> Hardened app + working CI. Mostly .NET config warm-up.
> Order: CI(1) -> Secrets(3,4) -> Identity(5,6,7)

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 1 | GitHub Actions: PR trigger, `dotnet test`, `npm ci`, .NET 10 + Angular 22 | S | -- | Pipeline validates all future work |
| 3 | Secrets -> User Secrets (dev) + Azure Key Vault (prod). Deploy config BEFORE rotating keys | M | -- | LEARN: Config provider chain. CRITICAL: keys in git history — rotate after deploy verified |
| 4 | Admin seed password -> env var / Key Vault | S | 3 | Hardcoded Pa$$w0rd |
| 5 | Identity password: min 12, upper+number+special | S | -- | Batch 5+6+7 — do together |
| 6 | Account lockout: 5 attempts, 15 min | S | -- | |
| 7 | Cookie: SameSite=Strict, Secure, HttpOnly, 24h | S | -- | |

Sprint end: verify local + Azure deploy. Update README.

**Time:** Best 5d | Normal 8d | Worst 12d (buffer consumed — ended Apr 2)

---

## Sprint 2 — Testing + Data Integrity *(done: May 7)*

> Fix race condition + idempotency + coverage. All C#. TDD: failing test first.
> Order: CORS/CSP(9) -> Race condition(13-15) -> Idempotency(16,18) -> Price detection(17) -> Tests(19)

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 9 | CORS explicit + security headers (HSTS, X-Content-Type, X-Frame, Referrer, CSP w/ `unsafe-inline` for Material) | M | -- | Carried from S1. Test visually after CSP |
| 13 | Failing test for stock race condition + fix: `[ConcurrencyCheck]` on QuantityInStock, catch `DbUpdateConcurrencyException` | M | -- | LEARN: Optimistic vs pessimistic concurrency |
| 14 | Wrap order creation in `BeginTransactionAsync()` via IUnitOfWork, rollback on failure | M | 13 | LEARN: Transaction isolation levels |
| 15 | Verify cart delete is AFTER commit, not inside transaction | S | 14 | Already done, just verify |
| 16 | Stripe idempotency: `IdempotencyKey = cart.Id` | S | -- | |
| 18 | Order idempotency: unique constraint on PaymentIntentId, return existing if duplicate | S | -- | Group with 16 (both idempotency) |
| 17 | Price change detection: cart vs DB prices, 409 + snackbar + redirect to cart | M | -- | No dialog, just snackbar |
| 19 | Expand unit tests: specs, discount edges, validators. Target 80% backend | M | -- | Build on 14 existing files |
| BUG-1 | Checkout: Address tab "Next" button stays disabled until voucher field is clicked | S | -- | Pre-existing. Fix reactive form validation trigger |

Sprint end: full test suite green. Verify Azure. Update README.

**Time:** Best 5d | Normal 8d | Worst 12d
- **Best:** CORS/CSP done in 1d, race condition fix is clean. Prep "optimistic concurrency" + "transaction isolation" answers.
- **Normal:** CSP needs `unsafe-inline` tuning. Coverage lands at 55% not 80% — move on.
- **Worst:** CSP breaks Material (1d debug). **Talking point:** "We had a race condition — here's how I fixed it" = strong interview story.

---

## Sprint 3 — Admin Panel *(Jun 16 – Jul 1)*

> Most clickable CV feature. Backend first, then Angular.
> Pattern: copy ShopComponent's httpResource + Material table. Don't break existing orders/refund.

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 8 | FluentValidation: auto-validation, CreateOrderDto + CreateProductDTO validators, strip ClientSecret from cart | M | -- | Carried from S1. LEARN: FluentValidation vs Data Annotations |
| 24 | Bogus seed: 50+ products, 10+ users, 20+ orders. `dotnet run -- --seed` | M | -- | Need data before admin pages |
| 27 | Dashboard backend: `GET /api/admin/dashboard` (counts, revenue, lowStock, recent) | S | -- | Pure C#, finish all backend before Angular |
| 25 | Admin layout: parent + sidebar + child routes (/admin/dashboard, /products, /orders) | M | -- | LEARN: Lazy loading + child routes. Verify orders still work |
| 26 | Order status update: `PUT /api/admin/orders/{id}/status` + Material dropdown | S | 25 | |
| 28 | Dashboard frontend: 3 Material Cards + recent orders table | M | 25,27 | |
| 29 | Product list: Material Table + Paginator, wire to existing GET /api/products | M | 25 | Copy existing admin table pattern |
| 30 | Product create/edit: reactive form, Material inputs. Text URL for image (no upload) | M | 25 | |

Sprint end: test CRUD products, dashboard stats, refund still works. Verify Azure. Update README.

**Time:** Best 5d | Normal 8d | Worst 11d
- **Best:** FluentValidation in 1d, backend flies (pure C#), Angular copies ShopComponent pattern cleanly. Start Sprint 4 quick wins (31,32).
- **Normal:** Angular child routes + guards take 2d (junior Angular risk). Refund breaks, you fix it. Dashboard shows real Bogus data.
- **Worst:** Child routes cause 3d of confusion. Ship product list(29) without form(30). **Never cut this sprint short.** Working admin CRUD is what separates you from "followed a tutorial" devs on CV reviews.

---

## Sprint 4 — Frontend Quality + UX *(Jul 2 – Jul 17)*

> Polish existing features. Isolated Angular tasks, easiest first.
> Order: ESLint(54) -> Lighthouse(55) -> Product delete(31) -> Leak audit(32) -> Forms(33) -> Skeleton(34) -> Images(35) -> Performance(56) -> Playwright(37) -> README(38)

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 54 | ESLint + `@angular-eslint` + Prettier: setup + CI integration. Roslyn analyzers for .NET | S | -- | **Do first.** Enforces quality for rest of sprint and all future PRs |
| 55 | Lighthouse baseline: run on `/`, `/shop`, `/checkout`, screenshot + record scores | S | -- | 2 hours max. Baseline before T35 so you have before/after numbers |
| 31 | Product delete: reuse ConfirmationDialogComponent | S | 29 | Carried from S3. Dep 29 done. |
| 32 | Memory leak audit: unsubscribed Observables -> `takeUntilDestroyed()` | S | -- | Likely minimal (signals+httpResource). Done early in T17 session |
| 33 | Form errors: `<mat-error>` on login/register via TextInputComponent | S | -- | |
| 34 | Skeleton loaders: SkeletonCardComponent + SkeletonRowComponent | M | -- | |
| 35 | NgOptimizedImage: `ngSrc` + priority/lazy on product images | S | -- | Mostly HTML changes |
| 56 | Performance pass: MiniProfiler (.NET) + bundle analyzer (Angular), fix top 3 findings, re-run Lighthouse | M | 35 | After T35 so NgOptimizedImage is already in. Compare against T55 baseline |
| 37 | Playwright E2E: 3 tests (checkout, auth+orders, admin). Timebox 4 days | L | 1 | LEARN: Testing pyramid + Page Object Model |
| 38 | README update with UX screenshots | S | -- | |
| BUG-2 | Checkout: declined card can't retry in-session — "card unavailable" (Stripe `elements.submit()` is single-use). Re-init PaymentElement on payment failure | S | -- | Pre-existing. Found during T17/T18 testing |
| BUG-3 | Checkout: Address "Next" stays disabled with no hint when a required field (e.g. postal code) is empty — Stripe Address Element shows no error until a field is touched. Add a hint/validation cue | S | -- | Pre-existing. Found during checkout testing |

Sprint end: Playwright + backend tests green. Verify deploy.

**Time:** Best 5d | Normal 8d | Worst 11d
- **Best:** T54+T55 done in 1d, quick wins (31,32,33,35) next day, performance pass fast. Start OrderStatus(40) early.
- **Normal:** Playwright setup is painful, CI is flaky — run locally only.
- **Worst:** Playwright eats the sprint. Timebox at 4d, ship 2 tests not 3. **Talking point:** Even 2 E2E tests signals testing maturity that most candidates don't have.

---

## Sprint 5 — Domain Expansion *(Jul 18 – Aug 1)*

> Domain modeling depth. Backend comfort zone.
> Order: Status(40) -> Cancel(41) -> Email(39) -> Soft delete(42) -> Indexes(43) -> Tests(46) -> Integration tests(20)

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 40 | OrderStatus: add Processing/Shipped/Delivered/Cancelled + append-only OrderStatusHistory | M | -- | LEARN: State machine + audit trail. Foundational — unlocks 41 |
| 41 | Order cancellation: POST cancel, only Pending/PaymentReceived, Stripe refund if paid. Log email (decouple from 39) | M | 40 | |
| 39 | Email: IEmailService + Channel\<T\> BackgroundService. Dev: log. Prod: MailKit/Azure Comms. Graceful shutdown | M | -- | LEARN: Channel\<T\> producer-consumer. Infrastructure — no blockers |
| 42 | Soft delete: IsDeleted+DeletedAt, global query filter, admin IgnoreQueryFilters | S | -- | LEARN: EF Core global query filters |
| 43 | DB indexes: unique PaymentIntentId, BuyerEmail, composite (Brand,Type), Price | S | -- | One migration |
| 46 | Unit tests: cancellation, soft delete, OrderStatusHistory, discount edges | M | 40,41,42 | |
| 20 | Integration tests: WebApplicationFactory + TestContainers (SQL+Redis) | L | 19 | Carried from S2. LEARN: TestContainers. Timebox 3 days |

Sprint end: full test suite. Test cancel, soft delete, status history. Verify Azure. Update README.

**Time:** Best 5d | Normal 8d | Worst 11d
- **Best:** State machine is clean, Channel<T> clicks, TestContainers works first try. Pick 1 from S6 "if ahead".
- **Normal:** Cancellation + Stripe refund edge cases take an extra day. TestContainers setup takes 2d — timebox it.
- **Worst:** TestContainers flaky. Timebox at 3d, fallback EF InMemory. **Talking point:** OrderStatus history + cancellation = strong domain modeling story.

---

## Sprint 6 — Polish + Standout *(Aug 2 – Aug 16)*

> Recruiter spends 30 seconds on your repo. README day 1 = highest ROI.

**Core — do in order, 53 always last:**

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 47 | README rewrite: overview, stack, diagram, screenshots/GIFs, "how to run", Azure link | M | -- | **DAY 1. Highest ROI.** |
| 52 | Open redirect: validate returnUrl starts with `/` | S | -- | Security fix — do early |
| 48 | C4 diagram (Context+Container) in Mermaid -> `docs/architecture.md` | M | -- | |
| 50 | API docs: `[ProducesResponseType]` + XML summaries | S | -- | |
| 49 | docker-compose full: SQL Server + Redis + Seq | M | -- | Required before 22 |
| 51 | RequireConfirmedEmail. Dev: auto-confirm. Prod: via IEmailService | S | 39 | |
| 53 | Final Azure smoke test: full user + admin journey | S | -- | **Non-negotiable. Last task always.** |

**If Ahead — top to bottom, stop when out of time:**

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 10 | Health checks: SQL Server + Redis, `/health` + `/ready` | S | -- | Carried from S1 |
| 22 | Serilog + Seq: Console (dev), Seq (prod), add Seq to docker-compose | M | 49 | LEARN: Structured logging vs interpolation |
| 23 | Correlation IDs: middleware -> LogContext -> response headers | S | 22 | |
| 21 | Coverage gate: coverlet, fail PR < 60% | S | 19,1 | |
| 2 | Dockerfile multi-stage + `.dockerignore` | M | -- | LEARN: Why `COPY *.csproj` before `COPY .`? |
| 12 | Cart ownership: userId in Redis, verify on read/write. Angular stops nanoid for authed users | M | -- | Security bug, has Angular side |
| 11 | Pagination: TotalPages, HasNext, HasPrev on `Pagination<T>` | S | -- | |
| 36 | Accessibility: axe on home/shop/checkout. Fix ARIA, keyboard nav, focus | M | -- | Just 3 pages |

Sprint end: final deploy + smoke test. Share with someone.

**Time:** Best 4d | Normal 8d | Worst 12d
- **Best:** README + diagram take 1d each. Work through "if ahead" tasks top to bottom. Apply to jobs.
- **Normal:** README takes 2d (screenshots, GIFs, wording). C4 diagram needs iteration. Deployed, smoke test passes.
- **Worst:** Running out of runway. **Hard priority: README(47) > smoke test(53) > diagram(48) > cut everything else.** A mediocre app with a great README beats a polished app nobody can run.

---

## Progress

- [x] Sprint 1 — Security + CI/CD (6 tasks) *(done: Apr 2)*
- [x] Sprint 2 — Testing + Data Integrity (9 tasks) *(done: May 7)*
- [ ] Sprint 3 — Admin Panel (8 tasks) *(Jun 16 – Jul 1)*
- [ ] Sprint 4 — Frontend Quality + UX (7 tasks) *(Jul 2 – Jul 17)*
- [ ] Sprint 5 — Domain Expansion (7 tasks) *(Jul 18 – Aug 1)*
- [ ] Sprint 6 — Polish + Standout (7 core + 8 if-ahead) *(Aug 2 – Aug 16)*
- [ ] Interview-deck / Anki prep *(Aug 17 – Aug 30)*

## Certifications *(never steals build days or run days)*

Uber-day reality: 2 days are 13h drives (cognitive zeros — passive audio only), 1 is a half-day (the real study slot).

- [ ] **AZ-900** — MS Learn path: passive audio on the 13h drives + real study on the half Uber day. **Exam target ~Jul 1.** Mostly review (Azure deploy + Key Vault already done).
- [ ] **AZ-204** — NO separate study during Sprints 3–6; the project IS the lab (Key Vault, App Service, App Config, Managed Identity, App Insights covered by building). Concentrated study in the **Aug 17–30 prep window** on the gaps only (Cosmos, Functions, Service Bus, Event Grid). **Exam end of Aug, slip to early Sept if Sprint 6 ran over.** Project ships first.

## When behind (life buffer)

Aug 30 is the **target**, not a cliff. The hard commitment is: ship with a working Azure deploy + great README.
When a medical check / contract renewal / bad day eats time → **cut the lowest-priority task** (see cut order below), never extend hours or sacrifice sleep/runs. Realistic finish: Aug 30 clean, mid-Sept if life hits. Both fine.

Every sprint end: E2E run, Azure verify, README update.

## Tech Stack Additions

| Sprint | Added |
|--------|-------|
| 1 | Azure Key Vault |
| 2 | NWebsec, Coverlet |
| 3 | FluentValidation, Bogus |
| 4 | Playwright |
| 5 | MailKit / Azure Comms, TestContainers |
| 6 | Mermaid, Serilog + Seq *(if ahead)* |

## LEARN Summary (12 exercises)

| # | Concept | Interview Question |
|---|---------|-------------------|
| 2 | Docker multi-stage | "Optimize Docker size/build?" *(S6 if ahead)* |
| 3 | Config providers | "Manage secrets in .NET?" |
| 8 | FluentValidation | "Annotations vs FluentValidation?" |
| 13 | Optimistic concurrency | "Concurrent writes in EF Core?" |
| 14 | Transaction isolation | "Isolation levels? Serializable?" |
| 20 | Integration testing | "Testing pyramid? TestContainers?" |
| 22 | Structured logging | "Why not interpolation?" |
| 25 | Angular lazy loading | "Route structure in large app?" |
| 37 | E2E testing | "Strategy? Page Object Model?" |
| 39 | Channel\<T\> + BackgroundService | "Background jobs without Hangfire?" |
| 40 | State machine + audit | "Model order status changes?" |
| 42 | Global query filters | "Soft delete in EF Core?" |

## If Ahead (pick max 2)

Domain events (M, backend) | Category hierarchy (M) | Wishlist (M) | SEO slugs (M)

## Risks

| Risk | Mitigation |
|------|------------|
| CSP breaks Material | `unsafe-inline`, test visually |
| Secrets rotation breaks Azure | Deploy config first, rotate second |
| Integration tests eat S5 | Timebox 3d, fallback EF InMemory |
| Admin Angular too slow | Backend first; ship list without form |
| Admin refactor breaks orders | Verify list+detail+refund post-refactor |
| Playwright CI fails | Run locally only |
| CancellationToken too big | Controllers+services first |
| Email not ready for cancel | Log "would send", decoupled |
| No time for Reviews | Cut. README+diagram+smoke = min Sprint 6 |
| Falling behind | Cut lowest task. Never cut README or sprint-end verify |

## Skipped

OAuth2/JWT, multi-currency, i18n, K8s, GitOps, CSRF, Prometheus/Grafana/Jaeger/OTel, Outbox, Service Bus, CQRS, Domain events, PWA, SSR, file upload, Angular unit tests, and more.

---

claude --resume 855033c5-43ce-41cf-afc0-7ed0bd5ba0b5
