# Testing

xUnit (.NET). No Angular unit tests — UI flows covered by Playwright E2E (Task 37).

## .NET Test Projects

```
tests/
├── Core.Tests/           # Entity + domain logic
├── Application.Tests/    # DTOs, specs, mapping, validators
├── Infrastructure.Tests/ # Services (mocked Redis, InMemory EF)
└── API.Tests/            # (placeholder — controllers covered by Playwright)
```

Shared helpers in `tests/Tests.Shared/`. Uses xUnit v3 + FluentAssertions + Moq.

## Coverage (Task 19 — Apr 2026)

| Layer | Coverage | Notes |
|---|---|---|
| Core | **90%** | Entities, value objects, domain logic |
| Application | **89.3%** | Specs, mappings, DTOs, validators |
| Infrastructure (services) | **~85%** | CartService 100%, OrderService 80% |
| API controllers | 0% | Covered by Playwright E2E |

`PaymentService` and `CouponService` are marked `[ExcludeFromCodeCoverage]` — they call Stripe SDK classes directly with no DI seam. Architectural fix: inject `IStripeClient` abstraction (deferred).

## Running .NET Tests

```bash
# All tests
dotnet test

# Specific project
dotnet test tests/Core.Tests

# With coverage (requires dotnet-coverage tool)
dotnet-coverage collect "dotnet test" --output coverage.xml --output-format cobertura
reportgenerator -reports:coverage.xml -targetdir:coverage-report -reporttypes:TextSummary "-assemblyfilters:+Core;+Application;+Infrastructure"
```

## Test Naming

`MethodName_Scenario_ExpectedBehavior`

## What Gets Tests

- Every new service with business logic → tests in the same PR
- Every new spec or mapping → tests in the same PR
- Infrastructure bootstrapping (factories, seed, config) → no unit tests
- Controllers → no unit tests (Playwright)
- Stripe services → no unit tests (no DI seam)

## Rules / Constraints

- Use FluentAssertions (`.Should().Be(...)`) — not raw `Assert.Equal`.
- Infrastructure tests use InMemory EF for OrderService, mock Redis for CartService.
- Never call real external APIs (Stripe, Redis) in unit tests.
