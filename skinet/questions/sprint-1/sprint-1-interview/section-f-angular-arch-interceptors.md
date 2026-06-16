## SECTION F — ANGULAR ARCHITECTURE, INTERCEPTORS & GUARDS

> **Source:** Frontend architecture docs — standalone/zoneless setup, functional interceptors, route guards, component input binding.

---

### EASY

**F1.** [HIGH] [OUT-OF-SCOPE] What is a standalone Angular component and what does it replace from the NgModule world?

<details>
<summary>The 5% Answer</summary>

A **standalone component** declares its own imports and providers — no NgModule required:

```typescript
@Component({
    selector: 'app-product-card',
    standalone: true,
    imports: [CurrencyPipe, RouterLink, NgIf],
    template: `...`
})
export class ProductCardComponent {}
```

| NgModule role | Standalone equivalent |
|---|---|
| Declaring components | Component is self-declared |
| Importing feature modules | Component `imports: []` array |
| Providing services (root) | `providedIn: 'root'` on the service |
| Bootstrap module | `bootstrapApplication(AppComponent, appConfig)` |
| Lazy feature module | `loadComponent(() => import('./comp'))` |
| Shared module | No equivalent needed — each component imports what it needs |

This project has no NgModules at all. `app.config.ts` holds all root providers via `provide*()` functions.

**Why it matters:** lazy loading now works at the **component level** — `loadComponent` downloads only that component's code on first navigation, not an entire module.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Standalone components are the same as modules with different syntax." Modules group components for shared imports/declarations. Standalone components are self-contained — each component controls its own dependencies. No grouping concept exists; imports are explicit per component.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `importProvidersFrom(SomeModule)` for library compatibility — it bridges legacy NgModule-based libraries (like `BrowserAnimationsModule`) into standalone apps. In Angular 18+, most built-in modules have `provide*()` equivalents (`provideAnimations()`, `provideHttpClient()`). The bridge is needed only for third-party libraries that haven't been updated.

</details>

---

**F2.** [HIGH] What is `ChangeDetectionStrategy.OnPush` and why is it mandatory for every component in this project?

<details>
<summary>The 5% Answer</summary>

**Default strategy:** Angular checks every component after every async event (click, HTTP response, timer). Even components unrelated to the change are re-evaluated.

**`OnPush`:** Angular re-checks a component only when:
1. An `@Input()` receives a new reference
2. An event originates inside the component
3. An Observable via `AsyncPipe` emits a new value
4. **A signal the component reads changes** ← the key trigger in this project
5. `ChangeDetectorRef.markForCheck()` is explicitly called

**Why mandatory in this project (zoneless mode):**
`provideZonelessChangeDetection()` removes Zone.js. Zone.js normally monkey-patches `setTimeout`, `Promise`, XHR, and triggers change detection after every async event. Without it, CD only runs when one of the 5 triggers above fires.

Without `OnPush` in zoneless mode: a component with the default strategy never gets re-checked automatically — it's frozen after initial render. Data updates but the view stays stale.

**With `OnPush` + signals:** Angular runs CD precisely on affected component trees when signals change. This is O(changed) vs O(all) — the architecture for scalable Angular apps.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"`OnPush` is optional for performance — just use it on slow components." In zoneless mode, it's not optional. Default strategy components literally never update without Zone.js. Omitting `OnPush` is a correctness bug, not a performance choice.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that migrating from zone-based to zoneless Angular exposes a long tail of existing `OnPush` violations — components that "worked" with Zone.js because the zone triggered CD incidentally are suddenly broken. Zoneless migration is as much a bug-hunt as an architecture change. Running `ng build --configuration zoneless` as a CI check surfaces these early.

</details>

---

### MEDIUM

**F3.** [HIGH] The interceptor order is: register `errorInterceptor` first, then `loadingInterceptor`, then `authInterceptor` last. Why does this specific order matter and what breaks if you reverse auth and error?

<details>
<summary>The 5% Answer</summary>

Angular interceptors form a chain where the **request path** follows registration order and the **response path** is reversed:

```
Request:  error → loading → auth → HTTP server
Response: error ← loading ← auth ← HTTP server
```

**Why `authInterceptor` last (closest to HTTP on request):**
It adds `withCredentials: true` to the outgoing request. Being last on the request path means it's applied as the final transform — nothing downstream can strip it. On the response path, it runs first — it sees the raw response before error handling.

**Why `errorInterceptor` first (registered first = runs last on responses):**
It must be the final interceptor to see errors so it can dispatch `router.navigate`, snackbar messages, and cart reloads as the last action — after loading spinner cleanup.

**If auth and error are reversed:**
- Response path: auth interceptor runs after error → `router.navigate('/login')` fires first, then auth tries to process a response that's already been redirected
- 401 on `/api/account/user-info` (expected for anonymous users) → error interceptor fires, navigates to login → auth interceptor runs after on a dead response → state corruption

**Observable chain:**
```typescript
// Each interceptor wraps the next:
authInterceptor → loadingInterceptor → errorInterceptor → HTTP
// Response travels back through:
HTTP → errorInterceptor → loadingInterceptor → authInterceptor
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Interceptor order doesn't matter for most cases." The response pipeline is always the reverse of the request pipeline. Getting this wrong causes subtle state bugs — the spinner stays active after a 401 redirect, or navigation fires before cleanup.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `HttpInterceptorFn` interceptors compose via the `next` function — you can trace the exact processing order by logging in each interceptor's request and response paths in dev. Middleware composition is explicit, not implicit — understanding the model eliminates guesswork about ordering.

</details>

---

**F4.** [HIGH] The `authGuard` checks `accountService.currentUser()` (a signal) before making an HTTP call. Why does this short-circuit matter in a multi-route app?

<details>
<summary>The 5% Answer</summary>

**Without the signal check (always making the HTTP call):**
Every navigation to a protected route fires `GET /api/account/auth-status`. On a page with multiple child routes each having `canActivate: [authGuard]`, multiple concurrent calls fire on a single navigation. A checkout flow with 4 steps, each guarded, would fire 4 auth requests before the first step renders.

**With the signal check:**
```typescript
const canActivate: CanActivateFn = (): Observable<boolean> | boolean => {
    const user = inject(AccountService).currentUser();

    if (user) return true;  // signal set → allow immediately, no HTTP

    return inject(AccountService).getAuthState().pipe(
        map(state => state.isAuthenticated || (router.navigate(['/login']), false))
    );
};
```

The signal is populated during app initialization before any route renders — it's authoritative for the current session. HTTP is only called when the signal is empty (fresh tab, session check needed).

**Failure scenario without short-circuit on slow networks:**
User navigates to `/checkout/step-2`. Guard fires HTTP, 300ms RTT. Another navigation fires before the first resolves. RxJS pipelines overlap. Angular cancels the first guard resolution and restarts — infinite redirect loop under poor conditions.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Always call the API to ensure accuracy — the signal might be stale." The signal is set from the server-authoritative `/user-info` response on startup. If the session expires mid-session, the next actual API call returns 401 and the error interceptor handles the redirect. The signal doesn't need constant revalidation.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `canMatch` as the modern alternative for some guard use cases — it runs before route matching and can prevent lazy module loading. `canActivate` runs after the route is matched and the component starts loading. For cases where you want to prevent code download for unauthorized users, `canMatch` is more appropriate than `canActivate`.

</details>

---

**F5.** [MEDIUM] `orderCompleteGuard` uses `orderService.orderComplete` — a plain boolean, not a signal. Describe where this causes a bug in a zoneless app and write the fix.

<details>
<summary>The 5% Answer</summary>

**Where it's fine (the guard itself):**
Guards run synchronously at navigation time — they read the value once and return. No reactivity needed.

**Where it breaks — template binding:**
```typescript
// In a component template
@if (orderService.orderComplete) { <div>Order confirmed!</div> }
```
In zoneless Angular, plain property mutations are invisible to change detection. When `orderComplete = true` is set after the order is created:
- No signal notification → Angular doesn't re-check this component
- The template stays showing the old value (`false`) — the confirmation never renders

**The fix — convert to a signal:**
```typescript
// In OrderService
orderComplete = signal(false);

// Set after successful order:
this.orderComplete.set(true);

// In guard
if (!this.orderService.orderComplete()) {
    this.router.navigate(['/shop']);
    return false;
}
return true;

// In template — reactive
@if (orderService.orderComplete()) { ... }
```

Signals notify Angular automatically when `.set()` is called, triggering re-render on any component that reads the signal in its template.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Call `ChangeDetectorRef.markForCheck()` after setting the boolean." This works but is fragile — every future place that sets `orderComplete` must also call `markForCheck`. Signals encode the reactivity contract in the type system — you cannot set a signal without notifying Angular.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `toSignal(observable$)` for bridging Observable state to signals. If `orderComplete` was derived from a SignalR event (Observable), `toSignal` wraps it in a signal that integrates with Angular's CD automatically — no `markForCheck` needed, no manual subscription management.

</details>

---

### HARD

**F6.** [HIGH] The app uses `withComponentInputBinding()`. What does this enable, what code does it eliminate, and what is the zoneless-specific benefit?

<details>
<summary>The 5% Answer</summary>

**Without `withComponentInputBinding()`:**
Route params require `ActivatedRoute` injection:
```typescript
constructor(private route: ActivatedRoute) {}

ngOnInit() {
    // Option 1: snapshot (misses param changes on same component)
    this.id = this.route.snapshot.paramMap.get('id')!;

    // Option 2: Observable subscription (requires cleanup)
    this.route.params.pipe(takeUntilDestroyed()).subscribe(p => this.id = p['id']);
}
```

**With `withComponentInputBinding()` (in `app.config.ts`):**
```typescript
@Input() id!: string;       // route param :id bound directly
@Input() search?: string;   // query param ?search= bound directly
```

Router uses Angular's input binding system — same mechanism as parent components passing values. No `ActivatedRoute` injection, no Observable, no subscription cleanup.

**Code eliminated:**
- `ActivatedRoute` import and injection
- `ngOnInit` with param subscription
- `takeUntilDestroyed()` for cleanup

**Zoneless-specific benefit:**
`ActivatedRoute.params` is a `Subject` (Observable). In zoneless mode, subscribing to it without `AsyncPipe` requires `markForCheck()` after each emission — easy to forget, causing silent view staleness. `@Input()` binding is synchronous and part of Angular's native input change detection — no subscription, no cleanup, automatic CD integration.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"You still need `ActivatedRoute` for query params and resolve data." With `withComponentInputBinding()`, all three — route params, query params, and resolver data — bind to `@Input()`. Query param `?search=` binds to `@Input() search?: string` directly.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning Angular 17.2+ `input()` signals — signal-based inputs that work with `withComponentInputBinding`:
```typescript
id = input<string>();           // signal, reads: this.id()
search = input<string>('');     // with default
```
Signal inputs are explicitly reactive — template expressions using `id()` re-evaluate automatically when the route param changes. This is the future-facing pattern; `@Input()` decorator still works but doesn't expose the value as a signal.

**Production consequence:** without `withComponentInputBinding()`, navigating from `/products/1` to `/products/2` when Angular reuses the component leaves `ActivatedRoute.snapshot.paramMap.get('id')` returning `1` — the component renders the wrong product without any error. Only Observable-based param subscription catches param changes mid-lifecycle, and missing `takeUntilDestroyed()` on that subscription leaks memory on every navigation.

</details>

---

**F7.** [HIGH] [OUT-OF-SCOPE] Your Angular app makes an authenticated request. `withCredentials: true` is set by the interceptor. The request still returns 401. Walk through all 3 possible causes and how to diagnose each.

<details>
<summary>The 5% Answer</summary>

**Cause 1 — CORS `AllowCredentials()` missing on the server:**
The server's CORS policy accepts the origin but doesn't include `AllowCredentials()`. The browser blocks the credentialed request before it reaches the server, or the server returns 401 because the cookie was stripped.

Diagnose: DevTools → Network → the OPTIONS preflight response. Check for `Access-Control-Allow-Credentials: true` header. If missing, fix CORS configuration.

**Cause 2 — Cookie expired, not set, or wrong domain:**
The Identity cookie doesn't exist in the browser, has expired, or is scoped to a different domain/path.

Diagnose: DevTools → Application → Cookies → check if the `.AspNetCore.Identity.Application` cookie exists, its expiry, and its `Domain` attribute. If missing, the user isn't logged in.

**Cause 3 — `SameSite` blocking cross-site cookie transmission:**
In production, if Angular (on `app.skinet.com`) calls the API (on `api.differentdomain.com`), they're cross-site. `SameSite=Strict` or `Lax` blocks the cookie.

Diagnose: DevTools → Network → the actual request (not preflight) → check if the `Cookie` header appears in the request. If absent, `SameSite` is blocking it. Fix: ensure both domains share the same eTLD+1 (`skinet.com`) or use `SameSite=None; Secure`.

**Note on localhost:** `localhost:4200` → `localhost:7096` is **same-site** — different ports, same hostname. `SameSite=Strict` does NOT block this in development.

**Production consequence:** `withCredentials: true` silently doing nothing (due to missing `AllowCredentials()`, wrong domain, or `SameSite` blocking) causes all authenticated API calls to return 401. The Angular app behaves as permanently logged-out for every user. No browser console error explains why the cookie isn't attached — only DevTools Network inspection of the request headers reveals the missing `Cookie` header.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Set `withCredentials: true` on each individual request instead of using the interceptor." Per-request is error-prone — a single missed call means that request behaves as unauthenticated. The interceptor is the global guarantee; individual overrides should only be used to opt-out, not opt-in.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `withCredentials: true` must be set on ALL requests — including unauthenticated ones like `GET /api/account/user-info`. Without it, the browser doesn't send the cookie even if one exists, so the auth check at app startup always returns "not authenticated" for logged-in users on page refresh.

</details>

---

**F8.** [HIGH] `adminGuard` checks `accountService.isAdmin()` — a `computed()` signal derived from `currentUser().roles`. The guard is registered as `canActivate: [adminGuard]` without `authGuard` first. Walk through the exact failure for an unauthenticated user, then for an authenticated non-admin, and write the correct guard chain.

<details>
<summary>The 5% Answer</summary>

**`isAdmin()` definition:**
```typescript
// AccountService
private _currentUser = signal<User | null>(null);
readonly currentUser = this._currentUser.asReadonly();
isAdmin = computed(() => this._currentUser()?.roles?.includes('Admin') ?? false);
```

**Failure 1 — unauthenticated user:**
`currentUser()` is `null` → `?.roles` is `undefined` → `?? false` → `isAdmin()` returns `false` → `adminGuard` redirects to `/account/login`.

This appears to work. But: `currentUser` is only populated **after** `InitService.init()` completes. If the user navigates directly to an admin URL before initialization (slow network, deep link), `currentUser()` is transiently `null` even for a logged-in admin. `adminGuard` fires early, redirects the admin to login — false negative.

**Failure 2 — authenticated non-admin:**
`currentUser()` is populated, `roles` doesn't include `'Admin'` → `isAdmin()` is `false` → redirect. This is correct behaviour, but without `authGuard` first, the admin check happens before the user identity is confirmed from the server — the guard is checking a cached signal, not a server-authoritative value.

**The correct chain:**
```typescript
// app.routes.ts
{
    path: 'admin',
    loadComponent: () => import('./features/admin/admin.component'),
    canActivate: [authGuard, adminGuard]   // authGuard FIRST
}
```

`authGuard` runs first:
1. If `currentUser()` is set → allow (signal check, no HTTP)
2. If not set → call `GET /api/account/auth-status` → populate signal → then `adminGuard` checks the now-authoritative signal

Without `authGuard` first, `adminGuard` may check a stale (null) signal and redirect a valid admin. The guards.md doc explicitly states: "`authGuard` must precede `adminGuard` — admin check assumes the user is loaded."

**Production consequence:** a logged-in admin who deep-links to `/admin` on a cold load gets bounced to the login page — a silent UX regression that's hard to reproduce locally (local state is usually warm).

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Just check `currentUser()` in `adminGuard` and call the API if null." Duplicating `authGuard`'s HTTP logic in `adminGuard` violates single responsibility — now two guards maintain separate HTTP call logic. Changes to auth-status endpoint path or shape must be updated in two places.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing that `canActivate` guard array in Angular runs guards **sequentially** — the next guard only runs if the previous returned `true`. This makes the chain `[authGuard, adminGuard]` a short-circuit: if `authGuard` redirects to login, `adminGuard` never runs. The guards form a pipeline, not parallel checks.

</details>

---

**F9.** [HIGH] A CI test for an Angular login form was failing because `form.value.email` typed as `string | null | undefined` couldn't be assigned to `string`. The form was built with `this.fb.group({ email: [''] })`. What does switching to `this.fb.nonNullable.group(...)` change about the type, and why does the `null` distinction matter in a zoneless app when form state drives a signal?

<details>
<summary>The 5% Answer</summary>

**Why `FormGroup.value` is `Partial<T>` by default:**

Standard `FormBuilder.group(...)` marks all controls as potentially nullable because any control can be disabled at runtime. TypeScript models this correctly:

```typescript
const form = this.fb.group({ email: [''], password: [''] });
// form.value type: { email?: string | null; password?: string | null }
// TypeScript error: string | null | undefined is not assignable to string
```

**What `fb.nonNullable.group(...)` does:**

```typescript
const form = this.fb.nonNullable.group({ email: [''], password: [''] });
// form.value type: { email: string; password: string }
// reset() restores initial value '' instead of setting null
```

`nonNullable` makes every control a `NonNullableFormControl` — `.value` returns the concrete type without `null | undefined`, and `.reset()` restores the initial value instead of setting `null`.

**Why null matters with signals in a zoneless app:**

```typescript
// Bug — null assertion hides the real type, signal receives null at runtime
email = signal('');
onSubmit() {
    this.email.set(this.form.value.email!);  // ! compiles away the error, doesn't fix it
}

// Correct — nonNullable ensures form.value.email is string
onSubmit() {
    this.email.set(this.form.value.email);   // clean assignment, no assertion needed
}
```

In zoneless Angular, `signal.set()` is what triggers change detection. A `null` that sneaks in via `!` doesn't cause a CD problem — it causes a data problem: `null` is sent to the API instead of validation failing. The CI failure surfaces a type contract violation that `!` was silently hiding.

**Production consequence:** `form.value.email!` with a disabled email control returns `null` (disabled controls return `undefined` from `.value`). The `!` coerces `undefined` to `never` at the type level but at runtime `undefined` is passed to `signal.set()` — the signal holds `undefined`, and `POST /api/account/login` sends `{ email: undefined }`.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Use `form.getRawValue()` — it always returns all fields." `getRawValue()` returns disabled fields (unlike `.value`) but its return type is still `T | null` without `nonNullable`. It solves the "disabled control is missing" problem, not the null-type problem. The TypeScript error persists.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing that `FormControl<string>` with an explicit generic (typed reactive forms, Angular 14+) is the per-control alternative: `new FormControl<string>('')` makes the value type exactly `string`. `fb.nonNullable.group(...)` applies this to an entire group at once without listing types individually — more concise for forms with many fields.

</details>

---

**F10.** [HIGH] Find all 3 bugs in this functional `authInterceptor` and write the corrected version:

```typescript
export const authInterceptor: HttpInterceptorFn = (req, next) => {
    const accountService = inject(AccountService);

    req.clone({ withCredentials: true });

    return next(req).pipe(
        catchError(err => {
            if (err.status === 401) {
                accountService.currentUser.set(null);
                inject(Router).navigate(['/account/login']);
            }
            return throwError(() => err);
        })
    );
};
```

<details>
<summary>The 5% Answer</summary>

**Bug 1 — `req.clone({...})` result is discarded:**
`HttpRequest.clone()` returns a **new immutable request** — it does not mutate `req`. The line evaluates a clone, throws it away, then `next(req)` forwards the **original unmodified request**. `withCredentials` is never applied — the browser drops the auth cookie on cross-origin requests, every authenticated call returns 401 forever.

**Bug 2 — `currentUser.set(null)` on a readonly signal:**
`AccountService.currentUser` is `readonly currentUser = this._currentUser.asReadonly()` (or a `computed()`). Readonly signals don't expose `.set()` — this is a TypeScript compile error. Even if it compiled, clearing auth state belongs behind a service method (`signOut()` / `clearCurrentUser()`) so cart reset, SignalR disconnect, and redirect happen atomically — not scattered across callers.

**Bug 3 — `inject(Router)` inside `catchError`:**
`inject()` must be called synchronously inside the **injection context** — the interceptor factory body runs once per request in that context, but the `catchError` callback runs **later**, after the HTTP response arrives. By that time the injection context is gone. Runtime throws:
```
NG0203: inject() must be called from an injection context
```
Every 401 error triggers a cryptic `NG0203` instead of a redirect. Dependencies must be captured at the top of the interceptor.

**Corrected version:**
```typescript
export const authInterceptor: HttpInterceptorFn = (req, next) => {
    const accountService = inject(AccountService);
    const router = inject(Router);

    const authReq = req.clone({ withCredentials: true });

    return next(authReq).pipe(
        catchError(err => {
            if (err.status === 401) {
                accountService.signOut();            // service method — clears signal + cart + SignalR
                router.navigate(['/account/login']);
            }
            return throwError(() => err);
        })
    );
};
```

**Production consequence:**
- Bug 1 alone: every logged-in user appears permanently logged out on cross-origin API calls — `/api/account/user-info` returns 401, `AccountService.currentUser` never populates, app behaves as anonymous for all sessions. Silent in dev (same-origin `localhost:4200` → `localhost:7096` sends cookies without `withCredentials`), catastrophic in prod.
- Bug 3: the `NG0203` crash on 401 masks the underlying expired-session UX — users see a console stack trace instead of a login redirect. Error interceptor may still catch the outer error, but the redirect path is dead.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Set `req.withCredentials = true` directly — saves the clone." `HttpRequest` is immutable by design; the `withCredentials` property has no public setter. Attempting direct assignment is a TypeScript error, and even if it weren't, mutating a request the caller still holds a reference to would break other interceptors' assumptions. `clone()` with the new options is the only correct pattern.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `runInInjectionContext(injector, () => inject(Router))` is the escape hatch for legitimate cases where `inject()` must be called outside the original context (e.g., a long-lived RxJS operator). The idiomatic fix in an interceptor is simpler: capture all dependencies at the top. Also: `next.handle` was the old `HttpHandler` API — the functional `HttpInterceptorFn` signature passes `next` as a function directly (`next(req)` not `next.handle(req)`), which is a common mis-port from legacy class-based interceptors.

</details>

---

## ⚡ Syntax Quick-Fire

**F-QF1.** [HIGH] Angular — what is the difference between `inject()` and constructor injection?

<details>
<summary>The 5% Answer</summary>

- **`inject()`** — works in any **injection context**: class constructors, field initializers, factory functions, `computed()`, `effect()`, functional guards, and functional interceptors. No class required.
- **Constructor injection** — only works inside a class constructor. Cannot be used in standalone functions or signal primitives.

```typescript
// Functional interceptor — no class, inject() required
export const authInterceptor: HttpInterceptorFn = (req, next) => {
    const accountService = inject(AccountService);
    return next(req.clone({ withCredentials: true }));
};
```

`inject()` is the idiomatic pattern in standalone Angular 17+ — it removes the need to declare every dependency in a constructor signature.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Constructor injection is safer because it's more explicit." Both are explicit — `inject()` is type-safe and tree-shakeable. The practical difference is context: functional interceptors and guards **cannot use** constructor injection because they are not classes.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing `inject()` must be called synchronously during construction — not inside a `setTimeout`, Promise callback, or `effect()` body. Calling it outside an injection context throws `NG0203: inject() must be called from an injection context`.

</details>

---

**F-QF2.** [HIGH] Angular — what does `@defer` do and when would you use it?

<details>
<summary>The 5% Answer</summary>

`@defer` splits template content into a **separate lazy chunk** that is only downloaded and rendered when a trigger fires:

```html
@defer (on viewport) {
    <app-product-reviews />    <!-- lazy chunk, downloaded on scroll -->
} @placeholder {
    <div class="skeleton" />   <!-- shown until trigger fires -->
}
```

**Triggers:** `on viewport` (intersection observer), `on interaction` (click/focus), `on idle`, `on timer(2s)`, `when condition`.

**Use for:** below-the-fold content (reviews, recommendations), heavy third-party components (charts, maps), anything that doesn't affect the initial LCP.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Using `@defer` on above-the-fold content for "performance." Deferring the primary content delays the LCP — the opposite of the intended effect. `@defer` only improves performance when the deferred content is not needed for first meaningful paint.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Combining `@defer (on viewport)` with `@placeholder` and `@loading` to give the user immediate visual feedback (skeleton screen) while the chunk downloads — eliminates cumulative layout shift (CLS) from content popping in after load.

</details>

---

**F-QF3.** [HIGH] Angular — `toSignal(observable$)` vs `AsyncPipe` — when is `toSignal` preferable?

<details>
<summary>The 5% Answer</summary>

- **`AsyncPipe`** — template-only, creates a **separate subscription per pipe instance**. Can't be used in `computed()` or `effect()`. Requires `| async` syntax in every template binding.
- **`toSignal(obs$)`** — creates one subscription, exposes the value as a **composable signal** usable in `computed()`, `effect()`, and template expressions. Cleans up automatically on component destroy.

**Prefer `toSignal` when:**
- The value is read in multiple template locations (one subscription vs many)
- The value feeds into `computed()` or `effect()`
- You want a synchronous initial value: `toSignal(obs$, { initialValue: [] })`

```typescript
products = toSignal(this.productService.getAll(), { initialValue: [] });
filteredCount = computed(() => this.products().filter(p => p.price < 10).length);
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Using `AsyncPipe` for a value also needed inside `computed()` — `AsyncPipe` is template-only; you'd need a separate subscription in the component class, creating two subscriptions to the same Observable. `toSignal` unifies them.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing `toSignal` requires an injection context (called at field initialization time, not inside a method). For late-created signals, use `runInInjectionContext(injector, () => toSignal(...))` or restructure so the signal is created at construction time.

</details>
