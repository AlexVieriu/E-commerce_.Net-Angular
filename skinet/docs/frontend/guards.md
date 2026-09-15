# Route Guards

Functional `CanActivateFn` guards.

## The rule: a guard answers, it does not navigate

**Return a `UrlTree`. Never `router.navigate(...)` + `return false`.**

> *"If you need to redirect the user, return a `URLTree` or `RedirectCommand`. Do **not** return `false`
> and then programmatically `navigate` the user."* — [Route guards](https://angular.dev/guide/routing/route-guards)

Why it is not just style (BUG-17):

- The router subscribes **every** guard in a `canActivate` array at once — `combineLatest` — so they all
  run. There is no short-circuit on execution.
- It then reads the results **in array order**: `true` → keep looking · still pending → wait · `false` or
  a `UrlTree` → that is the answer. So a denial at index 0 is used immediately; a denial at index 1 waits
  for index 0 to answer first.
- Because the router deliberately withholds its decision until earlier guards answer, a guard that calls
  `navigate()` itself moves the page **before** the router has decided. It cannot lose.

That is what broke: a logged-out visitor hitting `/checkout` got `/cart` and *"Your cart is empty"*,
because `emptycartGuard` navigated while `authGuard` was still resolving. The fix is not ordering — it is
that no guard navigates.

**Side effects follow the same logic.** A snackbar is not a return value, so it fires whatever the router
decides. A guard that might not be the one answering must stay quiet — see `emptycartGuard` and
`adminGuard` below.

## Registration

There is no single place. Guards are attached where the route is declared:

| Route | File | Guards |
|---|---|---|
| `/checkout` | `features/checkout/routes.ts` | `authGuard`, `emptycartGuard` |
| `/checkout/success` | `features/checkout/routes.ts` | `authGuard`, `orderCompleteGuard` |
| `/orders`, `/orders/:id` | `features/orders/routes.ts` | `authGuard` |
| `/admin` | `app.routes.ts` | `authGuard`, `adminGuard` |

## Guard Summary

| Guard | Protects | Returns on fail |
|---|---|---|
| `authGuard` | Authenticated routes | `UrlTree` → `/account/login?returnUrl=...` |
| `adminGuard` | Admin routes | `UrlTree` → `/account/login` |
| `emptycartGuard` | Checkout | `UrlTree` → `/cart` |
| `orderCompleteGuard` | Order-complete page | `UrlTree` → `/shop` |

## authGuard

1. `accountService.currentUser()` set → allow, no HTTP.
2. Otherwise → `getAuthState()` (GET `/api/account/auth-status`), then allow or return the login `UrlTree`
   with `returnUrl` set from `state.url`.

Step 2 is rarely reached: `provideAppInitializer` blocks bootstrap on `InitService.init()`, so
`currentUser` is already settled before any route activates. It still covers a cookie that dies
mid-session.

## adminGuard

Checks `accountService.isAdmin()` — a `computed()` over `currentUser().roles`.

Applied as `canActivate: [authGuard, adminGuard]` on the parent `/admin` route only, which guards the
whole `features/admin` subtree: reaching any child requires the Router to activate the guarded parent
segment first.

The permission snackbar is gated on `currentUser()`. An anonymous visitor is not short of a role, they
are short of a session, and `authGuard` is already sending them to login with a `returnUrl` attached.

## emptycartGuard

Returns `true` immediately when nobody is logged in, then checks the cart.

`clearCurrentUser()` nulls the user **and** the cart in one step (BUG-18), so after logout every visitor
looks cart-less. Standing down hands that case to `authGuard`, keeps *"Your cart is empty"* off the login
page, and makes the outcome independent of the `canActivate` array's order.

## orderCompleteGuard

Checks `orderService.orderComplete` — a plain boolean, not a signal.

## Rules / Constraints

- **Return a `UrlTree`; never navigate.** The whole of BUG-17.
- **A guard that might not be the one answering stays silent.** Snackbars fire regardless of what the
  router picks, so gate them on the condition that makes this guard the right one to speak.
- `authGuard` is listed first everywhere it is paired. With `UrlTree` this is belt-and-braces rather than
  load-bearing — `emptycartGuard` and `adminGuard` both stand down for anonymous visitors on their own.
- Guards use `inject()`, not constructor injection.
- Specs live in `core/guards/guards.spec.ts` — one file, since all four share the contract. They assert
  the destination *and* that `navigate` / `navigateByUrl` were never called.
