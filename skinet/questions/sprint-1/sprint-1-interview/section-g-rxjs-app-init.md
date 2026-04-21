## SECTION G — RXJS & APP INITIALIZATION

> **Source:** Task 4 — `forkJoin` failure replaced with sequential `switchMap` chain; RxJS operators: `switchMap`, `tap`, `catchError`, `of`, `forkJoin`.

---

### EASY

**G1.** [HIGH] [OUT-OF-SCOPE] What is the difference between `map`, `switchMap`, and `mergeMap`? When does using `map` instead of `switchMap` cause `Observable<Observable<T>>`?

<details>
<summary>The 5% Answer</summary>

- **`map(fn)`** — transforms each value synchronously: `Observable<A>` → `Observable<B>`. If `fn` returns an Observable, you get `Observable<Observable<B>>` — the inner Observable is never subscribed to or flattened.
- **`switchMap(fn)`** — `fn` returns an Observable; `switchMap` subscribes and flattens it. Cancels the previous inner Observable when a new value arrives: `Observable<A>` → `Observable<B>`.
- **`mergeMap(fn)`** — same flattening as `switchMap` but doesn't cancel previous inner subscriptions — all run concurrently.

**The mistake:**
```typescript
// WRONG — fn returns Observable → result is Observable<Observable<User>>
this.route.params.pipe(
    map(params => this.userService.getUser(params['id']))  // getUser returns Observable
)

// CORRECT — flattens to Observable<User>
this.route.params.pipe(
    switchMap(params => this.userService.getUser(params['id']))
)
```

**When to pick which:**
- User typing in a search box → `switchMap` (cancel the previous search when a new character is typed)
- Multiple parallel file uploads → `mergeMap` (run all concurrently, don't cancel)
- Sequential queue (order matters, can't cancel) → `concatMap`

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Use `mergeMap` everywhere — it handles async." `mergeMap` doesn't cancel. For a search box, every keystroke fires a request and all run in parallel — responses arrive out of order, the last displayed result might not be the most recent query.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `concatMap` as the fourth variant — processes inner Observables sequentially (one completes before the next starts). Used when order matters and you can't cancel (e.g., audit log entries that must be written in order, or sequential API calls where each depends on the previous result).

</details>

---

**G2.** [HIGH] Inside the app initializer's inner `catchError` on `cartService.getCartAsync(cartId)`, the code calls `localStorage.removeItem('cart_id')` before returning `of({ user, cart: null })`. What specific failure does this cleanup prevent for a user who reopens the tab after their Redis cart session has expired?

<details>
<summary>The 5% Answer</summary>

**Without the cleanup — the permanent-404 loop:**

1. User adds items → server generates `cart_id = "abc123"`, stored in both Redis and `localStorage`.
2. Redis TTL expires (default 30 days in this project) → Redis deletes the entry.
3. A month later the user returns → `localStorage.getItem('cart_id')` still returns `"abc123"`.
4. Init service calls `cartService.getCartAsync("abc123")` → Redis miss → controller returns 404 → inner `catchError` fires → init continues with `cart: null`.
5. **Without the `removeItem` line:** `localStorage` still holds `"abc123"`. Every subsequent page reload repeats steps 3–4 — a guaranteed 404 on every `init` for the rest of the browser's lifetime.

**With the removal line:**
The next load reads `cartId = null`. The outer `switchMap` short-circuits (`if (!user || !cartId) return of(null)`) — no stale ID fetch. When the user adds their first item again, a fresh `cart_id` is generated and persisted.

**Why it lives in the inner `catchError`, not the outer one:**
The outer `catchError` handles **auth** failures (401 = not logged in). Wiping the cart ID there would log out users whose session is actually fine. The inner `catchError` only fires for cart-fetch failures — exactly the invariant "cart ID exists but the server has no cart for it" — which is the only case where removing the ID is correct.

**Production consequence:** without the cleanup, every returning user past the Redis TTL generates a guaranteed 404 on every app load. The error is silent to the user (swallowed by the inner catch), but DevTools Network shows a 404 on every page load and Application Insights accumulates ghost error events from legitimate sessions — noise that drowns out real cart failures worth investigating.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Clear it in a global `catchError` at the end of the chain." That clears the cart ID on **any** init failure — transient network errors, a 503 from a bad deploy, anything. The invariant "cart ID exists but Redis lost it" should be the only trigger. A global wipe destroys valid cart IDs during brief outages and loses the user's cart permanently.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that a server-driven invalidation (SignalR `cart-expired` event, or a `410 Gone` status code from the cart endpoint) would be cleaner than inferring "missing cart" from a generic 404. The current solution is pragmatic for a cookie-auth app without server-push cart invalidation — but it assumes `404 ≡ cart-gone`, which a misconfigured API path could falsely trigger.

</details>

---

### MEDIUM

**G3.** [HIGH] In the app initializer, `forkJoin([getUserInfo(), getCart()])` was replaced with a sequential `switchMap` chain. Trace the exact failure scenario that required this change.

<details>
<summary>The 5% Answer</summary>

**The failure with `forkJoin`:**
`CartController` has `[Authorize]`. An unauthenticated user returns 401 from `GET /api/cart/{id}`.

`forkJoin` subscribes to ALL sources simultaneously and errors if ANY source errors:
```typescript
forkJoin([
    this.accountService.getUserInfo(),    // unauthenticated → returns null (safe)
    this.cartService.getCartAsync(cartId) // unauthenticated → 401 → ERROR
])
// forkJoin propagates the 401 error → subscribe's error handler fires
// APP_INITIALIZER rejects → Angular never renders → blank page for all anonymous users
```

**Why `getUserInfo()` is safe but `getCart()` isn't:**
`/api/account/user-info` returns `204 No Content` for unauthenticated users — not a 401. It's designed to be polled without auth. `CartController` is `[Authorize]` — any anonymous request gets 401.

**The fix — sequential: auth first, cart only if authenticated:**
```typescript
getUserInfo().pipe(
    catchError(err => err.status === 401 ? of(null) : throwError(err)),
    switchMap(user => {
        if (!user || !cartId) return of({ user: null, cart: null });
        return getCartAsync(cartId).pipe(
            catchError(() => of({ user, cart: null }))
        );
    })
)
```

**Production consequence:** `forkJoin` breaks the app for every user who isn't logged in — that's most users on first visit.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Add `catchError` to `forkJoin` to handle the 401." `forkJoin` with a top-level `catchError` aborts both observables — you can't recover partial results. When the cart observable errors, `forkJoin` cancels the user observable too, even if it had already succeeded. The problem requires sequential logic, not just error recovery.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `combineLatest` as a parallel alternative for non-conditional streams — it emits when any source changes and doesn't abort on individual errors (with per-source `catchError`). But for this case, cart fetching is *conditional* on auth — `switchMap`'s conditional inner Observable is the correct model. `combineLatest` can't express "only run if the previous value was truthy."

</details>

---

**G4.** [HIGH] What does `tap(user => { if (user) signalrService.createHubConnection(); })` do and why is `tap` the correct operator here instead of `map`?

<details>
<summary>The 5% Answer</summary>

**What it does:**
`tap` executes a side effect for each emitted value without changing the value. The `user` object passes through unchanged. `createHubConnection()` is called as a side effect when `user` is non-null.

**Why `tap` not `map`:**
- `map` semantically communicates "I'm transforming this value." A reader who sees `map` expects the output type or value to differ from the input.
- `tap` semantically communicates "side effect here, value unchanged." Reading `tap` instantly tells you: the stream continues with the same value; this is just doing something else.

**Why not in `subscribe`:**
The SignalR connection must be established **before** the cart fetch begins. If done in `subscribe`, both the `switchMap` (cart fetch) and the SignalR connection would race to start. With `tap` mid-chain, the order is deterministic:
1. `getUserInfo()` completes
2. `tap` — if user exists, connect SignalR
3. `switchMap` — if user exists, fetch cart

If SignalR connected after the cart fetch, the success page might show before the hub connection is established, missing order update notifications.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Use `map` — it returns the same value if you don't transform." Technically correct but semantically wrong. Future readers would expect a transformation. Code should communicate intent clearly, not just function correctly.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `tap({next: ..., error: ..., complete: ...})` — `tap` accepts a full observer, not just a `next` callback. This is useful for logging both success and error paths:
```typescript
.pipe(
    tap({
        next: user => logger.log('User loaded', user?.email),
        error: err => logger.warn('Auth check failed', err.status)
    }),
    catchError(...)
)
```
The error `tap` runs before `catchError` handles it — useful for observability without consuming the error.

</details>

---

**G5.** [HIGH] Why does `catchError(err => err.status === 401 ? of(null) : throwError(() => err))` use `of(null)` for 401 but `throwError` for other errors? What happens if `throwError` is used for all cases?

<details>
<summary>The 5% Answer</summary>

**`of(null)`** creates an Observable that immediately emits `null` and completes. It converts the 401 error into a successful emission — treating "not authenticated" as a valid state (null user) rather than an error.

**`throwError(() => err)`** re-throws the error, propagating it down the error path of the Observable chain. The downstream subscriber's error handler fires.

**If `throwError` is used for all cases (including 401):**
1. Anonymous user visits the app
2. `getUserInfo()` → 401 (expected for unauthenticated users)
3. `catchError` re-throws → error propagates through the chain
4. `switchMap` never runs → cart is never fetched
5. `APP_INITIALIZER` receives an error → Angular rejects initialization
6. Angular never renders → blank page for all anonymous users

**The discrimination is critical:**
```typescript
// 401 = "not logged in" — expected, recoverable
err.status === 401 ? of(null) : throwError(() => err)
// Other errors (503, network timeout) = unexpected — rethrow for global handling
```

A 503 from the auth service means the server is down — that's not recoverable by returning null user. Rethrowing lets a global error handler show a "service unavailable" page.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"`catchError` should always recover by returning `of(null)`." Only recover from errors that are expected and semantically recoverable. Returning `of(null)` for a 503 hides that the service is down — the app appears to work but no data loads.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `EMPTY` (an Observable that completes immediately without emitting) as an alternative when you want to stop the stream without a value. `of(null)` emits null — downstream receives null and must handle it. `EMPTY` just completes — downstream receives nothing. For the initializer, `of(null)` is correct because `switchMap` needs the null value to decide not to fetch the cart. `EMPTY` would skip `switchMap` entirely.

</details>

---

### HARD

**G6.** [HIGH] Design the complete app initializer: loads user, conditionally loads cart, connects SignalR if authenticated. Write the full RxJS chain and justify each operator choice.

<details>
<summary>The 5% Answer</summary>

```typescript
// InitService.init()
init(): Observable<void> {
    const cartId = localStorage.getItem('cart_id');

    return this.accountService.getUserInfo().pipe(
        // 1. 401 = anonymous (expected) → null. Other errors → rethrow.
        catchError(err =>
            err.status === 401 ? of(null) : throwError(() => err)
        ),

        // 2. Side effect: connect SignalR only if authenticated (doesn't change the value)
        tap(user => {
            if (user) this.signalrService.createHubConnection(user);
        }),

        // 3. Conditional cart fetch: only if authenticated AND cart ID exists
        switchMap(user => {
            if (!user || !cartId) return of(null);   // unauthenticated or no cart → skip
            return this.cartService.getCartAsync(cartId).pipe(
                // 4. Cart failure is non-fatal (expired Redis session, Redis down)
                catchError(() => {
                    localStorage.removeItem('cart_id');  // clean up invalid ID
                    return of(null);
                })
            );
        }),

        // 5. Map to void — caller doesn't need the cart/user values
        map(() => void 0)
    );
}
```

**Operator justification:**
| Operator | Why |
|---|---|
| `catchError` (outer) | Distinguishes recoverable 401 from fatal errors |
| `tap` | Side effect (SignalR) without disrupting the value stream |
| `switchMap` | Async conditional chain; `of(null)` short-circuits synchronously |
| `catchError` (inner) | Cart failure is non-fatal — app boots without cart |
| `map(() => void 0)` | `APP_INITIALIZER` expects `Observable<void>` — clean interface |

**In `app.config.ts`:**
```typescript
{
    provide: APP_INITIALIZER,
    useFactory: (init: InitService) => () =>
        firstValueFrom(init.init()),  // convert Observable to Promise for APP_INITIALIZER
    deps: [InitService],
    multi: true
}
```
`firstValueFrom` (replace deprecated `.toPromise()`) takes the first emission, converts to Promise. Since `init()` completes after one emission, this is correct.

**Production consequence:** an app initializer without `catchError` on the cart fetch crashes `APP_INITIALIZER` when Redis is unavailable — the entire Angular app refuses to boot for every user, even unauthenticated ones who don't need the cart. A missing `map(() => void 0)` at the end causes the initializer to emit a typed value that `APP_INITIALIZER` (which expects `void`) may not handle consistently across Angular versions.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Use `forkJoin` for parallel loading — it's faster." Parallel loading is correct for independent operations. The cart fetch is conditional on the user being authenticated — `forkJoin` can't express conditional dependencies. The performance gain is milliseconds; the correctness gain from sequential loading is critical.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `APP_INITIALIZER` blocks Angular's first render until the Promise resolves. For performance, consider a progressive approach: show the app shell immediately, load auth state in the background, and let guards handle access control reactively. `APP_INITIALIZER` is appropriate when data is required for first render (like a feature flag deciding which routes to register), but can be replaced by reactive patterns for better perceived performance.

</details>

---

**G7.** [HIGH] [OUT-OF-SCOPE] Your Angular app uses `provideZonelessChangeDetection()`. A third-party library updates state in a `setTimeout` callback. The UI doesn't update. Explain the mechanism and list all valid fixes in priority order.

<details>
<summary>The 5% Answer</summary>

**Why it breaks:**
Zone.js monkey-patches `setTimeout` — when it fires, Zone.js notifies Angular to run CD. In zoneless mode, `setTimeout` is native. The callback runs, updates state, but Angular has no notification. `ChangeDetectorRef.markForCheck()` is never called → component tree doesn't re-render.

**Fixes in priority order:**

**1. Convert to signals (best — zero boilerplate):**
```typescript
private data = signal<Data | null>(null);
// In library callback:
this.libraryInstance.onUpdate(newData => this.data.set(newData));
// Template: {{ data() }} — updates automatically
```

**2. Wrap in `NgZone.run()` (compatibility bridge):**
```typescript
constructor(private ngZone: NgZone) {}
this.libraryInstance.onUpdate(data =>
    this.ngZone.run(() => this.data = data)  // re-enters zone context
);
```
Forces CD for just this callback. Doesn't require refactoring the rest.

**3. `ChangeDetectorRef.markForCheck()`:**
```typescript
constructor(private cdr: ChangeDetectorRef) {}
this.libraryInstance.onUpdate(data => {
    this.data = data;
    this.cdr.markForCheck();
});
```
Works but fragile — every future update site must also call `markForCheck`.

**4. Wrap in RxJS + `AsyncPipe`:**
```typescript
data$ = new Subject<Data>();
this.libraryInstance.onUpdate(data => this.data$.next(data));
// Template: {{ data$ | async }} — AsyncPipe calls markForCheck internally
```

**Priority reasoning:** signals > NgZone.run > markForCheck. `NgZone.run` reintroduces zone dependency for this specific callback. Signals are the future-facing, self-documenting approach.

**Production consequence:** a third-party map or chart library that updates state via `setTimeout` in a zoneless app produces views that silently freeze after initial render — the component throws no errors and looks healthy, but live data stops updating. The bug is invisible in unit tests that mock the library and only appears when the real integration is exercised in a browser.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Remove `OnPush` from components using that library." Reverts to default strategy — defeats the purpose of zoneless, creates performance regression, and hides the root cause. Future changes to the component will be slower by default.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `afterNextRender()` and `afterRender()` as zoneless-native lifecycle hooks for post-render DOM work. They run inside Angular's rendering cycle — state mutations inside them trigger CD. For one-time setup (attaching a third-party chart library), `afterNextRender` is the correct hook in zoneless apps.

</details>

---

**G8.** [HIGH] `switchMap` cancels the previous inner Observable when a new value arrives. In the app initializer this is safe — but in a checkout flow, the same cancellation causes a subtle data loss bug. Describe the scenario and the fix.

<details>
<summary>The 5% Answer</summary>

**In the initializer: safe.**
`getUserInfo()` emits exactly once and completes. There's nothing to cancel — `switchMap` has no prior inner Observable.

**In a checkout flow: potential bug.**

Suppose the checkout steps are driven by a stepper Observable:
```typescript
this.stepperIndex$.pipe(
    switchMap(step => {
        if (step === 2) return this.paymentService.createPaymentIntent(this.cart());
        return EMPTY;
    })
)
```

If the user navigates forward to step 3 before `createPaymentIntent` resolves:
- `stepperIndex$` emits `3`
- `switchMap` cancels `createPaymentIntent` request
- The PaymentIntent creation is aborted mid-flight (HTTP abort sent)
- But Stripe may have already processed it on the server
- Result: a PaymentIntent exists in Stripe with no corresponding client state — orphaned

**The fix — don't use `switchMap` for payment operations:**
```typescript
// Use exhaustMap — ignores new values while inner Observable is in progress
this.stepperIndex$.pipe(
    exhaustMap(step => {
        if (step === 2) return this.paymentService.createPaymentIntent(this.cart());
        return EMPTY;
    })
)
```
`exhaustMap` ignores navigation events while the payment intent creation is in flight — the user can't trigger a new one until the first completes. No orphaned intents, no cancelled requests.

Or: make the payment intent creation **outside** the reactive stream — triggered once by user action, not by stepper events.

**Production consequence:** `switchMap` on a payment operation sends an HTTP abort to the Angular client, but Stripe may have already processed the request. The result is an orphaned `PaymentIntent` in Stripe — potentially charged to the customer's card with no corresponding order created. The customer sees a charge; the merchant sees no order. Reconciliation requires manual Stripe dashboard investigation per incident.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"`switchMap` always finishes the current request before starting the next." Wrong — `switchMap` sends an HTTP abort immediately on new emission. The server may still process the aborted request. "Cancel" in RxJS means "stop waiting for the response" — the server doesn't know.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning the four flattening operators as a decision matrix:
- `switchMap` — cancel previous, start new (search, navigation)
- `mergeMap` — run all concurrently (parallel operations)
- `concatMap` — queue sequentially (ordered operations)
- `exhaustMap` — ignore new while busy (button click guards, payment operations)

The right choice depends entirely on what should happen when a new value arrives before the previous inner Observable completes. Defaulting to `switchMap` is the most common source of subtle RxJS bugs.

</details>

---

**G9.** [HIGH] [OUT-OF-SCOPE] Angular 19+ introduced `httpResource()`. Show how it would replace the `getUserInfo()` call in the app initializer and explain why `APP_INITIALIZER + switchMap` is still the right pattern for this project despite `httpResource()` being available.

<details>
<summary>The 5% Answer</summary>

**`httpResource()` for `getUserInfo`:**
```typescript
// AccountService — Angular 19+
userResource = httpResource<User | null>(() => ({
    url: '/api/account/user-info',
    method: 'GET'
}));

// Template or computed:
currentUser = computed(() => this.userResource.value() ?? null);
isAdmin = computed(() => this.currentUser()?.roles?.includes('Admin') ?? false);
isLoading = this.userResource.isLoading;
hasError = this.userResource.error;
```

`httpResource()` returns a signal with `.value()`, `.isLoading()`, `.error()`, and `.status()`. No `subscribe`, no `catchError`, no `tap` — the HTTP call, loading state, and error state are encoded in the signal type.

**Why `APP_INITIALIZER + switchMap` is still correct for this project:**
`httpResource()` resolves **lazily, per-component lifecycle** — the HTTP call fires when the component that owns the resource is created, not at app bootstrap. `APP_INITIALIZER` blocks Angular's first render **globally** until auth + cart are loaded.

Without `APP_INITIALIZER`, the auth guard fires before `userResource.value()` is populated — every guard check on cold load would trigger an HTTP round-trip, and guarded routes would briefly flash before redirecting. The initializer ensures all state is available before any route renders.

**Where `httpResource()` replaces the pattern:** non-blocking data that can load after render — product lists, order history, search results. Not for the authentication bootstrap that must be resolved before routing.

**Production consequence:** swapping `APP_INITIALIZER` for `httpResource()` in auth bootstrap breaks cold loads — the first render fires before the resource resolves, guards read `undefined`, and authenticated users see the login page for a split second before the resource populates and a second navigation fires.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Replace `APP_INITIALIZER` with `httpResource()` everywhere — it's simpler." `httpResource()` loads on component creation. Removing `APP_INITIALIZER` means guards check a null signal on cold load → authenticated users get bounced to login → must add loading states to every guarded component. The complexity moves from one initializer to every guarded route.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing `linkedSignal()` (Angular 19+) as the companion to `httpResource()` — it creates a **writable signal** derived from another signal with a reset-on-change behaviour. Useful for the cart service: `linkedSignal(() => cartResource.value())` gives a local mutable cart signal that resets when the server state refreshes, without manual `tap` and `.set()` calls.

</details>

---

## ⚡ Syntax Quick-Fire

**G-QF1.** [HIGH] `of(null)` vs `EMPTY` in RxJS — what is the difference?

<details>
<summary>The 5% Answer</summary>

- **`of(null)`** — emits `null` once, then completes. Downstream operators receive the `null` value.
- **`EMPTY`** — completes immediately without emitting anything. Downstream operators receive no value at all.

```typescript
// of(null) — switchMap receives null, can branch on it
getUserInfo().pipe(
    catchError(() => of(null)),
    switchMap(user => {
        if (!user) return of(null);   // user is null — skip cart fetch
        return getCart();
    })
)

// EMPTY — switchMap is never invoked, chain terminates silently
getUserInfo().pipe(
    catchError(() => EMPTY),
    switchMap(user => getCart())  // never runs
)
```

**In the app initializer:** `of(null)` is correct — `switchMap` needs the null value to decide not to fetch the cart.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Using `EMPTY` as a "safe" error recovery in the app initializer's `catchError`. The chain terminates without emitting — `switchMap` never runs, cart is never fetched, `APP_INITIALIZER` completes without loading any state. The app boots but silently skips initialization.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing `NEVER` as a third option — emits nothing and never completes. Use when you need to block a pipeline indefinitely (e.g., a timeout race where you want one branch to simply not resolve). Distinct from `EMPTY` which completes immediately.

</details>

---

**G-QF2.** [HIGH] Angular — `takeUntilDestroyed()` vs `takeUntil(destroy$)` — what does `takeUntilDestroyed()` eliminate?

<details>
<summary>The 5% Answer</summary>

`takeUntilDestroyed()` eliminates the entire manual cleanup boilerplate:

```typescript
// Old pattern — 4 pieces of infrastructure per component
private destroy$ = new Subject<void>();
ngOnDestroy() { this.destroy$.next(); this.destroy$.complete(); }
obs$.pipe(takeUntil(this.destroy$)).subscribe(...);

// Angular 16+ — zero boilerplate
obs$.pipe(takeUntilDestroyed()).subscribe(...);
```

It uses `DestroyRef` from the injection context and completes the stream automatically when the component, directive, or service is destroyed. Must be called in an injection context (field initializer or constructor).

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Forgetting `takeUntilDestroyed()` on long-lived Observables (WebSocket, interval, route events). The subscription outlives the component — callbacks fire on a destroyed component, causing "expression changed after checked" errors, memory leaks, and ghost state mutations.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Using `takeUntilDestroyed(this.destroyRef)` when calling from inside a method rather than a field initializer — pass the `DestroyRef` explicitly (injected in the constructor) to escape the injection context requirement. Works anywhere in the class lifecycle.

</details>

---

**G-QF3.** [HIGH] `firstValueFrom` vs `lastValueFrom` — when does the choice actually matter?

<details>
<summary>The 5% Answer</summary>

- **`firstValueFrom(obs$)`** — resolves with the **first emitted value**, then unsubscribes. Safe for any Observable.
- **`lastValueFrom(obs$)`** — resolves with the **last emitted value** after the Observable **completes**. On an infinite or non-completing Observable, it never resolves — the Promise hangs forever.

**When the choice matters:**
- HTTP calls and the app initializer (emit once and complete) → equivalent, either works
- A `BehaviorSubject` or `interval()` that never completes → use `firstValueFrom` — `lastValueFrom` hangs

```typescript
// APP_INITIALIZER — safe: init() completes after one emission
firstValueFrom(initService.init())

// BehaviorSubject — lastValueFrom hangs forever; firstValueFrom is correct
firstValueFrom(accountService.currentUser$)
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Using `.toPromise()` — deprecated since RxJS 7. `toPromise()` behaves like `lastValueFrom` (waits for completion), but resolves to `undefined` if the Observable completes without emitting — a silent empty result with no error. `firstValueFrom` throws `EmptyError` instead, making the failure observable.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing both throw `EmptyError` if the Observable completes without emitting. For the initializer, add a fallback: `firstValueFrom(init().pipe(defaultIfEmpty(undefined)))` — ensures `APP_INITIALIZER` never rejects due to an unexpectedly empty stream.

</details>
