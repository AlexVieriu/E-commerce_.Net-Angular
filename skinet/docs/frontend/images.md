# Images

One rule, so the next image added does not re-open T35.

## The rule

**Use `NgOptimizedImage` on every `<img>` — except a solitary, above-the-fold LCP image.**

Why the exception: `ngSrc` withholds `src` until `ngOnInit` runs, so the browser cannot start the
download when the DOM is written. Angular repays that delay with a preload link (SSR) or a smaller file
(a CDN loader). **This app has neither.** So the directive pays off only where there is lazy loading to
gain or a layout shift to prevent. On a lone hero — already eager, already stable — it is pure cost:
**+217 ms LCP on `/`**, identical across `fill`, `fill` without `priority`, and `width`/`height`.

The hero in `home.component.html` stays a plain `<img>`. Angular stays quiet about it: the LCP warning
only fires for images the directive owns.

## Which mode

| Situation | Mode | Example |
|---|---|---|
| Size known, box scales with the viewport | `width` + `height` at the **intrinsic** size | the catalogue card, `800`/`800` |
| Size known, box is a fixed pixel square | `width` + `height` at the **rendered** size | cart line `80`, admin table `48`, order and checkout rows `40` |
| Size unknowable | `fill`, in a parent with a set size and `position: relative` | the admin form preview — a user-typed URL of any ratio |

`fill` leaves the flow (`position: absolute; inset: 0`), so it needs a wrapper to give it a box. Without
one it stretches to the nearest positioned ancestor and covers it. `.mat-mdc-card` is already
`position: relative` — which is why the catalogue card uses `width`/`height` instead.

Angular writes `loading`, `fetchpriority` and `decoding` itself. Never set them by hand; an explicit
`decoding` is ignored on a `priority` image anyway.

## Who gets `priority`

**The LCP element of that route, and nothing else.** Everything else stays lazy — that is the point.

- `/shop` — the **first card only**. `shop.component.html` passes `[priority]="$index === 0"`. Marking
  the whole visible row trades away lazy loading for one eager fetch.
- `/shop/:id` — the details image, the largest product image in the app.

`priority` forces `decoding="sync"`, which costs ~20 ms of TBT. That buys painting in the same frame, and
is only worth it on an LCP element.

## The files

- `client/public/images/` is the **only** copy. `ng build` writes `src/API/wwwroot/`, which is ignored.
- Product images are **800 × 800 WebP behind a `.png` extension**. They were always WebP; the extension
  is historical. Renaming them would break every `PictureUrl` row, so the server relabels them instead.
- Re-encode with a one-off `npx sharp-cli --input "*.png" --output <dir> resize 800 800`, then rename the
  `.webp` output back to `.png`. Nothing in the repo resizes images, deliberately — 18 files that never
  change do not need a pipeline.

Why 800: it covers the catalogue card (294 px) at DPR 2.7 and the details view (576 px, capped by
`max-w-xl`) at 1.39. Sharp where ten images load at once, soft only on a retina details page showing one.

## Alt text

Bind the product name, never a literal. `alt="product image"` on ten cards is ten identical
announcements to a screen reader.

Verification and the before/after numbers: `docs/smoke-tests/sprint-e/task-35-image-optimization.md`.
