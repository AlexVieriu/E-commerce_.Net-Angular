## SECTION G — TEST QUALITY & MOQ OVERLOAD RESOLUTION

> **Source:** Task 19 — CartService mock overload fix, Verify patterns, coverage targets, `[ExcludeFromCodeCoverage]`.

---

### EASY

**G1.** [HIGH] [OUT-OF-SCOPE] What are the 3 properties of a good unit test?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**
1. **It can fail** — if you delete the production code it's testing, the test breaks. A test that still passes when the code is gone is untested wishful thinking.
2. **It fails for the right reason** — the assertion is the thing you care about, not a side effect. If the assertion is `result.Should().NotBeNull()` but the test was meant to verify a side-effect on Redis, the assertion is wrong.
3. **It tests behaviour, not implementation** — if you refactor the code without changing behaviour, the test shouldn't break. Tests coupled to implementation are maintenance burden.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"The test should pass." The point of a test is not to pass — it's to **fail reliably when the code is broken**.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that property #1 is best verified by **mutation testing** — tools like Stryker intentionally break the code and check if any test fails. Tests that don't detect the mutation are "dead weight."


</details>

---

**G2.** [HIGH] [OUT-OF-SCOPE] What does `mock.Verify(...)` do that `mock.Setup(...)` doesn't?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

- `Setup` — **conditional**: "if this method is called with these args, return this value." Silent if never called.
- `Verify` — **assertion**: "this method was called with these args exactly N times." Fails if not called.

Without `Verify`, a mock setup that never matches returns the default (`null` for objects, `false` for bool, `0` for int) and the test silently proves nothing.

```csharp
mock.Setup(x => x.Save(It.IsAny<Cart>())).ReturnsAsync(true);
// Code never calls Save → mock returns default (false)
// Test assertion on return value fails, but for the wrong reason

mock.Verify(x => x.Save(It.IsAny<Cart>()), Times.Once);
// If code never called Save → Verify throws → test fails with clear message
```


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Setup is for mocking, Verify is for verifying." Tautology. You need to show the silent-default failure mode.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `Mock.VerifyAll()` / strict mocks (`MockBehavior.Strict`) as alternatives — strict mocks fail if ANY unconfigured method is called. More rigorous but more fragile. Favour explicit `Verify` for clarity.


</details>

---

### MEDIUM

**G3.** [HIGH] Why does this test pass but prove nothing? Explain step by step.

```csharp
[Fact]
public async Task SetCartAsync_ShouldReturnNull_WhenSaveFails()
{
    mockDatabase.As<IDatabaseAsync>()
        .Setup(x => x.StringSetAsync(
            shoppingCart.Id,
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
        .ReturnsAsync(false);

    var result = await cartService.SetCartAsync(shoppingCart);

    result.Should().BeNull();
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Step by step:**

1. The `Setup` targets the **OLD** `StringSetAsync` overload: `(key, value, TimeSpan?, When, CommandFlags)`. Since StackExchange.Redis 2.6+, the production code calls the NEW overload: `(key, value, Expiration, ValueCondition, CommandFlags)`.

2. The types differ: `TimeSpan?` vs `Expiration`, `When` vs `ValueCondition`. Moq's matcher sees different signatures → **the setup never matches**.

3. When the production code calls the real overload, Moq has no matching setup → returns the **default value** (`false` for `bool`).

4. `SetCartAsync` sees `false` → returns null → test assertion `result.Should().BeNull()` passes.

5. The test passes for the WRONG reason: **not because the setup simulated a failed save, but because the unmatched setup returned default(false)**.

6. **Proof it's worthless:** delete the `Setup` call entirely. Test still passes. The mock's default behaviour alone produces the assertion.

7. **Proof it doesn't verify anything:** delete the entire `SetCartAsync` implementation body (replace with `return null;`). Test still passes.

**Fix — match the real overload AND add `Verify`:**

```csharp
mockDatabase.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<Expiration>(),
        It.IsAny<ValueCondition>(),
        It.IsAny<CommandFlags>()))
    .ReturnsAsync(false);

var result = await cartService.SetCartAsync(shoppingCart);

result.Should().BeNull();

mockDatabase.As<IDatabaseAsync>()
    .Verify(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<Expiration>(),
        It.IsAny<ValueCondition>(),
        It.IsAny<CommandFlags>()),
        Times.Once);
```


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Only spotting "no Verify." The overload mismatch is the root cause — Verify catches it but doesn't explain it.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that upgrading a mocked library (StackExchange.Redis 2.5 → 2.6) silently broke this test — a common hidden cost of dependency updates. This argues for **explicit contract tests** against real Redis (Testcontainers) for the hot paths, not just mocked unit tests.


</details>

---

**G4.** [MEDIUM] Why do you need `As<IDatabaseAsync>()` when mocking `StringSetAsync`?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

`StringSetAsync` is an **explicit interface implementation** on `StackExchange.Redis.IDatabase`. The method is declared on `IDatabaseAsync` (parent interface) and implemented explicitly:

```csharp
public interface IDatabaseAsync { Task<bool> StringSetAsync(...); }
public interface IDatabase : IDatabaseAsync { /* inherits */ }

// In Redis: explicit interface implementation
Task<bool> IDatabaseAsync.StringSetAsync(...) { ... }
```

When called through `IDatabase`, the compiler routes the call to the `IDatabaseAsync` vtable slot. Moq's setup on `Mock<IDatabase>` targets the `IDatabase` dispatch table, which doesn't have `StringSetAsync` as a member (it's inherited and explicitly implemented elsewhere).

`mockDatabase.As<IDatabaseAsync>()` tells Moq: "treat the mock as its `IDatabaseAsync` facet and set up on *that* dispatch table." Now the setup matches.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Because of inheritance." Inheritance alone doesn't cause this — most mocks work across interfaces. The explicit implementation is the specific cause.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that explicit interface implementations are a **.NET-specific subtlety** — Java doesn't have them. A common trap for Java devs coming to C#. Mentioning this shows deep .NET idioms.


</details>

---

**G5.** [MEDIUM] `It.IsAny<RedisKey>()` vs `It.Is<RedisKey>(k => k == "cart-1")` — when to use which?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

- `It.IsAny<T>()` — "any value of type T." Use when the specific value doesn't matter to the test.
- `It.Is<T>(predicate)` — "a value matching this predicate." Use when the test is specifically about the argument.

**Rule of thumb:**
- Setup for the HAPPY path (return a value): use `It.IsAny` — don't constrain more than needed.
- Verify to assert the call site: use `It.Is` with a specific value to prove "the right key was passed."

Example:
```csharp
// Setup: don't care, any call returns the value
mock.Setup(x => x.Get(It.IsAny<string>())).ReturnsAsync(cart);

// Verify: proves the code called Get with the RIGHT key
mock.Verify(x => x.Get(It.Is<string>(k => k == "cart-1")), Times.Once);
```

**Over-specifying setups** (using `It.Is` everywhere) makes tests brittle — any refactor that adds a new argument transformation breaks unrelated tests.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Always use `It.Is` for safety." Over-specified setups fail when the code changes in safe ways. You want just enough specificity.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `Moq.Protected` for protected members, and `It.IsAny` combined with captured arguments via `Callback<...>(...)` for advanced assertion — the interviewer sees you know Moq beyond the surface API.


</details>

---

**G6.** [MEDIUM] [OUT-OF-SCOPE] Your coverage target for Core — what % and why? What do you explicitly NOT test?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Target: 90% on Core and Application layers.**

Reason: these are pure C# with no external dependencies. No excuse not to test. Missing coverage here is usually untested domain rules, not infrastructure gaps.

**Infrastructure: ~85% on services (CartService ~100%, OrderService ~80%).** The raw "Infrastructure" number can look lower (2.6% in our report) because it includes factories, seed scripts, and EF config classes — all non-logic. The meaningful services hit 80-100%.

**Controllers: 0% via unit tests, covered by Playwright E2E.** Controllers are HTTP plumbing + DI orchestration; unit-testing them produces brittle tests that just re-assert middleware behaviour. E2E proves the whole pipeline works.

**What NOT to test:**

| Category | Why skip |
|---|---|
| EF configuration classes (`builder.Property(...)`) | No logic, just declarations |
| Factory / bootstrapping classes | Tested by the app starting up |
| Seeding scripts | Data loading, not business logic |
| `PaymentService` / `CouponService` | No DI seam (calls `new PaymentIntentService()` directly) — would need real Stripe calls |
| Auto-properties, records, DTOs | Compiler-generated; nothing to break |


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"100% coverage." Coverage is a proxy, not a goal. 100% via trivial tests is worse than 85% via meaningful tests.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Quoting a concrete benchmark — "WorldPay targets 90% because they process financial transactions with regulatory audits; for a CV project, 85% Core + Application is honest and defensible." Shows you know coverage targets vary by context.


</details>

---

**G7.** [MEDIUM] When is `[ExcludeFromCodeCoverage]` legitimate vs lazy?

<details>
<summary>The 5% Answer</summary>

- **Legitimate** when:
  - The class has no injectable seam for external dependencies (e.g. calls `new PaymentIntentService()` directly)
  - Testing would require real external calls (Stripe sandbox, real SMTP)
  - You have **documented the reason and the fix** in a comment

```csharp
// PaymentService calls Stripe SDK via `new PaymentIntentService()` directly — no DI seam to mock.
// Fix: inject IStripeClient abstraction. Deferred to Sprint 5.
[ExcludeFromCodeCoverage]
public class PaymentService : IPaymentService { ... }
```

- The comment is **mandatory** — without it, the exclude is indistinguishable from hiding laziness
- **Also legitimate:** auto-generated code (Migrations, gRPC stubs, EF scaffolding) where you don't own the logic
- **Lazy** — hiding uncovered logic without explanation, or skipping tests that *could* be written with minor refactoring. Red flag in code review.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Using `[ExcludeFromCodeCoverage]` on any class you wrote yourself without a comment. Interviewer should ask "why can't this be tested?"


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the right architectural fix for `PaymentService` is to introduce `IStripeClient` (an abstraction over the Stripe SDK), inject it, and mock it in tests. Documenting this as deferred tech debt is senior-level — it names the debt and when to pay it.


</details>

---

### HARD

**G8.** [HIGH] This test claims to verify the idempotency behaviour. Does it actually prove idempotency? What should it assert?

```csharp
[Fact]
public async Task CreateOrderAsync_TwiceWithSameIntent_ReturnsSuccess()
{
    await service.CreateOrderAsync("buyer@test.com", MakeOrderDto(cartId: "cart-1"));
    var result = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto(cartId: "cart-1"));

    result.Should().NotBeNull();
}
```

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**What this test actually proves:** "the second call doesn't crash." Nothing about idempotency.

**What it must prove:**
1. Both calls return the **same order ID** (deduplicated).
2. Stock is decremented **exactly once**, not twice.
3. No second row in `Orders` table for the same PaymentIntentId.

**Correct test:**
```csharp
[Fact]
public async Task CreateOrderAsync_TwiceWithSameIntent_ReturnsExistingOrderAndDoesNotDoubleDecrement()
{
    // Arrange
    var product = new Product { Id = 1, QuantityInStock = 10, Price = 50m };
    await seedProduct(product);

    // Act
    var first = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto(cartId: "cart-1", productId: 1, quantity: 2));
    var second = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto(cartId: "cart-1", productId: 1, quantity: 2));

    // Assert
    second!.Id.Should().Be(first!.Id);                    // same order, not a duplicate
    
    var orderCount = await _context.Orders.CountAsync(o => o.PaymentIntentId == "pi_abc");
    orderCount.Should().Be(1);                            // exactly one row in DB

    var productFresh = await _context.Products.FindAsync(1);
    productFresh!.QuantityInStock.Should().Be(8);         // 10 - 2, not 10 - 4
}
```

**Why these three assertions are the minimum:**
- Same order ID proves the app-level dedup check works.
- Row count proves no duplicate insert happened.
- Stock count proves the decrement logic was NOT re-executed (the whole point of idempotency).


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Adding only the `second.Id.Should().Be(first.Id)` assertion. It misses the stock double-decrement bug which is the real production risk.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that this is a **behavioural test, not an implementation test** — it doesn't care HOW idempotency is achieved (app check, unique index, both), only that the observable outcome is correct. Refactoring the implementation shouldn't break it.


</details>

---

**G9.** [MEDIUM] `Order.GetTotal()` returns -5 when discount > subtotal + shipping. Is that a bug? How do you handle it in tests?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**You don't know yet** — it's **undocumented behaviour**. Three possible truths:
- Bug: someone forgot a `Math.Max(0, ...)` floor.
- Feature: finance team wants to see negative totals as a signal of "discount exceeded cart value, issue credit."
- Unimportant: coupon logic prevents discount > subtotal elsewhere, so this branch is unreachable.

**Correct response — write a test that documents the current behaviour:**

```csharp
[Fact]
public void GetTotal_WhenDiscountExceedsSubtotal_TotalIsNegative()
{
    // Documents current behaviour: no floor at zero.
    // If a floor is added later, change Should().Be(-5m) to Should().Be(0m).
    // The test will fail and remind you to update this comment.
    var order = MakeOrder(subtotal: 50m, discount: 60m, shipping: 5m);

    order.GetTotal().Should().Be(-5m);
}
```

**What this test does:**
1. Locks in the current output (regression alarm).
2. Documents it's intentional-by-capture, not intentional-by-design.
3. Forces a conscious decision if anyone changes the logic.

**Anti-pattern to avoid:**
```csharp
// WRONG — silently "fixes" without asking anyone
Assert.True(order.GetTotal() >= 0);
```

This is "drive-by fixing": the test asserts behaviour that might not match the actual code. Either the test fails now (and you've wasted time) or someone later writes `Math.Max(0, ...)` to make the test pass without a design discussion.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Add `Math.Max(0, ...)` — negatives are wrong." Confident wrong-fixing. Without domain context, you can't call this a bug.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Proposing a follow-up conversation with product — "I've locked in the current behaviour. Here are the three interpretations. Which should we make explicit?" Shows you know code changes need alignment, not just commits.


</details>

---

**G10.** [HIGH] Design a complete test strategy for `CreateOrderAsync` covering all four failure modes: (1) happy path, (2) stock concurrency conflict, (3) price change, (4) double-submit idempotency. For each scenario: name the EF provider used, what you mock, and why those choices are correct.

<details>
<summary>The 5% Answer</summary>

| Scenario | EF Provider | Test doubles | Why |
|---|---|---|---|
| Happy path | InMemory | Mock `ICartService`, Mock `IUnitOfWork` | No constraints needed; tests order build + stock decrement logic |
| Concurrency conflict | InMemory | Two real `DbContext` instances, shared DB | InMemory enforces `[ConcurrencyCheck]` since EF Core 5 |
| Price change | InMemory | Mock `ICartService` returning old price; real product in DB | Tests comparison logic, not infrastructure |
| Double-submit idempotency | **SQLite in-memory** | None | Unique index on `PaymentIntentId` requires a real SQL engine — InMemory ignores unique indexes |

```csharp
// Scenario 1 — Happy path
[Fact]
public async Task CreateOrderAsync_HappyPath_DecrementsStockAndReturnsOrder()
{
    // seed product stock=5, cart with matching price + pi_abc
    // act: service.CreateOrderAsync(email, dto)
    // assert: order returned, product.QuantityInStock==4, cart deleted
}

// Scenario 2 — Concurrency conflict (InMemory, two contexts)
[Fact]
public async Task CreateOrderAsync_StockChangedConcurrently_ThrowsInvalidOperation()
{
    // ctx1.FindAsync, ctx2.FindAsync both read stock=1
    // ctx1.SaveChangesAsync() → succeeds
    // ctx2.SaveChangesAsync() → DbUpdateConcurrencyException → caught → InvalidOperationException
}

// Scenario 3 — Price change
[Fact]
public async Task CreateOrderAsync_PriceChanged_ThrowsAndUpdatesCartInRedis()
{
    // cart has price=50, DB product has price=75
    // act
    // assert: PriceChangedException thrown, cartService.SetCartAsync called with price=75
    // assert: cart.PaymentIntentId == null
}

// Scenario 4 — Idempotency (SQLite required)
[Fact]
public async Task CreateOrderAsync_CalledTwice_ReturnsSameOrder_StockDecrementedOnce()
{
    // SQLite in-memory, unique index enforced
    // call twice with same cart/PaymentIntentId
    // assert: second.Id == first.Id
    // assert: Orders.Count(pi_abc) == 1
    // assert: product.QuantityInStock == initial - qty (not initial - 2*qty)
}
```

**Why InMemory fails scenario 4:** InMemory skips SQL constraint enforcement. The test would pass even with no duplicate-prevention logic — giving false confidence. The unique index is the safety net; you must test it with a provider that enforces it.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Using InMemory for all four scenarios. Three pass correctly, but scenario 4 (idempotency) gives a false green — InMemory doesn't enforce unique indexes, so the test passes even when the DB would reject the duplicate in production.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Calling this a **test-type matrix** — each scenario selects the minimum infrastructure to prove its invariant. Using SQLite everywhere adds schema-setup overhead; using InMemory everywhere misses SQL constraints. The principle: use the cheapest provider that can actually fail the test when the code is wrong. Knowing this distinction is the difference between 85% and 95% test confidence.

</details>

---

**G11.** [MEDIUM] You need to write a test for `SetCartAsync` success path. Show the full test including setup, act, and all verifications a senior would include.

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

```csharp
[Fact]
public async Task SetCartAsync_WhenRedisAccepts_ReturnsCartAndPersistsCorrectData()
{
    // Arrange
    var cart = new ShoppingCart
    {
        Id = "cart-1",
        Items = new List<CartItem>
        {
            new() { ProductId = 1, Quantity = 2, Price = 50m }
        }
    };

    var serialized = JsonSerializer.Serialize(cart, JsonOpts);
    RedisValue? capturedValue = null;

    mockDatabase.As<IDatabaseAsync>()
        .Setup(x => x.StringSetAsync(
            It.Is<RedisKey>(k => k == cart.Id),
            It.IsAny<RedisValue>(),
            It.Is<Expiration>(e => e.ToTimeSpan() == TimeSpan.FromDays(30)),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()))
        .Callback<RedisKey, RedisValue, Expiration, ValueCondition, CommandFlags>(
            (_, v, _, _, _) => capturedValue = v)
        .ReturnsAsync(true);

    // Act
    var result = await cartService.SetCartAsync(cart);

    // Assert
    result.Should().NotBeNull();
    result!.Id.Should().Be(cart.Id);
    result.Items.Should().HaveCount(1);

    // Verify Redis was actually called with the right key and TTL
    mockDatabase.As<IDatabaseAsync>()
        .Verify(x => x.StringSetAsync(
            It.Is<RedisKey>(k => k == cart.Id),
            It.IsAny<RedisValue>(),
            It.Is<Expiration>(e => e.ToTimeSpan() == TimeSpan.FromDays(30)),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()),
            Times.Once);

    // Verify the serialised payload matches the cart (captured via Callback)
    capturedValue.HasValue.Should().BeTrue();
    var deserialized = JsonSerializer.Deserialize<ShoppingCart>(capturedValue!.ToString(), JsonOpts);
    deserialized!.Id.Should().Be(cart.Id);
    deserialized.Items[0].Price.Should().Be(50m);
}
```

**Why each piece matters:**
- `It.Is<RedisKey>(k => k == cart.Id)` — proves the RIGHT key was used.
- `It.Is<Expiration>(...)` — proves the TTL is exactly 30 days (a silent TTL change would be a disaster; this catches it).
- `Callback(...)` + `capturedValue` — captures the serialised payload for content assertion.
- `Verify(..., Times.Once)` — proves Redis was actually called (not skipped).
- Deserialising `capturedValue` — proves round-trip fidelity of the serialiser, catching JSON config regressions.


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Only asserting `result.Should().NotBeNull()`. Proves nothing about Redis; the mock returns default `true` anyway.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the TTL assertion caught a real bug in this project — the TTL was accidentally set to 30 *minutes* instead of 30 *days* during a refactor, and without the explicit assertion, tests would have passed. Concrete past-bug callbacks build credibility.


</details>

---

**G12.** [MEDIUM] Your `CartService` is 100% mocked against `IDatabase`. When is that insufficient, and how would you add Testcontainers-backed tests without doubling your CI time?

<details>
<summary>The 5% Answer</summary>

**The 5% Answer:**

**Why 100% mocked is insufficient:**
1. **Mocks encode what YOU think Redis does, not what Redis actually does.** The G3 bug proved this — the test passed for 18 months while the production call silently returned default(false).
2. **Serializer round-trips are untested.** `JsonSerializer.Serialize` → `RedisValue` → `JsonSerializer.Deserialize` has real edge cases (enum names vs numbers, DateTime kind, decimal precision) that mocks skip.
3. **TTL behaviour is untested.** A mock doesn't expire the key at T+30 days. Only a real Redis does.
4. **Connection-level concerns** (timeouts, multiplexer failover, pipelining) are invisible until production.

**Testcontainers pattern — one fixture, shared across tests:**

```csharp
public class RedisFixture : IAsyncLifetime
{
    public RedisContainer Container { get; } = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public IConnectionMultiplexer Multiplexer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
        Multiplexer = await ConnectionMultiplexer.ConnectAsync(Container.GetConnectionString());
    }

    public async Task DisposeAsync() => await Container.DisposeAsync();
}

[Collection("Redis")]
public class CartServiceIntegrationTests
{
    private readonly CartService _service;
    public CartServiceIntegrationTests(RedisFixture fx)
        => _service = new CartService(fx.Multiplexer);

    [Fact]
    public async Task SetThenGet_RoundTrips_WithCorrectTTL()
    {
        var cart = new ShoppingCart { Id = "cart-int-1", Items = [...] };
        await _service.SetCartAsync(cart);

        var fetched = await _service.GetCartAsync("cart-int-1");
        fetched!.Items.Should().HaveCount(cart.Items.Count);

        var ttl = await fx.Multiplexer.GetDatabase().KeyTimeToLiveAsync("cart-int-1");
        ttl.Should().BeCloseTo(TimeSpan.FromDays(30), TimeSpan.FromSeconds(5));
    }
}
```

**Avoiding CI-time doubling:**
1. **Split unit vs integration test projects** — `CartService.UnitTests` runs on every commit; `CartService.IntegrationTests` runs on PR merge and nightly.
2. **One container per collection, not per test** — `[Collection("Redis")]` reuses the same container across all tests in the collection. Startup cost is paid once.
3. **Use `redis:7-alpine`** — 40MB image, starts in < 2s.
4. **Parallelise by isolating keys per test** — use `Guid.NewGuid()` prefixes so tests don't step on each other; same container serves them all.

**Decision rule for this project:**
- Mock for branching/returns-shape logic (90% of unit tests).
- Testcontainer for serialization, TTL, and concurrent-write behaviour (the hot-path invariants).

**Production consequence:** without integration coverage on the hot path, a silent library upgrade (the G3 overload change) or a serializer setting tweak (enum names → numbers) ships uncaught. Integration tests catch "my code compiles but Redis didn't understand it."


</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Just add more mocks." Doubling down on the tool that failed you. More mocks ≠ more confidence when the bug was mock-vs-real divergence.


</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **contract tests** (Pact-style) between your service and Redis — where the test asserts a specific Redis key shape and value — are the middle ground between pure mocks and full integration. The contract file becomes the shared truth; unit tests verify your code against the contract, integration tests verify the contract against real Redis.


</details>

---

### ⚡ Syntax Quick-Fire

**GQ1.** [HIGH] xUnit: `[Fact]` vs `[Theory]` with `[InlineData]` — one sentence each, and when do you reach for `[MemberData]`?

<details>
<summary>The 5% Answer</summary>

- **`[Fact]`**: single test, no parameters. "This specific scenario should hold."
- **`[Theory]` + `[InlineData(...)]`**: parameterised test — xUnit runs it once per `[InlineData]` row. Same logic, different inputs.
- **`[MemberData]`**: supplies test data from a static property or method — use when the data is too complex for `[InlineData]` (objects, large datasets, computed values).

```csharp
[Theory]
[InlineData(1, 1, 2)]
[InlineData(0, 5, 5)]
[InlineData(-1, 1, 0)]
public void Add_ShouldReturnSum(int a, int b, int expected)
    => Assert.Equal(expected, a + b);

// MemberData for complex objects:
public static IEnumerable<object[]> OrderData =>
    new[] { new object[] { new CreateOrderDto { ... }, OrderStatus.Pending } };

[Theory, MemberData(nameof(OrderData))]
public async Task CreateOrder_ShouldReturnPendingStatus(CreateOrderDto dto, OrderStatus expected) { ... }
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Use `[Theory]` for all tests." `[Fact]` is clearer for single-scenario tests — `[Theory]` implies data-driven.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** `[ClassData]` for theory data that lives in a separate class — keeps test files clean when the data set is large. Also mentioning that xUnit runs each `[InlineData]` as a separate test entry in Test Explorer, making failures easy to identify by row.

</details>

---

**GQ2.** [HIGH] Moq: `MockBehavior.Strict` vs `MockBehavior.Loose` (default) — what happens on an unconfigured call?

<details>
<summary>The 5% Answer</summary>

- **`Loose`** (default): unconfigured calls return `default(T)` — `null` for reference types, `false` for bool, `0` for int. Silent.
- **`Strict`**: unconfigured calls throw `MockException` immediately. Every invocation must have a matching `Setup`.
- **Recommendation**: prefer explicit `Verify` over `Strict`. Strict tests break whenever internal implementation calls change, even if behaviour doesn't — they're coupled to implementation, not contract.

```csharp
var mock = new Mock<ICartService>(MockBehavior.Strict);
mock.Setup(x => x.GetCartAsync(It.IsAny<string>())).ReturnsAsync(cart);
// Any other method call throws MockException
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Strict is always better." It makes tests brittle — any refactoring that adds a new internal call breaks the test even if behaviour is correct.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Using `MockBehavior.Strict` only on **boundary mocks** (external services, third-party SDKs) where unexpected calls indicate a real bug. For internal collaborators, `Loose` + explicit `Verify` is cleaner.

</details>

---

**GQ3.** [HIGH] FluentAssertions: `.Should().Throw<T>()` vs `await act.Should().ThrowAsync<T>()` — which for async methods?

<details>
<summary>The 5% Answer</summary>

- **`ThrowAsync<T>()`** for any `async Task` method. It awaits the task and checks the exception.
- **`Throw<T>()`** is synchronous — it invokes the delegate synchronously. For async methods, the exception is swallowed inside the `Task` and never rethrown synchronously → **test always passes even when the code throws**.

```csharp
// WRONG for async — exception is swallowed
Action act = () => service.CreateOrderAsync(dto);  // doesn't await
act.Should().Throw<InvalidOperationException>();    // always passes!

// CORRECT
Func<Task> act = async () => await service.CreateOrderAsync(dto);
await act.Should().ThrowAsync<InvalidOperationException>()
    .WithMessage("*concurrent*");
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Using `Should().Throw<T>()` on async code. The test will always green — it never sees the exception.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Noting that `.WithMessage("*concurrent*")` uses glob-style matching — the `*` wildcard means "contains". More robust than exact string match for messages that may include variable data.

</details>

---
