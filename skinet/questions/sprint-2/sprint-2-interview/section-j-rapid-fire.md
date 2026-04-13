## SECTION J — RAPID-FIRE SYNTAX (30 seconds each)

**J1.** What is `@if` vs `*ngIf` in Angular 17+? Why did Angular add the new syntax?

<details><summary>Answer</summary>

- **`*ngIf`** — structural directive, requires `NgIf` imported in `imports[]`, uses microsyntax (`<div *ngIf="condition; else tmpl">`), requires a separate `<ng-template #tmpl>` for else.
- **`@if`** — built-in control flow (Angular 17+), no imports needed, supports `@else if` inline, compiles to more efficient code, easier to read.

Angular added `@if` to: reduce import boilerplate, improve compile-time optimisation, enable better SSR streaming (templates can be deferred), and make templates more readable (looks like actual control flow, not magic attributes).

```html
<!-- Old -->
<div *ngIf="user; else loading">{{ user.name }}</div>
<ng-template #loading><p>Loading...</p></ng-template>

<!-- New -->
@if (user) {
  <p>{{ user.name }}</p>
} @else {
  <p>Loading...</p>
}
```
</details>

<details><summary>Deep Dive</summary>

**Why the old syntax had limitations**

`*ngIf` is syntactic sugar for `[ngIf]` on `<ng-template>`. It's a directive, so it requires the `NgIf` directive to be available in scope — either via `CommonModule` or explicit import. In standalone components, developers frequently forgot to import it.

`@else if` was impossible without nesting multiple `*ngIf` directives:

```html
<!-- Old — painful to nest -->
<div *ngIf="status === 'loading'">Loading...</div>
<div *ngIf="status === 'error'">Error</div>
<div *ngIf="status === 'success'">{{ data }}</div>
```

```html
<!-- New — clean -->
@if (status === 'loading') {
  <p>Loading...</p>
} @else if (status === 'error') {
  <p>Error</p>
} @else {
  <p>{{ data }}</p>
}
```

**`@for` and `@switch` — same family**

```html
@for (item of items; track item.id) {
  <li>{{ item.name }}</li>
} @empty {
  <li>No items</li>
}

@switch (role) {
  @case ('admin') { <admin-panel /> }
  @case ('user')  { <user-dashboard /> }
  @default        { <login-page /> }
}
```

`@empty` replaces `*ngIf="items.length === 0"` alongside `*ngFor`. `@switch` replaces the verbose `ngSwitch` directive chain.

**Performance: why the compiler cares**

The old structural directives are evaluated at runtime by the change detection engine. New control flow syntax is compiled into optimised instruction sequences at build time — Angular knows the structure of the template statically. This enables better tree-shaking and smaller bundles.
</details>

---

**J2.** What does `inject()` function do in Angular? How is it different from constructor injection?

<details><summary>Answer</summary>

`inject()` is a function-based DI mechanism (Angular 14+) that retrieves a service from the DI container without requiring it to be a constructor parameter.

```typescript
// Constructor injection
constructor(private router: Router) {}

// inject() — works anywhere in injection context
readonly router = inject(Router);
```

**Differences:**
- Works in field initializers, factory functions, functional guards/interceptors — not just constructors
- Enables composable utility functions that inject services internally
- Reduces constructor boilerplate in components with many dependencies
- Limitation: must be called during construction (in an injection context — constructor body, field initializer, or `runInInjectionContext`)
</details>

<details><summary>Deep Dive</summary>

**Where `inject()` works — and doesn't**

```typescript
// ✅ Field initializer — injection context
export class MyComponent {
  private router = inject(Router); // OK
  private service = inject(MyService); // OK
}

// ✅ Constructor body — injection context
export class MyComponent {
  constructor() {
    const router = inject(Router); // OK
  }
}

// ✅ Functional interceptor — injection context provided by framework
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService); // OK — framework provides context
  return next(req);
};

// ❌ Outside injection context — throws
export class MyComponent {
  ngOnInit() {
    const router = inject(Router); // RuntimeError: inject() must be called from an injection context
  }
}
```

**Composable utility functions — the main benefit**

```typescript
// Reusable hook (like React hooks, but Angular)
function useCurrentUser() {
  const authService = inject(AuthService);
  return computed(() => authService.currentUser());
}

// Component uses the hook — no need to inject AuthService directly
export class ProfileComponent {
  user = useCurrentUser(); // inject() called inside the function, in correct context
}
```

This is impossible with constructor injection — you'd have to pass the service as a parameter to the utility function.

**`inject()` in guards (functional style)**

```typescript
// Old class-based guard
@Injectable()
export class AuthGuard implements CanActivate {
  constructor(private auth: AuthService, private router: Router) {}
  canActivate() { ... }
}

// New functional guard — no class, no DI decorator
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn() ? true : router.createUrlTree(['/login']);
};
```

Less boilerplate, easier to test (just call the function with a mocked injection context).
</details>

---

**J3.** What does `takeUntilDestroyed()` do? Why is it important for preventing memory leaks?

<details><summary>Answer</summary>

`takeUntilDestroyed()` (Angular 16+) is an RxJS operator that automatically completes a subscription when the component/directive/service that contains it is destroyed.

```typescript
// Old — manual cleanup required
export class MyComponent implements OnDestroy {
  private destroy$ = new Subject<void>();

  ngOnInit() {
    this.data$.pipe(takeUntil(this.destroy$)).subscribe(...);
  }
  ngOnDestroy() { this.destroy$.next(); this.destroy$.complete(); }
}

// New — zero boilerplate
export class MyComponent {
  constructor() {
    this.data$.pipe(takeUntilDestroyed()).subscribe(...);
  }
}
```

**Why it prevents memory leaks:** when a component is destroyed but its subscriptions aren't cancelled, the observable keeps running and the callback keeps firing — even though the component is gone. This holds a reference to the component in memory (preventing GC) and can cause errors when the callback tries to update destroyed UI. `takeUntilDestroyed()` ensures cleanup is automatic and guaranteed.
</details>

<details><summary>Deep Dive</summary>

**What a memory leak looks like in practice**

```typescript
// BUG — no cleanup
export class ProductListComponent implements OnInit {
  ngOnInit() {
    interval(1000).subscribe(() => {
      this.products = this.productService.getAll(); // fires every second
    });
  }
}
```

Navigate away from this component → `ngOnDestroy` never called → the `interval` keeps firing → `this.products` assignment targets a destroyed component → if your route renders 5 of these components and the user navigates in and out 10 times, you have 50 running intervals.

**Fix with `takeUntilDestroyed`**

```typescript
export class ProductListComponent {
  private destroyRef = inject(DestroyRef); // optional: inject DestroyRef for use outside constructor

  constructor() {
    interval(1000).pipe(
      takeUntilDestroyed()     // automatically cancels on destroy
    ).subscribe(() => {
      this.products = this.productService.getAll();
    });
  }
}
```

**Using `DestroyRef` for subscriptions outside constructor**

`takeUntilDestroyed()` must be called in an injection context. If your subscription is in `ngOnInit`:

```typescript
export class MyComponent implements OnInit {
  private destroyRef = inject(DestroyRef); // inject in field initializer (injection context)

  ngOnInit() {
    this.data$.pipe(
      takeUntilDestroyed(this.destroyRef) // pass DestroyRef explicitly
    ).subscribe(...);
  }
}
```

**Async pipe — the even simpler alternative**

For template bindings, the `async` pipe handles subscription AND cleanup automatically:

```html
<div *ngFor="let product of products$ | async">...</div>
```

When the component is destroyed, the `async` pipe unsubscribes. No `takeUntilDestroyed`, no `ngOnDestroy`, no manual cleanup. Prefer `async` pipe in templates; use `takeUntilDestroyed` when you need the value in component logic.
</details>

---

**J4.** What is the difference between `subscribe()` and the `async` pipe? Which one requires manual cleanup?

<details><summary>Answer</summary>

- **`subscribe()`** — manually subscribes to an observable in component code. You own the subscription lifecycle. Requires manual cleanup: call `unsubscribe()` in `ngOnDestroy`, or use `takeUntilDestroyed()`.
- **`async` pipe** — used in templates (`{{ data$ | async }}`). Angular automatically subscribes when the component renders and unsubscribes when it destroys. Zero cleanup code.

**`subscribe()` requires manual cleanup. `async` pipe does not.**

```typescript
// subscribe() — must clean up
subscription = this.data$.subscribe(d => this.data = d);
ngOnDestroy() { this.subscription.unsubscribe(); }

// async pipe — automatic
// Template: {{ data$ | async }}
// No ngOnDestroy needed
```

Use `async` pipe when you only need the value in the template. Use `subscribe()` when you need to react to the value in component logic (e.g. navigate, show a toast, set multiple properties).
</details>

<details><summary>Deep Dive</summary>

**When `subscribe()` is the right choice**

```typescript
// Need to do something on value — can't use async pipe
this.orderService.placeOrder(dto).subscribe({
  next: (order) => {
    this.router.navigateByUrl('/checkout/success');
    this.snackbar.success(`Order #${order.id} placed!`);
  },
  error: (err) => this.snackbar.error(err.message)
});
```

The `async` pipe just binds a value to the template — it can't call methods or navigate.

**Multiple subscriptions to the same observable**

`async` pipe creates a NEW subscription per pipe usage:

```html
<!-- BUG — subscribes twice, two HTTP requests -->
<div>{{ products$ | async | json }}</div>
<p>Count: {{ (products$ | async)?.length }}</p>
```

Fix with `as`:

```html
@if (products$ | async; as products) {
  <div>{{ products | json }}</div>
  <p>Count: {{ products.length }}</p>
}
```

Or convert to a signal:
```typescript
products = toSignal(this.products$, { initialValue: [] });
```

**`toSignal()` — the modern bridge**

```typescript
import { toSignal } from '@angular/core/rxjs-interop';

export class ProductListComponent {
  private productService = inject(ProductService);
  products = toSignal(this.productService.getAll(), { initialValue: [] });
  // No subscribe, no async pipe, no cleanup — signal is reactive and auto-cleaned
}
```

`toSignal` is the preferred approach in Angular 17+ for integrating observables into signal-based components. It handles cleanup automatically via the component's `DestroyRef`.

**The subscription accumulation trap**

```typescript
// BUG — each call to ngOnInit accumulates a new subscription
ngOnInit() {
  this.route.params.subscribe(params => {
    this.productId = params['id'];
  });
}
```

If this component is reused (same route, different params), Angular may call `ngOnInit` multiple times without destroying the component. Each call adds another subscription. Fix: use `takeUntilDestroyed` or `async` pipe.
</details>

---

**J5.** What does `@defer` do in Angular? When would you use it?

<details><summary>Answer</summary>

`@defer` (Angular 17+) lazy-loads a block of content — including components, directives, and pipes — only when a specified trigger condition is met. The deferred block is not included in the initial bundle.

```html
@defer (on viewport) {
  <heavy-chart-component [data]="chartData" />
} @loading {
  <p>Loading chart...</p>
} @placeholder {
  <div class="chart-skeleton"></div>
} @error {
  <p>Failed to load chart</p>
}
```

**When to use:**
- Heavy components below the fold (charts, data grids, code editors)
- Third-party widgets loaded only on interaction
- Admin panels or rarely-accessed tabs
- Any component that slows Initial Contentful Paint (ICP) and isn't needed immediately
</details>

<details><summary>Deep Dive</summary>

**Trigger types**

```html
@defer (on idle)          <!-- when browser is idle (requestIdleCallback) -->
@defer (on viewport)      <!-- when placeholder enters the viewport (IntersectionObserver) -->
@defer (on interaction)   <!-- when user clicks/focuses the placeholder -->
@defer (on hover)         <!-- when user hovers the placeholder -->
@defer (on timer(3s))     <!-- after 3 seconds -->
@defer (when condition)   <!-- when a boolean expression becomes true -->
```

**`@placeholder` vs `@loading`**

- `@placeholder` — shown BEFORE any loading starts (the "before" state)
- `@loading` — shown WHILE the deferred chunk is being fetched (network in progress)
- `@error` — shown if the lazy load fails
- `@defer` block — shown after successful load

```html
@defer (on interaction; prefetch on idle) {
  <rich-text-editor />
} @placeholder (minimum 100ms) {
  <div class="editor-placeholder">Click to edit</div>
} @loading (minimum 500ms; after 100ms) {
  <spinner />
}
```

`minimum` prevents flickering for fast loads. `after 100ms` delays showing the spinner until 100ms has passed (avoids spinner flash for fast connections).

**`prefetch` — load without rendering**

```html
@defer (on interaction; prefetch on idle) { ... }
```

Prefetches the chunk while the browser is idle, so when the user clicks, the component loads instantly. Good for components that are likely to be used but not immediately.

**What `@defer` does to the bundle**

Without `@defer`: `HeavyChartComponent` is included in the main bundle — downloaded even if the user never scrolls to see it.

With `@defer`: Angular's build tool creates a separate lazy chunk. Only downloaded when the trigger fires. Initial bundle is smaller → faster FCP, better Lighthouse score.

**When NOT to use `@defer`**

- Components that are always visible above the fold (they'd just add loading flash)
- Critical path components the user needs immediately
- Small components where the network overhead of a separate chunk exceeds the savings
</details>

---

**J6.** What is `ChangeDetectionStrategy.OnPush`? What are the three things that trigger change detection in an OnPush component?

<details><summary>Answer</summary>

`ChangeDetectionStrategy.OnPush` tells Angular: only check this component for changes in specific situations — don't check it on every event/timer in the app.

**Three triggers that cause OnPush to check the component:**

1. **An `@Input()` reference changes** — a new object/array reference is passed in (mutation of the same object does NOT trigger)
2. **An event fires from within the component** — user clicks a button, types in an input, any `(event)` binding inside this component's template
3. **An Observable/Signal emits** — when using `async` pipe or signals (`.set()`, `.update()`) inside the component

**Benefit:** Angular can skip entire subtrees of the component tree when checking for changes. Critical for performance in large apps with many components.
</details>

<details><summary>Deep Dive</summary>

**Default vs OnPush — what changes**

Default change detection: Angular walks the ENTIRE component tree on every browser event (click, keyup, setTimeout, HTTP response). Every component is checked every time, regardless of whether its inputs changed.

OnPush: Angular skips the component and its children unless one of the three triggers fires. The component "opts out" of the global check.

```typescript
@Component({
  selector: 'app-product-card',
  template: `<h2>{{ product().name }}</h2>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductCardComponent {
  product = input<Product>(); // signal-based input
}
```

**Trigger 1: @Input reference change**

```typescript
// Parent component
this.product = { ...this.product, price: 60 }; // NEW object → triggers OnPush child
// vs
this.product.price = 60; // MUTATION → does NOT trigger OnPush child
```

Immutability is not optional with OnPush — it's required.

**Trigger 2: events from within the component**

```html
<button (click)="increment()">+</button> <!-- click event fires → component checked -->
```

Events bubble up through Angular's event system. When the event originates inside the OnPush component, Angular knows to check it.

**Trigger 3: async pipe or signals**

```typescript
// Async pipe — Angular marks component dirty when observable emits
products$ = this.productService.getAll();
// Template: {{ products$ | async }}

// Signals — Angular tracks which templates read which signals
count = signal(0);
// Template: {{ count() }} — automatically re-renders when count changes
```

Signals are the cleanest integration with OnPush — they tell Angular exactly which components to re-check, with no manual marking required.

**`markForCheck()` — the escape hatch**

```typescript
constructor(private cdr: ChangeDetectorRef) {}

// Force check even without a trigger
someExternalCallback() {
  this.data = externalData;
  this.cdr.markForCheck(); // tell Angular: check me on the next cycle
}
```

Used when data arrives from outside Angular's zone (WebSockets, native events, third-party libraries).

**When to use OnPush**

- Presentational/dumb components that receive data only via `@Input()` → always use OnPush
- Smart components that use observables/signals → OnPush with `async` pipe or signals
- Components you're profiling and finding too slow → add OnPush and measure

Rule of thumb: **all components should be OnPush by default** in a well-structured Angular app.
</details>

---
