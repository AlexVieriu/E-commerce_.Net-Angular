# Progress Log

## Daily Log

**The table is the ledger — one line per row, scannable.** Depth goes in **Detail** below, as bullets
under the same `Date · Task` label. A row that needs a paragraph belongs there, not in a table cell
nobody finishes reading. Where a questions doc already covers the mechanism, Detail links to it rather
than repeating it.

| Date | Task | What happened |
|------|------|---------------|
| Apr 2 | S1: T1,3,4,5,6,7 | Sprint 1 complete. Azure verified. |
| Apr 6 | T9, BUG-1 | CORS + security headers, checkout Next button fix |
| Apr 7 | T15, T16, T18 | Cart delete ordering, Stripe idempotency, order unique constraint |
| Apr 8 | docs | Refactored + updated all affected docs |
| Apr 8 | T17 | Price change detection: 409 + snackbar + redirect. Also fixed order detail routing (/order → /orders) |
| Apr 9 | T19 | The repo's first unit tests: 128 green — Core 90%, Application 89%, Infrastructure ~85%. `[ExcludeFromCodeCoverage]` on the Stripe services. |
| Apr 21 | Sprint 2 interview polish | Audited the 11-section interview set against `interview-gen-prompt.md`; +7 questions, Anki regenerated for G/H/I/J/K. |
| Jun 18 | Sprint 2 → live on Azure + BUG-4..10 | Deployed to Azure; six deploy-hardening bugs fixed (BUG-4..9), BUG-10 deferred. `main` became one squashed commit per sprint. |
| Jun 25 | T8 | FluentValidation gate passed; deep LEARN on the filter's open generics. 136 green. |
| Jun 25 | T24 | Bogus seed via `dotnet run -- --seed` — 70 products / 13 users / 25 orders, one-shot, dev-only, idempotent and deterministic. 136 green. |
| Jun 26 | fix/deps | Dependency refresh: all packages → latest GA (framework 10.0.9; majors Redis 2→3.0.7, Stripe.net 51→52.1.0) + cleared SQLitePCLRaw advisory (`bundle_e_sqlite3 3.0.3` override). Build 0/0, 136 green, 0 vulnerable solution-wide. Dev-smoked Redis + Stripe. |
| Jun 26 | T27 | Dashboard backend — one aggregation endpoint, thin controller. Revenue counts `PaymentReceived` only. |
| Jul 1 | T25 | Admin shell + lazy child routes, guarded once at the parent. Gate passed. |
| Jul 2 | T26 | Payment reconciliation endpoint — the recovery path for a missed webhook. Rescoped mid-spike. Gate passed. |
| Jul 13 | T26 merge, BUG-12 | **The admin paginator was dead:** the client sent `pageNumber`, the API binds `PageIndex`, and model binding dropped it silently. |
| Jul 13 | BUG-11 | **Checkout 400 for stateless countries.** `State` was mandatory at four layers, not one — and an earlier fix had patched the wrong entity. |
| Jul 13 | BUG-13 | **Shipping name saved as the literal `"null null"`.** A lying TS model is what made the concat look safe. |
| Jul 14 | T54 *(PR #57 + #58 — merged)* | **Lint + analyzer gate, strict:** 50 → 0 ESLint, 368 → 0 Roslyn. Found three real bugs review had missed; tried to create a fourth. |
| Jul 15 | T28 *(PR #61)* | Admin dashboard frontend on `httpResource` — one GET fills the page, with an honest "showing 10 of 30" header. |
| Jul 16 | T30 | Product create/edit — one component, mode inferred from `:id`. Signal Forms evaluated and rejected, on Material support. |
| Jul 15 | T29 | Admin product list with full facets, reusing the shop's cached endpoints. Gate skipped — pattern-copy. |
| Jul 18 | BUG-15 | **Product edit served stale data for up to 10 minutes** while the list looked fresh — a PUT never cleared the by-id cache. Two causes, both in the cache key. |
| Jul 18 | T31 | Product delete closes admin CRUD and Sprint A. Refresh re-derives from the server rather than removing optimistically — on a paginated list that would buy a lying `count`. |
| Jul 27 | BUG-16 | **The concurrency check had never worked on this endpoint** — a detached entity made EF compare the new value against itself. |
| Jul 27 | Sprint A → live | `main` was two sprints behind. The pre-deploy Release build caught five fresh advisories that would have failed the Azure publish. |
| Aug 4 | T37 | The repo's first browser tests — 3 specs, self-starting environment. Two real races fixed; found BUG-17. |
| Aug 13 | BUG-18 *(PR #72)* | **Cart IDOR closed at the source:** the key parameter is *gone*, so the exploit no longer compiles. The failing build was the audit. |
| Aug 13 | Sprint B → live | Closed and released. Three defects surfaced between the gate and the deploy — incl. a test host booting on real Stripe credentials. |
| Aug 18 | Sprint C #1 — SQL credential rotation | Rotated the Azure SQL password — `Pa$$w0rd`, in git history for four months. All three firewall rules were stale. |
| Aug 20 | BUG-20 | **Every coupon had been rejected for ~2 months.** An SDK major bump silently performed an API migration. |
| Aug 20 | deps *(PR #74)* | Packages refreshed as an *audit*, not a chore. `xunit.v3` held at 3.2.2 — v4's runner finds zero tests under .NET 10. |
| Aug 20 | BUG-19 *(PR #75)* | **A coupon was validated once, then trusted forever** — by the order, the charge, and time. Three cases proven live. |
| Aug 21 | T47 *(PR #76)* | The repo's first README, cut 207 → 134 lines against two real big-tech examples. Verifying it caught three defects. |
| Aug 21 | T48 *(PR #77)* | C4 **container** diagram in Mermaid. The spike described the four .NET layers as containers — wrong, and caught only after deciding. |
| Aug 30 | T52 *(PR #78)* | **No defect** — a `UrlTree` has no host field, so the router already sent a hostile `returnUrl` to `/not-found`. Guarded at the read site anyway; adds the client's first 14 unit tests. |

---

## Detail

*The rows worth reading twice. Same date + task label as the table above; Ctrl-F the task.*

### Jun 18 · Sprint 2 → live on Azure + BUG-4..10

- **Six deploy-hardening bugs**, all found by deploying: auth-401 nav (4), wwwroot root-500 (5), fonts CSP (6), Stripe CSP+COEP (7), serverless SQL idle-pause retry (8), config-driven admin password (9). BUG-10 (loading bar) deferred.
- **`main` rewritten to one squashed commit per sprint** — the shape it has kept since.
- Live smoke green end to end: register → cart → Stripe → order → refund → admin.

### Jun 25 · T8 — FluentValidation

- Cold rebuild: filter PARTIAL, validators PASS, wiring PASS, tests PARTIAL (the repo's first unit tests).
- **Deep LEARN on the filter internals** — `continue`, open generics + `MakeGenericType`, `System.Type`, `ValidationContext` type-erasure, `ValidateAsync`/`CascadeMode`, `SetValidator`.
- 136 tests green; `task-8-fluentvalidation.md` regenerated from the real gaps.

### Jun 26 · T27 — dashboard backend

- `GET /api/admin/dashboard` via `IDashboardService`, in-memory aggregation, controller stays thin.
- **Revenue counts `PaymentReceived` only** — net of discount, including shipping. Low-stock threshold 5; recent and low-stock previews capped.
- 138 green; live-verified against the seed (70 products / 25 orders).

### Jul 1 · T25 — admin layout

- `AdminComponent` shell (mat-sidenav + `<router-outlet>`) with child routes lazy-loaded via `loadChildren`, **guarded once at the parent** (`[authGuard, adminGuard]`).
- Verified live 5/5: guard blocks non-admin, `/admin` redirects, child routes swap, deep-link survives F5, orders + refund work.
- Gate passed — EXPLAIN/BREAK covered guard *activation* (not inheritance) and the `pathMatch:'full'` infinite loop.

### Jul 2 · T26 — payment status reconciliation

- `POST /api/admin/orders/{id}/sync-payment-status` — the recovery path for a missed webhook.
- **Rescoped mid-spike**: a free-form status dropdown was rejected because payment status must be Stripe-sourced only. The endpoint reuses the webhook's own match/mismatch rule via `IPaymentService.ReconcileOrderPayment`. `Refunded` orders excluded.
- 140 green (2 new); verified through the UI (stuck-Pending → sync → PaymentReceived) and Scalar (Refunded / not-found / idempotent resync).
- Gate passed — the risks named were shared-method drift, SignalR's circular-reference constraint, and a missing guard corrupting dashboard revenue.

### Jul 13 · T26 merge, BUG-12 — the dead admin paginator

- Angular sent `pageNumber`; the backend binds `PagingParams.PageIndex`. **Model binding dropped it silently**, so page 2+ re-served page 1.
- `pageSize` bound correctly, which is exactly why the paginator looked alive.
- The shop already did it right (`shop.service.ts:38`) — only the admin table skipped the rename. BUG-11 and BUG-12 were also missing from `Execution plan.md` and were added.

### Jul 13 · BUG-11 — checkout 400 for stateless countries

- **The spike said "1-line fix (the validator)" and was wrong.** `State` was mandatory at **four** layers: TS model, validator, entity, NOT NULL column. Dropping the validator alone would have turned a clean 400 into a `DbUpdateException` **after Stripe took the money**.
- **Root discovery:** `MakeAddressStateOptional` (Apr 6) had fixed the *wrong entity* — `Address` (saved profile), not `ShippingAddress` (the owned entity checkout actually writes).
- Fixed per-country: `State` required only for US/CA/AU, Stripe's own validated list. New migration on both providers; `AddressPipe` stopped rendering `", ,"`.
- 147 green (7 new). Verified live: RO order created, US still enforced.

### Jul 13 · BUG-13 — `"null null"` on the shipping name

- `stripe.service.ts` did `user.firstName + ' ' + user.lastName`, and **JS coerces null to the string `"null"`**. The seeded admin has neither name, so `"null null"` was persisted onto the order.
- **Third lying TS model that day:** `user.ts` declared `firstName: string` / `address: Address` while the backend has `string?` / `user.Address?.toDto()`. The type lie is *why* the concat looked safe.
- Fixed with a null-safe join plus honest types. Forward-only — orders already saved keep the bad value.

### Jul 14 · T54 — lint + analyzer gate *(PR #57 + #58)*

*Full write-up: `questions/sprint-A/task-54-lint-analyzers.md`.*

- **50 → 0 ESLint errors, 368 → 0 Roslyn warnings.** `TreatWarningsAsErrors` scoped to **Release**, so CI fails a PR on any warning while local Debug stays warning-only. The gate was *proven* — a violation was injected into each half and both rejected it.
- **Forced side-quest:** Angular was pinned to `22.0.0-next.1`, and semver sorts a prerelease *below* its own release, so `angular-eslint`'s peer range rejected it → upgraded to 22.0.6 stable (+ Node 24.18) and stripped the 8 `$safeNavigationMigration()` shims `ng update` injected.
- **Three bugs the linter found that review had not:** `address.pipe.ts`'s `?.address!` (TypeError on a nullish address); `ProductSpecParams.Search`'s culture-sensitive `ToLower()` (Turkish `I` → `ı` silently breaks search); and `<NoWarn>CS8618</NoWarn>` *assigning* instead of appending, wiping every `Directory.Build.props` suppression.
- **One bug it tried to create:** CA1862 wanted `StringComparison` inside an EF expression tree — EF *throws* on those overloads, so obeying it would have broken every product search.
- Surfaced **BUG-14** (add-to-cart locks the button until quantity changes) — pre-existing, descoped. 147 green.
- **The gate itself broke and was rebuilt (PR #58):** ~30 questions answered incrementally = 4h of ping-pong. Now capped, timeboxed, and fronted by a Change Map — a 157-file diff is unstudiable, an 8-file map is not.

### Jul 15 · T28 — admin dashboard frontend *(PR #61)*

*Full write-up: `questions/sprint-A/task-28-httpresource-read-view.md`.*

- `httpResource<Dashboard>` — **one GET fills the page** — with `OnPush`, 5 stat cards, a read-only recent-orders table and a low-stock table.
- **An honest "(showing 10 of 30)" header** when the card count exceeds the capped list; demoed by pushing 13 products low.
- 3 `type` models with **zero `?`**, because the backend DTO has no nullable fields. Live-verified: every number matched the raw JSON.
- **Process:** the gate was consolidated to one file (the questions doc) and the journal retired.

### Jul 16 · T30 — product create/edit

*Full write-up: `questions/sprint-A/task-30-reactive-form-create-edit.md`.*

- **One `ProductFormComponent`, mode inferred from `:id`**, on dedicated routes rather than a dialog — deep-linkable and F5-survivable. `getRawValue()` *is* `ProductToCreate = Omit<Product,'id'>`.
- `matAutocomplete` over `mat-select` so an admin can introduce a **new** brand or type, not only pick one.
- **Signal Forms evaluated and rejected** — stable in v22, so a real option; blocked because Material's form-field support is an open, unassigned, off-roadmap issue ([components#32072](https://github.com/angular/components/issues/32072)) while this form is `mat-form-field` throughout. ~2h rewrite against unbounded integration risk for zero user-visible gain.
- Double-submit guard is **UI-only by design** — server idempotency is reserved for the money path, not admin CRUD.
- Smoke-testing surfaced **BUG-15**. Gate was honest: *can build it, cannot yet explain why or predict how it breaks* — two EXPLAIN answers were read off study material in the same file.

### Jul 15 · T29 — admin product list

*Full write-up: `questions/sprint-A/task-29-reactive-facet-table.md`.*

- `httpResource<Pagination<Product>>` + `ShopParams` signal + `OnPush`, copying `admin-orders`.
- **Full facets** — search, brand/type multi-select, sort — reusing the shop's already-cached `brands`/`types` endpoints. Thumbnail column; ✏️/🗑️ rendered *disabled* until T30/T31 existed.
- `pageIndex` on the wire (BUG-12's lesson). Gate **skipped** — pattern-copy, no new concept.

### Jul 18 · BUG-15 — a PUT that never cleared the by-id cache

- `/admin/products/:id/edit` re-served the old product for up to the 10-min TTL, **while the list looked fresh** — so the bug was invisible from the page that mattered most.
- **Cause one:** the `[InvalidateCache]` pattern's trailing `\|` (`"api/products\|"`) matched list query-keys but never by-id keys like `/api/products/72`.
- **Cause two:** cache keys stored the **raw request path**, so `/api/Products/72` (capital — Scalar/OpenAPI) and `/api/products/72` (the Angular app) were two different keys the case-sensitive Redis pattern could not both clear.
- Fixed by dropping the `\|` and lowercasing the path in the key. Bonus: brands/types stop going stale when a product with a new brand is created.
- **A pre-existing backend defect that the T30 edit UI merely exposed.** 147 green; verified in Scalar (GET → PUT → GET returns fresh).

### Jul 18 · T31 — product delete

- The 🗑️ that T29 rendered disabled now routes through the existing `DialogService.confirm` — **reused, not rebuilt**: `afterClosed()` resolves `true \| undefined`, so Cancel, Esc and backdrop all fall through one `if (!confirmed) return`.
- **Refresh re-derives from the server via `reload()` rather than removing optimistically.** On a *paginated* list the optimistic path still leaves `count` stale — it buys a lying paginator for no saved round-trip. Safe because `DeleteProduct` carries `[InvalidateCache]` (BUG-15's fix).
- **Empty-last-page edge:** deleting the only row on a page > 1 steps *back* a page instead of showing "no products match" on a list that has plenty. Because the params signal **is** the resource's request, the step-back *is* the refetch — one GET, not two.
- Live-verified including the authorization failure path at the API — **403** for a signed-in non-admin, **401** anonymous. The Angular `adminGuard` only hides the UI.
- **Smoke-testing surfaced BUG-16**, which it then outranked.

### Jul 27 · BUG-16 — concurrency check on a detached entity

*Full write-up: `questions/sprint-A/bug-16-optimistic-concurrency-detached-entities.md`.*

- Editing a product's `QuantityInStock` **always 500d**. `UpdateProduct` called `Exists(id)` — a `SELECT EXISTS` that **loads nothing** — then `Update()` on a detached entity. `Attach` has no load-time snapshot, so EF took the submitted stock as *both* current and original and emitted `WHERE Id=7 AND QuantityInStock=42` against a row holding 10.
- **The check had never worked on this endpoint.** Since T13 it compared the new value against itself, passing by accident whenever stock was unchanged — which is why editing names and prices always worked, and why T30's smoke test never caught it.
- Fixed by loading the tracked entity via `GetByIdAsync`; the `Exists(id)` it replaces makes the extra SELECT net zero. `DbUpdateConcurrencyException` → **409**, and the interceptor's 409 branch now gates its cart-reload on `POST …/orders` so an admin conflict stays on the form.
- **Known limit, accepted:** the original is captured microseconds before the UPDATE, so it guards the request's SELECT→UPDATE window, *not* form-open→save think-time. Two admins on stale forms still last-write-wins silently. 149 green.

### Jul 27 · Sprint A → live

- `main` was **two sprints behind** — still at Sprint 2 (Jun 18), so Sprint 3 had never been released. Landed as one squashed commit per sprint; both Azure deploys green.
- **`main` shares no git history with `development`** (orphan branch from the Jun 18 rewrite), so GitHub refuses any PR between them. `git-workflow.md`'s documented `gh pr create --base main` **cannot work**; the real mechanic is `git read-tree --reset -u <branch>` + commit + direct push.
- **The pre-deploy Release build caught a live blocker:** `System.Security.Cryptography.Xml` 10.0.9 had picked up five high-severity advisories since Jul 13 — unchanged code, changed advisory DB — and Release-only `TreatWarningsAsErrors` turned `NU1903` into 20 build errors that would have failed the Azure publish. Bumped to 10.0.10 and back-ported into the Sprint 3 commit so it stays a valid rollback target.
- **CI never flagged it** because NuGet resolves advisories at restore time against a live feed. A nightly run would have caught it days earlier — logged as a follow-up.

### Aug 4 · T37 — Playwright E2E

*Full write-up: `questions/sprint-B/task-37-playwright-e2e.md`.*

- The repo's first browser tests: 3 specs, 8 page objects, and a **self-starting environment** — `globalSetup` brings up Redis and seeds, `webServer` starts `dotnet run` + `ng serve` and tears both down, so `npx playwright test` is the entire command.
- **`customer-orders` depends on `checkout`**, so the order it asserts on is created by its own dependency — the assertion cannot pass on leftovers or fail on a clean DB.
- Stripe's iframes are targeted by exact `title`: Stripe mounts ~11 frames and most are untitled, so position is not viable — but the titles are **undocumented**, an observed detail a Stripe update can break.
- **Two genuine races, one rule.** The stepper's "Next" can be clicked while enabled yet not advance (Angular hasn't processed Stripe's async `change` event); `page.goto('/cart')` could abort the in-flight add-to-cart write. Both fixed by **asserting the result, never trusting the action**.
- **Found BUG-17** — after logout `/checkout` lands on `/cart`, because `authGuard` and `emptycartGuard` both redirect *imperatively* instead of returning a `UrlTree`, and the synchronous one wins. Left red on purpose and scoped out.

### Aug 13 · BUG-18 — object-level authorization (IDOR) *(PR #72)*

*Full write-up: `questions/sprint-B/bug-18-object-level-authorization.md`.*

- The cart key parameter is **gone** from `ICartService`, so `CartService` derives `cart:{ICurrentUser.Id}` and the old exploit no longer compiles. 8 call sites had to change — **and that failing build *was* the audit** of who had been trusting a client id.
- `ICurrentUser` is declared in **Application**, implemented in **API**, so Infrastructure never learns HTTP exists. `Id` **throws** rather than returning null, because `$"cart:{null}"` is `"cart:"` — one valid Redis key shared by every caller without an identity.
- `ShoppingCart.Id` **deleted, not ignored**, so a forged id is an unknown property with nowhere to bind.
- **Second bug the fix exposed:** the E2E's "B's cart is empty" passed for the wrong reason — only bootstrap ever fetched the cart, so after login a real user saw an empty cart until F5. A **positive** assertion was added alongside.
- Absorbed T20's `WebApplicationFactory` harness and **closes T12**. 152 green.
- **Found, not fixed:** `OrderService.CalculateDiscount` read `AmountOff`/`PercentOff` straight off the client-supplied coupon → became BUG-19.

### Aug 13 · Sprint B → live

- Closed (goal met, 2/2 + T12 and T20 absorbed) and released as one squashed commit. Live-verified: `/api/cart` returns **401** both bare and with a smuggled `?id=`.
- **Three defects surfaced between the gate and the deploy**, all fixed in PR #72.
- **`NU1903`:** `TestContainers` → `SSH.NET` carried a high-severity advisory that `TreatWarningsAsErrors` turned into a Release failure while `dotnet test` stayed green. No test project referenced it, so it was removed rather than version-chased.
- **CI had no Redis** — `API.Tests` runs against the real thing → health-gated `services:` container.
- **The test host had been booting on real Stripe credentials.** `AddApplicationServices` reads `StripeSettings:SecretKey` *eagerly*, before `WebApplicationFactory`'s configuration exists; locally user-secrets filled the gap, so the harness looked green while depending on one machine. On CI it failed at startup. Fixed with environment variables — **the same shape as BUG-18 itself: something looked verified because a value was quietly supplied by a source nobody chose.**
- The first hit took 52s — serverless SQL resuming, which is BUG-8's retry working, not a fault.

### Aug 18 · Sprint C #1 — SQL credential rotation

- The old password was `Pa$$w0rd` and sat **in git history** across 5 Sprint 1 commits. T3 removed it from `appsettings.json`, but **a removal is not a rotation** — it stayed valid for four months.
- New 28-char password → App Service `SqlServerAzureConnection` (its only home) → restart → `/api/products` returned real data, proving the reconnect.
- **All three client-IP firewall rules were stale.** The current IP matched none, because the ISP hands out dynamic addresses in the same `/24` — each rule was a standing grant to an address a stranger now holds. Deleted; only `AllowAllWindowsAzureIps` remains.

### Aug 20 · BUG-20 — every coupon rejected for ~2 months

- `GET /api/coupons/{code}` returned 400 for **every** valid code, so discounts were dead on the live demo.
- `promotion.coupon` is an **expandable** reference: unexpanded, Stripe sends a bare id string, Stripe.net leaves `Coupon` null, and `CouponService.cs:22`'s guard is never true.
- **Broke at `fix(deps)` #50** (Jun 26), which bumped Stripe.net 51→52 — **an SDK version pins the API version, so a dependency refresh silently performed an API migration.** Stripe documents that strongly-typed SDKs cannot target a different version, so the control is process, not config.
- **Nothing caught it:** `CouponService` has no DI seam and is `[ExcludeFromCodeCoverage]`, and coupons were not in the deploy gate.
- Fix `Expand = ["data.promotion.coupon"]` + `Active = true`, which also closed a latent second defect — two codes shared `gimme5`, and `FirstOrDefault()` picked by list order, not by active.

### Aug 20 · deps refresh *(PR #74)*

- Run **as an audit, not a chore**, straight after BUG-20 proved what a blanket refresh can do — and that **the test suite cannot see that kind of break**. The Stripe money path was verified by hand afterwards.
- **`xunit.v3` held at 3.2.2:** 4.0.0 makes Microsoft Testing Platform the default, but .NET 10's `dotnet test` still routes via VSTest, so the build errors; the `global.json` runner switch clears that but then `dotnet test` finds **zero tests** while the test `.exe` runs all of them. Unresolved discovery issue, zero production benefit, retry post-v1.0.
- Policy for both exceptions written into `docs/build-deployment.md`.

### Aug 20 · BUG-19 — a coupon validated once, then trusted forever *(PR #75)*

- **Three cases proven live.** Forging `amountOff` through `POST /api/cart` wrote **−$999.00** onto order #37. Forging `coupon.id` charged **$100 on a $185 order**, because `ApplyDiscountAsync` re-read Stripe by an id **the client controls**. Worst: a coupon applied before its campaign was retired still paid out — **$90 on $180, with the order total and the charge agreeing**, so `PaymentMismatch` never fired.
- `POST /api/cart` writes the request body to Redis verbatim, so none of this needs stolen credentials, and **the third case needs no attacker at all**.
- **Fix:** `OrderService` *and* `PaymentService` both re-derive from `cart.Coupon.PromotionCode`, the only field the customer typed. **By code, not by id, because it fails closed** — `promotion_codes list` with `Active = true` returns nothing for a retired campaign, while `coupons retrieve` ignores `valid` entirely.
- **Two defects the fix would otherwise have introduced:** `AmountOff` is `long?` cents, so `/ 100` truncated $5.50 → $5.00 — harmless in the order record, a 50¢ overcharge the moment the charge read it; and a blank `promotionCode` left Stripe's `Code` filter **unset**, which drops the filter rather than matching nothing.
- **Two existing unit tests asserted the bug** — that the cart's `AmountOff` reaches `Order.Discount` — and were inverted. 159 green (was 152).
- `payment.md`'s checkout diagram was **wrong about when the payment intent is created** (page load, not the Delivery step) — a belief that cost four attempts at Test B.

### Aug 21 · T47 — the README *(PR #76)*

- The repo had none, and it is the file a recruiter actually opens. Written for **both** readers with the interviewer's half first; shown through the **live link plus screenshots and GIFs**, because they fail independently and do different jobs; and **linking out rather than duplicating**, so no fact exists in two places to drift.
- Checked against two real big-tech READMEs (Netflix/dispatch, facebook/react) rather than recall: both are ~one screen with a picture high up. The draft was cut **207 → 134 lines** — every fact kept, the prose around them removed.
- **Verifying instead of assuming caught three defects.** The stock-race section claimed a 409 where `OrderService` rethrows as `InvalidOperationException` → **400**. "Run it locally" **would not have worked on a fresh clone**: nothing pins port 7096 (`launchSettings.json` sets no `applicationUrl`) while the client hardcodes it — `docs/build-deployment.md` had the same gap. And Node 24, not 20.

### Aug 21 · T48 — the C4 container diagram *(PR #77)*

- Drawn in **Mermaid** so it lives in git as text and diffs like code, rather than as a stale PNG. **Container level only** — the Context diagram was drawn, reviewed and cut, because with a single external system it is a strict subset of the Container view.
- **The spike's own options were wrong first time:** they described the four .NET layers as C4 "containers", when c4model.com says a container is *"something that needs to be running"*. Core/Application/Infrastructure/API are four projects inside **one** box. Caught only after every decision was made — **a wrong option description produces a confident decision about the wrong thing**, and it hides afterwards because the Decision still reads well. `spikes/README.md` now requires any named model or spec in an option to be checked against vendor docs and cited.
- **Verifying the drawing against the code found two more:** the diagram showed only the API talking to Stripe, when `stripe.service.ts:117` confirms the payment **from the browser** — the API only creates the intent, so card data never reaches it. And Stripe was listed among "what has to be running" in two docs, though it is an external system.
- Renders verified on GitHub, not only locally.

### Aug 30 · T52 — open redirect, and guarding a non-bug *(PR #78)*

*Full write-up: `questions/sprint-C/task-52-open-redirect-and-url-parsing.md`.*

- **There was no defect to fix.** `navigateByUrl` parses its argument into a `UrlTree`, which holds only segments, query params and a fragment — **no field for a scheme or host** — so `https://evil.com` becomes the segment `https:`, matches nothing, and lands on our own `/not-found`.
- **Shipped a guard anyway, because that safety is an accident of the router, not a rule.** One line — `window.location.href = this.returnUrl` for a full page reload after login — would remove it, and it would read as a UI fix in review.
- **The guard sits at the read site**, in the constructor, so `returnUrl` can only ever hold a checked value and the navigation call is free to change. `auth.guard.ts` and `error.interceptor.ts` both send a `returnUrl`; one check covers both and every future sender.
- Rule ported from ASP.NET Core's `SharedUrlHelper.IsLocalUrl` rather than invented — it rejects `//`, `/\` **and control characters**, because the browser strips tab/CR/LF and a validated string can change before it is used.
- **The client's first 14 unit tests**, on **Vitest** because root `overrides` force `glob 10` / `rimraf 5` on the whole tree and `karma@6.4.4` needs 7 / 3 — three unrelated-looking failures, one cause. Plus the CI step that makes deleting the guard fail.

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
- **Arithmetic:** ~50 pts remaining vs ~30 pts capacity to Sep 6 (55 days @ ~0.55 pts/day with build hours
  reclaimed) → **~22 pts (~40%) descoped.**
- **Levers spent:** Aug 30 → **Sep 6** (+1 wk, hard limit) · build hours reclaimed · **AZ-204 pushed past v1.0**
  (it was double-booked against the same hours).
- **No slack left.** One more bad week = cut again immediately, don't grind. Cut order: README(47) >
  deploy(53) > admin CRUD(28–31) > E2E(37) > rest.

## Sprint A — Finish the Admin Panel *(Jul 14 – Jul 27 · closed Jul 27 — **goal met**, 5/5 + 2 unplanned bugs)*

- [x] T54 — ESLint + `@angular-eslint` + Prettier + Roslyn analyzers *(first — gates every PR after it)*
- [x] T28 — Dashboard frontend: 5 Material Cards + recent orders + low-stock table *(spillover)*
- [x] T29 — Product list: Material Table + Paginator *(spillover)*
- [x] T30 — Product create/edit: reactive form, Material inputs *(spillover)*
- [x] T31 — Product delete: reuse ConfirmationDialogComponent
- [x] BUG-15 — Product edit served stale cache (by-id key never invalidated)
- [x] BUG-16 — Editing a product's stock always 500s (concurrency check vs detached update)

## Sprint B — E2E Tests + IDOR Fix *(Jul 28 – Aug 11 · closed Aug 13 — **goal met**, 2/2 + T12 and T20 absorbed)*

- [x] T37 — Playwright E2E: 3 tests (checkout, auth+orders, admin) — timebox 4d
- [x] BUG-18 — Cart leaks between users. `/api/cart` takes the cart id from the client
      (`CartController.cs:7,17,26`), so any authenticated user can read **or overwrite** any cart by
      id — an IDOR. Logout compounds it: `_cart` and `cart_id` survive, so the next person on that
      browser sees the previous user's items. Root cause: cart identity is client-owned while the
      controller is `[Authorize]` — half-migrated between anonymous and user-owned carts. Fix: drop the
      key parameter from `ICartService` itself, so `CartService` derives `cart:{user id}` from a new
      `ICurrentUser` seam and every old call site stops compiling; delete `cart_id` entirely, clear
      `_cart` on logout. The same client-supplied id also reaches `POST /api/payments/{cartId}`
      and `CreateOrderDto.CartId` (`OrderService.cs:7,103`), so it extends to paying for and **placing
      an order from** another user's cart — money path. Order history is **not** affected. Found Aug 5
      smoke-testing BUG-17. Also delivers T20's `WebApplicationFactory` harness and **closes T12**.

## Sprint C — README + Demo Fixes *(Aug 12 – Aug 25)*

**Start here.** Only #1 is order-critical; the rest can flex with the day's energy.

| # | Item | Needs | Why here |
|---|---|---|---|
| ~~1~~ | ~~Rotate the Azure SQL password + audit the firewall~~ | ✅ done Aug 18 | Password was `Pa$$w0rd` **and in git history**; three stale client-IP firewall rules also removed |
| ~~2~~ | ~~BUG-19 — read the coupon discount from Stripe~~ | ✅ done Aug 20 | Same lesson as BUG-18, one field over — the pairing is the interview story |
| ~~3~~ | ~~T47 — the README~~ | ✅ done Aug 21 | 0.5d against a 2d estimate; T38 (images) now closes the sprint |
| 4 | T48 → T52 → T43 → BUG-10 → BUG-14 → T33 → BUG-3 → T38 | — | Plan order; T38 last, once the site's idea settles |
| — | Deploy-gate cleanup | mechanical | Slot into any low-energy gap |

- [x] T47 — README **create** *(#76 — 0.5d against a 2d estimate; no README existed before this)*
- [ ] T38 — README screenshots + GIFs *(last task of the sprint; placeholders already in the README)*
- [x] T48 — C4 **Container** diagram in Mermaid → `README.md` *(#77 — Context level cut Aug 21: drawn,
      then dropped as redundant)*
- [x] T52 — Open redirect: reject a non-local `returnUrl` *(#78 — no defect; kept as defence in
      depth. First Angular unit tests (14, Vitest) + a `npm test` CI step)*
- [x] BUG-20 — Every coupon rejected; `promotion.coupon` needs `Expand`. Broke at the Jun 26 Stripe.net
      51→52 bump *(an SDK version pins the API version — a dependency refresh was an API migration)*
- [x] BUG-19 — Coupon validated once, then trusted forever: by the order, the charge **and time**. Both
      paths now re-derive from the promotion code *(#75)*
- [ ] T43 — DB indexes: BuyerEmail, composite (Brand,Type), Price *(unique PaymentIntentId already exists)*
- [ ] BUG-10 — Top loading bar spins forever *(1d timebox — root cause unproven)*
- [ ] BUG-14 — "Add to cart" disables on click (`product-details.component.html:22`)
- [ ] T33 — `<mat-error>` on the checkout forms *(register already has it via `app-text-input`)*
- [ ] BUG-3 — Checkout: hint on an empty required address field
- [x] Rotate the Azure SQL password + audit the server firewall *(done Aug 18 — 28-char password; three
      stale client-IP rules removed)*
- [ ] Deploy-gate cleanup — retire the four legacy manual checks in `testing.md` *(they move into T20b's
      integration harness and Playwright; the gate currently grows every sprint and shrinks never)*
- [x] Sprint B Anki deck — 29 cards *(`questions/sprint-B/sprint-B-interview/`, local-only)*
- [x] T12 — Cart ownership — **closed by BUG-18**, same work

## Sprint D — Order Domain *(Aug 26 – Sep 8)*

- [ ] T40 — OrderStatus + append-only OrderStatusHistory *(3d — spike first: one enum or two?)*
- [ ] T41 — Order cancellation: POST cancel + Stripe refund *(refund plumbing already exists)*
- [ ] BUG-2 — Checkout: declined card can't retry in-session *(money path)*
- [ ] BUG-17 — Logout → `/checkout` doesn't redirect to login *(`auth.guard.ts` navigates instead of
      returning a `UrlTree`, and loses the race with `emptycart.guard.ts`). Found by T37, currently red.*
- [ ] T50 — API docs: `[ProducesResponseType]` + XML summaries *(re-sized M — 27 endpoints, 0 attributes)*
- [ ] T21 — Coverage gate: coverlet, fail PR < 60%
- [ ] Sprint C Anki deck

## Sprint E — Production Hardening *(Sep 9 – Sep 22)* — **v1.0 ships**

- [ ] T22 — Serilog structured logging *(App Service, no Seq)*
- [ ] T23 — Correlation IDs — one id per request, on every log line
- [ ] T42 — Soft delete + EF global query filter *(`ProductsController.cs:76` hard-deletes today)*
- [ ] T20b — Extend the API integration harness: auth, orders, payments happy paths
- [ ] T10 — Health-check endpoints
- [ ] Scheduled weekly CI advisory run (`on: schedule`)
- [ ] T49 — `docker-compose.yml`: Redis + SQL Server *(one-command local startup)*
- [ ] T35 — `NgOptimizedImage` on catalog images
- [ ] fix/BUG-16 — move the checkout cart-reload out of `errorInterceptor`
- [ ] fix/401-ctx — `HttpContextToken` instead of the 401 URL sniff

## Interview Prep *(Sep 23 – Sep 30)* — DO NOT FILL

- [ ] T53 — Final Azure smoke test *(non-negotiable, always last)*
- [ ] Sprint D + E Anki · full interview deck · re-read every gate doc · slack

> Descoped for v1.0 (~22 pts): see `Execution plan.md`.

