# Progress Log

## Daily Log

| Date | Task | Notes |
|------|------|-------|
| Apr 2 | S1: T1,3,4,5,6,7 | Sprint 1 complete. Azure verified. |
| Apr 6 | T9, BUG-1 | CORS + security headers, checkout Next button fix |
| Apr 7 | T15, T16, T18 | Cart delete ordering, Stripe idempotency, order unique constraint |
| Apr 8 | docs | Refactored + updated all affected docs |
| Apr 8 | T17 | Price change detection: 409 + snackbar + redirect. Also fixed order detail routing (/order → /orders) |
| Apr 9 | T19 | Unit tests: 128 tests, Core 90%, Application 89.3%, Infrastructure services ~85%. Fixed broken CartService mock, added OrderService happy path + idempotency, discount edges, search/sort specs, mapping tests, DTO validators, coupon tests. [ExcludeFromCodeCoverage] on Stripe services. |
| Apr 21 | Sprint 2 interview polish | Audited 11-section interview set vs `interview-gen-prompt.md` spec; added 7 questions (G12 Testcontainers, H12 `signal.mutate()` removal, I9 outbox pattern, I10 3DS/SCA, J8 captive dependency, J9 sync-over-async, K7 zoneless runtime-mode bug). Regenerated Anki for G/H/I/J/K (12+12+10+9+7 cards). |
| Jun 18 | Sprint 2 → live on Azure + BUG-4..10 | Deployed Sprint 2 to `skinet-2025-test`. Fixed deploy-hardening bugs: auth-401 nav (4), wwwroot root-500 (5), fonts CSP (6), Stripe CSP+COEP (7), serverless SQL idle-pause retry (8), config-driven admin password (9); logged BUG-10 (loading bar, deferred). `main` rewritten to 1 squashed commit/sprint. Live smoke green: register→cart→Stripe→order→refund→admin. |
| Jun 25 | T8 | FluentValidation gate passed. Cold rebuild: filter PARTIAL, validators PASS, wiring PASS, tests PARTIAL (first-ever unit tests). Deep LEARN on the filter internals (`continue`, open generics + `MakeGenericType`, `System.Type`, `ValidationContext` type-erasure, `ValidateAsync`/`CascadeMode`, `SetValidator`). Regenerated `task-8-fluentvalidation.md` from the real gaps. 136 tests green; scratch files removed. |
| Jun 25 | T24 | Bogus seed: 70 products / 13 users / 25 orders via `dotnet run -- --seed` (one-shot, dev-only, idempotent, deterministic). Real images by type; unique `PaymentIntentId`; brand casing aligned to baseline. Verified live: product/brand/order endpoints, order totals checked. 136 tests green. |
| Jun 26 | fix/deps | Dependency refresh: all packages → latest GA (framework 10.0.9; majors Redis 2→3.0.7, Stripe.net 51→52.1.0) + cleared SQLitePCLRaw advisory (`bundle_e_sqlite3 3.0.3` override). Build 0/0, 136 green, 0 vulnerable solution-wide. Dev-smoked Redis + Stripe. |
| Jun 26 | T27 | Dashboard backend: `GET /api/admin/dashboard` via `IDashboardService` (in-memory aggregation, controller stays thin). `DashboardDto` + lean `LowStockProductDto`/`RecentOrderDto`; revenue = `PaymentReceived` only (net of discount, incl. shipping); low-stock threshold 5; recent/low-stock previews capped. 138 tests green; live-verified against seed (70 products / 25 orders). |
| Jul 1 | T25 | Admin layout: `AdminComponent` shell (mat-sidenav + `<router-outlet>`) + `features/admin/routes.ts` child routes (dashboard/products/orders), guarded once at the parent (`[authGuard, adminGuard]`), lazy-loaded via `loadChildren`. Verified live (5/5): guard blocks non-admin, `/admin` redirects to dashboard, child routes swap, deep-link survives refresh, orders+refund work. Gate passed: cold rebuild PASS/PARTIAL across all 4 pieces, EXPLAIN + BREAK answered (shell/placeholder, guard activation not inheritance, `pathMatch:'full'` infinite-loop). |
| Jul 2 | T26 | Payment status reconciliation: `POST /api/admin/orders/{id}/sync-payment-status`, recovery path for a missed webhook. Rescoped mid-spike from a free-form status dropdown (rejected — payment status must be Stripe-sourced only) to a reconcile endpoint reusing the webhook's match/mismatch rule via new `IPaymentService.ReconcileOrderPayment`/`SyncOrderPaymentStatus`. `Refunded` orders excluded. 140 tests green (2 new); verified live via UI (stuck-Pending → sync → PaymentReceived, webhook regression check) + Scalar (Refunded/not-found/idempotent-resync edge cases). Gate passed: cold rebuild PASS on backend, EXPLAIN + BREAK answered (shared-method drift risk, SignalR circular-reference constraint, missing-guard corrupts Dashboard revenue). |
| Jul 13 | T26 merge, BUG-12 | Merged T26 (PR #53). Found + fixed BUG-12: admin orders paginator was dead — Angular sent `pageNumber`, backend binds `PagingParams.PageIndex`, so model binding silently dropped it and page 2+ re-served page 1 (`pageSize` bound fine, which is why it looked alive). Shop already did it right (`shop.service.ts:38`); the admin table skipped the rename. Verified live. Also logged BUG-11 + BUG-12 in `Execution plan.md` — neither was tracked there. |
| Jul 13 | BUG-11 | Checkout 400 for stateless countries (Romania). Spike said "1-line fix (the validator)" — wrong: `State` was mandatory at **4** layers (TS model, validator, entity, NOT NULL column), so dropping the validator alone would have turned a clean 400 into a `DbUpdateException` **after** Stripe took the money. Root discovery: `MakeAddressStateOptional` (Apr 6) had already fixed the *wrong* entity — `Address` (saved profile), not `ShippingAddress` (owned entity in `Orders`, what checkout uses). Fixed per-country (Option B): `State` required only for US/CA/AU — Stripe's own validated list. New migration both providers; `AddressPipe` now drops empty parts (was rendering `", ,"`). 147 tests green (7 new). Verified live: RO order created, US still enforced. |
| Jul 13 | BUG-13 | Shipping name persisted as the literal string `"null null"` — `stripe.service.ts` did `user.firstName + ' ' + user.lastName`, and JS coerces null to `"null"`; the seeded admin has neither name, so `"null null"` was saved onto the order. Third lying TS model of the day: `user.ts` declared `firstName: string` / `address: Address` while the backend has `string?` / `user.Address?.toDto()` — the type lie is *why* the concat looked safe. Fixed with a null-safe join + honest types. Verified live (named account prefills, RO/US both clean). Forward-only: orders already saved with `"null null"` keep it. |
| Jul 14 | T54 *(PR #57 + #58 — merged)* | Lint + analyzer gate, **strict**. Frontend **50 → 0** ESLint errors; backend **368 → 0** Roslyn warnings. `TreatWarningsAsErrors` scoped to **Release** so CI fails the PR on any warning while local Debug stays warning-only. Gate *proven*, not assumed — injected a violation into each half and watched both reject it. **Forced side-quest:** Angular was pinned to `22.0.0-next.1`; semver sorts a prerelease *below* its own release, so `angular-eslint`'s peer range rejected it outright → upgraded to **22.0.6 stable** (+ Node 24.14→24.18), stripping the 8 `$safeNavigationMigration()` shims `ng update` injected (templates diff byte-identical). **Three bugs the linter found that review had not:** `address.pipe.ts` `?.address!` (TypeError on a nullish address); `ProductSpecParams.Search` culture-sensitive `ToLower()` (Turkish `I`→`ı` silently breaks search); `<NoWarn>CS8618</NoWarn>` *assigning* instead of appending, wiping every `Directory.Build.props` suppression. **One bug it tried to create:** CA1862 wanted `StringComparison` inside an EF expression tree — EF *throws* on those overloads, so obeying it would have broken every product search. A self-review pass then caught a regression I'd introduced (delivery `<label>`→`<div>` killed the card's click target) plus two template diagnostics `ng update` had silently suppressed. 147 tests green. Visual check passed (cart empty state, delivery cards, item page) and surfaced **BUG-14** — add-to-cart locks the button until the quantity changes; pre-existing, not from T54 → Descoped. **Gate:** REBUILD **5 PASS / 1 PARTIAL** · EXPLAIN **2 PASS / 3 PARTIAL** · BREAK **4 BLANK** (focus gone ~4h in; a fake PASS is worse than an honest BLANK). Real gaps found: Roslyn's *timing* (build, not runtime), "already running" vs "added", and the analyzer-can't-tell-which trap. **The gate itself broke and got rebuilt (PR #58):** ~30 questions answered incrementally over chat = 4h of ping-pong. Now capped at 3 EXPLAIN + 2 BREAK, 25-min timebox, six non-blendable phases, and a `## The Change Map` STUDY step — because a 157-file diff is unstudiable but an 8-file map isn't. |

---

## Sprint 1 — Security + CI/CD *(done: Apr 2)*

- [x] T1 — GitHub Actions: PR trigger, `dotnet test`, `npm ci`
- [x] T3 — Secrets → User Secrets (dev) + Azure Key Vault (prod)
- [x] T4 — Admin seed password → env var / Key Vault
- [x] T5 — Identity password: min 12, upper+number+special
- [x] T6 — Account lockout: 5 attempts, 15 min
- [x] T7 — Cookie: SameSite=Strict, Secure, HttpOnly, 24h

## Sprint 2 — Testing + Data Integrity *(done: May 7)*

- [x] T9 — CORS explicit + security headers
- [x] T13 — Stock race condition: `[ConcurrencyCheck]` + `DbUpdateConcurrencyException`
- [x] T14 — Transaction: `BeginTransactionAsync()` via IUnitOfWork
- [x] T15 — Verify cart delete is AFTER commit
- [x] T16 — Stripe idempotency: `IdempotencyKey = cart.Id`
- [x] T18 — Order idempotency: unique constraint on PaymentIntentId
- [x] BUG-1 — Checkout: Address tab "Next" button stays disabled
- [x] T17 — Price change detection: cart vs DB, 409 + snackbar + redirect
- [x] T19 — Expand unit tests: specs, discount edges, validators. Target 80% → achieved 90% Core, 89.3% Application

## Sprint 3 — Admin Panel *(Jun 16 – Jul 1 · closed Jul 13 — **goal not met**, 5/8 delivered)*

- [x] T8 — FluentValidation: auto-validation, CreateOrderDto + CreateProductDTO
- [x] T24 — Bogus seed: 50+ products, 10+ users, 20+ orders
- [x] T27 — Dashboard backend: `GET /api/admin/dashboard`
- [x] T25 — Admin layout: parent + sidebar + child routes
- [x] T26 — Payment status reconciliation: `POST /api/admin/orders/{id}/sync-payment-status`
- [x] BUG-12 — Admin orders paginator dead (`pageNumber` → `pageIndex` on the wire)
- [x] BUG-11 — Checkout 400 for stateless countries (e.g. Romania)
- [x] BUG-13 — Shipping name saved as literal `"null null"` (JS null-concat in `stripe.service.ts`)
- → T28 / T29 / T30 **spilled to Sprint A**

> **Why it missed:** over-ran 12 days. Backend flew (T27 in a day); the Angular half (T25) + a mid-sprint
> rescope (T26) + an 11-day energy stop ate the slack. Three real bugs surfaced late and none were planned
> for. **Lesson: the plan had no room for bugs, and bug-time is not optional.**

## Re-baseline — 2026-07-13

Original plan slipped ~2 weeks; not recoverable by re-sequencing. Re-planned against **measured velocity**.

- **Root cause:** the six "sprints" were **epics**, not **timeboxes** — a themed container can't absorb
  spillover without breaking its own theme, so each became a mini-waterfall that couldn't end. Now split:
  sprints = timeboxes (end on the date, whatever fits); epics = labels.
- **Measured velocity** (Jun 16 → Jul 13, 28 days): ~12 pts / 28 days = **0.43 pts/day** (S=1, M=2, L=4).
- **Arithmetic:** ~50 pts remaining vs ~30 pts capacity to Sep 6 (55 days @ ~0.55 pts/day with Uber hours
  reduced) → **~22 pts (~40%) descoped.**
- **Levers spent:** Aug 30 → **Sep 6** (+1 wk, hard limit) · Uber hours reduced · **AZ-204 pushed past v1.0**
  (it was double-booked against the same hours).
- **No slack left.** One more bad week = cut again immediately, don't grind. Cut order: README(47) >
  deploy(53) > admin CRUD(28–31) > E2E(37) > rest.

## Sprint A — Finish the Admin Panel *(Jul 14 – Jul 27)* ⏳ CURRENT

- [x] T54 — ESLint + `@angular-eslint` + Prettier + Roslyn analyzers *(first — gates every PR after it)*
- [ ] T28 — Dashboard frontend: 3 Material Cards + recent orders table *(spillover)*
- [ ] T29 — Product list: Material Table + Paginator *(spillover)*
- [ ] T30 — Product create/edit: reactive form, Material inputs *(spillover)*
- [ ] T31 — Product delete: reuse ConfirmationDialogComponent

## Sprint B — Testing + Domain Depth *(Jul 28 – Aug 10)*

- [ ] T37 — Playwright E2E: 3 tests (checkout, auth+orders, admin) — timebox 4d
- [ ] T40 — OrderStatus: Processing/Shipped/Delivered/Cancelled + OrderStatusHistory
- [ ] T41 — Order cancellation: POST cancel + Stripe refund
- [ ] BUG-2 — Checkout: declined card can't retry in-session (money path)

## Sprint C — Ship It *(Aug 11 – Aug 24)*

- [ ] T47 — README rewrite *(day 1, highest ROI)*
- [ ] T12 — Cart ownership: userId in Redis, verify on read/write *(IDOR — must not ship)*
- [ ] T52 — Open redirect: validate returnUrl starts with `/`
- [ ] T48 — C4 diagram (Context+Container) in Mermaid
- [ ] T50 — API docs: `[ProducesResponseType]` + XML summaries
- [ ] T43 — DB indexes: unique PaymentIntentId, BuyerEmail, composite
- [ ] T21 — Coverage gate: coverlet, fail PR < 60%
- [ ] T38 — README UX screenshots

## Buffer *(Aug 25 – Sep 6)* — DO NOT FILL

- [ ] T53 — Final Azure smoke test *(non-negotiable, always last)*
- [ ] Interview deck + Anki prep + slack

> Descoped for v1.0 (~22 pts): see `Execution plan.md`.

