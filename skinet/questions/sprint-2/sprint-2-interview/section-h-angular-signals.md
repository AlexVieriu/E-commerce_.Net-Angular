## SECTION H — ANGULAR SIGNALS & ZONELESS CHANGE DETECTION

> **Source:** Bug 1 — `completionStatus` signal not re-rendering because of reference equality.

---

### EASY

**H1.** [HIGH] What is a signal and how does Angular detect changes?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

A signal is a **reactive value container** — a function you call to read the value, with notifications to dependents when it changes.

```typescript
const count = signal(0);
count();            // read: 0
count.set(1);       // write: triggers re-render of anything using count()
count.update(v => v + 1); // functional write
```

Angular detects "changed" via **strict reference equality (`===`)** on the old vs new value. Primitives compare by value; objects compare by reference.

```typescript
const a = { x: 1 };
signal(a).set(a);          // same reference → no re-render
signal(a).set({ ...a });   // new reference → re-render
```


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Signals use change detection." Circular — signals *are* the change detection mechanism in zoneless Angular.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `signal(value, { equal: fn })` lets you override the equality check — useful for deep-compare or for primitives that should be treated specially. Rarely needed but shows you know the API.


</details>

---

**H2.** [HIGH] [OUT-OF-SCOPE] What's the difference between zone-based Angular and zoneless?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

- **Zone-based (classic, up to v17 default)** — Angular uses `zone.js`, a global monkey-patch that intercepts all async events (clicks, timers, HTTP) and triggers change detection on every one. Mutation works because the zone catches everything.
- **Zoneless (v18+, opt-in via `provideZonelessChangeDetection()`)** — no global monkey-patch. Change detection runs only when signals change, `markForCheck()` is called, or `ChangeDetectorRef.detectChanges()` is explicitly invoked.

This project is zoneless — which is why mutation without creating a new reference silently fails to re-render.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Zoneless is faster." True but shallow — it's faster *because* it doesn't poll for changes after every async event, not magically.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that zoneless requires every third-party library to be **signal-aware or opt into change detection manually**. Many libraries (non-reactive ones) break subtly in zoneless mode. Migration effort is real.


</details>

---

### MEDIUM

**H3.** [HIGH] The checkout button stayed disabled even though the signal value was `true`. Trace the root cause.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**The code:**
```typescript
this.completionStatus.update(state => {
    state.address = true;
    return state;
});
```

**What happens in memory:**
```
Before: state @ 0x001 = { address: false, card: false, delivery: false }
After:  state @ 0x001 = { address: true,  card: false, delivery: false }
```

Same memory address → same reference → `state === state` is `true` → Angular's signal equality check returns "no change" → **no re-render**.

**But the value in the signal IS `true`** — the signal's internal `.value` was mutated in place. The problem is purely about change *detection*, not state.

**Fix — return a new object:**
```typescript
this.completionStatus.update(state => ({ ...state, address: true }));
```

Memory:
```
Before: state @ 0x001 = { address: false, ... }
After:  state @ 0x002 = { address: true,  ... }
```

Different reference → `0x001 !== 0x002` → re-render fires → button enables.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Signals don't support object updates." They do, but only if you respect reference equality.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that this bug is **invisible in zone-based Angular** — the zone would catch the next async event (a click, a setTimeout, etc.) and force change detection, masking the mutation. Migrating from zone to zoneless exposes a long tail of these bugs.


</details>

---

**H4.** [HIGH] Pick the correct signal update pattern(s):

```typescript
// A
this.status.update(s => { s.loading = true; return s; });

// B
this.status.update(s => ({ ...s, loading: true }));

// C
this.status.set({ ...this.status(), loading: true });

// D
this.status.update(s => Object.assign({}, s, { loading: true }));
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Correct: B, C, D. Wrong: A.**

- **A — WRONG:** mutates the existing object and returns the same reference. `===` check sees no change → no re-render.
- **B — CORRECT:** spread creates a new object inside `update`. Clean and idiomatic.
- **C — CORRECT:** spread outside the `update`, then `set`. Equivalent behaviour, slightly more verbose.
- **D — CORRECT:** `Object.assign({}, s, ...)` merges into a fresh empty object. Pre-spread syntax; still correct.

**Preferred:** B — shortest, most readable, uses modern JS.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Calling any of B, C, or D "less efficient." The spread / assign cost is negligible compared to the re-render cost you're triggering. Micro-optimising the update is wrong optimisation.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the old `signal.mutate()` API (Angular 17) was a trap — it invited option A's pattern but claimed to work via manual change notification. It was **removed in v18** exactly because it was misused. Knowing API history signals depth.


</details>

---

**H5.** [HIGH] This nested signal update has a shallow-spread bug. Find and fix:

```typescript
const form = signal({ user: { name: '', email: '' }, submitted: false });

form.update(s => {
    s.user.name = 'Alex';
    return { ...s };
});
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**The bug:** `{ ...s }` creates a new outer object, but `s.user` is still the **same reference**. The nested mutation (`s.user.name = 'Alex'`) changes the original `user` object. The signal thinks it changed (outer reference is new), but **any derived signal or computed reading `form().user` sees the same `user` object** — if it uses its own equality check on `user`, it won't detect the name change.

Worse: if any other code holds a reference to the original `form().user`, they see the mutation silently propagate.

**Fix — deep spread the nested object:**
```typescript
form.update(s => ({ ...s, user: { ...s.user, name: 'Alex' } }));
```

Memory after fix:
```
Before: { user: @0x001, submitted: ... }  where @0x001 = { name: '', ... }
After:  { user: @0x002, submitted: ... }  where @0x002 = { name: 'Alex', ... }
Both outer and inner objects have new references.
```

**Deep updates beyond 2 levels** become painful with manual spreading. For deeply nested state, reach for:
- **Immer** — `produce(state, draft => { draft.user.name = 'Alex' })` — looks like mutation, produces immutable updates.
- **@ngrx/signals** with `patchState` — scoped updates with automatic immutability.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Wrap in JSON.parse(JSON.stringify(s))." Destroys class instances, doesn't handle dates/maps/symbols, and is massively slower than targeted spreads.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that deeply nested form state is a **code smell** — flatten the model (one signal per form section) or use a reactive forms API. Signals are best for flat, value-oriented state.


</details>

---

**H6.** [MEDIUM] Why does `count.update(v => v)` not re-render? What if `count` is an object signal?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**For primitives:**
```typescript
const count = signal(0);
count.update(v => v);   // returns 0, same value, === check passes → no re-render
count.update(v => v + 1); // returns 1, different value → re-render
```

`0 === 0` is `true` → signal equality detects no change → no re-render.

**For objects:**
```typescript
const user = signal({ name: 'Alex' });
user.update(v => v);   // returns same reference → === passes → no re-render
user.update(v => ({ ...v })); // returns new reference → === fails → re-render
```

Same reference → no re-render, even though the value is semantically identical.

**Edge case — primitive with side effects:**
```typescript
let counter = 0;
const s = signal(0);
s.update(v => { counter++; return v; });  // counter increments, but no re-render
```

The update callback runs (side effects happen!) but the signal still skips re-render on reference equality. Don't rely on update callbacks for side effects.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Updates always re-render." Simply wrong — the equality check is the gate.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that this is why `signal(value, { equal: () => false })` exists — forces every update to trigger re-render, even with the same value. Useful for "refresh" signals that represent events, not values.


</details>

---

### HARD

**H7.** [HIGH] Find all 3 anti-patterns in this checkout state management:

```typescript
@Component({ ... })
export class CheckoutComponent {
    completionStatus = signal({
        address: false,
        card: false,
        delivery: false,
        items: []
    });

    onAddressFilled(address: Address) {
        this.completionStatus().address = true;
    }

    onItemAdded(item: CartItem) {
        this.completionStatus.update(s => {
            s.items.push(item);
            return { ...s };
        });
    }

    onDeliveryChosen(delivery: Delivery) {
        this.completionStatus.set(this.completionStatus());
        this.completionStatus().delivery = true;
    }
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Anti-pattern 1 — direct mutation on the signal's value (line 6):**
```typescript
this.completionStatus().address = true;
```
Reads the signal's current value and mutates it. Same reference → no re-render. Even worse than update-then-mutate because there's no attempt to notify the signal.

**Anti-pattern 2 — mutating nested array while shallow-spreading outer (line 11):**
```typescript
s.items.push(item);     // mutates same array
return { ...s };         // new outer, same items reference
```
Same shallow-spread bug as H5. The outer signal triggers re-render, but any `computed(() => items())` won't detect the change because `items` is the same array reference.

**Anti-pattern 3 — setting with same reference, then mutating (lines 16-17):**
```typescript
this.completionStatus.set(this.completionStatus());  // same reference, no-op
this.completionStatus().delivery = true;              // mutation, no re-render
```
The `set` is a no-op (same reference). The mutation doesn't notify. Double-broken.

**Corrected:**
```typescript
onAddressFilled(address: Address) {
    this.completionStatus.update(s => ({ ...s, address: true }));
}

onItemAdded(item: CartItem) {
    this.completionStatus.update(s => ({ ...s, items: [...s.items, item] }));
}

onDeliveryChosen(delivery: Delivery) {
    this.completionStatus.update(s => ({ ...s, delivery: true }));
}
```

**Production consequence:** these three patterns silently break the UI. The hardest is that they look plausible — "I updated the signal, why doesn't it render?" — and zoneless migration exposes all three simultaneously.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Only spotting #1. The nested array mutation (#2) is the subtle one that seniors flag.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Suggesting a **lint rule** (`@angular-eslint/prefer-signals` combined with custom AST rules) to catch these patterns at the linting stage rather than review. Systematic prevention beats per-PR vigilance.


</details>

---

**H8.** [MEDIUM] [OUT-OF-SCOPE] Design: a complex checkout form with 20+ fields across 4 steps. How do you model state with signals without creating a shallow-spread nightmare?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Bad — single mega-signal:**
```typescript
form = signal({
    address: { line1: '', line2: '', city: '', ... },
    payment: { cardNumber: '', cvv: '', ... },
    delivery: { method: '', slot: '', ... },
    review: { ... }
});
// Update example: form.update(s => ({ ...s, address: { ...s.address, line1: 'X' } }))
// → gets ugly fast
```

**Good — one signal per step, a derived signal for completion:**
```typescript
address = signal<Address>(initialAddress);
payment = signal<Payment>(initialPayment);
delivery = signal<Delivery>(initialDelivery);

completionStatus = computed(() => ({
    address: !!address().line1 && !!address().city,
    payment: !!payment().cardNumber,
    delivery: !!delivery().method,
    allComplete: /* ... */
}));
```

Updates are localised; only the affected signal re-renders. `computed` auto-recalculates.

**Better — feature-scoped signal store (@ngrx/signals or a hand-rolled service):**
```typescript
@Injectable({ providedIn: 'root' })
export class CheckoutStore {
    private readonly state = signal<CheckoutState>(initial);

    readonly address = computed(() => this.state().address);
    readonly payment = computed(() => this.state().payment);
    readonly completionStatus = computed(() => ({ ... }));

    updateAddress(patch: Partial<Address>) {
        this.state.update(s => ({ ...s, address: { ...s.address, ...patch } }));
    }
    // etc.
}
```

Keeps mutation logic in one place; components consume computed views.

**For truly complex state — Immer or @ngrx/signals:**
```typescript
import { produce } from 'immer';
state.update(s => produce(s, draft => {
    draft.address.line1 = 'X';
    draft.payment.cardNumber = '1234';
}));
```

Immer generates the immutable update automatically.

**Decision tree:**
- ≤ 3 fields → single flat signal, direct spread.
- 5-20 fields, 2-3 nesting levels → split into logical signals + computed aggregators.
- 20+ fields or 4+ nesting levels → signal store + Immer.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"One signal for the whole form." Becomes unmaintainable by step 3 of a checkout.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **signal-based reactive forms** (Angular's signal forms, in development, v19+) — the framework is moving toward native signal integration for forms. Knowing roadmap context signals senior awareness.


</details>

---

**H9.** [MEDIUM] Zoneless Angular — what explicit triggers cause change detection, and where do you need `markForCheck()`?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Automatic triggers in zoneless:**
1. **Signal changes** — `.set()` / `.update()` with a different reference.
2. **AsyncPipe** on an Observable — the pipe internally calls `markForCheck()` on emission.
3. **Router navigation** — triggers CD on the outlet component.
4. **HttpClient responses via async pipe** — same as point 2 via the pipe.

**When you need explicit `markForCheck()`:**
1. **RxJS subscribe outside a pipe** — manually calling `.subscribe()` and updating a non-signal property:
   ```typescript
   this.data$.subscribe(d => {
       this.data = d;             // not a signal, not a pipe
       this.cdr.markForCheck();   // REQUIRED in zoneless
   });
   ```
2. **setTimeout / setInterval with side effects on non-signal state** — no zone to catch it.
3. **Third-party callbacks** (map libraries, charting, WebSocket handlers not wrapped in signals).

**Prefer:** convert those paths to signals. `toSignal(this.data$)` wraps an Observable as a signal and integrates with CD automatically. No manual `markForCheck` needed.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"You never need `markForCheck` in zoneless." False — any legacy Observable subscription pattern requires it.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `afterNextRender()` and `afterRender()` hooks are the zoneless equivalent of `ngAfterViewInit` for post-render DOM work. Zoneless requires a richer lifecycle vocabulary — knowing these shows you've actually built zoneless apps.


</details>

---

**H10.** [HIGH] [OUT-OF-SCOPE] Signal vs BehaviorSubject vs computed — when to use each?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

| Use case | Tool |
|---|---|
| Synchronous, value-oriented state with reactive UI binding | `signal` |
| Derived value from other signals | `computed` |
| Multi-subscriber async event stream (clicks, WebSocket messages) | `BehaviorSubject` / `Subject` |
| Bridge between RxJS and signals | `toSignal()` / `toObservable()` |
| HTTP responses (single emission) | `signal(undefined)` + manual set, or `toSignal(http.get(...))` |
| Effects / side effects from signal changes | `effect()` |

**Key differences:**
- **Signals are pull-based** — you read the current value. They push notifications but dependents pull the value.
- **Observables are push-based** — each emission pushes a value; late subscribers may miss prior emissions (unless BehaviorSubject which caches last).
- **Signals are synchronous** — always have a value. Observables can emit at any time or never.

**In this project:**
- Cart state → `signal<ShoppingCart>` (value-oriented, always one cart).
- SignalR order notification → `BehaviorSubject` subscribed in `SignalRService`, exposed as signal via `toSignal`.
- Computed totals → `computed(() => cart().items.reduce(...))`.
- Side effects (analytics on cart change) → `effect(() => { analytics.track(cart()) })`.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Signals replace RxJS." They don't — streams of events still need observables. Signals replace *state containers* (BehaviorSubject as state).


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `toObservable(signal)` for interop with legacy RxJS code, and that **`effect()` should not have side effects that modify signals** (infinite loop guard) — this is a real footgun in practice.


</details>

---

**H11.** [MEDIUM] Clicking an unrelated field "fixed" the broken button. What does that say about your change detection mental model?

<details>
<summary>The 5% Answer</summary>

- **What happened:** the voucher input triggered an event → Angular re-evaluated template expressions → `completionStatus().address` was re-read → returned `true` (mutation DID update the value) → DOM updated

- **What it tells you:**
  1. **The signal's value was correct the whole time** — the bug was in *notification*, not state
  2. **Zoneless Angular doesn't sweep automatically** — CD only runs on explicit notification; the voucher input firing it was incidental, not guaranteed
  3. **Hidden dependency on another component's CD triggers** — fragile, untestable coupling

- **The deeper lesson:** if a bug is "fixed by interacting with an unrelated element," you're in zoneless-drift territory — the bug isn't gone, it's hidden

- **Diagnostic technique:**
  - `effect(() => console.log('completionStatus:', completionStatus()))` — if this logs, CD ran
  - Logs but DOM doesn't update → template binding issue
  - Doesn't log → signal equality returned "no change" → reference-equality bug


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"I must have fixed it by clicking around." Confirmation bias. The bug is still there; you just stopped triggering it.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **tests should cover exactly this scenario** — setting a signal, then asserting the template updates WITHOUT any intervening event. Tests for zoneless apps need to verify change propagation, not just state.


</details>

---

**H12.** [MEDIUM] [OUT-OF-SCOPE] Angular 17 shipped `signal.mutate()`; v18 removed it. What did it do, why was it a trap, and what does the removal teach about reactive API design?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**What `mutate()` claimed to do:**

```typescript
// Angular 17
const cart = signal({ items: [] });
cart.mutate(c => c.items.push(newItem));  // push directly, signal "knows"
```

The API let you mutate the value in place without returning a new reference, and manually notified dependents that the value had "changed." On paper: ergonomic. No spread gymnastics.

**Why it was a trap:**

1. **It bypassed reference equality — the ONE invariant the rest of the signal ecosystem relied on.** `computed`, `effect`, and the change detector all use `===`. `mutate` told them "trust me, it changed" without changing the reference.

2. **Downstream `computed` signals memoised on reference.** If `computed(() => cart().items.length)` cached against the old array reference, a subsequent `.length` read returned the CACHED value — because the reference was still `===` equal to what was memoised.

3. **RxJS interop (`toObservable`) broke silently.** The observable emits on reference change. `mutate` didn't produce one. Subscribers saw no emission even though the value "changed."

4. **It trained the wrong mental model.** Developers learned "signals are containers I can mutate," generalised that to `signal()....push()` (which doesn't work), and then couldn't debug why their real signal updates didn't re-render.

**Why it was removed (v18):**

The Angular team chose consistency over ergonomics. One rule for signal identity (`===` on the value) is simpler to reason about than "sometimes reference-based, sometimes notification-based." The API surface shrank; the mental model got cleaner.

**The API design lesson:**

- **An optimisation that requires users to violate the system's invariant is not an optimisation — it's a foot-gun.** `mutate` saved three characters of spread syntax at the cost of making the signal model inconsistent.
- **Reactive systems need ONE source of truth for "did it change."** Signals picked `===`. Anything that claims to be a signal update must respect that rule.
- **Removing an API is a legitimate design move.** Teams that ship broken APIs and let them linger to "avoid breaking changes" accumulate a worse kind of debt: confused users.

**Modern replacement:**
```typescript
// v18+ — no mutate, always produce new reference
cart.update(c => ({ ...c, items: [...c.items, newItem] }));
```

Immer or signal-store libraries give you the ergonomic feel without breaking the invariant — they do the immutable update under the hood but present a mutation-style API.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"They removed it because nobody used it." They removed it because it was subtly broken. Popularity wasn't the issue — correctness was.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the same bug class exists in other reactive systems — Vue's `reactive()` proxies mutations automatically (works), MobX observes property access via Proxy (works), but anything that tries to combine "identity-based change detection" with "in-place mutation" will hit the same wall. The lesson generalises: pick a model and enforce it at the API boundary.


</details>

---

### ⚡ Syntax Quick-Fire

**HQ1.** [HIGH] Angular: one-line difference between `signal()`, `computed()`, and `effect()`.

<details>
<summary>The 5% Answer</summary>

- **`signal()`**: writable reactive value container — you `.set()` or `.update()` it.
- **`computed()`**: read-only value derived from other signals — lazy, memoised, recalculates only when dependencies change.
- **`effect()`**: side-effect runner — re-executes its callback whenever signal dependencies change. For DOM integration, logging, analytics. Not for deriving values.

```typescript
const price = signal(50);
const qty = signal(2);

const total = computed(() => price() * qty());  // 100

effect(() => {
    console.log('Total changed:', total());  // runs on init + on each change
});

price.set(75);  // triggers computed recalc + effect re-run
```

- **`effect()` footgun:** modifying a signal inside an `effect` that reads it → infinite loop. Angular throws `NG0600` if detected.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Use `effect()` to derive values." `effect` is for side effects only. Derived values belong in `computed`.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing `effect()` requires an injection context (constructor / field initializer). Calling it in a method outside injection context throws `NG0203`. Use `runInInjectionContext(injector, () => effect(...))` to escape this if needed.

</details>

---

**HQ2.** [HIGH] `inject(Service)` in a class field vs constructor injection — are they equivalent? Where does `inject()` NOT work?

<details>
<summary>The 5% Answer</summary>

- **Functionally equivalent** in components and services — both resolve from the injector at construction time.
- **`inject()` does NOT work:**
  - Inside class methods called after construction
  - Inside `setTimeout` / `setInterval` callbacks
  - Outside an active injection context (any code running after `constructor` completes)

```typescript
@Component({ ... })
export class CheckoutComponent {
    // ✓ Works — field initializer runs during construction
    private cartService = inject(CartService);

    someMethod() {
        // ✗ Throws NG0203 — no injection context
        const s = inject(CartService);
    }
}
```

- **Fix for late injection:** inject in constructor or field, capture as property, use in methods.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "They're exactly the same." The injection context restriction makes them different — `inject()` is runtime context-dependent.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** `runInInjectionContext(this.injector, () => inject(Service))` — escapes the context restriction when you legitimately need late injection. Useful in dynamic component creation and testing harnesses.

</details>

---

**HQ3.** [HIGH] Angular `@if` vs `*ngIf` — what's the practical difference in Angular 17+?

<details>
<summary>The 5% Answer</summary>

- **`@if`** (built-in control flow, v17+): no directive import needed, better SSR hydration, native template syntax, slightly better tree-shaking.
- **`*ngIf`**: legacy structural directive, requires `NgIf` import (or `CommonModule`), still works but deprecated in favour of `@if`.

```html
<!-- Legacy -->
<div *ngIf="isLoggedIn; else loginBlock">Welcome</div>
<ng-template #loginBlock>Please log in</ng-template>

<!-- Modern — cleaner, no import needed -->
@if (isLoggedIn()) {
  <div>Welcome</div>
} @else {
  <div>Please log in</div>
}
```

- `@if` + `@else if` + `@else` replaces the awkward `ngIf` + `ng-template` + `#ref` pattern.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "They're the same." `*ngIf` requires an import; `@if` doesn't. For standalone components, every import matters.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing `@for` also gained a `@empty` block (renders when the collection is empty) — cleaner than `*ngIf="items.length === 0"` as a separate element. Shows you track the Angular changelog.

</details>

---
