# BUG-2 — can a declined card be retried in the same session?

The cart **used to be** deleted when the order was created, one step before the charge — so a decline left
nothing to retry with. Section A reproduces that; B and C prove the fix. A is kept because it is the only
description of what the bug actually looked like.

**Why a file:** no automated test can reach a real decline. Playwright covers the retry; the cart's
disappearance from Redis, and an intent Stripe has finished with, are only visible from outside the app.

Needs Redis, the API, the client, and the Stripe CLI:

```powershell
docker compose up -d redis
stripe listen --forward-to https://localhost:7096/api/payments/webhook   # its own terminal
dotnet run --project skinet/src/API/API.csproj                           # its own terminal
cd skinet/client; npx ng serve                                           # its own terminal
```

Commands in this file are **PowerShell**, run from `skinet/`.

## Reading the cart live

Carts live in Redis DB 0 under `cart:{userId}` (`redis-cli` opens DB 0 by default). **PowerShell**, one
cart — `--raw` is what makes it readable, without it `redis-cli` escapes every quote in the JSON:

```powershell
docker compose exec -T redis redis-cli --raw GET "cart:<userId>" | ConvertFrom-Json | ConvertTo-Json -Depth 5
```

The seeder and the Playwright suite leave carts behind, so `KEYS` returns several. Paste this once per
session and run `carts` for one line each — short id, intent, item count:

```powershell
function carts {
  docker compose exec -T redis redis-cli --raw KEYS "cart:*" | ForEach-Object {
    $c = docker compose exec -T redis redis-cli --raw GET $_ | ConvertFrom-Json
    "{0}  {1}  items={2}" -f $_.Substring(5,8), ($c.PaymentIntentId ?? '(no intent)'), ($c.Items | Measure-Object Quantity -Sum).Sum
  }
}
```

```
6f3130ca  pi_3UFRILGMOucV11Le0wkO3T3E  items=2
7702c036  (no intent)  items=1
```

⚠️ These are PowerShell. `carts` will not exist in a bash shell, and a trailing `\` line continuation
does the reverse — bash treats it as a continuation, PowerShell passes it through as a literal argument
and `redis-cli` reports *"wrong number of arguments for 'get' command"*. Keep commands on one line.

## Reading the orders and the stock

`skinet/data/store.db`. Swap `36` for the `ProductId` in your cart's JSON.

**The one-row query** — re-run this between steps and compare:

```sql
select (select count(*) from Orders) as Orders,
       (select QuantityInStock from Products where Id = 36) as Stock;
```

**The detail query** — only when a step asks which intent an order sits on:

```sql
select Id, Status, PaymentIntentId, BuyerEmail from Orders order by Id desc limit 5;
```

**Cards:** `4000 0000 0000 0002` declines · `4242 4242 4242 4242` succeeds. Any future expiry, any CVC.

## A — before the fix *(what is broken today)*

Base: `https://localhost:4200`, logged in, one item in the cart.

| # | Do this | Expect |
|---|---|---|
| 1 | Reach the **Payment** step, then **[terminal]** `carts` | your `cart:<userId>` is listed — the baseline |
| 2 | Pay with `4000 0000 0000 0002`, then **[terminal]** `carts` again | your cart is **gone** — deleted before the card was ever charged |
| 3 | Read the snackbar | *"Cart unavailable"* — not a decline message |
| 4 | Open `/cart` — it still lists everything — then **F5** | after the reload it is empty; `/checkout` now bounces you to `/cart` |
| 5 | Admin → orders · the product page | one `Pending` order · the product's stock is one lower |

**Why row 4 looks like a pass at first:** `/cart` renders the browser's `_cart` signal, which the decline
never touches. F5 re-fetches from the server, and the server's cart is gone — two copies, disagreeing.

## B — after the fix

Logged in, one product in the cart, sitting on the **Confirmation** step ready to pay.

Steps marked **[terminal]** are shell commands, not clicks.

| # | Do this | Expect |
|---|---|---|
| 0 | **[terminal]** `carts`, then the one-row query | write down the order count, the stock, the cart's `pi_…` and its quantity |
| 1 | Pay with `4000 0000 0000 0002`, then **[terminal]** `carts` | the key is **still there**, same `pi_…` |
| 2 | Read the snackbar | Stripe's own words — *"Your card was declined."* |
| 3 | Look at the stepper and the header | back on **Payment**, badge still shows the item count |
| 4 | **[terminal]** the one-row query, then the detail one | **one more order** than at step 0, and the **stock is lower by the cart's quantity**. The new order is `Pending`, on that same `pi_…` |
| 5 | Type `4242 4242 4242 4242` and pay — **no reload** | `/checkout/success` |
| 6 | **[terminal]** the one-row query | **exactly the same two numbers as step 4** — the retry reused the order instead of making its own. A second order here, or stock down twice, is the failure |
| 7 | Admin → orders | that order now `PaymentReceived` (needs `stripe listen`; else the admin sync icon) |
| 8 | **[terminal]** `carts` | the key is **gone** — the cart goes with the successful payment |

**Step 6 is the one that matters.** One intent means one order and one stock decrement, however many
cards the customer tries — `CreateOrderAsync` looks an existing order up by `PaymentIntentId`.

## C — an intent Stripe has finished with *(Decision 5)*

Stripe cancels an intent confirmed past its limit, which is not reproducible on demand — the limit is
*"a variable upper limit"* with no published number. Cancelling one by hand forces the same state.

Starts from an **empty cart**: B's was deleted by its successful payment. Step 7 needs a terminal of its
own, since `stripe listen` is occupying the one it runs in.

| # | Do this | Expect |
|---|---|---|
| 1 | **[terminal]** the one-row query | write down the order count and the stock |
| 2 | `/shop` → add a product to the cart | badge shows the count |
| 3 | `/cart` → **Checkout** | the Address step |
| 4 | Type an address whose **city differs** from the one saved on the account. Leave *Save my information* **unticked** | — |
| 5 | **Next** → pick a delivery method → **Next** | the Payment step |
| 6 | **[terminal]** `carts` to find your cart's id, then the full `GET` on it | note the `PaymentIntentId` as **pi_A**, and the `ProductId` — `carts` alone does not show the product |
| 7 | **[terminal, a second one]** `stripe payment_intents cancel <pi_A>` | it prints the intent back, with `"status": "canceled"`. `stripe listen` also logs a `payment_intent.canceled` — harmless, the app only handles `succeeded` |
| 8 | **Back** to Shipping, then **Next** to Payment | no error, no snackbar |
| 9 | **[terminal]** `carts` | a **different** `pi_…` — call it **pi_B**. This is the whole test |
| 10 | Look at the card field | **empty** — the elements were rebuilt on pi_B's secret |
| 11 | **Back**, **Back** to Address | your **step 4 city**, not the account's — the rebuild carried it over |
| 12 | **Next**, **Next** to Payment, type `4242 4242 4242 4242`, **Next**, **Pay** | `/checkout/success` |
| 13 | **[terminal]** the one-row query, then the detail one | **one more order** than at step 1, and the **stock is lower by the cart's quantity** — once, not twice. The new order is on **pi_B**, and nothing is on pi_A |
| 14 | Admin → orders | that order `PaymentReceived` |

Step 11 is why step 4 says *differs*: if the rebuild fell back to the account's saved address you would
see the old city, and the carry-over would look like a pass when it was not.

⚠️ Never click **Pay** before step 7. Do, and step 13 shows **two** orders: the swap makes
`CreateOrderAsync` miss the first one, which is keyed on pi_A. That is known and deferred to **T41**.

⚠️ **Reading a failure:** a second order at **B6**, or stock moving twice, means the retry created its own
intent instead of reusing the cart's. An error or a `/server-error` redirect at **C8** means the app is
still trying to update a dead intent — the spent-intent check did not fire.
