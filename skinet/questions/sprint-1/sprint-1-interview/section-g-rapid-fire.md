## SECTION G — RAPID-FIRE SYNTAX (30 seconds each)

**G1.** What does `??=` do in C#? Give an example.

<details><summary>Answer</summary>

`??=` is the null-coalescing assignment operator. Assigns the right side only if the left side is null. Example: `name ??= "default";` — if `name` is null, assigns `"default"`; otherwise keeps current value. Equivalent to: `if (name == null) name = "default";`
</details>

<details><summary>Deep Dive</summary>

**When it's most useful**

Lazy initialisation — create the value only when you need it and only once:

```csharp
private List<string>? _errors;
public List<string> Errors => _errors ??= new List<string>();

// Instead of:
public List<string> Errors
{
    get
    {
        if (_errors == null) _errors = new List<string>();
        return _errors;
    }
}
```

**Chaining**

```csharp
a ??= b ??= c ??= "default";
// Assign "default" to c if null, then c to b if null, then b to a if null
// Evaluated right-to-left
```

**`??` vs `??=`**

```csharp
string name = existing ?? "default";    // ?? returns right side if left is null — doesn't assign
name ??= "default";                     // ??= assigns right side to left if left is null
```

`??` is for expressions, `??=` is for mutation. Don't confuse them.

**Not limited to strings**

```csharp
List<int>? items = null;
items ??= new List<int>();   // works with any nullable reference or value type
int? count = null;
count ??= 0;                 // works with nullable value types
```
</details>

---

**G2.** What does `?.` do? What does `!` (null-forgiving operator) do? When is `!` dangerous?

<details><summary>Answer</summary>

`?.` short-circuits to null if the left side is null: `order?.Customer?.Name` returns null instead of throwing. `!` tells the compiler "trust me, this isn't null" — suppresses nullable warning. Dangerous because: if you're wrong, you get a `NullReferenceException` at runtime. `!` hides bugs — use only when you're genuinely certain.
</details>

<details><summary>Deep Dive</summary>

**`?.` — the null-conditional operator**

```csharp
// Without ?.
if (order != null && order.Customer != null)
    Console.WriteLine(order.Customer.Name);

// With ?.
Console.WriteLine(order?.Customer?.Name); // null if any step is null
```

Works on method calls too: `order?.GetTotal()` returns `null` (as `decimal?`) if `order` is null.

Works with indexers: `list?[0]` returns null if `list` is null.

**`!` — the null-forgiving operator**

```csharp
string? name = GetNameOrNull();
int length = name!.Length; // tells compiler: I know name is not null here
```

The `!` is COMPILE-TIME ONLY. It suppresses the `CS8602` warning but adds ZERO runtime null check. If `name` is actually null, you get `NullReferenceException` — same as if nullable analysis didn't exist.

**When `!` is legitimate**

```csharp
// Pattern: just validated above, compiler doesn't track it
if (name == null) throw new ArgumentNullException(nameof(name));
return name!.Length; // safe: we threw above

// Pattern: DI-injected in constructor, always non-null in practice
private readonly IOrderRepository _repo = null!;
// (assigned by DI in constructor body — null! suppresses the uninitialized warning)
```

**When `!` is dangerous**

```csharp
var order = orders.FirstOrDefault(o => o.Id == id);
return order!.Total; // if no order found, FirstOrDefault returns null → NullReferenceException
```

This compiles cleanly, passes all static analysis, and crashes in production. The correct fix is a null check: `if (order == null) throw new NotFoundException(id);`.

Rule: before typing `!`, ask "what happens if this IS null?" If the answer is "it crashes," write the null check instead.
</details>

---

**G3.** What is the difference between `string?` and `string` in a nullable-enabled context?

<details><summary>Answer</summary>

`string` = non-nullable reference type — compiler warns if you assign null. `string?` = nullable — explicitly allows null. In nullable context (`<Nullable>enable</Nullable>`), the compiler tracks null flow and warns about potential `NullReferenceException`. It's a compile-time check only — no runtime difference.
</details>

<details><summary>Deep Dive</summary>

**How to enable in a project**

```xml
<!-- .csproj -->
<PropertyGroup>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

Once enabled, the compiler performs null flow analysis on every method.

**Flow analysis — the compiler is smart**

```csharp
string? name = GetName(); // might be null

Console.WriteLine(name.Length); // CS8602 warning: might be null

if (name != null)
{
    Console.WriteLine(name.Length); // no warning — compiler tracked the null check
}

Console.WriteLine(name?.Length ?? 0); // no warning — ?. handles null
```

**Reference types vs value types**

For VALUE types (int, bool, DateTime), `?` means "can be unset" — `int?` is `Nullable<int>` with a different runtime representation. For REFERENCE types, `string?` vs `string` is compile-time annotation only — both are the same `string` at runtime. The CLR doesn't enforce reference nullability.

**DTOs and EF models — common gotcha**

```csharp
// EF required property — non-nullable in C# maps to NOT NULL in DB
public class Product
{
    public string Name { get; set; } = string.Empty; // required, non-null
    public string? Description { get; set; }          // optional, nullable
}
```

Without the `= string.Empty` initializer, EF/C# warns "non-nullable property not initialized." Use `= string.Empty` for required strings, `string?` for optional ones. This maps correctly to DB schema via EF conventions.
</details>

---

**G4.** What does `init` accessor do on a property? How is it different from `set`?

<details><summary>Answer</summary>

`init` allows the property to be set only during object initialization (constructor or object initializer). After construction, it's read-only. `set` allows modification at any time. `init` enables immutable objects with convenient initialization: `new Order { Id = 1, Status = "New" }` — but `order.Status = "Done"` later won't compile.
</details>

<details><summary>Deep Dive</summary>

**`init` vs `set` — the full comparison**

```csharp
public class Order
{
    public int Id { get; init; }     // set once at construction, then immutable
    public string Status { get; set; } // mutable any time
}

var order = new Order { Id = 1, Status = "Pending" }; // both work here
order.Status = "Shipped"; // compiles — set
order.Id = 2;             // CS8852 error — init only
```

**Why `init` matters for immutability**

Without `init`, you have two choices: `get; set;` (mutable anywhere) or `get;` with constructor-only assignment (forces constructor parameters). `init` gives you a third option: constructor OR object initializer, but never later mutation.

```csharp
// Before init: forced to use constructor for immutable properties
public class Address
{
    public string Street { get; }
    public string City { get; }
    public Address(string street, string city) { Street = street; City = city; }
}
var a = new Address("123 Main", "London"); // constructor only

// With init: can use object initializer
public class Address
{
    public string Street { get; init; }
    public string City { get; init; }
}
var a = new Address { Street = "123 Main", City = "London" }; // cleaner
```

**`init` + `required` (C# 11)**

```csharp
public class CreateOrderDto
{
    public required string BuyerEmail { get; init; }
    public required string CartId { get; init; }
    public string? CouponCode { get; init; } // optional
}

// Compiler error if required properties are omitted:
var dto = new CreateOrderDto { BuyerEmail = "alice@test.com" }; // CS9035: CartId required
```

**Records use `init` by default**

```csharp
public record Product(int Id, string Name, decimal Price);
// Equivalent to:
public class Product
{
    public int Id { get; init; }
    public string Name { get; init; }
    public decimal Price { get; init; }
    // + Equals, GetHashCode, ToString generated
}
```

Records are value-semantic immutable types by default. Use them for DTOs, value objects, and response models.
</details>

---

**G5.** What does `required` keyword do on a property in C# 11?

<details><summary>Answer</summary>

`required` forces the caller to set the property during initialization — compiler error if omitted. `public required string Name { get; init; }` means `new Person { Name = "Alex" }` is required — `new Person()` won't compile. Replaces constructor parameters for mandatory properties, especially useful with `init` and records.
</details>

<details><summary>Deep Dive</summary>

**Before `required` — the problem**

```csharp
public class CreateOrderDto
{
    public string BuyerEmail { get; set; } = string.Empty; // silently defaults to ""
    public string CartId { get; set; } = string.Empty;
}

// No compiler error, but creates a useless DTO:
var dto = new CreateOrderDto(); // BuyerEmail = "", CartId = "" — invalid, but compiles
```

**With `required`**

```csharp
public class CreateOrderDto
{
    public required string BuyerEmail { get; init; }
    public required string CartId { get; init; }
    public string? CouponCode { get; init; } // optional — no required
}

var dto = new CreateOrderDto(); // CS9035: Required member 'BuyerEmail' must be set
var dto = new CreateOrderDto { BuyerEmail = "alice@test.com" }; // CS9035: CartId required
var dto = new CreateOrderDto { BuyerEmail = "alice@test.com", CartId = "cart-1" }; // OK
```

**`required` vs constructor parameters**

```csharp
// Constructor approach — enforces at compile time but verbose
public class CreateOrderDto
{
    public string BuyerEmail { get; }
    public string CartId { get; }
    public CreateOrderDto(string buyerEmail, string cartId) { BuyerEmail = buyerEmail; CartId = cartId; }
}

// required approach — same enforcement, cleaner syntax
public class CreateOrderDto
{
    public required string BuyerEmail { get; init; }
    public required string CartId { get; init; }
}
```

`required` wins when you have many optional + required properties and don't want a constructor with 10 parameters.

**`[SetsRequiredMembers]` — bypass for existing constructors**

```csharp
public class CreateOrderDto
{
    public required string BuyerEmail { get; init; }

    [SetsRequiredMembers]
    public CreateOrderDto(string buyerEmail) { BuyerEmail = buyerEmail; }
    // Tells compiler: this constructor satisfies all required members
}

var dto = new CreateOrderDto("alice@test.com"); // OK — [SetsRequiredMembers]
```

Used when you want both constructor initialisation and object initialiser support.

**Model binding and `required`**

ASP.NET Core model binding respects `required` for request validation — if the JSON doesn't include a required field, model validation fails with a 400. Works alongside `[Required]` DataAnnotation but is enforced at compile time (not just runtime).
</details>

---

**G6.** What does `inject()` function do in Angular? How is it different from constructor injection?

<details><summary>Answer</summary>

`inject()` is a function-based alternative to constructor injection (Angular 14+). Works in constructors, field initializers, and injection context (route guards, interceptors). Advantages: works in standalone functions (functional guards/interceptors), enables composition (utility functions that inject services), reduces constructor boilerplate. Limitation: must be called in an injection context.
</details>

<details><summary>Deep Dive</summary>

**The injection context rule**

`inject()` can only be called during component/directive construction or in a framework-provided injection context (interceptors, guards). Calling it in `ngOnInit`, `ngOnChanges`, or any lifecycle hook outside of construction throws:

```
RuntimeError: NG0203: inject() must be called from an injection context such as
a constructor, a factory function, a field initializer, or a function used with
`runInInjectionContext`.
```

**`runInInjectionContext` — the escape hatch**

```typescript
export class MyComponent {
  private injector = inject(Injector);

  ngOnInit() {
    // Needs a service but ngOnInit isn't an injection context
    runInInjectionContext(this.injector, () => {
      const service = inject(MyService); // OK inside runInInjectionContext
      service.doSomething();
    });
  }
}
```

Rarely needed — restructure your code to inject in the constructor/field instead.

**Composable "hooks" pattern**

```typescript
// Reusable utility that injects its own dependencies
function usePaginatedProducts(page: Signal<number>) {
  const productService = inject(ProductService);
  const router = inject(Router);

  const products = computed(() =>
    productService.getPage(page())
  );

  function navigate(newPage: number) {
    router.navigate([], { queryParams: { page: newPage } });
  }

  return { products, navigate };
}

// Component — clean, no direct service injection needed
export class ProductListComponent {
  page = signal(1);
  { products, navigate } = usePaginatedProducts(this.page);
}
```

This pattern (inspired by React hooks) is only possible with `inject()` — constructor injection can't be composed into utility functions.

**When to prefer constructor injection**

If you're building a service (not a component), class-based constructor injection is still the standard — it makes dependencies explicit and visible:

```typescript
@Injectable({ providedIn: 'root' })
export class OrderService {
  constructor(
    private http: HttpClient,
    private authService: AuthService
  ) {} // clear dependency list
}
```

For services, constructor injection wins on readability. For components, `inject()` wins on ergonomics.
</details>

---

**G7.** What does `takeUntilDestroyed()` do? Why is it important for preventing memory leaks?

<details><summary>Answer</summary>

`takeUntilDestroyed()` (Angular 16+) automatically completes an observable when the component/directive is destroyed. Prevents memory leaks from long-lived subscriptions (WebSockets, intervals, Subjects). Usage: `this.data$.pipe(takeUntilDestroyed()).subscribe(...)`. Must be called in injection context (constructor or field initializer). Replaces the manual `ngOnDestroy` + `Subject` pattern.
</details>

<details><summary>Deep Dive</summary>

**The problem it solves**

```typescript
// BUG — subscription outlives the component
export class NotificationComponent implements OnInit {
  ngOnInit() {
    this.signalRService.notifications$.subscribe(n => {
      this.notifications.push(n); // component might be destroyed by the time this fires
    });
    // No cleanup → subscription runs forever → memory leak
  }
}
```

**Old pattern (pre-Angular 16)**

```typescript
export class NotificationComponent implements OnDestroy {
  private destroy$ = new Subject<void>();

  ngOnInit() {
    this.notifications$.pipe(
      takeUntil(this.destroy$)
    ).subscribe(n => this.notifications.push(n));
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
```

5 lines of boilerplate per component. Easy to forget `ngOnDestroy`. `destroy$` is often duplicated across 20 components.

**New pattern (Angular 16+)**

```typescript
export class NotificationComponent {
  constructor() {
    this.notificationService.notifications$.pipe(
      takeUntilDestroyed()
    ).subscribe(n => this.notifications.push(n));
  }
}
```

Zero boilerplate. Angular automatically calls `DestroyRef.onDestroy()` which completes the observable.

**Using `DestroyRef` explicitly for ngOnInit subscriptions**

```typescript
export class NotificationComponent implements OnInit {
  private destroyRef = inject(DestroyRef); // injection context (field initializer)

  ngOnInit() {
    this.notifications$.pipe(
      takeUntilDestroyed(this.destroyRef) // pass explicit ref
    ).subscribe(...);
  }
}
```

**What constitutes a memory leak in Angular**

A leak means the garbage collector cannot reclaim memory because something holds a reference to it. With a live subscription:
- The observable holds a reference to the subscriber callback
- The callback closes over `this` (the component)
- The component's memory cannot be freed even after it's removed from the DOM

On a page with 10 navigations in and out of the same component, you have 10 ghost component instances consuming memory and potentially writing to the DOM.

**`toSignal()` — the cleanest option**

```typescript
notifications = toSignal(this.notificationService.notifications$, { initialValue: [] });
// Automatically subscribes + unsubscribes + converts to signal
// No takeUntilDestroyed, no subscribe, no async pipe
```

For read-only template bindings, `toSignal` is better than `takeUntilDestroyed` + `subscribe`.
</details>

---

**G8.** Write a one-liner: convert `string[] names` to a comma-separated string of names longer than 3 characters, sorted alphabetically.

<details><summary>Answer</summary>

```csharp
string result = string.Join(", ", names.Where(n => n.Length > 3).OrderBy(n => n));
```

Filter with `Where`, sort with `OrderBy`, join with `string.Join`. `OrderBy` is alphabetical by default for strings.
</details>

<details><summary>Deep Dive</summary>

**Breaking it down**

```csharp
names
  .Where(n => n.Length > 3)   // filter: keep names longer than 3 chars
  .OrderBy(n => n)             // sort: alphabetical (uses string comparison)
  // → IEnumerable<string>
string.Join(", ", ...)         // join: concatenate with ", " separator
```

**`OrderBy` string comparison — culture matters**

```csharp
.OrderBy(n => n)                                        // culture-sensitive (current culture)
.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)      // case-insensitive, fast
.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase) // locale-aware, case-insensitive
```

For UI display, use `CurrentCultureIgnoreCase`. For sorting IDs or keys, use `Ordinal`.

**Alternative: method chain vs query syntax**

```csharp
// Method chain (above)
string.Join(", ", names.Where(n => n.Length > 3).OrderBy(n => n));

// Query syntax — more readable for complex queries
string result = string.Join(", ",
    from n in names
    where n.Length > 3
    orderby n
    select n);
```

Same result. Query syntax compiles to the same method chain. Prefer method chain for simple pipelines, query syntax for joins or complex projections.

**Defensive version — handle empty/null**

```csharp
string result = names is null or { Length: 0 }
    ? string.Empty
    : string.Join(", ", names.Where(n => n?.Length > 3).OrderBy(n => n));
```

In production code, `names` could be null (from a null DTO property) and individual names could be null (if the list is `string?[]`). Guard both.

**Performance: `string.Join` vs `StringBuilder`**

For small arrays (< 100 items), `string.Join` is fine — it allocates once. For large arrays, `StringBuilder` with manual iteration is faster. Rule: use `string.Join` until you have profiler evidence it's a bottleneck.
</details>

---
