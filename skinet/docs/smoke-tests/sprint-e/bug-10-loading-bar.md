# BUG-10 — the top loading bar: does it clear by itself?

The bar used to appear on the click that started a request and only leave on the next click.
`BusyService` is now signal-based.

**Why a file:** the specs run in jsdom — no navigation, nothing rendered — so a real 401 redirect and a
real cancelled request are out of reach.

Needs Redis, the API and the client. No Stripe:

```bash
docker compose up -d redis
dotnet run --project skinet/src/API/API.csproj
cd skinet/client && npx ng serve
```

## Reading the count live

`delay(500)` is dev-only, so the bar is held half a second on every request. For the number:

```js
ng.getComponent(document.querySelector('app-header')).busyService.busyRequestCount()
```

**It must read `0` when the page is idle** — whatever the bar looks like. (`ng` is a dev-build global.)

Throttle where a run says so. **Slow 3G is not enough** — these payloads are small and finish before you
can click again. Use DevTools → Network → throttling dropdown → **Add profile**, latency **3000 ms**:

| Field | Value |
|---|---|
| Profile name | `overlap` |
| Download / Upload | leave default |
| Latency | **3000** ms |

## The runs

Base: `https://localhost:4200`. **Overlapping requests are not here** — `header.component.spec.ts` already pins *"stays while a second request is still in flight"*.

| # | Do this | Expect | Reaches what no spec can |
|---|---|---|---|
| 1 | Load `/shop`, then **touch nothing** | bar appears, then clears on its own · count back to `0` | the original defect — clearing with no user interaction |
| 2 | Log in. DevTools → Application → Cookies → delete the auth cookie. Trigger any authenticated call (`/checkout`) | redirected to login **and** the bar clears · count `0` | a 401 through `errorInterceptor` — no response body, error path only |
| 3 | Throttle to `overlap`. Start loading `/shop`, then click another nav link before it lands | the request is cancelled (Network shows it red/cancelled), the bar clears anyway · count `0` | a genuinely cancelled request — the spec only simulates it |
| 4 | Filter the shop to something with no matches | "no results" appears **only after** loading finishes, never during | `empty-state` — the second consumer, which had the same defect unreported |

## Result — 2026-09-13

| # | Result | Count observed | Notes |
|---|---|---|---|
| 1 | ✅ pass | `0 → 1 → 0` | bar cleared on its own, nothing touched |
| 2 | ⏳ not run | — | needs a deleted auth cookie; run at the deploy gate |
| 3 | ✅ pass | `0 → 1 → 0` | paged the grid, routed to `/` 60 ms later; count back to 0 and the bar gone. **Note:** the route change is itself a click, so this proves `finalize` on unsubscribe, not the render path — row 1 and row 4 prove that |
| 4 | ✅ pass | `0 → 1 → 0` | searched `zzzznomatch`. While loading: 0 cards, `app-empty-state` present but **rendering nothing**. Count hit 0 and *"No products match this filter"* appeared **with no further interaction** — the strongest evidence in the file |

**Why row 4 is the best of the four:** the message appears on the `1 → 0` transition alone. Before the fix
`empty-state` read `busyRequestCount` as a plain field, so that transition rendered nothing and the panel
stayed blank until the user clicked something.

⚠️ **Clears only when you click** = the render fix is not working; that is the original symptom.
**Never clears** = the count is stranded above zero, so the leak is in the interceptor, not the signal.
