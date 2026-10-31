# BUG-18 + BUG-19 — Postman demo

Reproduces both bugs on the code **before** the fix, then shows the fix holding — red, then green.

| Bug | Fix | Before the fix | After the fix |
|---|---|---|---|
| **BUG-18** cart IDOR — the client chose the cart id | [#72](https://github.com/AlexVieriu/E-commerce_.Net-Angular/pull/72) | user B reads and overwrites user A's cart | B only ever sees their own cart |
| **BUG-19** coupon trusted after one check | [#75](https://github.com/AlexVieriu/E-commerce_.Net-Angular/pull/75) | a forged `amountOff: 999` gives an order of −$789 | discount $5, total $205, Stripe charge 20500 |

## Files

- `skinet-bug18-bug19.postman_collection.json` — 4 folders; the tests on the key requests turn red or green.
- `demo.ps1` — starts the API at the right commit, on a fresh database, for each step.

## Before you start

1. Docker Desktop running (Redis).
2. Stripe test keys in user secrets (`dotnet user-secrets list --project src/API`) — the coupon steps call Stripe.
3. Postman: import the collection · Settings → **SSL certificate verification: off** (localhost dev certificate).

## Run it

```powershell
cd skinet\docs\smoke-tests\postman
.\demo.ps1
```

The script walks through the 4 steps. At each one, run the folder it names (**⋯ → Run folder → Start run**), read the result, then press **Enter**.

| Step | Code | Postman folder | Expected |
|---|---|---|---|
| 1 | `bb6f853` (before #72) | 1 · BUG-18 IDOR · pre-fix | 🔴 2 failed |
| 2 | `development` | 2 · BUG-18 IDOR · fixed | ✅ all passed |
| 3 | `487a41b` (before #75) | 3 · BUG-19 coupon · pre-fix | 🔴 discount 999 |
| 4 | `development` | 4 · BUG-19 coupon · fixed | ✅ all passed |

Done for good: `.\demo.ps1 -Clean` removes the demo copy of the code (`../skinet-demo`).

## If something goes wrong

| Symptom | Cause | Fix |
|---|---|---|
| Login → **401 (Unauthorized)** | every step starts on a fresh database | the folder needs its `Register - User A` request first |
| Place order → *"Prices have changed"* | the forged cart's price differs from the product's | `"price": 200` in *Forge the cart* (product 1 costs $200) |
| Runner stuck on *Preparing to run* | Postman cache | quit Postman from the tray, delete its temp files, reopen |
