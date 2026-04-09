# Task 19 — Unit Test Quality & Coverage

## Why this approach

Task 19 wasn't just "write more tests." It was a quality audit — find tests that pass but prove nothing, identify uncovered logic that actually matters, and make deliberate decisions about what NOT to test. That's what separates a junior who hits a coverage number from a mid-level who can defend every line.

---

## Concept 1 — Test Quality: Noise vs Value

### What confused me

Not all passing tests are useful. We found tests that:
- Assert a string equals itself (`Pending.Should().Be(Pending)`)
- Test the compiler, not your code (`product.Should().BeAssignableTo<BaseEntity>()`)
- Pass accidentally because a mock setup was never matched

### Explanation

A good test has three properties:
1. **It can fail** — if you delete the production code, the test breaks
2. **It fails for the right reason** — the assertion is the thing you care about, not a side effect
3. **It tests behavior, not implementation** — it doesn't care HOW the code works, only WHAT it does

The worst category is a test that passes when it shouldn't. Example from `CartService_Tests`:

```csharp
// WRONG — uses old StringSetAsync overload
mockDatabase.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(
        shoppingCart.Id,
        It.IsAny<RedisValue>(),
        It.IsAny<TimeSpan?>(),   // ← old signature
        It.IsAny<When>(),
        It.IsAny<CommandFlags>()))
    .ReturnsAsync(false);
```

Moq silently ignores setups that don't match. The mock returns `false` by default. The test passes — but it's not testing anything. Delete `SetCartAsync` entirely and the test still passes.

**Fix:** match the exact overload the production code calls, then add `Verify`:

```csharp
// CORRECT — matches Expiration/ValueCondition overload (StackExchange.Redis 2.6+)
mockDatabase.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<Expiration>(),       // not TimeSpan?
        It.IsAny<ValueCondition>(),   // not When
        It.IsAny<CommandFlags>()))
    .ReturnsAsync(false);

// Verify it was actually called — catches silent failures
mockDatabase.As<IDatabaseAsync>()
    .Verify(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<Expiration>(),
        It.IsAny<ValueCondition>(),
        It.IsAny<CommandFlags>()),
        Times.Once);
```

**Why `As<IDatabaseAsync>()`?** `StringSetAsync` is an explicit interface implementation — only accessible through `IDatabaseAsync`, not through `IDatabase`. Moq's `Setup` on the concrete type can't reach it.

---

## Concept 2 — Coverage Targets: What % and Why

### Explanation

We ran coverage after Task 19:

| Layer | Coverage | Reason |
|---|---|---|
| Core | **90%** | Pure logic, no dependencies — no excuse not to test |
| Application | **89.3%** | Specs and mappings are pure C# — easy to test |
| Infrastructure (services) | **~85%** | CartService 100%, OrderService 80% |
| API controllers | 0% | Covered by Playwright E2E (Task 37) |

**Infrastructure overall showed 2.6%** — misleading. That number includes factories, seed scripts, and EF config classes that have no testable logic. The meaningful services are at 80-100%.

**The 90% rule:** WorldPay targets 90% because they process financial transactions with regulatory audits. For a CV project, 85% on Core + Application is honest and defensible. The difference between 85% and 90% is chasing noise tests that add zero value.

### What NOT to test

| Category | Why skip |
|---|---|
| EF configuration classes | No logic — just `builder.Property(...)` calls |
| Factory/bootstrapping classes | Run once at startup, tested by the app working |
| Seeding scripts | Data setup, not business logic |
| `PaymentService` / `CouponService` | No DI seam — call `new PaymentIntentService()` directly |
| Controllers | Test via integration/E2E, not unit tests |

### `[ExcludeFromCodeCoverage]` — When to use it

```csharp
// PaymentService calls Stripe SDK via `new` — no DI seam to mock.
// Fix: inject IStripeClient abstraction. Deferred.
[ExcludeFromCodeCoverage]
public class PaymentService(ICartService cartService, IUnitOfWork unitOfWork) : IPaymentService
```

Use it when:
- The class has **no injectable seam** for external dependencies
- Testing would require real HTTP calls (Stripe test-mode sandbox)
- You've documented WHY and what the architectural fix would be

Don't use it to hide laziness. The comment explaining the tradeoff is mandatory.

---

## Concept 3 — Documenting Known Behavior (Edge Cases)

### What confused me

`Order.GetTotal()` returns a **negative number** when discount > subtotal + shipping. Is that a bug or a feature?

```csharp
public decimal GetTotal() => Subtotal - Discount + DeliveryMethod.Price;
// 50 - 60 + 5 = -5  ← no floor at zero
```

### Explanation

When you don't know if it's intentional, **write a test that documents the current behavior**. Add a comment explaining it's documentation, not validation:

```csharp
[Fact]
public void GetTotal_WhenDiscountExceedsSubtotal_TotalIsNegative()
{
    // Documents current behaviour: no floor at zero.
    // If a floor is added later, change Should().Be(-5m) to Should().Be(0m).
    // The test will fail and remind you to update the comment.
    var order = MakeOrder(subtotal: 50m, discount: 60m, shipping: 5m);

    order.GetTotal().Should().Be(-5m);
}
```

This test has two jobs:
1. Documents what the code currently does
2. Acts as a regression alarm if someone adds a floor — the test breaks and forces a conscious decision

---

## Concept 4 — Idempotency Testing Pattern

### Explanation

Task 18 added a unique constraint on `PaymentIntentId` — calling `CreateOrderAsync` twice with the same cart should return the existing order, not create a duplicate.

The naive test approach seeds an order manually then calls the service once. This fails because EF InMemory handles navigation properties differently than SQL Server when seeding.

**The correct approach:** call the real flow twice and assert on observable outcomes:

```csharp
// First call — creates the order, decrements stock
var first = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto());

// Second call — same PaymentIntentId, should return existing
var second = await service.CreateOrderAsync("buyer@test.com", MakeOrderDto());

second!.Id.Should().Be(first!.Id);         // same order, no duplicate

// Stock decremented once only
product!.QuantityInStock.Should().Be(8);   // 10 - 2, not 10 - 4
```

This tests the user-visible behavior: place the same order twice, get one result, stock not double-charged.

---

## Concept 5 — GitGuardian & Test Credentials

### What happened

GitGuardian flagged `"Pa$$w0rd12"` in `Validation_Tests.cs` as a "Generic Password" — 3 hits on the original commit, growing with each fix attempt.

### Explanation

GitGuardian scans **every commit** in a PR diff, not just the latest. Adding commits to fix it adds more hits. The correct responses:

1. **Suppress with pragma** (prevents future detections):
```csharp
private const string TestPassword = "Pa$$w0rd12"; // pragma: allowlist secret
```
The comment must be on the **same line** as the literal, not above it.

2. **Use GitGuardian's "Skip: test cred" button** (dismisses existing findings):
Goes to the failing check → Details → click the button. Marks all findings as test credentials. Immediate green.

Don't keep committing fix attempts — each new commit adds another scan hit.

---

## Exercise

1. Write a `SetCartAsync` test for the failure path that uses the **correct** `Expiration/ValueCondition` overload and includes a `Verify` call. Run it. It should pass.

2. Change the setup to use `TimeSpan?/When` (the old overload). Run it again. It still passes — demonstrate you understand why.

3. Now add `Verify` with `Times.Once`. Run again. Does it fail? Why or why not?

4. What would you add to `GetTotal()` to prevent negative totals? Write the test first (TDD), then the fix.

---

## Solution

See `tests/Infrastructure.Tests/Services/CartService_Tests.cs` — `SetCartAsync_ShouldReturnNull_WhenSaveFails`.

For Q4: add `Math.Max(0, Subtotal - Discount + DeliveryMethod.Price)` to `GetTotal()`. The test becomes:
```csharp
order.GetTotal().Should().Be(0m); // floor at zero, not -5m
```

---

## Tests

- `CartService_Tests.cs` — `SetCartAsync_ShouldReturnNull_WhenSaveFails` (fixed mock)
- `CartService_Tests.cs` — `SetCartAsync_ShouldSaveAndReturnCart_WhenSuccessful` (Verify pattern)
- `OrderService_Tests.cs` — `CreateOrderAsync_WithSamePaymentIntentId_ShouldReturnExistingOrder`
- `Order_DiscountEdge_Tests.cs` — `GetTotal_WhenDiscountExceedsSubtotal_TotalIsNegative`

---

## Interview Q&A

**Q: How do you verify a Moq setup was actually called?**
A: `mock.Verify(x => x.Method(...), Times.Once)` after the act. Without it, an unmatched setup silently returns the default — the test passes but proves nothing.

**Q: Why do you need `As<IDatabaseAsync>()` when mocking StackExchange.Redis?**
A: `StringSetAsync` is an explicit interface implementation — only accessible through `IDatabaseAsync`. Moq's `Setup` on the concrete type can't reach explicit implementations.

**Q: What is your coverage target and why?**
A: 90% on Core and Application — pure logic with no external dependencies, no excuse not to test. ~85% on Infrastructure services. Controllers at 0% by design — covered by Playwright E2E. I use `[ExcludeFromCodeCoverage]` on Stripe services with a documented reason, not to hide gaps.

**Q: You have a test that documents a negative total. Is that a bug?**
A: It's undocumented behavior. I wrote a test that captures what the code currently does and left a comment explaining that if a floor is ever added, the test will fail and force a conscious decision. That's more useful than either ignoring it or silently "fixing" it.

**Q: How would you catch a broken mock in code review?**
A: Look for `Setup(...).ReturnsAsync(value)` with no corresponding `Verify`. Any failure-path test must verify the mock was actually called — otherwise the test proves nothing when the setup doesn't match.

**Q: Why didn't you unit test PaymentService?**
A: It calls `new PaymentIntentService()` and `new RefundService()` directly — no DI seam to inject a mock. Unit testing would require real HTTP calls to Stripe's sandbox. The right fix is injecting an `IStripeClient` abstraction, which I documented as deferred architectural debt. I added `[ExcludeFromCodeCoverage]` with a comment explaining exactly this.
