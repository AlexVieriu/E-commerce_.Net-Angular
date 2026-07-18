# Execution Plan

> .NET 10 + Angular 22 | Azure-only | Solo dev | B2C physical goods
> **Milestone: `v1.0 — Interview Ready` · Sep 6, 2026**
> Mid .NET / junior Angular | CV project | Claude Code for speed, saved time for learning

**Estimates:** S (<1d), M (1-3d), L (1wk+) — without Claude Code.
**LEARN:** Claude implements -> you read every line + do exercise -> write `skinet/questions/sprint-N/task-N-*.md` + one line in `interview-prep.md`.
**Git:** Branch per task -> development (squash); sprint -> main (**squash** — one commit per sprint, never `--no-ff`). Conventional commits. See `docs/git-workflow.md`.
**Re-baselined 2026-07-13** (~40% descoped, deadline Aug 30 -> Sep 6). Rationale + velocity math: `progress.md`.

---

## What Already Exists

Full CRUD products/orders/cart/payments/coupons | Cookie Identity (Admin+Customer) | Stripe (PaymentIntent+webhook+refund+coupons) | SignalR notifications | Redis cart+cache | Angular 22 zoneless+signals+Tailwind+Material | 4-step checkout | Admin orders+refund | Guards (auth/admin/emptycart/orderComplete) | Rate limiting | ExceptionMiddleware+errorInterceptor | GitHub Actions CI/CD to Azure | 24 docs | 14 test files | Security audit done (stock race condition fixed in T13)

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
| BUG-4 | Navbar shows stale logged-in state after a 401 — client auth signal not cleared | S | -- | Found pre-deploy (Jun 18). `fix(bug-4)` |
| BUG-5 | Root URL serves stale SPA / 500 — Angular `outputPath` at `API/wwwroot` not `src/API/wwwroot` | S | -- | Found pre-deploy (Jun 18). `fix(bug-5)` |
| BUG-6 | Roboto + Material Icons blocked by CSP on prod artifact (Google Fonts CDN) — self-host fonts | S | -- | Surfaced once `:7096` was the tested origin. `fix(bug-6)` |
| BUG-7 | Stripe Elements blocked by CSP + COEP on prod artifact — allow Stripe origins, COEP `unsafe-none` | M | -- | Same root cause as BUG-6. `fix(bug-7)` |
| BUG-8 | App 500.30 on startup when serverless Azure SQL is idle-paused — `MigrateAsync` hits transient SQL 40613, no retry resiliency | S | -- | Found smoke-testing live Azure. `fix(bug-8)`: `EnableRetryOnFailure` on `UseSqlServer` |
| BUG-9 | Admin password not config-driven — seed only created-if-missing, so rotating Key Vault `AdminSettings:Password` never updated the existing admin (locked out, unrecoverable) | S | -- | Found logging into live admin. `fix(bug-9)`: seed resets admin password from config on startup |
| BUG-10 | Top loading bar stuck spinning on live — busy/loading counter never returns to 0 (likely SignalR connect counted by `loadingInterceptor`) | S | -- | Found in Sprint 2 live smoke. **Deferred** (frontend polish). `fix(bug-10)` |

> BUG-4..8 are deploy-hardening defects surfaced while smoke-testing the Sprint 2 release on Azure / the prod artifact (`:7096`); the dev server (`:4200`) sends no security headers and dev uses SQLite, so they stayed hidden until the real deploy.

Sprint end: full test suite green. Verify Azure. Update README.

**Time:** Best 5d | Normal 8d | Worst 12d
- **Best:** CORS/CSP done in 1d, race condition fix is clean. Prep "optimistic concurrency" + "transaction isolation" answers.
- **Normal:** CSP needs `unsafe-inline` tuning. Coverage lands at 55% not 80% — move on.
- **Worst:** CSP breaks Material (1d debug). **Talking point:** "We had a race condition — here's how I fixed it" = strong interview story.

---

## Sprint 3 — Admin Panel *(Jun 16 – Jul 1 · closed Jul 13)*

> Most clickable CV feature. Backend first, then Angular.
> **T28/29/30 spilled → Sprint A.**

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 8 | FluentValidation: auto-validation, CreateOrderDto + CreateProductDTO validators, strip ClientSecret from cart | M | -- | Carried from S1. LEARN: FluentValidation vs Data Annotations |
| 24 | Bogus seed: 50+ products, 10+ users, 20+ orders. `dotnet run -- --seed` | M | -- | Need data before admin pages |
| 27 | Dashboard backend: `GET /api/admin/dashboard` (counts, revenue, lowStock, recent) | S | -- | Pure C#, finish all backend before Angular |
| 25 | Admin layout: parent + sidebar + child routes (/admin/dashboard, /products, /orders) | M | -- | LEARN: Lazy loading + child routes. Verify orders still work |
| 26 | Payment status reconciliation: `POST /api/admin/orders/{id}/sync-payment-status` — re-sync from Stripe | S | 25 | Reuses webhook match/mismatch logic; no free-form entry. Fulfillment status (Shipped/Delivered) admin control rides with T40 |
| 28 | Dashboard frontend: 3 Material Cards + recent orders table | M | 25,27 | |
| 29 | Product list: Material Table + Paginator, wire to existing GET /api/products | M | 25 | Copy existing admin table pattern |
| 30 | Product create/edit: reactive form, Material inputs. Text URL for image (no upload) | M | 25 | |
| BUG-11 | Checkout 400 for stateless countries (e.g. Romania) — `ShippingAddressValidator` requires `State`, Stripe's Address Element omits it | S | -- | Found in Sprint 2 live smoke. Money path. `fix(bug-11)` |
| BUG-12 | Admin orders paginator dead — Angular sends `pageNumber`, backend binds `PagingParams.PageIndex`, so page 2+ silently re-serves page 1 | S | 25 | Found Jul 13 reviewing T26. Shop does it right (`shop.service.ts:38`); admin skipped the rename. `fix(bug-12)` |
| BUG-13 | Shipping name persisted as the literal string `"null null"` — `stripe.service.ts:69` does `user.firstName + ' ' + user.lastName`, and JS coerces null to `"null"` | S | -- | Found Jul 13 verifying BUG-11. Seeded admin has no first/last name. `user.ts` lies (`firstName: string` vs `AppUser.FirstName` = `string?`). `fix(bug-13)` |

Sprint end: test CRUD products, dashboard stats, refund still works. Verify Azure. Update README.

---

## Sprint A — Finish the Admin Panel *(Jul 14 – Jul 27)* · ⏳ CURRENT

> **Spillover from Sprint 3 — highest priority.** The admin CRUD is the headline CV feature.
> Epic: `Admin Panel`. Order: 54 -> 28 -> 29 -> 30 -> 31.

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 54 | ESLint + `@angular-eslint` + Prettier + Roslyn analyzers: setup + CI | S | -- | Goes first — 29/30/31 are the sprint's biggest Angular PRs, and a gate added after them never sees them |
| 28 | Dashboard frontend: 3 Material Cards + recent orders table | M | 25,27 | Spillover. Backend (T27) already ships the whole payload in one call |
| 29 | Product list: Material Table + Paginator, wire to existing GET /api/products | M | 25 | Spillover. Copy the admin-orders table pattern |
| 30 | Product create/edit: reactive form, Material inputs. Text URL for image (no upload) | M | 25 | Spillover |
| 31 | Product delete: reuse ConfirmationDialogComponent | S | 29 | Pulled from S4 — completes CRUD, cheap |
| BUG-15 | Edit form shows **stale** product — a PUT didn't clear the by-id cache, so `/products/:id/edit` re-served old data for up to the 10-min TTL (list looked fresh) | S | 30 | Found Jul 18 smoke-testing T30; pre-existing, the edit UI just exposed it. Two causes: **(1)** invalidation pattern `"api/products\|"` (trailing `\|`) matched list keys but not by-id keys `/api/products/72` → dropped the `\|`. **(2)** cache keys kept the raw path, so `/api/Products/72` (capital, Scalar) and `/api/products/72` (app) were different keys the case-sensitive Redis pattern couldn't both match → lowercased the path. Bonus: brands/types no longer go stale on create. `fix(bug-15)` |

**Points: 8.** Sprint end: full admin CRUD works. Products create/edit/delete. Dashboard renders.

---

## Sprint B — Testing + Domain Depth *(Jul 28 – Aug 10)*

> The two things that most separate you from tutorial-followers: E2E tests and a real domain model.
> Epics: `Testing`, `Domain`. Order: 37 -> 40 -> 41 -> BUG-2.

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 37 | Playwright E2E: 3 tests (checkout, auth+orders, admin). **Timebox 4 days** | L | 1 | LEARN: Testing pyramid + Page Object Model. Ship 2 tests if it drags. Would have caught BUG-12 |
| 40 | OrderStatus: add Processing/Shipped/Delivered/Cancelled + append-only OrderStatusHistory | M | -- | LEARN: State machine + audit trail. The strongest domain story in the plan |
| 41 | Order cancellation: POST cancel, only Pending/PaymentReceived, Stripe refund if paid | M | 40 | Log the email, don't build the email service (T39 is descoped) |
| BUG-2 | Checkout: declined card can't retry in-session — Stripe `elements.submit()` is single-use. Re-init PaymentElement on failure | S | -- | **Money path.** Pre-existing, found during T17/T18 |

**Points: 9.** Sprint end: E2E green. Order status history + cancellation work. Declined card can retry.

---

## Sprint C — Ship It *(Aug 11 – Aug 24)*

> Recruiter spends 30 seconds on your repo. README day 1 = highest ROI.
> Epics: `Docs`, `Security`. Order: 47 -> 12 -> 52 -> 48 -> 50 -> 43 -> 21 -> 38.

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 47 | README rewrite: overview, stack, diagram, screenshots/GIFs, "how to run", Azure link | M | -- | **DAY 1. Highest ROI of anything left.** |
| 12 | Cart ownership: userId in Redis, verify on read/write. Angular stops nanoid for authed users | M | -- | **IDOR — any client holding a cart id can read/write it.** Was buried in S6 "if ahead"; a known auth hole must not ship |
| 52 | Open redirect: validate returnUrl starts with `/` | S | -- | Security fix — cheap |
| 48 | C4 diagram (Context+Container) in Mermaid -> `docs/architecture.md` | M | -- | Gives an interviewer something concrete to ask about |
| 50 | API docs: `[ProducesResponseType]` + XML summaries | S | -- | |
| 43 | DB indexes: unique PaymentIntentId, BuyerEmail, composite (Brand,Type), Price | S | -- | One migration. Good perf talking point |
| 21 | Coverage gate: coverlet, fail PR < 60% | S | 19,1 | |
| 38 | README UX screenshots | S | 47 | Folds into 47 if time is tight |

**Points: 11.** Sprint end: README done, security holes closed, docs + diagram shipped.

---

## Buffer *(Aug 25 – Sep 6)* — **DO NOT FILL**

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 53 | Final Azure smoke test: full user + admin journey | S | -- | **Non-negotiable. Always last.** |

Everything else in this window: **interview deck + Anki prep + slack for slippage.**
If a sprint over-runs, it eats *this* — that is what the buffer is for. **No feature work lands here.**

---

## Descoped for v1.0 — *not shipping by Sep 6*

> Cut consciously on the 2026-07-13 re-baseline. **Not forgotten** — revisit after v1.0 ships.

| # | Task | Why cut |
|---|------|---------|
| 55 | Lighthouse baseline | Scores don't get you hired |
| 56 | Performance pass (MiniProfiler + bundle analyzer) | Depends on 55; premature at this scale |
| 34 | Skeleton loaders | Polish |
| 35 | NgOptimizedImage | Polish |
| 33 | Form errors: `<mat-error>` | Nice UX, no signal |
| fix/T27 | Dashboard preview sizes: recent orders 5→15, low-stock 10→15 (`DashboardService.cs`) | Cosmetic (fill the screen). Backend caps + 2 test asserts; deferred from T28 (2026-07-15). `fix/task-27-dashboard-preview-sizes` — do if ahead |
| fix/T30 | `register`/`login`: template-only members (`registerForm`, `validationErrors`, `onSubmit`) → `protected`, matching the T30 convention | Cosmetic, zero runtime effect. T30's modifier tidy (2026-07-16) left `product-form` the only file following the Angular style guide; these two are now the odd ones out. `fix/task-30-access-modifiers` — user's call: **after T31** |
| 32 | Memory leak audit | "Likely minimal (signals + httpResource)" — plan's own words |
| BUG-3 | Checkout address hint on empty required field | UX nicety, not on the money path |
| BUG-14 | Product page: "Add to cart" disables itself the instant you click it (`[disabled]="quantity() === quantityInCart()"`), so the button reads as broken even though the cart *did* update. Re-enables only when you change the Quantity box. Found Jul 14 during the T54 visual check | Same class as BUG-3 — confusing, but the cart works and it's not on the money path. Working as designed, just designed poorly |
| 39 | Email: IEmailService + `Channel<T>` BackgroundService | Expensive. Stripe + webhooks + SignalR already prove the async/infra story |
| 42 | Soft delete + global query filter | Nice EF trick, small payoff |
| 46 | Unit tests for cancellation/soft-delete/history | Follows its features; T40/41 get tests inline |
| 20 | Integration tests: WebApplicationFactory + TestContainers | T37 (E2E) buys the same "testing maturity" story for less |
| 49 | docker-compose full (SQL + Redis + Seq) | Only needed for 22 |
| 51 | RequireConfirmedEmail | Blocked on 39 |
| 22 | Serilog + Seq | Talking point, not a demo |
| 23 | Correlation IDs | Blocked on 22 |
| 10 | Health checks | Cheap, but nothing depends on it |
| 2 | Dockerfile multi-stage | Azure deploy already works without it |
| 11 | Pagination: TotalPages/HasNext/HasPrev | Cosmetic |
| 36 | Accessibility: axe pass | Real value, no interview signal for these roles |

---

## Progress

**Status lives in `skinet/progress.md`** — task checkboxes + daily log. Not duplicated here.

## Certifications *(never steals build days or run days)*

Uber-day reality: 2 days are 13h drives (cognitive zeros — passive audio only), 1 is a half-day (the real study slot).

- [ ] **AZ-900** — MS Learn path: passive audio on the 13h drives + real study on the half Uber day. **Exam target ~Jul 1.** Mostly review (Azure deploy + Key Vault already done).
- [ ] **AZ-204** — **PUSHED PAST v1.0 (re-baseline 2026-07-13).** It was double-booked against the exact
  hours the re-baseline reclaims from Uber, and the Aug 17–30 study window is now buffer. The project
  IS the lab (Key Vault, App Service, Managed Identity, App Insights — all covered by building it).
  **Start studying after v1.0 ships (Sep 6); exam late Sept/Oct.** The portfolio gets interviews; the
  cert supports it. Not the other way round.

## When behind (life buffer)

**Sep 6 is the target, not a cliff.** The hard commitment is: ship with a working Azure deploy + great README.
When a medical check / contract renewal / bad day eats time → **cut the lowest-priority task**, never extend
hours or sacrifice sleep/runs.

**The re-baseline already spent every lever** (+1 week, reduced Uber hours, AZ-204 pushed, 40% descoped).
There is nothing left to trade but scope. If another bad week lands, **cut again immediately** — do not
absorb it by grinding. Cut order: README (47) > Azure deploy (53) > admin CRUD (28–31) > E2E (37) > rest.

Every sprint end: E2E run, Azure verify, README update.

## Tech Stack Additions

| Sprint | Added |
|--------|-------|
| 1 | Azure Key Vault |
| 2 | NWebsec, Coverlet |
| 3 | FluentValidation, Bogus |
| A | ESLint (`angular-eslint`) + Prettier, Roslyn analyzers (`AnalysisMode=Recommended`). Angular -> 22.0.6 stable |
| B | Playwright |
| C | Mermaid |
| ~~descoped~~ | ~~MailKit / Azure Comms, TestContainers, Serilog + Seq~~ |

## LEARN Summary (12 exercises)

| # | Concept | Interview Question |
|---|---------|-------------------|
| ~~2~~ | ~~Docker multi-stage~~ | ~~"Optimize Docker size/build?"~~ **descoped** |
| 3 | Config providers | "Manage secrets in .NET?" |
| 8 | FluentValidation | "Annotations vs FluentValidation?" |
| 13 | Optimistic concurrency | "Concurrent writes in EF Core?" |
| 14 | Transaction isolation | "Isolation levels? Serializable?" |
| ~~20~~ | ~~Integration testing~~ | ~~"Testing pyramid? TestContainers?"~~ **descoped** — T37 (E2E) carries this story |
| ~~22~~ | ~~Structured logging~~ | ~~"Why not interpolation?"~~ **descoped** |
| 25 | Angular lazy loading | "Route structure in large app?" |
| 37 | E2E testing | "Strategy? Page Object Model?" |
| 54 | Static analysis (ESLint / Roslyn) | "Linter vs formatter? When is the linter *wrong*?" |
| ~~39~~ | ~~Channel\<T\> + BackgroundService~~ | ~~"Background jobs without Hangfire?"~~ **descoped** |
| 40 | State machine + audit | "Model order status changes?" |
| ~~42~~ | ~~Global query filters~~ | ~~"Soft delete in EF Core?"~~ **descoped** |

## If Ahead — **suspended until v1.0 ships**

Domain events | Category hierarchy | Wishlist | SEO slugs

> Not a menu right now. The re-baseline has ~2 pts of slack across 8 weeks. If you find yourself
> "ahead", the correct move is to **pull the buffer forward and ship early**, not to add scope.
> Revisit this list after Sep 6, while interviewing.

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
