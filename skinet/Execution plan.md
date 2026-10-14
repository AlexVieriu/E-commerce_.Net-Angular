# Execution Plan

> .NET 10 + Angular 22 | Azure-only | Solo dev | B2C physical goods
> **Milestone: `v1.0 — Interview Ready` · Oct 8, 2026 — 🔴 FIXED, NEVER MOVES** · interview Nov 11, 2026
> **Freeze Oct 6:** no new work after that date — rehearsal, fixes and the release only.
> Mid .NET / junior Angular | CV project | Claude Code for speed, saved time for learning

🔴 **v1.0 closed: Sprints A–E.** Sprints F–H are deferred to v2 — no dates, no deadline.
Open Sprint E rows (T59, T60, T61, the gate) carry over with them.

**Schedule:** 7 days a week, 10 working hours a day (12h − 2h break).
**1 plan-day = 1 calendar day. A sprint is one week: 7d capacity, 6d committed, 1d slack.**

🔴 **This file is the plan, not the record of how it changed.** No dated churn — no "re-costed on…",
"moved on…", "cut on…", no post-mortem tallies. Sprint-level history goes in `progress.md`. Two things
stay: a **row's own note**, shipped or open, because that is what the row is; and **closed sprints**,
left exactly as they closed.

**Estimates:** every row is in days.
- ✅ **measured** — re-sized against the code. ⚠️ **unmeasured** — a T-shirt guess × a multiplier, never
  checked. 🔴 — unmeasured *and* big enough to blow a week.
- Days are the only size. Closed sprints still show a T-shirt `E` column — it predates this one, it
  never agreed with it, and it is not kept up.
- **The blow-out rule, from this repo's own actuals:** a row that touches **something the repo has never
  done before** runs **3–5× its estimate** — T52 0.5d→2.5d (first Angular spec + a CI step) · T43
  0.5d→2d (`HasMaxLength` + per-provider migrations) · BUG-10 0.25d→1d (zoneless render path) · BUG-19
  0.5d→1.5d. A row doing **more of what already exists** lands on estimate — T48, BUG-14, BUG-2 all did.
- **Big T-shirt rows fail the other way** — measured against the code they come in at ~half: T40 6d→3.5d
  (and split) · T22 4d→1.5d · T42 4d→1.5d.
- **So: cost novelty up, cost bulk down, and measure before committing a week to either.**
- **Mechanical ×1.2** — wiring, templates, config, docs. **Concept ×2** — work introducing something new.
- **Gate: one day per sprint, at sprint end** (`mindset/the-gate.md`), Anki consolidation straight after.
  🔴 **Never estimated in advance, and never queued.** The list is built on the gate morning from the
  sprint's diff and your own transcript turns; only then does it split into full and light concepts,
  capped at 2 full + 2 light. So the rows below cost out **task work only** — each sprint's remaining
  headroom is what the gate draws on, and a purely mechanical sprint skips it for the deck pass alone.
- **Scope ladder per sprint: Must / Should / Could.** Only **Must** is committed.
- **Row order inside a sprint:** shipped rows first, in the order they landed; then the open rows —
  **Must → Should → Could, biggest first inside each tier**, so the essential work goes first and the
  small rows fall to the end. **A dependency beats size:** a row never sits above something it needs.
- **A shipped row's Est cell reads `✅ <date> (actual vs est)`** — the same shape every closed sprint uses.
  On an open row ✅/⚠️ mark whether the estimate was measured; on a shipped row ✅ marks delivery.
- **Mid-sprint checkpoint is day 4:** Must under 50% → drop Could, then Should, that day. Day 5 of 7 is
  71% through — too late to cut.
- **The date is fixed; the ladder is the lever.** On a 7-day week the ladder is the *only* brake left —
  there is no weekend to absorb a bad run, so a sprint that eats its slack drops a tier rather than
  borrowing from next week (`mindset/energy-protocol.md`).

---

## What Already Exists

Full CRUD products/orders/cart/payments/coupons | Cookie Identity (Admin+Customer) | Stripe (PaymentIntent+webhook+refund+coupons) | SignalR notifications | Redis cart+cache, **keyed by the caller** (`ICurrentUser`) | Angular 22 zoneless+signals+Tailwind+Material | 4-step checkout | Admin orders+refund | Guards (auth/admin/emptycart/orderComplete) | Rate limiting | ExceptionMiddleware+errorInterceptor | GitHub Actions CI/CD to Azure | 30 docs | 25 unit/integration test files + 4 Angular specs + 4 Playwright specs | API integration harness (`WebApplicationFactory`) | Security audit done (stock race condition fixed in T13, cart IDOR in BUG-18)

**No README** — T47 creates it in Sprint C.

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
| BUG-10 | Top loading bar stuck spinning on live — busy/loading counter never returns to 0 (likely SignalR connect counted by `loadingInterceptor`) | S | -- | Found in Sprint 2 live smoke. **Deferred** → tracked in *Pull-in candidates* (rank 1). `fix(bug-10)` |

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

## Sprint A — Finish the Admin Panel *(Jul 14 – Jul 27)* · ✅ CLOSED Jul 27

> **Spillover from Sprint 3 — highest priority.** The admin CRUD is the headline CV feature.
> Epic: `Admin Panel`. Order: 54 -> 28 -> 29 -> 30 -> 31.

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 54 | ESLint + `@angular-eslint` + Prettier + Roslyn analyzers: setup + CI | S | -- | Goes first — 29/30/31 are the sprint's biggest Angular PRs, and a gate added after them never sees them |
| 28 | Dashboard frontend: 3 Material Cards + recent orders table | M | 25,27 | Spillover. Backend (T27) already ships the whole payload in one call |
| 29 | Product list: Material Table + Paginator, wire to existing GET /api/products | M | 25 | Spillover. Copy the admin-orders table pattern |
| 30 | Product create/edit: reactive form, Material inputs. Text URL for image (no upload) | M | 25 | Spillover |
| 31 | Product delete: reuse ConfirmationDialogComponent | S | 29 | Pulled from S4 — completes CRUD, cheap |
| BUG-15 | Edit form shows **stale** product — a PUT didn't clear the by-id cache, so `/products/:id/edit` re-served old data for up to the 10-min TTL (list looked fresh) | S | 30 | Found Jul 18 smoke-testing T30. Two causes: the `[InvalidateCache]` pattern's trailing `\|` skipped the by-id keys, and raw-path keys split `/api/Products/72` from `/api/products/72`. Fix: drop the `\|`, lowercase the path. Full story → `progress.md`. `fix(bug-15)` |
| BUG-16 | **Editing a product's stock always 500s** — `PUT /products/{id}` throws `DbUpdateConcurrencyException` whenever `QuantityInStock` changes; every other field saves fine | S | 30 | Found Jul 18 smoke-testing T31. `Update()` on a **detached** entity puts the *new* `[ConcurrencyCheck]` stock in the `WHERE`, so it matches 0 rows (`ProductsController.cs:49`). Fix: load the tracked entity, map the DTO onto it, save. Blocks all stock edits. Approach → `spikes.md`. `fix(bug-16)` |

**Points: 10** *(8 planned + 2 unplanned bugs — BUG-15, BUG-16)*. Sprint end: full admin CRUD works. Products create/edit/delete. Dashboard renders.

---

## Sprint B — E2E Tests + IDOR Fix *(Jul 28 – Aug 11)* · ✅ CLOSED Aug 13 *(+2d)*

> Browser tests, and the security hole the first one of them uncovered.
> Epic: `Testing`. Order: 37 -> BUG-18.
> **Re-scoped 2026-08-10:** T40/T41 moved to Sprint D, BUG-2/BUG-17 to Sprint D. BUG-18 turned out to be an
> unplanned L on the money path and ate the sprint — that is the honest reason, and it was the right trade.

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 37 | Playwright E2E: 3 tests (checkout, auth+orders, admin). **Timebox 4 days** | L | 1 | LEARN: Testing pyramid + Page Object Model. Ship 2 tests if it drags. Would have caught BUG-12 |
| BUG-18 | **Cart leaks between users** — `GET/POST/DELETE /api/cart` take the cart id from the client, so any authenticated user can read or overwrite **any** cart by id (IDOR); logout also leaves the previous user's items in the `_cart` signal and `cart_id` in `localStorage` | L | 37 | Found Aug 5 smoke-testing BUG-17. Cart identity was client-owned while `CartController` is `[Authorize]` — a half-migration to user-owned carts. The same id also reached `POST /api/payments/{cartId}` and `CreateOrderDto.CartId`, so it could pay for and order from someone else's cart. Money path, cross-layer, **outranks T40**. Fix: drop the key parameter from `ICartService` so `CartService` derives it from an `ICurrentUser` seam. **Sized L** — absorbs **T20**'s `WebApplicationFactory` harness, without which the fix isn't proven at the API layer. **Closes T12** — same work. Approach → `spikes.md`. `fix(bug-18)` |

**Points: 7.** Sprint end: E2E suite green (BUG-17 red on purpose → Sprint E). Cart no longer leaks between
users — proven at the API and in the browser.

---

## Sprint C — README + Demo Fixes *(Aug 12 – Aug 25)* · ✅ CLOSED Aug 31 *(+6d)*

> A recruiter spends 30 seconds on the repo and 2 minutes on the live demo. This sprint makes both
> survive that. **There was no README** — T47 wrote the first one.
> Epics: `Docs`, `UX`. **As run:** rotate the SQL password -> BUG-20 -> BUG-19 -> deps -> 47 -> 48 -> 52.
> The UX half (43, BUG-10, BUG-14, 33, BUG-3, 38, deploy-gate) never started and moved to **Sprint D**.

| # | Task | E | Est | Dep | Notes |
|---|------|---|-----|-----|-------|
| 47 | README **create**: pitch, stack, architecture, "how to run", Azure link | S | **✅ Aug 21** *(0.5d vs 2d est)* | -- | Highest ROI of anything left — no file existed. Writing rather than building, and `docs/` already held the content. Screenshots/GIFs are T38; the C4 diagram is T48. `feat(task-47)` |
| 48 | C4 **Container** diagram in Mermaid -> `README.md` | M | **✅ Aug 21** *(1d, on est)* | -- | Cheaper than it looks: the two architecture docs already hold the prose (100 lines, 0 diagrams) — this is drawing what's written. **Re-scoped Aug 21:** Context level cut — drawn, then dropped as redundant against the Container diagram. No new `docs/architecture.md`; it lives in the README and the docs link to it. `feat(task-48)` |
| 52 | Open redirect: reject a non-local `returnUrl` | S | **✅ Aug 31** *(2.5d vs 0.5d est)* | -- | `navigateByUrl` *parses* the value and never assigns `window.location`, so `https://evil.com` lands on `/not-found`. Kept as defence in depth against a future `window.location.href = returnUrl`, which no reviewer would read as a security change. Check at `login.component.ts:26` in `IsLocalUrl` shape; brings the repo's **first Angular unit test + a `npm test` CI step**. |
| BUG-19 | Coupon validated once, then trusted forever — by the order, the charge, and time | S | **✅ Aug 20** *(1.5d vs 0.5d est)* | -- | Found Aug 13 during BUG-18; **scope grew Aug 20** when all three cases were reproduced live (`docs/smoke-tests/sprint-c/bug-19-coupon-trust.md`). **(A)** `CalculateDiscount` read `AmountOff`/`PercentOff` off `cart.Coupon` → forged $999 discount on the order. **(B)** `ApplyDiscountAsync` re-read Stripe by `cart.Coupon.Id` — **also client-supplied** → $100 charged on a $185 order. **(C)** a coupon applied before its campaign was retired still paid out, and the order total matched the charge so nothing flagged. Fix (spike 1a/2d/3b/4b): both paths re-derive from `cart.Coupon.PromotionCode`; a failed lookup drops the coupon, nulls the intent and 409s. Two defects the fix would otherwise have introduced were caught first — `AmountOff / 100` truncated the cents, and a blank code dropped Stripe's `Code` filter. `fix(bug-19)` (#75) |
| BUG-20 | **Every coupon rejected — `GET /api/coupons/{code}` always 400** | S | **✅ Aug 20** | -- | `promotion.coupon` is expandable: unexpanded it arrives as a bare id, so Stripe.net leaves the `Coupon` object null and `CouponService.cs:22`'s guard never passes. Broke at `fix(deps)` #50 — **an SDK version pins the API version, so a dependency refresh was an API migration.** No test can reach it (no DI seam, `[ExcludeFromCodeCoverage]`). Fix: `Expand` + `Active = true`. `fix(bug-20)` |
| — | .NET dependency refresh to latest GA *(#74)* | S | **✅ Aug 20** *(0.5d, unplanned)* | -- | **Unplanned**, and the reason Aug 19–20 went sideways. Triggered by BUG-20, which proved a blanket "latest GA" bump can be an **API migration** the test suite cannot see. `Stripe.net` is now flagged never-blanket-upgradable and `xunit.v3` is **held at 3.2.2** — 4.0.0 makes Microsoft Testing Platform the default while .NET 10's `dotnet test` still routes via VSTest, so they disagree and the build errors. Policy written up in `docs/build-deployment.md` |
| 12 | ~~Cart ownership: userId in Redis~~ | — | **0** | -- | ✅ **CLOSED by BUG-18** — same work, already shipped and proven |
| — | Rotate the Azure SQL password + audit the server firewall | S | **✅ Aug 18** | -- | **Done first, as planned.** Password was `Pa$$w0rd` **and in git history**, so it stayed valid for four months after T3 "removed" it; three stale client-IP firewall rules also went. `skinet-2025-test-server` runs a weak password on a publicly resolvable host, so the server firewall is the only other control. New password → update the App Service `SqlServerAzureConnection` → restart → re-smoke. Never mid-deploy: rotating a live connection string is how a green sprint becomes a 500 |
| — | Sprint B Anki deck | — | **1d** | -- | 1 day per sprint |

**Points: 7 · Est 8.5d of 10 — 1.5d slack.** *(Committed at sprint start. Frozen — never re-summed as work completes or scope moves; the rows carry what actually happened.)*

**Sprint end (as closed Aug 31):** README live, C4 diagram published, open redirect closed, and the
coupon money path fixed twice over. Screenshots, indexes and the four demo defects **carried to Sprint D** —
the timebox ended, the epic did not. **Over by 6 days**; T52 alone ran 2.5d against a 0.5d estimate, all of
it gate.

---

## Sprint D — Demo Polish *(Aug 31 – Sep 8 · 7 working days)* · ✅ CLOSED Sep 12 *(T43 + T49 shipped; six rows re-homed across E, G and H)*

> Sprint C's remainder, given its own timebox. Everything a visitor clicks in the first two minutes,
> plus the index task already in flight. **The same theme as C on purpose** — C's timebox ended, its
> epic did not.
> Epics: `UX`, `Docs`.

| # | Task | E | Est | Dep | Notes |
|---|------|---|-----|-----|-------|
| 43 | DB indexes: BuyerEmail, composite (Brand,Type), Price | S | **✅ Sep 1** *(2d vs 0.5d est)* | -- | **One migration per provider.** `Products` has **zero** indexes today, so the shop grid scans. `Brand`/`Type`/`BuyerEmail` are `nvarchar(max)` — **`HasMaxLength` before `HasIndex`**, or the composite exceeds SQL Server's 1,700-byte key. The grid's default sort is `Name`, not `Price`. Spike: `spikes/sprint-d/t43-db-indexes.md` |
| 49 | `docker-compose.yml`: add **SQL Server** beside Redis + document it as the way to start both | S | **✅ Sep 1** *(shipped with T43)* | 47 | ⬅ **pulled forward from Sprint F** — T43's measurement needs a local SQL Server, and that container is this task. Compose already has `redis` + `sqlite`; what's missing is SQL Server and any mention in the docs, so `dotnet test` fails on a clean machine with no hint why. Developer edition, not Express — closer to Azure SQL than LocalDB |
| — | Sprint C Anki deck | — | **1d** | -- | 1 day per sprint |

**Delivered: T43 + T49.** Six rows re-homed across E, G and H.

**As closed Sep 12:** T43 and T49 shipped (PR #80 + two follow-ups). BUG-10 built, not merged. The
remaining six rows were re-homed, not carried as a block: BUG-10 + BUG-14 -> Sprint E · T33 cut Sep 12 ·
BUG-3 -> pull-in rank 5 · T38 -> Sprint H · deploy-gate cleanup -> Sprint H.

---

## Sprint E — Demo Polish + Money Paths *(Sep 12 – Sep 21)* · ⏳ CURRENT

> Everything a visitor touches in the first two minutes, and the two places their card can fail.
> Epics: `UX`, `Money`. **First sprint on the weekly cadence** — it absorbs Sep 12–14, already spent.
> **Must:** BUG-10 ✔, BUG-14 ✔, BUG-2 ✔, BUG-17 ✔ · **Should:** T58 ✔, T10 ✔, T35 ✔, T59, T60 ·
> **Could:** T61
> **Gate day at sprint end** — sized then, from the diff and the transcripts, not now.

| # | Task | Est | Dep | Notes |
|---|------|---------------------|-----|-------|
| BUG-10 | Top loading bar spins forever on live — the busy counter never returns to 0 | **✅ Sep 13** *(1d vs 0.25d est)* | -- | PR #83 → `61c900e`. The cause was the zoneless render path: a plain field notifies nothing, so the bar only cleared on the next click. `BusyService` is signal-based, the interceptor counts inside `defer`, 9 specs. Live smoke in `docs/smoke-tests/sprint-e/bug-10-loading-bar.md` |
| BUG-14 | Product page: "Add to cart" disables on click | **✅ Sep 13** *(0.25d, as est)* | -- | PR #85 → `c9e784f`. The click satisfied the `[disabled]` rule it had just changed — the cart did update, only the button looked broken. Split into an always-enabled add plus a cart-line stepper |
| BUG-2 | Checkout: a declined card can't be retried in-session | **✅ Sep 14** *(0.5d, as est)* | -- | Money path. PR #86 → `1aa8136`. `CreateOrderAsync` deleted the Redis cart one step before the charge, so the retry hit `POST /payments` with no cart and a 400 buried Stripe's decline. The cart now dies on payment success, and a spent intent is replaced instead of updated. `elements.submit()` is not single-use — disproved by the new Playwright spec. Stock and the orphaned order defer to T40/T41. Spike: `bug-2-declined-card-retry.md` |
| BUG-17 | Logout then hit `/checkout` (empty cart) → lands on `/cart`, not the login page | **✅ Sep 15** *(0.7d vs 0.4d est)* | 37 | PR #87 → `b7f6cd5`. All four guards return a `UrlTree`. The router subscribes every guard with `combineLatest` and picks by array order, so a guard that navigates itself cannot lose — the defect was the side effect, not the round trip. `emptycartGuard` and `adminGuard` stand down for anonymous visitors. 12 guard specs, the repo's first. Spike: `bug-17-logout-checkout-guard-race.md` |
| 58 | Interceptor cleanup: 409 cart reload out of `errorInterceptor`, delete the dead 401 skip | **✅ Sep 16** *(1.5d vs 1.0d est)* | BUG-16 | PR #88 → `dbc0126`. The live probe killed the `HttpContextToken` half: `user-info` answers 204, never 401, so the skip and `InitService`'s catch were dead code. The 409 recovery moved to the two services that own the endpoints. 2-sentence test not passed; concept owed at the gate |
| 10 | Health-check endpoints | **✅ Sep 17** *(0.7d vs 0.5d est)* | -- | PR #89 → `869560e`. `/health` runs no checks, so Azure's 60s probe costs 0 database wakes. `/health/ready` checks SQL + Redis on demand behind `HealthSettings:ReadyKey`, guarded before the endpoint so a scanner cannot spend a wake either — not Identity, which reads the table that is down. Tests pin the body and content type, because the SPA catch-all makes a 200 meaningless. **Azure `healthCheckPath` + the alert rule wait for the release.** Concept owed at the gate. Spike: `t10-health-check-endpoints.md` |
| 35 | `NgOptimizedImage` on catalog images | **✅ Sep 19** *(1.0d, as est)* | -- | PR #90 → `d01f3da`. 18 product files re-encoded 1024 → 800px WebP and 8 of 9 `<img>` sites moved to the directive; `priority` goes to the LCP element only. The home hero stays a plain `<img>` — every directive mode cost it 217 ms, measured. `/shop` 3440 → 1759 KB, CLS 0.092 → 0. Layout-shift half of the 2-sentence test owed at the gate. Spike: `t35-ngoptimized-images.md` |
| 59 | Static-file `Cache-Control` | ⚠️ **0.75–1.0d** | 35 | `app.UseStaticFiles()` takes no options, so nothing carries a `max-age` and freshness is each browser's guess. Three buckets: content-hashed `*-HASH.js/css` + `/media/**` → `immutable`; `index.html` → `no-cache`; `/images/**` → its own answer, because `PictureUrl` rows pin the filenames. **The header must also be set in `FallbackController`** — every SPA deep link serves `index.html` through `PhysicalFile`, which never sees `StaticFileOptions`. The same options carry the `image/webp` override for `/images/products/`. The test is the unmeasured half: `wwwroot` does not exist on CI, so the factory needs its own web root. Options live in `WebApplicationExtensions`. Spike: `t59-cache-control.md`. `fix/59-cache-control-static-files` |
| 60 | Scheduled weekly CI advisory run (`on: schedule`) | ⚠️ **0.25d** | -- | More of what already exists. `ci-cd.yml` has no `schedule` trigger and no audit step — `push` and `pull_request` only. NU1903 was caught while deploying, not by CI. `chore/60-scheduled-dependency-audit` |
| 61 | Prune unused client deps | ⚠️ **0.25d** | -- | All four confirmed present: `@vitejs/plugin-react` + `autoprefixer` (dev), `esbuild` + `@babel/runtime` (**runtime deps**). A React plugin in an Angular `package.json` is the one a reviewer notices. Check `vite` too — same category. Leave `jsdom`, `vitest`, `istanbul-lib-instrument`, `@angular/animations`, `postcss`. Verify with `ng build` + `ng test` + `ng lint`. `fix/61-prune-unused-deps` |
| — | **Gate — sprint end** (Anki consolidation straight after) | *sized on the day* | -- | 🔴 **Never estimated, never queued.** The list is derived on the gate morning from the sprint's diff and your own transcript turns, then split into full and light concepts (`mindset/the-gate.md`). It draws on whatever headroom the sprint has left |

**Open: 1.25–1.5d against Sep 20 — Sep 21 is the gate day.** T59 is the only Should still open; T60 and
T61 drop first if it runs long.
🔴 **T59 moves to Sprint F if it has not started by the end of Sep 19.** The gate does not slip for it —
nothing depends on it, and three concepts are already owed there.

**Sprint end:** nothing a recruiter clicks looks broken · a declined card retries · guards redirect the
way Angular says to · the app answers a probe truthfully · the two interceptor URL-string couplings gone.

---

## Sprint F — Order Domain *(Sep 22 – Sep 28)*

> The strongest domain story in the plan. **T40 splits cleanly at 3.5d** — a column change, then a new table.
> Epic: `Domain`. Order: 40a → 40b → 41; T21 is independent.
> **Must:** T40a, T40b · **Should:** T21, T41 · **Could:** —
> **Day-4 checkpoint (Sep 25):** T40a+T40b under 50% → T21 goes first, then T41 to Sprint G, that day.

| # | Task | Est | Dep | Notes |
|---|------|---------------------|-----|-------|
| 40a | **Split the enum:** payment status stays, `FulfillmentStatus` (Processing/Shipped/Delivered/Cancelled) is new | ✅ **1.5d** | -- | **Spike first: one enum or two?** Today's enum is payment-shaped (`Pending, PaymentReceived, PaymentFailed, PaymentMismatch, Refunded`), so fulfillment states would mix two concerns in one field. Footprint: **9 backend files** — `AdminController`, `OrderSpecification`, `OrderConfiguration`, `DashboardService`, `PaymentService`, `OrderMappingExtensions` (exposes the status as a string), `BogusSeeder`, `Order`, and the enum itself — plus the client union in `models/order/order.ts` and the filter array in `admin-orders.component.ts`. One migration per provider. Ships working Shipped/Delivered/Cancelled on its own |
| 40b | **Append-only `OrderStatusHistory`** + transition guard + admin status endpoint | ✅ **2d** | 40a | LEARN: state machine + audit trail — **this is the gate concept**, 40a is the wiring under it. New entity + EF config + one migration per provider. **Only two sites write the status today** — `AdminController.RefundOrder` and `PaymentService` — plus the default on creation, so there are few places to hook the history. `POST /orders/{id}/status` reuses `AdminController`'s existing shape. **T26 parks admin fulfillment control here.** |
| 21 | Coverage gate: coverlet, fail PR < 60% | ⚠️ **1.5d** | 19,1 | T52's exact shape: a tool the repo does not have plus a CI step, and T52 ran 0.5d → 2.5d. **Coverlet is not installed** — nothing in any test `.csproj` or in `tests/Directory.Build.props`. The row is the package, the collector switches, a threshold and a CI step |
| 41 | Order cancellation: POST cancel, only Pending/PaymentReceived, Stripe refund if paid | ⚠️ **0.9d** | 40b | Refund plumbing already exists — `AdminController.RefundOrder`, `IPaymentService.RefundPayment`, `PaymentService` — so this reuses it. Log the email, don't build the email service (T39 descoped). **Cancelling an unpaid `Pending` order is where the stock goes back** — BUG-2's deferred half, including the order orphaned when a spent intent is swapped mid-checkout |
| — | **Gate — sprint end** (Anki consolidation straight after) | *sized on the day* | -- | 🔴 **Never estimated, never queued.** The list is derived on the gate morning from the sprint's diff and your own transcript turns, then split into full and light concepts (`mindset/the-gate.md`). It draws on whatever headroom the sprint has left |

**Task work 5.9d of 7 — 1.1d headroom for the gate and the unplanned.**

**Sprint end:** order status split into two concerns · an append-only audit trail behind it · if Should
holds, cancellation refunds through Stripe.

---

## Sprint G — Production Hardening *(Sep 29 – Oct 5)*

> The last build sprint. Everything here is what separates "it runs on my machine" from "I can operate it".
> Epics: `Observability`, `Data`. Order: 22 → 42 → 23 → 20b (T23 needs T22).
> **Must:** T22, T42, T23 · **Should:** T20b · **Could:** —
> **Day-4 checkpoint (Oct 2):** Must under 50% → T20b goes to Sprint H, that day.

| # | Task | Est | Dep | Notes |
|---|------|---------------------|-----|-------|
| 22 | Serilog structured logging *(App Service logs, no Seq)* | ✅ **1.5d** | -- | LEARN: "why not string interpolation?". Zero Serilog references today. **5 log call sites in 2 files** (`PaymentsController`, `ExceptionMiddleware`), already written as `[LoggerMessage]` source-generated templates — so the row is package refs, a sink config and the concept, not 4d. If the live demo breaks there is nothing to diagnose with today |
| 42 | Soft delete + EF global query filter | ✅ **1.5d** | -- | LEARN: "soft delete in EF Core?". No `IsDeleted` or `HasQueryFilter` anywhere. **One hard-delete site** — `ProductsController.DeleteProduct` → `GenericRepository.Delete`. `ProductItemOrdered` snapshots a bare `ProductId` with **no navigation to `Product`**, so the required-navigation cascade that makes query filters expensive does not exist here. One migration per provider |
| 23 | Correlation IDs — one id per request, on every log line | ⚠️ **0.3d** | 22 | Answers "how do you trace one request across logs?" for a third of a day. **Absorbs pull-in rank 2** — `ApiErrorResponse` carries `StatusCode`/`Message`/`Details` and no id, and `TraceIdentifier` appears nowhere in `src/`. Same feature; doing it later means touching `ExceptionMiddleware` twice |
| 20b | Extend the API integration harness: auth, orders, payments happy paths | ⚠️ **1.2d** | -- | `CustomWebApplicationFactory` already carries **three** suites (`CartOwnershipTests`, `CouponTrustTests`, `HealthEndpointTests`), and `StubCouponService` shows the stubbing pattern. Auth and orders are cheap; **payments needs a Stripe stub that does not exist yet** — that unmeasured half is why this is the Should that drops. ⚠️ The suites need Redis up, or they fail on `RedisConnectionException` rather than on your code. TestContainers stays cut |
| — | **Gate — sprint end** (Anki consolidation straight after) | *sized on the day* | -- | 🔴 **Never estimated, never queued.** The list is derived on the gate morning from the sprint's diff and your own transcript turns, then split into full and light concepts (`mindset/the-gate.md`). It draws on whatever headroom the sprint has left |

**Task work 4.5d of 7 — 2.5d headroom for the gate and the unplanned.**
⚠️ Two LEARN rows land here (T22, T42) — likeliest week to need a full gate day.

**Sprint end — the last *build* sprint:** structured logs with correlation ids · soft delete behind a
global filter · everything after this is release work.

---

## Sprint H — Freeze + Release *(Oct 6 – Oct 12 · ship target Oct 8)*

> **Deliberately near-empty — reserved capacity, not spare scope.** Whatever moved down the ladder in
> Sprints E–G lands here; if nothing moved, v1.0 ships **Oct 8** and the rest of the week is buffer.
> Epic: `Release`. 🔴 **Freeze Oct 6** — nothing new starts; rehearsal, fixes and the release only.

| # | Task | Est | Dep | Notes |
|---|------|---------------------|-----|-------|
| — | Overflow from Sprints E–G | **4.4d reserved** | -- | Whatever the day-4 checkpoints dropped — T20b is the likeliest. If E–G landed clean, this is buffer, not new scope |
| — | Demo rehearsal × 2 — the 5-minute walkthrough, out loud, on live | **1d** | -- | The first run always finds something. Run it twice, on different days, and fix what it surfaces |
| 62 | Deploy-gate cleanup: retire the four legacy manual checks in `testing.md` | ⚠️ **0.6d** | 20b | **Depends on 20b** — T17 price change, T26 payment sync and BUG-11 move *into* that harness, BUG-12 into Playwright. Only then delete the *Legacy checks with no file yet* table in `docs/testing.md` |
| 38 | README UX screenshots + GIFs | ⚠️ **0.5d** | 47 | Three placeholders wait in `README.md` — catalogue screenshot, checkout GIF, admin-refund GIF. First thing after the freeze, on purpose: the UI has stopped moving, which is what the freeze guarantees |
| — | v1.0 release: `development` → `main`, one squashed commit, verify on Azure | **0.5d** | -- | **Pinned last — pushing `main` deploys.** Run the full deploy gate in `docs/testing.md` first, including T10's `healthCheckPath` and its alert rule |
| — | **Final deck consolidation** | *sized on the day* | -- | If the gate morning finds no concept in the sprint's diff or transcripts, **Sprint H skips the gate entirely** (`the-gate.md`) and only the deck pass runs: merge the sprint decks, dedupe across them, order by difficulty. Consolidation, not generation — `skinet/questions/interview-gen-prompt.md` |

**Task work 2.6d of 7 — 4.4d reserve.** 🔴 **Last buffer in the plan. Reserved capacity, not spare scope.**

**Sprint end: v1.0 ships Oct 8**, Oct 9–12 is float. Then 30 days to rehearse against a frozen app
instead of building against a moving one — which is the whole point of pulling the date in.

---

## Buffer + Interview Prep *(Oct 13 – Nov 11 · 30 days)* — 🔴 **DO NOT FILL**

> The run-up to **Nov 11**, against an app frozen since Oct 6. Pulling the ship date in turned a 6-day
> window into a **30-day** one — that runway is the point, and it is not scope to fill.

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 53 | Final Azure smoke test: full user + admin journey | S | -- | **Non-negotiable. Always last.** |

Everything else in this window: **full interview deck consolidation · re-read every
gate doc · mock questions · slack for slippage.**
**Sprint H's 4.4d reserve absorbs slippage now, not this window** — that is the whole point of
leaving H half empty. **No feature work lands here.**

---

## Pull-in candidates *(if a sprint finishes early)*

> **Not committed work.** Fires only when a sprint's committed scope is done **with days left in that
> sprint's own timebox**.
> **Ranked against the code**, by one question: does it make committed work cheaper, does a
> visitor see it, or is it only tidy? Ranks 1–3 are not really pull-ins — they ride inside a committed PR
> that is already in the same files, so they cost a fraction of what they cost alone.
> **One item, then re-assess.** Never "I have 3 days, take four."
> 🔴 **Buffer + Interview Prep (Oct 13 – Nov 11) is never the window**, and neither is Sprint H's 4.4d reserve.
> **Everything here is a known defect or cleanup — never a new feature.** That line is the whole rule.

| Rank | # | Task | Est | Pull into | Notes |
|---|---|------|-----|-----------|-------|
| **1** | 57 | Dev database = SQL Server: retire SQLite, one provider and one migration set | **2.5d** | 🔴 **Don't — revisit after v1.0** | ⚠️ **It does not pay for itself.** The case was the migration tax on T40 and T42; measured, the whole remaining tax is **three duplicated migrations**, well under an hour. 2.5d to save that is a bad trade. It would delete `SqliteStoreContext`, `SqliteContextFactory` and `Migrations/SQLite/`, and close the dev/prod gap that hid T18's missing index for four months — but **`CustomWebApplicationFactory` is SQLite-backed**, so CI would need a SQL Server service container, and T20b builds on that harness |
| **2** | fix/error-correlation-id | 500 responses carry no correlation id | **0.1d inside T23** | **Sprint G — same PR as T23** | Not a separate task. `ApiErrorResponse(int statusCode, string message, string? details)` has no id field and `TraceIdentifier` appears nowhere in `src/`. T23 already wires one id per request through the logs; this is the response half of that same feature. Doing it alone later means touching `ExceptionMiddleware` twice |
| **2b** | fix/409-conflict-payload | A 409 doesn't say **which** conflict it is, and withholds the cart it just re-priced | ⚠️ **0.3d inside T23** | **Sprint G — same PR as T23 + rank 2** | Same file and contract as rank 2, so one review. Two exceptions share 409 (`ExceptionMiddleware.cs:35-42`) with only prose to separate them, so the client branches on *which service called*; RFC 9457 `type` fixes that. Both throw sites `SetCartAsync` before throwing, so the client GETs a cart the server just had. Not sooner: `ApiErrorResponse` is on every error path, and no visitor sees this |
| **4** | fix/cart-delivery-fee | Cart shows a delivery fee carried over from an **abandoned** checkout | **0.3d** | **Sprint E, in the gate's headroom** | `cart.service.ts` prices `shipping` off the in-memory `_selectedDelivery`, cleared only on a completed order — so pick a delivery method, abandon checkout, go back to the cart, and the fee is still there. **Display only** (the server prices from `cart.DeliveryMethodId`), but it is on the path a recruiter clicks, which is exactly Sprint E's theme |
| **5** | BUG-3 | Checkout: explain why **Next** is disabled on the Address step | **0.25d** | **Sprint E, in the gate's headroom — confirm on live first** | The step is Stripe's Address Element, which renders its own field errors, and `[disabled]="!completionStatus().address"` follows that element's `change` event. It may not be a defect at all — five minutes on live decides whether this is a row or a deletion |
| **6** | fix/T27 | Dashboard preview sizes: recent orders 5→15, low-stock 10→15 | **0.25d** | **Sprint H, pre-freeze** | `DashboardService` holds `RecentOrdersSize = 5` and `LowStockListSize = 10`; the admin dashboard looks half-empty on a seeded database. Two constants + two test asserts. Demo-facing, so it belongs with the screenshots, not before them |
| **7** | 2 | Multi-stage Dockerfile | **2d, plus whatever its gate costs** | **Sprint H, only if the reserve is untouched** | Confirmed greenfield — there is **no Dockerfile** in the repo, only `docker-compose.yml`. The last remaining LEARN row, so it carries real interview signal ("how do you cut image size?"), but Azure deploys without it: pure signal, zero demo value, and it costs a gate day on top of the build |
| **8** | fix/T29 | Unify the paging param name (`pageIndex` vs `pageNumber`) | **0.3d** | **Doesn't matter** | Live in three files and already *documented* by comments in both admin components (`shop.service.ts` maps `pageNumber` → `pageIndex`). BUG-12 fixed the real break; what is left is naming. No visitor sees it, no interviewer asks about it |
| **9** | fix/T30 | `register`/`login` template-only members → `protected` | **0.2d** | **Doesn't matter** | Cosmetic, zero runtime effect. Leaves `product-form` the last file off the Angular style guide |
| **10** | fix/angular-animations-deprecation | `provideAnimationsAsync()` is deprecated, removal intended in v23 | **0.5d** | **🔴 Don't** | `app.config.ts:34`. Deprecated in 20.2, but **v22 works and no Angular major lands before Nov 11**. Replacing a working provider now is risk with no payoff — this is a v23-upgrade task, not a v1.0 task |
| **11** | docs | Spike retrofit — `Why (best answer)` on ~11 decision blocks; fill 4 `<your call>` Decisions | **1d+** | **Gate day, never a sprint** | Blocked until the user writes `Why (by me)` first — Claude writing it spends the attempt, which is the whole point of the exercise |

---

## Descoped for v1.0 — *not shipping by Oct 8*

> Cut consciously. **Not forgotten** — revisit after v1.0 ships.
>
> **T33 (`<mat-error>` on the checkout forms) — cut:** there are no checkout forms. The four steps
> are Stripe's Address Element, a `mat-radio-group`, Stripe's Payment Element and a review panel — and
> `mat-error` needs a `matInput` inside a `mat-form-field`. The row was specced from an assumption the code
> never matched.

| # | Task | Why cut |
|---|------|---------|
| 55 | Lighthouse baseline | Scores don't get you hired |
| 56 | Performance pass (MiniProfiler + bundle analyzer) | Depends on 55; premature at this scale |
| 34 | Skeleton loaders | Polish |
| 32 | Memory leak audit | "Likely minimal (signals + httpResource)" — plan's own words |
| 39 | Email: IEmailService + `Channel<T>` BackgroundService | Expensive (M+). Stripe + webhooks + SignalR already prove the async/infra story. **Best remaining LEARN item if a whole sprint frees up** |
| 46 | Unit tests for cancellation/soft-delete/history | Not a task — T40/41/42 each carry their own tests inline |
| 20 | TestContainers | The `WebApplicationFactory` half ships (BUG-18 + T20b, Sprint G). Containers add setup cost, not interview signal |
| 49 | docker-compose **with Seq** | Redis + SQL shipped in Sprint D with T43; Seq stays cut — App Service logs are enough |
| 51 | RequireConfirmedEmail | Blocked on 39 |
| 11 | Pagination: TotalPages/HasNext/HasPrev | Cosmetic |
| 36 | Accessibility: axe pass | Real value, no interview signal for these roles |
| 50 | API docs: `[ProducesResponseType]` + XML summaries | Scalar already renders the API (`Program.cs:46`); this only adds response shapes to a UI that works. 2d against CS1591 on a zero-warning repo, for something no recruiter clicks and no interviewer asks to see. Contracts are already answerable from FluentValidation, `ApiErrorResponse` and the problem-details middleware |

> **Dependency note:** 22 (Serilog) is committed to Sprint G, which is what un-blocked 23 (correlation IDs) and
> the Redis+SQL half of 49. 51 (RequireConfirmedEmail) stays blocked on 39.

---

## Progress

**Status lives in `skinet/progress.md`** — task checkboxes + daily log. Not duplicated here.

## Certifications *(never steals build days or run days)*

Capacity: **none until Oct 8.** The 2 days off a week are gone — every day is a project day now, so the
first real study window is the Oct 13 – Nov 11 buffer, alongside interview prep.

- [ ] **AZ-900** — **exam target ~Nov 11** *(moves with the interview; Sep 12 re-baseline)*. Study in the
  Oct 13 – Nov 11 buffer, never in a build week. Mostly review (Azure deploy + Key Vault already done).
  ⚠️ That still lands the same week as the interview — if the week gets tight, the exam moves, not the interview.
- [ ] **AZ-204** — **exam target ~early Jan 2027** (~1.5 months after AZ-900). Study starts once v1.0 ships
  (Oct 8). The project IS the lab — Key Vault, App Service, Managed Identity, App Insights.
  The portfolio gets interviews; the cert supports it. Not the other way round.

## When behind (life buffer)

**Oct 8 is the date and it does not move.** The hard commitment is: ship with a working Azure deploy + a
great README. When something eats time, **drop a tier** — never extend hours.

Escalation, in order:

1. **Drop Could, then Should** in the running sprint — at the day-4 checkpoint, or the day the week goes bad.
2. **Push to Sprint H's 4.4d reserve.**
3. Only if both are gone: the scope conversation, in writing, in `progress.md`.

**Never cut:** T47 README (shipped), T40a, BUG-2 (shipped), T53. **Never raid:** the Oct 13 – Nov 11 buffer, sleep, runs.

**Committed vs capacity:**

**Task work only — the gate is never pre-costed** (`mindset/the-gate.md`). Each sprint's headroom is what
its gate day draws on, sized on the gate morning from the diff and the transcripts.

| Window | Days | Task work | Headroom *(gate + unplanned)* |
|---|---|---|---|
| Sprint E · Sep 15–21 | 7 | 4.6d | 2.4d |
| Sprint F · Sep 22–28 | 7 | 5.9d | 1.1d |
| Sprint G · Sep 29 – Oct 5 | 7 | 4.5d | 2.5d — *two LEARN rows; likeliest full gate day* |
| Sprint H · Oct 6–12 *(ship Oct 8)* | 7 | 2.6d | **4.4d reserved** |
| **Build total** | **28** | **17.6d** | **10.4d (37%)** |
| Buffer + Interview Prep · Oct 13 – Nov 11 | 30 | T53 + deck | — |

Every sprint since A has produced unplanned bugs; this budget assumes that happens again. **Slack is
protection, not capacity** — the sprint that spends it is the one that puts T40 or the release at risk.

**A sprint that looks slack is re-costed before it is re-filled.** Apparent headroom is usually rows
priced by T-shirt rather than against the code. Pulling extra work in on top of an unpriced bill is how
a comfortable week becomes a lost one — and dropping one row at the day-4 checkpoint costs less than
holding days idle from Monday to insure against the same risk.

**What the re-slice bought, and from where:** the ship date moved **26 days**, and **almost none of it
came from working more hours.** ~7.5d from re-measuring the three biggest rows against the code
(T40/T22/T42), and the rest from 7 calendar days a week instead of 5 — partly given back by re-costing
Sprint E up by 3.25d, which is a number the old plan was hiding rather than a new cost. Clock hours were
never the constraint — `mindset/energy-protocol.md` says so and it was right.

Every sprint end: E2E run, Azure verify, README update.

## Tech Stack Additions

| Sprint | Added |
|--------|-------|
| 1 | Azure Key Vault |
| 2 | NWebsec |
| 3 | FluentValidation, Bogus |
| A | ESLint (`angular-eslint`) + Prettier, Roslyn analyzers (`AnalysisMode=Recommended`). Angular -> 22.0.6 stable |
| B | Playwright, `WebApplicationFactory` |
| C | Mermaid |
| D | docker-compose (Redis + SQL Server) |
| E | ASP.NET health checks, `NgOptimizedImage`, Coverlet *(T21 installs it — not present today)* |
| F | — |
| G | Serilog (App Service logs, no Seq), EF global query filters |
| H | — |
| *if pulled in* | Docker multi-stage |
| ~~descoped~~ | ~~MailKit / Azure Comms, TestContainers~~ |

## LEARN Summary (10 committed · 1 more if pulled in)

| # | Concept | Interview Question |
|---|---------|-------------------|
| 2 | Docker multi-stage | "Optimize Docker size/build?" — *pull-in candidate* |
| 3 | Config providers | "Manage secrets in .NET?" |
| 8 | FluentValidation | "Annotations vs FluentValidation?" |
| 13 | Optimistic concurrency | "Concurrent writes in EF Core?" |
| 14 | Transaction isolation | "Isolation levels? Serializable?" |
| ~~20~~ | ~~Integration testing~~ | ~~"Testing pyramid? TestContainers?"~~ **descoped** — T37 (E2E) carries this story |
| 22 | Structured logging | "Why not interpolation?" — **Sprint G** |
| 25 | Angular lazy loading | "Route structure in large app?" |
| 37 | E2E testing | "Strategy? Page Object Model?" |
| 54 | Static analysis (ESLint / Roslyn) | "Linter vs formatter? When is the linter *wrong*?" |
| ~~39~~ | ~~Channel\<T\> + BackgroundService~~ | ~~"Background jobs without Hangfire?"~~ **descoped** |
| 40b | State machine + audit | "Model order status changes?" — **Sprint F** |
| BUG-17 | Guards return a destination | "Why not `navigate()` + `false` in a guard?" — **Sprint E** |
| 42 | Global query filters | "Soft delete in EF Core?" — **Sprint G** |

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

> **This table is a Sprint 1–3 artifact.** Several rows name sprints that no longer exist (S5, Sprint 6),
> tasks that were descoped (20, 39), or risks already resolved (CSP/Material, secrets rotation, admin
> refactor). Kept as the record of what was feared up front — **not** a live risk register.

## Skipped

OAuth2/JWT, multi-currency, i18n, K8s, GitOps, CSRF, Prometheus/Grafana/Jaeger/OTel, Outbox, Service Bus, CQRS, Domain events, PWA, SSR, file upload, and more.

*(“Angular unit tests” is no longer skipped — T52 brought the first Vitest specs and `npm test` into CI.)*

---
