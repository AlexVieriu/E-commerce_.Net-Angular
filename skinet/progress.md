# Progress Log

## Daily Log

| Date | Task | Notes |
|------|------|-------|
| Apr 2 | S1: T1,3,4,5,6,7 | Sprint 1 complete. Azure verified. |
| Apr 6 | T9, BUG-1 | CORS + security headers, checkout Next button fix |
| Apr 7 | T15, T16, T18 | Cart delete ordering, Stripe idempotency, order unique constraint |
| Apr 8 | docs | Refactored + updated all affected docs |
| Apr 8 | T17 | Price change detection: 409 + snackbar + redirect. Also fixed order detail routing (/order → /orders) |

---

## Sprint 1 — Security + CI/CD *(done: Apr 2)*

- [x] T1 — GitHub Actions: PR trigger, `dotnet test`, `npm ci`
- [x] T3 — Secrets → User Secrets (dev) + Azure Key Vault (prod)
- [x] T4 — Admin seed password → env var / Key Vault
- [x] T5 — Identity password: min 12, upper+number+special
- [x] T6 — Account lockout: 5 attempts, 15 min
- [x] T7 — Cookie: SameSite=Strict, Secure, HttpOnly, 24h

## Sprint 2 — Testing + Data Integrity *(Apr 6 – Apr 19)*

- [x] T9 — CORS explicit + security headers
- [x] T13 — Stock race condition: `[ConcurrencyCheck]` + `DbUpdateConcurrencyException`
- [x] T14 — Transaction: `BeginTransactionAsync()` via IUnitOfWork
- [x] T15 — Verify cart delete is AFTER commit
- [x] T16 — Stripe idempotency: `IdempotencyKey = cart.Id`
- [x] T18 — Order idempotency: unique constraint on PaymentIntentId
- [x] BUG-1 — Checkout: Address tab "Next" button stays disabled
- [x] T17 — Price change detection: cart vs DB, 409 + snackbar + redirect
- [ ] T19 — Expand unit tests: specs, discount edges, validators. Target 80%

## Sprint 3 — Admin Panel *(Apr 20 – May 3)*

- [ ] T8 — FluentValidation: auto-validation, CreateOrderDto + CreateProductDTO
- [ ] T24 — Bogus seed: 50+ products, 10+ users, 20+ orders
- [ ] T27 — Dashboard backend: `GET /api/admin/dashboard`
- [ ] T25 — Admin layout: parent + sidebar + child routes
- [ ] T26 — Order status update: `PUT /api/admin/orders/{id}/status`
- [ ] T28 — Dashboard frontend: 3 Material Cards + recent orders table
- [ ] T29 — Product list: Material Table + Paginator
- [ ] T30 — Product create/edit: reactive form, Material inputs

## Sprint 4 — Frontend Quality + UX *(May 4 – May 17)*

- [ ] T31 — Product delete: reuse ConfirmationDialogComponent
- [ ] T32 — Memory leak audit: `takeUntilDestroyed()`
- [ ] T33 — Form errors: `<mat-error>` on login/register
- [ ] T34 — Skeleton loaders: SkeletonCardComponent + SkeletonRowComponent
- [ ] T35 — NgOptimizedImage: `ngSrc` + priority/lazy
- [ ] T37 — Playwright E2E: 3 tests (checkout, auth+orders, admin)
- [ ] T38 — README update with UX screenshots

## Sprint 5 — Domain Expansion *(May 18 – May 31)*

- [ ] T40 — OrderStatus: Processing/Shipped/Delivered/Cancelled + OrderStatusHistory
- [ ] T41 — Order cancellation: POST cancel + Stripe refund
- [ ] T39 — Email: IEmailService + Channel\<T\> BackgroundService
- [ ] T42 — Soft delete: IsDeleted+DeletedAt + global query filter
- [ ] T43 — DB indexes: unique PaymentIntentId, BuyerEmail, composite
- [ ] T46 — Unit tests: cancellation, soft delete, OrderStatusHistory
- [ ] T20 — Integration tests: WebApplicationFactory + TestContainers

## Sprint 6 — Polish + Standout *(Jun 1 – Jun 14)*

- [ ] T47 — README rewrite: overview, stack, diagram, screenshots/GIFs
- [ ] T52 — Open redirect: validate returnUrl starts with `/`
- [ ] T48 — C4 diagram (Context+Container) in Mermaid
- [ ] T50 — API docs: `[ProducesResponseType]` + XML summaries
- [ ] T49 — docker-compose full: SQL Server + Redis + Seq
- [ ] T51 — RequireConfirmedEmail
- [ ] T53 — Final Azure smoke test

### If Ahead

- [ ] T10 — Health checks: SQL Server + Redis
- [ ] T22 — Serilog + Seq
- [ ] T23 — Correlation IDs
- [ ] T21 — Coverage gate: fail PR < 60%
- [ ] T2 — Dockerfile multi-stage
- [ ] T12 — Cart ownership: userId in Redis
- [ ] T11 — Pagination: TotalPages, HasNext, HasPrev
- [ ] T36 — Accessibility: axe on home/shop/checkout
