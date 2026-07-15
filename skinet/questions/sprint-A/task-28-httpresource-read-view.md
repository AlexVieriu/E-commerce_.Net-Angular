# Task 28 — httpResource Read Views (admin dashboard)

> **How to use this doc** (the gate → [`mindset/the-gate.md`](../../../mindset/the-gate.md)) — one file, top to bottom:
> 1. Read 📂 Read First (the code) → 🗺️ Change Map (the ideas). ~10 min.
> 2. Attempt 🔁 REBUILD / 🧠 EXPLAIN / 💥 BREAK **inline**. Know it → write it; stuck → 🧭 Hints, then I answer, move on.
> 3. I fill ⚖️ / 🌫️ / 💡 / 🎤 / 📚 from the gaps you hit. Read 💡 Explanation once, front to back.

---

## 📂 Read First

*Open these and read the real code before the Change Map's abstraction. Depth per file — don't read them all the same way.*

| Depth | File | Focus on |
|---|---|---|
| **Read fully** — it *is* the task | `features/admin/dashboard/admin-dashboard.component.ts` | every line: the `httpResource<Dashboard>(() => …)` declaration, the `lowStockShown` computed, the imports array |
| **Read the key lines** | `features/admin/dashboard/admin-dashboard.component.html` | the `@if isLoading() / @else if error() / @else` state chain, and the `@if (data.lowStockCount > lowStockShown())` header. *Skim* the five cards + the two `<table mat-table>` blocks |
| **Read fully** — tiny | `shared/models/dashboard/dashboard.ts` · `recentOrder.ts` · `lowStockProduct.ts` | the `type`s: note **zero `?`**, and `status` reuses the `OrderStatus` union, not `string` |
| **Just locate it** | `docs/frontend/state-management.md` → §"Read-only server views" | reference for later — know it exists, don't study it |

---

## 🗺️ The Change Map

### The Ideas — the spine

**Everything in this doc is a view of these four.** Nothing new appears at test time.

| # | Idea |
|---|---|
| **1** | **`httpResource` is a reactive, signal-based *read*.** You give it a function that returns a request (URL/params); when a signal it reads changes, it re-fetches. It hands back **signals**: `.value()`, `.isLoading()`, `.error()`, `.reload()`. No `subscribe`, no `async` pipe, no manual loading flag. **Use it for read-only data the component owns; use a `core/services/` signal when state is shared or mutated** (cart, current user). |
| **2** | **A hand-written TS model is an *assertion*, not a guarantee.** It only checks what you *told* it. So mirror the backend **exactly**: `DashboardDto` has **zero** nullable fields (`required string` + value types) → the model has **zero** `?`. And `status` reuses the **`OrderStatus` union**, not `string` — a union rejects a typo'd status at compile time; `string` waves it through. *(This is the BUG-11/BUG-13 lesson: the type lied, so the bug looked safe.)* |
| **3** | **The Low-stock *card* counts *all* low products; the Low-stock *table* shows at most 10 — so the UI must admit when it's hiding rows.** The card binds `lowStockCount` (every product with stock ≤ 5, uncapped); the table binds `lowStockProducts` (capped **server-side at 10** — `LowStockListSize`). Same set, two elements: when the count beats the rows shown, the header says *"(showing 10 of 12)"*; when they're equal it's hidden. *(The Recent-orders table is a capped top-5 too, but it's NOT this idea — nobody reads a "Recent" table as "all orders.")* A Low-stock table that silently shows 10 of 30 **lies about data it doesn't have.** |
| **4** | **Signals + `OnPush` are why the view updates with no `subscribe`.** Reading `dashboard.value()` in the template *registers a dependency* on that signal; when the fetch completes and the signal changes, Angular re-renders just this component. That's exactly what `OnPush` needs — and in a **zoneless** app `OnPush` isn't optional, it's the model. |

**The one-line version:** *fetch is a signal you read, not a stream you subscribe to — and the model + the header must never claim more than the data actually guarantees.*

### The files

*Read this before the cold sit. The task is **6 files**, and 3 of them are the concept.*

**LOAD-BEARING — this *is* T28:**

| File | Idea | What it does |
|---|---|---|
| `admin-dashboard.component.ts` | **1, 4** | `httpResource<Dashboard>(() => …'admin/dashboard')` · `OnPush` · `lowStockShown = computed(...)` — the count-vs-cap guard |
| `admin-dashboard.component.html` | **3, 4** | the `@if isLoading() / @else if error() / @else` state chain; the `@if (lowStockCount > lowStockShown())` header; reads signals directly (no `async`) |
| `shared/models/dashboard/*.ts` | **2** | `dashboard.ts` + `recentOrder.ts` + `lowStockProduct.ts` — `type`, **no `?`**, `status` is the `OrderStatus` union |

**MECHANICAL — skim, don't study:**

- **The two `<table mat-table>` column blocks** — shape copied from `admin-orders.component.html`. *Look, don't reuse:* the contract differs — `RecentOrder` (5 lean fields) is **not** `Order` (paginated, actions, dialog). Same look, different data.
- **The five `mat-card`s** — first `MatCard` use in the app, but each card is four trivial lines (label + value).
- **`state-management.md`** — one new section documenting the `httpResource` read-view pattern. Reference, not concept.
- **The commits** — WIP feat + a docs commit. Noise.

> **The lesson about "reuse":** the obvious instinct was *reuse the admin-orders table*. The Change Map
> shows why that was the trap — the two tables share a **look**, not a **contract**. `RecentOrder` has no
> id-paginated list, no refund/sync actions, no `Order` fields. A shared component would bridge a gap that
> doesn't need bridging, for a second caller (T29) that lists **products** and can't use it anyway.

---

## 🧭 Hints — where to look

*Location anchors only. These give nothing away — they tell you where to connect the dots.*

| Q | Look here | What to notice |
|---|---|---|
| E1 | `admin-dashboard.component.ts` line with `httpResource` vs `cart.service.ts` `_cart` signal | One *owns* read-only data; the other holds *shared, mutated* state. What decides which you reach for? |
| E2 | `admin-dashboard.component.html` — the `@else { … data.value() … }` block | There is no `subscribe` and no `\| async`. So what tells Angular to re-render when the fetch lands? Then look at the decorator. |
| E3 | `DashboardService.cs` `LowStockListSize` vs `LowStockCount` · then the component's `lowStockShown` computed | The count and the list are sized by **different** rules. Where does the header condition live, and when is it false? |
| B1 | `models/dashboard/recentOrder.ts` `status` field · and `models/order/order.ts` `OrderStatus` | It reuses a union, not `string`. Ask what a `string` would have let compile. Then recall `user.ts` in BUG-13. |
| B2 | `admin-dashboard.component.html` — the `@if (data.lowStockCount > lowStockShown())` header | Delete that condition in your head and always show the 10-row table. Who reads this page, and what do they *do* with it? |

---

## 🔁 REBUILD — write the key part cold

*(2026-07-15 — honest results)*

| Piece | Idea | Mark |
|---|---|---|
| `httpResource<Dashboard>(() => …'admin/dashboard')` | 1 | **PASS** — wrote it cold |
| `lowStockShown = computed(...)` | 3 | **BLANK** — didn't know what `computed` did |
| the imports array | — | skipped — mechanical, the IDE adds them |
| which model fields are `?` | 2 | **BLANK** — misread the question; answer is **none** |

## 🧠 EXPLAIN — why it works

> Why does the view update with **no** `subscribe` / `async` pipe? · Why a `computed`, not a plain field?

Covered via discussion, not a cold attempt (2026-07-15). Answers in 💡 Explanation §4–5.

## 💥 BREAK — what fails in prod

> `status: string` instead of the `OrderStatus` union · dropping the `count > shown` header.

Not separately attempted — folded into 💡 Explanation §6 and the Change Map (Idea 3).

## ⚖️ Why this approach

*(T28 spike decisions — see `mindset/spikes.md`)*
- **5 stat cards (1c)** — full at-a-glance view; cards are cheap, so show every scalar.
- **Dumb read-only recent-orders table (2a)** — not a shared/reused component. T29 lists *products*, so a shared orders-table would have no second caller.
- **Render the low-stock table (3a)** — the data's already on the wire; it makes the count actionable.
- **No live updates (4a)** — reload on navigation + a refresh button. SignalR would need an admin group + a push at every mutation → real backend scope, parked in `backlog.md`.

## 🌫️ What confused me

| Gap | Tier | Anchor |
|---|---|---|
| `.value()` reads the parsed body — a signal, `undefined` until it loads | **OWN** | `admin-dashboard.component.ts` · the `httpResource` line |
| `computed` vs a plain field — why the count would freeze at `0` | **OWN** | 💡 Explanation §5 |
| honest models carry no `?` — the backend sends no nulls | **OWN** | `models/dashboard/*.ts` |
| the count-vs-cap header ("showing 10 of 30") — card counts all, table shows ≤10 | **RECOGNIZE** | `admin-dashboard.component.html` header |
| `httpResource` vs `HttpResponse` — a name collision | **LET GO** | one's a signal-fetch, one's a response class |

## 💡 Explanation

**Answers your three gaps:** what `.value()` reads · what `computed` does (and why not a plain number) · which model fields carry a `?`. Read top to bottom — each step sets up the next.

**1. The old way, then `httpResource`**
- **Concrete:** to show server data the old way you'd `inject(HttpClient)`, call `.get<Dashboard>(url).subscribe(...)`, stash the result in a field, and hand-manage `loading` / `error` booleans. Four parts you wire yourself.
- **`httpResource` collapses all four:** `dashboard = httpResource<Dashboard>(() => environment.apiUrl + 'admin/dashboard')`. You hand it a function that returns the URL; it runs the GET and hands back a bundle of **signals**. *(It's a wrapper around `HttpClient`, GET by default.)*

**2. `.value()` — the answer to "what reads the body"**
- **`dashboard.value()`** is a signal holding the parsed JSON body, typed `Dashboard`. You read it by *calling* it: `dashboard.value().totalRevenue`.
- **It is `undefined` until the first response lands.** Hold onto that — it's the whole reason the `computed` needs a guard.
- Siblings on the same object: **`.isLoading()`**, **`.error()`** (your `@if / @else if` states, free), **`.reload()`** (re-run the GET — your refresh button). The docs also give **`.hasValue()`** as the idiomatic "has the body arrived?" check.

**3. Why it lives in the component, not a service**
- The cart sits in a `core/services/` signal because it is **shared and mutated** across the app.
- The dashboard data is **read-only and owned by one screen** → `httpResource` right in the component. **Rule:** *shared/mutated → service signal; read-only + one owner → `httpResource`.*

**4. Why the view updates with no `subscribe`, no `async` pipe**
- `value()` is a **signal**. Reading `dashboard.value()` in the template **registers a dependency**.
- When the GET finishes, that signal changes → with **OnPush**, Angular re-renders exactly this component. No subscription, no `async` pipe. *(In a zoneless app this signal-tracking IS change detection — OnPush isn't optional.)*

**5. `computed` — the count-vs-cap guard (your TODO 2)**
- `lowStockShown = computed(() => this.dashboard.value()?.lowStockProducts.length ?? 0)`
- **What `computed()` makes:** a **read-only signal** whose value is *derived* from other signals — here, from `dashboard.value()`. It's **memoized** (recomputes only when `dashboard.value()` changes) and **lazy** (only when read).
- **Why `computed()` and not a plain number** — the real point:
  - `dashboard.value()` **changes over time**: `undefined` at first → the data after the GET → new data after `reload()`.
  - A plain `lowStockShown = this.dashboard.value()?.lowStockProducts.length ?? 0` runs **once**, at construction, while `value()` is still `undefined` → it's stuck at `0` **forever**.
  - `computed()` re-derives **every time** `value()` changes, and because it's a signal the template re-renders. **A plain number is a dead snapshot; a `computed` is live.**
- **The `?.` and `?? 0`:** since `value()` is `undefined` until the response lands, `?.` stops `.lowStockProducts` from throwing, and `?? 0` shows `0` meanwhile.

**6. The `?` trick (your TODO 3)**
- The question — *"which model fields are marked `?` (i.e. the field can be null / absent)?"* — has the answer **none.** That's the trick. *(In TS, `field?: type` says "this may be missing or null".)*
- `DashboardDto` (backend) has **zero nullable fields**: the scalars are value types, and `BuyerEmail` / `Status` / `Name` are `required string`. So the honest TS models (`dashboard.ts`, `recentOrder.ts`, `lowStockProduct.ts`) carry **no `?`**.
- **Idea 2 restated:** mirror the backend *exactly*. A `?` where the API never sends null is a lie in the "safe" direction (and forces pointless guards); a *missing* `?` where the field IS nullable is the **BUG-13** lie (`"null null"`). Here, honest = no `?`.

**The one chain:** `httpResource` gives you the body as a signal — `.value()`, `undefined` until it loads → reading that signal in an OnPush template is what refreshes the view → `computed` wraps a *derived* read of it so the count stays live instead of freezing at the first `undefined` → and every model it's typed against carries no `?`, because the backend never sends null.

## 🎤 Interview Q&A

**Q: How do you fetch read-only data in a modern Angular component?**
- `httpResource(() => url)` — a reactive wrapper over `HttpClient`.
- Returns **signals**: `value()` (parsed body), `isLoading()`, `error()`, `reload()`. No `subscribe`, no manual loading flags.
- **When:** read-only data owned by one screen → in the component. Shared/mutated state (cart, user) → a `core/services/` signal.

**Q: Why `computed()` for a derived value instead of computing it once?**
- The source signal changes over time: `value()` is `undefined` → data → new data after `reload()`.
- A plain field computes **once** at construction (while `value()` is still `undefined`) and freezes at that value.
- A `computed` re-derives whenever its dependency changes and, being a signal, re-renders the OnPush view.
- Bonus: it's **memoized** (cached) and **lazy** (runs only when read).

**Q: Why type a hand-written model precisely instead of `string` / optional fields?**
- A TS model is an **assertion, not a guarantee** — TS only checks what you told it.
- Mirror the backend exactly: reuse the `OrderStatus` **union** (a typo'd status fails to compile; `string` wouldn't).
- Add `?` **only** where the API can actually send null. This DTO has zero nullable fields → the model has none.
- Getting nullability wrong is what caused **BUG-13** (`"null null"` saved onto the order).

## 📚 Sources

- **Angular — `httpResource` guide** ([angular.dev/guide/http/http-resource](https://angular.dev/guide/http/http-resource)): confirms it's *"a wrapper around HttpClient"*, *"reactive — whenever one of the signals it depends on changes … the resource will emit a new http request"*, and exposes `value()` / `isLoading()` / `error()` / `hasValue()`. *(`reload()` verified live via the dashboard refresh button.)*
- **Angular — Signals guide** ([angular.dev/guide/signals](https://angular.dev/guide/signals)): `computed()` creates *"read-only signals that derive their value from other signals"*, the value is *"cached"* and recomputed only when a dependency changes, evaluation is lazy (*"does not run … until the first time you read"*), and computed signals *"are not writable"*. Also: reading a signal inside an OnPush template registers it as a dependency, so the view updates when the signal changes.
