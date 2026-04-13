## SECTION A — C# / .NET FUNDAMENTALS

### EASY

**A1.** What is the difference between `readonly` and `const` in C#? When would you use each?

<details><summary>Answer</summary>

- **`const`** — compile-time, implicitly static, must be assigned at declaration, only works for primitives/strings.
- **`readonly`** — runtime, can be set in the constructor, works for any type.

Use `const` for true constants (e.g. `Math.PI`), `readonly` for values set at construction (config, injected dependencies).
</details>

<details><summary>Deep Dive</summary>

**`const` — compiled away entirely**

When you write `const int Max = 100;`, the compiler replaces every usage of `Max` with the literal `100` at compile time. The constant doesn't even exist at runtime in IL — it's just a number burned into the bytecode. This means:

- **Binary breaking change risk**: if a NuGet library you publish changes `const int Max = 100` to `const int Max = 200`, consumers who compiled against the old version still see `100` — they must recompile. This is why public API constants are dangerous.
- Works only for: primitives (`int`, `double`, `bool`), `string`, `enum` values, and `null` for reference types.
- `const` is implicitly `static` — you never write `static const`, just `const`.

**`readonly` — enforced at runtime by the CLR**

`readonly` fields are set once — either at declaration or in a constructor (including multiple constructors). After the constructor finishes, the CLR enforces immutability.

```csharp
public class Config
{
    public readonly List<string> Hosts;  // reference is readonly, list content is NOT

    public Config(bool isProduction)
    {
        Hosts = isProduction
            ? new List<string> { "prod.api.com" }
            : new List<string> { "localhost" };
    }
}
```

**Critical trap**: `readonly` on a reference type only locks the *reference*, not the *object*. You can't do `config.Hosts = new List<string>()`, but you *can* do `config.Hosts.Add("evil.com")`. For true deep immutability, use `IReadOnlyList<T>` as the field type.

**`static readonly` vs `const` — the practical choice for strings**

```csharp
// BAD: binary breaking change across assemblies
public const string DefaultScheme = "https";

// GOOD: resolved at runtime, safe to change
public static readonly string DefaultScheme = "https";
```

For cross-assembly constants (public APIs, NuGet packages), prefer `static readonly`. For private or sealed internal values, `const` is fine and marginally faster.

**When `readonly` matters for DI**

Constructor injection into `readonly` fields is the standard DI pattern:

```csharp
public class OrderService
{
    private readonly IOrderRepository _repo;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IOrderRepository repo, ILogger<OrderService> logger)
    {
        _repo = repo;
        _logger = logger;
    }
}
```

This guarantees the service is always fully initialized — no accidental null at runtime from a missed assignment.
</details>

---

**A2.** What does `sealed` do on a class? Why would you seal a class?

<details><summary>Answer</summary>

`sealed` prevents inheritance. Reasons to use it:
- **Security** — prevent malicious subclass overrides.
- **Performance** — JIT can devirtualize and inline calls.
- **Design clarity** — signals the class is not designed for extension.

Many framework types are sealed — e.g. `string`.
</details>

<details><summary>Deep Dive</summary>

**How `sealed` enables JIT devirtualization**

Virtual method calls require an indirect dispatch through the vtable (virtual method table) — the JIT can't inline them because it doesn't know the concrete type at compile time. When a class is sealed, the JIT *knows* exactly which method body will be called, so it can:
1. Replace the indirect vtable call with a direct call.
2. Inline the method body entirely if it's small.

This can have a meaningful impact in tight loops. Benchmarks have shown 2–5× speedup on sealed types in hot paths, because inlining enables further optimizations like constant folding and register allocation.

**Why `string` is sealed**

`string` caches its hash code after the first call to `GetHashCode()`. If `string` weren't sealed, a subclass could override the contents *after* the hash was cached — breaking hash-based data structures like `Dictionary<string, T>` (wrong bucket, lost data). Sealing prevents this class of bug.

**Security example in ASP.NET**

Consider a permission-checking class:

```csharp
public class PermissionChecker
{
    public virtual bool CanDelete(User user) => user.IsAdmin;
}
```

Without `sealed`, an attacker who controls deserialization (e.g., polymorphic JSON) could substitute a subclass that always returns `true`. Sealing the class prevents any such override.

**Design signal: "we tested the inheritance surface, not the extension points"**

When you don't seal a class, you implicitly promise that overriding its virtuals is safe. `sealed` is a contract: "there are no designed extension points — overriding would break invariants." This is why most DTOs, record types, and value-object classes should be sealed.

**`sealed` on methods (not just classes)**

You can seal an individual method in a derived class to prevent further overriding in subclasses:

```csharp
public class Base { public virtual void Process() { } }

public class Derived : Base
{
    public sealed override void Process() { /* finalized here */ }
}
```

`ThirdLevel : Derived` cannot override `Process` — the chain stops at `Derived`.

**Real-world framework examples**

- `string`, `StringBuilder`, `Int32`, `Guid` — all sealed.
- ASP.NET Core's `ControllerBase` is NOT sealed (designed for extension).
- Most EF Core internal types are sealed.
- Rule of thumb: if you're writing a class that is not designed as a base class, seal it. The future maintainers will thank you.
</details>

---

**A3.** What is the difference between `IEnumerable<T>` and `IList<T>`? When do you choose one over the other?

<details><summary>Answer</summary>

- **`IEnumerable<T>`** — read-only, forward-only, lazy — supports deferred execution.
- **`IList<T>`** — adds indexing, `Count`, `Add`/`Remove` — materialized in memory.

Return `IEnumerable<T>` when consumers only iterate; use `IList<T>` when you need random access or mutation.
</details>

<details><summary>Deep Dive</summary>

**The interface hierarchy**

```
IEnumerable<T>           — MoveNext() + Current only. Forward, read-only, one pass.
  └─ ICollection<T>      — Count, Contains, Add, Remove, CopyTo
       └─ IList<T>        — indexer [i], IndexOf, Insert, RemoveAt
```

`IReadOnlyList<T>` is a separate branch: indexer + Count, but no mutation. Use this when you want to expose indexed access without allowing callers to modify the collection.

**The double-enumeration trap (critical)**

`IEnumerable<T>` is a *recipe* for producing values, not the values themselves. Every time you enumerate it, the recipe runs again:

```csharp
// GetOrdersFromDatabase() actually returns IQueryable<Order> (EF DbSet)
// assigned to IEnumerable<Order> — the query is NOT executed yet
IEnumerable<Order> orders = dbContext.Orders.Where(o => o.IsActive);

int count = orders.Count();       // executes: SELECT COUNT(*) FROM Orders WHERE IsActive=1
var first = orders.First();       // executes: SELECT TOP 1 * FROM Orders WHERE IsActive=1
foreach (var o in orders) { }    // executes: SELECT * FROM Orders WHERE IsActive=1
```

Three separate SQL queries for the same data. The fix: materialize with `.ToList()` first:

```csharp
List<Order> orders = dbContext.Orders.Where(o => o.IsActive).ToList();  // one query
int count = orders.Count;        // in-memory
var first = orders.First();      // in-memory
foreach (var o in orders) { }   // in-memory
```

Note: the double-enumeration trap only applies when the underlying type is `IQueryable<T>` (EF/database). Pure in-memory `IEnumerable<T>` (e.g., a `List<T>`) just re-iterates in memory — slower but not a DB round-trip.

**When lazy IEnumerable is powerful**

Lazy evaluation means you can compose an infinite sequence without allocating anything:

```csharp
IEnumerable<int> Fibonacci()
{
    int a = 0, b = 1;
    while (true)
    {
        yield return a;
        (a, b) = (b, a + b);
    }
}

var first10 = Fibonacci().Take(10).ToList(); // only computes 10 values
```

No array, no pre-allocation — the values are computed on demand.

**Liskov Substitution in API design**

Accept the most general type you need; return the most specific type your caller will use:

```csharp
// BAD: forces caller to create a List even when they have an array
void Process(List<Product> products)

// GOOD: works with array, List, HashSet, yield return
void Process(IEnumerable<Product> products)

// GOOD: caller gets indexing without exposing mutation
IReadOnlyList<Product> GetTopProducts()

// BAD: exposes Add/Remove — internal list can be corrupted
IList<Product> GetTopProducts()
```

**IList<T> vs List<T>**

`List<T>` is a concrete class (backed by an array, doubles when full). `IList<T>` is the interface. Returning `IList<T>` from a method is still better than `List<T>` — it lets you swap the implementation (e.g., return an `ArraySegment<T>` or a custom list) without changing the API.
</details>

---

**A4.** What does `async void` do differently than `async Task`? Why is `async void` dangerous?

<details><summary>Answer</summary>

- **`async void`** — can't be awaited, exceptions crash the process (no caller to catch them), fire-and-forget.
- **`async Task`** — awaitable, exceptions propagate normally to the caller.

`async void` is only acceptable for event handlers.

https://claude.ai/chat/5bd1eb7b-a65f-42ec-80a8-9043d925b1e8
</details>

<details><summary>Deep Dive</summary>

**What happens to the exception — step by step**

When an exception escapes an `async Task` method, it's captured inside the returned `Task` object and stored there. The exception only re-throws when the caller `await`s the Task. If nobody awaits it, the exception is observed by `TaskScheduler.UnobservedTaskException` (a last-resort handler).

With `async void`, there is no `Task` object to store the exception in. Instead, the exception is thrown directly onto the `SynchronizationContext` that was active when the method started. In ASP.NET Core (no SyncContext), this goes to the `ThreadPool`, which by default calls `Environment.FailFast()` — **the process dies immediately, no catch block in the world can stop it**.

```csharp
// This KILLS the process. No try/catch outside can stop it.
async void DoSomethingDangerous()
{
    await Task.Delay(100);
    throw new InvalidOperationException("Boom");
}

try
{
    DoSomethingDangerous(); // returns void immediately, exception comes later
}
catch (Exception ex)
{
    // NEVER reached. The exception flies past this entirely.
}
```

**Why event handlers are the exception**

Event handlers in .NET must return `void` — it's how the delegate signature is defined. `button.Click += async (s, e) => { ... }` works because the underlying delegate is `EventHandler` (returns `void`). There's no way around this. In event handlers, the SyncContext is usually a UI thread (WPF/WinForms) that handles exceptions via `Application.UnhandledException`.

Even in event handlers, consider wrapping the body:

```csharp
button.Click += async (s, e) =>
{
    try
    {
        await LoadDataAsync();
    }
    catch (Exception ex)
    {
        // handle gracefully — show error dialog, log, etc.
        _logger.LogError(ex, "Failed to load data");
    }
};
```

**The fire-and-forget problem**

`async void` is also dangerous because the caller has no way to track progress or completion:

```csharp
async void StartProcessing() => await Task.Delay(5000); // takes 5 seconds

StartProcessing(); // returns immediately
Console.WriteLine("Done?"); // prints immediately — the task is still running
```

If you need fire-and-forget properly, use `Task.Run()` and optionally observe the task with `_ = task`:

```csharp
// Proper fire-and-forget with crash protection
_ = Task.Run(async () =>
{
    try { await LongRunningWorkAsync(); }
    catch (Exception ex) { _logger.LogError(ex, "Background task failed"); }
});
```

**How to detect async void in code review**

Look for `async void` in any method that is NOT an event handler (not subscribed to events, not a UI event callback). Analyzers like Roslyn's `AsyncFixer` or `Roslynator` flag these automatically.
</details>

---

**A5.** Explain boxing and unboxing. Give one example where boxing happens implicitly in C#.

<details><summary>Answer</summary>

Boxing wraps a value type in a heap-allocated `object`. Unboxing extracts it back. Implicit boxing: `int x = 42; object o = x;` — also happens with `ArrayList.Add(42)`, `string.Format("{0}", 42)`, and casting value types to interfaces.
</details>

<details><summary>Deep Dive</summary>

**What actually happens in memory when you box**

A value type (`int`, `struct`, `bool`) lives on the stack or inline in its parent object — no heap allocation. When boxed, the CLR:
1. Allocates a new object on the managed heap.
2. Copies the value type's bytes into that object.
3. Adds an object header (sync block index + method table pointer = 16 bytes on 64-bit).
4. Returns a reference to the heap object.

So boxing an `int` (4 bytes) costs you a 20-byte heap allocation plus a GC object to track. In a tight loop doing this millions of times, this is a significant performance cost.

**Why two boxed values are never reference-equal**

```csharp
int x = 42;
object a = x;  // new heap object A
object b = x;  // new heap object B (different allocation!)

Console.WriteLine(a == b);              // True  — object.Equals() checks value
Console.WriteLine(ReferenceEquals(a, b)); // False — different heap objects
```

Each box is a fresh allocation. This is why you should never rely on `ReferenceEquals` for boxed primitives.

**Interface boxing — the subtle case**

Structs can implement interfaces. When you cast a struct to an interface, it's boxed — even if the interface method doesn't need the full object:

```csharp
interface IValue { int Get(); }
struct MyStruct : IValue { public int Get() => 42; }

IValue v = new MyStruct(); // BOXED — allocated on heap
```

This is why `IEnumerable<T>` (where T is a struct) with LINQ can produce many boxes. Generic constraints (`where T : struct`) combined with generic methods avoid this.

**Generics eliminate boxing**

The entire reason `List<int>` was introduced in .NET 2.0 was to avoid the boxing of `ArrayList`:

```csharp
ArrayList list = new();
list.Add(42);        // boxes every int → heap allocation per element
int x = (int)list[0]; // unboxes

List<int> genericList = new();
genericList.Add(42); // NO boxing — int stored inline in the backing array
int y = genericList[0]; // NO unboxing
```

**`string.Format` boxing and modern alternatives**

```csharp
// Old: boxes 'count' (int → object)
string msg = string.Format("Count: {0}", count);

// Modern: NO boxing — uses interpolated string handler in .NET 6+
string msg = $"Count: {count}";

// Even better for hot paths: no allocation at all
Span<char> buffer = stackalloc char[64];
count.TryFormat(buffer, out int written);
```

**Unboxing requires exact type match**

```csharp
object boxed = 42; // boxed as int
long x = (long)boxed; // InvalidCastException! Must unbox to int first.
long y = (long)(int)boxed; // correct
```

The CLR enforces exact type matching during unbox — it reads the stored type from the object header and throws if it doesn't match.
</details>

---

### MEDIUM

**A6.** What does this code print and why?

```csharp
string a = "hello";
string b = "hello";
string c = new string("hello".ToCharArray());

Console.WriteLine(a == b);
Console.WriteLine(a == c);
Console.WriteLine(ReferenceEquals(a, b));
Console.WriteLine(ReferenceEquals(a, c));
```

<details><summary>Answer</summary>

`True, True, True, False`. `==` for strings does value comparison. `a` and `b` reference the same interned string literal, so `ReferenceEquals` is True. `c` is a new string object with the same value — `==` is True (value) but `ReferenceEquals` is False (different heap object).
</details>

<details><summary>Deep Dive</summary>

**String interning — the behind-the-scenes optimization**

When you write `string a = "hello";`, the compiler detects this is a string literal and looks for it in the **string interning pool** (a hash table of all string literals in your assembly). If "hello" is already there, `a` points to the existing object. If not, it's added to the pool first.

This is an automatic optimization by the CLR — every string literal with the same value shares the same memory location. It saves memory in large applications but can hide subtle bugs if you rely on reference equality.

```csharp
string a = "hello";
string b = "hello";
// Both point to the SAME heap object (interned)

string c = new string("hello".ToCharArray());
// Forces a new object allocation, bypassing interning
```

`string.Intern()` lets you manually intern a dynamically-created string:

```csharp
string x = new string("hello".ToCharArray());
string y = string.Intern(x);
ReferenceEquals(x, y); // False — y is interned, x is not
```

**Why interning is dangerous**

In the days of .NET Framework, string interning was pervasive and led to hard-to-debug reference-equality bugs:

```csharp
if (userInput == "admin") { /* safe comparison */ }
if (ReferenceEquals(userInput, "admin")) { /* DANGEROUS — depends on interning */ }
```

Modern code avoids `ReferenceEquals` for strings entirely — always use `==` or `.Equals()` for value comparison.

**Performance implications of the ToCharArray() pattern**

Creating a new string via `new string(charArray)` allocates a new heap object and copies the characters — it's slower and wastes memory compared to a literal. In code review, `new string(...)` is a red flag. The only valid use case is when you're dynamically constructing a string and explicitly want a new object (rare).

**String equality vs ordinal comparison**

```csharp
string a = "naïve";
string b = "naïve"; // Different binary representation (decomposed vs composed Unicode)

a == b; // True — uses CurrentCulture comparison, considers accents equivalent
a.Equals(b, StringComparison.Ordinal); // May be False — byte-by-byte comparison
```

For security and caching, always use `StringComparison.Ordinal` or `Ordinal IgnoreCase`. Never rely on culture-sensitive comparison in security contexts.
</details>

---

**A7.** You have a `List<Order>` with 50,000 orders. Find all where `Status == "Shipped"` and `Total > 100`, sorted by `Total` descending, top 10. Write the LINQ query. Does LINQ evaluate lazily or eagerly? When does it execute?

<details><summary>Answer</summary>

```csharp
var result = orders
    .Where(o => o.Status == "Shipped" && o.Total > 100)
    .OrderByDescending(o => o.Total)
    .Take(10)
    .ToList();
```

The query builds lazily via `Where`, `OrderByDescending`, `Take` — these return `IEnumerable<T>` and compose a pipeline. Execution is deferred until `.ToList()` forces materialization. Without `.ToList()`, iterating the result also triggers execution. `OrderByDescending` must buffer all matching items to sort.
</details>

<details><summary>Deep Dive</summary>

**Lazy execution — the beauty and the danger**

LINQ to Objects composes methods into a chain that returns an `IEnumerable<T>`. Each method (`Where`, `OrderBy`, `Select`) wraps the previous enumerator — creating a chain. When you call `MoveNext()` on the outermost enumerator, it recursively calls down the chain:

```
Take(10).MoveNext() 
  → OrderByDescending.MoveNext() [checks internal buffer]
    → Where.MoveNext()
      → IEnumerable<Order>.MoveNext()
```

The advantage: you can chain unbounded operations without allocating intermediate arrays.

The danger: `OrderByDescending` forces full materialization — it must read ALL matching items into memory to sort them. Combining `.OrderBy()` + `.Take(10)` on a 50,000-row list materializes the entire filtered set before taking the top 10. This can OOM on large datasets.

**When LINQ decides to be eager**

Some operations force materialization (eager execution):
- **Sorting**: `OrderBy`, `OrderByDescending`, `ThenBy`, `Reverse`
- **Grouping**: `GroupBy` (must materialize to group)
- **Aggregation**: `Count()`, `Sum()`, `Average()`, `Any()` (not `Any` with a predicate, which can short-circuit)
- **Set operations**: `Union`, `Intersect`, `Except`, `Distinct`

Operations that are lazy (streaming):
- **Filtering**: `Where`, `Skip`, `SkipWhile`, `Take`, `TakeWhile`
- **Projection**: `Select`, `SelectMany`, `Cast`

In the example, `OrderByDescending` kills lazy evaluation. To optimize, you'd move `.Take(10)` before the sort (if possible), or sort in the database with `LINQ to SQL`/EF Core (which generates SQL that sorts first, limits second).

**Database vs in-memory LINQ — vastly different semantics**

```csharp
// With LINQ to SQL or EF Core — translates to SQL
var topOrders = dbContext.Orders
    .Where(o => o.Total > 100)        // WHERE clause
    .OrderByDescending(o => o.Total)  // ORDER BY
    .Take(10)                         // TOP 10
    .ToList();                        // executes the single optimized SQL query
```

The database optimizer reorders the operations for efficiency. In-memory LINQ executes the operations in the exact order you write them.

**The `.ToList()` at the end is essential in the example** — without it, the result is still lazy. Multiple iterations re-filter and re-sort.
</details>

---

**A8.** What is the difference between `Task.Run()` and `Task.Factory.StartNew()`? When would you explicitly choose one over the other?

<details><summary>Answer</summary>

- **`Task.Run`** — shorthand with safe defaults: thread pool, `DenyChildAttach`, `TaskScheduler.Default`. Use for 99% of cases.
- **`Task.Factory.StartNew`** — fine-grained control over `LongRunning`, custom schedulers, creation options.

Choose `StartNew` only for long-running CPU-bound work or custom task schedulers.
</details>

<details><summary>Deep Dive</summary>

**The `DenyChildAttach` default — preventing task tree explosion**

`Task.Run` sets `TaskCreationOptions.DenyChildAttach` by default. This prevents child tasks from joining the parent's task tree. Without this:

```csharp
// Without DenyChildAttach, child tasks attach to parent
Task parent = Task.Run(() =>
{
    Task child = new Task(() => LongRunningWork()); // attaches to parent by default
    child.Start();
});
await parent; // waits for child too
```

With `DenyChildAttach`, child tasks are independent:

```csharp
// With DenyChildAttach (Task.Run default)
Task parent = Task.Run(() =>
{
    Task child = new Task(() => LongRunningWork()); // does NOT attach
    child.Start();
});
await parent; // completes before child (dangerous — child might still be running)
```

This prevents naive code from accidentally creating unbounded task hierarchies that cause GC pressure.

**When to use `StartNew` with `LongRunning`**

For CPU-intensive work (like a tight compute loop), the thread pool may not be optimal — it's designed for I/O-bound work with lots of short tasks. `TaskCreationOptions.LongRunning` signals the scheduler to possibly create a dedicated thread:

```csharp
Task.Factory.StartNew(
    () => ExpensiveComputationAsync(),
    TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach
);
```

The scheduler may allocate a non-pooled thread for this task, avoiding thread pool starvation where short-lived tasks starve long-lived ones.

**Custom `TaskScheduler` — a niche use case**

```csharp
var priorityScheduler = new PriorityScheduler();
Task.Factory.StartNew(
    () => ImportantWork(),
    CancellationToken.None,
    TaskCreationOptions.DenyChildAttach,
    priorityScheduler  // use custom scheduler, not the default
);
```

Custom schedulers are rare — almost never needed in ASP.NET Core (uses thread pool default).

**In modern code, stick with `Task.Run`** — it's the API the team expects to see.
</details>

---

**A9.** Explain the difference between `ConfigureAwait(false)` and the default `ConfigureAwait(true)`. In what context does this matter? Does it matter in ASP.NET Core?

<details><summary>Answer</summary>

`ConfigureAwait(true)` (default) resumes on the captured SynchronizationContext. `ConfigureAwait(false)` resumes on any thread pool thread. Matters in UI apps (WPF/WinForms) and old ASP.NET (had a SyncContext). ASP.NET Core has NO SyncContext, so `ConfigureAwait(false)` is unnecessary there — but library code should still use it for portability.
</details>

<details><summary>Deep Dive</summary>

**SynchronizationContext — what it captures**

When you `await` an async operation, the `SynchronizationContext.Current` is captured (if one exists). The awaiter resumes the continuation (the code after the `await`) on that context. In UI frameworks:
- **WPF/WinForms**: `WindowsFormsSynchronizationContext` (marshals continuations back to the UI thread)
- **Console**: `null` — no context, default behavior
- **ASP.NET Framework**: `AspNetSynchronizationContext` (was used for async-over-sync scenarios)

In ASP.NET Core: **no synchronization context** — `SynchronizationContext.Current` is `null` from the start.

**Why UI frameworks need SyncContext**

In WPF, you can only modify UI elements from the UI thread. If you `await` an HTTP call on the UI thread:

```csharp
// WPF button click handler
private async void OnFetchData(object sender, RoutedEventArgs e)
{
    var data = await http.GetStringAsync("/api/data"); // SyncContext captured: the UI thread
    // After await, you're back on the UI thread — safe to touch UI
    textBox.Text = data;
}
```

With `ConfigureAwait(false)`:

```csharp
private async void OnFetchData(object sender, RoutedEventArgs e)
{
    var data = await http.GetStringAsync("/api/data").ConfigureAwait(false); // no SyncContext capture
    // After await, you're on a thread pool thread — accessing textBox throws cross-thread exception
    textBox.Text = data; // CRASH
}
```

**ASP.NET Core has no SyncContext, so ConfigureAwait(false) is technically unnecessary**

```csharp
// ASP.NET Core endpoint
app.MapGet("/data", async () =>
{
    var data = await http.GetStringAsync("/api/external").ConfigureAwait(false);
    // Even without ConfigureAwait(false), you're already on a thread pool thread
    // There's no "UI thread" to return to
    return data;
});
```

**However, library authors SHOULD use ConfigureAwait(false)**

A library doesn't know where it will be called from. If someone uses your library in WinForms + .NET Framework, they benefit from `ConfigureAwait(false)` that lets them avoid unneeded context switching. It's a best practice even if unnecessary in most modern contexts.

**Performance implication of context switching**

Switching from thread pool back to a SyncContext has overhead — scheduler queue, context marshaling. In high-throughput ASP.NET apps, avoiding this overhead (even negligible) per-request multiplied by millions of requests can matter slightly.
</details>

---

**A10.** What is the `IDisposable` pattern? What happens if you forget to dispose an `HttpClient` in a loop? How does `IHttpClientFactory` solve this?

<details><summary>Answer</summary>

`IDisposable` provides deterministic cleanup via `Dispose()` / `using`. Creating/disposing `HttpClient` in a loop causes socket exhaustion — sockets linger in `TIME_WAIT`. `IHttpClientFactory` pools `HttpMessageHandler` instances (2min default lifetime), recycles them for DNS changes, and lets you create `HttpClient` instances cheaply without socket exhaustion.
</details>

<details><summary>Deep Dive</summary>

**The socket exhaustion trap — TIME_WAIT state**

When you dispose an `HttpClient`, its underlying `HttpMessageHandler` closes the TCP connection. The OS doesn't immediately deallocate the socket — it enters `TIME_WAIT` state (lasts ~60 seconds on Windows) to handle any retransmitted packets from the peer. If you create and dispose 1000s of `HttpClient` instances in a loop, you accumulate thousands of TIME_WAIT sockets. The OS has a limit (e.g., 16K open sockets on Windows). You hit the limit and new connections fail.

```csharp
// DANGEROUS — one socket per iteration, all enter TIME_WAIT
for (int i = 0; i < 10000; i++)
{
    using var client = new HttpClient();
    var response = await client.GetAsync("https://example.com");
}
// 10K sockets in TIME_WAIT — OS limit exhausted
```

**`IHttpClientFactory` — the solution**

`HttpClientFactory` creates and reuses `HttpMessageHandler` instances (the expensive part). The `HttpClient` wrapper is lightweight — you can create many of them cheaply.

```csharp
// In Startup:
builder.Services.AddHttpClient("myapi")
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.example.com"));

// In a service:
public class OrderService
{
    private readonly IHttpClientFactory _factory;
    
    public async Task ProcessAsync()
    {
        // Creates an HttpClient wrapping a pooled HttpMessageHandler
        using var client = _factory.CreateClient("myapi");
        var response = await client.GetAsync("/orders");
        // Disposing the client doesn't dispose the handler — it's returned to the pool
    }
}
```

The `HttpMessageHandler` is reused for 2 minutes (by default), then recycled to pick up DNS changes. This is efficient and avoids socket exhaustion.

**Misconception: "HttpClient should be static"**

Some resources recommend making `HttpClient` static and reusing it forever. This is _safe_ but incomplete — it doesn't pick up DNS changes. If you point your API at a load balancer that gets replaced, your static client keeps connecting to the old IP. With factory pooling, handlers are recycled regularly and DNS is re-resolved.

**`Dispose()` on `HttpClient` is a no-op in factories** — the client doesn't own the handler, so disposing it is safe and expected.
</details>

---

**A11.** What does this code do? Is there a bug?

```csharp
public async Task<List<Product>> GetAllAsync()
{
    var tasks = productIds.Select(id => GetByIdAsync(id));
    var results = await Task.WhenAll(tasks);
    return results.ToList();
}
```

<details><summary>Answer</summary>

It fetches all products in parallel using `Task.WhenAll`. The subtle bug: `Select` with an async lambda creates tasks immediately — if `productIds` is large, this fires ALL requests simultaneously (no throttling). Could overwhelm the server. Fix: use `SemaphoreSlim` or `Parallel.ForEachAsync` with `MaxDegreeOfParallelism`. Also: multiple enumeration risk if `tasks` is lazy.
</details>

<details><summary>Deep Dive</summary>

**`Select` with async — eager evaluation trap**

`Select` on `IEnumerable<T>` is lazy (deferred). But when you call `.Select(id => GetByIdAsync(id))`, the lambda creates a `Task` object for each ID **immediately**. The task is created but not awaited yet — they're all scheduled on the thread pool concurrently. This is different from the collection being materialized lazily.

```csharp
// ALL tasks are created NOW, before WhenAll even runs
var tasks = productIds.Select(id => GetByIdAsync(id)); // 10K tasks created, all running in parallel
var results = await Task.WhenAll(tasks); // waits for all 10K
```

If `productIds` has 10,000 items and `GetByIdAsync` does a database query, you fire 10,000 concurrent queries. The server crashes under load.

**Fix 1: SemaphoreSlim for concurrency control**

```csharp
var semaphore = new SemaphoreSlim(10); // max 10 concurrent requests
var tasks = productIds.Select(async id =>
{
    await semaphore.WaitAsync(); // acquire permit
    try { return await GetByIdAsync(id); }
    finally { semaphore.Release(); }
});
var results = await Task.WhenAll(tasks);
```

Only 10 requests run at a time, the rest queue up.

**Fix 2: `Parallel.ForEachAsync` (simpler)**

```csharp
var results = new List<Product>();
var options = new ParallelOptions { MaxDegreeOfParallelism = 10 };
await Parallel.ForEachAsync(productIds, options, async (id, ct) =>
{
    var product = await GetByIdAsync(id, ct);
    lock (results) results.Add(product);
});
```

**Also: the multiple enumeration risk**

If `tasks` is lazy (`IEnumerable<Task>`) and you pass it to `WhenAll`, it's enumerated. If you enumerate it again, the lambda runs again and new tasks are created:

```csharp
IEnumerable<Task> tasks = productIds.Select(id => GetByIdAsync(id)); // lazy
var result1 = await Task.WhenAll(tasks);  // enumerates, creates and runs tasks
var result2 = await Task.WhenAll(tasks);  // enumerates AGAIN, creates NEW tasks — 2x requests
```

Fix: materialize early with `.ToList()`.
</details>

---

**A12.** What is the difference between `throw;` and `throw ex;` in a catch block? Why does it matter?

<details><summary>Answer</summary>

`throw;` preserves the original stack trace. `throw ex;` resets the stack trace to the current catch block, losing the original call site. Always use `throw;` to re-throw. `throw ex;` makes debugging much harder because you can't see where the exception originated.
</details>

<details><summary>Deep Dive</summary>

**Stack trace preservation — how the CLR tracks origin**

When an exception is thrown, the CLR captures the entire call stack and stores it in the exception object's `StackTrace` property. This is a snapshot — it doesn't change unless you explicitly reset it.

```
Method A calls Method B calls Method C throws exception
  StackTrace: A → B → C
```

When you `throw;` in a catch block, you're re-throwing the SAME exception object — the stack trace is unchanged. Debuggers and logs show exactly where it originated.

When you `throw ex;`, you're throwing the caught exception object, which resets its stack trace:

```csharp
try { SomeMethodThatThrows(); }
catch (Exception ex)
{
    throw ex; // StackTrace now shows: CurrentMethod → throw statement (line 47)
    // Original origin (SomeMethodThatThrows) is lost
}
```

**Why this matters in production debugging**

Logs show:

With `throw;`:
```
StackTrace: 
  at SomeMethodThatThrows() in file.cs:100
  at CurrentMethod() in file.cs:45
```
You can see the original failure at line 100.

With `throw ex;`:
```
StackTrace: 
  at CurrentMethod() in file.cs:47
```
You only know the exception was re-thrown, not where it came from.

**The ExceptionDispatchInfo pattern — for code that needs to modify exceptions**

If you genuinely need to re-throw with additional context, use `ExceptionDispatchInfo`:

```csharp
try { SomeMethodThatThrows(); }
catch (Exception ex)
{
    // Log additional context without losing the stack trace
    _logger.LogError(ex, "Failed during operation X");
    ExceptionDispatchInfo.Capture(ex).Throw();
}
```

This preserves the original stack trace while allowing you to add logging.

**In async code, re-throw is automatic**

With `await`, if an exception occurs, it's automatically re-thrown on the caller with the original stack trace preserved:

```csharp
try { await SomeAsyncMethod(); }
catch (Exception ex)
{
    throw; // This is redundant in async — the exception already has the original trace
    // But it's still good practice to be explicit
}
```

**Code review: `throw ex;` should always be flagged**. There's almost no reason to use it.
</details>

---

**A13.** You have a service registered as `Scoped`. A `Singleton` service tries to inject it. What happens? What is the name of this problem?

<details><summary>Answer</summary>

This is called a **Captive Dependency**. The Scoped service gets captured by the Singleton and lives forever — it never gets disposed/recreated per request. This causes stale data, thread-safety issues, and DbContext tracking bugs. ASP.NET Core throws an `InvalidOperationException` if `ValidateScopes` is enabled (default in Development).
</details>

<details><summary>Deep Dive</summary>

**How the captive dependency trap happens**

The DI container resolves services based on lifetime. When resolving a Singleton, it reuses the same instance forever. If that Singleton has a constructor dependency on a Scoped service, the container passes a single instance of the Scoped service to the Singleton's constructor — capturing it for the Singleton's entire lifetime.

```csharp
services.AddScoped<IOrderRepository, SqlOrderRepository>();
services.AddSingleton<OrderCache>(sp =>
{
    // BUG: The Singleton captures a Scoped DbContext instance
    var repo = sp.GetRequiredService<IOrderRepository>(); // Gets A Scoped instance
    return new OrderCache(repo); // Captured forever
});
```

Now `OrderCache` holds a reference to the SAME `IOrderRepository` instance across ALL requests. If the repository has state (like EF Core tracking), that state accumulates and is never cleared.

**The symptoms**

1. **DbContext tracking bugs**: EF Core's change tracker never resets. You save an entity, modify it in request 2, and get a "DbContext has been disposed" error because the context from request 1 is reused.
2. **Stale cached data**: If the repository caches query results, subsequent requests get stale data (from the first request).
3. **Thread-safety violations**: The scoped service was never designed to be thread-safe (ASP.NET reuses scopes per request on different threads). Now it's shared across threads.

**How ASP.NET Core detects this at startup**

In Development, `ValidateScopes = true` enables a validation check at startup. The DI container scans all registered services and throws if it finds a captive dependency:

```
System.InvalidOperationException: Called from scope validation
The dependency 'SqlOrderRepository' has a captive dependency on a Scoped service 'AppDbContext' from 'MyApp.Services'. 
The Scoped service will be locked to the root scope...
```

In Production, `ValidateScopes` defaults to `false` — the error silently happens at runtime.

**The fix**

Restructure so Singletons don't depend on Scoped services. Inject `IServiceScopeFactory` and create + dispose scopes at the point of use:

```csharp
// OrderCache constructor receives the factory, not the scoped service
services.AddSingleton<OrderCache>();

public class OrderCache
{
    private readonly IServiceScopeFactory _scopeFactory;

    public OrderCache(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<Order?> GetAsync(int id)
    {
        using var scope = _scopeFactory.CreateScope();  // scope is disposed after the block
        var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        return await repo.GetByIdAsync(id);
    }
}
```

The scope — and everything it owns — is disposed at the end of the `using` block. This is the correct pattern. The common mistake of `sp.CreateScope().ServiceProvider.GetRequiredService<T>()` without `using` leaks the scope and every disposable it holds.

**The broader lesson: think about object lifetime and state sharing**

A Singleton that shares state with a Scoped service is a design smell. Either:
1. The service should be Singleton too (stateless, shared across requests).
2. The dependency is wrong and should be injected at the point of use, not in the constructor.
</details>

---

### HARD

**A14.** Explain the .NET garbage collector's generational model. What are Gen 0, Gen 1, Gen 2? What triggers a collection? What is the Large Object Heap and why does it matter?

<details><summary>Answer</summary>

Gen 0: short-lived objects (locals, temp strings) — collected frequently and fast. Gen 1: buffer between short/long-lived. Gen 2: long-lived objects — collected rarely and expensive. Objects surviving a collection get promoted. Triggered when Gen 0 budget is exceeded. LOH holds objects ≥85KB, collected with Gen 2, not compacted by default (fragmentation risk). `GCSettings.LargeObjectHeapCompactionMode` can force compaction.
</details>

<details><summary>Deep Dive</summary>

**Generational hypothesis — the insight**

The GC's design assumes: most objects die young. Empirically true — locals, temporary strings, intermediate LINQ results live milliseconds. Only a few objects live long (application state, caches, database connections).

Gen 0 is optimized for fast collection: it's small (4-10 MB), packed tightly in memory, and collected frequently. Gen 1 and 2 are larger and collected less often.

**The promotion process**

When Gen 0 is full, the GC collects it. Survivors are promoted to Gen 1. When Gen 1 is full, its survivors are promoted to Gen 2. Gen 2 collects include Gen 1 and Gen 0.

If an object survives 2 Gen 0 collections, it's in Gen 1. If it survives a Gen 1 collection, it's in Gen 2.

**Large Object Heap — the exception**

Objects ≥85KB don't go in the generational heaps. They go to the LOH (a separate, non-compacting heap). Why? Compacting a 1GB Gen 2 heap is expensive — moving millions of objects around. The LOH keeps large objects where they are.

Downside: LOH fragmentation (Swiss cheese memory). If you allocate/free many large objects, LOH becomes fragmented and allocation fails even though total free space is large.

Fix: `GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce` forces LOH compaction during the next full GC collection. The setting resets to `Default` after the compaction runs — it's a one-time request, not a permanent mode.

**Forcing collection — almost always wrong**

```csharp
GC.Collect();  // DON'T DO THIS
```

Calling `GC.Collect()` pauses all threads to collect, disrupting real-time performance. It's only valid in very specific cases (after startup, before a latency-sensitive operation). For e-commerce APIs, never call it.

Let the GC do its job automatically. It's incredibly sophisticated and outperforms manual collection 99% of the time.
</details>

---

**A15.** What is `Span<T>` and why was it introduced? How does it reduce allocations compared to `Substring()`? Write a code example that parses a comma-separated string using `Span<T>` without allocating new strings.

<details><summary>Answer</summary>

`Span<T>` is a stack-only ref struct providing a type-safe view over contiguous memory (arrays, strings, native buffers) without copying. `Substring()` allocates a new string each time; `Span<T>.Slice()` just moves pointers — zero allocation. Key: `Span<T>` can't be stored on the heap (no class fields, no async), enabling safe stack-only memory access.

```csharp
ReadOnlySpan<char> input = "alice,bob,carol";
while (input.Length > 0)
{
    int comma = input.IndexOf(',');
    ReadOnlySpan<char> name = comma == -1 ? input : input.Slice(0, comma);
    Console.WriteLine(name.ToString());
    input = comma == -1 ? ReadOnlySpan<char>.Empty : input.Slice(comma + 1);
}
```
</details>

<details><summary>Deep Dive</summary>

**Stack-only constraint — why it matters**

`Span<T>` is a `ref struct` — it can only live on the stack. You can't store it in a class field, array, or async context. Why? Spans point directly to memory (a pointer + length). If the underlying memory is freed or reallocated, the span becomes invalid ("dangling pointer"). Restricting spans to the stack ensures the compiler can verify the memory is still valid.

**Zero-copy views — the power**

```csharp
string original = "alice,bob,carol";
ReadOnlySpan<char> bob = original.AsSpan(6, 3);  // Points to "bob" inside the original string
```

This creates a view into the string without allocating new memory. The span is just `{pointer, length}` — 16 bytes on the stack.

**stackalloc for on-stack arrays**

```csharp
Span<byte> buffer = stackalloc byte[256];  // allocate 256 bytes on the stack
parser.Parse(buffer);
```

No heap allocation. The 256 bytes live on the stack and are automatically freed when the scope exits.

**When to use**

Use `Span<T>` in hot code paths (parsing, crypto, compression) where allocations matter. For most code, the overhead is negligible — use regular arrays/strings.
</details>

---

**A16.** You see this in a code review. What are the two subtle problems? How would you fix each?

```csharp
public class OrderProcessor
{
    private static readonly HttpClient _client = new HttpClient();

    public async Task ProcessAsync(Order order)
    {
        var response = await _client.PostAsJsonAsync("/api/process", order);
        response.EnsureSuccessStatusCode();
    }
}
```

<details><summary>Answer</summary>

**Problem 1:** Static `HttpClient` never refreshes DNS — if the target IP changes, requests go to the stale address. Fix: use `IHttpClientFactory` or set `PooledConnectionLifetime`.

**Problem 2:** No base address, no timeout, no resilience. `EnsureSuccessStatusCode` throws a generic exception with no detail. Fix: configure `BaseAddress`, add Polly retry policy, read error body before throwing.
</details>

<details><summary>Deep Dive</summary>

**DNS staling — the sneaky problem**

When you create a static `HttpClient`, its `HttpMessageHandler` caches DNS entries. If `api.example.com` resolves to `1.2.3.4`, and later the IP changes to `5.6.7.8`, the handler keeps connecting to `1.2.3.4` forever. Modern load balancers and cloud deployments change IPs frequently.

Fix: `IHttpClientFactory` recycles handlers every 2 minutes by default, forcing DNS re-resolution.

Or configure the socket handler lifetime directly:
```csharp
services.AddHttpClient<MyClient>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)  // recycle TCP connections after 5 min
    });
```

**Resilience with Polly**

```csharp
services.AddHttpClient<MyClient>()
    .AddTransientHttpErrorPolicy(p =>
        p.WaitAndRetryAsync(retryCount: 3, sleepDurationProvider: retry =>
            TimeSpan.FromSeconds(Math.Pow(2, retry))));
```

This retries on transient errors (5xx, connection timeouts) with exponential backoff.

**Error details**

`EnsureSuccessStatusCode` throws `HttpRequestException` with minimal info. Always read the response body:

```csharp
var response = await client.PostAsync(...);
if (!response.IsSuccessStatusCode)
{
    var body = await response.Content.ReadAsStringAsync();
    throw new Exception($"API error: {response.StatusCode} - {body}");
}
```
</details>

---

**A17.** Explain `ValueTask<T>` vs `Task<T>`. When is `ValueTask<T>` faster? When is it dangerous? What happens if you `await` a `ValueTask<T>` twice?

<details><summary>Answer</summary>

`ValueTask<T>` avoids allocation when the result is already available synchronously — it wraps either a `T` value or a `Task<T>`. Faster in hot paths with cache hits. Dangerous: must only be awaited ONCE. Awaiting twice is undefined behavior — may throw, return wrong result, or corrupt state. Can't `.Result` or `.GetAwaiter()` concurrently. Use `.AsTask()` if you need to store/await multiple times.
</details>

<details><summary>Deep Dive</summary>

**When ValueTask is faster**

```csharp
public ValueTask<int> GetCachedValue(string key)
{
    if (_cache.TryGetValue(key, out var value))
        return new ValueTask<int>(value);  // Synchronous — no Task allocation
    
    return new ValueTask<int>(FetchAsync(key));  // Wrapped async Task
}
```

If the value is cached, the result is available immediately — no Task allocation. If it's not cached, we wrap the async operation.

**The single-await rule**

```csharp
var vt = GetCachedValue("key1");
var result1 = await vt;  // OK
var result2 = await vt;  // DANGER — undefined behavior
```

`ValueTask<T>` is not reentrant. Awaiting twice may reuse state from the first await, causing corruption or exceptions.

Fix: convert to Task if you need multiple awaits:
```csharp
var task = vt.AsTask();
var r1 = await task;
var r2 = await task;  // Safe
```

**Use sparingly in APIs**

`ValueTask<T>` is seductive but dangerous. Only use in hot paths (database caches, CPU caches) where the performance gain justifies the danger. For most APIs, return `Task<T>`.
</details>

---

**A18.** What are `record` types in C#? How do they differ from classes in terms of equality, immutability, and generated code? What is `with` expression? When would you use `record struct` vs `record class`?

<details><summary>Answer</summary>

Records provide value-based equality (all properties compared), auto-generated `ToString` (human-readable), `Deconstruct`, and `with` expressions for non-destructive mutation. `record class` is a reference type with immutability via `init` properties. `record struct` is a value type — good for small, frequently-copied data. Records are ideal for DTOs, events, and immutable domain objects.
</details>

---

**A19.** You have a hot code path that creates thousands of small temporary objects per request. The GC is causing latency spikes. What strategies can you use to reduce GC pressure? Name at least four.

<details><summary>Answer</summary>

1. `ArrayPool<T>.Shared` / `ObjectPool<T>` — rent and return instead of alloc/GC.
2. `Span<T>` + `stackalloc` — stack allocation, zero GC.
3. Use structs for small, short-lived data.
4. `StringBuilder` pooling or `string.Create`.
5. Avoid LINQ in hot paths (allocates iterators/delegates).
6. `RecyclableMemoryStream` for streams.
7. Cache frequently-used objects.
8. Use `ref struct` / `ref returns` to avoid copies.
</details>

---

