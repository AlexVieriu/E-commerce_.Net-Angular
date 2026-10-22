# Frontend Architecture

Standalone, zoneless, lazy-loaded Angular app.

The SPA is one of four containers. How it sits against the API, Redis, Azure SQL and Stripe is drawn in
the [README](../../../README.md#architecture).

## Folder Structure

```
src/app/
├── core/
│   ├── guards/        # CanActivateFn guards
│   ├── interceptors/  # Functional HTTP interceptors
│   └── services/      # Singleton services (state + HTTP)
├── features/          # Feature components (lazy-loaded)
├── layout/header/     # Persistent shell UI
└── shared/
    ├── components/    # Reusable UI
    ├── models/        # TS interfaces matching backend DTOs
    ├── directives/
    └── pipes/
```

## Key Choices

**Standalone** — no NgModules. Every component declares its own `imports: []`.

**Zoneless** — `provideZonelessChangeDetection()`. Angular re-renders **only when told**: a signal read in
a template changing, `markForCheck`, `ComponentRef.setInput`, a bound listener firing, or attaching a view
marked dirty by one of those. All components declare `ChangeDetectionStrategy.OnPush`, which is also the
v22 default — the line states intent, it does not change behaviour.

**App initializer** — `InitService.init()` before first render: loads cart, user, connects SignalR.

**Functional interceptors** — order matters (error → loading → auth on request; reversed on response).

**Component input binding** — `withComponentInputBinding()`. Route params bind to `input()` signals directly.

## Routing

All features lazy-loaded via `loadComponent` / `loadChildren`. Child routes in `features/<name>/routes.ts`.

**Shell + child routes pattern** (`features/admin/`) — a parent component (`AdminComponent`) renders
persistent layout (sidebar) plus a `<router-outlet>`; its route's `children` array swaps content into
that outlet as the URL changes (dashboard/products/orders). The parent and children never reference each
other directly — the Router matches the URL against `routes.ts` and inserts the matched component into
the outlet. One `canActivate` on the parent route guards the whole subtree, since reaching any child
requires the Router to first activate the guarded parent segment. Empty-path redirects into the default
child (`'' → 'dashboard'`) must use `pathMatch: 'full'` — with the default `'prefix'`, an empty path
matches every URL under it, causing an infinite redirect loop.

## Rules / Constraints

- Standalone components only — no NgModules.
- All components: `ChangeDetectionStrategy.OnPush` — the v22 default, kept explicit.
- Services → `core/services/`. Shared UI → `shared/components/`. Models → `shared/models/`.
- New routes: `loadComponent` or `loadChildren` — never eager in `app.routes.ts`.

## Common Mistakes

- `component:` instead of `loadComponent:` — breaks lazy loading.
- Showing service state a template reads from a **plain field**, or capturing a signal in a class field
  (`isBusy = svc.loading();`) — the view never updates (BUG-10, see `state-management.md`).
