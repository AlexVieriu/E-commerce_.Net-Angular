## SECTION C — ANGULAR SIGNALS

### EASY

**C1.** What is a signal in Angular? How is it different from a BehaviorSubject?

<details><summary>Answer</summary>

- **Signal** — a reactive value wrapper; read with `count()`, write with `.set()` / `.update()`. Angular tracks which templates and `computed()` signals read it and re-evaluates only those.
- **BehaviorSubject** — an RxJS observable holding a current value; you must `.subscribe()` to react, manually `.unsubscribe()` to avoid memory leaks, and use `async` pipe or `takeUntilDestroyed` in components.
- **Key difference** — signals are **synchronous, pull-based, and automatically tracked** by Angular's rendering engine. BehaviorSubject is **push-based, requires subscription management**, and isn't natively integrated with change detection.
</details>

<details><summary>Deep Dive</summary>

**Signal basics**

```typescript
import { signal, computed, effect } from '@angular/core';

const count = signal(0);    // create with initial value

count();                     // read — returns 0
count.set(5);                // write — replaces value
count.update(n => n + 1);   // write — based on current value
count();                     // read — returns 6
```

**BehaviorSubject equivalent**

```typescript
import { BehaviorSubject } from 'rxjs';

const count$ = new BehaviorSubject(0);

count$.getValue();           // read current value (sync)
count$.next(5);              // write
count$.subscribe(v => ...);  // must subscribe to react
// must unsubscribe or pipe(takeUntilDestroyed())
```

**Subscription management — the main pain point of BehaviorSubject**

```typescript
// Component with BehaviorSubject
@Component({ ... })
export class CartComponent implements OnInit, OnDestroy {
    private destroy$ = new Subject<void>();
    cartCount = 0;

    ngOnInit() {
        cartService.count$
            .pipe(takeUntil(this.destroy$))
            .subscribe(n => this.cartCount = n);
    }

    ngOnDestroy() {
        this.destroy$.next();
        this.destroy$.complete();
    }
}

// Same component with signals — no lifecycle hooks needed
@Component({ ... })
export class CartComponent {
    cartCount = cartService.count;  // signal reference — template reads it directly
}
```

**Change detection integration**

- **BehaviorSubject**: when `.next()` is called, Zone.js catches the event and marks the whole component tree dirty (or you use `async` pipe + `OnPush`)
- **Signal**: Angular's signal graph tracks which template nodes read which signals — only those nodes are re-evaluated. No Zone.js needed.

**When to use which**

| Use case | Signal | BehaviorSubject |
|---|---|---|
| Simple component state | Yes | Overkill |
| Service-level shared state | Yes | Yes |
| Complex async streams (debounce, merge, switchMap) | No | Yes |
| HTTP calls with transformation pipeline | No | Yes |
| Cross-component communication | Yes | Yes |
| One-time events (button click, navigation) | Prefer EventEmitter | Subject |

</details>

---

**C2.** What does `computed()` do? When does it re-evaluate?

<details><summary>Answer</summary>

`computed()` creates a derived signal whose value is calculated from other signals. It re-evaluates **lazily** — only when one of its signal dependencies changes AND the computed value is read. Angular tracks the signals accessed during the computation function and marks the computed as stale when any of them change. Reading a stale computed triggers re-evaluation; if no dependency changed, the cached value is returned immediately.
</details>

<details><summary>Deep Dive</summary>

**Basic usage**

```typescript
const price = signal(10);
const quantity = signal(3);

const total = computed(() => price() * quantity());

total(); // 30 — evaluates on first read

price.set(15);
// computed is now marked stale (price changed)
// but NOT yet re-evaluated — lazy

total(); // 45 — re-evaluates now because it was stale and read
total(); // 45 — cached — no re-evaluation
```

**Dependency tracking — how Angular knows what to watch**

When `computed(() => price() * quantity())` runs for the first time, Angular's reactive context records every signal called inside (`price` and `quantity`). These become the computed's dependencies. If only `price` changes, `quantity` doesn't, and `total` is marked stale.

**Lazy vs eager — why lazy matters**

```typescript
const a = signal(1);
const b = computed(() => a() * 2);
const c = computed(() => b() + 100);

// No one reads c
a.set(10);
// b and c are stale, but neither re-evaluated yet
// Zero computation wasted

const value = c(); // now both b and c re-evaluate — 120
```

This is important for performance: if a computed is used in a tab that's not visible, it won't re-evaluate until it's actually read.

**Computed signals are read-only**

```typescript
const total = computed(() => price() * quantity());
total.set(100); // TypeError: total.set is not a function
```

You can only change a computed's value by changing its dependencies.

**Computed with conditional dependencies**

```typescript
const showTax = signal(true);
const price = signal(100);
const tax = signal(0.2);

const displayPrice = computed(() => {
    if (showTax()) {
        return price() * (1 + tax()); // depends on price AND tax
    }
    return price();                    // depends only on price
});
```

When `showTax()` is `false`, Angular does NOT track `tax` as a dependency — if `tax` changes, `displayPrice` won't re-evaluate. Dependencies are **dynamic**, re-tracked on each evaluation.

**`computed()` vs `OnPush` + getters**

```typescript
// Old way — no caching, runs every change detection cycle
get total() { return this.price * this.quantity; }

// Signal computed — cached, runs only when dependencies change
readonly total = computed(() => this.price() * this.quantity());
```

</details>

---

**C3.** Why does mutating a signal object not trigger re-render in Angular?

<details><summary>Answer</summary>

Signals use **reference equality** by default. When you mutate an object in-place (e.g., `cart.push(item)`), the object reference doesn't change — it's still the same array/object in memory. Angular compares the new value to the old value using `===`. Since the reference is identical, Angular sees "value unchanged" and no re-render is triggered. You must replace the reference by creating a new object/array to make the signal detect a change.
</details>

<details><summary>Deep Dive</summary>

**The mutation trap**

```typescript
const cart = signal<CartItem[]>([]);

// WRONG — mutates in place, same reference
cart().push({ id: 1, name: 'Widget' });
// cart's internal value is still the same array object
// === comparison: same reference → no change detected → no re-render

// CORRECT — creates new array reference
cart.update(items => [...items, { id: 1, name: 'Widget' }]);
// new array reference → !== old reference → change detected → re-render
```

**How Angular checks equality**

Signals use an internal `equal` function. Default is `Object.is` (strict reference equality):

```typescript
const a = { x: 1 };
const b = a;
b.x = 2;         // mutation
Object.is(a, b); // true — same reference, mutation invisible

const c = { ...a }; // new object
Object.is(a, c);    // false — new reference, change detected
```

**Custom equality function**

You can override the equality check if reference equality is too strict:

```typescript
const user = signal(
    { name: 'Alice', age: 30 },
    { equal: (a, b) => a.name === b.name && a.age === b.age }
);

user.set({ name: 'Alice', age: 30 }); // equal returns true → no re-render
user.set({ name: 'Bob', age: 30 });   // equal returns false → re-render
```

This is useful to avoid re-renders when the logical value hasn't changed, even if the reference has.

**The `mutate()` method — removed in Angular 17+**

Early Angular signal drafts had a `.mutate()` method that allowed in-place mutation and still triggered change detection. It was removed because it made tracking difficult and was error-prone. The official solution is always to replace the value.

**Deep object mutation — the nested trap**

```typescript
const state = signal({ user: { name: 'Alice' } });

// WRONG — spreads outer but mutates inner reference
const current = state();
current.user.name = 'Bob';    // inner object mutated
state.set({ ...current });    // new outer reference, but user is same object
                              // any computed reading state().user.name gets
                              // the mutated value, but may not re-evaluate
                              // since it saw the same user reference before

// CORRECT — replace all the way down
state.update(s => ({
    ...s,
    user: { ...s.user, name: 'Bob' }
}));
```

Every level of nesting that changes must be replaced with a new object.

</details>

---

### MEDIUM

**C4.** What does this code output? Why?

```typescript
const count = signal(0);
const doubled = computed(() => count() * 2);
const isEven = computed(() => doubled() % 2 === 0);

console.log(isEven()); // ?
count.set(3);
console.log(isEven()); // ?
count.set(4);
console.log(isEven()); // ?
```

<details><summary>Answer</summary>

```
true   // 0 * 2 = 0, 0 % 2 === 0 → true
true   // 3 * 2 = 6, 6 % 2 === 0 → true
true   // 4 * 2 = 8, 8 % 2 === 0 → true
```

All outputs are `true`. `doubled` is always even (any integer × 2 is divisible by 2), so `isEven` is always `true` regardless of `count`. The dependency chain is: `count` → `doubled` → `isEven`. Angular's signal graph evaluates lazily and correctly through the chain.
</details>

<details><summary>Deep Dive</summary>

**Tracing the evaluation chain**

```typescript
const count = signal(0);
// count = 0

const doubled = computed(() => count() * 2);
// not evaluated yet — lazy

const isEven = computed(() => doubled() % 2 === 0);
// not evaluated yet — lazy

console.log(isEven());
// isEven reads doubled() → doubled reads count() → count = 0
// doubled = 0 * 2 = 0
// isEven = 0 % 2 === 0 → true ✓

count.set(3);
// count = 3
// doubled marked stale (depends on count)
// isEven marked stale (depends on doubled)
// no re-evaluation yet — lazy

console.log(isEven());
// isEven reads doubled() → doubled re-evaluates: 3 * 2 = 6
// isEven = 6 % 2 === 0 → true ✓

count.set(4);
// count = 4
// doubled marked stale, isEven marked stale

console.log(isEven());
// doubled re-evaluates: 4 * 2 = 8
// isEven = 8 % 2 === 0 → true ✓
```

**The mathematical reason all are true**

`doubled = count * 2` — multiplying any integer by 2 always yields an even number. `even % 2 === 0` is always true. This is a contrived example, but it demonstrates the chain.

**Angular's glitch-free evaluation**

Signal graphs are designed to be "glitch-free" — no intermediate inconsistent states. If `count` changes from 0 to 3, Angular evaluates the graph in topological order: `count` → `doubled` → `isEven`. You never see `isEven` evaluated with a stale `doubled` value. This is unlike some naive reactive implementations where intermediate states can be visible.

**What if `count` were `signal(3)` initially?**

```typescript
const count = signal(3);
const doubled = computed(() => count() * 2); // 6
const isEven = computed(() => doubled() % 2 === 0); // true — same result
```

**Memoization — computed only re-runs when dependencies change**

```typescript
const count = signal(0);
let evalCount = 0;
const doubled = computed(() => { evalCount++; return count() * 2; });

doubled(); doubled(); doubled(); // read 3 times
console.log(evalCount); // 1 — evaluated only once, cached after

count.set(1);
doubled(); // evalCount = 2 — re-evaluated because count changed
doubled(); // evalCount still 2 — cached
```

</details>

---

**C5.** You have a signal holding a shopping cart:

```typescript
const cart = signal<CartItem[]>([]);
```

Write a function `addItem(item: CartItem)` that correctly triggers re-render. Write a version that DOESN'T trigger re-render. Explain the difference.

<details><summary>Answer</summary>

```typescript
// CORRECT — creates new array reference
function addItem(item: CartItem) {
    cart.update(items => [...items, item]);
}

// WRONG — mutates in place, no re-render
function addItemBroken(item: CartItem) {
    cart().push(item);
}
```

The difference: `.update()` calls the signal's setter with a new array value (`[...items, item]`). Angular's `Object.is` comparison sees a new reference and triggers re-render. `.push()` mutates the existing array — the reference stays the same, Angular's equality check returns `true`, no update fires.
</details>

<details><summary>Deep Dive</summary>

**Under the hood — how `update()` works**

```typescript
// What signal.update() does internally
update(updateFn: (value: T) => T): void {
    this.set(updateFn(this._value));
}

// set() calls the equality check
set(newValue: T): void {
    if (!this._equal(this._value, newValue)) {
        this._value = newValue;
        this._notifyConsumers(); // mark computed/effects as stale
    }
}
```

`[...items, item]` produces a new array → `Object.is(oldArray, newArray)` → `false` → notify.

**Three correct ways to add an item**

```typescript
// 1. Spread — most common
cart.update(items => [...items, item]);

// 2. concat — returns new array
cart.update(items => items.concat(item));

// 3. Explicit new array
cart.update(items => {
    const next = [...items];
    next.push(item);   // push on the NEW copy, not the original
    return next;
});
```

**Two broken ways**

```typescript
// BROKEN 1 — push on original
cart().push(item);  // same reference

// BROKEN 2 — get ref, mutate, set same object
const items = cart();
items.push(item);
cart.set(items);    // items IS the same object — Object.is returns true — no update
```

This second broken version is subtle — even calling `cart.set()` explicitly with the same mutated array won't trigger a re-render because `set()` runs the equality check first.

**Remove and update operations**

```typescript
// Remove item by id
function removeItem(id: number) {
    cart.update(items => items.filter(i => i.id !== id));
}

// Update quantity
function updateQuantity(id: number, qty: number) {
    cart.update(items =>
        items.map(i => i.id === id ? { ...i, quantity: qty } : i)
    );
    // Note: { ...i, quantity: qty } creates a new item object — important for
    // any computed that reads individual items
}

// Clear cart
function clearCart() {
    cart.set([]);
}
```

**Derived computed on the cart**

```typescript
const itemCount = computed(() => cart().length);
const cartTotal = computed(() =>
    cart().reduce((sum, i) => sum + i.price * i.quantity, 0)
);
const isEmpty = computed(() => cart().length === 0);
```

All of these automatically stay in sync with any `addItem` / `removeItem` call that correctly replaces the array reference.

</details>

---

**C6.** What is `effect()` in Angular signals? When does it run? What are the rules about writing to signals inside an effect? Why does Angular restrict this?

<details><summary>Answer</summary>

- **What it does** — runs a function whenever any signal it reads changes. Executes once immediately on creation (to establish dependencies), then again on each dependency change.
- **Rules** — you cannot write to signals inside an effect by default. Angular throws `NG0600: Writing to signals is not allowed in a reactive context`. To allow it, pass `{ allowSignalWrites: true }`.
- **Why** — writing to a signal inside an effect creates a potential feedback loop: the write triggers other effects/computed, which could re-trigger the original effect, causing infinite loops or unpredictable evaluation order.
</details>

<details><summary>Deep Dive</summary>

**Basic usage**

```typescript
import { effect, signal } from '@angular/core';

const count = signal(0);

// Must be created inside an injection context (constructor, factory)
effect(() => {
    console.log('count changed:', count()); // runs immediately, then on each change
});

count.set(1); // logs: count changed: 1
count.set(2); // logs: count changed: 2
```

**Where effects must be created**

Effects require an injection context — they use Angular's `DestroyRef` to automatically clean up when the component/service is destroyed:

```typescript
@Component({ ... })
export class MyComponent {
    private count = signal(0);

    constructor() {
        // Inside constructor — valid injection context
        effect(() => {
            console.log(this.count());
        });
    }
}

// Outside constructor — fails
const e = effect(() => ...); // Error: effect() can only be used within an injection context
```

To create an effect outside the constructor (e.g., in a method), pass an injector:

```typescript
constructor(private injector: Injector) {}

someMethod() {
    effect(() => { ... }, { injector: this.injector });
}
```

**The signal write restriction — why it matters**

```typescript
const a = signal(0);
const b = signal(0);

// DANGEROUS — angular throws NG0600 by default
effect(() => {
    b.set(a() * 2); // reading a, writing b
    // If something reads b and writes a, infinite loop
});
```

Angular can't statically prove this is safe — it bans all writes to enforce correctness. The `allowSignalWrites` escape hatch:

```typescript
effect(() => {
    b.set(a() * 2);
}, { allowSignalWrites: true });
// You're asserting: "I know this won't loop"
```

**Use `computed()` instead of `effect()` for derived values**

```typescript
// WRONG — use effect to keep b in sync with a
effect(() => { b.set(a() * 2); }, { allowSignalWrites: true });

// CORRECT — b IS derived from a, use computed
const b = computed(() => a() * 2);
```

**Legitimate uses of `effect()`**

Effects are for **side effects that don't produce signal state** — things the signal graph can't model:

```typescript
// Logging
effect(() => {
    console.log('[Analytics] Cart changed:', cart().length, 'items');
});

// DOM manipulation that Angular templates can't express
effect(() => {
    document.title = `(${cartCount()}) My Shop`;
});

// Persisting to localStorage
effect(() => {
    localStorage.setItem('userPrefs', JSON.stringify(prefs()));
});

// NOT appropriate: HTTP calls (use toObservable + switchMap instead — see C7)
```

**Cleanup inside effects**

```typescript
effect((onCleanup) => {
    const timer = setInterval(() => poll(url()), 1000);
    onCleanup(() => clearInterval(timer)); // runs before next effect execution or on destroy
});
```

When `url` changes, the effect re-runs: `onCleanup` clears the old timer first, then a new one is set with the new URL.

</details>

---

**C7.** You have a component using signals. You want to make an HTTP call when a signal changes. Should you use `effect()` or `computed()`? Why? What's the danger of using `effect()` for side effects that call APIs?

<details><summary>Answer</summary>

Use `toObservable()` to convert the signal to an observable, then `switchMap` for the HTTP call — proper async handling, request cancellation on rapid changes, and error management via RxJS operators.

`effect()` for HTTP is dangerous because:
- it doesn't cancel in-flight requests when the signal changes again (race conditions)
- it has no built-in error handling
- it misuses the effect API, which is for synchronous side effects

`computed()` is wrong because it's for synchronous derived values — it can't return a `Promise` or `Observable`.
</details>

<details><summary>Deep Dive</summary>

**Why `effect()` for HTTP is problematic**

```typescript
const productId = signal(1);

// WRONG — race condition, no cancellation
effect(() => {
    this.http.get(`/api/products/${productId()}`).subscribe(p => {
        this.product.set(p); // what if productId changed again before this returns?
    });
});

// Scenario:
// productId.set(1) → HTTP request 1 starts
// productId.set(2) → HTTP request 2 starts
// Request 2 returns first → product = product 2
// Request 1 returns (late) → product = product 1 ← stale data overwrites correct data
```

This is the classic HTTP race condition. RxJS's `switchMap` cancels the previous request automatically.

**Why `computed()` doesn't work for HTTP**

```typescript
// WRONG — computed is synchronous, can't await
const product = computed(async () => {
    const p = await this.http.get(...).toPromise(); // can't await in computed
    return p;
});
// product() returns a Promise, not a Product — templates can't use this
```

`computed()` is evaluated synchronously — it cannot be async.

**The correct approach: `toObservable()` + `switchMap`**

```typescript
import { toObservable } from '@angular/core/rxjs-interop';
import { switchMap } from 'rxjs/operators';

@Component({ ... })
export class ProductComponent {
    productId = signal(1);
    product = signal<Product | null>(null);

    constructor(private http: HttpClient) {
        toObservable(this.productId)
            .pipe(
                switchMap(id => this.http.get<Product>(`/api/products/${id}`)),
                takeUntilDestroyed()
            )
            .subscribe(p => this.product.set(p));
    }
}
```

`toObservable()` emits a new value whenever `productId` changes. `switchMap` cancels the previous in-flight HTTP request when a new `productId` arrives.

**With `debounceTime` for search inputs**

```typescript
const searchTerm = signal('');

toObservable(searchTerm)
    .pipe(
        debounceTime(300),          // wait 300ms after last keystroke
        distinctUntilChanged(),     // don't repeat identical searches
        filter(term => term.length > 2),
        switchMap(term => this.http.get<Product[]>(`/api/products?search=${term}`)),
        catchError(err => {
            this.error.set('Search failed');
            return EMPTY;           // don't break the stream
        }),
        takeUntilDestroyed()
    )
    .subscribe(results => this.results.set(results));
```

**Converting back: `toSignal()` from observable**

```typescript
import { toSignal } from '@angular/core/rxjs-interop';

const products$ = toObservable(categoryId).pipe(
    switchMap(id => this.http.get<Product[]>(`/api/products?category=${id}`))
);

// Convert back to signal for template use
readonly products = toSignal(products$, { initialValue: [] as Product[] });
```

Now `products` is a signal — no `async` pipe needed in template, automatic unsubscription.

**Summary: when to use each**

| Need | Use |
|---|---|
| Synchronous derived value | `computed()` |
| Log, DOM update, localStorage on change | `effect()` |
| HTTP call on signal change | `toObservable()` + `switchMap` |
| HTTP result as a signal | `toSignal(obs$)` |
| Complex async: race condition, debounce, retry | RxJS pipeline |

</details>

---

**C8.** A signal holds a nested object: `signal({ user: { name: '', address: { city: '' } } })`. You spread the outer object but mutate `address.city` directly. Does the template update? Explain the inner reference problem and the fix.

<details><summary>Answer</summary>

It depends on what the template reads. Spreading the outer object creates a new signal value reference — the signal's equality check (`Object.is`) sees a new object and fires. Templates and computed signals that read the signal will re-evaluate. However, the inner `address` object is still the **same reference** — any `computed()` that reads `signal().user.address` and uses reference equality to check it won't detect a change. More critically, the mutation happens before `set()` is called, meaning the original signal value is also mutated (they share the `address` reference).

Fix: replace every level of nesting you change with a new object.
</details>

<details><summary>Deep Dive</summary>

**The broken pattern**

```typescript
const state = signal({
    user: {
        name: 'Alice',
        address: { city: 'Paris' }
    }
});

// WRONG — spreads outer but address is same reference
function updateCity(city: string) {
    const current = state();
    current.user.address.city = city;  // mutates the nested object
    state.set({ ...current });          // new outer object, but user and address are same refs
}
```

Problem 1: `current.user.address.city = city` mutates the live signal value before `set()` is called — the original signal object is now dirty.

Problem 2: `{ ...current }` creates a new outer object, but `current.user` is the same reference as the original `state().user`. Any computed that reads `state().user` might see it as unchanged (same reference) and skip re-evaluation.

**Visualizing the reference tree**

```
BEFORE:
state._value → { user: →──────┐ }
                               ↓
                         { name: 'Alice', address: →──┐ }
                                                       ↓
                                                { city: 'Paris' }

AFTER (broken spread):
state._value → { user: →──────┐ }   ← new outer object
                               ↓
                         { name: 'Alice', address: →──┐ }  ← SAME reference
                                                       ↓
                                                { city: 'London' }  ← mutated
```

Both old and new signal values share the `address` object — the mutation is visible in both.

**The correct deep update**

```typescript
function updateCity(city: string) {
    state.update(s => ({
        ...s,                           // new outer
        user: {
            ...s.user,                  // new user
            address: {
                ...s.user.address,      // new address
                city                    // only city changes
            }
        }
    }));
}
```

Reference tree after correct update:

```
state._value → { user: →──────┐ }   ← new outer
                               ↓
                         { name: 'Alice', address: →──┐ }  ← new user
                                                       ↓
                                                { city: 'London' }  ← new address
```

Every level in the changed path is a new object. Unchanged siblings (`preferences`, `cart`) keep their original references — computeds that read those won't re-evaluate.

**Why this matters for `computed()` granularity**

```typescript
const addressCity = computed(() => state().user.address.city);
const userName    = computed(() => state().user.name);

// After correct city update:
addressCity() // re-evaluates — address is new reference
userName()    // may or may not re-evaluate depending on whether user ref changed
              // since we spread user, it IS a new reference — userName re-evaluates
              // but the VALUE is the same — Angular skips the DOM update (value equality)
```

**Practical takeaway: keep nested state flat**

Deep nesting requires deep spreading. The fix for this complexity is to flatten state:

```typescript
// Flat signals — each update only touches what it needs to
const userName    = signal('Alice');
const addressCity = signal('Paris');

// Update city — only one signal changes
function updateCity(city: string) {
    addressCity.set(city); // done
}
```

Or use a dedicated state management approach (NgRx SignalStore, or a service with multiple signals per concern).

</details>

---

**C9.** Create a signal with `{ address: boolean, payment: boolean }`. Write a `computed` that returns `true` only when both are `true`. Write a function that correctly sets `address` to `true`. Demonstrate what happens if you mutate instead of spread.

<details><summary>Answer</summary>

```typescript
const steps = signal({ address: false, payment: false });

const canPlaceOrder = computed(() => steps().address && steps().payment);

function completeAddress() {
    steps.update(s => ({ ...s, address: true }));
}

// BROKEN — mutation, no re-render
function completeAddressBroken() {
    steps().address = true; // same object reference — canPlaceOrder never updates
}
```
</details>

<details><summary>Deep Dive</summary>

**Full working example**

```typescript
import { signal, computed } from '@angular/core';

// State
const steps = signal({ address: false, payment: false });

// Derived state — re-evaluates whenever steps() changes
const canPlaceOrder = computed(() => {
    const { address, payment } = steps();
    return address && payment;
});

// Update functions
function completeAddress() {
    steps.update(s => ({ ...s, address: true }));
}

function completePayment() {
    steps.update(s => ({ ...s, payment: true }));
}

function resetStep(step: keyof typeof steps) {
    steps.update(s => ({ ...s, [step]: false }));
}

// Trace:
console.log(canPlaceOrder()); // false — neither done
completeAddress();
console.log(canPlaceOrder()); // false — only address done
completePayment();
console.log(canPlaceOrder()); // true — both done
```

**The mutation failure — step by step**

```typescript
function completeAddressBroken() {
    steps().address = true;
    // steps() returns the live object reference
    // .address = true mutates it in place
    // steps._value is now { address: true, payment: false }
    // BUT the signal was never notified — no set() was called
    // computed(canPlaceOrder) is never marked stale
    // template reads canPlaceOrder() — returns cached false
    // nothing updates even though the underlying value changed!
}
```

This is the worst kind of bug — the data is right (`address` is `true`) but the UI is wrong (still showing disabled button). Debugging is confusing because `console.log(steps())` shows the correct value.

**Using `{ ...s }` vs `Object.assign`**

```typescript
// Spread — creates new object, preferred
steps.update(s => ({ ...s, address: true }));

// Object.assign — mutates TARGET, returns target
// Object.assign({}, s, { address: true }) — creates new object, OK
// Object.assign(s, { address: true }) — mutates s, WRONG
steps.update(s => Object.assign({}, s, { address: true })); // OK but less readable
steps.update(s => Object.assign(s, { address: true }));     // WRONG — mutates s
```

**In a component template**

```html
<button [disabled]="!canPlaceOrder()">Place Order</button>

<div>
  <input type="checkbox" (change)="completeAddress()" /> Address complete
  <input type="checkbox" (change)="completePayment()" /> Payment complete
</div>
```

The button reactively enables when both checkboxes are ticked — no `ngModel`, no manual change detection calls.

</details>

---

### HARD

**C10.** You have deeply nested state:

```typescript
const store = signal({
  user: {
    profile: { name: '', address: { city: '', zip: '' } },
    preferences: { theme: 'dark', notifications: true }
  },
  cart: { items: [], total: 0 }
});
```

Write a function that updates ONLY `user.profile.address.city` to `'London'` while correctly triggering re-render. Now explain: if a `computed()` signal reads ONLY `store().cart.total`, does updating the city cause it to re-evaluate? Why?

<details><summary>Answer</summary>

```typescript
function updateCity(city: string) {
    store.update(s => ({
        ...s,
        user: {
            ...s.user,
            profile: {
                ...s.user.profile,
                address: {
                    ...s.user.profile.address,
                    city
                }
            }
        }
    }));
}
```

A `computed(() => store().cart.total)` **will be marked stale** when `store` changes — because it reads `store()`, and `store` is a new reference after the update. However, if `cart.total` hasn't changed, Angular's computed memoization will return the cached value and **skip re-rendering** any templates that depend on it. The computed re-evaluates (runs the function), but because the returned value is `===` the previous result, downstream consumers are not notified.
</details>

<details><summary>Deep Dive</summary>

**The deep spread — every level in the changed path**

```typescript
store.update(s => ({
    ...s,                           // new store root (cart reference unchanged)
    user: {
        ...s.user,                  // new user (preferences reference unchanged)
        profile: {
            ...s.user.profile,      // new profile
            address: {
                ...s.user.profile.address,  // new address
                city: 'London'      // only city changes, zip unchanged
            }
        }
    }
}));
```

Reference diagram after update:

```
store._value:
  ├── user (NEW ref)
  │   ├── profile (NEW ref)
  │   │   ├── address (NEW ref)
  │   │   │   ├── city: 'London'   ← changed
  │   │   │   └── zip: ''          ← unchanged value, same position
  │   │   └── name: ''             ← unchanged
  │   └── preferences (SAME ref)   ← untouched, no new object
  └── cart (SAME ref)              ← untouched
```

`preferences` and `cart` keep their original references — computeds that read those don't detect a new reference on their specific sub-reads.

**What happens to `computed(() => store().cart.total)`**

Step 1: `store` changes → any signal that read `store()` is marked stale → this computed is marked stale.

Step 2: When the computed is next read (by template or another computed), it re-executes: `store().cart.total`. `store()` returns the new store object, `.cart` is the SAME reference as before, `.total` is `0` (unchanged).

Step 3: The computed's new result (`0`) is compared to the cached result (`0`). `Object.is(0, 0)` → `true`. **The computed returns the cached value and does NOT notify downstream consumers.**

Step 4: Template nodes that read this computed are NOT re-rendered. Angular skips them.

**This is "fine-grained reactivity" — but only for primitives**

```typescript
const cartTotal = computed(() => store().cart.total);
// Computed re-evaluates on any store() change
// But only propagates downstream if the returned NUMBER changes
// Numbers use Object.is — 0 === 0 → no downstream re-render ✓

const cartRef = computed(() => store().cart);
// Computed re-evaluates on any store() change
// store().cart is SAME reference → Object.is returns true → no downstream re-render ✓

const cartItems = computed(() => store().cart.items);
// store().cart.items is SAME array reference → no downstream re-render ✓
```

**To avoid unnecessary re-evaluations: read deeply in computeds**

```typescript
// BAD — cartTotal re-evaluates on ANY store change (city, preferences, etc.)
const cartTotal = computed(() => store().cart.total);

// BETTER — separate signals for unrelated concerns
const cart = signal({ items: [], total: 0 });
const userProfile = signal({ name: '', address: { city: '', zip: '' } });

const cartTotal = computed(() => cart().total);
// cartTotal ONLY re-evaluates when cart() changes — city updates are invisible to it
```

Flat, separated signals give you true fine-grained reactivity. One big nested `store` signal means every computed re-evaluates on every store mutation (though they may return cached values, the evaluation cost is still there).

</details>

---

**C11.** Compare signals with RxJS for state management in Angular. When would you still use RxJS over signals? Give three concrete scenarios where signals are insufficient and RxJS is necessary.

<details><summary>Answer</summary>

Signals are simpler for synchronous state — no subscription boilerplate, automatic cleanup, integrated with change detection. RxJS is superior for:
1. **Async streams with cancellation** — HTTP with `switchMap`
2. **Time-based operations** — `debounceTime`, `throttleTime`, `interval`
3. **Combining multiple async sources** — `combineLatest`, `merge`, `zip`

Signals have no concept of time, no cancellation, and can't natively model event streams.
</details>

<details><summary>Deep Dive</summary>

**Scenario 1: HTTP with cancellation — RxJS wins**

```typescript
// Signals — no cancellation of in-flight requests
const productId = signal(1);
effect(() => {
    // Race condition: changing productId quickly causes multiple requests,
    // no way to cancel previous one
    this.http.get(`/api/products/${productId()}`).subscribe(...);
}, { allowSignalWrites: true });

// RxJS — switchMap cancels previous request automatically
toObservable(productId).pipe(
    switchMap(id => this.http.get<Product>(`/api/products/${id}`)),
    takeUntilDestroyed()
).subscribe(p => this.product.set(p));
```

`switchMap` unsubscribes from the previous HTTP observable when a new value arrives. Signals have no equivalent — they're synchronous and have no concept of "cancel the previous async operation."

**Scenario 2: Time-based operations — RxJS wins**

```typescript
// Signals can't debounce, throttle, or create intervals
const search = signal('');

// RxJS — debounce search input, avoid API call on every keystroke
toObservable(search).pipe(
    debounceTime(300),
    distinctUntilChanged(),
    filter(s => s.length > 2),
    switchMap(s => this.http.get<Product[]>(`/api/search?q=${s}`))
);

// Also: polling with interval
interval(5000).pipe(
    switchMap(() => this.http.get('/api/status')),
    takeUntilDestroyed()
).subscribe(status => this.status.set(status));
```

Signals have no concept of time. You can't `signal.debounce(300)`. You need RxJS for anything involving time.

**Scenario 3: Combining multiple async sources — RxJS wins**

```typescript
// Wait for BOTH user AND permissions to load before rendering
const user$ = this.userService.getUser();
const permissions$ = this.authService.getPermissions();

// RxJS — combineLatest waits for all sources to emit
combineLatest([user$, permissions$]).pipe(
    map(([user, permissions]) => ({ user, permissions })),
    takeUntilDestroyed()
).subscribe(({ user, permissions }) => {
    this.user.set(user);
    this.permissions.set(permissions);
});

// Signals — no way to "wait for both async values to be ready"
// You'd have to manually coordinate with loading states and effects
```

**When signals win**

```typescript
// UI state — no subscription needed, no memory leaks
readonly sidebarOpen = signal(false);
readonly activeTab = signal<'address' | 'payment' | 'review'>('address');
readonly isLoading = signal(false);

// Derived state — automatic, cached
readonly canCheckout = computed(() =>
    this.cart().items.length > 0 && !this.isLoading()
);
```

**The hybrid approach — signals + RxJS**

Modern Angular best practice: use RxJS for async data pipelines, convert results into signals for template consumption:

```typescript
@Component({ ... })
export class ProductListComponent {
    // Signal state — sync UI state
    readonly category = signal('all');
    readonly page = signal(1);

    // RxJS pipeline — async data fetching
    private products$ = combineLatest([
        toObservable(this.category),
        toObservable(this.page)
    ]).pipe(
        switchMap(([cat, p]) => this.http.get<Product[]>(
            `/api/products?category=${cat}&page=${p}`
        ))
    );

    // Convert back to signal for templates — no async pipe needed
    readonly products = toSignal(this.products$, { initialValue: [] as Product[] });
}
```

**Decision table**

| Scenario | Signals | RxJS |
|---|---|---|
| Sync UI state (toggle, tab, modal) | ✓ | Overkill |
| Derived/computed values | ✓ | Verbose |
| HTTP calls | toObservable + switchMap | ✓ |
| Debounce / throttle | No | ✓ |
| Combining async sources | No | ✓ |
| WebSocket / event streams | No | ✓ |
| Polling | No | ✓ |
| Retry on failure | No | ✓ |

</details>

---

**C12.** Design a signal-based state management solution for a checkout flow with three steps: address, payment, review. Each step has validation state. The "Place Order" button is enabled only when all three steps are valid. Show the signal declarations, computed signals, and update functions. Handle the case where the user goes back and changes the address (payment should NOT be invalidated).

<details><summary>Answer</summary>

```typescript
// Step validity state
const addressValid  = signal(false);
const paymentValid  = signal(false);
const reviewAcknowledged = signal(false);

// Derived — all steps must be valid
const canPlaceOrder = computed(() =>
    addressValid() && paymentValid() && reviewAcknowledged()
);

// Updating address does NOT touch payment signal
function submitAddress(formValid: boolean) {
    addressValid.set(formValid);
}

function submitPayment(formValid: boolean) {
    paymentValid.set(formValid);
}
```

Payment validity is independent from address — changing the address doesn't reset `paymentValid` unless you explicitly call `paymentValid.set(false)`. Each step owns its own signal.
</details>

<details><summary>Deep Dive</summary>

**Full checkout state design**

```typescript
import { signal, computed } from '@angular/core';

// ── Step data ──────────────────────────────────────────────────────────────

interface AddressData {
    street: string;
    city: string;
    zip: string;
    country: string;
}

interface PaymentData {
    cardToken: string;   // Stripe token — never store raw card numbers
    billingAddress: 'same' | 'different';
}

// ── Step state signals ─────────────────────────────────────────────────────

// Validity — each step independent
const addressValid   = signal(false);
const paymentValid   = signal(false);
const reviewChecked  = signal(false);

// Data — each step holds its own data
const addressData    = signal<AddressData | null>(null);
const paymentData    = signal<PaymentData | null>(null);

// Current step navigation
const currentStep    = signal<'address' | 'payment' | 'review'>('address');

// Loading state
const isSubmitting   = signal(false);
const submitError    = signal<string | null>(null);

// ── Derived signals ────────────────────────────────────────────────────────

const canGoToPayment = computed(() => addressValid());
const canGoToReview  = computed(() => addressValid() && paymentValid());
const canPlaceOrder  = computed(() =>
    addressValid() && paymentValid() && reviewChecked() && !isSubmitting()
);

// Progress indicator
const completedSteps = computed(() => {
    const steps: string[] = [];
    if (addressValid()) steps.push('address');
    if (paymentValid()) steps.push('payment');
    if (reviewChecked()) steps.push('review');
    return steps;
});

// ── Update functions ───────────────────────────────────────────────────────

function submitAddress(data: AddressData, isValid: boolean) {
    addressData.set(data);
    addressValid.set(isValid);
    // NOTE: paymentValid is NOT touched here
    // User fixing address does not require re-entering payment
    if (isValid) currentStep.set('payment');
}

function goBackToAddress() {
    currentStep.set('address');
    // addressValid stays true — user can see their saved address
    // paymentValid stays true — they don't need to re-enter card
}

function submitPayment(data: PaymentData, isValid: boolean) {
    paymentData.set(data);
    paymentValid.set(isValid);
    if (isValid) currentStep.set('review');
}

function acknowledgeReview(checked: boolean) {
    reviewChecked.set(checked);
}

async function placeOrder(orderService: OrderService) {
    if (!canPlaceOrder()) return;

    isSubmitting.set(true);
    submitError.set(null);

    try {
        await orderService.submit({
            address: addressData()!,
            payment: paymentData()!
        });
        // Navigate to confirmation
    } catch (err) {
        submitError.set('Order failed. Please try again.');
    } finally {
        isSubmitting.set(false);
    }
}
```

**Why address change should NOT invalidate payment**

The key design decision: `paymentValid` is an independent signal. It only changes when `submitPayment()` is called. Calling `submitAddress()` touches only `addressData` and `addressValid` — `paymentValid` is completely untouched.

This is correct UX: if a user realizes their city is wrong and goes back to fix it, their credit card info should still be valid. Forcing them to re-enter payment details for an address typo fix is frustrating and bad UX.

**When you SHOULD invalidate payment**

If the billing address is "same as shipping" and the user changes the shipping address, you might want to update the payment step:

```typescript
function submitAddress(data: AddressData, isValid: boolean) {
    const previousCity = addressData()?.city;
    addressData.set(data);
    addressValid.set(isValid);

    // If payment uses "same address" and city changed, re-validate payment
    if (paymentData()?.billingAddress === 'same' && data.city !== previousCity) {
        paymentValid.set(false); // require payment re-confirmation
    }
}
```

This is explicit, controlled invalidation — not automatic side-effect coupling.

**Template usage**

```html
<!-- Step navigation -->
<div class="steps">
    <button (click)="currentStep.set('address')" 
            [class.active]="currentStep() === 'address'">
        Address {{ addressValid() ? '✓' : '' }}
    </button>
    <button (click)="currentStep.set('payment')"
            [disabled]="!canGoToPayment()"
            [class.active]="currentStep() === 'payment'">
        Payment {{ paymentValid() ? '✓' : '' }}
    </button>
    <button (click)="currentStep.set('review')"
            [disabled]="!canGoToReview()"
            [class.active]="currentStep() === 'review'">
        Review
    </button>
</div>

<!-- Place Order -->
<button (click)="placeOrder()"
        [disabled]="!canPlaceOrder()">
    {{ isSubmitting() ? 'Processing...' : 'Place Order' }}
</button>

@if (submitError()) {
    <p class="error">{{ submitError() }}</p>
}
```

No `ChangeDetectorRef.markForCheck()`, no `async` pipe, no subscriptions in the component — signals handle all reactivity.

</details>

---

**C13.** You're migrating from Zone.js to zoneless Angular. Your app has 50 components using `OnPush`. After removing Zone.js, some components stop updating. Why? What do you need to change?

<details><summary>Answer</summary>

With Zone.js, `OnPush` components still update when async events complete — Zone.js patches `setTimeout`, `Promise`, `EventListener`, etc., and triggers change detection. Without Zone.js, Angular only re-renders when:
- a **signal changes**
- an `async` pipe emits
- `ChangeDetectorRef.markForCheck()` is called explicitly

Components stop updating when they hold plain properties or rely on Zone.js-patched events. The fix: convert component state to signals (or call `markForCheck()` at each mutation point), and replace direct property bindings with signal reads.
</details>

<details><summary>Deep Dive</summary>

**Why Zone.js-based OnPush "just worked"**

Zone.js wraps every async API: `setTimeout`, `setInterval`, `Promise.then`, `fetch`, `addEventListener`, `XMLHttpRequest`. When any of these complete, Zone.js notifies Angular: "something happened — run change detection." With `OnPush`, Angular checks only the subtree from changed inputs — but the trigger still came from Zone.js.

```typescript
// OnPush component — worked with Zone.js, breaks without
@Component({
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductComponent {
    product: Product | null = null; // plain property, not a signal

    ngOnInit() {
        this.http.get<Product>('/api/product/1').subscribe(p => {
            this.product = p;
            // Zone.js catches this subscribe callback completing
            // → triggers change detection → template updates
            // Without Zone.js: nothing triggers → template shows null forever
        });
    }
}
```

**What changes in zoneless**

Without Zone.js, Angular re-renders a component only when:
1. A signal it reads changes
2. An `async` pipe receives a new emission
3. `ChangeDetectorRef.markForCheck()` is called
4. An `@Input()` with signal semantics changes

**Migration paths**

**Option 1: Convert to signals (preferred)**

```typescript
@Component({
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
        @if (product()) {
            <h1>{{ product()!.name }}</h1>
        }
    `
})
export class ProductComponent {
    product = signal<Product | null>(null); // signal, not plain property

    constructor(private http: HttpClient) {
        this.http.get<Product>('/api/product/1')
            .pipe(takeUntilDestroyed())
            .subscribe(p => this.product.set(p)); // set() triggers re-render
    }
}
```

**Option 2: Use `async` pipe (still works zoneless)**

```typescript
@Component({
    template: `
        @if (product$ | async; as product) {
            <h1>{{ product.name }}</h1>
        }
    `
})
export class ProductComponent {
    product$ = this.http.get<Product>('/api/product/1'); // observable, async pipe handles it
}
```

The `async` pipe calls `markForCheck()` internally when a new value arrives.

**Option 3: Manual `markForCheck()` (least preferred)**

```typescript
constructor(private cdr: ChangeDetectorRef) {}

ngOnInit() {
    this.http.get<Product>('/api/product/1').subscribe(p => {
        this.product = p;
        this.cdr.markForCheck(); // explicitly tell Angular to re-check
    });
}
```

This works but defeats the purpose of migrating to zoneless — you're manually doing what Zone.js did automatically.

**How to enable zoneless in Angular 18+**

```typescript
// main.ts
bootstrapApplication(AppComponent, {
    providers: [
        provideExperimentalZonelessChangeDetection() // Angular 18
        // or provideZonelessChangeDetection() in Angular 19+
    ]
});
```

Also remove from `angular.json`:
```json
"polyfills": ["zone.js"]   // remove this line
```

**What to audit across 50 components**

1. Any plain property set inside a `.subscribe()` callback
2. Any plain property set inside a `setTimeout()` / `setInterval()` callback
3. Any `@HostListener` that modifies plain properties
4. Any usage of `router.events.subscribe()` that sets properties
5. Third-party libraries that manipulate the DOM directly (they won't trigger signals)

**Quick audit grep**

```bash
# Find components using OnPush that still have plain property assignments in subscribe callbacks
grep -r "\.subscribe" --include="*.ts" -l
# Then check each for signal usage
```

**The zoneless benefit**

Once migrated, Angular only runs change detection for the exact signals that changed — no tree walk from root, no unnecessary component re-evaluations. For large apps, this can be a 30–50% rendering performance improvement.

</details>

---
