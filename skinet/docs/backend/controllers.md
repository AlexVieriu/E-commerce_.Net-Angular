# Controllers

Endpoints, auth requirements, and return types for the four controllers with non-obvious rules.
`Account`, `Products`, `Payments`, `Buggy` and `Fallback` are not covered here.

## AdminController

`[Authorize(Roles = "Admin")]` on the class — all endpoints require Admin role.

| Method | Route | Returns | Notes |
|---|---|---|---|
| GET | `/api/admin/dashboard` | `DashboardDto` | Counts, revenue (`PaymentReceived` only), low-stock + recent-orders previews. Via `IDashboardService` (in-memory aggregation) |
| GET | `/api/admin/orders` | `Pagination<OrderDto>` | `[FromQuery] OrderSpecParams` — filter/sort/page |
| GET | `/api/admin/orders/{id}` | `OrderDto` | 400 if not found |
| POST | `/api/admin/orders/refund/{id}` | `OrderDto` | 400 if Pending or refund fails |
| POST | `/api/admin/orders/{id}/sync-payment-status` | `OrderDto` | Re-fetches the PaymentIntent from Stripe and reconciles — recovery path for a missed webhook. 400 if not found or already Refunded |

**Dashboard payload (`DashboardDto`)** — the admin's "how's the shop doing?" screen in one call:

| Field | What the admin learns | Why it matters |
|---|---|---|
| `TotalProducts` | catalog size | sanity check |
| `TotalOrders` / `PendingOrders` | order volume + backlog | pending = work to fulfill today |
| `TotalRevenue` | revenue (`PaymentReceived` only) | the headline number |
| `LowStockProducts` | items running out (≤ threshold) | restock before they sell out — 0 stock = lost sales |
| `RecentOrders` | latest orders | quick "what just happened" |

**Refund rules:**
- `OrderStatus.Pending` cannot be refunded (payment not captured yet)
- Calls `PaymentService.RefundPayment(paymentIntentId)` — must return `"succeeded"`
- Sets `order.Status = OrderStatus.Refunded` and saves

**Sync-payment-status rules:**
- `OrderStatus.Refunded` is excluded — a stale Stripe read must never overwrite money that already moved
- Calls `PaymentService.SyncOrderPaymentStatus(order)`, which re-fetches the `PaymentIntent` from Stripe and
  runs it through the same `ReconcileOrderPayment` the webhook uses — one rule, two triggers (push vs pull)
- Pushes the same `OrderCompleteNotification` SignalR event the webhook pushes, if the buyer is connected

## OrdersController

`[Authorize]` — requires any authenticated user. Orders are scoped to the caller's email.

| Method | Route | Returns | Notes |
|---|---|---|---|
| GET | `/api/orders` | `IReadOnlyList<OrderDto>` | Filtered by `User.GetEmail()` |
| GET | `/api/orders/{id}` | `OrderDto` | 404 if not found or belongs to another user |
| POST | `/api/orders` | `OrderDto` | `CreateOrderDto` body; 400 if creation fails |

**Create order:** delegates to `IOrderService.CreateOrderAsync(email, dto)`. Returns `201 Created` with location `/api/orders/{id}`.

## CartController

`[Authorize]` — requires authentication.

| Method | Route | Returns | Notes |
|---|---|---|---|
| GET | `/api/cart` | `ShoppingCart` | Empty cart if none stored |
| POST | `/api/cart` | `ShoppingCart` | Full cart in body; 400 if Redis write fails |
| DELETE | `/api/cart` | `204 NoContent` | |

**No endpoint takes a cart id.** `CartService` derives the Redis key from the authenticated caller, so
there is no parameter a client could point at someone else's cart (BUG-18 — see `auth.md`).

```
client                  CartController          CartService            Redis
  |  GET /api/cart  -->  (no parameter)  -->  cart:{userId}  -->  [ the caller's cart ]
                                                    ^
                                          ICurrentUser, from the auth cookie
```

## CouponsController

`[Authorize]` + `[EnableRateLimiting("strict")]` — 30 requests/min per IP.

| Method | Route | Returns | Notes |
|---|---|---|---|
| GET | `/api/coupons/{code}` | `AppCoupon` | 400 if code not found in Stripe |

Validates promo code against Stripe. Returns coupon with `AmountOff`/`PercentOff`.

## Rate Limiting

Configured globally + one named policy.

| Policy | Limit | Window | Applied to |
|---|---|---|---|
| Global | 200 req/min | 1 min | All endpoints (per IP) |
| `strict` | 30 req/min | 1 min | Identity endpoints (prod only) + CouponsController |

Identity endpoints (`/api/login`, `/api/register`) only get `strict` in non-Development — avoids throttling yourself locally.
