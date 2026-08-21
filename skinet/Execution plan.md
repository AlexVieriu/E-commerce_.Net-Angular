# Execution Plan

> .NET 10 + Angular 22 | Azure-only | Solo dev | B2C physical goods
> **Milestone: `v1.0 — Interview Ready` · Sep 22, 2026** — 🔴 **interview Sep 30, 2026**
> Mid .NET / junior Angular | CV project | Claude Code for speed, saved time for learning

**Estimates:** S (<1d), M (1-3d), L (1wk+) — without Claude Code.
**LEARN:** Claude implements -> you read every line + do exercise -> write `skinet/questions/sprint-N/task-N-*.md` + one line in `interview-prep.md`.
**Git:** Branch per task -> development (squash); sprint -> main (**squash** — one commit per sprint, never `--no-ff`). Conventional commits. See `docs/git-workflow.md`.
**Re-baselined 2026-07-13** (~40% descoped, deadline Aug 30 -> Sep 6). Rationale + velocity math: `progress.md`.
**Re-baselined 2026-08-10** (interview Sep 30 -> v1.0 Sep 22, four sprints B/C/D/E). New scope is defects and
cleanups from *Pull-in candidates* only — never features.

---

## What Already Exists

Full CRUD products/orders/cart/payments/coupons | Cookie Identity (Admin+Customer) | Stripe (PaymentIntent+webhook+refund+coupons) | SignalR notifications | Redis cart+cache, **keyed by the caller** (`ICurrentUser`) | Angular 22 zoneless+signals+Tailwind+Material | 4-step checkout | Admin orders+refund | Guards (auth/admin/emptycart/orderComplete) | Rate limiting | ExceptionMiddleware+errorInterceptor | GitHub Actions CI/CD to Azure | 25 docs | 23 unit/integration test files + 4 Playwright specs | API integration harness (`WebApplicationFactory`) | Security audit done (stock race condition fixed in T13, cart IDOR in BUG-18)

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

**Points: 7.** Sprint end: E2E suite green (BUG-17 red on purpose → Sprint D). Cart no longer leaks between
users — proven at the API and in the browser.

---

## Sprint C — README + Demo Fixes *(Aug 12 – Aug 25 · 10 working days)* · ⏳ CURRENT

> A recruiter spends 30 seconds on the repo and 2 minutes on the live demo. This sprint makes both
> survive that. **There is no README today** — T47 writes the first one.
> Epics: `Docs`, `UX`. Order: **rotate the SQL password -> BUG-19** -> 47 -> 48 -> 52 -> 43 ->
> BUG-10 -> BUG-14 -> 33 -> BUG-3 -> 38. Only the first is order-critical; the rest flex with the day's energy.
> The start-here table in `progress.md` is the same order — keep them in step.

| # | Task | E | Est | Dep | Notes |
|---|------|---|-----|-----|-------|
| 47 | README **create**: pitch, stack, architecture, "how to run", Azure link | S | **✅ Aug 21** *(0.5d vs 2d est)* | -- | Highest ROI of anything left — no file existed. Writing rather than building, and `docs/` already held the content. Screenshots/GIFs are T38; the C4 diagram is T48. `feat(task-47)` |
| 48 | C4 diagram (Context+Container) in Mermaid -> `docs/architecture.md` | M | **1d** | -- | Cheaper than it looks: `docs/backend/architecture.md` + `docs/frontend/architecture.md` already hold the prose (100 lines, 0 diagrams) — this is drawing what's already written |
| 52 | Open redirect: validate `returnUrl` starts with `/` | S | **0.5d** | -- | Security fix — cheap. Set in `auth.guard.ts:19` + `error.interceptor.ts:32`, consumed in `login.component.ts:26` |
| BUG-19 | Coupon validated once, then trusted forever — by the order, the charge, and time | S | **✅ Aug 20** *(1.5d vs 0.5d est)* | -- | Found Aug 13 during BUG-18; **scope grew Aug 20** when all three cases were reproduced live (`docs/smoke-tests/sprint-c/bug-19-coupon-trust.md`). **(A)** `CalculateDiscount` read `AmountOff`/`PercentOff` off `cart.Coupon` → forged $999 discount on the order. **(B)** `ApplyDiscountAsync` re-read Stripe by `cart.Coupon.Id` — **also client-supplied** → $100 charged on a $185 order. **(C)** a coupon applied before its campaign was retired still paid out, and the order total matched the charge so nothing flagged. Fix (spike 1a/2d/3b/4b): both paths re-derive from `cart.Coupon.PromotionCode`; a failed lookup drops the coupon, nulls the intent and 409s. Two defects the fix would otherwise have introduced were caught first — `AmountOff / 100` truncated the cents, and a blank code dropped Stripe's `Code` filter. `fix(bug-19)` (#75) |
| BUG-20 | **Every coupon rejected — `GET /api/coupons/{code}` always 400** | S | **✅ Aug 20** | -- | `promotion.coupon` is expandable: unexpanded it arrives as a bare id, so Stripe.net leaves the `Coupon` object null and `CouponService.cs:22`'s guard never passes. Broke at `fix(deps)` #50 — **an SDK version pins the API version, so a dependency refresh was an API migration.** No test can reach it (no DI seam, `[ExcludeFromCodeCoverage]`). Fix: `Expand` + `Active = true`. `fix(bug-20)` |
| 43 | DB indexes: BuyerEmail, composite (Brand,Type), Price | S | **0.5d** | -- | One migration. Unique `PaymentIntentId` already exists (`OrderConfiguration.cs:22`) |
| BUG-10 | Top loading bar spins forever on live — the busy counter never returns to 0 | S | **1d timebox** | -- | ⬅ pull-in **rank 1**. ⚠️ Root cause not yet identified — `loading.interceptor.ts` already decrements in `finalize`, which fires on error *and* unsubscribe. Reproduce first, then fix. `fix(bug-10)` |
| BUG-14 | Product page: "Add to cart" disables on click — reads as broken though the cart did update | S | **0.5d** | -- | ⬅ pull-in **rank 2**. Confirmed: `product-details.component.html:22` `[disabled]="quantity() === quantityInCart()"`. `fix(bug-14)` |
| 33 | Wire `<mat-error>` validation on the checkout forms | S | **0.5d** | -- | ⬅ pull-in **rank 3**. Register already covered — it uses `<app-text-input>`, which renders `mat-error`. Verify that before starting |
| BUG-3 | Checkout: hint on an empty required address field | S | **0.5d** | -- | ⬅ pull-in **rank 4**. Money-path UX, cheap. `fix(bug-3)` |
| 38 | README UX screenshots + GIFs | S | **0.5d** | 47 | **Last task of the sprint** — un-folded from T47 on Aug 21 and moved here, so the images are captured once the products and the site's idea are settled. The README already carries HTML-comment placeholders |
| 12 | ~~Cart ownership: userId in Redis~~ | — | **0** | -- | ✅ **CLOSED by BUG-18** — same work, already shipped and proven |
| — | Rotate the Azure SQL password + audit the server firewall | S | **✅ Aug 18** | -- | **Done first, as planned.** Password was `Pa$$w0rd` **and in git history**, so it stayed valid for four months after T3 "removed" it; three stale client-IP firewall rules also went. `skinet-2025-test-server` runs a weak password on a publicly resolvable host, so the server firewall is the only other control. New password → update the App Service `SqlServerAzureConnection` → restart → re-smoke. Never mid-deploy: rotating a live connection string is how a green sprint becomes a 500 |
| — | Deploy-gate cleanup: retire the four legacy manual checks in `testing.md` | S | **0.5d** | -- | The gate grows a manual entry per sprint and nothing ever removes one — by Sprint E it is a 30-min ritual nobody runs honestly. Sprint 2 price change / Sprint 3 payment sync / BUG-11 belong in **T20b**'s harness (Sprint E); BUG-12 belongs in Playwright. This row moves them and deletes the blockquotes |
| — | Sprint B Anki deck | — | **1d** | -- | 1 day per sprint |

**Points: 7 · Est 7.5d of 10 — 2.5d slack.** *(Re-summed Aug 21: T47 dropped 2d → 0.5d, T38 came out of T47 at 0.5d of its own. The Anki day is slack, not work.)*

⚠️ **Burn-down 2026-08-20 — badly over-committed.** **4 working days left (Aug 20, 21, 24, 25) vs ~7.5d of
unstarted work — ~3.5d over.** Aug 19–20 went to BUG-20 and the dependency refresh, both unplanned.
**T47 alone is 2d of the 4.**
**Decision (the user): cut nothing, re-assess at sprint end.** With no cut the overflow lands on whatever
runs last, so **T47 must not be last** — start it by Aug 20; let BUG-3/T33 slip if something must.

Sprint end: README live with screenshots, C4 diagram
published, open redirect closed, indexes shipped, and the four defects a live demo actually shows.

---

## Sprint D — Order Domain *(Aug 26 – Sep 8 · 10 working days)*

> The strongest domain story in the plan, plus the two money-path bugs left over from Sprint B.
> Epic: `Domain`. Order: 40 -> 41 -> BUG-2 -> BUG-17 -> 50 -> 21.

| # | Task | E | Est | Dep | Notes |
|---|------|---|-----|-----|-------|
| 40 | OrderStatus: add Processing/Shipped/Delivered/Cancelled + append-only OrderStatusHistory | M | **3d** | -- | LEARN: state machine + audit trail. **Spike first: one enum or two?** The current enum is payment-shaped (`Pending, PaymentReceived, PaymentFailed, PaymentMismatch, Refunded`), so fulfillment states would mix two concerns in one field. T26 parks admin fulfillment control here |
| 41 | Order cancellation: POST cancel, only Pending/PaymentReceived, Stripe refund if paid | M | **1.5d** | 40 | Refund plumbing already exists (`AdminController`, `PaymentService`, `IPaymentService`) — this reuses it. Log the email, don't build the email service (T39 descoped) |
| BUG-2 | Checkout: declined card can't retry in-session — Stripe `elements.submit()` is single-use | S | **0.5d** | -- | **Money path.** `stripe.service.ts:149` already resets `elements = undefined`; the fix is calling it on failure and re-creating the payment element. Needs a manual declined-card test |
| BUG-17 | Logout then hit `/checkout` (empty cart) → lands on `/cart`, not the login page | S | **0.5d** | 37 | Found Aug 4 by `auth-orders.spec.ts`, left red on purpose. `auth.guard.ts` navigates imperatively instead of returning a `UrlTree`, so it races `emptycart.guard.ts` and loses. Rider: `checkout.component.ts` prints the raw `HttpErrorResponse` on a 401. `fix(bug-17)` |
| 50 | API docs: `[ProducesResponseType]` + XML summaries | M | **1d** | -- | **27 endpoints across 7 controllers**, none annotated today |
| 21 | Coverage gate: coverlet, fail PR < 60% | S | **0.5d** | 19,1 | Coverlet is installed; `ci-cd.yml` doesn't collect coverage yet |
| — | Sprint C Anki deck | — | **1d** | -- | |

**Points: 8 · Est 8d of 10 — 2d slack.** Sprint end: order status + history working with an audit trail,
cancellation refunds through Stripe, both money-path bugs closed, API self-documenting, coverage gated.

---

## Sprint E — Production Hardening *(Sep 9 – Sep 22 · 10 working days)*

> The last build sprint. Everything here is what separates "it runs on my machine" from "I can operate it".
> Epics: `Observability`, `Data`. Order: 22 -> 23 -> 42 -> 20b -> 10 -> ci/audit -> 49 -> 35 -> the two interceptor fixes.

| # | Task | E | Est | Dep | Notes |
|---|------|---|-----|-----|-------|
| 22 | Serilog structured logging *(App Service logs, no Seq)* | M | **2d** | -- | ⬅ **promoted from pull-in.** LEARN: "why not string interpolation?". Confirmed absent — zero Serilog references. If the live demo breaks there is nothing to diagnose with |
| 42 | Soft delete + EF global query filter | M | **2d** | -- | ⬅ **promoted from pull-in.** LEARN: "soft delete in EF Core?". Real correctness gap: `ProductsController.cs:76` hard-deletes. Orders snapshot the product, so no FK risk |
| 10 | Health-check endpoints | S | **0.5d** | -- | ⬅ pull-in. Confirmed absent — no `AddHealthChecks`. The serverless-SQL idle-pause history (BUG-8) makes a probe worth having |
| — | Scheduled weekly CI advisory run (`on: schedule`) | S | **0.5d** | -- | ⬅ pull-in. ~5 lines in `ci-cd.yml`. NU1903 was caught while deploying, not by CI. `chore/scheduled-dependency-audit` |
| 35 | `NgOptimizedImage` on catalog images | S | **0.5d** | -- | ⬅ pull-in. Cheap, image-heavy catalog, real Angular talking point |
| — | `fix/BUG-16` — move the checkout cart-reload out of `errorInterceptor` | S | **0.5d** | BUG-16 | ⬅ pull-in. Domain logic in a cross-cutting interceptor; URL-sniffing breaks silently on a route rename |
| — | `fix/401-ctx` — `HttpContextToken` instead of the 401 URL sniff | S | **0.5d** | -- | ⬅ pull-in. Same class, different domain → its own PR. Kill the URL *signal*, keep the auth-redirect logic |
| 23 | Correlation IDs — one id per request, on every log line | S | **0.5d** | 22 | ⬅ **un-descoped.** Was blocked on T22; T22 now ships here. Answers "how do you trace one request across logs?" for half a day |
| 20b | Extend the API integration harness: auth, orders, payments happy paths | S | **1d** | -- | ⬅ **un-descoped (partial).** BUG-18 built `CustomWebApplicationFactory`; tests on top of it are now cheap. TestContainers stays cut |
| 49 | Document `docker-compose.yml` as the way to start Redis | S | **0.25d** | 47 | The file already exists (`redis` + `sqlite`) and T37's `global-setup.ts` uses it. What's missing: it's named nowhere in the docs, so `dotnet test` fails on a clean machine with no hint why. Folds into T47's "how to run" |

**Points: 10 · Est 8.5d of 10 — 1.5d slack.** Sprint end: **v1.0 ships Sep 22** — structured logs with
correlation ids, soft delete behind a global filter, health probes live, dependency advisories on a
schedule, one-command local startup, both interceptor compromises unwound.

---

## Interview Prep *(Sep 23 – Sep 30 · 6 working days)* — 🔴 **DO NOT FILL**

> The run-up to **Sep 30**. Absorbs slippage from any sprint above.

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 53 | Final Azure smoke test: full user + admin journey | S | -- | **Non-negotiable. Always last.** |

Everything else in this window: **Sprint D + E Anki · full interview deck consolidation · re-read every
gate doc · mock questions · slack for slippage.**
If a sprint over-runs, it eats *this* — that is what the window is for. **No feature work lands here.**

---

## Pull-in candidates *(if a sprint finishes early)*

> **Not committed work.** Fires only when a sprint's committed scope is done **with days left in that
> sprint's own timebox**.
> **Promoted at the Aug 10 re-baseline:** ranks 1–4 (BUG-10, BUG-14, T33, BUG-3) → Sprint C ·
> T22, T42, T10, T35, ci/audit, fix/BUG-16, fix/401-ctx → Sprint E. What's left is below.
> **Take T2 first** — it's the only remaining LEARN item and the last one with real interview signal.
> Below it, order is meaningless: pick by the size of the gap you actually have.
> **One item, then re-assess.** Never "I have 3 days, take four."
> 🔴 **Interview Prep (Sep 21–30) is never the window.** It absorbs slippage — do not raid it.
> **Everything here is a known defect or cleanup — never a new feature.** That line is the whole rule;
> see *Post-v1.0* below.

| Rank | # | Task | E | Dep | Notes |
|---|---|------|---|-----|-------|
| — | 2 | Multi-stage Dockerfile | M | -- | Azure deploy works without it; decent interview signal |
| — | fix/T27 | Dashboard preview sizes: recent orders 5→15, low-stock 10→15 (`DashboardService.cs`) | S | -- | Cosmetic (fill the screen). Backend caps + 2 test asserts. Deferred from T28 (2026-07-15). `fix/task-27-dashboard-preview-sizes` |
| — | fix/T30 | `register`/`login` template-only members → `protected` (match the T30 convention) | S | -- | Cosmetic, zero runtime effect. Leaves `product-form` the last file off the Angular style guide. `fix/task-30-access-modifiers` |
| — | fix/T29 | Unify the paging param name (`pageIndex` vs `pageNumber`) across shop + admin | S | -- | Naming consistency only; BUG-12 already fixed the real break. `fix/task-29-paginate-one-name` |
| — | fix/interceptor-hardening | `errorInterceptor` cleanups — guard `err.error?.…` derefs; flag the mixed `throw` vs `return throwError` | S | -- | Default-leave, capture only. Low risk (`ExceptionMiddleware` always writes a body; the split is intentional). Only worth doing if already in the file. `fix/interceptor-hardening` |
| — | fix/cart-delivery-fee | Cart page shows a delivery fee carried over from an **abandoned** checkout — `totals()` prices shipping off `_selectedDelivery` (`cart.service.ts:44`), an in-memory signal cleared only on a completed order (`checkout.component.ts:172`) | S | -- | **Display only** — the server prices shipping from `cart.DeliveryMethodId`, so no charge is ever wrong. Survives navigation but not F5, which is the real inconsistency. Found Aug 20 running the BUG-19 smokes |
| — | docs | Spike retrofit — add `Why (best answer)` to the ~11 decision blocks (T30/T29/fix-paginate/T26/T25/T8/T54); fill the 4 `<your call>` Decisions (T27, T24, SQLitePCLRaw, backlog ClientSecret) from PR history; unify the two Decision styles | M | -- | Blocked (BUG-11/T27/T24/SQLitePCLRaw/backlog) until the user writes `Why (by me)` first — Claude writing it spends the attempt. Do at sprint close, with the as-built diff + Anki (raised 2026-07-18) |

**Post-v1.0 — *not* pull-in candidates:** Domain events | Category hierarchy | Wishlist | SEO slugs

> Suspended until v1.0 ships. **New scope is never the answer to being ahead** — if you finish early, the
> move is to pull the buffer forward and ship early. Fixing a known defect above is *not* new scope;
> adding a wishlist is. That distinction is the only thing separating the table above from feature creep.
> Revisit this list after Sep 22, while interviewing.

---

## Descoped for v1.0 — *not shipping by Sep 22*

> Cut consciously on the 2026-07-13 re-baseline. **Not forgotten** — revisit after v1.0 ships.

| # | Task | Why cut |
|---|------|---------|
| 55 | Lighthouse baseline | Scores don't get you hired |
| 56 | Performance pass (MiniProfiler + bundle analyzer) | Depends on 55; premature at this scale |
| 34 | Skeleton loaders | Polish |
| 32 | Memory leak audit | "Likely minimal (signals + httpResource)" — plan's own words |
| 39 | Email: IEmailService + `Channel<T>` BackgroundService | Expensive (M+). Stripe + webhooks + SignalR already prove the async/infra story. **Best remaining LEARN item if a whole sprint frees up** |
| 46 | Unit tests for cancellation/soft-delete/history | Not a task — T40/41/42 each carry their own tests inline |
| 20 | TestContainers | The `WebApplicationFactory` half ships (BUG-18 + T20b, Sprint E). Containers add setup cost, not interview signal |
| 49 | docker-compose **with Seq** | Redis + SQL ship in Sprint E; Seq stays cut — App Service logs are enough |
| 51 | RequireConfirmedEmail | Blocked on 39 |

| 11 | Pagination: TotalPages/HasNext/HasPrev | Cosmetic |
| 36 | Accessibility: axe pass | Real value, no interview signal for these roles |

> **Dependency note:** 22 (Serilog) is committed to Sprint E, which is what un-blocked 23 (correlation IDs) and
> the Redis+SQL half of 49. 51 (RequireConfirmedEmail) stays blocked on 39.

---

## Progress

**Status lives in `skinet/progress.md`** — task checkboxes + daily log. Not duplicated here.

## Certifications *(never steals build days or run days)*

Capacity: 2 days off per week — the only study slots outside project days, and not reliably usable.

- [ ] **AZ-900** — **exam target ~Sep 30.** Study on days off only. Mostly review (Azure deploy + Key Vault
  already done).
  ⚠️ That lands the same week as the interview — if the week gets tight, the exam moves, not the interview.
- [ ] **AZ-204** — **exam target ~mid-Nov 2026** (~1.5 months after AZ-900). Study starts once v1.0 ships
  (Sep 22). The project IS the lab — Key Vault, App Service, Managed Identity, App Insights.
  The portfolio gets interviews; the cert supports it. Not the other way round.

## When behind (life buffer)

**Sep 22 is the target; Sep 30 is the cliff.** The hard commitment is: ship with a working Azure deploy + great README.
When a medical check / contract renewal / bad day eats time → **cut the lowest-priority task**, never extend
hours or sacrifice sleep/runs.

The Aug 10 re-baseline bought ~3.5 weeks. **That was the last lever** — after this, only scope moves.
If a bad week lands, **cut immediately** — do not absorb it by grinding.
**Cut order (first to go, left):** T41 > T48 > T50 > T21 > T35. **Never cut:** T47 README, T40, BUG-2, T53.

**Committed vs capacity (2026-08-10 · Sprint C row corrected 2026-08-20):**

| Window | Days | Work | Slack |
|---|---|---|---|
| Sprint C · Aug 12–25 | 10 | 8.5d | 1.5d |
| Sprint D · Aug 26 – Sep 8 | 10 | 8d | 2d |
| Sprint E · Sep 9–22 | 10 | 8.5d | 1.5d |
| **Build total** | **30** | **25d** | **5d** |
| Interview Prep · Sep 23–30 | 6 | T53 + deck | — |

**5 days of slack** *(was 6 — the Sprint C row said 7.5d while its own task rows summed to 8.5d)*.
Sprints A and B each produced 2 unplanned bugs; this budget assumes that happens again and still lands.
Slack is protection, not capacity — the first sprint to fill it is the one that puts the README or T40 at
risk. **Sprint C has already spent its 1.5d** (Sprint B ran +2d into it) — see the burn-down in that
sprint's section.

Every sprint end: E2E run, Azure verify, README update.

## Tech Stack Additions

| Sprint | Added |
|--------|-------|
| 1 | Azure Key Vault |
| 2 | NWebsec, Coverlet *(installed but never wired into CI — that's T21, Sprint D)* |
| 3 | FluentValidation, Bogus |
| A | ESLint (`angular-eslint`) + Prettier, Roslyn analyzers (`AnalysisMode=Recommended`). Angular -> 22.0.6 stable |
| B | Playwright, `WebApplicationFactory` |
| C | Mermaid |
| D | — |
| E | Serilog (App Service logs, no Seq), EF global query filters, ASP.NET health checks, docker-compose (Redis + SQL) |
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
| 22 | Structured logging | "Why not interpolation?" — **Sprint E** |
| 25 | Angular lazy loading | "Route structure in large app?" |
| 37 | E2E testing | "Strategy? Page Object Model?" |
| 54 | Static analysis (ESLint / Roslyn) | "Linter vs formatter? When is the linter *wrong*?" |
| ~~39~~ | ~~Channel\<T\> + BackgroundService~~ | ~~"Background jobs without Hangfire?"~~ **descoped** |
| 40 | State machine + audit | "Model order status changes?" |
| 42 | Global query filters | "Soft delete in EF Core?" — **Sprint E** |

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

OAuth2/JWT, multi-currency, i18n, K8s, GitOps, CSRF, Prometheus/Grafana/Jaeger/OTel, Outbox, Service Bus, CQRS, Domain events, PWA, SSR, file upload, Angular unit tests, and more.

---
