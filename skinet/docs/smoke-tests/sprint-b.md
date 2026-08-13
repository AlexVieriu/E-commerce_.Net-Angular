# Sprint B — smoke tests

Manual checks for Sprint B, one section per task or bug that changed something you can see in a
browser. Run a section when you finish that task; run the whole file before the sprint's deploy gate.

These are the checks no automated test makes: real Stripe, real cookies, real browser state across
two people. Automated coverage and the deploy-gate checklist live in [`../testing.md`](../testing.md).

## Before any section

| Service | Start it | Verify |
|---|---|---|
| Redis | `docker compose up -d redis` (from `skinet/`) | `docker exec redis redis-cli PING` → `PONG` |
| API | `dotnet run --project src/API/API.csproj` | `https://localhost:7096/api/products` → 200 |
| Client | `npx ng serve` (from `skinet/client/`) | `https://localhost:4200` → 200 |
| Stripe | `stripe listen --forward-to https://localhost:7096/api/payments/webhook` | see the warning below |

⚠️ `stripe listen` prints a `whsec_…` secret. It **must match** `StripeSettings:WhSecret`
(`dotnet user-secrets list --project src/API`). If it doesn't, every webhook fails its signature
check and orders stay **Pending** — which looks exactly like a broken payment flow.

⚠️ Docker Desktop must be running before `docker compose`. Without it the API boots fine and then
500s on the first cart or cache call, because Redis is resolved lazily.

---

## BUG-18 — cart ownership (IDOR)

The cart key moved from a client-supplied id to the authenticated caller. That touches the cart,
the payment intent, order creation, login, logout, and the 401 path — so the smoke run walks all six.

### Accounts

| Who | Email | Password |
|---|---|---|
| A | `e2e.cart.a@skinet-test.local` | `Pa$$w0rd12!@` |
| B | `e2e.cart.b@skinet-test.local` | `Pa$$w0rd12!@` |
| admin | `admin@test.com` | `Admin@12345!` |

Both cart accounts are created by the Playwright spec on its first run. Their carts live in Redis
for 30 days, so **check the starting state before you begin** — a previous run's leftovers change
the counts you should expect:

```bash
docker exec redis redis-cli --scan --pattern 'cart:*'
```

### The run — in order

It is one continuous story. Steps 2–8 depend on A's cart being left alone, so don't shuffle them.

| # | Area | Do | Expect |
|---|---|---|---|
| 1 | app loads | open `https://localhost:4200`, F12 open | products render, no red console errors |
| 2 | login loads cart | log in as A — **do not touch the cart** | badge shows A's count at once, no refresh needed |
| 3 | isolation | log out, log in as B | B's cart is empty — A's item is not there |
| 4 | cart writes | as B: add 2 products, change a qty, remove one | badge tracks every change |
| 5 | payment intent | as B: Checkout → reach the Payment step | no 400; the Stripe card form renders |
| 6 | order creation | pay `4242 4242 4242 4242`, future expiry, any CVC | success page, order listed in `/orders` |
| 7 | webhook | watch the `stripe listen` window | order flips Pending → PaymentReceived |
| 8 | cart cleared | as B: open the cart | empty — the order consumed it |
| 9 | A untouched | log out, log in as A | A's cart is unchanged — B's checkout never touched it |
| 10 | admin refund | admin → `/admin/orders` → refund B's order | status becomes Refunded |
| 11 | logout | user menu → Logout | badge clears; auth cookie gone (DevTools → Application → Cookies) |
| 12 | 401 path | delete the auth cookie, then **change an item's quantity** in the cart | bounced to login, navbar back to Login/Register, cart cleared |

### Why the order matters

- **Step 2 comes before you touch anything.** Logout clears the cart from memory, and only login
  reloads it — `InitService` runs at app startup and nowhere else. If you have to press F5 to see
  the badge, that pairing is broken.
- **Leave A's cart alone through steps 2–8.** It is the control sample for step 9.
- **Steps 8 and 9 together are the money-path proof.** B's checkout must consume B's cart and leave
  A's alone. The original bug got this exactly backwards: it placed the attacker's order *and*
  deleted the victim's cart.
- **Step 12 covers the 401 path, not the Logout button.** The cart is cleared in
  `AccountService.clearCurrentUser()`, which both paths call — clicking Logout alone doesn't prove it.
- **Step 12 needs a write, not a navigation.** Deleting the cookie leaves `_currentUser` and `_cart`
  untouched in memory, and `/cart` has no guard and fetches nothing (`app.routes.ts`,
  `CartComponent`) — so simply opening the cart sends no request and nothing 401s. The redirect is
  driven by an HTTP *response*, so the check needs a real call: change a quantity.
  A refresh (F5) is not a substitute either — the interceptor deliberately skips the redirect for
  `account/user-info`, so startup clears the session silently instead of bouncing you.

### Known noise, not a failure

Going to `/checkout` with the cookie deleted redirects to login **and** shows a red snackbar. The
redirect is the interceptor doing its job; the snackbar is `checkout.component.ts`'s catch printing the
raw `HttpErrorResponse` text (`… 401 Unauthorized`), which reads like a login error but isn't one.
Pre-existing, unrelated to BUG-18.

### What no manual check covers

An attacker sending a forged cart id can't be reproduced in a browser — the parameter no longer
exists to tamper with. That half is proved at the wire level by
`tests/API.Tests/Integration/CartOwnershipTests.cs`.
