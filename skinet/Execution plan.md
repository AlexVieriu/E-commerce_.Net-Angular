# Execution Plan

> .NET 10 + Angular 22 | Azure-only | Solo dev | B2C physical goods
> **Milestone: `v1.0 — Interview Ready` · Oct 8, 2026 — 🔴 FIXED, NEVER MOVES** · interview Nov 11, 2026
> **Freeze Oct 6:** no new work after that date — rehearsal, fixes and the release only.
> Mid .NET / junior Angular | CV project | Claude Code for speed, saved time for learning

**Schedule (re-sliced 2026-09-15):** 7 days a week, 10 working hours a day (12h − 2h break).
**1 plan-day = 1 calendar day. A sprint is one week: 7d capacity, 6d committed, 1d slack.**
The date moved **in**, from Nov 3 to **Oct 8** — never out. Nov 3 is the outer bound, not the target.

**Estimates:** every row is in days.
- ✅ **measured** — re-sized against the code. ⚠️ **unmeasured** — a T-shirt guess × a multiplier, never
  checked. 🔴 — unmeasured *and* big enough to blow a week.
- **The blow-out rule, from this repo's own actuals:** a row that touches **something the repo has never
  done before** runs **3–5× its estimate** — T52 0.5d→2.5d (first Angular spec + a CI step) · T43
  0.5d→2d (`HasMaxLength` + per-provider migrations) · BUG-10 0.25d→1d (zoneless render path) · BUG-19
  0.5d→1.5d. A row doing **more of what already exists** lands on estimate — T48, BUG-14, BUG-2 all did.
- **Big T-shirt rows fail the other way.** The three largest were checked against the code on 2026-09-15
  and all came in at ~half: T40 6d→3.5d (and split) · T22 4d→1.5d · T42 4d→1.5d. **14d → 6.5d.**
- **So: cost novelty up, cost bulk down, and measure before committing a week to either.**
- **Mechanical ×1.2** — wiring, templates, config, docs. **Concept ×2** — work introducing something new.
- **Gate: one day per sprint, at sprint end** (`mindset/the-gate.md`), Anki consolidation straight after.
  🔴 **Never estimated in advance, and never queued.** The list is built on the gate morning from the
  sprint's diff and your own transcript turns; only then does it split into full and light concepts,
  capped at 2 full + 2 light. So the rows below cost out **task work only** — each sprint's remaining
  headroom is what the gate draws on, and a purely mechanical sprint skips it for the deck pass alone.
- **Scope ladder per sprint: Must / Should / Could.** Only **Must** is committed.
- **Mid-sprint checkpoint is day 4** (was day 5 on the 10-day sprints): Must under 50% → drop Could, then
  Should, that day. Day 5 of 7 is 71% through — too late to cut.
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
> **Re-costed 2026-09-15:** same eight rows, honest estimates. The week did not grow; the estimates
> stopped under-selling it (3.65d → 6.9d). Nothing was pulled in from another sprint.
> **Must:** BUG-10 ✅, BUG-14 ✅, BUG-2 ✅, BUG-17 · **Should:** interceptor cleanup, T10, CI schedule, T35, T21 · **Could:** T50, dep prune
> **Gate day at sprint end** — sized then, from the diff and the transcripts, not now.
> **Day-4 checkpoint (Sep 18):** Must under 50% → **T50 goes first**, and the week gets 2d back.

| # | Task | E | Est | Dep | Notes |
|---|------|---|-----|-----|-------|
| BUG-10 | Top loading bar spins forever on live — the busy counter never returns to 0 | S | **✅ Sep 13** *(1d vs 0.25d est — gate included)* | -- | Shipped #83 -> `61c900e`. `BusyService` is signal-based, the interceptor counts inside `defer`, 9 specs green; the live smoke is recorded in `docs/smoke-tests/sprint-e/bug-10-loading-bar.md`. The cause was the zoneless render path — a plain field notifies nothing, so the bar only cleared on the next click |
| BUG-14 | Product page: "Add to cart" disables on click — reads as broken though the cart did update | S | **✅ Sep 13** *(0.25d, as est)* | -- | `product-details.component.html:22` — `[disabled]="quantity() === quantityInCart()"`. `getButtonText()` already exists for the label, so this is one template decision. `fix(bug-14)` |
| BUG-2 | Checkout: a declined card can't be retried in-session | S | **✅ Sep 14** *(0.5d, as est)* | -- | **Money path. Shipped #86 → `1aa8136`.** `CreateOrderAsync` deleted the Redis cart when the order committed — one step before the charge — so the retry hit `POST /payments` with no cart and got a 400 *"Cart unavailable"* that buries Stripe's decline. Fixed by deleting the cart on payment success and replacing the PaymentIntent once Stripe has finished with the old one; `elements.submit()` turned out **not** to be single-use, disproved by the new Playwright retry spec. The `Pending` order and its stock defer to T40/T41, as does the order orphaned when an intent is replaced. Spike: `mindset/spikes/sprint-e/bug-2-declined-card-retry.md` |
| BUG-17 | Logout then hit `/checkout` (empty cart) → lands on `/cart`, not the login page | S | ✅ **0.4d** *(held — built Sep 15)* | 37 | Four guards return a `UrlTree` instead of `navigate()` + `false`. The router subscribes every guard with `combineLatest` and picks the answer by array order, so a guard that navigates itself cannot lose — the defect is the side effect, not the round trip. `emptycartGuard` and `adminGuard` also stand down for anonymous visitors, so the wrong reason never reaches the login page. Rider: 401 joins the 409 skip at `checkout.component.ts:120`. First guard specs in the repo (`guards.spec.ts`). Spike: `mindset/spikes/sprint-e/bug-17-logout-checkout-guard-race.md`. `fix(bug-17)` |
| — | Interceptor cleanup: cart-reload out of `errorInterceptor` + `HttpContextToken` for the 401 | S | ⚠️ **1.0d** | BUG-16 | **Re-costed: `HttpContextToken` is new to this repo** — that is the novelty marker that has cost 3–5× before. **One PR, not two** — both live in `error.interceptor.ts` lines 26–50 (401 at 26–33, 409 at 37–50); splitting them just rebases one onto the other. Kills two URL-string signals that break silently on a route rename. Absorbs pull-in rank 3 (`err.error?.…` derefs) — same file, near-free |
| 10 | Health-check endpoints | S | ⚠️ **0.5d** | -- | **Re-costed: a new package and a new endpoint**, plus the Azure probe config to point at it. Confirmed absent — no `AddHealthChecks` anywhere. The serverless-SQL idle-pause history (BUG-8) makes a probe worth having |
| — | Scheduled weekly CI advisory run (`on: schedule`) | S | ⚠️ **0.25d** | -- | **Held** — more of what already exists in `ci-cd.yml`, which runs only on `push` / `pull_request`. ~15 lines. NU1903 was caught while deploying, not by CI. `chore/scheduled-dependency-audit` |
| 35 | `NgOptimizedImage` on catalog images | S | ⚠️ **1.0d** | -- | **Re-costed: a directive the repo has never used, across 7 sites, each needing a visual check.** Seven product `<img>` sites (nine in all — the header logo and the home hero are not catalog). Most are Tailwind-sized (`h-20 w-20`, `w-full`), but `product-item.component.html` uses `mat-card-image` with no size classes at all, and the directive needs explicit width/height or `fill` |
| 21 | Coverage gate: coverlet, fail PR < 60% | S | ⚠️ **1.5d** | 19,1 | **Re-costed: this is T52's exact shape — a tool the repo does not have, plus a CI step. T52 ran 0.5d → 2.5d.** **Coverlet is *not* installed** — no reference in any test `.csproj` or in `tests/Directory.Build.props`. So this row is the package, the collector switches, a threshold and a CI step |
| 50 | API docs: `[ProducesResponseType]` + XML summaries | M | 🔴 **2.0d** | -- | **Re-costed, and the first row the checkpoint drops.** 27 endpoints across 7 controllers, none annotated. **The analyzer gate is the real cost:** turning on `GenerateDocumentationFile` raises CS1591 on every undocumented public member, and this repo runs at zero warnings. ⚠️ **Cut candidate, not just a drop candidate** — no recruiter clicks it and no interviewer asks about it |
| — | Prune unused client deps: `@vitejs/plugin-react`, `esbuild`, `@babel/runtime`, `autoprefixer` | S | ⚠️ **0.25d** | -- | **Held** — removal plus a verify run, all of it familiar ground. A **React** plugin in an Angular `package.json` is the one a reviewer notices; the other three are transitives declared as direct deps. **Leave alone:** `jsdom` / `vitest` / `istanbul-lib-instrument`, `@angular/animations`, `postcss`. Verify with `ng build` + `ng test` + `ng lint`. `fix/prune-unused-deps` |
| — | **Gate — sprint end** (Anki consolidation straight after) | — | *sized on the day* | -- | 🔴 **Never estimated in advance.** Nothing is queued: on the gate morning the list is derived from the sprint's diff and your own transcript turns, and only then does it split into full and light concepts (`mindset/the-gate.md`). It draws on whatever headroom the sprint has left |

**Task work 6.9d of 7 — 0.1d headroom, and the gate comes out of that too.** *(BUG-10/14/2 already spent, not re-counted.)*
⚠️ **Tightest week in the plan.** Drop T50 at the day-4 checkpoint → 4.9d of work, 2.1d for the gate.

**Sprint end:** nothing a recruiter clicks looks broken · a declined card retries · guards redirect the
way Angular says to · coverage gated in CI.

---

## Sprint F — Order Domain *(Sep 22 – Sep 28)*

> The strongest domain story in the plan. **T40 was 6d on a T-shirt guess; measured against the code it
> is 3.5d and splits cleanly** — a column change, then a new table.
> Epic: `Domain`. Order: 40a -> 40b -> 41.
> **Must:** T40a, T40b · **Should:** T41 · **Could:** —
> **Day-4 checkpoint (Sep 25):** T40a+T40b under 50% → T41 goes to Sprint G, that day.

| # | Task | E | Est | Dep | Notes |
|---|------|---|-----|-----|-------|
| 40a | **Split the enum:** payment status stays, `FulfillmentStatus` (Processing/Shipped/Delivered/Cancelled) is new | M | ✅ **1.5d** | -- | **Spike first: one enum or two?** The current enum is payment-shaped (`Pending, PaymentReceived, PaymentFailed, PaymentMismatch, Refunded`), so fulfillment states would mix two concerns in one field. Measured: **15 lines** of reads across 8 backend files — `AdminController:44,51,66` · `OrderSpecification:47,71,93` · `DashboardService:25,28` · `PaymentService:52` · `OrderConfiguration:14` · `BogusSeeder:27-32` — plus one client union (`order.ts:7`) and one filter array (`admin-orders.component.ts:43-46`). One migration per provider. Ships working Shipped/Delivered/Cancelled on its own |
| 40b | **Append-only `OrderStatusHistory`** + transition guard + admin status endpoint | M | ✅ **2d** | 40a | LEARN: state machine + audit trail — **this is the gate concept**, 40a is the wiring under it. New entity + EF config + one migration per provider; history written at the 5 existing status-write sites; `POST /orders/{id}/status` reuses `AdminController`'s existing shape. **T26 parks admin fulfillment control here.** Splits from 40a at a real seam: 40a is a column, 40b is a table |
| 41 | Order cancellation: POST cancel, only Pending/PaymentReceived, Stripe refund if paid | M | ⚠️ **0.9d** | 40b | Refund plumbing already exists (`AdminController`, `PaymentService`, `IPaymentService`) — this reuses it. Log the email, don't build the email service (T39 descoped). **Cancelling an unpaid `Pending` order is where the stock goes back** — BUG-2's deferred half, including the order orphaned when BUG-2's spent-intent check swaps a customer's intent mid-checkout |
| — | **Gate — sprint end** (Anki consolidation straight after) | — | *sized on the day* | -- | 🔴 **Never estimated in advance.** Nothing is queued: on the gate morning the list is derived from the sprint's diff and your own transcript turns, and only then does it split into full and light concepts (`mindset/the-gate.md`). It draws on whatever headroom the sprint has left |

**Task work 4.4d of 7 — 2.6d headroom for the gate and the unplanned.**

**Sprint end:** order status split into two concerns · an append-only audit trail behind it · if Should
holds, cancellation refunds through Stripe.

---

## Sprint G — Production Hardening *(Sep 29 – Oct 5)*

> The last build sprint. Everything here is what separates "it runs on my machine" from "I can operate it".
> **Both big rows re-measured down:** T22 4d→1.5d, T42 4d→1.5d.
> Epics: `Observability`, `Data`. Order: 22 -> 23 -> 42 -> 20b.
> **Must:** T22, T23, T42 · **Should:** T20b · **Could:** —
> **Day-4 checkpoint (Oct 2):** Must under 50% → T20b goes to Sprint H, that day.

| # | Task | E | Est | Dep | Notes |
|---|------|---|-----|-----|-------|
| 22 | Serilog structured logging *(App Service logs, no Seq)* | M | ✅ **1.5d** | -- | LEARN: "why not string interpolation?". Confirmed absent — zero Serilog references. Measured: only **8** `ILogger` call sites, in **2** files (`PaymentsController`, `ExceptionMiddleware`), so the row is package refs + a sink config + 8 message templates + the concept. Not 4d. If the live demo breaks there is nothing to diagnose with today |
| 23 | Correlation IDs — one id per request, on every log line | S | ⚠️ **0.3d** | 22 | Answers "how do you trace one request across logs?" for a third of a day. **Absorbs pull-in rank 2** — `ApiErrorResponse` has no id field and `TraceIdentifier` appears nowhere in `src/`; the response half is the same feature, and doing it later means touching `ExceptionMiddleware` twice |
| 42 | Soft delete + EF global query filter | M | ✅ **1.5d** | -- | LEARN: "soft delete in EF Core?". Confirmed gap — no `IsDeleted` or `HasQueryFilter` anywhere. Measured: **one** hard-delete site (`ProductsController.cs:76`), and `ProductItemOrdered.cs:5` snapshots a bare `ProductId` with **no navigation to `Product`** — so the required-navigation cascade that makes query filters expensive does not exist here. One migration per provider |
| 20b | Extend the API integration harness: auth, orders, payments happy paths | S | ⚠️ **1.2d** | -- | `CustomWebApplicationFactory` exists with two suites on it (`CartOwnershipTests`, `CouponTrustTests`), and `StubCouponService` shows the stubbing pattern. Auth and orders are cheap; **payments needs a Stripe stub that does not exist yet** — that unmeasured half is why this is the Should that drops. ⚠️ These suites need Redis running (docker-compose); without it they fail on `RedisConnectionException`, not on your code. TestContainers stays cut |
| — | **Gate — sprint end** (Anki consolidation straight after) | — | *sized on the day* | -- | 🔴 **Never estimated in advance.** Nothing is queued: on the gate morning the list is derived from the sprint's diff and your own transcript turns, and only then does it split into full and light concepts (`mindset/the-gate.md`). It draws on whatever headroom the sprint has left |

**Task work 4.5d of 7 — 2.5d headroom for the gate and the unplanned.**
⚠️ Two LEARN rows land here (T22, T42) — likeliest week to need a full gate day.

**Sprint end — the last *build* sprint:** structured logs with correlation ids · soft delete behind a
global filter · everything after this is release work.

---

## Sprint H — Freeze + Release *(Oct 6 – Oct 12 · ship target Oct 8)*

> **Deliberately near-empty — reserved capacity, not spare scope.** Whatever moved down the ladder in
> Sprints E–G lands here; if nothing moved, v1.0 ships **Oct 8** and the rest of the week is buffer.
> Epic: `Release`. 🔴 **Freeze Oct 6** — nothing new starts; rehearsal, fixes and the release only.

| # | Task | E | Est | Dep | Notes |
|---|------|---|-----|-----|-------|
| — | Overflow from Sprints E–G | — | **4.4d reserved** | -- | Whatever the day-4 checkpoints dropped — T50 and T20b are the likeliest. If E–G landed clean, this is buffer, not new scope |
| — | Deploy-gate cleanup: retire the four legacy manual checks in `testing.md` | S | ⚠️ **0.6d** | 20b | **Depends on 20b** — T17 price change, T26 payment sync and BUG-11 move *into* that harness, BUG-12 into Playwright. Only then delete the table at `docs/testing.md:50` |
| 38 | README UX screenshots + GIFs | S | ⚠️ **0.5d** | 47 | Three placeholders wait at `README.md:12,116,117`. First thing after the freeze, on purpose — the UI has stopped moving, which is what the freeze guarantees |
| — | Demo rehearsal × 2 — the 5-minute walkthrough, out loud, on live | — | **1d** | -- | The first run always finds something. Run it twice, on different days, and fix what it surfaces |
| — | v1.0 release: `development` -> `main`, one squashed commit, verify on Azure | — | **0.5d** | -- | Pushing `main` deploys. Full deploy gate in `docs/testing.md` first |
| — | **Final deck consolidation** | — | *sized on the day* | -- | If the gate morning finds no concept in the sprint's diff or transcripts, **Sprint H skips the gate entirely** (`the-gate.md`) and only the deck pass runs: merge the sprint decks, dedupe across them, order by difficulty. Consolidation, not generation — `skinet/questions/interview-gen-prompt.md` |

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
> **Ranked 2026-09-12 against the code**, by one question: does it make committed work cheaper, does a
> visitor see it, or is it only tidy? Ranks 1–3 are not really pull-ins — they ride inside a committed PR
> that is already in the same files, so they cost a fraction of what they cost alone.
> **One item, then re-assess.** Never "I have 3 days, take four."
> 🔴 **Buffer + Interview Prep (Oct 13 – Nov 11) is never the window**, and neither is Sprint H's 4.4d reserve.
> **Everything here is a known defect or cleanup — never a new feature.** That line is the whole rule.

| Rank | # | Task | Est | Pull into | Notes |
|---|---|------|-----|-----------|-------|
| **1** | 57 | Dev database = SQL Server: retire SQLite, one provider and one migration set | **2.5d** | 🔴 **Downgraded 2026-09-15 — don't** | ⚠️ **It no longer pays for itself.** The case was the migration tax on T40 and T42; re-measuring both put the whole remaining tax at **three duplicated migrations**, well under an hour. 2.5d to save that is a bad trade, and it is 2.5d against a Sprint E that is already 0.4d over — there is no longer a week with room for it. Revisit after v1.0. *Original case:* There are **6 SQLite migrations against 5 SqlServer** ones (13 and 11 *files* — each migration is a `.cs` + a `.Designer.cs`, plus one `ModelSnapshot.cs`), and every schema row still ahead pays twice: T40's history entity and T42's soft-delete column each need a migration per provider. Done before T40, it deletes that tax from both. Also closes the dev/prod gap that hid T18's missing index for four months. **Risk:** `CustomWebApplicationFactory` is SQLite-backed, so CI needs a SQL Server service container — and T20b later builds on that harness. Deletes `SqliteStoreContext`, `SqliteContextFactory`, `Migrations/SQLite/` |
| **2** | fix/error-correlation-id | 500 responses carry no correlation id | **0.1d inside T23** | **Sprint G — same PR as T23** | Not a separate task. `ApiErrorResponse(int statusCode, string message, string? details)` has no id field and `TraceIdentifier` appears nowhere in `src/`. T23 already wires one id per request through the logs; this is the response half of that same feature. Doing it alone later means touching `ExceptionMiddleware` twice |
| **3** | fix/interceptor-hardening | `errorInterceptor` cleanups — guard `err.error?.…` derefs; flag mixed `throw` vs `return throwError` | **0.2d, inside Sprint E** | **✅ absorbed into Sprint E's interceptor row** | Its own note already said "only worth doing if already in the file". Sprint E's committed interceptor row puts you in that file for BUG-16 and the 401 token, so this is near-free there and a whole PR anywhere else |
| **4** | fix/cart-delivery-fee | Cart shows a delivery fee carried over from an **abandoned** checkout | **0.3d** | **Sprint E, if T50 drops** | `cart.service.ts` prices `shipping` off the in-memory `_selectedDelivery`, cleared only on a completed order — so pick a delivery method, abandon checkout, go back to the cart, and the fee is still there. **Display only** (the server prices from `cart.DeliveryMethodId`), but it is on the path a recruiter clicks, which is exactly Sprint E's theme |
| **5** | BUG-3 | Checkout: explain why **Next** is disabled on the Address step | **0.25d** | **Sprint E, if T50 drops — confirm on live first** | Re-specced Sep 12: the step is Stripe's Address Element, which renders its own field errors, and `[disabled]="!completionStatus().address"` follows that element's `change` event. It may not be a defect at all — five minutes on live decides whether this is a row or a deletion |
| **6** | fix/T27 | Dashboard preview sizes: recent orders 5→15, low-stock 10→15 | **0.25d** | **Sprint H, pre-freeze** | `DashboardService` holds `RecentOrdersSize = 5` and `LowStockListSize = 10`; the admin dashboard looks half-empty on a seeded database. Two constants + two test asserts. Demo-facing, so it belongs with the screenshots, not before them |
| **7** | 2 | Multi-stage Dockerfile | **2d, plus whatever its gate costs** | **Sprint H, only if the reserve is untouched** | Confirmed greenfield — there is **no Dockerfile** in the repo, only `docker-compose.yml`. The last remaining LEARN row, so it carries real interview signal ("how do you cut image size?"), but Azure deploys without it: pure signal, zero demo value, and it costs a gate day on top of the build |
| **8** | fix/T29 | Unify the paging param name (`pageIndex` vs `pageNumber`) | **0.3d** | **Doesn't matter** | Live in three files and already *documented* by comments in both admin components (`shop.service.ts` maps `pageNumber` → `pageIndex`). BUG-12 fixed the real break; what is left is naming. No visitor sees it, no interviewer asks about it |
| **9** | fix/T30 | `register`/`login` template-only members → `protected` | **0.2d** | **Doesn't matter** | Cosmetic, zero runtime effect. Leaves `product-form` the last file off the Angular style guide |
| **10** | fix/angular-animations-deprecation | `provideAnimationsAsync()` is deprecated, removal intended in v23 | **0.5d** | **🔴 Don't** | `app.config.ts:34`. Deprecated in 20.2, but **v22 works and no Angular major lands before Nov 11**. Replacing a working provider now is risk with no payoff — this is a v23-upgrade task, not a v1.0 task |
| **11** | docs | Spike retrofit — `Why (best answer)` on ~11 decision blocks; fill 4 `<your call>` Decisions | **1d+** | **Gate day, never a sprint** | Blocked until the user writes `Why (by me)` first — Claude writing it spends the attempt, which is the whole point of the exercise |

---

## Descoped for v1.0 — *not shipping by Oct 8*

> Cut consciously on the 2026-07-13 re-baseline. **Not forgotten** — revisit after v1.0 ships.
>
> **T33 (`<mat-error>` on the checkout forms) cut 2026-09-12:** there are no checkout forms. The four steps
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

**Committed vs capacity (re-sliced 2026-09-15):**

**Task work only — the gate is never pre-costed** (`mindset/the-gate.md`). Each sprint's headroom is what
its gate day draws on, sized on the gate morning from the diff and the transcripts.

| Window | Days | Task work | Headroom *(gate + unplanned)* |
|---|---|---|---|
| Sprint E · Sep 15–21 | 7 | 6.9d | **0.1d ⚠️ — T50 is the drop, and it buys back 2d** |
| Sprint F · Sep 22–28 | 7 | 4.4d | 2.6d |
| Sprint G · Sep 29 – Oct 5 | 7 | 4.5d | 2.5d — *two LEARN rows; likeliest full gate day* |
| Sprint H · Oct 6–12 *(ship Oct 8)* | 7 | 2.6d | **4.4d reserved** |
| **Build total** | **28** | **18.4d** | **9.6d (34%)** |
| Buffer + Interview Prep · Oct 13 – Nov 11 | 30 | T53 + deck | — |

Every sprint since A has produced unplanned bugs; this budget assumes that happens again. **Slack is
protection, not capacity** — the sprint that spends it is the one that puts T40 or the release at risk.

⚠️ **Sprint E has 0.1d of headroom and still owes a gate day — it is tagged rather than fixed.** Its slack
lives in the **ladder**, not in a reserve: T50 is 2d and Could-tier, so the day-4 checkpoint drops it and
leaves 2.1d for the gate. Holding 2d idle from Monday to insure against the same risk costs more than
dropping one row on Thursday.

**Why Sprint E was re-costed rather than re-filled:** it looked like it had 2.35d of slack. It did not —
the eight rows were priced at 3.65d when the blow-out rule puts them at 6.9d. Pulling extra rows in on
top of an unpriced bill is how a comfortable week becomes a lost one. Same eight rows, honest numbers.

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

*(“Angular unit tests” left this list on 2026-08-31 — T52 brought the first Vitest specs and `npm test` into CI.)*

---
