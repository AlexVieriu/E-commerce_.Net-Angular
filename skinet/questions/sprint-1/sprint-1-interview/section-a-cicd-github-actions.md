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

<details>
<summary>Explanation</summary>

### Behind the Scenes

**`pull_request`:**
1. You open (or update) a PR.
2. GitHub silently creates a **throwaway merge commit** in memory: `base branch + your branch`.
3. GitHub fires a webhook with that synthetic SHA → runner picks it up.
4. Runner checks out the **virtual merge result**, not your branch tip.
5. CI runs on "what would `main` look like if you clicked Merge right now."
6. The commit is never stored. It vanishes after the run.

**`push`:**
1. You run `git push`. Git sends commits to GitHub over HTTPS/SSH.
2. GitHub stores the commit, advances the branch ref.
3. GitHub fires a webhook → runner picks it up.
4. Runner checks out the **exact commit SHA** that just landed.
5. CI runs on that commit. Whatever is in the branch is already there.

**The key difference:** `push` tests what *is*. `pull_request` tests what *would be*.

---

### What `pull_request` CI actually does

When you open a PR, GitHub doesn't just test your branch. It silently creates a **virtual merge commit** — your branch already merged into the base branch — and runs CI on that. The commit is thrown away after. It never lands anywhere.

The question it's answering is not "does my branch work?" but **"if I click Merge right now, does `main` still work?"**

---

### Why testing branches individually isn't enough

Your branch passes. `main` passes. But the *combination* can still be broken.

**Example:**
- `main` has `Calculate(int a, int b)`
- You add a caller: `Calculate(1, 2, 3)` — you're planning to extend the signature
- Your colleague merges a change to `main` that simplifies it to `Calculate(int a)`
- Both branches pass CI on their own
- The merged result → compile error. Your 3-arg call no longer matches

`pull_request` CI catches this before merge. `push` CI catches it after — when it's already in `main`.

---

### The stale CI problem (multiple PRs)

With 4-5 PRs open at the same time (common in corporations):

- PR-A and PR-B both pass CI — but both were tested against `main` from this morning
- PR-A merges first
- PR-B's CI result is now **stale** — it was never tested against `main + PR-A`
- PR-B merges → `main` is broken

**Fix: GitHub Merge Queue.** PRs enter a queue. Each one is re-tested against the accumulated state before merging. Guaranteed: every PR is tested against the exact `main` it will produce.

---

### Real-world scenario — WorldPay style

At a payments company the pipeline looks like:

```
feature branch
    ↓ PR + CI
development
    ↓ PR + CI + QA sign-off
testing
    ↓ PR + CI + integration tests
integration
    ↓ PR + CI + performance/security tests
staging           ← mirrors production exactly
    ↓ PR + CI + manual approval
production
```

Each level is a gate. You cannot skip. `pull_request` CI runs at every boundary.

**Why so strict:** a bug in production at WorldPay isn't "users see an error." It's millions in failed transactions, regulatory fines, and SLA breaches with banks. The cost of a bad merge vastly outweighs the cost of slower delivery.

**The tradeoff:** a feature can take 2-3 weeks to reach production — not because of coding time, but because of waiting for sign-offs and test runs at each gate. Startups skip layers and accept the risk. Payment processors cannot.

</details>

---

**A2.** [HIGH] [OUT-OF-SCOPE] What is OIDC-based authentication for Azure deployments? Why is it safer than a stored client secret?

<details>
<summary>The 5% Answer</summary>

- **OIDC (OpenID Connect)** — GitHub and Azure establish a trust relationship via a **federated credential** on a Managed Identity. No password is stored anywhere.
- GitHub requests a **short-lived token** per workflow run from Azure's STS(Security Token Service). The token is scoped to the run and expires within minutes.
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

---

### MEDIUM

**A3.** [HIGH] Your pipeline has `push: branches: [main, development]` and a deploy job with no branch condition. What's the production impact and how do you fix it?

<details>
<summary>The 5% Answer</summary>

**Impact:** every merge to `development` (every PR merge during daily development) triggers a production deploy. Developers merging feature work multiple times a day would each cause a production deployment — including half-finished features and work-in-progress branches.

**Fix — separate triggers:**
```yaml
on:
  pull_request:
    branches: [main, development]  # CI only
  push:
    branches: [main]               # deploy only
```
</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Add `environment: production` to the job." That adds an approval gate but doesn't prevent the trigger — the job still fires on every `development` push, it just pauses waiting for manual approval.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Splitting deploy into a **dedicated workflow file** (`deploy.yml`) with only `push: [main]` as its trigger — CI and deploy are never in the same YAML file. This eliminates trigger-bleed entirely: a CI workflow physically cannot deploy, and a deploy workflow physically cannot run tests. Each file's intent is unambiguous at a glance, and a future developer adding `push: [development]` to `ci.yml` cannot accidentally affect deploys.

</details>

<details>
<summary>Explanation</summary>

### What actually happens when you add `development` to `push:`

```yaml
on:
  push:
    branches: [main, development]  # danger
```

Every time a PR merges into `development`, this fires. With no branch condition on the deploy job, the pipeline deploys to whatever Azure resource is wired up in that job. If there is only one App Service configured and it points to production — every feature merge hits production, mid-sprint, before QA has touched anything.

---

### Best practice — what corporations actually do

Two separate Azure resources, two deploy jobs with `if:` conditions:

```
feature/task-N  →  PR to development  →  CI only (pull_request trigger)
development     →  push trigger        →  CI + deploy to STAGING App Service
main            →  push trigger        →  CI + deploy to PRODUCTION App Service
```

```yaml
deploy-staging:
  if: github.ref == 'refs/heads/development'
  steps:
    - uses: azure/webapps-deploy@v2
      with:
        app-name: skinet-staging

deploy-production:
  if: github.ref == 'refs/heads/main'
  steps:
    - uses: azure/webapps-deploy@v2
      with:
        app-name: skinet-production
```

On top of this, corporations add a **manual approval gate** on the production job — a human clicks approve before it runs. Staging is automatic, production is gated.

---

### For this project

Solo + learning — staging deploy on `development` is optional. CI on feature PRs is the important gate. The minimum correct setup:

```
feature/task-N  →  PR to development  (CI only)
development     →  PR to main         (CI + deploy to production)
```

`development` accumulates sprint work. Only `main` deploys.

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

<details>
<summary>Explanation</summary>

### What `node_modules` is

When you run `npm install`, npm reads `package.json`, downloads every dependency, and dumps them all into a folder called `node_modules`. That folder is your project's local library shelf — all third-party code lives there, ready to import.

```
your-project/
  package.json       ← wish list: "I want @angular/core ^18"
  package-lock.json  ← exact receipt: "I got @angular/core 18.0.1"
  node_modules/      ← the actual files on disk
    @angular/
    rxjs/
    zone.js/
    ... (often 50,000+ files)
```

### Real-world analogy

Think of it like a recipe app:

- `package.json` = your shopping list ("any brand of olive oil, at least 500ml")
- `package-lock.json` = the exact receipt from last time ("Tesco Extra Virgin 500ml, batch #A3")
- `node_modules` = your actual pantry where the bottles sit

**`npm install`** goes shopping using the wish list. If a newer bottle is on the shelf, it grabs that instead. Your pantry differs from your colleague's pantry.

**`npm ci`** goes shopping using the receipt — same brand, same batch, every time. If the receipt says `18.0.1`, it installs exactly `18.0.1`. If someone changed the wish list without updating the receipt, it refuses to shop at all.

### Why `node_modules` is never committed to git

50,000+ files, 300 MB+, contains platform-specific binaries (Windows `.dll`, Linux `.so`). Committing it would make every `git clone` massive and break cross-platform CI. The lock file is the commitment — 1 file, 2 KB, platform-neutral.

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

**Cause 2 — Wrong syntax when referencing the variable in the workflow YAML:**
`env:` is a block inside your `.github/workflows/pipeline.yml` file — nothing to do with Azure. "Job level" means it sits under the job definition, so every step in that job can read it. But there are two reference syntaxes and they only work in specific places:

```yaml
# .github/workflows/pipeline.yml

jobs:
  deploy:
    runs-on: ubuntu-latest
    env:                                  # ← job-level: all steps below inherit this
      NEW_VAR: ${{ secrets.NEW_VAR }}

    steps:
      - run: echo "$NEW_VAR"             # ✅ bash syntax — works inside run: scripts
      - uses: some-action@v1
        with:
          token: $NEW_VAR               # ❌ broken — bash syntax doesn't work in with:
          token: ${{ env.NEW_VAR }}     # ✅ correct — GitHub expression syntax required here
```

The bug: the secret is wired up correctly in `env:` but then referenced with `$NEW_VAR` (bash) inside a `with:` block where only `${{ env.NEW_VAR }}` works — so the value arrives as an empty string at runtime.

**Cause 3 — Secret exists in GitHub but not in Azure App Service:**
The GitHub workflow can read `${{ secrets.NEW_VAR }}` during the deploy step (pipeline time), but the running app reads its config from Azure App Service → Settings → Environment variables (runtime). These are two separate stores — adding a secret to GitHub does not put it in App Service.

The deploy job completes green (it just uploaded files). The app starts, calls `IConfiguration["NewVar"]`, gets `null`, and throws "Resource not found."

Fix: Azure Portal → App Service → Settings → Environment variables → add `NEW_VAR`.

**Diagnosis order:**
1. Check the exact step that fails → narrow down build-time vs runtime
2. Compare secret scopes (repo vs environment)
3. Check Azure App Service Configuration blade

**Production consequence:** the workflow completes green, the artifact is pushed, but the running app crashes on first use of the missing config. Without a smoke test after every deploy, the failure goes undetected until a user hits the broken feature — the pipeline reported success.

**In this project:** secrets never live in GitHub — they live in Azure Key Vault, read at startup via `AddAzureKeyVault` + `DefaultAzureCredential`. The equivalent failure is either (a) the Key Vault secret was never created, or (b) the name uses `:` instead of `--` so the `IConfiguration` mapping silently misses it. Diagnosis: Azure Portal → Key Vault → Secrets blade, not GitHub Settings.

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

<details>
<summary>Explanation</summary>

> This project uses Azure + OIDC so secrets live in Key Vault, not GitHub. But most companies store credentials directly in GitHub Secrets — knowing how it works is an interview staple.

---

### Two secret scopes

GitHub has two places to store secrets:

| Scope | Where to add it | Who sees it |
|---|---|---|
| **Repository** | Settings → Secrets → Repository secrets | Every job in every workflow |
| **Environment** | Settings → Environments → `production` → Secrets | Only jobs that declare `environment: production` |

**Gotcha:** environments don't exist by default. A fresh repo has none. You must go to `Settings → Environments → New environment` and create one manually. Until you do, `environment: production` in your YAML is a no-op for secrets.

---

### The failure scenario

A company deploys to Fly.io using a static API token. Everything works. A developer adds a Stripe webhook secret:

- They go to `Settings → Secrets → Repository secrets` and add `STRIPE_WEBHOOK_SECRET`
- They think: "repo secrets are visible everywhere"

But the deploy job has `environment: production` — it looks at the production environment's secrets first. `STRIPE_WEBHOOK_SECRET` was never added there, so it resolves to an empty string at runtime → app crashes on startup → "Resource not found."

**CI passed** because the `test` job runs without `environment:`, uses no secrets, and always succeeds.

**The fix:** `Settings → Environments → production → Secrets → add STRIPE_WEBHOOK_SECRET`.

---

### What to say in an interview

- "I'd check whether the deploy job has `environment:` set, then verify the secret is scoped to that environment, not just the repository."
- Junior mistake: "I added it to repo settings, that should be enough."

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

**Bug 1 — wrong matcher type:**
- Moq checks `actualArg is T` at runtime. `RedisKey is string` → false. No match, no error, default return value.
- `StringSetAsync` is an explicit impl on `IDatabaseAsync` — `.As<IDatabaseAsync>()` is required to mount those members on the proxy.

```csharp
mockDb.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),    // declared type — not string
        It.IsAny<RedisValue>(),
        It.IsAny<Expiration>(),
        It.IsAny<When>(),
        It.IsAny<CommandFlags>()))
    .ReturnsAsync(true);
```

**Bug 2 — setup after constructor:**
- `CartService` calls `GetDatabase()` inside its constructor. Setup registered after `new CartService(...)` is too late — the constructor already ran against an unconfigured mock.

```csharp
mockRedis.Setup(x => x.GetDatabase(...)).Returns(mockDb.Object); // configure first
var service = new CartService(mockRedis.Object);                  // then construct
```

**Both bugs are silent** — the test compiles, runs, and passes while covering zero production code paths.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Change `It.IsAny<string>()` to `It.IsAny<object>()` — that matches everything." `object` is still not the declared type. Moq matches the exact declared parameter type — `object` produces the same no-match. The only correct fix is `It.IsAny<RedisKey>()`.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Using `MockBehavior.Strict` or calling `mockDb.VerifyAll()` in teardown — a strict mock throws immediately if any call is made that has no matching setup. A test with a wrong `It.IsAny<string>()` matcher would fail `VerifyAll()` because the setup was registered but never invoked. Strict mocks surface silent no-match scenarios automatically rather than letting the test pass on default return values.

</details>

<details>
<summary>Explanation</summary>

### Bug 2 — Setup after constructor

`CartService` calls `mockRedis.GetDatabase()` inside its constructor. At that moment, Moq intercepts the call and returns whatever is set up — if nothing is set up yet, it returns `null` (loose mock default).

```csharp
// ❌ Wrong order — constructor runs against an unconfigured mock
var service = new CartService(mockRedis.Object);          // GetDatabase() fires here
mockRedis.Setup(x => x.GetDatabase(...)).Returns(mockDb.Object); // too late
```

The service now holds a `null` database reference. Every subsequent call (`StringSetAsync`, `StringGetAsync`) throws `NullReferenceException` — or worse, returns a default and the test passes while exercising nothing.

```csharp
// ✅ Correct order — configure before constructing
mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
         .Returns(mockDb.Object);

var service = new CartService(mockRedis.Object); // GetDatabase() now returns mockDb.Object
```

**Why this is silent:** loose mocks return defaults (`null`, `false`, `0`) for unconfigured calls instead of throwing. The test compiles and often passes — assertions are satisfied by the wrong value, not the production path.

**General rule:** always configure a mock fully before passing it to the constructor. With `MockBehavior.Strict`, an unconfigured call throws immediately — no silent defaults, no debugging.

</details>

---

**A11.** [HIGH] `mockDatabase.Setup(x => x.StringSetAsync(...))` compiles and registers but never fires — the test always gets back `false` and passes for the wrong reason. Why does this happen with StackExchange.Redis, and what is the fix?

<details>
<summary>The 5% Answer</summary>

**Root cause — `StringSetAsync` is hidden behind a parent interface:**

`IDatabase` extends `IDatabaseAsync`. StackExchange.Redis declared `StringSetAsync` on `IDatabaseAsync` using **explicit interface implementation** — meaning the method is only reachable through an `IDatabaseAsync`-typed variable, even though `IDatabase` inherits it.

In production code this is invisible — C# finds it automatically. But Moq creates a fake object for `Mock<IDatabase>` that only intercepts calls made through `IDatabase`'s own declared members. Calls routed through `IDatabaseAsync` bypass it completely — no setup fires, no exception, just `false` back silently.

**Fix — tell Moq to also cover `IDatabaseAsync`:**
```csharp
// ❌ setup registers but never fires — IDatabase mock doesn't intercept IDatabaseAsync calls
mockDatabase.Setup(x => x.StringSetAsync(...)).ReturnsAsync(true);

// ✅ extends the same mock to also intercept IDatabaseAsync calls
mockDatabase.As<IDatabaseAsync>()
    .Setup(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<Expiration>(),
        It.IsAny<ValueCondition>(),
        It.IsAny<CommandFlags>()))
    .ReturnsAsync(true);
```

This is exactly what `SetCartAsync_ShouldSaveAndReturnCart_WhenSuccessful` does in this project (line 83).

**How to catch it:** add a `Verify` at the end of the test — this is already in the project (line 111):
```csharp
mockDatabase.As<IDatabaseAsync>()
    .Verify(x => x.StringSetAsync(...), Times.Once);
```
If `StringSetAsync` was never intercepted, `Verify` throws — the test fails loudly instead of silently passing.

**How to know `.As<IDatabaseAsync>()` exists:** when a mock setup compiles but never fires, check the interface the method actually lives on (F12 in VS on the method). If it's on a parent interface, search "Moq explicit interface implementation" — `.As<T>()` is Moq's documented fix for this exact case.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Just change the variable to `Mock<IDatabaseAsync>`." That breaks the constructor — `CartService` takes `IDatabase`, not `IDatabaseAsync`. `.As<T>()` extends the existing mock without touching the constructor contract.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Adding `Verify(Times.Once)` after the act — as done in this project. If the setup never fired, Verify catches it immediately instead of letting the test silently pass on a default return value. `MockBehavior.Strict` is the stronger version: it throws the moment any unmocked call is made, so you never even reach the assert.

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
