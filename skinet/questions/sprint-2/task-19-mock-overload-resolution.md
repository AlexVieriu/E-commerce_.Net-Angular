# Task 19 — Mock Overload Resolution

## Why this approach

We use Moq to unit test `CartService`, which calls StackExchange.Redis's `StringSetAsync`. The mock setup must match the exact overload the production code calls — otherwise the setup is silently ignored, the mock returns the default value, and the test passes accidentally rather than by design.

## What confused me

The test `SetCartAsync_ShouldReturnNull_WhenSaveFails` was set up with:

```csharp
mockDatabase.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(
        shoppingCart.Id,
        It.IsAny<RedisValue>(),
        It.IsAny<TimeSpan?>(),    // ← old overload
        It.IsAny<When>(),
        It.IsAny<CommandFlags>()))
    .ReturnsAsync(false);
```

But `CartService.SetCartAsync` calls the **newer** overload introduced in StackExchange.Redis 2.6+:

```csharp
StringSetAsync(RedisKey, RedisValue, Expiration, ValueCondition, CommandFlags)
```

The `TimeSpan?` + `When` overload no longer matches. Moq silently ignores the unmatched setup, Redis returns `false` by default for bool returns — so the test "passes" but is testing nothing.

## Explanation

StackExchange.Redis introduced a new `StringSetAsync` overload that takes `Expiration` (a struct) and `ValueCondition` (an enum) instead of `TimeSpan?` and `When`. The old overload still exists but is no longer what `CartService` calls when you pass a `TimeSpan`.

Because `IDatabaseAsync.StringSetAsync` is an **explicit interface implementation**, you must use `mockDatabase.As<IDatabaseAsync>()` to set it up — `mockDatabase.Setup(...)` alone won't reach it.

Moq does **not warn** when a setup is never matched. A test that passes because the mock returns a default value is worse than a failing test — it hides real bugs.

## Exercise

Given this production code:

```csharp
public async Task<bool> SetValueAsync(IDatabase db, string key, string value)
{
    return await db.StringSetAsync(key, value, TimeSpan.FromMinutes(30));
}
```

Write a Moq setup that correctly intercepts the call. Then break the setup intentionally (wrong overload) and verify the test still passes — demonstrating the silent failure.

## Solution

```csharp
// Correct setup — matches the Expiration/ValueCondition overload
mockDatabase.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<Expiration>(),       // not TimeSpan?
        It.IsAny<ValueCondition>(),   // not When
        It.IsAny<CommandFlags>()))
    .ReturnsAsync(false);

// Wrong setup — silently ignored, test passes with default (false) by accident
mockDatabase.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<TimeSpan?>(),        // ← wrong type
        It.IsAny<When>(),             // ← wrong type
        It.IsAny<CommandFlags>()))
    .ReturnsAsync(true);              // Would return true if matched — but it never is
```

To catch silent failures, always add a `Verify` call:

```csharp
mockDatabase.As<IDatabaseAsync>()
    .Verify(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<Expiration>(),
        It.IsAny<ValueCondition>(),
        It.IsAny<CommandFlags>()),
        Times.Once);
```

If the setup never matched, `Verify` will throw — catching the bug.

## Tests

`Infrastructure.Tests/Services/CartService_Tests.cs`
- `SetCartAsync_ShouldSaveAndReturnCart_WhenSuccessful` — correct `Expiration` overload + `Verify`
- `SetCartAsync_ShouldReturnNull_WhenSaveFails` — correct `Expiration` overload (fixed in Task 19)

## Interview Q&A

**Q: How do you verify a Moq setup was actually called?**
A: `mock.Verify(x => x.Method(...), Times.Once)` after the act. Without it, an unmatched setup silently returns the default and the test gives false confidence.

**Q: Why do you need `As<IDatabaseAsync>()` when mocking StackExchange.Redis?**
A: `StringSetAsync` is an explicit interface implementation on `IDatabase` — it's only accessible through the `IDatabaseAsync` interface. Moq's `Setup` on the concrete mock type can't reach explicit implementations, so you cast with `As<T>()` first.

**Q: How would you catch this class of bug in code review?**
A: Look for `Setup(...).ReturnsAsync(value)` with no corresponding `Verify`. Setups without verification are assertions waiting to be silently skipped. Any "failure path" test (ReturnsAsync false/null/throws) must verify the mock was called.
