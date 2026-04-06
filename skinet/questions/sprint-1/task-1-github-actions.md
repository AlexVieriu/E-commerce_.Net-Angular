claude --resume dbc7a4f4-df3b-4f9f-880f-e344aee3a522

# Task 1 — GitHub Actions CI/CD

## Plan

- CI job: `dotnet test` + `ng build` on every PR to `main` or `development`
- CD job: deploy to Azure App Service on merge to `main` only
- Auth: OIDC via Azure Managed Identity (no stored secrets)
- Fix all pre-existing test failures before pipeline can go green

## Todo

- [x] Create `.github/workflows/cicd.yml` with 3 jobs: test-backend, test-frontend, deploy
- [x] Configure OIDC federated credential on Azure Managed Identity
- [x] Fix 12 pre-existing test failures caught by CI
- [x] Add `.gitignore` entries for Angular cache + wwwroot build output

---

## Why this approach

Every PR to `main` or `development` must pass .NET tests and Angular build before merge.
Deploy only fires on merge to `main` — not on PRs, not on pushes to `development`.

Using OIDC instead of a stored client secret: GitHub gets a short-lived token per run via
a federated credential on a Managed Identity. Nothing to rotate, nothing to leak.

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

**Why OIDC instead of a stored password for Azure?**
Azure and GitHub trust each other via a federated credential on a managed identity.
No secret stored anywhere — GitHub gets a short-lived token per run. Safer and easier
to rotate than a client secret.

## Explanation

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

### Fixes found by CI (12 pre-existing failures)

CI is valuable not just for future regressions — it caught bugs that already existed.

Key ones:

- **GlobalUsing.cs** — wrong sub-namespaces (`Application.Specifications.OrderSpecifications` doesn't exist — it's flat under `Application.Specifications`)
- **PagingParams test** — expected `50`, production code had `60`
- **CartService mock** — `redis.GetDatabase()` is called in the constructor; mock setup must come before `new CartService()`
- **StringSetAsync overload** — Redis 2.12.x added new overload using `Expiration`/`ValueCondition`; `TimeSpan` implicitly converts so the old `TimeSpan?/When` overload is never matched
- **`It.IsAny<string>()` vs `RedisKey`** — Moq 4.20+ won't match `string` where `RedisKey` is expected
- **Angular `fb.nonNullable`** — `FormGroup.value` returns `Partial<T>` with nulls; `nonNullable` controls return `string` directly

## Exercise

1. You have a workflow that deploys on `push` to both `main` and `development`. What goes wrong and how do you fix it?
2. Why must mock setup happen before instantiating the class under test when the dependency is used in the constructor?
3. `npm install` passes on CI but installs a newer patch version of a library. Why is `npm ci` safer?

## Solution

1. Every merge to `development` triggers a deploy to production. Fix: add `if: github.ref == 'refs/heads/main'` to the deploy job, or move the deploy trigger to `push: [main]` only.
2. The constructor runs at `new CartService()`. If the mock isn't set up yet, the real (or unmocked) dependency runs instead. The setup call must precede instantiation so the constructor sees the mock.
3. `npm ci` reads `package-lock.json` exactly and fails if it doesn't match `package.json`. `npm install` resolves ranges and can silently bump patch versions, producing a different dependency tree on CI than local.

## Interview Q&A

**Q: Walk me through your CI/CD pipeline.**
A: One workflow, 3 jobs. PRs to `main` or `development` trigger CI — backend tests and
Angular build run in parallel. Deploy only fires on merge to `main`, after both pass,
using OIDC via Azure Managed Identity. No secrets stored.

**Q: Why separate CI and CD triggers?**
A: `pull_request` gates the proposed change before merge. `push` fires after.
With only `push`, broken code is already in `main` before CI catches it.

**Q: What is OIDC and why use it over a client secret?**
A: OIDC lets GitHub prove its identity to Azure using a short-lived token, not a stored
password. GitHub and Azure share a trust relationship via a federated credential on a
Managed Identity. Nothing to rotate, nothing to leak.

**Q: What does `npm ci` do differently from `npm install`?**
A: Installs exactly what's in `package-lock.json`, fails if the lock file is out of sync
with `package.json`. Reproducible builds. `npm install` can silently update packages.

**Q: You found 12 test failures during CI setup — how?**
A: The pipeline ran `dotnet test` on the first push. Failures were pre-existing — never
caught locally because they were never run in CI. Key bugs: wrong expected values, mock
setup order, wrong overload matched, `string` vs `RedisKey` type mismatch in Moq.

**Q: Why does `As<IDatabaseAsync>()` matter in Moq?**
A: StackExchange.Redis implements some async methods as explicit interface implementations
on `IDatabaseAsync`. Moq doesn't expose those directly on `Mock<IDatabase>`.
`As<IDatabaseAsync>()` lets you set up and verify the explicit interface members.
