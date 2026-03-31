# Testing

xUnit (.NET). No Angular test setup.

## .NET Test Projects

```
tests/
├── Core.Tests/           # Entity + domain logic
├── Application.Tests/    # DTOs, specs, mapping
├── Infrastructure.Tests/ # Services (mocked dependencies)
└── API.Tests/            # (empty — controller tests not added yet)
```

Shared helpers in `tests/Tests.Shared/`. Uses xUnit + FluentAssertions + Moq.

## Running .NET Tests

```bash
# All tests
dotnet test

# Specific project
dotnet test tests/Core.Tests

# With output
dotnet test --logger "console;verbosity=detailed"
```

## Test Naming

`MethodName_Scenario_ExpectedBehavior`

## Angular Tests

No custom spec files. Angular default test scaffold not used. No test runner configured.

## Rules / Constraints

- Use FluentAssertions (`.Should().Be(...)`) — not raw `Assert.Equal`.
- Infrastructure tests mock external dependencies (Stripe, Redis) — don't call real APIs.
