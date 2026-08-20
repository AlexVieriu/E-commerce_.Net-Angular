# Payment

Stripe integration: payment intents, webhooks, refunds.

## Checkout Flow (UI → Order → Stripe → Webhook)

```
Delivery step → StripeService.createOrUpdatePaymentIntent() → POST /api/payments
                  → PaymentsController.CreateOrUpdatePaymentIntent → PaymentService.CreateOrUpdatePaymentIntent
                  → Stripe PaymentIntentService.CreateAsync → cart.PaymentIntentId + ClientSecret (Redis)

Card step     → Stripe Elements collect card details client-side — never touches our backend

Click "Pay"   → CheckoutComponent.confirmPayment()
                  1. createOrderModel() → OrderToCreate DTO
                  2. OrderService(Angular).createOrder() → POST /api/orders
                       → OrdersController.CreateOrder → OrderService(Infrastructure).CreateOrderAsync
                       → new Order { Status = Pending, PaymentIntentId = cart.PaymentIntentId } → saved
                  3. StripeService.confirmPayment() → stripe.confirmPayment() (browser → Stripe directly)
                       → UI sees 'succeeded' → navigates to /checkout/success (does NOT touch order.Status)
                       → Stripe also fires the webhook, server-to-server, separately (see Webhook Flow below)
```

**Order is created before Stripe confirms the charge, deliberately** (`checkout.component.ts:154-159`) —
so the webhook always has an order to find. A slow client could otherwise let
`payment_intent.succeeded` arrive before the order exists.

## PaymentService Flow

`CreateOrUpdatePaymentIntent()` — no cart id parameter; the cart is the caller's (see `auth.md`):
1. Load cart from Redis.
2. Get shipping price from `DeliveryMethod`.
3. Re-fetch item prices from DB — corrects stale Stripe values.
4. Apply the coupon discount if present — re-read from Stripe by promotion code (see below).
5. Create/update Stripe `PaymentIntent` (amounts in cents).
6. Save updated cart to Redis (`PaymentIntentId` + `ClientSecret`).

## Coupons: validated once is not enough (BUG-19)

A coupon is applied by typing a code, but the cart it lands on is client-writable — `POST /api/cart`
stores the request body in Redis verbatim. So **no coupon field on the cart may be trusted**, and a
coupon that was live when it was applied may not be live at checkout.

Both money paths re-read the coupon on every call, keyed on the one field the customer typed:

```
OrderService.ResolveDiscountAsync   ─┐
PaymentService.ApplyDiscountAsync   ─┴─→ ICouponService.GetCouponFromPromoCode(cart.Coupon.PromotionCode)
                                             └─→ Stripe promotion_codes list, Active = true
```

- **By code, never by id.** `promotion_codes list` with `Active = true` returns nothing for a retired
  campaign, so the lookup fails closed. `coupons retrieve` by id ignores `valid` entirely and would
  hand back a dead coupon — and the id sits on the cart, where the client can change it.
- **`Active` covers coupon validity for free.** Stripe: *"a promotion code is only active if the coupon
  is also valid"*, so an expired or fully-redeemed coupon is already filtered out.
  ([promotion code object](https://docs.stripe.com/api/promotion_codes/object))
- **A blank code is a failed lookup.** An unset `Code` filter is dropped, not matched — Stripe would
  return every live code for `FirstOrDefault` to pick one.
- **Units:** `GetCouponFromPromoCode` returns **dollars** (`AmountOff / 100m` — decimal division, or the
  cents truncate). `OrderService` works in dollars; `PaymentService` multiplies back to cents.

**When the lookup resolves to nothing** — both paths do the same thing: drop the coupon, null the
intent, and throw `PriceChangedException` → **409**. `error.interceptor` handles that status for
`POST /orders` and `POST /payments` by refreshing the cart and returning the customer to it, re-priced.
Dropping the coupon matters: left on the cart, the retry would hit the same 409 forever.

`AppCoupon.Id` is still returned to the client and stored on the cart, but **no server path reads it
any more.**

## Endpoints

| Method | Route | Auth | Notes |
|---|---|---|---|
| POST | `/api/payments` | `[Authorize]` | Create/update PaymentIntent for the caller's cart, return it |
| GET | `/api/payments/delivery-methods` | No | All delivery methods |
| POST | `/api/payments/webhook` | No | Stripe webhook — verified by signature |

## Webhook Flow

```
POST /api/payments/webhook
  └── ConstructStripeEvent() — verifies Stripe-Signature header
        └── HandlePaymentIntentSucceeded(intent)
              ├── Find order by PaymentIntentId
              ├── PaymentService.ReconcileOrderPayment(order, intent.Amount)
              │     ├── Match amount    → OrderStatus.PaymentReceived
              │     └── Mismatch amount → OrderStatus.PaymentMismatch
              └── SignalR push to buyer
```

Only `PaymentIntent` events with `status == "succeeded"` are processed — all others are silently ignored.

## Reconciliation (recovery path for a missed webhook)

If the webhook never reaches the server (network blip, endpoint down during a redeploy, retries
exhausted), an order can sit at `Pending` with no automatic recovery. `AdminController`'s
`POST /api/admin/orders/{id}/sync-payment-status` covers that gap:

```
Admin orders table → "sync" icon → AdminOrdersComponent.syncPaymentStatus(id)
  → AdminService.syncPaymentStatus(id) → POST /api/admin/orders/{id}/sync-payment-status
  └── PaymentService.SyncOrderPaymentStatus(order)
        ├── PaymentIntentService.GetAsync(order.PaymentIntentId) — pull from Stripe
        └── ReconcileOrderPayment(order, intent.Amount) — same method the webhook calls
  └── SignalR push to buyer (same event as the webhook)
```

One rule (`ReconcileOrderPayment`), two triggers — push (webhook) or pull (admin sync). `Refunded`
orders are excluded: a stale Stripe read must never overwrite money that already moved via the refund
endpoint.

## Rules / Constraints

- Never trust client-side prices — always re-fetch from DB before creating the intent.
- Same for coupons: re-read from Stripe by promotion code. Never read an amount or an id off the cart.
- Webhook endpoint must not have `[Authorize]` — verified by Stripe signature only.
- Stripe amounts always in cents (multiply by 100).
- `PaymentIntent` creation uses `IdempotencyKey = Guid.NewGuid()`. The cart-id prefix went with `ShoppingCart.Id` (BUG-18); the Guid was always what made the key unique, so a new intent can still be created for the same cart after a price-change rejection.
- Payment status is Stripe-sourced only — never admin-entered. A "fix a stuck order" action must reconcile from Stripe's API (re-fetch the `PaymentIntent`, reuse the webhook's match/mismatch logic), not accept a status value typed in by a human.

