# Testing

xUnit (.NET). No Angular unit tests — UI flows covered by Playwright E2E (Task 37).

This doc covers **automated (unit) tests** — how they run and what gets them.
Manual "does the site actually work" smoke testing lives in `sprint-end-checklist.md`.

## When to test (cadence)

| When | What | Where |
|---|---|---|
| **After each task** | `dotnet test` green + smoke the one new behavior + pass the gate | below |
| **Before `development` → `main`** | full suite + core smoke + this sprint's behaviors + infra | `sprint-end-checklist.md` |
| **After deploying to `main`** | Azure smoke (live URL, prod flow) | `sprint-end-checklist.md` |

### After each task (before opening the PR)

- [ ] `dotnet test` green locally
- [ ] Smoke the one behavior the task changed — happy path **and** the failure path it touches
- [ ] Pass the gate (EXPLAIN / REBUILD / BREAK) → tick `progress.md`

CI re-runs `dotnet test` + `npm run build` on the task PR — that's the backstop, not the first check.

## .NET Test Projects

```
tests/
├── Core.Tests/           # Entity + domain logic
├── Application.Tests/    # DTOs, specs, mapping, validators
├── Infrastructure.Tests/ # Services (mocked Redis, InMemory EF)
└── API.Tests/            # (placeholder — controllers covered by Playwright)
```

Shared helpers in `tests/Tests.Shared/`. Uses xUnit v3 + FluentAssertions + Moq.

## Coverage

Target ~80% backend. What's intentionally **not** covered:

- API controllers → Playwright E2E (Task 37)
- `PaymentService` / `CouponService` → `[ExcludeFromCodeCoverage]`: they call Stripe SDK classes directly with no DI seam. Architectural fix: inject an `IStripeClient` abstraction (deferred).

Snapshot percentages live in `progress.md` (Task 19 log), not here — they go stale.

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
