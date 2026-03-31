claude --resume 855033c5-43ce-41cf-afc0-7ed0bd5ba0b5

# Execution Plan — Skinet (v5)

> .NET 11 + Angular 22 | Azure-only | Solo dev | B2C physical goods
> **47 tasks | 6 sprints (2 wk each) | Deadline: June 2026**
> Mid .NET / junior Angular | CV project | Claude Code for speed, saved time for learning

**Estimates:** S (<1d), M (1-3d), L (1wk+) — without Claude Code.
**LEARN:** 12 marked tasks. Claude implements -> you read every line + do exercise -> append to `questions/Technical Questions.txt`.
**Git:** Branch per sprint, conventional commits, PR to main at sprint end.

**Deadline reality (March 22 → June 2026 ≈ 50 working days):**
Normal case needs 49d — you have no slack. One slow sprint = one cut task somewhere.
CV priority order if forced to cut: Sprint 3 (admin) > Sprint 6 (README) > Sprint 2 (tests) > rest.
Never sacrifice working Azure deploy + README. That's what job hunters actually look at.

---

## What Already Exists

Full CRUD products/orders/cart/payments/coupons | Cookie Identity (Admin+Customer) | Stripe (PaymentIntent+webhook+refund+coupons) | SignalR notifications | Redis cart+cache | Angular 22 zoneless+signals+Tailwind+Material | 4-step checkout | Admin orders+refund | Guards (auth/admin/emptycart/orderComplete) | Rate limiting | ExceptionMiddleware+errorInterceptor | GitHub Actions CI/CD to Azure | 18 docs | 14 test files | Security audit done (stock race condition remains)

---

## Sprint 1 — Security + CI/CD

> Hardened app + working CI. Mostly .NET config warm-up.
> Order: CI(1) -> Secrets(3,4) -> Identity(5,6,7) -> FluentValidation(8) -> CORS/CSP(9) -> Cart ownership(12) -> Pagination(11) -> Health(10) -> Docker(2)

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 1 | GitHub Actions: PR trigger, `dotnet test`, `npm ci`, .NET 11 + Angular 22 | S | -- | Pipeline validates all future work |
| 3 | Secrets -> User Secrets (dev) + Azure Key Vault (prod). Deploy config BEFORE rotating keys | M | -- | LEARN: Config provider chain. CRITICAL: keys in git history |
| 4 | Admin seed password -> env var / Key Vault | S | 3 | Hardcoded Pa$$w0rd |
| 5 | Identity password: min 12, upper+number+special | S | -- | Batch 5+6+7 |
| 6 | Account lockout: 5 attempts, 15 min | S | -- | |
| 7 | Cookie: SameSite=Strict, Secure, HttpOnly, 24h | S | -- | |
| 8 | FluentValidation: auto-validation, CreateOrderDto + CreateProductDTO validators, strip ClientSecret from cart | M | -- | LEARN: FluentValidation vs Data Annotations |
| 9 | CORS explicit + security headers (HSTS, X-Content-Type, X-Frame, Referrer, CSP w/ `unsafe-inline` for Material) | M | -- | Test visually after CSP |
| 12 | Cart ownership: userId in Redis, verify on read/write. Angular stops nanoid for authed users | M | -- | Security bug — has Angular side |
| 11 | Pagination: TotalPages, HasNext, HasPrev on `Pagination<T>` | S | -- | |
| 10 | Health checks: SQL Server + Redis, `/health` + `/ready` | S | -- | |
| 2 | Dockerfile multi-stage + `.dockerignore` | M | -- | LEARN: Why `COPY *.csproj` before `COPY .`? Not needed until Sprint 6 |

Sprint end: verify local + Azure deploy. Update README.

**Time:** Best 5d | Normal 8d | Worst 12d
- **Best:** Key Vault connects first try, CSP doesn't break Material. Use extra days to write out "secrets management" + "cookie security" interview answers. Start task 13.
- **Normal:** Key Vault auth needs iteration, CSP needs `unsafe-inline` tuning. Finish sprint, do README update, move on.
- **Worst:** Azure credentials block you 3+ days (common first-time setup). Cut Docker(2) — not needed until S6. **Sprint 1 is invisible on CV. Don't let it drain time from Sprint 3.**

---

## Sprint 2 — Testing + Data Integrity

> Fix race condition + idempotency + coverage. All C#. TDD: failing test first.
> Order: Race condition(13-15) -> Idempotency(16,18) -> Price detection(17) -> Tests(19-21)

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 13 | Failing test for stock race condition + fix: `[ConcurrencyCheck]` on QuantityInStock, catch `DbUpdateConcurrencyException` | M | -- | LEARN: Optimistic vs pessimistic concurrency |
| 14 | Wrap order creation in `BeginTransactionAsync()` via IUnitOfWork, rollback on failure | M | 13 | LEARN: Transaction isolation levels |
| 15 | Verify cart delete is AFTER commit, not inside transaction | S | 14 | Already done, just verify |
| 16 | Stripe idempotency: `IdempotencyKey = cart.Id` | S | -- | |
| 18 | Order idempotency: unique constraint on PaymentIntentId, return existing if duplicate | S | -- | Group with 16 (both idempotency) |
| 17 | Price change detection: cart vs DB prices, 409 + snackbar + redirect to cart | M | -- | No dialog, just snackbar |
| 19 | Expand unit tests: specs, discount edges, validators. Target 80% backend | M | -- | Build on 14 existing files |
| 20 | Integration tests: WebApplicationFactory + TestContainers (SQL+Redis) | L | 19 | LEARN: TestContainers. Timebox 3 days |
| 21 | Coverage gate: coverlet, fail PR < 60% | S | 19,1 | |

Sprint end: full test suite green. Verify Azure. Update README.

**Time:** Best 5d | Normal 9d | Worst 14d
- **Best:** Race condition fix is clean, TestContainers clicks fast. Prep "optimistic concurrency" + "transaction isolation" answers. Start Serilog(22).
- **Normal:** TestContainers setup takes 2d, coverage lands at 55% not 80% — gate passes at 60%, move on.
- **Worst:** Integration tests are flaky (Docker/CI differences). Timebox at 3d, fallback EF InMemory. Cut coverage gate(21). **Talking point:** Even partial coverage beats zero. "We had a race condition — here's how I fixed it" = strong interview story.

---

## Sprint 3 — Admin Panel + Observability

> Most clickable CV feature. Backend first, then Angular.
> Pattern: copy ShopComponent's httpResource + Material table. Don't break existing orders/refund.

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 22 | Serilog + Seq: Console (dev), Seq (prod), add Seq to docker-compose | M | -- | LEARN: Structured logging vs interpolation |
| 23 | Correlation IDs: middleware -> LogContext -> response headers | S | 22 | |
| 24 | Bogus seed: 50+ products, 10+ users, 20+ orders. `dotnet run -- --seed` | M | -- | Need data before admin pages |
| 27 | Dashboard backend: `GET /api/admin/dashboard` (counts, revenue, lowStock, recent) | S | -- | Pure C#, finish all backend before Angular |
| 25 | Admin layout: parent + sidebar + child routes (/admin/dashboard, /products, /orders) | M | -- | LEARN: Lazy loading + child routes. Verify orders still work |
| 26 | Order status update: `PUT /api/admin/orders/{id}/status` + Material dropdown | S | 25 | |
| 28 | Dashboard frontend: 3 Material Cards + recent orders table | M | 25,27 | |
| 29 | Product list: Material Table + Paginator, wire to existing GET /api/products | M | 25 | Copy existing admin table pattern |
| 30 | Product create/edit: reactive form, Material inputs. Text URL for image (no upload) | M | 25 | |
| 31 | Product delete: reuse ConfirmationDialogComponent | S | 29 | |

Sprint end: test CRUD products, dashboard stats, refund still works. Verify Azure. Update README.

**Time:** Best 5d | Normal 8d | Worst 12d
- **Best:** Backend flies (pure C#), Angular copies ShopComponent pattern cleanly. Start Sprint 4 quick wins (32,33).
- **Normal:** Angular child routes + guards take 2d (junior Angular risk). Refund breaks, you fix it. Dashboard shows real Bogus data.
- **Worst:** Child routes cause 3d of confusion. Ship product list(29) without form(30). **Never cut this sprint short.** Working admin CRUD is what separates you from "followed a tutorial" devs on CV reviews.

---

## Sprint 4 — Frontend Quality + UX

> Polish existing features. Isolated Angular tasks, easiest first.
> Order: Leak audit(32) -> Forms(33) -> Skeleton(34) -> Images(35) -> A11y(36) -> Playwright(37) -> README(38)

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 32 | Memory leak audit: unsubscribed Observables -> `takeUntilDestroyed()` | S | -- | Likely minimal (signals+httpResource) |
| 33 | Form errors: `<mat-error>` on login/register via TextInputComponent | S | -- | |
| 34 | Skeleton loaders: SkeletonCardComponent + SkeletonRowComponent | M | -- | |
| 35 | NgOptimizedImage: `ngSrc` + priority/lazy on product images | S | -- | Mostly HTML changes |
| 36 | Accessibility: axe on home/shop/checkout. Fix ARIA, keyboard nav, focus | M | -- | Just 3 pages |
| 37 | Playwright E2E: 3 tests (checkout, auth+orders, admin). Timebox 4 days | L | 1 | LEARN: Testing pyramid + Page Object Model |
| 38 | README update with UX screenshots | S | -- | |

Sprint end: Playwright + backend tests green. Verify deploy. axe score on 3 pages.

**Time:** Best 4d | Normal 8d | Worst 12d
- **Best:** Signals/httpResource means near-zero leaks, skeleton loaders are fast. Start OrderStatus(40) early.
- **Normal:** Playwright setup is painful, CI is flaky — run locally only. axe flags 5-10 ARIA issues, fix the obvious ones.
- **Worst:** Playwright eats the sprint. Timebox at 4d, ship 2 tests not 3. Cut a11y(36) to keyboard nav only. **Talking point:** Even 2 E2E tests signals testing maturity that most candidates don't have.

---

## Sprint 5 — Domain Expansion

> Domain modeling depth. Backend comfort zone.
> Order: Status(40) -> Cancel(41) -> Email(39) -> Soft delete(42) -> Indexes(43) -> Images(44) -> CancToken(45) -> Tests(46)

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 40 | OrderStatus: add Processing/Shipped/Delivered/Cancelled + append-only OrderStatusHistory | M | -- | LEARN: State machine + audit trail. Foundational — unlocks 41 |
| 41 | Order cancellation: POST cancel, only Pending/PaymentReceived, Stripe refund if paid. Log email (decouple from 39) | M | 40 | |
| 39 | Email: IEmailService + Channel\<T\> BackgroundService. Dev: log. Prod: MailKit/Azure Comms. Graceful shutdown | M | -- | LEARN: Channel\<T\> producer-consumer. Infrastructure — no blockers |
| 42 | Soft delete: IsDeleted+DeletedAt, global query filter, admin IgnoreQueryFilters | S | -- | LEARN: EF Core global query filters |
| 43 | DB indexes: unique PaymentIntentId, BuyerEmail, composite (Brand,Type), Price | S | -- | One migration |
| 44 | Multiple images: ProductImage entity + data migration (PictureUrl -> IsMain) | M | -- | Without migration, detail page breaks |
| 45 | CancellationToken on all controller -> service -> repo -> EF calls | M | -- | 30+ signatures. Controllers+services first |
| 46 | Unit tests: cancellation, soft delete, OrderStatusHistory, discount edges | M | 40,41,42 | |

Sprint end: full test suite. Test cancel, soft delete, status history. Verify Azure. Update README.

**Time:** Best 5d | Normal 8d | Worst 11d
- **Best:** State machine is clean, EF migrations work first try, Channel<T> pattern clicks. Pick 1 from "If Ahead".
- **Normal:** Cancellation + Stripe refund edge cases take an extra day. Email channel needs re-reading. All features tested.
- **Worst:** CancellationToken (30+ signatures) bloats. Defer(45) to S6 polish. Cut multiple images(44) — cosmetic. **Talking point:** OrderStatus history + cancellation = strong domain modeling story. "How do you model state changes?" — you have a real answer.

---

## Sprint 6 — Polish + Standout

> Recruiter spends 30 seconds on your repo. README day 1 = highest ROI.
> Order: README(47) -> Security fix(52) -> Diagram(48) -> API docs(50) -> Docker(49) -> Email confirm(51) -> Smoke test(53) -> Reviews if ahead(54)

| # | Task | E | Dep | Notes |
|---|------|---|-----|-------|
| 47 | README rewrite: overview, stack, diagram, screenshots/GIFs, "how to run", Azure link | M | -- | **DAY 1. Highest ROI.** |
| 52 | Open redirect: validate returnUrl starts with `/` | S | -- | Security fix — do early |
| 48 | C4 diagram (Context+Container) in Mermaid -> `docs/architecture.md` | M | -- | |
| 50 | API docs: `[ProducesResponseType]` + XML summaries | S | -- | |
| 49 | docker-compose full: SQL Server + Redis + Seq | M | -- | |
| 51 | RequireConfirmedEmail. Dev: auto-confirm. Prod: via IEmailService | S | 39 | |
| 53 | Final Azure smoke test: full user + admin journey | S | -- | Non-negotiable. After all changes |
| 54 | Reviews + ratings: Review entity, AverageRating, star display | L | -- | **CUT if behind. README > Reviews** |

Sprint end: final deploy + smoke test. Share with someone.

**Time:** Best 4d | Normal 8d | Worst 12d
- **Best:** README + diagram take 1d each, everything clicks. Ship Reviews(54) as the stretch goal. Apply to jobs.
- **Normal:** README takes 2d (screenshots, GIFs, wording). C4 diagram needs iteration. Deployed, smoke test passes.
- **Worst:** Running out of runway. **Hard priority: README(47) > smoke test(53) > diagram(48) > cut everything else.** A mediocre app with a great README beats a polished app nobody can run. This is what recruiters actually see.

---

## Progress

- [ ] Sprint 1 — Security + CI/CD (12 tasks)
- [ ] Sprint 2 — Testing + Data Integrity (9 tasks)
- [ ] Sprint 3 — Admin Panel + Observability (10 tasks)
- [ ] Sprint 4 — Frontend Quality + UX (7 tasks)
- [ ] Sprint 5 — Domain Expansion (8 tasks)
- [ ] Sprint 6 — Polish + Standout (8 tasks)

Every sprint end: E2E run, Azure verify, README update.

## Tech Stack Additions

| Sprint | Added |
|--------|-------|
| 1 | Azure Key Vault, FluentValidation, NWebsec |
| 2 | TestContainers, Coverlet |
| 3 | Serilog + Seq, Bogus |
| 4 | Playwright, axe DevTools |
| 5 | MailKit / Azure Comms |
| 6 | Mermaid |

## LEARN Summary (12 exercises)

| # | Concept | Interview Question |
|---|---------|-------------------|
| 2 | Docker multi-stage | "Optimize Docker size/build?" |
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
| Integration tests eat sprint | Timebox 3d, fallback EF InMemory |
| Admin Angular too slow | Backend first; ship list without form |
| Admin refactor breaks orders | Verify list+detail+refund post-refactor |
| Playwright CI fails | Run locally only |
| CancellationToken too big | Controllers+services first |
| Email not ready for cancel | Log "would send", decoupled |
| No time for Reviews | Cut. README+diagram+smoke = min Sprint 6 |
| Falling behind | Cut lowest task. Never cut README or sprint-end verify |

## Skipped

OAuth2/JWT, multi-currency, i18n, K8s, GitOps, CSRF, Prometheus/Grafana/Jaeger/OTel, Outbox, Service Bus, CQRS, Domain events, PWA, SSR, file upload, Angular unit tests, and more.
