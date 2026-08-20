# BUG-19 — coupon validated once, trusted forever

Setup and the Scalar tamper steps: [`README.md`](README.md).

A coupon is checked when the customer types the code, and trusted from then on. Three tests, three ways
that goes wrong:

| | Forged | Charge | Order row | Flagged? |
|---|---|---|---|---|
| **A** | `amountOff` | correct | **−$999.00** | PaymentMismatch |
| **B** | `coupon.id` | **$100 on a $185 order** | looks normal | PaymentMismatch |
| **C** | nothing | **$90 on a $180 order** | agrees with the charge | **no** |

A and B ride `POST /api/cart`, which writes the request body to Redis verbatim. **C needs nobody.**

## Coupons used

| Code | Coupon id | Effect |
|---|---|---|
| `gimme5` | `hFVUDrfn` | $5 off |
| `gimme10` | `8uwMfxnw` | 10% off |
| `BLACKFRIDAY50` | created in test B | 50% off, code **deactivated** mid-test |

---

## Test A — forged `amountOff`: the order record lies

The charge is correct; the saved `Order.Discount` is not.

**1. Build the cart** — log in at `https://localhost:4200`, add **Angular Blue Boots** ($180), nothing
else. Cart → coupon box → `gimme5` → **Apply code**.

Expect: Subtotal $180.00 · Discount $5.00 · Delivery $10.00 · Total $185.00.

**2. Reach the Payment step** — Checkout → Address → Shipping (pick a method) → **Payment**. Entering
this step fires `POST /api/payments`, so the Stripe intent now exists with the **real** $5 off.

**3. Record the honest charge:**

```bash
stripe payment_intents list --limit 1
```

Write down `"amount": 18500` — 18000 subtotal − 500 coupon + 1000 delivery. That is what Stripe will
actually take.

**4. Move to Confirmation** — the last tab. **Do not submit yet.** This step only creates the Stripe
card token; it never touches the payment intent, which is why it's the safe moment to tamper.

**5. Tamper** ([the steps](README.md#the-tamper-move)) — `GET /api/cart` returns:

```json
{
  "items": [
    {
      "price": 180,
      "productId": 18,
      "quantity": 1,
      "brand": "Angular",
      "pictureUrl": "/images/products/boot-ang1.png",
      "productName": "Angular Blue Boots",
      "type": "Boots"
    }
  ],
  "deliveryMethodId": 1,
  "clientSecret": "pi_3U6SzRGMOucV11Le1SANRSgg_secret_hipI5OJgZXNQy443CrG3AkRRh",
  "paymentIntentId": "pi_3U6SzRGMOucV11Le1SANRSgg",
  "coupon": {
    "name": "GIMME5",
    "amountOff": 5,
    "percentOff": null,
    "promotionCode": "gimme5",
    "id": "hFVUDrfn"
  }
}
```

`POST` it back with one field changed:

```
"amountOff": 5     →     "amountOff": 999
```

**6. Place the order** — checkout tab → **Submit order** → `4242 4242 4242 4242`, any future expiry,
any CVC. Don't refresh first, and don't step back to Payment.

**7. Read the damage** — open the order from `/orders`:

| Source | Today (bug present) | After the fix |
|---|---|---|
| Order page | Discount **−$999.00** · Total **−$809.00** | Discount $5.00 · Total $185.00 |
| Stripe `"amount"` | **18500** — $185.00 charged | **18500** |
| Order status | **PaymentMismatch** (`−80900` ≠ `18500`) | **PaymentReceived** |

Three numbers for one order, and the only true one is Stripe's.

Verified 2026-08-20: order page **−$999.00** / **−$809.00**, Stripe **18500**, status
**PaymentMismatch**.

---

## Test B — forged `coupon.id`: the customer pays less

`ApplyDiscountAsync` re-reads the coupon from Stripe by `cart.Coupon.Id` — a field the client controls.
Nothing checks the id belongs to the promotion code that was applied. This one moves real money.

**The scenario: an expired campaign, replayed.** No Stripe access needed — the attacker is a normal
customer replaying an id the shop handed them months ago.

> **November:** `BLACKFRIDAY50` is live, 50% off. A customer uses it and finds the Stripe **coupon id**
> sitting in their own cart JSON. They keep it.
> **March:** the campaign is over, the code returns 400. They apply today's `gimme5`, swap the old id
> back in, and get 50% off again.

Steps 1–3 set up the shop's history. **Step 4 is where the attacker starts.**

**1. Run a Black Friday campaign** — a coupon *and* a public code, like any real promotion:

```bash
stripe coupons create --percent-off 50 --duration once --name BLACKFRIDAY50

stripe promotion_codes create \
  -d "promotion[type]=coupon" \
  -d "promotion[coupon]=<coupon-id>" \
  -d code=BLACKFRIDAY50
```

⚠️ `-d "promotion[coupon]="`, not `--coupon`. The coupon moved under a `promotion` object — the same
API change that caused BUG-20, where the fix was `Expand = ["data.promotion.coupon"]`.
([create a promotion code](https://docs.stripe.com/api/promotion_codes/create))

**2. A customer uses it, and keeps the id** — cart → apply `BLACKFRIDAY50` → `GET /api/cart`:

```json
"coupon": { "name": "BLACKFRIDAY50", "percentOff": 50, "id": "<coupon-id>", ... }
```

`<coupon-id>` is what they save. **The shop put it there** (`CouponService.cs:38`).

**3. The campaign ends** — deactivate the code (Stripe can't delete promotion codes, only deactivate):

```bash
stripe promotion_codes update <promo-id> -d active=false
```

⚠️ Use `-d active=false`. The `--active` flag is listed in `--help` but doesn't take effect — `--active
false` is rejected as a stray positional argument, and `--active=false` reports success while leaving
`active: true`. Verify with `stripe promotion_codes retrieve <promo-id>`.

Confirm it's really dead: cart → apply `BLACKFRIDAY50` → **400 invalid code**. `Active = true`
(`CouponService.cs:18`) filters it out. As far as the shop is concerned, the campaign is over.

**4. Months later — a normal cart** — fresh cart, **Angular Blue Boots** ($180), apply `gimme5`.
Expect Discount $5.00.

**5. Stop on Shipping** — Checkout → Address → **Shipping**, pick a delivery method, then **stop**.

⚠️ Pick delivery **before** tampering. `updateDeliveryMethod` POSTs the whole cart from Angular's
in-memory copy (`checkout-delivery.component.ts:39`), which would overwrite your forged id.

The payment intent does not exist yet — that's the point. Test A tampered *after* it was created; this
one tampers *before*.

**6. Replay the old id** — tamper, one field:

```
"id": "hFVUDrfn"     →     "id": "<coupon-id>"
```

Leave `amountOff: 5` and `promotionCode: "gimme5"` alone — the point is that the code still says
`gimme5` while the charge won't. `paymentIntentId` and `clientSecret` are `null` here; send them as
they came.

**7. Move to Payment** — fires `POST /api/payments` → `ApplyDiscountAsync` (`PaymentService.cs:103`)
calls `GetAsync(appCoupon.Id)`. Stripe still has that coupon and returns 50% off. The UI still shows
Discount $5.00.

**8. Read the charge:**

```bash
stripe payment_intents list --limit 1
```

⚠️ Read it **after** the forge. Re-entering Payment *updates* the existing intent
(`PaymentService.cs:132`, `else` branch), so an amount read earlier is stale. An intent created before a
delivery method is picked has `shipping = 0`, which looks like a third wrong number and isn't.

**9. Place the order** — Confirmation → **Submit order** → `4242 4242 4242 4242`.

**10. Compare:**

| Source | Today (bug present) | After the fix |
|---|---|---|
| Stripe `"amount"` | **10000** — $100.00 (18000 − 50% + 1000) | **18500** — $185.00 |
| Order page | Discount $5.00 · Total **$185.00** — looks normal | same, and correct |
| Order status | **PaymentMismatch** | **PaymentReceived** |

$85 gone, on a discount that expired months ago.

Verified 2026-08-20, exactly as above: the retired `BLACKFRIDAY50` coupon id produced a **10000**
charge while the UI and the order both showed $5.00 off. An earlier run with a 90%-off coupon charged
**$28.00** against an order recorded at **$185.00**, status **PaymentMismatch** (order #38).

---

## Test C — a retired coupon left on the cart: no tamper, no trace

No attacker. No Scalar. A slow shopper is enough.

> A customer applies `BLACKFRIDAY50` and gets distracted without checking out. The campaign ends and the
> code is deactivated. Days later they come back — the cart is still in Redis, the coupon still on it —
> and check out at 50% off.

Nothing revalidates the coupon between applying it and paying. The 400 only fires when someone **types**
the code, and the cart never types it again.

**1. Run the campaign** — reuse `BLACKFRIDAY50` from test B, or reactivate it:

```bash
stripe promotion_codes update <promo-id> -d active=true
```

**2. Apply it and walk away** — cart → **Angular Blue Boots** ($180) → apply `BLACKFRIDAY50` → Discount
**$90.00**. **Do not check out.**

**3. End the campaign:**

```bash
stripe promotion_codes update <promo-id> -d active=false
```

Confirm it's dead: typing `BLACKFRIDAY50` in the cart now returns **400**.

**4. Check out anyway** — Checkout → Address → Shipping → **Payment**:

```bash
stripe payment_intents list --limit 1
```

**5. Place the order** — Confirmation → **Submit order** → `4242 4242 4242 4242`.

**6. Compare:**

| Source | Today (bug present) | After the fix |
|---|---|---|
| Stripe `"amount"` | **9000** — $90.00 (18000 − 50%, $0 shipping) | **18000** — no discount |
| Order page | Discount $90.00 · Total $90.00 | Discount $0.00 · Total $180.00 |
| Order status | **PaymentReceived** | **PaymentReceived** |

**This is the worst of the three.** A and B leave the order total and the charge disagreeing, so
`ReconcileOrderPayment` raises `PaymentMismatch`. Here they agree exactly — the order is internally
consistent and completely wrong. No flag, no log, no admin screen shows it.

**Why the cart keeps it:**

- `applyCouponCode` (`order-summary.component.ts:36`) subscribes with a `next` handler and **no `error`
  handler** — a 400 leaves the existing coupon untouched.
- The cart's Redis TTL is 30 days (`CartService.cs:25`), and **every write resets it**. A customer who
  keeps shopping holds a retired coupon indefinitely.
- Closing the browser is irrelevant — the cart is server-side.

**A coupon expiry doesn't help either.** Set `redeem_by` 10 days out and the cart still holds it on day
20 — Stripe returns `valid: false` and nothing reads it. `grep` for `Valid`, `RedeemBy`,
`MaxRedemptions`, `TimesRedeemed` across `src/` returns **nothing**.

Verified 2026-08-20: charge **9000** on a retired 50% campaign, zero tampering. The `PaymentReceived`
row above is the prediction — the order wasn't submitted, so confirm it on the next run.

---

## Why none of Stripe's guards stop any of this

The discount is never *redeemed* at Stripe. `ApplyDiscountAsync` only **reads** the coupon and does the
arithmetic in C#:

```csharp
var coupon = await couponService.GetAsync(appCoupon.Id);            // a READ, not a redemption
if (coupon.PercentOff.HasValue)
    amount -= (long)(amount * (coupon.PercentOff.Value / 100));     // math here, not at Stripe
```

The result is a plain number on a PaymentIntent. **Stripe never sees a coupon on that charge** — coupons
belong to invoices and subscriptions, not PaymentIntents. So every guard Stripe offers is decorative:

| Guard on the coupon | Stripe's view | This code |
|---|---|---|
| `redeem_by` passed | `valid: false` | never reads `valid` → still discounts |
| `max_redemptions` reached | `valid: false` | never reads `valid` → still discounts |
| Promotion code deactivated | code unusable | uses the id, not the code → still discounts |
| Coupon **deleted** | 404 on retrieve | `GetAsync` throws → **stops** |

Only deletion stops it. Confirm it yourself — the dashboard shows **Redemptions 0** on every coupon,
even ones you've checked out with. `times_redeemed` is *"number of times this coupon has been applied to
a customer"*, and nothing here ever applies one, so `max_redemptions` can never trip.

Source: [Coupon object](https://docs.stripe.com/api/coupons/object).

## Why no automated test catches any of them

`CouponService` and `PaymentService` construct Stripe SDK classes with `new` — no DI seam, both marked
`[ExcludeFromCodeCoverage]`. The order half is reachable from `API.Tests` (forge the cart over the wire,
assert the saved `Order.Discount`); the charge half is not.

## Cleanup

```bash
stripe coupons delete <coupon-id>       # removes the campaign's promotion code with it
```

⚠️ Also clear the cart, or a retired coupon stays on it for another 30 days (that's test C).

The tampered orders stay in the dev DB. Harmless — or delete `data/store.db` and restart to reseed.
