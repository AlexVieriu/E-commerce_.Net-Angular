# State Management

Signal-based state in `core/services/`. No NgRx, no BehaviorSubject.

## Pattern

```typescript
// service
private _cart = signal<Cart | null>(null);
readonly cart = this._cart.asReadonly();
itemCount = computed(() => this.cart()?.items.reduce((s, i) => s + i.quantity, 0));

setCart(cart: Cart) {
  return this.http.post<Cart>(url, cart).pipe(tap(r => this._cart.set(r)));
}
```

```typescript
// component — read only
cartService = inject(CartService);
count = this.cartService.itemCount; // use as count() in template
```

## CartService Signals

| Signal | Type | Computed from it |
|---|---|---|
| `_cart` | `Cart \| null` | `itemCount`, `totals` |
| `_selectedDelivery` | `DeliveryMethod \| null` | included in `totals` |

Cart persisted in Redis via API, keyed server-side by the logged-in user. **The client holds no cart
id** — `Cart` has no `id` field and nothing is written to `localStorage`.

The cart is session state: it loads with the session and clears with it. Three moments, and you need
all three — `InitService` runs only at app startup, so login must load the cart itself.

| Moment | What runs | Effect on `_cart` |
|---|---|---|
| app startup | `InitService.init()` | authenticated → fetch from the server |
| login | `AccountService.login()` | fetch from the server |
| logout, or any 401 | `AccountService.clearCurrentUser()` | cleared in memory only — the server keeps it |

```
BEFORE (BUG-18)                        AFTER
localStorage: cart_id                  auth cookie
      |                                     |
      v                                     v
GET /api/cart?id=<client string>       GET /api/cart
the browser owned the cart             the user owns the cart
-> survived logout, so the next        -> logout clears it from memory;
   person inherited it                    the server still has it at next login
```

Coupon state lives on `cart().coupon` — not a separate signal. Use `applyDiscount(code)` / `removeCoupon()` on the service.

## AccountService Signals

`_currentUser: User | null` → `isAdmin = computed(() => currentUser()?.roles.includes('Admin') ?? false)`.
Set by `getUserInfo()` on init. Cleared by `logout()`.

## Read-only server views: `httpResource`

For a component that only **reads** server data (no shared mutable state), skip the service-signal
pattern above and use `httpResource` directly in the component. It fetches when its URL/params change
and exposes `.value()`, `.isLoading()`, `.error()`, and `.reload()` — the loading/error/refresh states
come for free.

```typescript
// component — read-only view (admin-orders, admin-dashboard)
dashboard = httpResource<Dashboard>(() => environment.apiUrl + 'admin/dashboard');
// template: @if (dashboard.isLoading()) … @else if (dashboard.error()) … @else { dashboard.value() }
```

Use a `core/services/` signal instead when the data is **shared** or **mutated** across the app (cart,
current user). A dashboard nobody else reads doesn't need a service. Mutations still go through a service:
`admin-dashboard` reads via `httpResource`, but a refund/sync would call `AdminService`, then `.reload()`.

## Rules / Constraints

- Never call `.set()` / `.update()` on a service signal from a component.
- All signals: `private` with `readonly` counterpart via `asReadonly()`.
- Use `computed()` for derived values — never recompute in templates.

## Common Mistakes

- Clearing the cart on logout but not loading it on login — the badge stays empty until the user
  refreshes the page, because `InitService` only runs at app startup. Clear and load must be paired.
- Calling `.set()` from a component — bypasses service logic.
- Using `BehaviorSubject` for new state — use signals.
- Forgetting `asReadonly()` — signal appears readonly but isn't enforced.
