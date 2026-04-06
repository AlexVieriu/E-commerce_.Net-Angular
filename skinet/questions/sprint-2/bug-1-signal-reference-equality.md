---
name: BUG-1 — Angular signals + reference equality
description: Why mutating a signal object doesn't trigger re-render, and how spread operator fixes it
type: feedback
---

## Why this approach

Angular signals (introduced in v16, default in v17+) use **reference equality** (`===`) to detect changes. If you update a signal by mutating the existing object and returning the same reference, Angular sees `old === new` and skips re-rendering. This is a common gotcha when migrating from zone-based Angular where mutation was fine because the zone caught everything.

---

## What confused me

- The button stayed disabled even though the signal value was correct in memory
- Clicking an unrelated field (voucher input) would suddenly enable the button
- The code "looked" like it was updating the signal correctly

---

## Explanation

### What is a Signal?

A signal is a reactive value container. When you read it in a template (`completionStatus().address`), Angular tracks that dependency. When the signal changes, Angular re-renders only the affected parts.

```typescript
const count = signal(0);
count.set(1);      // triggers re-render
count.update(v => v + 1); // triggers re-render
```

### How Angular detects "changed"

Angular uses strict reference equality (`===`) to compare the old and new signal value:

```typescript
const a = { x: 1 };
const b = a;        // same reference
b.x = 2;
a === b;            // true — same object in memory
```

```typescript
const a = { x: 1 };
const b = { ...a, x: 2 };  // new object
a === b;            // false — different objects
```

### The bug

```typescript
// BAD — mutates existing object, returns same reference
this.completionStatus.update(state => {
    state.address = true;  // mutates in place
    return state;          // same object → === old value → no re-render
});
```

Memory:
```
Before: state @ 0x001 = { address: false, card: false, delivery: false }
After:  state @ 0x001 = { address: true,  card: false, delivery: false }
Angular: 0x001 === 0x001 → no change → button stays disabled
```

### The fix

```typescript
// GOOD — spread creates a new object
this.completionStatus.update(state => ({ ...state, address: true }));
```

Memory:
```
Before: state @ 0x001 = { address: false, card: false, delivery: false }
After:  state @ 0x002 = { address: true,  card: false, delivery: false }
Angular: 0x001 !== 0x002 → change detected → button enables
```

### Why clicking the voucher "fixed" it

The voucher input triggered a full change detection cycle (via a different mechanism). During that cycle, Angular re-evaluated all template expressions including `completionStatus().address`, found it was `true`, and updated the DOM. The signal itself was correct in memory the whole time — Angular just never knew to re-read it.

### Spread operator recap

```typescript
const original = { a: 1, b: 2, c: 3 };
const copy = { ...original, b: 99 };
// copy = { a: 1, b: 99, c: 3 } — new object, original unchanged
```

### Zone.js vs Zoneless

In classic Angular (zone.js), a global monkey-patch catches all async events (clicks, timers, HTTP) and triggers change detection automatically. Mutation + zone = works.

In zoneless Angular (v18+, this project uses it), there is no global listener. Only signal changes, `markForCheck()`, or `ChangeDetectorRef.detectChanges()` trigger re-renders. Mutation without a new reference = silent failure.

---

## Exercises

**Exercise 1 — Spot the bug**
Which of these correctly updates a signal?

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

**Exercise 2 — Nested objects**
This signal has a nested object. What's wrong?

```typescript
const form = signal({ user: { name: '', email: '' }, submitted: false });

// Update name
form.update(s => {
    s.user.name = 'Alex';
    return { ...s };
});
```

Hint: does spreading `s` create a new `user` object?

**Exercise 3 — Array signals**
Fix this:

```typescript
const items = signal<string[]>([]);

// Add item — broken
items.update(arr => {
    arr.push('new item');
    return arr;
});
```

**Exercise 4 — Write it from scratch**
Given this signal:

```typescript
const checkout = signal<{
    address: boolean;
    payment: boolean;
    delivery: boolean;
}>({ address: false, payment: false, delivery: false });
```

Write a function `markAddressComplete()` that sets `address` to `true` without affecting the other fields, correctly triggering re-render.

**Exercise 5 — When does it matter?**
You have `const count = signal(0)`. You call `count.update(v => v)` (returns same primitive value 0).
Does Angular re-render? Why or why not?

---

## Solutions

**Exercise 1:** B, C, and D are all correct. A is wrong — mutates and returns same reference.
- B: spread in update callback ✓
- C: spread outside, then set ✓  
- D: `Object.assign({}, s, ...)` creates a new object ✓

**Exercise 2:** Shallow spread bug.
```typescript
form.update(s => {
    s.user.name = 'Alex';  // mutates s.user — still same reference!
    return { ...s };       // new outer object, but s.user is same reference
});
```
Fix — deep spread the nested object:
```typescript
form.update(s => ({ ...s, user: { ...s.user, name: 'Alex' } }));
```

**Exercise 3:** `push` mutates the array in place.
```typescript
// Fix A — spread into new array
items.update(arr => [...arr, 'new item']);

// Fix B — concat returns new array
items.update(arr => arr.concat('new item'));
```

**Exercise 4:**
```typescript
function markAddressComplete() {
    checkout.update(state => ({ ...state, address: true }));
}
```

**Exercise 5:** No re-render. Primitives use `===` too: `0 === 0` is `true`. Angular sees no change. Only if you return a different value (e.g. `v + 1` when `v = 0` → `1 !== 0`) does it trigger re-render.

---

## Interview Q&A

**Q: Why did mutating a signal object not trigger re-render in Angular?**
Angular signals use reference equality (`===`) to detect changes. Mutating an object and returning the same reference means old and new are the same object in memory — Angular sees no change and skips re-rendering. Fix: return a new object with spread (`{ ...state, field: value }`).

**Q: What's the difference between zone.js and zoneless Angular for change detection?**
Zone.js monkey-patches async APIs globally and triggers change detection after every event. Zoneless Angular relies entirely on signals and explicit triggers — mutations without new references are silently ignored. Zoneless is more performant but requires disciplined immutable updates.

**Q: When would `signal.update(v => v)` trigger a re-render?**
Only if `v` is a primitive with a different value, or a different object reference. If `v` is the same primitive or same object reference, Angular skips re-render. For primitives: `signal(0).update(v => v)` → no re-render. For objects: `signal({}).update(v => v)` → no re-render.

---

## Related incident — Checkout success spinner

### What happened

After completing a payment, the checkout success page showed a spinner forever:
```
Order processing... please wait
Loading order...
Your payment has been received. We are creating the order
```

### Root cause

The success page template waits for `signalrService.orderSignal()` to be set:

```html
@if(signalrService.orderSignal(); as order) { ... show order ... }
@if(!signalrService.orderSignal()) { ... show spinner ... }
```

`orderSignal` is set by the SignalR service when it receives an `OrderCompleteNotification` from the backend. That notification is sent by `HandlePaymentIntentSucceeded` in `PaymentsController` when the Stripe webhook fires.

The full chain:
```
Stripe payment succeeds
  → Stripe sends payment_intent.succeeded to /api/payments/webhook
  → HandlePaymentIntentSucceeded runs
  → Finds order by PaymentIntentId
  → Updates order status to PaymentReceived
  → Sends SignalR notification to buyer's connection
  → Angular sets orderSignal()
  → Spinner resolves, order displayed
```

### Why it was broken

The webhook was returning 500 (`Invalid signature`) because the `StripeSettings:WhSecret` in user secrets was stale — it was set before `stripe listen` was restarted, so the secret didn't match what the CLI was using to sign events.

**User secrets are loaded at startup only.** Hot reload (`dotnet watch`) does NOT reload user secrets. So after updating the secret, the API must be fully restarted (Ctrl+C → `dotnet watch`) for the new value to take effect.

### Fix

1. Copy the `whsec_...` from the `stripe listen` terminal output
2. `dotnet user-secrets set "StripeSettings:WhSecret" "<whsec_>" --project src/API`
3. Fully restart the API (not just hot reload)
4. Webhook returns 200 → SignalR fires → spinner resolves

### Key lesson

Always restart the API (not hot reload) after changing user secrets. The config provider chain loads once at startup — `IConfiguration` is not live-reloaded.
