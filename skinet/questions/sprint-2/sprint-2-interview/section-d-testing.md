## SECTION D — TESTING (Unit Tests, Moq, Test Quality)

### EASY

**D1.** What is the difference between a unit test and an integration test? Which is faster? Which gives more confidence?

<details><summary>Answer</summary>

- **Unit test** — tests one class/method in isolation; all dependencies are mocked. Fast (milliseconds), deterministic, no I/O.
- **Integration test** — tests multiple real components together (e.g. service + EF Core + SQLite). Slower, but proves real components work together.

**Speed**: unit tests win. **Confidence**: integration tests win — mocks can lie, a real DB can't. Use both: unit tests for logic coverage, integration tests for data access and wiring.
</details>

<details><summary>Deep Dive</summary>

**The test pyramid**

```
        /\
       /E2E\        ← few, slow, expensive, high confidence
      /------\
     /Integr. \     ← some, medium speed, tests real wiring
    /----------\
   /  Unit       \  ← many, fast, cheap, isolate logic
  /--------------\
```

The pyramid isn't just about speed — it's about where you get the most value per test written. Unit tests are cheap to write and run, so you write many. Integration tests are slower but catch whole-layer bugs. E2E tests are slow and fragile but test the real user flow.

**What unit tests are bad at**

A unit test that mocks `IOrderRepository` can never catch:
- Wrong SQL (index missing, wrong JOIN)
- EF Core configuration errors (unmapped column, wrong type)
- Transaction isolation bugs
- Migration mistakes

You'd need an integration test hitting a real database for those.

**What integration tests are bad at**

Integration tests are slow and often non-deterministic (DB state bleeds between tests if not isolated). They're also harder to set up. They don't replace unit tests for covering every branch of complex business logic.

**The right question: what am I actually testing?**

```csharp
// Unit test — tests LOGIC: "does the order total calculation work?"
[Fact]
public void GetTotal_WithDiscount_SubtractsCorrectly()
{
    var order = new Order { Subtotal = 100, Discount = 20, DeliveryPrice = 5 };
    Assert.Equal(85, order.GetTotal());
}

// Integration test — tests WIRING: "does SaveChanges persist the order to the DB?"
[Fact]
public async Task CreateOrder_PersistsToDatabase()
{
    using var db = CreateTestDb(); // real SQLite
    var service = new OrderService(db);
    await service.CreateOrderAsync(dto);
    Assert.Single(db.Orders);
}
```

The first test would be nonsensical as an integration test. The second would be unreliable as a unit test (mock returns what you tell it, not what the DB would do).
</details>

---

**D2.** What is the AAA pattern (Arrange, Act, Assert)?

<details><summary>Answer</summary>

- **Arrange** — set up the system under test, dependencies, and input data.
- **Act** — call the single method/operation being tested.
- **Assert** — verify the outcome matches expectations.

One Act per test. If you have two Act sections, you have two tests. The pattern forces tests to be focused and readable.
</details>

<details><summary>Deep Dive</summary>

**AAA in practice**

```csharp
[Fact]
public async Task CreateOrder_WithValidInput_ReturnsOrderWithCorrectBuyer()
{
    // Arrange
    var mockRepo = new Mock<IOrderRepository>();
    var mockProductRepo = new Mock<IProductRepository>();
    mockProductRepo
        .Setup(r => r.GetByIdAsync(1))
        .ReturnsAsync(new Product { Id = 1, QuantityInStock = 10, Price = 50 });
    mockRepo
        .Setup(r => r.AddAsync(It.IsAny<Order>()))
        .Returns(Task.CompletedTask);

    var service = new OrderService(mockRepo.Object, mockProductRepo.Object);
    var dto = new CreateOrderDto { BuyerEmail = "alice@test.com", ProductId = 1, Quantity = 2 };

    // Act
    var result = await service.CreateOrderAsync(dto);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("alice@test.com", result.BuyerEmail);
}
```

**Why "one Act per test" matters**

```csharp
// BAD — two Acts, unclear what's being tested
[Fact]
public async Task OrderService_Works()
{
    var order = await service.CreateOrderAsync(dto);    // Act 1
    await service.CancelOrderAsync(order.Id);           // Act 2
    Assert.Equal(OrderStatus.Cancelled, order.Status);
}
// If this fails, is CreateOrder broken or CancelOrder?
```

Split into: `CreateOrder_Returns_PendingStatus` and `CancelOrder_Sets_CancelledStatus`.

**The blank line convention**

Separating the three sections with blank lines makes the structure immediately visible in code review. Tests should read like a specification:
```
Given [this setup]
When [this action is taken]
Then [this outcome is expected]
```

**`TestInitialize` / constructor as shared Arrange**

For setup shared across all tests in a class, put it in the constructor (xUnit) or `[TestInitialize]` (MSTest):

```csharp
public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _mockRepo = new();
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        // Shared Arrange — runs before every [Fact]
        _sut = new OrderService(_mockRepo.Object);
    }
}
```

This avoids repeating the setup in every test. The per-test Arrange only sets up what's unique to that test.
</details>

---

**D3.** What does `Mock<T>` do in Moq? What is the difference between `Setup` and `Verify`?

<details><summary>Answer</summary>

- **`Mock<T>`** — creates a fake implementation of interface `T` at runtime. By default all methods return default values (`null`, `0`, `false`). You control behavior via `Setup`.
- **`Setup`** — configures what the mock returns or does when a specific method is called. Runs during the Act.
- **`Verify`** — asserts after the Act that a specific method was (or was not) called with specific arguments. Without `Verify`, you have no proof the method was called.

`Setup` controls behavior. `Verify` proves the interaction happened.
</details>

<details><summary>Deep Dive</summary>

**The mock lifecycle**

```csharp
// 1. Create
var mockRepo = new Mock<IOrderRepository>();

// 2. Setup (Arrange phase) — tell the mock what to return
mockRepo
    .Setup(r => r.GetByIdAsync(42))
    .ReturnsAsync(new Order { Id = 42, Status = OrderStatus.Pending });

// 3. Use — pass to system under test
var service = new OrderService(mockRepo.Object);
await service.CancelOrderAsync(42);

// 4. Verify (Assert phase) — prove the interaction happened
mockRepo.Verify(r => r.UpdateAsync(It.Is<Order>(o => o.Status == OrderStatus.Cancelled)), Times.Once);
```

**`Setup` without `Verify` — the silent passing test**

```csharp
// Setup tells the mock what to return IF called
mockRepo.Setup(r => r.GetByIdAsync(42)).ReturnsAsync(order);

// If the production code never calls GetByIdAsync, the Setup just...
// doesn't fire. No error. Test passes. Bug in production code is invisible.
```

This is the most common mistake in Moq tests — `Setup` alone is not a test of interaction.

**Verify options**

```csharp
mock.Verify(r => r.SaveAsync(), Times.Once);           // called exactly once
mock.Verify(r => r.SaveAsync(), Times.Never);          // never called
mock.Verify(r => r.SaveAsync(), Times.AtLeastOnce());  // one or more times
mock.Verify(r => r.SaveAsync(), Times.Exactly(3));     // called 3 times
mock.Verify(r => r.LogError(It.IsAny<string>()),       // called with any string
    Times.Once);
```

**`MockBehavior.Strict` — fail if unexpected calls are made**

```csharp
var mockRepo = new Mock<IOrderRepository>(MockBehavior.Strict);
// Any method called without a Setup throws immediately
// Forces you to Setup everything the code touches
// Better for catching unexpected calls than default Loose behavior
```

**`VerifyAll()` — verify every setup was invoked**

```csharp
mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);
mockRepo.Setup(r => r.UpdateAsync(It.IsAny<Order>())).Returns(Task.CompletedTask);

// Act...

mockRepo.VerifyAll(); // both Setups must have been called — or exception
```

Saves writing individual `Verify` for each interaction.
</details>

---

**D4.** What three properties make a test valuable vs noise?

<details><summary>Answer</summary>

- **Fast** — runs in milliseconds; slow tests get skipped.
- **Deterministic** — same result every run, regardless of time, order, or environment.
- **Meaningful** — fails when real bugs exist and passes only when the code is correct. A test that never fails (or fails randomly) is noise.

A bonus fourth: **readable** — the intent is clear without comments; when it fails, you know exactly what broke.
</details>

<details><summary>Deep Dive</summary>

**The inverse: what makes a test noise**

```csharp
// NOISE — passes regardless of what the code does
[Fact]
public async Task CreateOrder_DoesNotThrow()
{
    await Record.ExceptionAsync(() => service.CreateOrderAsync(dto));
    // passes even if result is null, wrong buyer, wrong total
}

// NOISE — asserts the wrong thing
[Fact]
public async Task CreateOrder_ReturnsOrder()
{
    var result = await service.CreateOrderAsync(dto);
    Assert.NotNull(result); // passes as long as something non-null is returned
    // doesn't check buyer, items, total, status
}

// SIGNAL — specific, meaningful assertion
[Fact]
public async Task CreateOrder_SetsCorrectBuyerEmail()
{
    var result = await service.CreateOrderAsync("alice@test.com", dto);
    Assert.Equal("alice@test.com", result.BuyerEmail);
}
```

**Determinism — the flakiness killer**

Tests that depend on:
- `DateTime.Now` — different result at midnight vs noon
- Random number generation — different each run
- Shared static state — polluted by previous tests
- Thread scheduling — race conditions
- External services — network timeouts

...are non-deterministic. They're worse than no test — they erode trust in the test suite.

Fix: inject `TimeProvider` (instead of `DateTime.Now`), use fixed seeds, isolate state, mock external calls.

**The test that always passes is useless**

```csharp
// This "test" will never fail — it's a checkbox, not a safety net
[Fact]
public void Order_CanBeInstantiated()
{
    var order = new Order();
    Assert.NotNull(order);
}
```

The value of a test is directly proportional to how often it would catch a real regression. If you can't imagine a code change that would break this test, delete it.

**Meaningful assertion heuristics**

A good assertion checks:
- The **specific value** returned, not just `!= null`
- The **side effect** that occurred (via `Verify`)
- The **exception type and message** for error paths
- The **state change** in a collaborator (via captured argument or Verify)
</details>

---

**D5.** When should you use `[ExcludeFromCodeCoverage]`? When is it a code smell?

<details><summary>Answer</summary>

**Legitimate uses:**
- Auto-generated code (EF migrations, scaffolded files).
- Boilerplate with no logic (DTOs, simple POCO constructors).
- Infrastructure glue code that only runs with a real external system.

**Code smell when:**
- Applied to business logic to inflate coverage numbers.
- Used on a method because "it's hard to test" — that's a design problem.
- Covering up untested error paths or edge cases.

Rule: if a method has a branch, a calculation, or a decision — it should be tested, not excluded.
</details>

<details><summary>Deep Dive</summary>

**When exclusion is correct**

EF Core migrations are a good example:

```csharp
// Auto-generated — no logic, no need to test
[ExcludeFromCodeCoverage]
public partial class AddProductDescriptionMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(...);
    }
}
```

Other valid cases:
- `Program.cs` startup code that only runs in real hosting
- Razor Pages / MVC views (markup, not logic)
- Empty constructors, `ToString()` overrides with no logic

**When exclusion is dishonest**

```csharp
// BAD — hiding complexity behind exclusion
[ExcludeFromCodeCoverage]
public decimal CalculateTax(decimal subtotal, string countryCode)
{
    // 30 lines of complex tax logic for 15 countries
    // "It was too hard to test" is not a reason
}
```

This is the most dangerous use — it hides exactly the code most likely to contain bugs.

**The "hard to test" signal**

If a method is hard to test, that's almost always a design issue:
- It depends on `DateTime.Now` directly → inject `TimeProvider`
- It calls `new ExternalService()` directly → inject via interface
- It reads from a file directly → inject `IFileProvider`
- It has too many responsibilities → split it

`[ExcludeFromCodeCoverage]` should never be the answer to "this is hard to test." Refactor instead.

**Coverage targets aren't the goal**

Teams that chase 80% or 90% coverage metrics often start sprinkling `[ExcludeFromCodeCoverage]` to hit the number without writing tests. The metric becomes the goal instead of the actual quality. A better metric: mutation score (see D11).
</details>

---

### MEDIUM

**D6.** You have this test. Is it a good test? Why or why not?

```csharp
[Fact]
public async Task CreateOrder_ShouldWork()
{
    var result = await _service.CreateOrderAsync("buyer@test.com", _orderDto);
    Assert.NotNull(result);
}
```

<details><summary>Answer</summary>

**No — it's noise.** Problems:
- `Assert.NotNull` only proves *something* was returned, not that it's correct.
- No assertions on buyer email, order items, total, status, or any business behavior.
- The test name "ShouldWork" communicates nothing about what "working" means.
- No `Verify` — doesn't confirm the order was saved to the repository.
- Will pass even if the method returns `new Order()` with all default values.

A test that doesn't constrain behavior provides false confidence.
</details>

<details><summary>Deep Dive</summary>

**What this test actually proves**

The test proves exactly one thing: `CreateOrderAsync` does not throw an exception and does not return null. That's a very low bar. A method that does this:

```csharp
public Task<Order?> CreateOrderAsync(string email, OrderDto dto)
{
    return Task.FromResult<Order?>(new Order()); // wrong buyer, no items, no save
}
```

...would pass this test with flying colours.

**What the test should verify**

```csharp
[Fact]
public async Task CreateOrder_WithValidInput_ReturnsPendingOrderWithCorrectBuyer()
{
    // Arrange
    var dto = new CreateOrderDto { ProductId = 1, Quantity = 2, DeliveryMethodId = 1 };
    _mockProductRepo.Setup(r => r.GetByIdAsync(1))
        .ReturnsAsync(new Product { Id = 1, Price = 50, QuantityInStock = 10 });

    // Act
    var result = await _service.CreateOrderAsync("alice@test.com", dto);

    // Assert — specific, meaningful
    Assert.NotNull(result);
    Assert.Equal("alice@test.com", result.BuyerEmail);
    Assert.Equal(OrderStatus.Pending, result.Status);
    Assert.Single(result.OrderItems);
    Assert.Equal(100m, result.GetTotal()); // 50 * 2

    // Verify the order was actually persisted
    _mockOrderRepo.Verify(r => r.AddAsync(It.IsAny<Order>()), Times.Once);
}
```

**Why the name matters**

`CreateOrder_ShouldWork` gives zero information when it fails. Good test names follow the pattern:
`MethodName_Scenario_ExpectedResult`

```
CreateOrder_WithInsufficientStock_ThrowsInvalidOperationException
CreateOrder_WithValidInput_ReturnsPendingOrderWithCorrectBuyer
CreateOrder_WithNullEmail_ThrowsArgumentException
```

When CI fails, you read the test name and immediately know what broke and why.

**The "Assert.NotNull treadmill"**

`Assert.NotNull` is almost always a test smell. The exceptions:
- You're testing a factory that should never return null
- You're testing a query result where null = "not found" and the test is for the found case

Even then, add at least one specific assertion after it.
</details>

---

**D7.** Your Moq `Setup` uses `It.IsAny<TimeSpan?>()` but the production code calls an overload that takes `Expiration` (not `TimeSpan?`). The test passes. You delete the production method. The test still passes. Explain the full chain of events. How do you prevent this?

<details><summary>Answer</summary>

The `Setup` targets the wrong overload — the one taking `TimeSpan?`. The production code calls a different overload taking `Expiration`. Moq never matches the setup; the mock falls back to the default loose behavior (returns `false` for `bool` methods). The production code proceeds anyway.

When you delete the production method, nothing changes — the test never called it through the mock. The setup was targeting dead air the whole time.

**Prevention:**
- Use `MockBehavior.Strict` — any unmocked call throws immediately.
- Add `Verify` — explicitly assert the specific overload was called.
- Check the exact method signature in IntelliSense before writing the `Setup`.
</details>

<details><summary>Deep Dive</summary>

**The overload mismatch in detail**

```csharp
// StackExchange.Redis has two overloads:
bool StringSet(RedisKey key, RedisValue value, TimeSpan? expiry = null, ...);
bool StringSet(RedisKey key, RedisValue value, Expiration expiry, ...);

// Test Setup — targets the TimeSpan? overload
_mockDb.Setup(x => x.StringSet(
    It.IsAny<RedisKey>(),
    It.IsAny<RedisValue>(),
    It.IsAny<TimeSpan?>(),   ← TimeSpan? overload
    ...))
    .Returns(true);

// Production code — calls the Expiration overload
_db.StringSet(key, value, new Expiration(ExpiryType.Absolute, TimeSpan.FromMinutes(30)));
//                         ↑ Expiration, not TimeSpan?
```

Moq sees the call to the `Expiration` overload, finds no matching Setup, and returns the default value (`false` for `bool`). The code may or may not care about the return value — if it doesn't, it silently proceeds.

**Why the test passes after deleting the method**

```
Test calls _cartService.SetCartAsync()
  → SetCartAsync calls _db.StringSet(Expiration overload)   ← this call was never setup
  → Mock returns false (default loose behavior)
  → SetCartAsync returns the cart object regardless
  → Assert.NotNull(result) passes

After deleting SetCartAsync:
  → Test doesn't call it at all
  → Assert.NotNull(result) — wait, result is now what?
  → Actually: if the test still passes, the method was never actually invoked
    OR the test variable was set in Arrange and never touched by Act
```

**MockBehavior.Strict — the safeguard**

```csharp
var mockDb = new Mock<IDatabaseAsync>(MockBehavior.Strict);
// Now ANY call without a matching Setup throws MockException immediately
// Forces you to explicitly Setup every method the production code touches
// Makes the overload mismatch immediately visible
```

**Explicit Verify as the minimal fix**

```csharp
_mockDb.Verify(x => x.StringSetAsync(
    It.IsAny<RedisKey>(),
    It.IsAny<RedisValue>(),
    It.IsAny<TimeSpan?>(),
    It.IsAny<When>(),
    It.IsAny<CommandFlags>()),
    Times.Once);
// If production code calls a different overload → Verify fails → test fails
// If production method is deleted → Verify fails → test fails
```
</details>

---

**D8.** Write a test for this method. Include happy path and at least two edge cases:

```csharp
public decimal GetTotal()
{
    return Subtotal - Discount + DeliveryMethod.Price;
}
```

<details><summary>Answer</summary>

```csharp
[Fact]
public void GetTotal_WithSubtotalDiscountAndDelivery_ReturnsCorrectSum()
{
    var order = new Order
    {
        Subtotal = 100m,
        Discount = 20m,
        DeliveryMethod = new DeliveryMethod { Price = 5m }
    };
    Assert.Equal(85m, order.GetTotal()); // 100 - 20 + 5
}

[Fact]
public void GetTotal_WhenDiscountExceedsSubtotal_ReturnsNegativeTotal()
{
    var order = new Order
    {
        Subtotal = 50m,
        Discount = 60m,
        DeliveryMethod = new DeliveryMethod { Price = 5m }
    };
    Assert.Equal(-5m, order.GetTotal()); // reveals whether negative totals are intended
}

[Fact]
public void GetTotal_WhenDiscountIsZero_ReturnsSubtotalPlusDelivery()
{
    var order = new Order
    {
        Subtotal = 100m,
        Discount = 0m,
        DeliveryMethod = new DeliveryMethod { Price = 10m }
    };
    Assert.Equal(110m, order.GetTotal());
}
```
</details>

<details><summary>Deep Dive</summary>

**Why the negative total test is important**

The edge case where `Discount > Subtotal` reveals a design question: *should this be allowed?* The test doesn't assert that it's correct — it documents current behavior and forces a conversation.

If the business rule is "minimum total is zero," the implementation is missing a guard:
```csharp
public decimal GetTotal()
{
    return Math.Max(0, Subtotal - Discount) + DeliveryMethod.Price;
}
```

Writing the test makes the bug visible before it reaches production (and before a customer sees a -$5 invoice).

**NullReferenceException edge case**

```csharp
[Fact]
public void GetTotal_WhenDeliveryMethodIsNull_ThrowsNullReferenceException()
{
    var order = new Order { Subtotal = 100m, Discount = 0m, DeliveryMethod = null };
    Assert.Throws<NullReferenceException>(() => order.GetTotal());
    // OR: if you want a better error, assert InvalidOperationException
}
```

This reveals that `DeliveryMethod` is not null-guarded. The fix might be a null check with a meaningful exception, or making the property non-nullable in the model.

**Zero values**

```csharp
[Fact]
public void GetTotal_WithAllZeroValues_ReturnsZero()
{
    var order = new Order
    {
        Subtotal = 0m,
        Discount = 0m,
        DeliveryMethod = new DeliveryMethod { Price = 0m }
    };
    Assert.Equal(0m, order.GetTotal());
}
```

**Using `[Theory]` to avoid repetition**

```csharp
[Theory]
[InlineData(100, 20, 5,  85)]   // normal
[InlineData( 50, 60, 5,  -5)]   // discount > subtotal
[InlineData(100,  0, 10, 110)]  // no discount
[InlineData(  0,  0,  0,   0)]  // all zero
public void GetTotal_ReturnsExpectedResult(
    decimal subtotal, decimal discount, decimal delivery, decimal expected)
{
    var order = new Order
    {
        Subtotal = subtotal,
        Discount = discount,
        DeliveryMethod = new DeliveryMethod { Price = delivery }
    };
    Assert.Equal(expected, order.GetTotal());
}
```

`[Theory]` + `[InlineData]` is the idiomatic xUnit pattern for parameterized tests. All cases run independently — one failure doesn't cancel the others.
</details>

---

**D9.** What is the difference between `mockDb.Setup(...)` and `mockDb.As<IDatabaseAsync>().Setup(...)`? When do you need the `As<>` cast? What happens if you use the wrong one?

<details><summary>Answer</summary>

`mockDb.Setup(...)` sets up methods on the concrete mock type `T`. `mockDb.As<IDatabaseAsync>().Setup(...)` sets up methods on an interface that `T` implements — necessary when the production code holds a reference of type `IDatabaseAsync`, not the concrete type.

If you use the wrong one: the production code calls through the `IDatabaseAsync` interface reference, Moq finds no matching setup on that interface, falls back to default values, and your setup silently never fires. The test passes even though the expected call never happened.
</details>

<details><summary>Deep Dive</summary>

**Why this matters for StackExchange.Redis**

`IDatabase` (StackExchange.Redis) inherits from `IDatabaseAsync`. If your service receives `IDatabaseAsync`:

```csharp
public class CartService
{
    private readonly IDatabaseAsync _db; // holds IDatabaseAsync reference

    public CartService(IDatabaseAsync db) { _db = db; }

    public async Task<bool> SetAsync(string key, string value)
    {
        return await _db.StringSetAsync(key, value); // called on IDatabaseAsync
    }
}
```

And your test does:

```csharp
var mockDb = new Mock<IDatabase>(); // mock is IDatabase

// WRONG — sets up on IDatabase, but code calls IDatabaseAsync
mockDb.Setup(x => x.StringSetAsync(...)).ReturnsAsync(true);

// CORRECT — cast to IDatabaseAsync first
mockDb.As<IDatabaseAsync>()
      .Setup(x => x.StringSetAsync(...))
      .ReturnsAsync(true);
```

**What Moq does under the hood**

`Mock<T>` generates a proxy class for `T`. `As<TInterface>()` extends that proxy to also implement `TInterface`, creating a separate "face" of the same mock object. Setups on `mockDb` and setups on `mockDb.As<IDatabaseAsync>()` are stored separately.

When the production code calls `_db.StringSetAsync(...)` on an `IDatabaseAsync` reference:
1. Moq checks: do I have a setup on `IDatabaseAsync` for this signature?
2. If no `As<IDatabaseAsync>().Setup(...)` was done → no match → returns default.

**The diagnostic test**

If your setup isn't firing, add `MockBehavior.Strict`:

```csharp
var mockDb = new Mock<IDatabase>(MockBehavior.Strict);
// Strict: any unexpected call throws MockException
// If StringSetAsync is called but not set up → immediate exception with full signature info
// → reveals exactly which interface/method is being called
```

**Multiple interface implementations**

```csharp
var mock = new Mock<IConnectionMultiplexer>();

// IConnectionMultiplexer also "is" IDisposable
mock.As<IDisposable>().Setup(x => x.Dispose()).Verifiable();

// Setup different methods on different interface faces of the same mock
mock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
    .Returns(mockDb.Object);
```
</details>

---

**D10.** You want to test that `OrderService.CreateOrderAsync` decrements stock correctly. You use EF Core InMemory database. You seed two products with stock=10. After creating an order with qty=2 for product 1, you assert `product1.QuantityInStock == 8`. The test passes. Is this test reliable? What can go wrong?

<details><summary>Answer</summary>

**Not fully reliable.** The test passes but gives false confidence because:

- EF Core InMemory **does not enforce concurrency tokens** (`[ConcurrencyCheck]`, `[Timestamp]`). Two simultaneous decrements won't throw `DbUpdateConcurrencyException` — both succeed, giving wrong stock.
- InMemory **doesn't enforce foreign keys, unique constraints, or CHECK constraints** — the test passes with data that would be rejected by a real database.
- The test might be testing **the same `DbContext` instance** used to seed, so the entity is already tracked and the assertion reads from the change tracker, not from a real "re-fetched" state.

Use **SQLite in-process** for tests that care about constraints or concurrency.
</details>

<details><summary>Deep Dive</summary>

**The change tracker trap**

```csharp
// Seed
var product1 = new Product { Id = 1, QuantityInStock = 10 };
context.Products.Add(product1);
await context.SaveChangesAsync();

// Act — service uses the SAME context
await service.CreateOrderAsync(dto);

// Assert — reading from the SAME tracked entity
var product = await context.Products.FindAsync(1);
Assert.Equal(8, product.QuantityInStock); // always passes because it's the same object
```

`FindAsync` with InMemory returns the SAME object from the identity map — not a fresh read. If the service modified `product1.QuantityInStock` directly on the tracked instance, this assertion would pass even without calling `SaveChangesAsync`.

**The correct approach — use a second context**

```csharp
var options = new DbContextOptionsBuilder<StoreContext>()
    .UseSqlite("DataSource=:memory:")
    .Options;

using var seedContext = new StoreContext(options);
seedContext.Database.EnsureCreated();
seedContext.Products.Add(new Product { Id = 1, QuantityInStock = 10 });
await seedContext.SaveChangesAsync();

using var actContext = new StoreContext(options);
var service = new OrderService(actContext);
await service.CreateOrderAsync(dto);

using var assertContext = new StoreContext(options); // FRESH context — no tracked entities
var product = await assertContext.Products.FindAsync(1);
Assert.Equal(8, product!.QuantityInStock);
```

Three separate contexts: one to seed, one to act, one to assert. This proves the data actually hit the database and was read back correctly.

**What InMemory silently ignores**

```csharp
// These all succeed with InMemory — fail with real SQL:
context.Orders.Add(new Order { ProductId = 999 }); // FK violation → ignored
context.Products.Add(new Product { Sku = "SAME" });
context.Products.Add(new Product { Sku = "SAME" }); // unique constraint → ignored
product.QuantityInStock = -100;                      // CHECK constraint → ignored
```

A test that passes with InMemory but fails in production is worse than no test — it builds false trust.
</details>

---

**D11.** What is mutation testing? How does it differ from code coverage? If you have 100% code coverage, does that mean your tests are good? Give an example where 100% coverage still misses a bug.

<details><summary>Answer</summary>

- **Code coverage** — measures which lines/branches were *executed* by tests. Does not measure whether assertions would catch a bug.
- **Mutation testing** — automatically introduces small bugs (mutations) into your code and checks whether your test suite catches them. If a test still passes after a mutation, the test is "weak."

100% coverage does NOT mean tests are good. A test with `Assert.NotNull(result)` covers every line of a method but wouldn't catch a wrong calculation, missing `Verify`, or off-by-one error.
</details>

<details><summary>Deep Dive</summary>

**The 100% coverage, 0% confidence example**

```csharp
public decimal ApplyDiscount(decimal price, decimal discountPercent)
{
    return price - (price * discountPercent / 100);
}

// Test — achieves 100% line coverage
[Fact]
public void ApplyDiscount_DoesNotThrow()
{
    var result = ApplyDiscount(100, 20);
    Assert.NotNull((object)result); // decimal is never null — always passes
}
```

100% coverage. The test never checks that `ApplyDiscount(100, 20)` returns `80`. If you change the formula to `price * discountPercent / 100` (returns just the discount amount, not the discounted price), the test still passes.

**What mutation testing does**

A mutation testing tool (Stryker.NET for C#) would:
1. Change `price - (price * discountPercent / 100)` to `price + (price * discountPercent / 100)`
2. Run your test suite
3. If the test still passes → "survived mutation" → test is weak

A "killed mutation" means your test caught the introduced bug. The goal is to kill all (or most) mutations.

**Common mutation operators**

| Original | Mutation |
|---|---|
| `a - b` | `a + b` |
| `> 0` | `>= 0` |
| `&&` | `\|\|` |
| `return true` | `return false` |
| `if (x != null)` | `if (x == null)` |
| `Times.Once` (Verify) | `Times.Never` |

**Stryker.NET usage**

```bash
dotnet tool install -g dotnet-stryker
dotnet stryker --project MyProject.Tests.csproj
```

Generates an HTML report showing which mutations survived (weak tests) and which were killed (strong tests). A mutation score of 80%+ is generally considered good.

**When 100% coverage is meaningful**

100% coverage is meaningful only when combined with:
- Specific assertions (`Assert.Equal(expected, actual)`, not `Assert.NotNull`)
- `Verify` calls confirming interactions
- Edge case tests (boundary values, empty collections, nulls)

Together, these produce both high coverage AND high mutation score.
</details>

---

**D12.** You have a Moq `Setup(...).ReturnsAsync(false)` but no `Verify`. You delete the production method. The test still passes. Why?

<details><summary>Answer</summary>

`Setup` only configures what the mock *returns if called* — it's not an assertion. If the production method is deleted, the mock method is never called, the `Setup` never fires, and no error occurs. Without a `Verify`, there's nothing to fail.

The test was testing the wrong thing: it was asserting the output of a method that may or may not have used the mock, not that the mock was actually called.

**Fix:** add `mockService.Verify(x => x.TheMethod(...), Times.Once)` after the Act.
</details>

<details><summary>Deep Dive</summary>

**The exact sequence of events**

```csharp
// Test (broken)
_mockDb.Setup(x => x.StringSetAsync(...)).ReturnsAsync(false);

var result = await _cartService.SetCartAsync(cart); // Act

result.Should().NotBeNull(); // passes regardless
// No Verify — test ends here, passes
```

After deleting `SetCartAsync`:
```csharp
// Now calling a non-existent method → compile error, test can't run
```

Wait — but the question says the test still passes. That means the test is calling a *different* method or the deleted method was on the production service, not the test's direct call target.

**The real scenario**

```csharp
// Production: CartService calls _db.StringSetAsync internally
public async Task<ShoppingCart?> SetCartAsync(ShoppingCart cart)
{
    // ... previously called _db.StringSetAsync here
    // NOW DELETED: the DB call is gone
    return cart; // returns cart without persisting
}

// Test
_mockDb.Setup(x => x.StringSetAsync(...)).ReturnsAsync(false);
var result = await _cartService.SetCartAsync(cart);
result.Should().NotBeNull(); // still passes — cart is returned in-memory

// Verify was never added — nobody checks _db.StringSetAsync was called
```

The test proves the cart object is returned but doesn't prove it was persisted. A critical data persistence bug is invisible.

**The correct test**

```csharp
_mockDb.Setup(x => x.StringSetAsync(
    It.IsAny<RedisKey>(),
    It.IsAny<RedisValue>(),
    It.IsAny<TimeSpan?>(),
    It.IsAny<When>(),
    It.IsAny<CommandFlags>()))
    .ReturnsAsync(true);

var result = await _cartService.SetCartAsync(cart);

result.Should().NotBeNull();
result!.Id.Should().Be(cart.Id);

// This is the critical assertion:
_mockDb.Verify(x => x.StringSetAsync(
    It.Is<RedisKey>(k => k == cart.Id),
    It.IsAny<RedisValue>(),
    It.IsAny<TimeSpan?>(),
    It.IsAny<When>(),
    It.IsAny<CommandFlags>()),
    Times.Once);
// NOW: delete SetCartAsync → Verify fails → test catches the regression
```
</details>

---

**D13.** `Order.GetTotal()` returns `-5` when discount is `60`, subtotal is `50`, shipping is `5`. You don't know if this is intentional. What do you do? Write the test.

<details><summary>Answer</summary>

Don't silently accept it — surface the question. Write the test to document current behavior, then raise the design question with the team.

```csharp
[Fact]
public void GetTotal_WhenDiscountExceedsSubtotal_ReturnsNegativeTotal()
{
    // Current behavior: no floor. Negative totals are possible.
    // TODO: confirm with business — should this be clamped to 0?
    var order = new Order
    {
        Subtotal = 50m,
        Discount = 60m,
        DeliveryMethod = new DeliveryMethod { Price = 5m }
    };
    Assert.Equal(-5m, order.GetTotal());
}
```

If the business confirms it's wrong, update the implementation and the test together.
</details>

<details><summary>Deep Dive</summary>

**Why document-then-ask, not assume-then-fix**

Fixing it silently (adding `Math.Max(0, ...)`) without consulting the business could break legitimate use cases:
- A heavily discounted order during a promotion
- A refund scenario where negative is intentional
- A rounding case in a specific jurisdiction

The test becomes living documentation: "as of [date], negative totals are possible." The comment `// TODO: confirm with business` makes the open question visible in code review and on CI.

**The three outcomes and their tests**

After consulting the business, one of these is correct:

```csharp
// Outcome A: negative totals allowed (current behavior is intentional)
Assert.Equal(-5m, order.GetTotal()); // test as-is, remove TODO

// Outcome B: total floors at zero, delivery still applied
// Implementation: Math.Max(0, Subtotal - Discount) + DeliveryMethod.Price
Assert.Equal(5m, order.GetTotal()); // 0 + 5

// Outcome C: discount cannot exceed subtotal — throw at assignment
// Implementation: throw InvalidOperationException in Discount setter
Assert.Throws<InvalidOperationException>(() => order.Discount = 60m);
```

Each outcome requires a different implementation change. Writing the test first forces clarity on which outcome is correct before touching the code.

**The broader principle: tests reveal design gaps**

When you find unexpected behavior while writing a test, it's almost always one of:
1. A genuine bug — fix it
2. An undocumented business rule — document and test it
3. A design ambiguity — resolve it with the team

`Assert.Equal(-5m, ...)` with a clear name and comment is vastly more honest than `Assert.True(result < 0)` or silently changing the method.
</details>

---

**D14.** What coverage target do you set for each layer (Core, Application, Infrastructure, API controllers) and why?

<details><summary>Answer</summary>

- **Core (entities, value objects, domain logic)** — 90–100%. Pure C#, no I/O, easy to test. This is where your most valuable logic lives.
- **Application (services, use cases)** — 80–90%. Complex orchestration; mock dependencies. Cover all branches and error paths.
- **Infrastructure (EF repositories, Redis, Stripe)** — 40–60%. Integration tests replace unit tests here; InMemory/SQLite tests for EF, skip Redis/Stripe unless critical.
- **API controllers** — 30–50%. Thin mapping layer; if service layer is covered, controllers add little value. Test only routing, model validation, and status codes.

Never chase a single global coverage number — it averages out the meaningful (Core) with the meaningless (migrations, DTOs).
</details>

<details><summary>Deep Dive</summary>

**Why Core deserves the highest target**

Core entities and domain logic have zero I/O dependencies — every test is a pure unit test. They're fast, deterministic, and easy to write. There's no excuse for < 90% here. This is the code that embodies your business rules — a bug here is a business bug.

```csharp
// Core — pure logic, no mocks needed
public class Order
{
    public decimal GetTotal() => Subtotal - Discount + DeliveryMethod.Price;
    public bool CanBeCancelled() => Status == OrderStatus.Pending;
    public void Cancel() { if (!CanBeCancelled()) throw new InvalidOperationException(); Status = OrderStatus.Cancelled; }
}
// Every method, every branch is trivially testable
```

**Why Infrastructure coverage is lower**

Infrastructure code (`OrderRepository`, `RedisCartService`) wraps external systems. Testing it with InMemory/SQLite catches EF configuration bugs. But you can't meaningfully unit-test Redis behavior without Redis. Integration tests (with a real test container) cover this layer — they don't count in standard code coverage metrics.

```csharp
// Not worth a unit test — the logic IS the EF call
public async Task<Order?> GetByIdAsync(int id) =>
    await _context.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == id);
```

An integration test with real SQLite proves this works. A unit test would just be mocking EF which tests nothing.

**Why controllers deserve low coverage targets**

Modern API controllers are thin:

```csharp
[HttpPost]
public async Task<ActionResult<OrderDto>> CreateOrder(CreateOrderDto dto)
{
    var order = await _orderService.CreateOrderAsync(User.GetEmail(), dto);
    return order is null ? BadRequest() : Ok(_mapper.Map<OrderDto>(order));
}
```

The business logic is in `_orderService` (tested in Application layer). All the controller does is:
- Call the service
- Map the result
- Return the right status code

Test the mapping and status codes, not every branch of the service.

**Practical CI enforcement**

```xml
<!-- In .csproj or coverlet.runsettings -->
<Threshold>80</Threshold>
<ThresholdType>line</ThresholdType>
<ExcludeByFile>**/Migrations/**,**/Program.cs</ExcludeByFile>
```

Set the threshold at 80% globally but exclude Infrastructure and Migrations. The meaningful layers (Core, Application) will pull the average up.
</details>

---

### HARD

**D15.** Design a test for optimistic concurrency. You need to prove that two simultaneous orders for the last item result in exactly one success and one failure. You cannot use `Thread.Sleep`. How do you simulate concurrency in a unit test? What database provider do you use? Why can't you use InMemory?

<details><summary>Answer</summary>

Use **SQLite in-process** (not InMemory — it doesn't enforce concurrency tokens). Simulate concurrency with two separate `DbContext` instances reading the same product, then saving in sequence — the second save should throw `DbUpdateConcurrencyException`.

You don't need real threading. The race condition is captured by the optimistic lock: both contexts read stock=1, first saves stock=0, second tries to save stock=0 with original=1 but the DB already shows 0 → mismatch → exception.
</details>

<details><summary>Deep Dive</summary>

**Why InMemory fails for this test**

InMemory doesn't run `WHERE Id = @id AND QuantityInStock = @original` — it ignores concurrency tokens entirely. Both saves succeed, stock goes to -1, and your test proves nothing.

**The test structure**

```csharp
[Fact]
public async Task CreateOrder_WhenTwoRequestsRaceForLastItem_OnlyOneSucceeds()
{
    // Arrange — SQLite in-memory with real SQL semantics
    var connection = new SqliteConnection("DataSource=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<StoreContext>()
        .UseSqlite(connection)
        .Options;

    // Seed: one product with stock = 1
    using (var seedCtx = new StoreContext(options))
    {
        await seedCtx.Database.EnsureCreatedAsync();
        seedCtx.Products.Add(new Product { Id = 1, QuantityInStock = 1, Price = 50 });
        await seedCtx.SaveChangesAsync();
    }

    // Simulate two concurrent requests — each reads the product BEFORE either saves
    using var ctx1 = new StoreContext(options);
    using var ctx2 = new StoreContext(options);

    var product1 = await ctx1.Products.FindAsync(1); // reads stock = 1
    var product2 = await ctx2.Products.FindAsync(1); // reads stock = 1

    // Request 1 decrements and saves first
    product1!.QuantityInStock -= 1; // stock = 0
    await ctx1.SaveChangesAsync();  // succeeds — WHERE Id=1 AND QuantityInStock=1 → 1 row affected

    // Request 2 tries to decrement — it still has original = 1 in its snapshot
    product2!.QuantityInStock -= 1; // stock = 0
    var exception = await Record.ExceptionAsync(() => ctx2.SaveChangesAsync());
    // WHERE Id=1 AND QuantityInStock=1 → 0 rows affected (DB has 0 now) → exception

    // Assert
    Assert.IsType<DbUpdateConcurrencyException>(exception);

    // Verify the DB reflects only one decrement
    using var verifyCtx = new StoreContext(options);
    var finalProduct = await verifyCtx.Products.FindAsync(1);
    Assert.Equal(0, finalProduct!.QuantityInStock);
}
```

**Why this doesn't need `Thread.Sleep` or real threads**

The concurrency scenario is about *data state*, not timing. Both contexts read the same stale value. The first write invalidates the second write's precondition. This is deterministic — no threading required.

Real threading would only matter if you were testing a locking mechanism. Optimistic concurrency is stateless — the race is simulated by reading before writing across two contexts.

**Ensuring `[ConcurrencyCheck]` is set up**

```csharp
// Entity must have the concurrency token
public class Product
{
    public int Id { get; set; }

    [ConcurrencyCheck]
    public int QuantityInStock { get; set; }
}
```

Without the token, EF generates `WHERE Id = @id` (no concurrency check) — both saves succeed even in SQLite.

**What the test proves**

1. `DbUpdateConcurrencyException` is thrown on the second save.
2. The database reflects exactly one decrement.
3. The `[ConcurrencyCheck]` attribute is correctly configured on the entity.
4. The service/retry logic handles the exception correctly (extend the test to cover retry behavior).
</details>

---

**D16.** A colleague writes this test and says "It validates the happy path":

```csharp
[Fact]
public async Task SetCart_ReturnsCart()
{
    _mockDb.Setup(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<TimeSpan?>(),
        It.IsAny<When>(),
        It.IsAny<CommandFlags>()))
        .ReturnsAsync(true);

    var result = await _cartService.SetCartAsync(new ShoppingCart { Id = "cart1" });

    result.Should().NotBeNull();
    result!.Id.Should().Be("cart1");
}
```

Delete `SetCartAsync` from the service. Does the test break? Why or why not? Write the corrected test.

<details><summary>Answer</summary>

- **Yes — the test breaks with a compile error** because it directly calls `_cartService.SetCartAsync(...)` which no longer exists. The test can't compile, let alone run.
- **However, the test is still weak**: there is no `Verify` confirming `StringSetAsync` was actually called. If `SetCartAsync` returns the cart without calling Redis at all, the test passes — the Setup's absence is invisible.

**The corrected test** adds a `Verify` on the Redis call and pins the exact cart ID in the key argument.
</details>

<details><summary>Deep Dive</summary>

**What the test currently proves**

1. `SetCartAsync` returns a non-null object.
2. The returned object has `Id == "cart1"`.

What it does **not** prove:
- That Redis was called at all.
- That the correct key was used (`"cart1"`, not `"default"` or `null`).
- That the serialized value is correct (correct JSON, correct fields).
- That the TTL/expiry was set.

A service that does `return cart;` (ignoring Redis entirely) passes this test perfectly.

**The corrected test**

```csharp
[Fact]
public async Task SetCart_PersistsToRedisAndReturnsCart()
{
    // Arrange
    var cart = new ShoppingCart { Id = "cart1", Items = new List<BasketItem>() };

    _mockDb.Setup(x => x.StringSetAsync(
        It.Is<RedisKey>(k => k == cart.Id),      // pin the exact key
        It.IsAny<RedisValue>(),
        It.IsAny<TimeSpan?>(),
        It.IsAny<When>(),
        It.IsAny<CommandFlags>()))
        .ReturnsAsync(true);

    // Act
    var result = await _cartService.SetCartAsync(cart);

    // Assert — return value
    result.Should().NotBeNull();
    result!.Id.Should().Be("cart1");

    // Assert — Redis was actually called with the right key
    _mockDb.Verify(x => x.StringSetAsync(
        It.Is<RedisKey>(k => k == cart.Id),
        It.IsAny<RedisValue>(),
        It.IsAny<TimeSpan?>(),
        It.IsAny<When>(),
        It.IsAny<CommandFlags>()),
        Times.Once);
}
```

Now: delete `SetCartAsync` → compile error. Implement it without Redis → `Verify` fails. Implement it with the wrong key → `It.Is<RedisKey>(k => k == cart.Id)` fails. The test is a real safety net.

**What to say in code review**

> "This test passes even if we remove the Redis call entirely — there's no `Verify` on `StringSetAsync`. The assertion only checks the return value, not the side effect. Add `_mockDb.Verify(...)` to prove Redis is actually called. Also consider using `MockBehavior.Strict` so any unexpected calls fail immediately."
</details>

---

**D17.** Your test suite has 128 tests. 126 pass, 2 fail intermittently (sometimes pass, sometimes fail on CI). These are called "flaky tests." Name five common causes of flaky tests and how to fix each.

<details><summary>Answer</summary>

1. **Shared mutable state** — tests pollute a static field or singleton. Fix: reset state in `[SetUp]`/`Dispose`, use fresh instances per test.
2. **Time-dependent assertions** — `DateTime.Now` changes between runs or across timezones. Fix: inject `TimeProvider` and freeze time in tests.
3. **Race conditions in async tests** — `Task.Run` + no proper await. Fix: always `await` in tests; use `Task.WhenAll` for true parallel.
4. **Test ordering dependency** — test B relies on state created by test A. Fix: each test must arrange its own state independently.
5. **External resource unavailability** — CI has no network, no Redis, no real DB. Fix: mock external calls or use containers (`Testcontainers`).
</details>

<details><summary>Deep Dive</summary>

**Cause 1: Shared static state**

```csharp
// BAD — static field shared across all test instances
public static class Counter { public static int Value = 0; }

[Fact]
public void Test1() { Counter.Value++; Assert.Equal(1, Counter.Value); }
[Fact]
public void Test2() { Counter.Value++; Assert.Equal(1, Counter.Value); } // fails if Test1 ran first
```

Fix: avoid static state in code under test. If unavoidable, reset in `IAsyncLifetime.InitializeAsync()` (xUnit) or `[TestInitialize]` (MSTest).

**Cause 2: Time-dependent logic**

```csharp
// BAD
var order = new Order { CreatedAt = DateTime.UtcNow };
Assert.True(order.IsExpired()); // what if the test runs at 23:59:59?
```

Fix: inject `TimeProvider` (abstract class in .NET 8):

```csharp
// Production
public class Order
{
    private readonly TimeProvider _time;
    public bool IsExpired() => _time.GetUtcNow() > ExpiresAt;
}

// Test
var fakeTime = new FakeTimeProvider();
fakeTime.SetUtcNow(new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero));
var order = new Order(fakeTime) { ExpiresAt = new DateTimeOffset(2025, 1, 1, 11, 0, 0, TimeSpan.Zero) };
Assert.True(order.IsExpired()); // deterministic
```

**Cause 3: Unwaited async**

```csharp
// BAD — fire and forget in a test
[Fact]
public void TestAsync()
{
    Task.Run(() => _service.DoWorkAsync()); // not awaited
    Assert.Equal(1, _service.CompletedCount); // races against the Task
}

// GOOD
[Fact]
public async Task TestAsync()
{
    await _service.DoWorkAsync(); // awaited
    Assert.Equal(1, _service.CompletedCount); // deterministic
}
```

**Cause 4: Test ordering dependency**

xUnit runs tests in a deterministic but unspecified order. Never rely on it.

```csharp
// BAD — Test2 depends on Test1 having run first
[Fact] public async Task Test1_CreateOrder() { /* creates order ID 1 */ }
[Fact] public async Task Test2_FetchOrder_Expects_Id1() { var o = await repo.GetByIdAsync(1); Assert.NotNull(o); }
```

Fix: every test seeds its own data. Use a unique random ID (`Guid.NewGuid()`) to avoid collisions.

**Cause 5: External dependency unavailability**

CI runners often have no Redis, no SMTP, no third-party API. Tests that hit these directly will pass locally and fail on CI.

```csharp
// BAD — real Redis in a unit test
private readonly IDatabase _redis = ConnectionMultiplexer.Connect("localhost").GetDatabase();
```

Fix: mock with Moq for unit tests, use Testcontainers for integration tests:

```csharp
// Integration test with real Redis in Docker
var redisContainer = new RedisBuilder().Build();
await redisContainer.StartAsync();
var connection = await ConnectionMultiplexer.ConnectAsync(redisContainer.GetConnectionString());
```

Testcontainers spins up a real Redis Docker container per test session, then tears it down. Fully reproducible in any CI environment with Docker.
</details>

---

**D18.** You need to test `PaymentService` but it calls `new PaymentIntentService()` directly — no DI seam. You can't modify the production code (it's a third-party SDK pattern). What are your options? Compare at least three approaches with trade-offs.

<details><summary>Answer</summary>

1. **Wrapper interface** — create `IStripePaymentIntentService` wrapping `PaymentIntentService`; inject the interface. Best option if you *can* add a seam — clean, testable.
2. **Integration test only** — don't unit-test; write an integration test against the Stripe test API. Honest but slow and requires network.
3. **Extract and override** — extract the `new PaymentIntentService()` call into a `protected virtual` method; subclass in tests to inject a fake. Works without changing the public API.
4. **Functional seam (delegate)** — pass a `Func<PaymentIntentService>` factory; tests inject a factory returning a fake.

If you truly cannot touch the production code, option 2 (integration test) is the most honest.
</details>

<details><summary>Deep Dive</summary>

**Option 1: Wrapper interface (ideal, requires code change)**

```csharp
// Production wrapper
public interface IStripePaymentService
{
    Task<PaymentIntent> CreateAsync(PaymentIntentCreateOptions options);
}

public class StripePaymentService : IStripePaymentService
{
    private readonly PaymentIntentService _stripe = new();

    public Task<PaymentIntent> CreateAsync(PaymentIntentCreateOptions options)
        => _stripe.CreateAsync(options);
}

// PaymentService now takes IStripePaymentService
public class PaymentService
{
    private readonly IStripePaymentService _stripe;
    public PaymentService(IStripePaymentService stripe) { _stripe = stripe; }
}

// Test — mock the interface
var mockStripe = new Mock<IStripePaymentService>();
mockStripe.Setup(s => s.CreateAsync(It.IsAny<PaymentIntentCreateOptions>()))
    .ReturnsAsync(new PaymentIntent { Id = "pi_test", Status = "succeeded" });
```

Trade-off: requires modifying `PaymentService`. Best long-term solution.

**Option 2: Integration test against Stripe Test API**

```csharp
// Uses real Stripe with test keys — no mocks
[Fact]
public async Task CreatePaymentIntent_WithValidAmount_ReturnsSucceeded()
{
    StripeConfiguration.ApiKey = "sk_test_..."; // from env variable, never committed

    var service = new PaymentService(); // real service, real Stripe
    var result = await service.CreatePaymentIntentAsync(amount: 1000);

    Assert.Equal("requires_payment_method", result.Status);
}
```

Trade-off: slow (network call), flaky (Stripe API outage), costs API calls, reveals test keys on CI if not handled carefully. But it's the most honest test of the actual integration.

**Option 3: Protected virtual factory method**

```csharp
// Production code — minimal change
public class PaymentService
{
    protected virtual PaymentIntentService CreateStripeService() => new();

    public async Task<PaymentIntent> CreateAsync(PaymentIntentCreateOptions options)
    {
        var stripeService = CreateStripeService(); // testable seam
        return await stripeService.CreateAsync(options);
    }
}

// Test subclass
public class TestablePaymentService : PaymentService
{
    private readonly PaymentIntentService _fake;
    public TestablePaymentService(PaymentIntentService fake) { _fake = fake; }
    protected override PaymentIntentService CreateStripeService() => _fake;
}
```

Trade-off: doesn't change the public API; testable via subclassing; slightly "abuses" inheritance. Acceptable when you can't introduce DI but need a seam.

**Option 4: Delegate factory injection**

```csharp
public class PaymentService
{
    private readonly Func<PaymentIntentService> _factory;

    public PaymentService(Func<PaymentIntentService> factory = null)
    {
        _factory = factory ?? (() => new PaymentIntentService());
    }
}

// Test
var fakeFactory = () => new FakePaymentIntentService();
var service = new PaymentService(fakeFactory);
```

Trade-off: awkward API, `FakePaymentIntentService` still needs to subclass the concrete SDK type. Works if the SDK class isn't `sealed`.

**Practical recommendation**

If you control the production code: always use Option 1 (wrapper interface). It's the cleanest and follows DI principles. If you truly cannot touch it and the SDK class isn't sealed: Option 3. If nothing else works: Option 2 (honest integration test with test keys and a CI environment variable).
</details>

---

**D19.** A colleague's PR has a `CartService_Tests.cs` failure-path test with no `Verify`. You run the tests — they pass. You delete `SetCartAsync` entirely — they still pass. Walk through why and what you'd say in code review.

<details><summary>Answer</summary>

The test calls `SetCartAsync` and asserts on the return value (e.g. `result.Should().BeNull()`). Without a `Verify`, there's no assertion that any mock method was or wasn't called. When `SetCartAsync` is deleted, the test still compiles if it was testing a different method, OR it was testing a null path that never hit the service method at all.

The test is a false safety net — it passes whether or not the production code does what it claims.

**In code review:** point out that without `Verify`, the test provides no protection against regressions in the Redis call. Suggest adding `_mockDb.Verify(x => x.StringSetAsync(...), Times.Never)` for the failure path, or `Times.Once` for the happy path.
</details>

<details><summary>Deep Dive</summary>

**Reconstructing the scenario**

```csharp
// Failure-path test — what happens when Redis returns false?
[Fact]
public async Task SetCart_WhenRedisReturnsFalse_ReturnsNull()
{
    _mockDb.Setup(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<TimeSpan?>(),
        It.IsAny<When>(),
        It.IsAny<CommandFlags>()))
        .ReturnsAsync(false);  // Redis "fails"

    var result = await _cartService.SetCartAsync(cart);

    result.Should().BeNull(); // passes
}
```

Delete `SetCartAsync`:
- If `_cartService.SetCartAsync` is gone, calling it → compile error → test can't run.
- BUT: if the test setup was wrong all along (e.g. testing the wrong interface overload — see D7), the Setup never fired, the mock returned the default (`false`), and `SetCartAsync` returned `null` without ever hitting the Setup. Deleting the Setup or the method changes nothing.

**The exact failure mode**

```csharp
// Production SetCartAsync never called _db.StringSetAsync (wrong overload, wrong mock)
// Returns null by default because the Redis call "failed" via default false
// Test was asserting the wrong path was exercised for the right reason
```

**What to say in code review**

```
This test doesn't verify that StringSetAsync was called at all — the null return 
could come from a completely different code path.

Without a Verify, deleting the Redis call from SetCartAsync wouldn't break 
this test. Add:

  _mockDb.Verify(x => x.StringSetAsync(...), Times.Once);

for the happy path, or

  _mockDb.Verify(x => x.StringSetAsync(...), Times.Never);

for tests where Redis shouldn't be called.

Also consider MockBehavior.Strict so any unexpected calls fail immediately 
rather than returning silent defaults.
```

**The bigger pattern: failure-path tests are the most important to get right**

Happy-path tests are usually easy to write correctly because the result is visible. Failure-path tests (what happens when Redis fails, what happens when the DB is down) are harder — there's often no visible output, only a side effect that should NOT have happened. `Verify(Times.Never)` is just as important as `Verify(Times.Once)`.
</details>

---

**D20.** You need to test that calling `CreateOrderAsync` twice with the same cart returns the existing order, not a duplicate. Your first approach: seed an order into EF InMemory, then call the service once. The test fails — a new order is created. Why does manual seeding fail? What's the correct approach?

<details><summary>Answer</summary>

The service likely checks for an existing order by querying the DB with the cart ID. The seeded order has the cart ID set, but it was inserted into a **different `DbContext` instance** than the one the service uses. Each test `DbContext` gets a fresh InMemory database — seeded data in one context isn't visible to a service using another context.

**Fix:**
- Seed using the same `DbContext` instance the service uses, or
- Use a shared `DbContextOptions` with a named InMemory database (`UseInMemoryDatabase("TestDb")`) so both contexts share the same in-memory store.
</details>

<details><summary>Deep Dive</summary>

**The context isolation bug**

```csharp
// BAD — two separate contexts, separate InMemory stores
var options1 = new DbContextOptionsBuilder<StoreContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString()) // new unique DB
    .Options;

using var seedCtx = new StoreContext(options1);
seedCtx.Orders.Add(new Order { CartId = "cart1", BuyerEmail = "alice@test.com" });
await seedCtx.SaveChangesAsync();
// Seeded into DB "abc123"

var options2 = new DbContextOptionsBuilder<StoreContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString()) // DIFFERENT new unique DB
    .Options;

var service = new OrderService(new StoreContext(options2)); // uses DB "def456"
// Service DB is empty — seed is in a completely different DB
var result = await service.CreateOrderAsync("cart1", dto); // creates new order
```

**Fix 1: Named shared InMemory database**

```csharp
var dbName = "SharedTestDb_" + Guid.NewGuid(); // unique per test run, shared within test
var options = new DbContextOptionsBuilder<StoreContext>()
    .UseInMemoryDatabase(dbName) // same name = same store
    .Options;

// Seed
using var seedCtx = new StoreContext(options);
seedCtx.Orders.Add(new Order { CartId = "cart1" });
await seedCtx.SaveChangesAsync();

// Service uses same options → same in-memory DB
var service = new OrderService(new StoreContext(options));
var result = await service.CreateOrderAsync("cart1", dto);
// Service finds the seeded order → returns existing → no duplicate
```

**Fix 2: Seed through the service itself**

More realistic — tests the full happy path first, then the idempotency path:

```csharp
var dbName = Guid.NewGuid().ToString();
var options = new DbContextOptionsBuilder<StoreContext>()
    .UseInMemoryDatabase(dbName).Options;

var service = new OrderService(new StoreContext(options));

// First call — creates the order
var first = await service.CreateOrderAsync("cart1", dto);
Assert.NotNull(first);

// Second call with same cart — should return existing order
var service2 = new OrderService(new StoreContext(options)); // fresh context, same DB
var second = await service2.CreateOrderAsync("cart1", dto);

// Assertions
Assert.Equal(first.Id, second.Id); // same order returned
Assert.Single(new StoreContext(options).Orders); // only one order in DB
```

This approach is better because it tests the actual deduplication logic end-to-end, not just whether seeding works.

**Why SQLite is better here too**

InMemory doesn't enforce unique constraints. If `CartId` has a unique constraint in your schema (preventing duplicates at the DB level), InMemory will silently allow two orders with the same `CartId`. SQLite in-process enforces the constraint, making the test more representative of production behavior.
</details>

---
