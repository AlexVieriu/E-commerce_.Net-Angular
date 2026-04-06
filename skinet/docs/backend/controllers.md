# Controllers

All endpoints, auth requirements, and return types.

## AdminController

`[Authorize(Roles = "Admin")]` on the class — all endpoints require Admin role.

| Method | Route | Returns | Notes |
|---|---|---|---|
| GET | `/api/admin/orders` | `Pagination<OrderDto>` | `[FromQuery] OrderSpecParams` — filter/sort/page |
| GET | `/api/admin/orders/{id}` | `OrderDto` | 400 if not found |
| POST | `/api/admin/orders/refund/{id}` | `OrderDto` | 400 if Pending or refund fails |

**Refund rules:**
- `OrderStatus.Pending` cannot be refunded (payment not captured yet)
- Calls `PaymentService.RefundPayment(paymentIntentId)` — must return `"succeeded"`
- Sets `order.Status = OrderStatus.Refunded` and saves

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
| GET | `/api/cart?id={cartId}` | `ShoppingCart` | Returns empty cart with same `id` if not found |
| POST | `/api/cart` | `ShoppingCart` | Full cart in body; 400 if Redis write fails |
| DELETE | `/api/cart?id={cartId}` | `204 NoContent` | 400 if Redis delete fails |

**Cart id:** generated client-side (nanoid), stored in `localStorage`. Sent as query param on GET/DELETE.

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
