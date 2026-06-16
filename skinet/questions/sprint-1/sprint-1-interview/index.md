# Sprint 1 — Interview Question Set

> **Coverage:** Tasks 1, 3, 4, 5-6-7 — CI/CD pipeline, secrets management, ASP.NET Identity, Clean Architecture, Specification pattern, error handling middleware, CSP/security headers, CORS, Angular standalone/zoneless, interceptors, guards, RxJS, Angular 19+ resource API.

---

## Sections

| Section | Topic | Questions | Difficulty Mix |
|---|---|---|---|
| [A](section-a-cicd-github-actions.md) | CI/CD & GitHub Actions | 11 | 2E / 4M / 5H |
| [B](section-b-dotnet-config-secrets.md) | .NET Config & Secrets | 11 | 2E / 5M / 4H |
| [C](section-c-identity-cookie-security.md) | ASP.NET Identity, Cookie Security & CORS | 11 | 2E / 4M / 5H |
| [D](section-d-clean-architecture-specs.md) | Clean Architecture & Specification Pattern | 9 | 2E / 3M / 4H |
| [E](section-e-error-handling-middleware.md) | Error Handling, Middleware & CSP | 8 | 1E / 3M / 4H |
| [F](section-f-angular-arch-interceptors.md) | Angular Architecture, Interceptors & Guards | 10 | 2E / 3M / 5H |
| [G](section-g-rxjs-app-init.md) | RxJS & App Initialization | 9 | 2E / 3M / 4H |
| [H](section-h-behavioral.md) | Behavioral & Situational | 6 | 0E / 6M–H / 0H |

**Total: 75 questions + 23 syntax quick-fire = 98 cards**

---

## Frequency Distribution

- `[HIGH]` — 66 questions (frequently asked in interviews, core best practices)
- `[MEDIUM]` — 9 questions (situational, good-to-know)
- `[LOW]` — 0 (none generated)

---

## Quick Reference by Topic

### CI/CD
- [A1] `pull_request` vs `push` triggers
- [A2] OIDC auth to Azure (no stored secrets)
- [A3] Pipeline deploying to prod on wrong branch
- [A6] Workflow `concurrency` group — cancel-in-progress + why `github.run_id` breaks it
- [A7] `StringSetAsync` overload mismatch — the 12 CI failures
- [A10] Moq type matching (`It.IsAny<string>()` vs `RedisKey`) + constructor setup order
- [A11] `Mock<IDatabase>.As<IDatabaseAsync>()` — explicit interface implementation in StackExchange.Redis

### Secrets & Configuration
- [B1] Config provider chain — 5 providers, last wins
- [B4] `DefaultAzureCredential` — App Service vs local
- [B7] `IOptions<T>` vs `IOptionsSnapshot<T>` vs `IOptionsMonitor<T>` — Key Vault rotation + captive dependency
- [B8] Key in 4 sources — which wins?
- [B10] Key Vault unavailable at startup — failure, mitigations, `.NET 10` `ValidateOnStart`
- [B11] 3 bugs in `Program.cs` config setup — provider order + missing Development guard + `?? throw`

### Identity, Security & CORS
- [C1] `HttpOnly` — prevents/doesn't prevent
- [C3] 5 failed logins — exact DB state
- [C6] `AllowedForNewUsers = true` — closes credential-stuffing window from account creation
- [C7] 3 vulnerabilities in bad cookie config
- [C9] Complete auth security design
- [C10] CSRF attack — exact HTTP request with `SameSite=None` vs `Strict`
- [C11] `AllowAnyOrigin()` + `AllowCredentials()` — exact exception and fix

### Architecture
- [D1] 4 layers + dependency direction
- [D3] `(double)` cast — SQLite passes, SQL Server crashes
- [D6] `ConcurrentDictionary` in UnitOfWork + singleton bug
- [D8] N+1 in `GenericRepository` — `AddInclude` fix + EF Core 10 split query
- [D9] 3 bugs in `GetAllWithSpecAsync` — `IEnumerable<T>` + `.Compile()` + missing `SpecificationEvaluator`

### Error Handling & Security Headers
- [E1] Why ExceptionMiddleware must be first
- [E5] 3 bugs in naive ExceptionMiddleware
- [E6] 404 semantics — empty collection vs not found
- [E7] `ApiErrorResponse` shape dev vs prod — why HTML breaks Angular
- [E8] CSP `unsafe-inline` — what it reintroduces + Angular 22 nonce approach

### Angular
- [F2] `OnPush` mandatory in zoneless — correctness not performance
- [F3] Interceptor order — why it matters
- [F7] `withCredentials: true` set but still 401 — 3 causes
- [F8] `adminGuard` must follow `authGuard` — exact failure + `isAdmin()` computed signal
- [F9] `fb.nonNullable.group(...)` — `FormGroup.value` returns `Partial<T>` vs `T` + signal null impact

### RxJS & Angular 19+
- [G2] `localStorage.removeItem('cart_id')` in inner `catchError` — prevents permanent-404 loop
- [G3] `forkJoin` failure for anonymous users
- [G6] Complete app initializer — full RxJS chain
- [G8] `switchMap` cancellation safe in initializer, breaks checkout
- [G9] `httpResource()` — Angular 19+ alternative and why `APP_INITIALIZER` is still correct

### Behavioral
- [H1] CI pipeline from scratch — 12 failures on first run
- [H2] Credentials found in codebase — response steps
- [H5] Pushing back on "wrap everything in BeginTransactionAsync"
