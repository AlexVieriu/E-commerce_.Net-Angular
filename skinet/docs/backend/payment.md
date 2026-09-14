# Payment

Stripe integration: payment intents, webhooks, refunds.

## Checkout Flow — what fires when

The stepper reads Address → Shipping → Payment → Confirmation, but **the first backend call happens
before any of them.**

```
/checkout renders    afterNextRender (checkout.component.ts, constructor)
                     └─ createPaymentElement → initializeElements
                        └─ POST /api/payments      ◀ the intent is created HERE,
                                                      while deliveryMethodId is still null,
                                                      so this first one carries $0 shipping

Address  step 1      onStepChange → saves the address to the account, only if the box is ticked.
                     Does not touch the cart.

Shipping step 2      clicking a method → updateDeliveryMethod (checkout-delivery.component.ts:39)
                     └─ POST /api/cart          ◀ the WHOLE cart, from Angular's in-memory copy

Payment  step 3      onStepChange (checkout.component.ts)
                     └─ POST /api/payments      ◀ UPDATES the same intent, now with shipping
                     card is typed into Stripe Elements — never reaches our backend

Confirm  step 4      getConfirmationToken → stripe.createToken (browser → Stripe direct)

Submit order         confirmPayment (checkout.component.ts)
                     1. POST /api/orders          ◀ order saved Pending, BEFORE any money moves
                                                    the cart is NOT deleted here (BUG-2)
                     2. stripe.confirmPayment()     browser → Stripe direct
                     3. Stripe → POST /api/payments/webhook
                        └─ ReconcileOrderPayment → PaymentReceived | PaymentMismatch
                     4. DELETE /api/cart          ◀ on success only
```

The order is created before Stripe confirms the charge **deliberately**, so the webhook always has an
order to find — a slow client could otherwise let `payment_intent.succeeded` arrive first. It also puts
price and stock validation ahead of the charge.

### Who writes what

| Write | Fired by |
|---|---|
| `POST /api/cart` | add/remove item · **clicking a delivery method** · apply/remove coupon |
| `POST /api/payments` | **`/checkout` page load** · entering the Payment step · apply/remove coupon while on `/checkout` |
| `POST /api/orders` | Submit order, before `stripe.confirmPayment` |
| `DELETE /api/cart` | a **successful** payment only (`checkout.component.ts`, `confirmPayment`) |

⚠️ `POST /api/cart` always sends Angular's **in-memory** cart. Change the cart server-side — Scalar,
another tab — and any trigger in that first row silently overwrites you. `POST /api/payments` is not
one of them: its response is stored in the cart signal locally, never posted back (BUG-2).

## PaymentService Flow

`CreateOrUpdatePaymentIntent()` — no cart id parameter; the cart is the caller's (see `auth.md`):
1. Load cart from Redis.
2. Get shipping price from `DeliveryMethod`.
3. Re-fetch item prices from DB — corrects stale Stripe values.
4. Apply the coupon discount if present — re-read from Stripe by promotion code (see below).
5. Create/update Stripe `PaymentIntent` (amounts in cents) — reusing the cart's, unless it is spent (below).
6. Save updated cart to Redis (`PaymentIntentId` + `ClientSecret`).

## Cart lifetime, and reusing the intent (BUG-2)

**The cart dies with the payment, not with the order.** `CreateOrderAsync` used to delete it the moment
the order committed — one step before the charge — so a declined card had no cart to retry through and
`POST /api/payments` answered **400 "Cart unavailable"**, burying Stripe's own message.

```
card declined -> intent returns to requires_payment_method, still payable
              -> customer stays on the Payment step, cart intact
              -> another card -> paid, same order, stock taken once
```

A tab closed before `DELETE /api/cart` now leaves the cart until its 30-day TTL. The webhook cannot
delete it instead: `CartService` builds `cart:{ICurrentUser.Id}` and the webhook is `[AllowAnonymous]`.

**So a cart can carry an intent Stripe has finished with.** `DropIntentIfSpentAsync` checks before reuse:

| Intent status | What happens |
|---|---|
| `succeeded` · `canceled` | drop the id, create a fresh intent |
| anything else, incl. `processing` | reuse it — a second intent for a payment in flight is a second charge |

Deny-list, never an allow-list. Three routes reach a spent intent: a closed tab (`succeeded`); Stripe
cancelling an intent confirmed past its limit — *"a variable upper limit … any further calls to this
endpoint will transition the PaymentIntent to the `canceled` state"*
([confirm](https://docs.stripe.com/api/payment_intents/confirm)); and a cancel from the Dashboard or
CLI. Nothing in `src/` calls `CancelAsync`.

⚠️ **A replaced intent orphans any order already created on it.** `CreateOrderAsync` recognises an
existing order by `PaymentIntentId`, so if Pay was clicked on the old intent, the retry misses that
`Pending` order — second order, stock taken twice. The first can never be paid and holds stock until
**T41** cancels it. On the closed-tab route there is no earlier order, so the second one is simply the
customer buying again.

**The client follows the swap.** `stripe.service.ts` stores every `POST /api/payments` response in the
cart signal, because `confirmPayment` reads the `clientSecret` from there; `checkout.component.ts`
watches that secret and rebuilds the Stripe elements when it changes — an `Elements` instance is bound
to the secret it was built with. The typed address is carried over; the card is typed again.

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
  return every live code for `FirstOrDefault` to pick one. Model validation rejects a blank `PromotionCode` at the
  boundary, so this guard is the second line — it covers any caller that skips model binding.
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
- Never delete the cart before the charge succeeds — the retry Stripe allows walks back through it (BUG-2).
- Never create a second intent for one Stripe has not finished with; `processing` may be mid-3DS (BUG-2).
- `PaymentIntent` creation uses `IdempotencyKey = Guid.NewGuid()`. The cart-id prefix went with `ShoppingCart.Id` (BUG-18); the Guid was always what made the key unique, so a new intent can still be created for the same cart after a price-change rejection.
- Payment status is Stripe-sourced only — never admin-entered. A "fix a stuck order" action must reconcile from Stripe's API (re-fetch the `PaymentIntent`, reuse the webhook's match/mismatch logic), not accept a status value typed in by a human.

