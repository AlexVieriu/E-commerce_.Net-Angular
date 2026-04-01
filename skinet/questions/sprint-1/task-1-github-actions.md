claude --resume dbc7a4f4-df3b-4f9f-880f-e344aee3a522

# Task 1 — GitHub Actions CI/CD

## Why this approach

Task 1 goal: every PR to `main` or `development` must pass .NET tests and Angular build
before merge. Deploys to Azure only when code lands in `main`.

---

## What we built

### Pipeline flow

```
feature/* → PR → development   →  CI: .NET tests + Angular build (parallel, no deploy)
development → PR → main        →  CI: same
merge to main                  →  CI + CD: deploy to Azure skinet-2025-test
```

### 3-job structure

| Job | Trigger | Does |
|---|---|---|
| `test-backend` | PR + push to main | restore → build → dotnet test (solution level) |
| `test-frontend` | PR + push to main | npm ci → ng build |
| `deploy` | push to main only | rebuild Angular → dotnet publish → Azure deploy via OIDC |

---

## What confused me

**Why split `pull_request` and `push` triggers?**
`pull_request` fires on the proposed change — before merge. `push` fires after.
If you only use `push`, broken code is already in `main` before CI catches it.

**Why `push: [main, development]` would deploy on both?**
That's the trap. `push` on both branches means deploy fires on every merge to `development`
too. The fix: keep `push: [main]` for deploy, use `pull_request: [main, development]` for CI.

**Why does the deploy job say "skipped" on PRs?**
By design. The `if: github.event_name == 'push' && github.ref == 'refs/heads/main'`
guard skips it on every PR run. It only runs after merge to `main`.

**Why solution level for dotnet test but API level for publish?**
`dotnet test ./skinet` picks up all 4 test projects via the `.sln` file automatically.
`dotnet publish ./skinet/src/API` targets only the API — we don't deploy tests.

**Why `npm ci` not `npm install`?**
`npm ci` installs exactly what is in `package-lock.json` and fails if out of sync.
`npm install` can silently update packages → different versions on CI vs local.

**Why Node 24.x?**
Angular 22 requires `>=24.13.1`. `24.x` satisfies that and stays on the latest patch.

**Why `.NET 11` needs `dotnet-quality: 'preview'`?**
.NET 11 is not yet GA. Without `dotnet-quality: 'preview'` the setup action can't
find the SDK and the build fails.

**Why OIDC instead of a stored password for Azure?**
Azure and GitHub trust each other via a federated credential on a managed identity.
No secret stored anywhere — GitHub gets a short-lived token per run. Safer and easier
to rotate than a client secret.

---

## Fixes found by CI (pre-existing failures)

CI is valuable not just for future regressions — it caught 12 bugs that already existed.

### 1. GlobalUsing.cs — wrong namespace

```csharp
// Wrong — these sub-namespaces don't exist
global using Application.Specifications.OrderSpecifications;
global using Application.Specifications.ProductSpecification;

// Correct — everything is flat under Application.Specifications
global using Application.Specifications;
```

### 2. PagingParams — wrong expected value in test

```csharp
// Test said 50, production code has 60
pagingParams.PageSize.Should().Be(60); // was Be(50)
```

### 3. OrderSpecification — ApplyCriteria only applies WHERE

`BaseSpecification.ApplyCriteria()` applies only the `WHERE` clause.
In production, `SpecificationEvaluator` applies ordering + paging on top.
Tests that assert ordering or pagination need a helper that mimics the evaluator:

```csharp
private static IQueryable<T> ApplyFullSpec<T>(BaseSpecification<T> spec, IQueryable<T> query)
{
    if (spec.Criteria != null) query = query.Where(spec.Criteria);
    if (spec.OrderByDesc != null) query = query.OrderByDescending(spec.OrderByDesc);
    else if (spec.OrderByAsc != null) query = query.OrderBy(spec.OrderByAsc);
    if (spec.IsPagingEnabled) query = query.Skip(spec.Skip).Take(spec.Take);
    return query;
}
```

### 4. PaymentSummary — assertion that could never pass

```csharp
// ExpYear IS 2025 — BeGreaterThan(2025) is always false
payment.ExpYear.Should().Be(2025); // was BeGreaterThan(2025)
```

### 5. CartService — mock setup order matters

`CartService` constructor calls `redis.GetDatabase()` immediately.
If you set up the mock after `new CartService()`, the constructor already ran
with no mock in place.

```csharp
// Setup MUST come before new CartService()
mockRedis.Setup(x => x.GetDatabase(...)).Returns(mockDatabase.Object);
var cartService = new CartService(mockRedis.Object); // constructor calls GetDatabase here
```

### 6. CartService — wrong StringSetAsync overload in Moq

StackExchange.Redis 2.12.x added a new `StringSetAsync` overload using `Expiration`
and `ValueCondition` types. `TimeSpan` implicitly converts to `Expiration`, so the
3-argument call routes to the new overload, not the old `TimeSpan?/When` one.

```csharp
// Old overload (not matched)
mockDatabase.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(key, value, It.IsAny<TimeSpan?>(), It.IsAny<When>(), ...))

// Correct — matches what CartService actually calls
mockDatabase.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<Expiration>(),      // TimeSpan → Expiration implicit conversion
        It.IsAny<ValueCondition>(),
        It.IsAny<CommandFlags>()))
    .ReturnsAsync(true);
```

Why `As<IDatabaseAsync>()`? StackExchange.Redis implements `StringSetAsync` as an
explicit `IDatabaseAsync` interface method. Moq can only set it up via `As<IDatabaseAsync>()`,
not directly on `Mock<IDatabase>`.

### 7. Moq — It.IsAny\<string\>() can't match RedisKey

```csharp
// Fails — implicit conversion from string to RedisKey makes this unmatchable in Moq 4.20+
mockDatabase.Verify(x => x.StringGetAsync(It.IsAny<string>(), ...))

// Correct
mockDatabase.Verify(x => x.StringGetAsync(It.IsAny<RedisKey>(), ...))
```

### 8. Angular — form .value vs fb.nonNullable

`FormGroup.value` returns `Partial<T>` with `string | null | undefined`.
`FormGroup.getRawValue()` returns `T` with `string | null`.
`fb.nonNullable.group()` makes controls return `string` — no null, no cast needed.

```typescript
// Wrong — .value returns Partial<{email: string | null}>
this.accountService.login(this.loginForm.value)

// Correct — nonNullable controls return string directly
loginForm = this.fb.nonNullable.group({
  email: ['', [Validators.required, Validators.email]],
  password: ['', Validators.required]
});
this.accountService.login(this.loginForm.getRawValue()) // {email: string, password: string}
```

### 9. .gitignore gaps

Files that should never be committed but were:

| Path | Why ignored |
|---|---|
| `skinet/client/.angular/` | Vite dev server cache, regenerated locally |
| `skinet/API/wwwroot/*.js/css/html` | Angular build output — CI rebuilds on every deploy |
| `.claude/settings.local.json` | Machine-local Claude Code settings |

---

## Interview Q&A

**Q: Walk me through your CI/CD pipeline.**
One workflow, 3 jobs. PRs to `main` or `development` trigger CI — backend tests and
Angular build run in parallel. Deploy only fires on merge to `main`, after both pass,
using OIDC via Azure managed identity. No secrets stored.

**Q: Why separate CI and CD triggers?**
`pull_request` gates the proposed change before merge. `push` fires after.
With only `push`, broken code is already in `main` before CI catches it.

**Q: What is OIDC and why use it over a client secret?**
OIDC lets GitHub prove its identity to Azure using a short-lived token, not a stored
password. GitHub and Azure share a trust relationship via a federated credential on a
managed identity. Nothing to rotate, nothing to leak.

**Q: What does `npm ci` do differently from `npm install`?**
Installs exactly what's in `package-lock.json`, fails if the lock file is out of sync
with `package.json`. Reproducible builds. `npm install` can silently update packages.

**Q: Why use `fb.nonNullable.group` in Angular reactive forms?**
Angular types form controls as `string | null` by default (resetting a control sets it
to null). `nonNullable` resets to the initial value instead, giving you `string` — no
cast or null check needed when passing the value to a service.

**Q: You found 12 test failures during CI setup — how?**
The pipeline ran `dotnet test` on the first push. Failures were pre-existing — they
passed locally before because they were never run in CI. Key bugs: wrong expected values,
mock setup order, wrong overload matched, `string` vs `RedisKey` type mismatch in Moq.

**Q: Why does `As<IDatabaseAsync>()` matter in Moq?**
StackExchange.Redis implements some async methods as explicit interface implementations
on `IDatabaseAsync`. When you mock `IDatabase`, Moq doesn't expose those methods directly.
`As<IDatabaseAsync>()` lets you set up and verify the explicit interface members.