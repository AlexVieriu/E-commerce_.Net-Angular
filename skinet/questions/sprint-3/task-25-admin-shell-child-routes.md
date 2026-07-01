# Task 25 — Admin Shell + Child Routes

## Why this approach

- Admin needed 3 sibling pages (dashboard/products/orders) sharing one sidebar — repeating it in each would duplicate layout code.
- `AdminComponent` becomes a **shell**: owns the sidebar + a `<router-outlet>`; `features/admin/routes.ts` nests dashboard/products/orders as `children` of that shell's route.
- One `canActivate: [authGuard, adminGuard]` on the parent route protects the whole subtree — no need to repeat it per child.
- Matches the existing `loadChildren` pattern already used by checkout/orders/account.

## What confused me — actual gaps from the LEARN + cold test

| Gap | Location | Tier |
|---|---|---|
| `<router-outlet>` is a placeholder the Router fills — not "communication" between parent and child | `admin.component.html · <router-outlet>` | **OWN** |
| Guard on the parent protects children by **activation**, not inheritance — reaching a child requires the Router to first match/activate the guarded parent segment | `app.routes.ts` admin entry + `guards.md` | **OWN** |
| Empty-path `redirectTo` needs `pathMatch:'full'` — default `'prefix'` matches every URL under it, causing an infinite redirect loop | `admin/routes.ts · { path:'', redirectTo:'dashboard' }` | **OWN** |
| `mat-sidenav` modes: `over` (default, backdrop) / `push` (shoves content) / `side` (fixed, no backdrop) | `admin.component.html · mode="side"` | **RECOGNIZE** |
| `routerLinkActive` has two unrelated uses: a CSS-class string, or `#rla="routerLinkActive"` to read `rla.isActive` programmatically | `admin.component.html · routerLinkActive` | **RECOGNIZE** |
| `httpResource` — signals-based reactive fetcher; re-runs automatically when a signal it reads changes | `admin-orders.component.ts · orders = httpResource(...)` | **RECOGNIZE** |
| TS `Record<K,T>` is a mapped type (index signature), not a class — different from a C# `Dictionary<K,V>` and unrelated to a C# `record` | `admin-orders.component.ts · params: Record<string,string>` | **RECOGNIZE** |
| TS structural typing — an object literal's type is inferred from its shape, no class/interface needed; `const` binds the variable, not the object (still mutable) | `admin.component.ts · navLinks` | **RECOGNIZE** |
| `[routerLink]` with no leading slash resolves **relative** to the component's own route segment, not the app root | `admin.component.html · [routerLink]="link.path"` | **RECOGNIZE** |
| `httpResource`: released 19.2 (`@experimental`), stable at 22 GA; without it you'd `inject(HttpClient)` + `.get()` + manual `.subscribe()`/loading state | `admin-orders.component.ts · orders = httpResource(...)` | **LET GO** |
| `templateUrl` splits markup into its own `.html` file; `export` makes a class importable by `routes.ts` | `admin-dashboard.component.ts` | **LET GO** |
| `calc(100vh - 8rem)` — `100vh` = full viewport height; subtracting reserves space for the fixed header | `admin.component.scss · .admin-shell` | **LET GO** |

## Explanation

### The shell pattern — no parent/child communication

`AdminComponent` and `AdminDashboardComponent` never reference each other. `<router-outlet>` is just a
marker in the parent's template. The **Router** reads the current URL, looks it up in `routes.ts`, and
inserts the matched component's DOM at the outlet's position. Navigating `/admin/dashboard` →
`/admin/products` only swaps what's in the outlet — the shell (sidebar, layout) never re-renders.

### Guard placement — activation, not inheritance

`canActivate` on a route only runs for **that route**. A child doesn't inherit it by copying rules down.
It works because reaching `/admin/products` means the Router must first resolve the `/admin` path
segment — which is where the guard sits. No match on the parent segment, no route to the child at all.
Put the guard on a child instead, and the *other* children have zero protection — you'd have to repeat
the guard on every one, and forgetting one is a real security hole.

### `pathMatch: 'full'` vs `'prefix'` — the infinite loop case

`pathMatch: 'prefix'` (default) matches when the URL *starts with* the path. An empty path `''` is a
prefix of everything, so `{ path:'', redirectTo:'dashboard' }` would fire again immediately **after**
redirecting to `dashboard` — because `''` still "starts" `/admin/dashboard` too. Infinite loop.
`pathMatch:'full'` only matches when the remaining URL is *exactly* empty, so once you're at
`/admin/dashboard` there's leftover text and the empty-path rule no longer applies.

### `mat-sidenav` modes

| Mode | Backdrop | Behavior |
|---|---|---|
| `over` (default) | yes | Overlays content (mobile drawer) |
| `push` | no | Shoves content sideways |
| `side` | no | Fixed alongside content — used here (persistent admin nav) |

`opened` (boolean attribute, no value = `true`) keeps it open on load; there's no toggle button, so it
stays open permanently.

### TS structural typing — why `navLinks` "just works" with no type declared

C# is **nominal**: two objects are only interchangeable if they're declared as the same named class/
interface. TS is **structural**: a type is defined by its *shape*, so any object with matching properties
qualifies — no declaration needed.

```typescript
const navLinks = [
  { path: 'dashboard', label: 'Dashboard', icon: 'dashboard' },
  { path: 'products', label: 'Products', icon: 'inventory_2' },
];
```
TS infers the element type as `{ path: string; label: string; icon: string }[]` straight from the
literal — no `interface NavLink { ... }` required. `@for (link of navLinks; track link.path)` then knows
`link.path` is a `string` purely from that inferred shape.

`const` only fixes the **binding** — `navLinks` can never point to a *different* array — but the array's
contents (and each object's properties) are still mutable. Closest C# comparison: an anonymous type
(`var x = new { Path = "dashboard" }`) also infers its shape from the literal, but C# still compiles it
to a fixed, compiler-generated type; TS's structural typing works across *any* two independently-declared
shapes that happen to match.

### `[routerLink]` relative resolution

**Concrete trace:** you're on `/admin/orders`. The sidebar link `[routerLink]="'dashboard'"` (no slash)
sits inside `AdminComponent`'s template, and `AdminComponent` itself is registered at `/admin`. Angular
asks *"whose template is this link in?"* — answer: `AdminComponent`, mounted at `/admin` — so it takes
`/admin` and continues from there: `/admin` + `dashboard` → `/admin/dashboard`.

**The abstract rule:** a `routerLink` with no leading slash resolves **relative to the route the link's
component lives in**, not the app root. A leading slash (`/dashboard`) skips that lookup entirely and is
**absolute** from the app root — which isn't a registered top-level route here, so it'd fail.

**Analogy:** no slash = *"from where you're standing, go to 'dashboard'"* (starting point = wherever the
link physically lives). Leading slash = *"ignore where you're standing, start from the site's front
entrance instead."* Same distinction as relative vs. absolute file paths.

**Why it's not just trivia:** the links say `'dashboard'`, not `'/admin/dashboard'` — so if the whole
admin section ever moved from `/admin` to `/backoffice`, only the **one** route registration changes;
every sidebar link keeps working automatically since none of them hardcoded `/admin/`.

### `Record<K,T>` vs C# `Dictionary` vs C# `record` — with the comparison that made it click

```csharp
// C# Dictionary — a runtime data structure you instantiate and grow
var dict = new Dictionary<string, string>();
dict["status"] = "PaymentReceived";
```
```csharp
// C# record — a reference type with value-based equality, unrelated to dictionaries
public record Point(int X, int Y);
var p1 = new Point(1, 2);
var p2 = new Point(1, 2);
// p1 == p2 is true — compares values, not references
```
```typescript
// StatusMap = a name for a shape: any object with string keys, string values (a type alias, not a class)
type StatusMap = Record<string, string>;

// {} creates the actual object — ": StatusMap" is a compiler promise, not a constructor (no "new")
const params: StatusMap = {};

// const locks the BINDING only — params can never point to a different object
params['status'] = 'PaymentReceived';
// ↑ this is a MUTATION of the same object — legal.
// params = { status: 'x' } would be a REASSIGNMENT — ❌ blocked by const.

// at runtime none of this exists — no Record, no StatusMap, just a plain object: { status: 'PaymentReceived' }
```
**The verdict, comparing all three:**
- **Runtime behavior** ≈ C# `Dictionary` — both are a key→value bag you index into.
- **Everything else** ≠ — no `new`, no class; it's compile-time-only (see comments above).
- **Unrelated to C#'s `record`** — that's about value equality, not key→value lookup. Same word, two different concepts.

## Exercise

Without looking at the code:
1. Write the 4-route child table for `features/admin/routes.ts` (parent + 3 children + redirect).
2. Write the `/admin` entry in `app.routes.ts` (guards + lazy load).
3. Explain what would happen if `pathMatch:'full'` were removed.
4. Explain what a non-admin could reach if the guard sat on `dashboard` only.

## Solution

```typescript
// features/admin/routes.ts
export const adminRoutes: Routes = [
  {
    path: '',
    component: AdminComponent,
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: AdminDashboardComponent },
      { path: 'products', component: AdminProductsComponent },
      { path: 'orders', component: AdminOrdersComponent },
    ],
  },
];

// app.routes.ts
{ path: 'admin', canActivate: [authGuard, adminGuard], loadChildren: () => import('./features/admin/routes').then(m => m.adminRoutes) }
```
3. `/admin` → redirect to `dashboard` → `''` still matches `/admin/dashboard` (prefix) → redirect again → infinite loop.
4. `products` and `orders` would have no protection at all — any authenticated (or even unauthenticated, depending on where `authGuard` sits) user could reach them directly.

## Also asked (LET GO — one-liner is enough)

- **`httpResource` history** — released as `@experimental` in Angular 19.2, stable at 22 GA. Without it:
  `inject(HttpClient)` + `.get<T>(url).subscribe(...)` and hand-roll your own loading/error signals.
- **Why a separate `.html` + why `export`** — `templateUrl` keeps markup out of the decorator; `export`
  is what lets `routes.ts` `import { AdminDashboardComponent } from ...` to route to it.
- **`calc(100vh - 8rem)`** — `100vh` = 100% of the browser viewport height (not the page); subtracting
  `8rem` carves out room for the fixed header so the shell fills exactly the rest, no extra scrollbar.

## Tests

No dedicated unit tests — this is routing/layout wiring, not logic. Verified live instead (5/5):
adminGuard blocks a non-admin, `/admin` redirects to `dashboard`, child routes swap under the shell,
a deep-link (`/admin/orders`) survives a hard refresh, and the orders table + refund flow work end to end.

## Interview Q&A

**Why does putting the guard on the parent protect all its children?**
- `canActivate` runs when its own route segment is matched
- Every child's URL includes the parent segment, so the Router must activate the guarded parent first
- Not inheritance — it's that there's no path to a child without passing through the guarded parent

**What does `<router-outlet>` actually do?**
- It's a placeholder/marker, not a communication channel
- The Router matches the URL to a route, then inserts that route's component into the outlet's position
- Parent and child components have zero reference to each other

**Why does dropping `pathMatch:'full'` on an empty-path redirect cause an infinite loop?**
- Default `pathMatch:'prefix'` matches if the URL *starts with* the path
- An empty path is a prefix of every URL, so it re-matches after its own redirect target loads
- `'full'` requires the entire remaining URL to be empty, so it only fires once

**When would you use `mode="side"` vs `mode="over"` on `mat-sidenav`?**
- `side` — persistent navigation that should always be visible alongside content (admin panel, dashboard)
- `over` — temporary drawer that overlays content and should be dismissed (mobile menu)

**Two ways to use `routerLinkActive` — when do you need each?**
- CSS-class string (`routerLinkActive="active"`) — just want styling, no logic
- `#rla="routerLinkActive"` template variable — need the boolean (`rla.isActive`) to feed into something else, e.g. `[activated]` on a Material component

**Why does `navLinks` work with no interface declared?**
- TypeScript is structurally typed — a type is its shape, not a declared name
- The array literal's shape is inferred directly; matching structure is enough, no `interface` required
- Contrast with C#'s nominal typing, where two objects need a shared declared type to be interchangeable

**Why does `[routerLink]="'dashboard'"` navigate to `/admin/dashboard`, not `/dashboard`?**
- No leading slash = relative navigation, resolved against the route the link is declared in
- The link lives in `AdminComponent`'s template, whose own route is `/admin`, so it resolves relative to that
- A leading slash would make it absolute from the app root instead

**`Record<K,T>` vs C# `Dictionary` vs C# `record`?**
- `Record<K,T>` behaves like a `Dictionary` at runtime (key→value lookup) but is a compile-time type, not a class you instantiate
- At runtime a `Record<K,T>`-typed variable is just a plain JS object literal
- Unrelated to C#'s `record` (value-equality types) — same word, different concept

## Sources

- [Route • Angular](https://angular.dev/api/router/Route) — `pathMatch`
- [Control route access with guards • Angular](https://angular.dev/guide/routing/route-guards) — guard mechanism
- [Sidenav • Angular Material](https://material.angular.dev/components/sidenav) — `mat-sidenav` modes
- [RouterLinkActive • Angular](https://angular.dev/api/router/RouterLinkActive) — dual usage
- [RouterLink • Angular](https://angular.dev/api/router/RouterLink) / [Navigate to routes • Angular](https://angular.dev/guide/routing/navigate-to-routes) — relative path resolution
- [Reactive data fetching with httpResource • Angular](https://angular.dev/guide/http/http-resource) — `httpResource`
- [TypeScript: Utility Types](https://www.typescriptlang.org/docs/handbook/utility-types.html) — `Record<K,T>`
- [TypeScript: Type Compatibility](https://www.typescriptlang.org/docs/handbook/type-compatibility.html) — structural typing
