# Payment

Stripe integration: payment intents, webhooks, refunds.

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
              ├── Match amount    → OrderStatus.PaymentReceived
              └── Mismatch amount → OrderStatus.PaymentMismatch
              └── SignalR push to buyer
```

Only `PaymentIntent` events with `status == "succeeded"` are processed — all others are silently ignored.

## Rules / Constraints

- Never trust client-side prices — always re-fetch from DB before creating the intent.
- Webhook endpoint must not have `[Authorize]` — verified by Stripe signature only.
- Stripe amounts always in cents (multiply by 100).

