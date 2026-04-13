## SECTION A — C# / .NET FUNDAMENTALS

### EASY

**A1.** What is the difference between `readonly` and `const` in C#? When would you use each?

<details><summary>Answer</summary>

- **`const`** — compile-time, implicitly static, must be assigned at declaration, only works for primitives/strings.
- **`readonly`** — runtime, can be set in the constructor, works for any type.

Use `const` for true constants (e.g. `Math.PI`), `readonly` for values set at construction (config, injected dependencies).

https://claude.ai/chat/b3e1ad34-130d-4c89-ae3f-2b0718974b59
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

---

**A3.** What is the difference between `IEnumerable<T>` and `IList<T>`? When do you choose one over the other?

<details><summary>Answer</summary>

- **`IEnumerable<T>`** — read-only, forward-only, lazy — supports deferred execution.
- **`IList<T>`** — adds indexing, `Count`, `Add`/`Remove` — materialized in memory.

Return `IEnumerable<T>` when consumers only iterate; use `IList<T>` when you need random access or mutation.

https://claude.ai/chat/46c27fdb-6053-4e9c-9fcc-755d1970603d
</details>

---

**A4.** What does `async void` do differently than `async Task`? Why is `async void` dangerous?

<details><summary>Answer</summary>

- **`async void`** — can't be awaited, exceptions crash the process (no caller to catch them), fire-and-forget.
- **`async Task`** — awaitable, exceptions propagate normally to the caller.

`async void` is only acceptable for event handlers.

https://claude.ai/chat/5bd1eb7b-a65f-42ec-80a8-9043d925b1e8
</details>

---

**A5.** Explain boxing and unboxing. Give one example where boxing happens implicitly in C#.

<details><summary>Answer</summary>

**Boxing** — wraps a value type in a heap-allocated `object` wrapper. **Unboxing** — extracts the value back out (must be the exact original type).

**Implicit boxing happens in:**
- Direct assignment: `object o = 42;`
- Collections: `ArrayList.Add(42)` (legacy non-generic)
- String formatting: `string.Format("{0}", 42)`
- Interface casting: `IComparable c = 42;` (int implements IComparable)

Example:
```csharp
int x = 42;
object o = x;  // Boxing — x is copied to heap
int y = (int)o;  // Unboxing — must cast back to int, not uint
```

If you try `uint z = (uint)o;` on an `int`-boxed value, you get `InvalidCastException`.
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

Output: `True, True, True, False`

- `==` on strings is **value comparison** (operator overloaded) — always compares content, not reference.
- `a` and `b` point to the **same interned string literal** — `ReferenceEquals` is `true`.
- `c` is `new string(...)` — **new heap object** with the same content — `==` is `true` but `ReferenceEquals` is `false`.
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

---

**A8.** What is the difference between `Task.Run()` and `Task.Factory.StartNew()`? When would you explicitly choose one over the other?

<details><summary>Answer</summary>

- **`Task.Run`** — shorthand with safe defaults: thread pool, `DenyChildAttach`, `TaskScheduler.Default`. Use for 99% of cases.
- **`Task.Factory.StartNew`** — fine-grained control over `LongRunning`, custom schedulers, creation options.

Choose `StartNew` only for long-running CPU-bound work or custom task schedulers.
</details>

---

**A9.** Explain the difference between `ConfigureAwait(false)` and the default `ConfigureAwait(true)`. In what context does this matter? Does it matter in ASP.NET Core?

<details><summary>Answer</summary>

- **`ConfigureAwait(true)`** (default) — resumes on the captured `SynchronizationContext`. Needed in WPF/WinForms to get back to the UI thread after an `await`.
- **`ConfigureAwait(false)`** — resumes on any thread pool thread. Avoids the overhead of capturing/restoring the context.

**In ASP.NET Core:** no `SynchronizationContext` exists, so `ConfigureAwait(false)` has no practical effect. Still use it in **library code** for portability across UI frameworks.
</details>

---

**A10.** What is the `IDisposable` pattern? What happens if you forget to dispose an `HttpClient` in a loop? How does `IHttpClientFactory` solve this?

<details><summary>Answer</summary>

`IDisposable` provides deterministic cleanup via `Dispose()` / `using`.

**Problem — `HttpClient` in a loop:**
- Creating/disposing `HttpClient` per request causes **socket exhaustion** — sockets linger in `TIME_WAIT` for minutes.
- Even though `HttpClient` implements `IDisposable`, disposing it closes the underlying `HttpMessageHandler` prematurely.

**`IHttpClientFactory` solution:**
- Pools `HttpMessageHandler` instances (2-minute default lifetime).
- Lets you call `CreateClient()` cheaply — no socket exhaustion.
- Recycles handlers automatically to respect DNS changes.
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

It fetches all products **in parallel** using `Task.WhenAll`.

**Subtle bugs:**
1. **No throttling** — `Select` with an async lambda creates ALL tasks immediately. With a large `productIds`, this fires all requests simultaneously — could overwhelm the DB or downstream service.
2. **Multiple enumeration risk** — if `productIds` is a lazy `IEnumerable`, iterating it creates two sets of tasks (once for `Select`, once for `WhenAll`).

**Fix:** use `SemaphoreSlim` to cap concurrency, or `Parallel.ForEachAsync(productIds, new ParallelOptions { MaxDegreeOfParallelism = 10 }, ...)` .
</details>

---

**A12.** What is the difference between `throw;` and `throw ex;` in a catch block? Why does it matter?

<details><summary>Answer</summary>

- **`throw;`** — re-throws the original exception, **preserving the full stack trace**. Always use this.
- **`throw ex;`** — creates a new throw point at the `catch` block, **resetting the stack trace**. You lose where the exception originally occurred.

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Failed");
    throw;     // ✅ original stack trace preserved
    // throw ex; ❌ stack trace reset — root cause hidden
}
```
</details>

---

**A13.** You have a service registered as `Scoped`. A `Singleton` service tries to inject it. What happens? What is the name of this problem?

<details><summary>Answer</summary>

This is called a **Captive Dependency**. The Scoped service gets captured by the Singleton and lives forever — it never gets disposed/recreated per request. This causes stale data, thread-safety issues, and DbContext tracking bugs. ASP.NET Core throws an `InvalidOperationException` if `ValidateScopes` is enabled (default in Development).
</details>

---

### HARD

**A14.** Explain the .NET garbage collector's generational model. What are Gen 0, Gen 1, Gen 2? What triggers a collection? What is the Large Object Heap and why does it matter?

<details><summary>Answer</summary>

**Generations:**
- **Gen 0** — short-lived objects (locals, temp strings). Collected frequently, very fast.
- **Gen 1** — buffer between short/long-lived. Collected less often.
- **Gen 2** — long-lived objects (statics, caches). Collected rarely, expensive.

Objects **surviving a collection are promoted** to the next generation.

**What triggers a collection:** Gen 0 budget exceeded → Gen 0 GC fires; may cascade to Gen 1/2.

**Large Object Heap (LOH):**
- Objects ≥ **85 KB** skip Gen 0/1 and go directly to the LOH.
- Collected only with Gen 2 (rare).
- **Not compacted by default** — can cause memory fragmentation.
- Force compaction: `GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce`.
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

---

**A17.** Explain `ValueTask<T>` vs `Task<T>`. When is `ValueTask<T>` faster? When is it dangerous? What happens if you `await` a `ValueTask<T>` twice?

<details><summary>Answer</summary>

- **`Task<T>`** — always allocates a heap object, even if the result is already available.
- **`ValueTask<T>`** — wraps either a `T` value (stack-allocated) or a `Task<T>`. **Avoids allocation** when the result is ready synchronously (e.g. cache hit).

**When `ValueTask<T>` is faster:** hot paths where the method frequently completes synchronously (reading from a memory cache before hitting async I/O).

**Dangers:**
- **Must be awaited exactly once.** Awaiting twice is **undefined behavior** — may throw, return the wrong value, or corrupt state.
- Cannot use `.Result` or `.GetAwaiter()` concurrently.
- **Fix:** call `.AsTask()` if you need to store, share, or await multiple times.
</details>

---

**A18.** What are `record` types in C#? How do they differ from classes in terms of equality, immutability, and generated code? What is `with` expression? When would you use `record struct` vs `record class`?

<details><summary>Answer</summary>

**Key features of `record`:**
- **Value-based equality** — all properties compared (not reference).
- Auto-generated: human-readable `ToString()`, `Deconstruct()`, `==` / `!=`.
- **`with` expression** — non-destructive mutation: `var b = a with { Name = "Bob" };`

**`record class` vs `record struct`:**
- **`record class`** — reference type; immutable via `init` properties.
- **`record struct`** — value type; good for small, frequently-copied data (avoids heap allocation).

**When to use:** DTOs, domain events, immutable value objects.
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

