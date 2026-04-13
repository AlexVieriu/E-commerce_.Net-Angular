## SECTION D — TESTING (Unit Tests, Moq, Test Quality)

### EASY

**D1.** What is the difference between a unit test and an integration test? Which is faster? Which gives more confidence?

<details><summary>Answer</summary>

- **Unit test** — tests one class/method in isolation; all dependencies are mocked. Fast (milliseconds), deterministic, no I/O.
- **Integration test** — tests multiple real components together (e.g. service + EF Core + SQLite). Slower, but proves real components work together.

**Speed**: unit tests win. **Confidence**: integration tests win — mocks can lie, a real DB can't. Use both: unit tests for logic coverage, integration tests for data access and wiring.
</details>

---

**D2.** What is the AAA pattern (Arrange, Act, Assert)?

<details><summary>Answer</summary>

- **Arrange** — set up the system under test, dependencies, and input data.
- **Act** — call the single method/operation being tested.
- **Assert** — verify the outcome matches expectations.

One Act per test. If you have two Act sections, you have two tests. The pattern forces tests to be focused and readable.
</details>

---

**D3.** What does `Mock<T>` do in Moq? What is the difference between `Setup` and `Verify`?

<details><summary>Answer</summary>

- **`Mock<T>`** — creates a fake implementation of interface `T` at runtime. By default all methods return default values (`null`, `0`, `false`). You control behavior via `Setup`.
- **`Setup`** — configures what the mock returns or does when a specific method is called. Runs during the Act.
- **`Verify`** — asserts after the Act that a specific method was (or was not) called with specific arguments. Without `Verify`, you have no proof the method was called.

`Setup` controls behavior. `Verify` proves the interaction happened.
</details>

---

**D4.** What three properties make a test valuable vs noise?

<details><summary>Answer</summary>

- **Fast** — runs in milliseconds; slow tests get skipped.
- **Deterministic** — same result every run, regardless of time, order, or environment.
- **Meaningful** — fails when real bugs exist and passes only when the code is correct. A test that never fails (or fails randomly) is noise.

A bonus fourth: **readable** — the intent is clear without comments; when it fails, you know exactly what broke.
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

---

**D9.** What is the difference between `mockDb.Setup(...)` and `mockDb.As<IDatabaseAsync>().Setup(...)`? When do you need the `As<>` cast? What happens if you use the wrong one?

<details><summary>Answer</summary>

`mockDb.Setup(...)` sets up methods on the concrete mock type `T`. `mockDb.As<IDatabaseAsync>().Setup(...)` sets up methods on an interface that `T` implements — necessary when the production code holds a reference of type `IDatabaseAsync`, not the concrete type.

If you use the wrong one: the production code calls through the `IDatabaseAsync` interface reference, Moq finds no matching setup on that interface, falls back to default values, and your setup silently never fires. The test passes even though the expected call never happened.
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

---

**D11.** What is mutation testing? How does it differ from code coverage? If you have 100% code coverage, does that mean your tests are good? Give an example where 100% coverage still misses a bug.

<details><summary>Answer</summary>

- **Code coverage** — measures which lines/branches were *executed* by tests. Does not measure whether assertions would catch a bug.
- **Mutation testing** — automatically introduces small bugs (mutations) into your code and checks whether your test suite catches them. If a test still passes after a mutation, the test is "weak."

100% coverage does NOT mean tests are good. A test with `Assert.NotNull(result)` covers every line of a method but wouldn't catch a wrong calculation, missing `Verify`, or off-by-one error.
</details>

---

**D12.** You have a Moq `Setup(...).ReturnsAsync(false)` but no `Verify`. You delete the production method. The test still passes. Why?

<details><summary>Answer</summary>

`Setup` only configures what the mock *returns if called* — it's not an assertion. If the production method is deleted, the mock method is never called, the `Setup` never fires, and no error occurs. Without a `Verify`, there's nothing to fail.

The test was testing the wrong thing: it was asserting the output of a method that may or may not have used the mock, not that the mock was actually called.

**Fix:** add `mockService.Verify(x => x.TheMethod(...), Times.Once)` after the Act.
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

---

**D14.** What coverage target do you set for each layer (Core, Application, Infrastructure, API controllers) and why?

<details><summary>Answer</summary>

- **Core (entities, value objects, domain logic)** — 90–100%. Pure C#, no I/O, easy to test. This is where your most valuable logic lives.
- **Application (services, use cases)** — 80–90%. Complex orchestration; mock dependencies. Cover all branches and error paths.
- **Infrastructure (EF repositories, Redis, Stripe)** — 40–60%. Integration tests replace unit tests here; InMemory/SQLite tests for EF, skip Redis/Stripe unless critical.
- **API controllers** — 30–50%. Thin mapping layer; if service layer is covered, controllers add little value. Test only routing, model validation, and status codes.

Never chase a single global coverage number — it averages out the meaningful (Core) with the meaningless (migrations, DTOs).
</details>

---

### HARD

**D15.** Design a test for optimistic concurrency. You need to prove that two simultaneous orders for the last item result in exactly one success and one failure. You cannot use `Thread.Sleep`. How do you simulate concurrency in a unit test? What database provider do you use? Why can't you use InMemory?

<details><summary>Answer</summary>

Use **SQLite in-process** (not InMemory — it doesn't enforce concurrency tokens). Simulate concurrency with two separate `DbContext` instances reading the same product, then saving in sequence — the second save should throw `DbUpdateConcurrencyException`.

You don't need real threading. The race condition is captured by the optimistic lock: both contexts read stock=1, first saves stock=0, second tries to save stock=0 with original=1 but the DB already shows 0 → mismatch → exception.
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

---

**D17.** Your test suite has 128 tests. 126 pass, 2 fail intermittently (sometimes pass, sometimes fail on CI). These are called "flaky tests." Name five common causes of flaky tests and how to fix each.

<details><summary>Answer</summary>

1. **Shared mutable state** — tests pollute a static field or singleton. Fix: reset state in `[SetUp]`/`Dispose`, use fresh instances per test.
2. **Time-dependent assertions** — `DateTime.Now` changes between runs or across timezones. Fix: inject `TimeProvider` and freeze time in tests.
3. **Race conditions in async tests** — `Task.Run` + no proper await. Fix: always `await` in tests; use `Task.WhenAll` for true parallel.
4. **Test ordering dependency** — test B relies on state created by test A. Fix: each test must arrange its own state independently.
5. **External resource unavailability** — CI has no network, no Redis, no real DB. Fix: mock external calls or use containers (`Testcontainers`).
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

---

**D19.** A colleague's PR has a `CartService_Tests.cs` failure-path test with no `Verify`. You run the tests — they pass. You delete `SetCartAsync` entirely — they still pass. Walk through why and what you'd say in code review.

<details><summary>Answer</summary>

The test calls `SetCartAsync` and asserts on the return value (e.g. `result.Should().BeNull()`). Without a `Verify`, there's no assertion that any mock method was or wasn't called. When `SetCartAsync` is deleted, the test still compiles if it was testing a different method, OR it was testing a null path that never hit the service method at all.

The test is a false safety net — it passes whether or not the production code does what it claims.

**In code review:** point out that without `Verify`, the test provides no protection against regressions in the Redis call. Suggest adding `_mockDb.Verify(x => x.StringSetAsync(...), Times.Never)` for the failure path, or `Times.Once` for the happy path.
</details>

---

**D20.** You need to test that calling `CreateOrderAsync` twice with the same cart returns the existing order, not a duplicate. Your first approach: seed an order into EF InMemory, then call the service once. The test fails — a new order is created. Why does manual seeding fail? What's the correct approach?

<details><summary>Answer</summary>

The service likely checks for an existing order by querying the DB with the cart ID. The seeded order has the cart ID set, but it was inserted into a **different `DbContext` instance** than the one the service uses. Each test `DbContext` gets a fresh InMemory database — seeded data in one context isn't visible to a service using another context.

**Fix:**
- Seed using the same `DbContext` instance the service uses, or
- Use a shared `DbContextOptions` with a named InMemory database (`UseInMemoryDatabase("TestDb")`) so both contexts share the same in-memory store.
</details>

---
