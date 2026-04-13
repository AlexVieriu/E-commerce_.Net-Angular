## SECTION G — RAPID-FIRE SYNTAX (30 seconds each)

**G1.** What does `??=` do in C#? Give an example.

<details><summary>Answer</summary>

`??=` is the **null-coalescing assignment** operator — assigns the right side only if the left side is `null`.

```csharp
name ??= "default";
// Equivalent to: if (name == null) name = "default";
```

Useful for lazy initialization of nullable fields.
</details>

---

**G2.** What does `?.` do? What does `!` (null-forgiving operator) do? When is `!` dangerous?

<details><summary>Answer</summary>

- **`?.`** (null-conditional) — short-circuits to `null` if the left side is `null`:
  ```csharp
  string? city = order?.Customer?.Address?.City; // null instead of NullReferenceException
  ```
- **`!`** (null-forgiving) — tells the compiler "trust me, this isn't null." Suppresses the nullable warning but provides **no runtime safety**.

**When `!` is dangerous:** if you're wrong about null, you get a `NullReferenceException` at runtime with no compile-time warning. Use only when you are genuinely certain.
</details>

---

**G3.** What is the difference between `string?` and `string` in a nullable-enabled context?

<details><summary>Answer</summary>

- **`string`** — non-nullable; compiler warns if you assign `null` or pass it where null may occur.
- **`string?`** — explicitly allows `null`; compiler tracks null flow and warns before dereferencing.

Requires `<Nullable>enable</Nullable>` in `.csproj`. **Compile-time analysis only** — no runtime difference between `string` and `string?`.
</details>

---

**G4.** What does `init` accessor do on a property? How is it different from `set`?

<details><summary>Answer</summary>

`init` allows a property to be set **only during object initialization** (constructor or object initializer). After construction it becomes read-only. `set` allows modification at any time.

```csharp
public class Order
{
    public int Id { get; init; }
    public string Status { get; init; }
}

var order = new Order { Id = 1, Status = "New" }; // ✅
order.Status = "Done"; // ❌ CS8852: init-only property
```
</details>

---

**G5.** What does `required` keyword do on a property in C# 11?

<details><summary>Answer</summary>

`required` forces the caller to set the property during object initialization — **compiler error if omitted**.

```csharp
public class Person
{
    public required string Name { get; init; }
}

new Person { Name = "Alex" }; // ✅
new Person();                 // ❌ CS9035: required member 'Name' must be set
```

Replaces constructor parameters for mandatory properties — especially useful with `record` and `init`.
</details>

---

**G6.** What does `inject()` function do in Angular? How is it different from constructor injection?

<details><summary>Answer</summary>

`inject()` is a functional alternative to constructor injection (Angular 14+).

```typescript
// Constructor injection
constructor(private router: Router) {}

// inject() — works anywhere in injection context
readonly router = inject(Router);
```

**Advantages:**
- Works in field initializers, functional guards, and interceptors — not just constructors.
- Enables composable utility functions that inject services internally.
- Reduces constructor boilerplate with many dependencies.

**Limitation:** must be called during construction (in an injection context).
</details>

---

**G7.** What does `takeUntilDestroyed()` do? Why is it important for preventing memory leaks?

<details><summary>Answer</summary>

`takeUntilDestroyed()` (Angular 16+) automatically completes a subscription when the component/directive is destroyed.

```typescript
// Before — manual cleanup
private destroy$ = new Subject<void>();
ngOnInit() { this.data$.pipe(takeUntil(this.destroy$)).subscribe(...); }
ngOnDestroy() { this.destroy$.next(); this.destroy$.complete(); }

// After — zero boilerplate
constructor() {
    this.data$.pipe(takeUntilDestroyed()).subscribe(...);
}
```

**Why it prevents memory leaks:** without cleanup, long-lived observables (WebSockets, intervals, `Subject`) keep running after the component is destroyed — holding references in memory and causing errors on destroyed UI. Must be called in an injection context.
</details>

---

**G8.** Write a one-liner: convert `string[] names` to a comma-separated string of names longer than 3 characters, sorted alphabetically.

<details><summary>Answer</summary>

```csharp
string result = string.Join(", ", names.Where(n => n.Length > 3).OrderBy(n => n));
```

Filter with `Where`, sort with `OrderBy`, join with `string.Join`. `OrderBy` is alphabetical by default for strings.
</details>

---
