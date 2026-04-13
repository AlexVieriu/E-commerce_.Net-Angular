## SECTION F — ANGULAR FUNDAMENTALS

### EASY

**F1.** What is the difference between `ngOnInit` and the constructor in an Angular component?

<details><summary>Answer</summary>

- **Constructor** — called by the JS engine; used for DI injection only — keep it lightweight.
- **`ngOnInit`** — called by Angular after `@Input()` values are set; use for HTTP calls, subscription setup, complex initialization.

Key: `@Input()` values are `undefined` in the constructor — they're only available from `ngOnInit` onwards.
</details>

<details><summary>Deep Dive</summary>

**The full Angular lifecycle sequence**

Understanding where constructor and ngOnInit fit in the complete lifecycle:

```
new Component()          ← constructor called (DI injection, field initialization)
    ↓
ngOnChanges(firstChange) ← FIRST time @Input values are set (only if component has @Input)
    ↓
ngOnInit()               ← component fully initialized, all @Input values available
    ↓
ngDoCheck()              ← every change detection cycle (use sparingly — very hot)
    ↓
ngAfterContentInit()     ← after <ng-content> is projected
ngAfterContentChecked()  ← after content change detection
    ↓
ngAfterViewInit()        ← after component's own view and child views are rendered
ngAfterViewChecked()     ← after view change detection
    ↓
[repeated on changes: ngOnChanges → ngDoCheck → ngAfterContentChecked → ngAfterViewChecked]
    ↓
ngOnDestroy()            ← just before component is removed from DOM
```

**Why @Input() is undefined in the constructor**

Angular's two-phase component initialization:
1. **Construction**: `new MyComponent(service1, service2)` — DI dependencies injected, but Angular hasn't processed the component's bindings yet.
2. **Initialization**: Angular reads the parent template, evaluates property bindings, and calls the `@Input()` setters.

If a parent does `<my-cmp [title]="pageTitle">`, the `title` Input is set AFTER the constructor, during the first `ngOnChanges` call. Any code in the constructor that reads `this.title` sees `undefined`.

```typescript
@Component({...})
export class ProductCardComponent {
  @Input() product!: Product;

  constructor() {
    console.log(this.product); // undefined — not set yet
  }

  ngOnInit() {
    console.log(this.product); // Product object — now available
    this.loadRelatedProducts(this.product.categoryId); // safe
  }
}
```

**The Angular Signals exception**

With Angular 17+ signal-based inputs, the pattern changes slightly:

```typescript
export class ProductCardComponent {
  product = input.required<Product>(); // signal-based input

  constructor() {
    // Still can't read the signal value here — not initialized yet
    // But you CAN use inject() here for other signals
    const router = inject(Router);
  }

  ngOnInit() {
    // Signal value available here
    const cat = this.product().categoryId;
  }
}
```

**Testing implications**

In unit tests, you construct components manually using `TestBed.createComponent()`. If the constructor makes HTTP calls or starts side effects, you can't control timing:

```typescript
// BAD: constructor makes HTTP call
constructor(private productService: ProductService) {
  this.productService.getAll().subscribe(p => this.products = p); // fires immediately in tests
}

// GOOD: ngOnInit makes HTTP call
ngOnInit() {
  this.productService.getAll().subscribe(p => this.products = p); // you control when ngOnInit runs
}
```

With `ngOnInit`, the test calls `fixture.detectChanges()` to trigger it, giving full control.

**`ngAfterViewInit` — when you need DOM access**

If you need to access a child component or a DOM element via `@ViewChild`, use `ngAfterViewInit` — not constructor or ngOnInit:

```typescript
@ViewChild('canvas') canvasRef!: ElementRef<HTMLCanvasElement>;

ngAfterViewInit() {
  // canvasRef.nativeElement is now available
  const ctx = this.canvasRef.nativeElement.getContext('2d');
}
```
</details>

---

**F2.** What is dependency injection in Angular? How do you inject a service into a component?

<details><summary>Answer</summary>

- Angular creates and provides service instances to components that need them.
- **Constructor injection**: `constructor(private productService: ProductService)` — Angular's injector resolves and injects the instance.
- **`inject()` function** (Angular 14+): functional alternative; works in constructors, field initializers, functional guards/interceptors.
</details>

<details><summary>Deep Dive</summary>

**How Angular's injector tree works**

Angular has a hierarchical injector system — not a single global container. There are multiple injector levels:

```
ApplicationRef (root injector)        ← providedIn: 'root' services live here
  └─ Route injectors                  ← per lazy-loaded route
       └─ Component injectors         ← providers: [] on @Component
            └─ Child component injectors
```

When a component requests a service, Angular walks UP the injector tree until it finds a provider. If it reaches the root injector without finding one → `NullInjectorError`.

**The singleton question: where is the instance created?**

```typescript
// Singleton across entire app — one instance at root level
@Injectable({ providedIn: 'root' })
export class CartService {}

// One instance per component — each instantiation gets its own CartService
@Component({
  selector: 'app-cart',
  providers: [CartService]  // overrides root, creates new instance here
})
export class CartComponent {}
```

If `CartComponent` is used 3 times in the template, and each declares `providers: [CartService]`, there are 3 separate `CartService` instances — they don't share state. This is powerful for isolated widget instances.

**`inject()` function — why it matters**

`inject()` allows dependency injection outside of a constructor, which enables functional composition patterns:

```typescript
// Functional guard — no class needed
export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);    // inject() in function context
  const router = inject(Router);

  return authService.isLoggedIn()
    ? true
    : router.createUrlTree(['/login']);
};

// Reusable utility — inject within a service constructor or field init
export function injectCurrentUser() {
  const auth = inject(AuthService);
  return computed(() => auth.currentUser());
}

// Usage in component — field initializer context is valid
export class ProfileComponent {
  user = injectCurrentUser(); // inject() called inside helper, still valid context
}
```

`inject()` is only valid in an **injection context** — constructor, field initializer, factory function, or a function called from one of those. Outside this context → runtime error.

**InjectionToken — for non-class dependencies**

Sometimes you need to inject a plain value (string, number, configuration object) rather than a class:

```typescript
// Define the token
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');
export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');

// Provide it in app.config.ts
export const appConfig: ApplicationConfig = {
  providers: [
    { provide: API_BASE_URL, useValue: environment.apiUrl },
    { provide: APP_CONFIG, useFactory: () => ({ theme: 'dark', lang: 'en' }) }
  ]
};

// Inject it
export class ApiService {
  private readonly baseUrl = inject(API_BASE_URL);
}
```

This avoids "magic strings" scattered across services — the URL is defined once and injected everywhere.

**`@Optional()` and `@Self()` decorators**

```typescript
constructor(
  @Optional() private readonly logger: LogService,  // null if not provided (no error)
  @Self() private readonly validator: ValidatorService,  // only look in THIS component's injector
) {}
```

`@Optional()` is common for features that are optionally enabled. `@Self()` prevents accidentally finding a parent injector's instance when you specifically need the component-local one.
</details>

---

**F3.** What does `providedIn: 'root'` do on a service? What's the alternative?

<details><summary>Answer</summary>

- **`providedIn: 'root'`** — singleton in the root injector; available app-wide; tree-shakable (removed from bundle if nothing injects it). Preferred for most services.
- **`providers: []` on a component** — new instance per component; lives and dies with that component.
- **`providers: []` on a module** — one instance per module injector; breaks tree-shaking.
</details>

<details><summary>Deep Dive</summary>

**Why tree-shaking matters — the mechanics**

Tree-shaking is dead code elimination — the bundler removes code that's never imported or referenced. With `providedIn: 'root'`, the service "self-registers" by declaring its provider on itself. The consuming component/module only needs to inject it. If nothing injects it, the bundler finds no import path to the service → removes it entirely.

Contrast with `providers: [MyService]` in an `@NgModule`:
- The MODULE imports the service file.
- Even if no component in the module ever injects `MyService`, the bundler sees the import and keeps the code.
- Module-based registration breaks tree-shaking.

With standalone components and `providedIn: 'root'`, Angular 17+ has near-perfect tree-shaking.

**The other `providedIn` values**

```typescript
// Root injector — singleton, tree-shakable
@Injectable({ providedIn: 'root' })

// Platform injector — shared across multiple Angular apps on same page (micro-frontends)
@Injectable({ providedIn: 'platform' })

// 'any' — separate instance per lazy-loaded module (rarely used)
@Injectable({ providedIn: 'any' })

// Specific module (NOT tree-shakable — that module always bundles this service)
@Injectable({ providedIn: SomeFeatureModule })
```

**Comparison: module providers vs providedIn: 'root'**

Before Angular 6 (pre-`providedIn`):
```typescript
// In every feature module that needed the service:
@NgModule({
  providers: [ProductService]  // creates a NEW instance per module injector
})
export class ProductsModule {}
```

This was error-prone: two modules both providing `ProductService` means two separate instances. If they both track state (e.g., a cart), they diverge. Bugs were hard to trace.

`providedIn: 'root'` solved this by guaranteeing exactly one instance no matter how many modules or components inject it.

**Component-level providers — intentional isolation**

```typescript
// Two instances of this component get two separate CartService instances
@Component({
  selector: 'app-product-widget',
  providers: [ProductWidgetState]  // scoped to this component subtree
})
export class ProductWidgetComponent {
  state = inject(ProductWidgetState);
}
```

This is the Angular equivalent of DI scope control. The service lives as long as the component lives — when the component is destroyed, its injector is also destroyed and the service is cleaned up. Useful for UI state that belongs exclusively to one component widget.

**Lazy-loaded modules and the "two singletons" trap**

In pre-standalone Angular with NgModules, a common gotcha:

1. `AppModule` imports `UserService` via `providedIn: 'root'` → one instance in root injector.
2. A lazy-loaded `AdminModule` also has `providers: [UserService]` in its module → second instance in the lazy module's injector.
3. Components in `AdminModule` inject the lazy-module's instance, not the root instance — different state.

The fix: never duplicate `providers` for services that should be singletons. Use `providedIn: 'root'` only. With standalone components, this footgun is largely eliminated.
</details>

---

**F4.** What is the difference between `*ngIf` and `@if` (new control flow syntax)?

<details><summary>Answer</summary>

- **`*ngIf`** (Angular <17) — structural directive, requires `CommonModule` import, no native `@else if`.
- **`@if`** (Angular 17+) — built-in template syntax, no import needed, full TypeScript type narrowing, native `@else if`.

`@if` is more readable and compiles to faster output (no directive overhead).
</details>

<details><summary>Deep Dive</summary>

**How `*ngIf` works under the hood (desugaring)**

Angular's `*` prefix on a structural directive is syntactic sugar. The compiler desugars it:

```html
<!-- What you write: -->
<div *ngIf="isLoggedIn">Welcome</div>

<!-- What the compiler sees: -->
<ng-template [ngIf]="isLoggedIn">
  <div>Welcome</div>
</ng-template>
```

The `NgIf` directive receives the `isLoggedIn` expression as the `[ngIf]` binding. It holds a `ViewContainerRef` and an `EmbeddedViewRef`. When the condition is true, it calls `viewContainerRef.createEmbeddedView(template)`. When false, it calls `viewContainerRef.clear()`. This creates and destroys the DOM, including running lifecycle hooks on child components.

**Type narrowing — the key practical difference**

With `*ngIf`, TypeScript narrows the type inside the template only partially:

```html
<!-- TypeScript sees user as User | null here -->
<div *ngIf="user">
  <!-- user could still be null according to the template type checker in some cases -->
  {{ user.name }}  <!-- might warn about possible null -->
</div>

<!-- *ngIf "as" syntax for template reference -->
<div *ngIf="user$ | async as user">
  {{ user.name }}  <!-- user is now typed as User (not null) within this block -->
</div>
```

With `@if`, TypeScript's narrowing works exactly like in regular TypeScript:

```html
@if (user) {
  <!-- user is narrowed to non-null here — no type errors -->
  {{ user.name }}
  {{ user.email }}
}
@else if (isLoading) {
  <app-spinner />
}
@else {
  <p>Please log in</p>
}
```

The `@else if` is native syntax — no need for separate `<ng-template #loading>` references.

**Performance — why @if is faster**

`*ngIf` instantiates a full `NgIf` directive class for every conditional in the template. It creates a `ViewContainerRef`, subscribes to change detection, and calls directive lifecycle hooks.

`@if` is compiled directly into the component's template instructions by the Angular compiler — it generates raw Angular runtime calls without directive overhead. Benchmarks show ~20% reduction in template compilation time for heavy template files with many conditionals.

**The new control flow family**

```html
<!-- @for replaces *ngFor — requires track expression -->
@for (product of products; track product.id) {
  <app-product-card [product]="product" />
} @empty {
  <p>No products found.</p>
}

<!-- @switch replaces [ngSwitch] -->
@switch (status) {
  @case ('active') { <span class="green">Active</span> }
  @case ('inactive') { <span class="red">Inactive</span> }
  @default { <span>Unknown</span> }
}
```

The `@for` block requires `track` — previously `*ngFor` allowed you to omit `trackBy` (causing performance bugs). The new syntax makes the tracking function mandatory, preventing a whole class of list-rendering bugs.

**Migrating from `*ngIf` to `@if`**

Angular provides a migration schematic:
```bash
ng generate @angular/core:control-flow
```

This auto-converts all `*ngIf`, `*ngFor`, and `[ngSwitch]` in your templates to the new block syntax. It's safe to run on any Angular 17+ project.
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

<details><summary>Deep Dive</summary>

**What the async pipe does internally**

The `AsyncPipe` class implements `PipeTransform` and `OnDestroy`. When it receives an Observable:
1. It calls `.subscribe()` on it, storing the `Subscription`.
2. On each emission, it stores the latest value and calls `ChangeDetectorRef.markForCheck()` — this is what makes it work with `OnPush`.
3. When the host component is destroyed, Angular calls `ngOnDestroy()` on all pipes — `AsyncPipe.ngOnDestroy()` calls `subscription.unsubscribe()`.

For Promises, it calls `.then()` and handles the single-value resolution the same way.

**The memory leak problem with manual subscribe**

```typescript
// LEAKS — subscription is never cleaned up
ngOnInit() {
  this.productService.getProducts().subscribe(p => this.products = p);
}
```

When the component is destroyed (navigate away from the page), the subscription lives in memory. If the Observable keeps emitting (WebSocket, interval, BehaviorSubject), the callback still runs — updating a component that no longer exists. In large SPAs navigated frequently, this leads to exponential memory growth and phantom state updates.

**The multiple-subscription trap with async pipe**

```html
<!-- BAD: TWO subscriptions to the same HTTP call -->
<p>Count: {{ products$ | async }}</p>
<ul>
  <li *ngFor="let p of products$ | async">{{ p.name }}</li>
</ul>
```

Each `async` pipe creates an independent subscription. If `products$` is an HTTP observable (cold), this fires TWO HTTP requests. Fix: use a template variable:

```html
<!-- GOOD: one subscription, one request -->
@if (products$ | async as products) {
  <p>Count: {{ products.length }}</p>
  @for (product of products; track product.id) {
    <li>{{ product.name }}</li>
  }
}
```

Or use `share()` / `shareReplay(1)` on the observable to multicast:

```typescript
// In service
getProducts(): Observable<Product[]> {
  return this.http.get<Product[]>('/api/products').pipe(
    shareReplay(1)  // cache and multicast — multiple subscribers, one HTTP call
  );
}
```

**OnPush change detection integration — why it matters**

With `ChangeDetectionStrategy.OnPush`, Angular doesn't automatically check this component when global change detection runs. The component only updates when:
- An `@Input()` reference changes
- An event fires within the component
- `ChangeDetectorRef.markForCheck()` is called

The `async` pipe calls `markForCheck()` internally on every emission, which is exactly what you need. Without the async pipe, you'd need to call it yourself:

```typescript
// Manual approach — more boilerplate, easy to forget
ngOnInit() {
  this.products$.subscribe(products => {
    this.products = products;
    this.cdr.markForCheck(); // must do this manually with OnPush
  });
}
```

**`takeUntilDestroyed()` — the modern alternative to async pipe**

When you need to subscribe in the component (e.g., to trigger side effects on emissions), use `takeUntilDestroyed()` instead of async pipe:

```typescript
export class ProductListComponent {
  private destroyRef = inject(DestroyRef);
  products: Product[] = [];

  ngOnInit() {
    this.productService.getProducts()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(p => {
        this.products = p;
        this.updateSomeSideEffect(p); // can't do this in template
      });
  }
}
```

`takeUntilDestroyed()` auto-completes the observable when the component is destroyed — no manual `ngOnDestroy` needed.
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

<details><summary>Deep Dive</summary>

**Emulated — the CSS attribute trick**

Angular rewrites every selector in the component's CSS to add an attribute:

```css
/* What you write */
button { background: blue; }

/* What Angular compiles it to */
button[_ngcontent-bnz-1] { background: blue; }
```

And in the HTML:
```html
<button _ngcontent-bnz-1>Click me</button>
```

The attribute (`_ngcontent-bnz-1`) is unique per component. This tricks the browser's CSS cascade: styles from other components (different attribute) don't match. It's NOT true Shadow DOM — the DOM is still accessible globally, but styling is effectively scoped by CSS specificity.

**ShadowDom — true encapsulation**

With Shadow DOM, each component has its own DOM tree:

```html
<my-component>
  #shadow-root (open)
    <style>button { background: blue; }</style>
    <button>Click me</button>
</my-component>
```

The `<style>` tag only applies inside the shadow tree. External styles can't touch it. This is real isolation but comes with overhead and browser compatibility concerns (older browsers).

**Angular Material's dilemma**

If Material components used `Emulated` or `ShadowDom`, their CSS would be scoped. Users couldn't override styles from the app layer:

```css
/* App global CSS */
button { color: red; }  /* Should apply to Material button, but it won't if scoped */
```

With `ViewEncapsulation.None`, Material's styles are global and user-overridable. This is why Material exports a theme CSS file that users import globally — the styles need to be globally accessible.

**When to use each**

- **Emulated**: default, recommended. Scoping without overhead.
- **ShadowDom**: when you need true isolation (third-party embedded components, micro-frontends).
- **None**: rarely needed. Only when you want global styles (libraries, themes, utility components).
</details>

---

**F8.** What is the difference between `template-driven` forms and `reactive` forms? When would you choose each? Which is easier to test?

<details><summary>Answer</summary>

**Template-driven:** defined in HTML with `ngModel`, two-way binding, simpler for small forms (login, search). Async by nature (directives).

**Reactive:** defined in TypeScript with `FormGroup`/`FormControl`, explicit, synchronous, powerful validation and dynamic fields.

Easier to test: reactive forms (no template rendering needed — test the `FormGroup` directly).

Choose template-driven for simple forms, reactive for complex forms (dynamic fields, cross-field validation, arrays).
</details>

<details><summary>Deep Dive</summary>

**Template-driven — magic in the template**

```typescript
@Component({
  template: `
    <form (ngSubmit)="onSubmit()">
      <input [(ngModel)]="user.name" name="name" required />
      <input [(ngModel)]="user.email" name="email" email />
      <button type="submit">Save</button>
    </form>
  `
})
export class LoginComponent {
  user = { name: '', email: '' };
}
```

The magic: `[(ngModel)]` creates a two-way binding. Angular automatically creates a form control behind the scenes. Validation (`required`, `email`) is applied declaratively. The form state (valid/invalid, touched/untouched) is accessible via a template reference variable: `<form #f="ngForm">`.

Disadvantage: the form state is implicit — you can't see it in the component code. Testing requires rendering the template.

**Reactive — explicit form control**

```typescript
@Component({
  template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()">
      <input formControlName="name" />
      <input formControlName="email" />
      <button type="submit">Save</button>
    </form>
  `
})
export class LoginComponent {
  form = new FormGroup({
    name: new FormControl('', Validators.required),
    email: new FormControl('', [Validators.required, Validators.email])
  });
}
```

The form structure is defined in TypeScript. Validation is explicit (`Validators.required`). The form state is in code and easy to test:

```typescript
// Jasmine/Jest — no TestBed needed, no component rendering
it('should be invalid with bad email', () => {
  form.get('email')!.setValue('not-an-email');
  expect(form.valid).toBeFalse();
  expect(form.get('email')!.hasError('email')).toBeTrue();
});
```

**Dynamic fields — reactive only**

If form fields are dynamic (add/remove fields at runtime based on data), use reactive forms with `FormArray`:

```typescript
form = new FormGroup({
  items: new FormArray([
    new FormGroup({ productId: new FormControl(), qty: new FormControl() })
  ])
});

addItem() {
  (this.form.get('items') as FormArray).push(
    new FormGroup({ productId: new FormControl(), qty: new FormControl() })
  );
}
```

Template-driven forms can't easily handle this.
</details>

---

**F9.** Explain Angular routing: what is a `RouterOutlet`? What is lazy loading? How do you lazy-load a standalone component? What is a route guard?

<details><summary>Answer</summary>

- **`RouterOutlet`** — placeholder directive where Angular renders the matched route's component.
- **Lazy loading** — `loadComponent: () => import('./product.component').then(m => m.ProductComponent)`; downloads the component only when the route is visited, reducing initial bundle size.
- **Route guards** — `CanActivate` (auth check), `CanDeactivate` (unsaved changes warning), `Resolve` (pre-fetch data before render). In Angular 15+, guards can be plain functions via `CanActivateFn`.
</details>

<details><summary>Deep Dive</summary>

**RouterOutlet — the viewport for routes**

```typescript
export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'products', component: ProductListComponent },
  { path: 'product/:id', component: ProductDetailComponent }
];

// In AppComponent template:
// <nav>...</nav>
// <router-outlet></router-outlet>  ← Component gets rendered here
```

When the user navigates to `/products`, `ProductListComponent` is instantiated and rendered inside `<router-outlet>`. When they navigate to `/product/5`, `ProductDetailComponent` renders there instead. The outlet changes content without full page reload.

**Lazy loading — split bundles, faster startup**

Without lazy loading, all components are bundled together → large initial JS payload.

With lazy loading:
```typescript
export const routes: Routes = [
  { path: '', component: HomeComponent },
  {
    path: 'admin',
    loadChildren: () => import('./admin/admin.routes').then(m => m.ADMIN_ROUTES),
    canActivate: [adminGuard]
  }
];
```

The admin module is downloaded only when the user navigates to `/admin`. Initial bundle is smaller. The app starts faster.

For standalone components:
```typescript
{ path: 'checkout', loadComponent: () => import('./checkout/checkout.component').then(m => m.CheckoutComponent) }
```

**Route guards — control navigation**

```typescript
const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  return authService.isLoggedIn() ? true : inject(Router).createUrlTree(['/login']);
};

export const routes: Routes = [
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard] }
];
```

Before navigation to `/dashboard`, the guard runs. If the user isn't logged in, they're redirected to `/login`.

Other guard types: `CanDeactivate` (check unsaved changes before leaving), `CanLoad` (check before lazy-loading module), `Resolve` (pre-fetch data before showing component).
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

<details><summary>Deep Dive</summary>

**HTTP Observables auto-complete — the critical distinction**

`HttpClient` returns cold observables that emit once and then complete:

```typescript
getAll() {
  return this.http.get<Product[]>('/api/products');
  // Returns Observable that completes after the HTTP response
}
```

When you subscribe, the HTTP request fires. After receiving the response, the observable completes. The subscription is automatically unsubscribed. No memory leak.

**Long-lived observables — the danger**

```typescript
export class ProductService {
  private productUpdates = new Subject<Product>();  // Never completes
  
  getUpdates() {
    return this.productUpdates.asObservable();  // Long-lived
  }
}

// In component
ngOnInit() {
  this.service.getUpdates().subscribe(p => this.handleUpdate(p));
}
```

The component subscribes to the Subject. The Subject never completes (it's a Subject, not tied to HTTP). When the component is destroyed, the subscription is still active. If the Subject continues to emit, the callback still fires — memory leak.

**Manual unsubscribe with ngOnDestroy**

```typescript
export class ProductListComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  ngOnInit() {
    this.productService.getUpdates()
      .pipe(takeUntil(this.destroy$))
      .subscribe(p => this.handleUpdate(p));
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
```

Or use `takeUntilDestroyed()` (Angular 16+):

```typescript
ngOnInit() {
  this.productService.getUpdates()
    .pipe(takeUntilDestroyed())
    .subscribe(p => this.handleUpdate(p));
}
```

The operator automatically completes the observable when the component is destroyed.
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

<details><summary>Deep Dive</summary>

**Interceptor anatomy — request/response pipeline**

An interceptor is a function that takes `HttpRequest` and a `next` function (the rest of the pipeline), and returns an Observable of `HttpEvent`:

```typescript
export const loggingInterceptor: HttpInterceptorFn = (req, next) => {
  console.log(`[${req.method}] ${req.url}`);  // Before request
  
  return next(req).pipe(
    tap(event => {
      if (event instanceof HttpResponse) {
        console.log(`Response: ${event.status}`);  // After response
      }
    }),
    catchError(error => {
      console.error(`Error: ${error.statusText}`);
      throw error;  // Re-throw so other interceptors/handlers see it
    })
  );
};
```

The key: `next(req)` returns an Observable. You can transform it with RxJS operators before returning.

**Order matters**

```typescript
provideHttpClient(withInterceptors([authInterceptor, errorInterceptor, loggingInterceptor]))
```

Interceptors are applied in order. Auth runs first (adds token), then error handling, then logging. Requests flow through them in order. Responses flow back in reverse order (logging sees the response first, then error handling, then auth).

**Error handling interceptor**

```typescript
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError(error => {
      if (error.status === 401) {
        // Clear stored auth token
        sessionStorage.removeItem('token');
        // Navigate to login
        inject(Router).navigateByUrl('/login');
      }
      return throwError(() => error);
    })
  );
};
```

This catches 401 responses globally — any endpoint returning 401 triggers logout and redirect.

**Clone, don't mutate**

Always clone the request before modifying:

```typescript
// BAD — mutates original request
req.headers = req.headers.set('Authorization', 'Bearer ...');

// GOOD — creates new request with modified headers
const newReq = req.clone({ setHeaders: { Authorization: 'Bearer ...' } });
```

The original request might be used elsewhere.
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

<details><summary>Deep Dive</summary>

**switchMap — the cancellation operator**

```typescript
searchBox.valueChanges
  .pipe(
    switchMap(query => this.api.search(query))
  )
  .subscribe(results => this.results = results);
```

User types "product". API request fires. User types "product s". Previous request is canceled, new request fires. User types "product search". Another cancel and new request. Only the last request's results matter.

Without switchMap (using mergeMap), all three requests would fire and you'd see jumbled results.

**mergeMap — parallel execution**

```typescript
selectedFiles$
  .pipe(
    mergeMap(file => this.upload(file))
  )
  .subscribe();
```

All files upload in parallel. Good for bulk operations. Bad if you overwhelm the server — mergeMap has no concurrency limit by default. Fix: pass the `concurrent` count as the second argument:

```typescript
mergeMap(file => this.upload(file), 3)  // max 3 concurrent uploads at once
```

**concatMap — ordered execution**

```typescript
commands$
  .pipe(
    concatMap(cmd => this.execute(cmd))
  )
  .subscribe();
```

Commands are executed in order. Second command waits for first to complete. Good for chat messages (order matters), API operations that depend on each other.

Downside: if one command hangs, subsequent commands are blocked forever.

**exhaustMap — drop incoming while busy**

```typescript
submitButton.click$
  .pipe(
    exhaustMap(() => this.api.submit())
  )
  .subscribe();
```

First click fires the request. Second click while waiting is ignored. Third click (after first completes) fires a new request. Prevents double-submit on slow networks.
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

<details><summary>Deep Dive</summary>

**How tree-shaking works — the import graph**

The bundler (Webpack, esbuild) builds a graph of all ES6 imports. Starting from `main.ts`, it traces every `import` statement:

```
main.ts → app.component.ts → product.service.ts → http.module.ts
                 ↓
             user.service.ts (DEAD CODE — no import)
```

If nothing imports `user.service.ts`, it's marked as dead code and removed from the bundle.

**`providedIn: 'root'` avoids importing in modules**

```typescript
@Injectable({ providedIn: 'root' })
export class UserService { }
```

The service self-registers. No module needs to import it. If no component injects it, the bundler never finds an import path to it → removed.

**Module providers force inclusion**

```typescript
@NgModule({
  providers: [UserService]
})
export class SharedModule { }

// app.module.ts
import { SharedModule } from './shared.module';
```

The app module imports SharedModule (to use its components/directives). Even if UserService is never used, it's in SharedModule's bundle.

**Blocking tree-shaking — side effects**

```typescript
// BAD: side effect in constructor
@Injectable({ providedIn: 'root' })
export class ConfigService {
  constructor() {
    console.log('ConfigService initialized');  // side effect
  }
}
```

Side effects can't be analyzed statically. The bundler assumes ConfigService might have important initialization logic and keeps it, even if unused.

**Barrel files — the anti-pattern**

```typescript
// shared/index.ts (barrel file)
export * from './user.service';
export * from './config.service';
export * from './logger.service';
```

When you `import { UserService } from './shared'`, the bundler includes the entire `index.ts` file, which re-exports everything. All three services are bundled, even if only UserService is used.

Fix: import directly from the service file:
```typescript
import { UserService } from './shared/user.service';
```
</details>

---

