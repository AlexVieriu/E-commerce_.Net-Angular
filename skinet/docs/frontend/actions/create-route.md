# How to: Create a Route

## When to use this

Adding a new lazy-loaded route to a feature.

## Steps

1. Create the component (see `create-component.md`).
2. If the feature has child routes, add to `features/<name>/routes.ts`. Otherwise add to `app.routes.ts`.
3. Use `loadComponent` for a single component, `loadChildren` for a child routes file.
4. Apply guards via `canActivate: [...]` if the route is protected.

## Example

Top-level route (`app.routes.ts`):
```typescript
{ path: 'shop', loadComponent: () => import('./features/shop/shop.component').then(c => c.ShopComponent) }
```

Child route (`features/orders/routes.ts`):
```typescript
export const orderRoutes: Routes = [
  { path: '', component: OrdersComponent },
  { path: ':id', component: OrderDetailComponent },
];
```

Registered in `app.routes.ts`:
```typescript
{ path: 'orders', canActivate: [authGuard], loadChildren: () => import('./features/orders/routes').then(m => m.orderRoutes) }
```

## Pitfalls

- `component:` instead of `loadComponent:` — eager loads the entire feature.
- Wrong guard order: `[adminGuard, authGuard]` — always `[authGuard, adminGuard]`.
