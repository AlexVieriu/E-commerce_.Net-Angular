# Interceptor cleanup — does the 409 recovery still fire from its new home?

The cart reload moved out of `errorInterceptor` into `CheckoutConflictService`, called by
`StripeService.createOrUpdatePaymentIntent()` and `OrderService.createOrder()`. The dead 401 skip for
`account/user-info` is gone.

**Why a file:** runs 4 and 5 are the only paths with no automated coverage — `StripeService` calls
`loadStripe()` in its constructor, so there is no DI seam (`testing.md`). Runs 1–3 are cheap insurance: the
diff wrapped both checkout funnels in `catchError` and removed a catch from app startup.

Needs Redis, the API and the client. Runs 2 and 5 need Stripe:

```bash
docker compose up -d redis
dotnet run --project skinet/src/API/API.csproj
cd skinet/client && npx ng serve
stripe listen --forward-to https://localhost:7096/api/payments/webhook
```

Run 5 also needs a promotion code you can **deactivate mid-test** in the Stripe dashboard — not `gimme10`
or `gimme5`, which the deploy gate expects to keep working.

## The runs

Base: `https://localhost:4200`. Run them in order — 1 and 2 fail loudest if the diff is wrong.

### 1 — the app still starts for a visitor with no session

- **Do:** log out, then hard-refresh the app.
- **Expect:** the page renders, the splash clears, the navbar shows Login / Register.
- **Why:** `init.service.ts` lost its 401 catch. If anything on the startup path now errors, the app
  initializer rejects and you get a blank page, not a degraded one.

### 2 — a real payment still goes through

- **Do:**
  - add an item, go to `/checkout`, fill the address, pick a delivery method;
  - pay with `4242 4242 4242 4242`, any future expiry, any CVC;
  - submit the order.
- **Expect:** the order is placed and you land on `/checkout/success`.
- **Why:** both checkout funnels gained a `catchError`. A mistake in either pipe breaks every checkout,
  not just the error path.

### 3 — a dead cookie mid-session sends you to login

- **Do:**
  - log in, put something in the cart;
  - DevTools → Application → Cookies → **Clear** the `https://localhost:4200` entry
    (deleting the `.AspNetCore.Identity.Application` row on its own does not take);
  - change a cart quantity.
- **Expect:** `/account/login?returnUrl=%2Fcart`, and the navbar drops to Login / Register.
- **Why:** the 401 branch lost a condition. A navigation alone proves nothing — the redirect is driven by
  a real 401 response, so the check needs a request that writes.

### 4 — a price change at checkout returns you to the cart

- **Do:**
  - add a product to the cart and note its **name**;
  - `/checkout` → address → delivery → card → stop on the **Confirmation** step;
  - only now, change its price in the dev SQLite database (`skinet/data/store.db`):
    `UPDATE Products SET Price = Price + 10 WHERE Name = '<that product>';`
  - press **Submit order**.
- **Expect:** snackbar *"Prices have changed: <product> ($x → $y). Your cart has been updated."*, back on
  `/cart` at the new price, **no order** in `/orders`, no charge in Stripe.
- **Why:** this is the `POST /orders` 409 — it proves the recovery fires from `OrderService`.
- ⚠️ **Order matters.** Moving onto the Payment step fires `POST /payments`, which silently rewrites the
  cart price (`PaymentService.cs:88-89`). Change the price before that and `POST /orders` sees no mismatch.

### 5 — a coupon retired mid-session returns you to the cart

- **Do:**
  - Stripe dashboard (**test mode**) → Coupons → create one → open it → **add a promotion code**
    (the coupon alone is invisible to the app — it looks up promotion codes);
  - in the app, add an item and go to **`/cart`**, not `/checkout`;
  - apply the code — the discount shows;
  - back in the dashboard, **deactivate** that promotion code;
  - click **Proceed to checkout**.
- **Expect:** snackbar *"Your coupon is no longer valid. Your cart has been updated."*, the coupon is gone,
  the total is re-priced, and you land back on `/cart` instead of checkout.
- **Why:** this is the `POST /payments` 409, recovered inside `StripeService`. No spec can reach it —
  `loadStripe()` in the constructor leaves no DI seam.
- Apply on `/cart` deliberately: the coupon box only calls `POST /payments` while on `/checkout`, so the
  first intent call is the one you are testing.

### The admin 409 is not a run here

- `error.interceptor.spec.ts` already pins *"does not navigate, whichever endpoint 409d"* against
  `/api/products/5` — a spec cannot be skipped, a manual step can.
- Two browser tabs cannot produce the conflict anyway: `ProductsController.cs:53` loads the tracked entity
  inside the request, so the `[ConcurrencyCheck]` WHERE uses the value EF read milliseconds earlier, not
  the one the form opened with. Last write wins.

## Result — 2026-09-16

| # | Result | Notes |
|---|---|---|
| 1 | ✅ pass | |
| 2 | ✅ pass | real payment, landed on `/checkout/success` |
| 3 | ✅ pass | `?returnUrl=%2Fcart` |
| 4 | ✅ pass | *"Angular Generic Boot ($205.75 → $215.75)"* — no order, no charge |
| 5 | ✅ pass | *"Your coupon is no longer valid."* — coupon dropped, back on `/cart` |

**Not a failure:** typing `/checkout/success` yourself, or refreshing it, lands on `/shop`. That is
`orderCompleteGuard` — `orderComplete` is an in-memory boolean set only when Stripe returns `succeeded`.

⚠️ **Snackbar but no redirect on 4 or 5** = the interceptor still messages, but the funnel's `catchError`
never ran — check the status is really 409 and that the call went through the service, not raw `HttpClient`.
**A `/cart` redirect from anywhere but checkout** = the recovery is reachable from a non-checkout path,
which is the whole defect back again.
