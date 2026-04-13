## SECTION C — ANGULAR SIGNALS

### EASY

**C1.** What is a signal in Angular? How is it different from a BehaviorSubject?

<details><summary>Answer</summary>

- **Signal** — a reactive value wrapper; read with `count()`, write with `.set()` / `.update()`. Angular tracks which templates and `computed()` signals read it and re-evaluates only those.
- **BehaviorSubject** — an RxJS observable holding a current value; you must `.subscribe()` to react, manually `.unsubscribe()` to avoid memory leaks, and use `async` pipe or `takeUntilDestroyed` in components.
- **Key difference** — signals are **synchronous, pull-based, and automatically tracked** by Angular's rendering engine. BehaviorSubject is **push-based, requires subscription management**, and isn't natively integrated with change detection.
</details>

---

**C2.** What does `computed()` do? When does it re-evaluate?

<details><summary>Answer</summary>

`computed()` creates a derived signal whose value is calculated from other signals. It re-evaluates **lazily** — only when one of its signal dependencies changes AND the computed value is read. Angular tracks the signals accessed during the computation function and marks the computed as stale when any of them change. Reading a stale computed triggers re-evaluation; if no dependency changed, the cached value is returned immediately.
</details>

---

**C3.** Why does mutating a signal object not trigger re-render in Angular?

<details><summary>Answer</summary>

Signals use **reference equality** by default. When you mutate an object in-place (e.g., `cart.push(item)`), the object reference doesn't change — it's still the same array/object in memory. Angular compares the new value to the old value using `===`. Since the reference is identical, Angular sees "value unchanged" and no re-render is triggered. You must replace the reference by creating a new object/array to make the signal detect a change.
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

---

**C6.** What is `effect()` in Angular signals? When does it run? What are the rules about writing to signals inside an effect? Why does Angular restrict this?

<details><summary>Answer</summary>

- **What it does** — runs a function whenever any signal it reads changes. Executes once immediately on creation (to establish dependencies), then again on each dependency change.
- **Rules** — you cannot write to signals inside an effect by default. Angular throws `NG0600: Writing to signals is not allowed in a reactive context`. To allow it, pass `{ allowSignalWrites: true }`.
- **Why** — writing to a signal inside an effect creates a potential feedback loop: the write triggers other effects/computed, which could re-trigger the original effect, causing infinite loops or unpredictable evaluation order.
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

---

**C8.** A signal holds a nested object: `signal({ user: { name: '', address: { city: '' } } })`. You spread the outer object but mutate `address.city` directly. Does the template update? Explain the inner reference problem and the fix.

<details><summary>Answer</summary>

**Spreading the outer object:** creates a new signal reference — `Object.is` detects a new object and templates re-evaluate. ✅

**The inner reference problem:**
```typescript
store.update(s => ({
    ...s,               // ✅ new outer object — signal fires
    user: {
        ...s.user,
        address: {
            ...s.user.address, // ❌ mutated in-place before set() was called
            city: 'London'
        }
    }
}));
```

The original `address` object is mutated **before** `.set()` is called — the old and new signal values share the same `address` reference. Any `computed()` that reads `signal().user.address` and compares by reference sees no change → no re-evaluation.

**Fix:** replace every level of nesting you change with a new object (spread at each level).
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

---

**C11.** Compare signals with RxJS for state management in Angular. When would you still use RxJS over signals? Give three concrete scenarios where signals are insufficient and RxJS is necessary.

<details><summary>Answer</summary>

Signals are simpler for synchronous state — no subscription boilerplate, automatic cleanup, integrated with change detection. RxJS is superior for:
1. **Async streams with cancellation** — HTTP with `switchMap`
2. **Time-based operations** — `debounceTime`, `throttleTime`, `interval`
3. **Combining multiple async sources** — `combineLatest`, `merge`, `zip`

Signals have no concept of time, no cancellation, and can't natively model event streams.
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

---

**C13.** You're migrating from Zone.js to zoneless Angular. Your app has 50 components using `OnPush`. After removing Zone.js, some components stop updating. Why? What do you need to change?

<details><summary>Answer</summary>

With Zone.js, `OnPush` components still update when async events complete — Zone.js patches `setTimeout`, `Promise`, `EventListener`, etc., and triggers change detection. Without Zone.js, Angular only re-renders when:
- a **signal changes**
- an `async` pipe emits
- `ChangeDetectorRef.markForCheck()` is called explicitly

Components stop updating when they hold plain properties or rely on Zone.js-patched events. The fix: convert component state to signals (or call `markForCheck()` at each mutation point), and replace direct property bindings with signal reads.
</details>

---
