# Frontend Architecture

Standalone, zoneless, lazy-loaded Angular app.

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

**Zoneless** — `provideZonelessChangeDetection()`. All components must use `ChangeDetectionStrategy.OnPush`.

**App initializer** — `InitService.init()` before first render: loads cart, user, connects SignalR.

**Functional interceptors** — order matters (error → loading → auth on request; reversed on response).

**Component input binding** — `withComponentInputBinding()`. Route params bind to `@Input()` directly.

## Routing

All features lazy-loaded via `loadComponent` / `loadChildren`. Child routes in `features/<name>/routes.ts`.

## Rules / Constraints

- Standalone components only — no NgModules.
- All components: `ChangeDetectionStrategy.OnPush`.
- Services → `core/services/`. Shared UI → `shared/components/`. Models → `shared/models/`.
- New routes: `loadComponent` or `loadChildren` — never eager in `app.routes.ts`.

## Common Mistakes

- `component:` instead of `loadComponent:` — breaks lazy loading.
- Forgetting `OnPush` — silent performance regression in zoneless mode.
