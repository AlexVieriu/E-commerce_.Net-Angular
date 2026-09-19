# T35 — did the image work actually make the pages faster?

`NgOptimizedImage` on 8 of the app's 9 `<img>` sites, the 18 product files re-encoded 1024 → 800px,
4 unreferenced files deleted, and `fetchpriority="high"` on the splash logo in `index.html`.

**Why a file:** no test can measure LCP (Largest Contentful Paint), CLS (Cumulative Layout Shift) or
page weight. Vitest renders nothing, and Playwright checks behaviour, not speed. Lighthouse itself
varies run to run, so the numbers only mean something if the conditions are pinned. This file pins them.

## Setup

Measure the **production artifact**, never `ng serve` — a dev build is unoptimized, so its numbers are noise.

```bash
docker compose up -d redis                      # /shop 500s without it: GetProducts carries [Cache(10)]
cd skinet/client && npx ng build                # writes src/API/wwwroot
cd ../.. && dotnet run --project skinet/src/API/API.csproj
```

Then, per page, three runs and take the **median**:

```powershell
npx --yes lighthouse@13.4.1 "https://localhost:7096/shop" --only-categories=performance --chrome-flags="--ignore-certificate-errors --headless=new --no-sandbox --disable-gpu" --output=json --output-path="$env:TEMP\shop-1.json" --quiet
```

One line on purpose: `\` is bash's line continuation and PowerShell rejects it. PowerShell uses a backtick.

- **`--ignore-certificate-errors` is required.** Lighthouse launches a fresh Chrome profile, which has
  never trusted the local dev certificate.
- **Keep the default preset** — mobile, 412 × 823 at dpr 1.75, simulated 150ms RTT / 1638 kbps / 4× CPU.
  Record it anyway: comparing against a differently-throttled run proves nothing.
- **The report lands at** `$env:TEMP\shop-1.json` (ex: `C:\Users\Alex\AppData\Local\Temp\shop-1.json`).
  Keep it out of the repo, or it shows up as untracked noise in `git status`.
- ⚠️ **Ignore the exit code.** chrome-launcher throws `EPERM` deleting its temp profile on Windows *after*
  writing the report. Check the report instead: `Test-Path "$env:TEMP\shop-1.json"` → `True` means it ran.
- **Want it readable?** Swap `--output=json` for `--output=html`.

## Results — 2026-09-18, median of 3

| | `/` before → after | `/shop` before → after |
|---|---|---|
| Performance | 68 → 68 | **68 → 71** |
| Total bytes | 931 → 936 KB | **3440 → 1759 KB (−49%)** |
| CLS (Cumulative Layout Shift) | 0 → 0 | **0.092 → 0** |
| TBT (Total Blocking Time) | 157 → 151 ms | 241 → 219 ms |
| LCP (Largest Contentful Paint) | 5513 → 5580 ms | 4380 → 4380 ms |
| `unsized-images` | fail → **pass** | fail → **pass** |
| `lcp-discovery-insight` | fail → fail | fail → **pass** |

What each row measures:

| Row | In one line |
|---|---|
| Performance | One score out of 100, blending the rows below. |
| Total bytes | How much the page downloads. This is what spends the F1 bandwidth. |
| CLS | How much the page jumps around while it loads. |
| TBT | How long the page ignores your clicks while it loads. |
| LCP | How long until the biggest thing on screen is visible. |
| unsized-images | Pass/fail: is any `<img>` missing width and height? |
| lcp-discovery-insight | Pass/fail: did the browser start the biggest image early enough? |

Image files on disk: **5521 → 1301 KB per tree (−76%)**.

Three results look like failures and are not:

- **`/shop` LCP (Largest Contentful Paint) does not move — by design.** Its LCP element is the splash
  logo, and `provideAppInitializer` holds the splash until `getUserInfo()` returns. The app renders
  after its data arrives, never before. T35's claim here is bytes and CLS (Cumulative Layout Shift).
- **`/` LCP (Largest Contentful Paint) does not improve.** `lcp-discovery-insight` fails on
  `requestDiscoverable` and always will: Angular renders the hero, so it is not in the initial HTML.
  Only SSR or a `<link rel=preload>` fixes that, and a static `index.html` would preload the hero on
  every route — including `/shop`.
- **`/` gains 5 KB.** That is `NgOptimizedImage` in the initial bundle. The header imports it, and the
  header is on every route.

## The runs

### 1 — the catalogue paints lazily, at the right size

- **Do:** open `https://localhost:7096/shop`. Do not scroll.
- **Expect:** ten cards, square and undistorted. In the console:

```js
const c = [...document.querySelectorAll('img[ng-img]')].filter(i => i.closest('app-product-item'));
({ eager: c.filter(i => i.getAttribute('loading') === 'eager').length,   // 1
   lazy:  c.filter(i => i.getAttribute('loading') === 'lazy').length,    // 9
   rendered: [...new Set(c.map(i => i.clientWidth))],                    // [294] at container 1536
   natural:  [...new Set(c.map(i => i.naturalWidth).filter(Boolean))] }) // [800]
```

What the snippet reads:

| | In one line |
|---|---|
| `c` | the ten card images — `ng-img` is the marker the directive stamps on what it controls |
| eager | how many download immediately |
| lazy | how many wait until you scroll |
| rendered | how wide the image is painted on screen |
| natural | how wide the image file really is — `0` until it downloads, which is why nine drop out |

- **Why:** `shop.component.html` feeds `priority` from `$index === 0`. Ten eager images means that input
  never reached `ProductItemComponent`, and lazy loading is doing nothing.

### 2 — the header logo is not stretched

- **Do:** any page. Console: `const l = document.querySelector('header img'); l.clientWidth + 'x' + l.clientHeight`
- **Expect:** **`160x64`**.
- **Why:** the logo is 250 × 100 and the directive writes those as real `width`/`height` attributes.
  Without `w-auto`, `max-h-16` clamps the height to 64 while the width attribute holds 250 — a squashed
  logo. `160x64` is the proof the ratio survived.

### 3 — the hero still covers, and is still a plain `<img>`

- **Do:** open `/`. Console: `document.querySelector('app-home img').hasAttribute('ng-img')`
- **Expect:** the hero fills its rounded box behind the heading, and the answer is **`false`**.
- **Why:** the hero is the one deliberate exception. `ngSrc` withholds `src` until `ngOnInit`, costing
  it ~217 ms of LCP (Largest Contentful Paint) for nothing — `fill`, `fill` without `priority`, and
  `width`/`height` all measured 5730 ms against a 5513 ms baseline. `true` here means the exception was
  undone.

### 4 — the thumbnails behind a login

Sign in, then check each one. Right-click the image → **Inspect**. In the Elements tab, hovering the
element in the tree shows the **rendered** box on the page, and hovering the image URL shows the
**intrinsic** size in the preview. Those two are the check: a product thumbnail should read intrinsic
800 × 800 with a square rendered box.

| Route | What to look at | Expected box |
|---|---|---|
| `/cart` | first element of each cart line, left of the product name | 80 × 80 |
| `/checkout` → **Confirmation**, step 4 of 4 | first cell of each row in the order review table, left of the name | 40 × 40 |
| `/orders/:id` | first cell of each row in the order items table, left of the name | 40 × 40 |
| `/admin/products` | the **Image** column — first column of the products table | 48 × 48 |
| `/admin/products/new` | below the **Picture URL** field; appears only once that field has a value | 128 × 128 |

- **Reaching Confirmation** means walking the stepper: Address → Shipping → Payment
  (`4242 4242 4242 4242`) → Confirmation. Stop there; you do not need to place the order.
- **Why the last row matters most:** the admin preview is the app's only `fill` image, and `fill` leaves
  the flow. Its `relative w-32 h-32` wrapper is what gives it a box. A preview that covers the card, or
  collapses to nothing, means the wrapper was dropped.
