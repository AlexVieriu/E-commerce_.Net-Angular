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

Cart persisted in Redis via API. Cart ID stored in `localStorage` (`cart_id`), loaded by `InitService`.
Coupon state lives on `cart().coupon` — not a separate signal. Use `applyDiscount(code)` / `removeCoupon()` on the service.

## AccountService Signals

`_currentUser: User | null` → `isAdmin = computed(() => currentUser()?.roles.includes('Admin') ?? false)`.
Set by `getUserInfo()` on init. Cleared by `logout()`.

## Rules / Constraints

- Never call `.set()` / `.update()` on a service signal from a component.
- All signals: `private` with `readonly` counterpart via `asReadonly()`.
- Use `computed()` for derived values — never recompute in templates.

## Common Mistakes

- Calling `.set()` from a component — bypasses service logic.
- Using `BehaviorSubject` for new state — use signals.
- Forgetting `asReadonly()` — signal appears readonly but isn't enforced.
