# Sprint C — smoke tests

One file per task, bug, or fix. Run a file when you finish that item; run them all before the sprint's
deploy gate. Automated coverage and the gate checklist live in [`../../testing.md`](../../testing.md).

| File | Covers |
|---|---|
| [`bug-19-coupon-trust.md`](bug-19-coupon-trust.md) | Coupon validated once, trusted forever — 4 tests |
| [`task-52-open-redirect.md`](task-52-open-redirect.md) | `?returnUrl=` cannot leave the origin — 9 URL runs + 2 logins |

## Read first

Skim the checkout timeline in [`../../backend/payment.md`](../../backend/payment.md) — **what fires
when, and who writes the cart.** Nearly every confusing reading traces to one of these three:

- The payment intent is created on **`/checkout` page load**, not at the Payment step. That first one
  has `deliveryMethodId` still null, so it carries **$0 shipping**.
- `POST /api/cart` always sends Angular's **in-memory** cart, and four different actions trigger it —
  so a cart you changed server-side can be silently overwritten.
- A rejected coupon leaves the cart **visually unchanged** (`applyCouponCode` has no error handler),
  so a code that 400'd looks like it applied.

## Before any file

| Service | Start it | Verify |
|---|---|---|
| Redis | `docker compose up -d redis` (from `skinet/`) | `docker exec redis redis-cli PING` → `PONG` |
| API | `dotnet run --project src/API/API.csproj` | `https://localhost:7096/api/products` → 200 |
| Client | `npx ng serve` (from `skinet/client/`) | `https://localhost:4200` → 200 |
| Stripe | `stripe listen --forward-to https://localhost:7096/api/payments/webhook` | prints a `whsec_…` |

⚠️ The `whsec_…` must match `StripeSettings:WhSecret` (`dotnet user-secrets list --project src/API`).
If it doesn't, every webhook fails its signature check and orders stay **Pending** — which looks
exactly like a broken payment flow.

⚠️ `stripe listen` prints event *types* only, never amounts. To read a charge:
`stripe payment_intents list --limit 1` (or add `--print-json` to `listen`).

⚠️ Every command here is on **one line** on purpose. PowerShell has no backslash line
continuation — pasting a bash-style multi-line command runs the first line, then reports
`The term '-d' is not recognized`. Also replace `<coupon-id>` / `<promo-id>` with real ids;
the CLI does not expand placeholders.

## The tamper move

The Angular page is not the client — HTTP is. Any logged-in user can send `POST /api/cart` themselves
with their **own** cookie, no stolen credentials involved. Scalar is just the handiest tool on a dev
box; in production it's curl or Postman and nothing changes.

1. New browser tab → [Scalar](https://localhost:7096/scalar/v1#tag/api). The auth cookie is set for host
   `localhost` and cookies ignore the port, so you're already logged in. (401? run `POST /login` with
   `useCookies` = `true` first.)
2. `GET /api/cart` → **Send** → copy the whole response JSON.
3. `POST /api/cart` → paste it as the body → change one field → **Send**.
4. Expect **200** with your changed value echoed back.

⚠️ **Leave `/checkout` before you tamper.** While that page is open, Angular holds its own copy of
the cart and writes it back on several actions — moving between steps, or leaving the Stripe payment
element and coming back. That copy predates your forge, so it silently restores the old value or nulls
`deliveryMethodId` / `paymentIntentId`, and your next reading looks like the fix failed. Navigate to
`/cart` first, tamper in Scalar, and confirm the `POST` response echoes every field you care about.

⚠️ Always send the **whole** cart body — `items`, `deliveryMethodId`, `paymentIntentId`,
`clientSecret`, `coupon`. `SetCartAsync` overwrites the Redis key, so anything you drop is gone and the
order won't create.
