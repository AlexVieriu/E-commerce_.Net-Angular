# Route Guards

Functional `CanActivateFn` guards. Registered via `canActivate: [...]` in `app.routes.ts`.

## Guard Summary

| Guard | Protects | Redirect on fail |
|---|---|---|
| `authGuard` | Authenticated routes | `/account/login?returnUrl=...` |
| `adminGuard` | Admin routes | `/account/login` |
| `emptycartGuard` | Checkout | `/cart` |
| `orderCompleteGuard` | Order-complete page | `/shop` |

## authGuard

1. `accountService.currentUser()` set → allow (no HTTP).
2. Otherwise → call `getAuthState()` (GET `/api/account/auth-status`).

## adminGuard

Checks `accountService.isAdmin()` (`computed()` from `currentUser().roles`).
On fail: snackbar + redirect to `/account/login`.
Applied as `canActivate: [authGuard, adminGuard]` on the parent `/admin` route only — guards the whole
`features/admin` child-route subtree (dashboard/products/orders), since reaching any child requires the
Router to activate the guarded parent segment first. `authGuard` always runs first.

## emptycartGuard

Cart null or `itemCount === 0` → snackbar "Your cart is empty" + redirect to `/cart`.

## orderCompleteGuard

Checks `orderService.orderComplete` (plain boolean, not a signal). False → redirect to `/shop`.

## Rules / Constraints

- `authGuard` must precede `adminGuard` — admin check assumes the user is loaded.
- Guards use `inject()`, not constructor injection.
