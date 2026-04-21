## SECTION A — CI/CD & GITHUB ACTIONS

> **Source:** Task 1 — 3-job GitHub Actions pipeline, OIDC auth, 12 pre-existing test failures.

---

### EASY

**A1.** [HIGH] What is the difference between `pull_request` and `push` triggers in GitHub Actions? Why does this project use both?

<details>
<summary>The 5% Answer</summary>

- `pull_request` fires on the **proposed change** — before merge. CI runs against the merge commit of the PR branch into base, catching integration failures that don't exist on the branch alone.
- `push` fires **after** a commit lands on the target branch.
- Using only `push`: broken code is already in `main` before CI catches it. The fix arrives one commit too late.
- This project uses `pull_request` for CI on both `main` and `development` (gate before merge), and `push: [main]` to trigger deploy only after merge completes.

```yaml
on:
  pull_request:
    branches: [main, development]   # CI gate — runs against the merge result
  push:
    branches: [main]                # deploy only — fires after merge
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"I just use `push` because it fires on everything." That deploys on every push to `development` — every feature branch merge triggers a production deploy.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `pull_request` CI runs against the **merged state** (PR branch + base branch combined), not just the PR branch. This catches conflicts introduced by parallel PRs — a scenario that branch-only CI misses entirely.

</details>

---

**A2.** [HIGH] [OUT-OF-SCOPE] What is OIDC-based authentication for Azure deployments? Why is it safer than a stored client secret?

<details>
<summary>The 5% Answer</summary>

- **OIDC (OpenID Connect)** — GitHub and Azure establish a trust relationship via a **federated credential** on a Managed Identity. No password is stored anywhere.
- GitHub requests a **short-lived token** per workflow run from Azure's STS. The token is scoped to the run and expires within minutes.
- Compare to a stored client secret: stored in GitHub Secrets as a long-lived credential, must be manually rotated, and is compromised if the secret store is breached.

```yaml
- uses: azure/login@v2
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}       # identifier, not a secret
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

These are IDs — not credentials. No password is stored or transmitted.

**Production consequence:** a leaked client secret grants persistent access until manually rotated. A leaked OIDC token is useless after minutes — and is scoped to one workflow run.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"OIDC is just another password stored differently." Wrong — it's a fundamentally different trust model. No credential is stored; trust is asserted cryptographically via GitHub's identity.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that OIDC tokens are bound to the specific repository, branch, and environment. A token generated for `main` cannot be used by a forked repo or a different branch — unlike a static secret that any holder can use from anywhere.

</details>

https://claude.ai/chat/e5da0b13-00a1-450f-b80c-9c440569473b

---

### MEDIUM

**A3.** [HIGH] Your pipeline has `push: branches: [main, development]` and a deploy job with no branch condition. What's the production impact and how do you fix it?

<details>
<summary>The 5% Answer</summary>

**Impact:** every merge to `development` (every PR merge during daily development) triggers a production deploy. Developers merging feature work multiple times a day would each cause a production deployment — including half-finished features and work-in-progress branches.

**Fix 1 — condition on the deploy job:**
```yaml
deploy:
  needs: [test-backend, test-frontend]
  if: github.ref == 'refs/heads/main'
```

**Fix 2 (cleaner) — separate triggers:**
```yaml
on:
  pull_request:
    branches: [main, development]  # CI only
  push:
    branches: [main]               # deploy only
```

The condition `github.ref == 'refs/heads/main'` is the correct check — `github.ref_name` and `github.head_ref` resolve differently on `pull_request` vs `push` events and can produce false matches.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Add `environment: production` to the job." That adds an approval gate but doesn't prevent the trigger — the job still fires on every `development` push, it just pauses waiting for manual approval.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `github.ref` on a `push` event is the fully-qualified ref (`refs/heads/main`). Using `github.ref_name` (just `main`) is simpler but `github.ref` is more explicit and unambiguous — no risk of a branch named `something/main` accidentally matching.

</details>

---

**A4.** [HIGH] Why does `dotnet test ./skinet` target the solution directory, but `dotnet publish ./skinet/src/API` targets the API project specifically?

<details>
<summary>The 5% Answer</summary>

- **`dotnet test ./skinet`** — runs against the `.sln` file which references all test projects. One command discovers and runs every test project automatically. Adding a new test project to the solution is sufficient — no CI change needed.
- **`dotnet publish ./skinet/src/API`** — only the API process needs to be deployed. Infrastructure, Application, and Core are project references and are compiled into the API's output automatically via transitive dependencies. Publishing the solution would produce multiple artifacts; publishing the API produces exactly what runs in Azure.

Publishing test assemblies to production: larger artifact, unused code in production memory, and potential security surface (test helpers sometimes contain backdoors or relaxed auth for testing).

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Publish the solution — it deploys everything cleanly." Test projects, seed scripts, and dev tooling would be deployed. The artifact would include xUnit, Moq, and Bogus in production.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `dotnet publish` automatically follows `<ProjectReference>` chains — publishing API includes Infrastructure, Application, and Core without explicitly listing them. Attempting to publish each layer separately produces multiple incomplete, unrunnable artifacts.

</details>

---

**A5.** [MEDIUM] [OUT-OF-SCOPE] `npm install` passes on CI but installs a different version than local. Why does this happen and how does `npm ci` prevent it?

<details>
<summary>The 5% Answer</summary>

**Why it happens:**
`npm install` resolves semver ranges in `package.json`. If `"@angular/core": "^18.0.0"` and a new patch `18.0.3` was released between the developer's last `npm install` and the CI run, CI installs `18.0.3` while local has `18.0.1`.

**How `npm ci` prevents it:**
- Reads `package-lock.json` **exactly** — no range resolution
- If `package.json` and `package-lock.json` are out of sync, `npm ci` **fails** immediately, forcing an intentional update
- Deletes `node_modules` before installing — guarantees a clean state, no leftover artefacts

**Practical failure mode:**
```
Local:  @angular/material@18.0.1 (installed 3 months ago)
CI:     @angular/material@18.2.0 (latest in ^18 range)
→ Angular build passes locally, fails on CI due to breaking change in 18.2.0
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Commit `node_modules` to avoid version drift." Bloats the repo with tens of thousands of files, breaks cross-platform CI (Linux binaries committed from Windows), and makes diffs unreadable.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `--frozen-lockfile` is the `yarn` equivalent, and `pnpm ci` for pnpm. Also: `npm ci` is significantly faster on CI because it skips dependency resolution entirely — it reads the lock file and extracts packages directly, bypassing the semver solver.

</details>

---

**A6.** [HIGH] [OUT-OF-SCOPE] A developer rapidly pushes 4 commits to the same PR branch. Four full CI runs fire, each consuming quota, and the earlier runs keep testing stale code. What's the one-block fix, and why does using `github.run_id` as the concurrency key break it?

<details>
<summary>The 5% Answer</summary>

**Without `concurrency:`**, every push queues a fresh run. Five pushes in a minute = five full `dotnet test` + `ng build` cycles against the first four stale commits. Minutes billed, runners clogged, and completion order can diverge from push order (one run on a slow runner finishes after a later run on a fast runner).

**The fix — one block under `on:`:**
```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true
```

- `group: workflow-ref` groups all runs on the same branch for the same workflow file
- `cancel-in-progress: true` cancels the in-flight run when a new one enters the group

**Why `github.run_id` breaks it:**
`github.run_id` is **unique per run** — each run lands in a group of size 1. The concurrency mechanism only activates when two runs share a group, so nothing is ever cancelled. Every push still runs to completion.

`github.ref` is the correct key: "same branch = same group = new push cancels the old."

**Production consequence:** on a busy repo with 15-minute CI, a developer force-pushing a fix while the old run is still going creates a race. Without `cancel-in-progress`, whichever finishes first triggers deploy logic downstream. If the old (broken) run completes seconds before the fixed one, the broken SHA ships.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"We'll cancel old runs manually in the Actions UI." Manual intervention across a dozen PRs per day is infeasible — and the first missed one deploys broken code. Concurrency is 3 lines of YAML.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Knowing that deploy jobs should **not** have `cancel-in-progress: true` — cancelling a half-finished Azure deploy leaves the App Service in a partial state (slot swap aborted, files mid-transfer). Split the workflow: CI uses cancellation, deploy uses a separate concurrency group without it — or no concurrency at all.

</details>

---

### HARD

**A7.** [HIGH] The CI pipeline found 12 pre-existing test failures on first run. Walk through the root cause of the `StringSetAsync` overload mismatch — why CI caught it but local dev didn't, and what the general lesson is.

<details>
<summary>The 5% Answer</summary>

**The specific bug:**
StackExchange.Redis 2.12.x added a new `StringSetAsync` overload with `Expiration` and `ValueCondition` parameters. Production code was updated to use the new overload, but the Moq setup still used the old parameter types:

```csharp
// Old overload (no longer matched):
mockDatabase.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<TimeSpan?>(),      // ← old type
        It.IsAny<When>(),           // ← old type
        It.IsAny<CommandFlags>()))
    .ReturnsAsync(false);

// New overload (what production code calls):
// StringSetAsync(RedisKey, RedisValue, Expiration, ValueCondition, CommandFlags)
```

Moq sees no matching setup → returns `false` (default for `bool`) → `SetCartAsync` returns `null` → assertion `result.Should().BeNull()` passes — but for the **wrong reason**. The mock was never called.

**Why local missed it:**
Developer machines may have different package versions cached, or a different Redis library version in their local `packages.lock.json`. CI runs `dotnet restore` in a clean environment against the exact lock file.

**General lesson:**
When upgrading a NuGet package, run all tests. Overload changes are **silent** in C# — the compiler selects the best match without warning. Existing mock setups become silently inoperative because Moq's type matching is exact, not coercive.

**Production consequence:** the test appeared to cover `SetCartAsync` failure behaviour. It covered nothing. A real Redis failure would have gone untested and potentially unhandled.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"We don't need CI because tests pass locally." Tests passing locally in a specific developer's environment is not a guarantee — it depends on their exact dependency versions, OS, and local configuration.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `RestorePackagesWithLockFile = true` in the `.csproj` — NuGet's equivalent of `package-lock.json`. With this flag, `dotnet restore` fails if the lock file is out of sync with package references, preventing silent version drift between dev and CI.

</details>

---

**A8.** [HIGH] A PR's CI passes on `pull_request`, but the deploy job on `push` to `main` fails with "Resource not found." The only change was adding a new environment variable. What are the 3 most likely causes?

<details>
<summary>The 5% Answer</summary>

**Cause 1 — Secret added to repository but not to the deployment environment:**
GitHub Actions has two secret scopes: repository-level and environment-level. If the deploy job has `environment: production`, it only sees secrets defined for that environment — not repository-level secrets.

```yaml
deploy:
  environment: production       # only sees secrets defined under this environment
  env:
    NEW_VAR: ${{ secrets.NEW_VAR }}  # was added to repo secrets — invisible here
```

Fix: add the secret to Settings → Environments → production → Secrets.

**Cause 2 — Variable used in deploy step but not passed via `env:`:**
The variable is defined in `env:` at the job level but referenced as an OS env var in a bash step that doesn't inherit it, or is referenced in the wrong context (e.g., `${{ env.NEW_VAR }}` vs `$NEW_VAR`).

**Cause 3 — Azure App Service app setting not added:**
The new env var is needed at runtime (not build time), but it was only added to the GitHub workflow — not to the Azure App Service Application Settings. The workflow deploys successfully but the app can't find the config at runtime.

**Diagnosis order:**
1. Check the exact step that fails → narrow down build-time vs runtime
2. Compare secret scopes (repo vs environment)
3. Check Azure App Service Configuration blade

**Production consequence:** the workflow completes green, the artifact is pushed, but the running app crashes on first use of the missing config. Without a smoke test after every deploy, the failure goes undetected until a user hits the broken feature — the pipeline reported success.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Restart the workflow — it might be a transient failure." A missing variable is deterministic, not transient. Retrying wastes time and produces the same failure.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that environment-scoped secrets **override** repository-level secrets of the same name — but only for jobs with `environment:` set. A secret named `CONNECTION_STRING` in both scopes has different values for CI (repo scope) vs deploy (environment scope). This intentional isolation is the correct pattern for staging vs production configs.

</details>

---

**A9.** [HIGH] Your CI runs `dotnet test` at solution level and all tests pass. A new test project is added, its tests are never run, and no one notices for 3 weeks. What went wrong and how do you enforce it never happens again?

<details>
<summary>The 5% Answer</summary>

**Root cause:**
The new test project was not added to the `.sln` file. `dotnet test ./skinet` discovers test projects by scanning the solution — a `.csproj` not referenced in the `.sln` is completely invisible.

**Verify:**
```bash
dotnet sln list                          # shows all projects in solution
find . -name "*.Tests.csproj" | sort    # shows all test projects on disk
diff <(dotnet sln list) <(find ...)     # spot the gap
```

**Fix:**
```bash
dotnet sln skinet.sln add path/to/NewTests/NewTests.csproj
```

**Enforce it never happens again — 3 approaches:**

1. **CI check:** add a step that counts test assemblies and fails if the count drops:
```yaml
- name: Verify test project count
  run: |
    COUNT=$(dotnet sln list | grep -c ".Tests.csproj")
    [ "$COUNT" -ge 4 ] || (echo "Expected ≥4 test projects, found $COUNT" && exit 1)
```

2. **Arch test:** write an architectural test that asserts all `*.Tests` folders are in the solution — catches drift automatically.

3. **PR checklist rule:** adding a new class library requires a paired test project + sln registration — enforced via PR description template.

**Production consequence:** 3 weeks of changes to the new project were untested in CI. If a regression was introduced, it shipped.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"I'd check the test count in the CI output." The output would show the same passing count as before — you'd never notice unless you knew the expected count. Passive monitoring doesn't catch omissions.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `dotnet test --list-tests` — runs test discovery without executing tests. Useful as a pre-flight: if 0 tests are discovered, something is wrong with the project setup. Integrating this as a "verify" step before the actual test run catches missing projects in seconds.

</details>

---

**A10.** [HIGH] The CI suite exposed two Moq failures in `CartServiceTests` on first run. Bug 1: `It.IsAny<string>()` was set up to match a `RedisKey` parameter and the mock silently never fired. Bug 2: the mock `.Setup(...)` call was written after `new CartService(redisDb)`. Explain the root cause of each and write the corrected setup.

<details>
<summary>The 5% Answer</summary>

**Bug 1 — Moq type matching (`It.IsAny<string>()` vs `It.IsAny<RedisKey>()`):**

Moq 4.20+ uses exact type matching for argument matchers. `RedisKey` is a struct with an implicit conversion from `string`, but Moq's matcher doesn't follow C# implicit conversions — it checks the declared parameter type.

```csharp
// WRONG — declared type is RedisKey, not string; matcher never fires
mockDb.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(
        It.IsAny<string>(),      // ← RedisKey != string to Moq
        It.IsAny<RedisValue>(),
        It.IsAny<Expiration>(),
        It.IsAny<When>(),
        It.IsAny<CommandFlags>()))
    .ReturnsAsync(true);

// CORRECT
mockDb.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),    // ← matches the declared type
        ...))
    .ReturnsAsync(true);
```

When the matcher doesn't fire, Moq returns the default value (`false` for `bool`) without error. Tests that assert on the mock's return value still pass — asserting behaviour that never actually ran.

**Why `.As<IDatabaseAsync>()` is required:**
StackExchange.Redis implements async methods as **explicit interface implementations** on `IDatabaseAsync`. `Mock<IDatabase>` doesn't expose them directly — `.As<IDatabaseAsync>()` lets you set up and verify those explicit members.

**Bug 2 — constructor runs before mock is configured:**

```csharp
// WRONG — CartService constructor calls GetDatabase() before the mock is set up
var mockRedis = new Mock<IConnectionMultiplexer>();
var service = new CartService(mockRedis.Object);  // constructor runs here, calls GetDatabase()
mockRedis.Setup(x => x.GetDatabase(...)).Returns(mockDb.Object);  // too late

// CORRECT — set up mock BEFORE passing it to the constructor
var mockRedis = new Mock<IConnectionMultiplexer>();
mockRedis.Setup(x => x.GetDatabase(...)).Returns(mockDb.Object);  // configured first
var service = new CartService(mockRedis.Object);  // constructor sees the configured mock
```

Constructor injection runs at instantiation. If the mock isn't configured before `new CartService(...)`, the constructor calls an unmocked dependency and any state established in the constructor is wrong for all subsequent test assertions.

**Production consequence:** a silently unmatched mock produces a test that compiles and passes while exercising zero production code paths. A real Redis failure would go untested — the feature's failure mode is invisible until production.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Change `It.IsAny<string>()` to `It.IsAny<object>()` — that matches everything." `object` is still not the declared type. Moq matches the exact declared parameter type — `object` produces the same no-match. The only correct fix is `It.IsAny<RedisKey>()`.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Using `MockBehavior.Strict` or calling `mockDb.VerifyAll()` in teardown — a strict mock throws immediately if any call is made that has no matching setup. A test with a wrong `It.IsAny<string>()` matcher would fail `VerifyAll()` because the setup was registered but never invoked. Strict mocks surface silent no-match scenarios automatically rather than letting the test pass on default return values.

</details>

---

**A11.** [HIGH] `Mock<IDatabase>.Setup(x => x.StringSetAsync(...))` registers cleanly but never fires — the test always sees the default `false`. Explain why StackExchange.Redis forces `Mock<IDatabase>().As<IDatabaseAsync>().Setup(...)`, and describe the exact runtime symptom when the `.As<>()` cast is omitted.

<details>
<summary>The 5% Answer</summary>

**The underlying mechanism — explicit interface implementation:**

`IDatabase` inherits from `IDatabaseAsync`, but StackExchange.Redis declares the async methods as **explicit interface implementations** on `IDatabaseAsync`:

```csharp
// Simplified — StackExchange.Redis source
public class RedisDatabase : IDatabase  // IDatabase : IDatabaseAsync
{
    // Explicit member — accessible only via IDatabaseAsync-typed reference
    Task<bool> IDatabaseAsync.StringSetAsync(RedisKey, RedisValue, ...)
        => /* real impl */;
}
```

Explicit members are invisible on the declaring class and on derived interfaces — they resolve **only through a reference typed as the declaring interface**. In production code, `IDatabase db; db.StringSetAsync(...)` compiles because C#'s overload resolution walks up the interface chain. But Moq's proxy generator builds a proxy for `IDatabase` — it does not automatically expose `IDatabaseAsync`'s explicit members through the `IDatabase` mock surface.

**The correct setup:**
```csharp
// WRONG — Mock<IDatabase>'s proxy has no handler for the explicit StringSetAsync
mockDb.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), ...)).ReturnsAsync(true);
// Setup compiles and registers, but the real call bypasses it.

// RIGHT — .As<IDatabaseAsync>() extends the proxy with IDatabaseAsync's members
mockDb.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), ...))
    .ReturnsAsync(true);
```

`.As<TInterface>()` tells Moq to mount `TInterface`'s members on the underlying proxy so that casts to `TInterface` actually intercept those calls.

**Runtime symptom if omitted:**

No exception. No warning. The unmocked method returns the default for its return type:

| Return type | Default |
|---|---|
| `Task<bool>` | `Task.FromResult(false)` |
| `Task<string?>` | `Task.FromResult<string?>(null)` |
| `Task<RedisValue>` | `Task.FromResult(RedisValue.Null)` |

A test asserting `result.Should().BeTrue()` fails with "expected True, got False" and no hint that the mock setup never matched. Debugging burns hours before someone notices the setup was registered but `mockDb.Invocations` is empty.

**Production consequence:** every Redis-related test with the missing `.As<>()` cast exercises zero production code — the mock returns defaults, the assertions drive on those defaults, and real Redis failures stay untested. This was one of the 12 pre-existing failures CI caught on first run (see A7). A green test suite for months meanwhile covering nothing.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Just declare the variable as `Mock<IDatabaseAsync>` from the start." That breaks the test — `CartService`'s constructor takes `IDatabase`, and `Mock<IDatabaseAsync>` doesn't fit the constructor. `.As<T>()` is the documented Moq API for extending a single mock instance across the explicit-interface seam without changing the constructor contract.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Mentioning **`MockBehavior.Strict`** as the preventive measure — `new Mock<IDatabase>(MockBehavior.Strict)` throws `MockException` on any call that has no matching setup, surfacing the omission on the first test run instead of letting it silently pass for months. Also: calling `mockDb.VerifyAll()` at teardown catches registered-but-never-invoked setups — the mirror failure mode where `.As<>()` is called but the wrong matcher (`It.IsAny<string>()` for a `RedisKey` parameter) was used, so the setup exists but never binds.

</details>

---

## ⚡ Syntax Quick-Fire

**A-QF1.** [HIGH] What does `github.ref` resolve to on a `push` to `main` vs on a `pull_request` targeting `main`?

<details>
<summary>The 5% Answer</summary>

- `push` to `main` → `github.ref` = `refs/heads/main`
- `pull_request` targeting `main` → `github.ref` = `refs/heads/main` (the **base** branch, not the PR branch)
- To get the PR source branch use `github.head_ref` — only populated on `pull_request` events
- Mixing them in a branch condition like `if: github.ref == 'refs/heads/main'` can match both a direct push AND a PR targeting main, accidentally triggering deploy jobs on PR runs

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Using `github.ref_name` (resolves to just `main`) and assuming it only matches on direct pushes. It also matches on `pull_request` targeting `main` — the same subtle ambiguity.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Pairing the branch condition with the event type: `if: github.event_name == 'push' && github.ref == 'refs/heads/main'` — unambiguously targets only direct pushes to main, never PR runs.

</details>

---

**A-QF2.** [HIGH] C# — what is the difference between `Task.WhenAll` and `Task.WhenAny`?

<details>
<summary>The 5% Answer</summary>

- **`Task.WhenAll(tasks)`** — returns a task that completes when **all** tasks complete. If any task throws, `WhenAll` re-throws the **first** exception; remaining exceptions are captured in `task.Exception.InnerExceptions` but not automatically observed.
- **`Task.WhenAny(tasks)`** — returns a task that completes when the **first** task completes (success or fault). The other tasks continue running unobserved in the background — they don't get cancelled.

```csharp
// WhenAll: parallel fan-out, wait for all
await Task.WhenAll(SaveOrderAsync(), SendEmailAsync(), UpdateInventoryAsync());

// WhenAny: race — take whichever finishes first (e.g., timeout race)
var winner = await Task.WhenAny(dataTask, Task.Delay(TimeSpan.FromSeconds(5)));
if (winner != dataTask) throw new TimeoutException();
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "If one task in `WhenAll` throws, the others are cancelled." They are not. `WhenAll` does not cancel remaining tasks on first failure — they continue running. Cancellation requires a shared `CancellationToken` passed to each task.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Mentioning that `WhenAll` swallows additional exceptions — if three tasks all throw, only the first is re-thrown directly. To observe all failures, inspect `task.Exception.InnerExceptions` on the returned aggregate task, or use individual `try/catch` per task before passing to `WhenAll`.

</details>

---

**A-QF3.** [HIGH] `npm ci` exits with an error on CI — what is the most likely cause?

<details>
<summary>The 5% Answer</summary>

`package-lock.json` is out of sync with `package.json`. `npm ci` requires them to be in exact agreement — if `package.json` was updated (e.g., a dependency added) without regenerating the lock file, `npm ci` fails immediately with:

```
npm error `npm ci` can only install packages when your package.json and package-lock.json are in sync
```

**Fix:** run `npm install` locally to regenerate `package-lock.json`, commit it, then re-run CI.

**Other causes (less common):**
- `node_modules` checked into the repo — `npm ci` deletes and reinstalls, but a dirty state can cause conflicts
- Node version mismatch between local and CI (`engines` field in `package.json`)

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Switching back to `npm install` on CI to "fix" the error. That defeats the purpose — `npm install` will silently install different versions than `package-lock.json` specifies, re-introducing version drift. The lock file must be fixed, not bypassed.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Mentioning that `npm ci` is intentionally strict: if it passes, every developer and every CI run installs byte-for-byte identical packages. This makes "works on my machine" dependency bugs impossible to hide — the lock file IS the reproducibility contract.

</details>
