# Docs Index

Map of `skinet/docs/`. Read the doc matching your task **before** starting it.
Replaces the old `Docs RoadMap.txt`. When you add/rename a doc, update this list.

## Cross-cutting (`docs/`)

- **build-deployment.md** — docker-compose, dev run (ports), prod build (`ng` → `src/API/wwwroot`), SQLite↔SqlServer, secrets, Azure config map, CSP/CORS
- **testing.md** — all verification: per-task / pre-deploy / post-deploy **checklists** (top) + how to write tests (reference, bottom)
- **git-workflow.md** — branch / PR / merge flow (pairs with the deploy gate in `testing.md`)
- **documentation.md** — how to write docs here *(DO NOT EDIT)*

## `backend/`

- **architecture.md** — Clean architecture layers (Core → Application → Infrastructure → API)
- **data-layer.md** — StoreContext, GenericRepository, UnitOfWork, migrations
- **specifications.md** — Specification pattern (ISpecification, BaseSpecification)
- **api-patterns.md** — BaseApiController conventions, ApiErrorResponse, FluentValidation (validators + `ValidationFilter`)
- **controllers.md** — controller specifics / endpoint conventions
- **error-handling.md** — ExceptionMiddleware, problem details
- **auth.md** — cookie Identity, roles, ClaimsPrincipal extensions
- **caching.md** — ResponseCacheService, Redis, `[Cache]` / `[InvalidateCache]`
- **payment.md** — Stripe, PaymentService, webhook, order status flow
- **signalr.md** — NotificationHub real-time order push

## `frontend/`

- **architecture.md** — standalone components, lazy routes, core/shared/features split
- **state-management.md** — signal-based state in services (cart, account)
- **interceptors.md** — auth / loading / error interceptors
- **guards.md** — auth / admin / emptycart / orderComplete route guards
- **error-handling.md** — error interceptor, snackbar, server-error/not-found pages
- **actions/** — step-by-step how-tos: `create-component`, `create-service`, `create-route`, `create-model`
