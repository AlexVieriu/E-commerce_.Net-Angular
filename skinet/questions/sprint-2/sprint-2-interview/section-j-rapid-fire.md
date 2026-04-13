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

---
