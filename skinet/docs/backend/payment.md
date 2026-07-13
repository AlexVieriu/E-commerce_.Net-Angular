# Payment

Stripe integration: payment intents, webhooks, refunds.

## Checkout Flow (UI → Order → Stripe → Webhook)

```
Delivery step → StripeService.createOrUpdatePaymentIntent() → POST /api/payments/{cartId}
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

**Order is created before Stripe confirms the charge, deliberately** (`checkout.component.ts:134-137`) —
so the webhook always has an order to find. A slow client could otherwise let
`payment_intent.succeeded` arrive before the order exists.

## PaymentService Flow

`CreateOrUpdatePaymentIntent(cartId)`:
1. Load cart from Redis.
2. Get shipping price from `DeliveryMethod`.
3. Re-fetch item prices from DB — corrects stale Stripe values.
4. Apply coupon discount if present.
5. Create/update Stripe `PaymentIntent` (amounts in cents).
6. Save updated cart to Redis (`PaymentIntentId` + `ClientSecret`).

## Endpoints

| Method | Route | Auth | Notes |
|---|---|---|---|
| POST | `/api/payments/{cartId}` | `[Authorize]` | Create/update PaymentIntent, return cart |
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
- Webhook endpoint must not have `[Authorize]` — verified by Stripe signature only.
- Stripe amounts always in cents (multiply by 100).
- `PaymentIntent` creation uses `IdempotencyKey = $"{cart.Id}-{Guid.NewGuid()}"` — cart prefix for Stripe log traceability, Guid suffix ensures a new intent can be created for the same cart after a price change rejection.
- Payment status is Stripe-sourced only — never admin-entered. A "fix a stuck order" action must reconcile from Stripe's API (re-fetch the `PaymentIntent`, reuse the webhook's match/mismatch logic), not accept a status value typed in by a human.

