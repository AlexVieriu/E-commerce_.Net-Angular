## SECTION G — RAPID-FIRE SYNTAX (30 seconds each)

**G1.** What does `??=` do in C#? Give an example.

<details><summary>Answer</summary>

`??=` is the null-coalescing assignment operator. Assigns the right side only if the left side is null. Example: `name ??= "default";` — if `name` is null, assigns `"default"`; otherwise keeps current value. Equivalent to: `if (name == null) name = "default";`
</details>

---

**G2.** What does `?.` do? What does `!` (null-forgiving operator) do? When is `!` dangerous?

<details><summary>Answer</summary>

`?.` short-circuits to null if the left side is null: `order?.Customer?.Name` returns null instead of throwing. `!` tells the compiler "trust me, this isn't null" — suppresses nullable warning. Dangerous because: if you're wrong, you get a `NullReferenceException` at runtime. `!` hides bugs — use only when you're genuinely certain.
</details>

---

**G3.** What is the difference between `string?` and `string` in a nullable-enabled context?

<details><summary>Answer</summary>

`string` = non-nullable reference type — compiler warns if you assign null. `string?` = nullable — explicitly allows null. In nullable context (`<Nullable>enable</Nullable>`), the compiler tracks null flow and warns about potential `NullReferenceException`. It's a compile-time check only — no runtime difference.
</details>

---

**G4.** What does `init` accessor do on a property? How is it different from `set`?

<details><summary>Answer</summary>

`init` allows the property to be set only during object initialization (constructor or object initializer). After construction, it's read-only. `set` allows modification at any time. `init` enables immutable objects with convenient initialization: `new Order { Id = 1, Status = "New" }` — but `order.Status = "Done"` later won't compile.
</details>

---

**G5.** What does `required` keyword do on a property in C# 11?

<details><summary>Answer</summary>

`required` forces the caller to set the property during initialization — compiler error if omitted. `public required string Name { get; init; }` means `new Person { Name = "Alex" }` is required — `new Person()` won't compile. Replaces constructor parameters for mandatory properties, especially useful with `init` and records.
</details>

---

**G6.** What does `inject()` function do in Angular? How is it different from constructor injection?

<details><summary>Answer</summary>

`inject()` is a function-based alternative to constructor injection (Angular 14+). Works in constructors, field initializers, and injection context (route guards, interceptors). Advantages: works in standalone functions (functional guards/interceptors), enables composition (utility functions that inject services), reduces constructor boilerplate. Limitation: must be called in an injection context.
</details>

---

**G7.** What does `takeUntilDestroyed()` do? Why is it important for preventing memory leaks?

<details><summary>Answer</summary>

`takeUntilDestroyed()` (Angular 16+) automatically completes an observable when the component/directive is destroyed. Prevents memory leaks from long-lived subscriptions (WebSockets, intervals, Subjects). Usage: `this.data$.pipe(takeUntilDestroyed()).subscribe(...)`. Must be called in injection context (constructor or field initializer). Replaces the manual `ngOnDestroy` + `Subject` pattern.
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
