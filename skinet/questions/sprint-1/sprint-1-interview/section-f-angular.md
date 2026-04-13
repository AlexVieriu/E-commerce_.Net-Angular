## SECTION F — ANGULAR FUNDAMENTALS

### EASY

**F1.** What is the difference between `ngOnInit` and the constructor in an Angular component?

<details><summary>Answer</summary>

- **Constructor** — called by the JS engine; used for DI injection only — keep it lightweight.
- **`ngOnInit`** — called by Angular after `@Input()` values are set; use for HTTP calls, subscription setup, complex initialization.

Key: `@Input()` values are `undefined` in the constructor — they're only available from `ngOnInit` onwards.
</details>

---

**F2.** What is dependency injection in Angular? How do you inject a service into a component?

<details><summary>Answer</summary>

- Angular creates and provides service instances to components that need them.
- **Constructor injection**: `constructor(private productService: ProductService)` — Angular's injector resolves and injects the instance.
- **`inject()` function** (Angular 14+): functional alternative; works in constructors, field initializers, functional guards/interceptors.
</details>

---

**F3.** What does `providedIn: 'root'` do on a service? What's the alternative?

<details><summary>Answer</summary>

- **`providedIn: 'root'`** — singleton in the root injector; available app-wide; tree-shakable (removed from bundle if nothing injects it). Preferred for most services.
- **`providers: []` on a component** — new instance per component; lives and dies with that component.
- **`providers: []` on a module** — one instance per module injector; breaks tree-shaking.
</details>

---

**F4.** What is the difference between `*ngIf` and `@if` (new control flow syntax)?

<details><summary>Answer</summary>

- **`*ngIf`** (Angular <17) — structural directive, requires `CommonModule` import, no native `@else if`.
- **`@if`** (Angular 17+) — built-in template syntax, no import needed, full TypeScript type narrowing, native `@else if`.

`@if` is more readable and compiles to faster output (no directive overhead).
</details>

---

**F5.** What does the `async` pipe do? Why is it preferred over manually subscribing in the component?

<details><summary>Answer</summary>

`async` pipe subscribes to an Observable/Promise in the template and auto-unsubscribes on component destroy. Preferred because:
1. No memory leaks (auto-cleanup).
2. Works with `OnPush` change detection.
3. Less boilerplate (no manual subscribe/unsubscribe).
4. Template stays reactive.

Pattern: `products$ | async` in template instead of `subscribe()` in component.
</details>

---

### MEDIUM

**F6.** Explain Angular's change detection. What triggers it? What is `OnPush` change detection? When does `OnPush` NOT trigger change detection?

<details><summary>Answer</summary>

Default: Angular checks every component top-down on any async event (click, HTTP, setTimeout).

OnPush only runs change detection when:
1. `@Input()` reference changes (not mutation!).
2. DOM event originates in the component.
3. `async` pipe emits.
4. Manually via `ChangeDetectorRef.markForCheck()`.

OnPush SKIPS when: parent mutates an input object without creating a new reference, or external observable emits without `async` pipe.
</details>

---

**F7.** What is `ViewEncapsulation` in Angular? What are the three modes? Why does Angular Material use `ViewEncapsulation.None`?

<details><summary>Answer</summary>

**Emulated** (default): Angular rewrites selectors with `_ngcontent-xxx` attributes — styles are scoped but CSS-only (no true shadow DOM).
**ShadowDom:** uses native Shadow DOM — true encapsulation, styles can't leak in or out.
**None:** styles are global — no scoping at all.

Angular Material uses `None` because its components need global theme styles (typography, colors, density) to cascade in from the app's theme. Scoping would block theme customization.
</details>

---

**F8.** What is the difference between `template-driven` forms and `reactive` forms? When would you choose each? Which is easier to test?

<details><summary>Answer</summary>

**Template-driven:** defined in HTML with `ngModel`, two-way binding, simpler for small forms (login, search). Async by nature (directives).

**Reactive:** defined in TypeScript with `FormGroup`/`FormControl`, explicit, synchronous, powerful validation and dynamic fields.

Easier to test: reactive forms (no template rendering needed — test the `FormGroup` directly).

Choose template-driven for simple forms, reactive for complex forms (dynamic fields, cross-field validation, arrays).
</details>

---

**F9.** Explain Angular routing: what is a `RouterOutlet`? What is lazy loading? How do you lazy-load a standalone component? What is a route guard?

<details><summary>Answer</summary>

- **`RouterOutlet`** — placeholder directive where Angular renders the matched route's component.
- **Lazy loading** — `loadComponent: () => import('./product.component').then(m => m.ProductComponent)`; downloads the component only when the route is visited, reducing initial bundle size.
- **Route guards** — `CanActivate` (auth check), `CanDeactivate` (unsaved changes warning), `Resolve` (pre-fetch data before render). In Angular 15+, guards can be plain functions via `CanActivateFn`.
</details>

---

**F10.** What does this code do? Is there a memory leak?

```typescript
@Component({...})
export class ProductListComponent implements OnInit {
  products: Product[] = [];

  constructor(private productService: ProductService) {}

  ngOnInit() {
    this.productService.getAll().subscribe(p => this.products = p);
  }
}
```

<details><summary>Answer</summary>

It fetches products on init and assigns them. Memory leak? **It depends:**
- **HTTP Observable** (`HttpClient`) — no leak; it emits once and auto-completes.
- **Long-lived observable** (WebSocket, interval, `Subject`) — yes, the subscription lives after the component is destroyed.

Best practice: always use `async` pipe, `takeUntilDestroyed()`, or manual unsubscribe in `ngOnDestroy`.
</details>

---

**F11.** What is an Angular interceptor? Give three real-world use cases. How do you register one with the new `provideHttpClient(withInterceptors([...]))` syntax?

<details><summary>Answer</summary>

Interceptor is middleware that processes every HTTP request/response. Use cases:
1. Attach auth token to every request (`Authorization: Bearer ...`).
2. Global error handling (catch 401 → redirect to login).
3. Request/response logging or loading spinner.

Register with `withInterceptors`:
```typescript
// app.config.ts
provideHttpClient(withInterceptors([authInterceptor, loggingInterceptor]))

// interceptor
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const newReq = req.clone({ setHeaders: { Authorization: 'Bearer ...' } });
  return next(newReq);
};
```
</details>

---

### HARD

**F12.** Explain RxJS operators: `switchMap`, `mergeMap`, `concatMap`, `exhaustMap`. For each, give a real-world use case and explain what happens when a new observable emits before the previous one completes.

<details><summary>Answer</summary>

`switchMap`: cancels previous inner observable. Use: search typeahead — new keystroke cancels pending request.

`mergeMap`: runs all inner observables concurrently. Use: batch file uploads — all in parallel. Risk: flooding.

`concatMap`: queues — waits for previous to complete before starting next. Use: ordered operations (chat messages, sequential API calls).

`exhaustMap`: ignores new emissions while current is active. Use: login/submit button — prevents duplicate submissions.
</details>

---

**F13.** What is tree-shaking? How does Angular's build process use it? What prevents a service from being tree-shaken? How does `providedIn: 'root'` enable tree-shaking compared to registering in `providers: []`?

<details><summary>Answer</summary>

Tree-shaking removes unused code via static analysis of ES module imports.

- **`providedIn: 'root'`** — tree-shakable: if nothing injects the service, the bundler finds no import path and removes it.
- **`providers: []` in a module** — NOT tree-shakable: the module imports the service file, keeping it in the bundle regardless of usage.

Tree-shaking blockers:
- Side effects in the constructor (bundler plays it safe and keeps the code).
- Barrel files (`export * from ...`) — force the entire file into the bundle.
- Dynamic `import()` patterns the bundler can't statically analyze.
</details>

---

