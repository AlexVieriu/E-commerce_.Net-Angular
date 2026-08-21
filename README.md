# Skinet

A production-shaped e-commerce store — catalogue, cart, Stripe checkout, admin panel — built to find out
what breaks when money is involved.

**▶ [Live demo](https://skinet-2025-test-hra4dvcpa3hvfyeq.polandcentral-01.azurewebsites.net)** ·
.NET 10 · Angular 22 · Stripe · Azure

> Serverless Azure SQL auto-pauses when idle — the first request takes 20–30s to wake it, everything
> after that is fast.

<!-- SCREENSHOT: catalogue page -->

---

## What it does

Catalogue with search, brand/type filters, sorting and paging · Redis cart keyed to the signed-in user ·
four-step Stripe checkout (address → shipping → payment → confirmation) · orders reconciled from the
Stripe webhook with a live SignalR push to the buyer · admin dashboard, refunds and payment-status sync ·
cookie Identity with Admin/Customer roles and route guards on both sides.

## Built with

| | |
|---|---|
| **Backend** | .NET 10 · ASP.NET Core · EF Core · clean architecture, 4 layers |
| **Frontend** | Angular 22 (zoneless, signals) · Tailwind 4 · Angular Material |
| **Data** | SQLite locally · Azure SQL in production · Redis for cart + response cache |
| **Payments** | Stripe — PaymentIntents, webhooks, refunds, promotion codes |
| **Realtime** | SignalR |
| **Tests** | 159 unit + integration (xUnit v3, Moq, FluentAssertions) · 4 Playwright E2E specs |
| **CI/CD** | GitHub Actions → Azure App Service |

## Architecture

```
Core  ←  Application  ←  Infrastructure  ←  API
```

- **Core** — entities, domain exceptions, repository interfaces. No project references at all.
- **Application** — DTOs, validators, specifications, and the interfaces Infrastructure implements.
- **Infrastructure** — EF Core, Redis, Stripe.
- **API** — controllers, middleware, DI wiring.

Infrastructure never learns that HTTP exists: the signed-in user arrives through an `ICurrentUser` seam
declared in `Application` and implemented in `API`.

→ [backend](skinet/docs/backend/architecture.md) · [frontend](skinet/docs/frontend/architecture.md) ·
[all 27 docs](skinet/docs/INDEX.md)

## The interesting bits

**The cart leaked between users.** Every cart endpoint took the cart id *from the client*, so any
signed-in user could read or overwrite anyone's cart — and the same id reached payments and orders, so
they could pay from it too. Fixed by **deleting** the parameter rather than validating it; the key is now
derived from `ICurrentUser`. Eight call sites stopped compiling, and that failing build *was* the audit.
→ [auth.md](skinet/docs/backend/auth.md)

**A coupon was validated once, then trusted forever.** Forge `amountOff` → −$999.00 on the order. Forge
`coupon.id` → **$100 charged on a $185 order**. Or forge nothing and let the campaign retire → **$90 on a
$180 order with nothing flagged**, because the order total and the charge agreed. Both money paths now
re-derive the coupon from the promotion code on every call — **by code, not by id, because a code fails
closed**: listing active promotion codes returns nothing for a retired campaign, while retrieving a
coupon by id ignores validity entirely. → [payment.md](skinet/docs/backend/payment.md)

**Two people buying the last item.** Stock is decremented inside the order transaction and
`Product.QuantityInStock` carries `[ConcurrencyCheck]`, so the loser of the race is told to retry rather
than both orders succeeding against stock that isn't there.

**The order exists before the money moves.** `POST /api/orders` runs *before* `stripe.confirmPayment()`,
so `payment_intent.succeeded` always finds an order, and price/stock validation lands ahead of the
charge. Payment status is Stripe-sourced only, never typed in by a human: one rule, two triggers — the
webhook pushes, an admin action pulls when a webhook never arrived.
→ [payment.md](skinet/docs/backend/payment.md)

## Demo

<!-- GIF: checkout through Stripe -->
<!-- GIF: admin refund -->

<details>
<summary><b>Run it locally</b></summary>

**Prerequisites** — .NET 10 SDK · Node 24+ · Docker · a Stripe test account · the
[Stripe CLI](https://docs.stripe.com/cli)

```bash
cd skinet

# 1. Redis
docker compose up -d redis
docker exec redis redis-cli PING                        # → PONG

# 2. Secrets
dotnet user-secrets set "StripeSettings:SecretKey" "sk_test_..."      --project src/API
dotnet user-secrets set "StripeSettings:WhSecret"  "whsec_..."        --project src/API
dotnet user-secrets set "AdminSettings:Password"   "<admin password>" --project src/API

# 3. API — migrations and seeding run at startup (--seed adds a bulk Bogus dataset)
dotnet run --project src/API/API.csproj                  # https://localhost:7096

# 4. Client (separate terminal)
cd client && npm ci && npx ng serve                     # https://localhost:4200

# 5. Stripe webhooks (separate terminal)
stripe listen --forward-to https://localhost:7096/api/payments/webhook
```

The port isn't free to change — the client hardcodes `https://localhost:7096` for both the API and the
SignalR hub (`client/src/environments/environment.development.ts`), so `launchSettings.json` pins it.
`WhSecret` must match what `stripe listen` prints, or every webhook fails its signature check and orders
sit at `Pending`.

Test card `4242 4242 4242 4242`, any future expiry, any CVC.

```bash
dotnet test                                             # from skinet/, needs Redis
cd e2e && npm ci && npx playwright test                 # needs API + client running
```

</details>

## What I'd do next

- **`PaymentService` has no DI seam** — it builds Stripe SDK classes with `new`, so the charge half of
  checkout is only verifiable by hand. Injecting `IStripeClient` fixes that and kills the global static
  API key.
- **The cart still accepts a coupon object the server ignores** — a server-owned cart, where the client
  sends a code and never a discount, would close that at the source.
- **Logging out on `/checkout` doesn't redirect** (BUG-17) — the guard navigates instead of blocking.
- **No soft delete** — deleting a product that has appeared on an order is permanent.
- **Coupon restrictions are ignored** — `minimum_amount`, `first_time_transaction` and per-customer limits
  are never read.
