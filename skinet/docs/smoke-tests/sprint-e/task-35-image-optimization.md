# T35 — did the image work actually make the pages faster?

`NgOptimizedImage` on 8 of the app's 9 `<img>` sites, the 18 product files re-encoded 1024 → 800px,
4 unreferenced files deleted, and `fetchpriority="high"` on the splash logo in `index.html`.

**Why a file:** no test can assert LCP, CLS or page weight — Vitest runs in jsdom with nothing rendered,
and Playwright asserts behaviour, not performance. Lighthouse also varies run to run, so the numbers only
mean something if the conditions are pinned. That is what this file is: the conditions, so the run is
repeatable at the gate or by a reviewer.

## Setup

Lighthouse measures the **production artifact**, not `ng serve` — a dev build is unoptimized and its
numbers are noise.

```bash
docker compose up -d redis                      # /shop 500s without it: GetProducts carries [Cache(10)]
cd skinet/client && npx ng build                # writes src/API/wwwroot
cd ../.. && dotnet run --project skinet/src/API/API.csproj
```

Then, per page, three runs and take the **median**:

```bash
npx --yes lighthouse@13.4.1 "https://localhost:7096/shop" \
  --only-categories=performance \
  --chrome-flags="--ignore-certificate-errors --headless=new --no-sandbox --disable-gpu" \
  --output=json --output-path=shop-1.json --quiet
```

- `--ignore-certificate-errors` is required: Lighthouse launches a fresh Chrome profile that has never
  trusted the local dev certificate.
- Default preset, unchanged — mobile, 412 × 823 at dpr 1.75, simulated 150ms RTT / 1638 kbps / 4× CPU.
  Record it anyway; a comparison against a differently-throttled run is meaningless.
- ⚠️ chrome-launcher throws `EPERM` deleting its temp profile on Windows **after** the report is written.
  Cosmetic. Check the `.json` exists rather than the exit code.

## Results — 2026-09-18, median of 3

| | `/` before → after | `/shop` before → after |
|---|---|---|
| Performance | 68 → 68 | **68 → 71** |
| Total bytes | 931 → 936 KB | **3440 → 1759 KB (−49%)** |
| CLS | 0 → 0 | **0.092 → 0** |
| TBT | 157 → 151 ms | 241 → 219 ms |
| LCP | 5513 → 5580 ms | 4380 → 4380 ms |
| `unsized-images` | fail → **pass** | fail → **pass** |
| `lcp-discovery-insight` | fail → fail | fail → **pass** |

Image files on disk: **5521 → 1301 KB per tree (−76%)**.

Three results that look like failures and are not:

- **`/shop` LCP does not move, by design.** Its LCP element is the splash logo in `index.html`, which
  `provideAppInitializer` holds on screen until `getUserInfo()` returns — the app renders after its data
  arrives, never before. T35's claim on `/shop` is bytes and CLS.
- **`/` LCP does not improve.** `lcp-discovery-insight` fails on `requestDiscoverable`, and always will:
  the hero is rendered by Angular, so it is not in the initial document. Only SSR or a `<link rel=preload>`
  changes that, and a static `index.html` would preload the hero on every route including `/shop`.
- **`/` gains 5 KB.** That is `NgOptimizedImage` in the initial bundle — the header imports it, and the
  header is on every route.

## The runs

### 1 — the catalogue paints, lazily, at the right size

- **Do:** open `https://localhost:7096/shop` with DevTools → Network, filter Images. Do not scroll.
- **Expect:** ten cards, square and undistorted. Only the **first** card's image is requested eagerly;
  the rest arrive as you scroll. In the console:

```js
const c = [...document.querySelectorAll('img[ng-img]')].filter(i => i.closest('app-product-item'));
({ eager: c.filter(i => i.getAttribute('loading') === 'eager').length,   // 1
   lazy:  c.filter(i => i.getAttribute('loading') === 'lazy').length,    // 9
   rendered: [...new Set(c.map(i => i.clientWidth))],                    // [294] at container 1536
   natural:  [...new Set(c.map(i => i.naturalWidth).filter(Boolean))] }) // [800]
```

- **Why:** `priority` is fed `$index === 0` from `shop.component.html`. If every card came back eager,
  the input is not reaching `ProductItemComponent` and lazy loading is doing nothing.

### 2 — the header logo is not stretched

- **Do:** any page. Console: `const l = document.querySelector('header img'); l.clientWidth + 'x' + l.clientHeight`
- **Expect:** **`160x64`**.
- **Why:** the logo is 250 × 100, and the directive writes those as real `width`/`height` attributes.
  Without `w-auto` beside `max-h-16`, `max-height` clamps the height to 64 while the width attribute holds
  it at 250 — a visibly squashed logo. `160x64` is the only proof the ratio survived.

### 3 — the hero still covers, and is still a plain `<img>`

- **Do:** open `/`. Console: `document.querySelector('app-home img').hasAttribute('ng-img')`
- **Expect:** the hero fills its rounded box behind the heading, and the answer is **`false`**.
- **Why:** the hero is the one deliberate exception. `ngSrc` withholds `src` until `ngOnInit`, which cost
  it ~217 ms of LCP for nothing — measured across `fill`, `fill` without `priority`, and `width`/`height`,
  all three landing on 5730 ms against a 5513 ms baseline. A `true` here means the exception was undone.

### 4 — the thumbnails that need a login

Sign in, then check each renders square and undistorted, and that no image is missing:

| Route | What to look at | Expected box |
|---|---|---|
| `/cart` | the line-item thumbnail | 80 × 80 |
| `/checkout` → Confirmation | the review row thumbnail | 40 × 40 |
| `/orders/:id` | the order line thumbnail | 40 × 40 |
| `/admin/products` | the table thumbnail | 48 × 48 |
| `/admin/products/new` | paste an image URL → the preview | 128 × 128, inside its wrapper |

- **Why the last row matters most:** the admin preview is the only `fill` image in the app, and `fill`
  leaves the flow. Its `relative w-32 h-32` wrapper is what gives it a box — without one it would
  stretch to the nearest positioned ancestor. A preview that fills the card, or collapses to nothing,
  means the wrapper was dropped.
