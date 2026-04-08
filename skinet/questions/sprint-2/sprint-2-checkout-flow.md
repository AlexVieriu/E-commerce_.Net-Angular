# Sprint 2 — Full Checkout Flow

Big picture reference. Shows exactly when Redis, SQLite, Stripe, and Angular interact
during a real checkout — and where Task 16 and Task 18 protect the flow.

---

```
╔══════════════════════════════════════════════════════════════╗
║              FULL CHECKOUT FLOW — YOUR EXPERIENCE            ║
╚══════════════════════════════════════════════════════════════╝

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
YOU: Add products, change quantities on Shop page
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Every add/change → PUT /api/cart
  Redis:  { cart-999: { Items[], DeliveryMethodId: null, PaymentIntentId: null } }
  SQLite: (nothing)
  Stripe: (nothing)


━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
YOU: Click "Checkout" → navigate to /checkout
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  No backend call yet.
  Angular mounts Stripe Address Element + Stripe Payment Element
  (Stripe.js loads in the background — card form is ready)

  Redis:  { cart-999: { Items[], PaymentIntentId: null } }
  SQLite: (nothing)
  Stripe: (nothing)


━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
TAB 1 — ADDRESS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  You fill in name, address fields (Stripe Address Element)
  No backend call — pure frontend form
  If "Save address" checkbox checked → PUT /api/account/address when you click Next

  Redis:  unchanged
  SQLite: unchanged
  Stripe: unchanged


━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
YOU: Click "Next" → move to TAB 2 (Shipping)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  onStepChange(selectedIndex === 1)
  → saves address to account if checkbox was ticked
  No payment call here yet.


━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
TAB 2 — SHIPPING
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Delivery methods load → GET /api/payments/delivery-methods
  You select a delivery method (e.g. UPS Standard $5.99)
  → PUT /api/cart (cart updated with deliveryMethodId in Redis)

  Redis:  { cart-999: { Items[], DeliveryMethodId: 1, PaymentIntentId: null } }
  SQLite: (nothing)
  Stripe: (nothing)


━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
YOU: Click "Next" → move to TAB 3 (Payment)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  onStepChange(selectedIndex === 2)
  → POST /api/payments/cart-999  ← PaymentIntent created HERE

  ┌──────────────────────────────────────────────────┐
  │ TASK 16 FAILURE ZONE                             │
  │                                                  │
  │ WITHOUT IdempotencyKey:                          │
  │   network blip → server retries                  │
  │   → Stripe creates 2nd PaymentIntent             │
  │   → 1st is orphaned on Stripe forever            │
  │                                                  │
  │ WITH IdempotencyKey = "cart-999":                │
  │   network blip → server retries with same key    │
  │   → Stripe returns same pi_abc123 ✓              │
  └──────────────────────────────────────────────────┘

  → Stripe creates PaymentIntent pi_abc123
  → Redis updated with PaymentIntentId + ClientSecret
  → Stripe.js payment form now active (card fields enabled)

  Redis:  { cart-999: { Items[], DeliveryMethodId: 1,
                        PaymentIntentId: "pi_abc123",
                        ClientSecret: "cs_abc" } }
  SQLite: (nothing)
  Stripe: pi_abc123 { status: "requires_payment_method" }


━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
TAB 3 — PAYMENT
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  You enter card details in the Stripe Payment Element
  Card data goes directly to Stripe — never touches your server
  Stripe validates the card format locally

  Redis:  unchanged
  SQLite: (nothing)
  Stripe: pi_abc123 { status: "requires_payment_method" }


━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
YOU: Click "Next" → move to TAB 4 (Confirmation)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  onStepChange(selectedIndex === 3)
  → stripeService.createToken()
  → Stripe.js generates a ConfirmationToken (snapshot of card + address)
    stored in Angular memory only — no backend call

  Redis:  unchanged
  SQLite: (nothing)
  Stripe: pi_abc123 { status: "requires_payment_method" }


━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
TAB 4 — CONFIRMATION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  You review: items, address, delivery, card summary, total $341
  No backend call yet.


━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
YOU: Click "Pay $341"
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  confirmPayment() runs. Two things happen in sequence:

  [1] stripeService.confirmPayment(confirmationToken)
      → Stripe.js sends card + token to Stripe directly
      → Stripe charges the card
      → returns paymentIntent.status = "succeeded"

  Stripe: pi_abc123 { status: "succeeded" }  ← money taken

  [2] POST /api/orders → orderService.createOrder()

  ┌──────────────────────────────────────────────────┐
  │ TASK 18 FAILURE ZONE                             │
  │                                                  │
  │ WITHOUT check + index:                           │
  │   double-click "Pay $341" → 2 requests           │
  │   Request A → inserts Order #1 (pi_abc123) ✓    │
  │   Request B → inserts Order #2 (pi_abc123) ✗    │
  │   Webhook updates only one → other stays Pending │
  │                                                  │
  │ WITH app-level check:                            │
  │   Request A → inserts Order #1 ✓                │
  │   Request B → finds Order #1 → returns it ✓     │
  │                                                  │
  │ WITH unique DB index (last resort):              │
  │   true race → 2nd insert rejected by SQLite ✓   │
  └──────────────────────────────────────────────────┘

  → OrderService reads cart from Redis → gets pi_abc123
  → Checks SQLite: order with pi_abc123 exists? No → insert
  → Deducts stock in SQLite
  → Inserts Order #1 in SQLite
  → Deletes cart from Redis

  Redis:  (cart-999 gone)
  SQLite: Order #1 { PaymentIntentId: "pi_abc123", Status: Pending }
  Stripe: pi_abc123 { status: "succeeded" }

  → Angular navigates to /checkout/success  ← user sees success page HERE


━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
STRIPE WEBHOOK (happens seconds later, async)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Stripe → POST /api/payments/webhook
  → Verifies signature
  → Finds Order WHERE PaymentIntentId = "pi_abc123"
    (unique index → exactly 1 row guaranteed)
  → Updates Order #1 Status = PaymentReceived
  → SignalR pushes notification to browser

  Redis:  (nothing)
  SQLite: Order #1 { Status: PaymentReceived }
  Stripe: pi_abc123 { status: "succeeded" }

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
DONE ✓
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## Key Moments Summary

| When | What happens | Task |
|---|---|---|
| Tab 2 → Tab 3 click | PaymentIntent created on Stripe | Task 16 guards here |
| "Pay $341" click | Stripe charged first, then order saved to SQLite | Task 18 guards here |
| Seconds later | Webhook updates order status, SignalR notifies browser | — |

---

## Known Limitation — Order Created After Payment

The current flow is:

```
1. Stripe.js confirms payment  ← money taken
2. POST /api/orders            ← order created in SQLite
3. navigate to /checkout/success
```

**The race:** The webhook can fire between step 1 and step 2 — before the order exists
in SQLite. Webhook finds nothing, does nothing. Order stays `Pending` forever.

Also: the user sees the success page before the webhook fires. If they immediately
click "View My Order", the status shows `Pending` — not `PaymentReceived`. This causes
user panic ("I paid $341, why does it say unpaid?").

**The correct senior fix:**

```
1. POST /api/orders first      ← Status: Pending
2. Stripe.js confirms payment  ← money taken
3. Webhook fires → order exists → Status: PaymentReceived
4. SignalR → navigate to /checkout/success  ← now safe to show
```

Order always exists before payment is confirmed. SignalR becomes the real "all clear"
signal. User never sees a `Pending` status for a paid order.

**Why not fixed:** Requires restructuring Angular checkout + backend flow.
CV project + deadline — documented as a known limitation.
Strong interview talking point: shows you understand the race and know the correct fix.

---

claude --resume 855033c5-43ce-41cf-afc0-7ed0bd5ba0b5
