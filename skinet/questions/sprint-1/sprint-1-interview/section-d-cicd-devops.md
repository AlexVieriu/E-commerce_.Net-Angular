## SECTION D — CI/CD & DEVOPS

### EASY

**D1.** What is the difference between `npm ci` and `npm install`? Which should you use in CI?

<details><summary>Answer</summary>

| | `npm ci` | `npm install` |
|---|---|---|
| Source | Installs exactly from `package-lock.json` | May update `package-lock.json` |
| `node_modules` | Deletes and reinstalls from scratch | Incremental update |
| Speed | Faster (no resolution step) | Slower |
| Fails if | Lock file out of sync with `package.json` | Never — resolves differences |

**Use `npm ci` in CI** — reproducible builds, no accidental package upgrades.
</details>

---

**D2.** What triggers a GitHub Actions workflow? Name three event types.

<details><summary>Answer</summary>

Common triggers:
- **`push`** — on commit to branches or tags.
- **`pull_request`** — on PR opened/synchronized/merged.
- **`workflow_dispatch`** — manual run from the GitHub UI.
- **`schedule`** — cron-based (e.g. nightly builds).
- **`release`** — on a published GitHub release.

Most CI pipelines use `push` + `pull_request` filtered to specific branches.
</details>

---

**D3.** What does `needs: [build]` do in a GitHub Actions job?

<details><summary>Answer</summary>

`needs: [build]` creates a dependency — the current job waits until the `build` job completes successfully before running. If `build` fails, dependent jobs are skipped. This enforces sequential execution: build → test → deploy.
</details>

---

**D4.** In the .NET config provider chain, if the same key exists in `appsettings.json` and User Secrets, which wins? Why?

<details><summary>Answer</summary>

User Secrets wins. .NET config uses a last-wins strategy — providers registered later override earlier ones. Default order: `appsettings.json` → `appsettings.{Environment}.json` → User Secrets → Environment Variables → Command Line.
</details>

---

### MEDIUM

**D5.** Your CI pipeline has three jobs: `build`, `test`, `deploy`. The `deploy` job runs even when `test` fails. What's wrong? Write the corrected YAML.

<details><summary>Answer</summary>

The original is missing `needs` directives — without them, all jobs run in parallel.

```yaml
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet build

  test:
    needs: [build]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet test

  deploy:
    needs: [test]
    runs-on: ubuntu-latest
    steps:
      - run: echo "Deploying..."
```

Now if build fails, test is skipped. If test fails, deploy is skipped.
</details>

---

**D6.** You have secrets stored in: `appsettings.json`, `appsettings.Development.json`, environment variables, User Secrets, and Azure Key Vault. Draw the provider chain. For each: when used, what belongs there, committed to git?

<details><summary>Answer</summary>

Chain (last wins):
1. `appsettings.json` — base config (URLs, feature flags). **Committed**.
2. `appsettings.{Env}.json` — env-specific overrides (logging levels). **Committed**.
3. User Secrets — local dev secrets (API keys). **NOT committed**.
4. Env vars — CI/CD, Docker config. **NOT committed**.
5. Key Vault — production secrets (connection strings, keys). **NOT committed**.

Rule: anything secret → User Secrets locally, Key Vault in prod. Never commit secrets.
</details>

---

**D7.** What is OIDC federation in the context of GitHub Actions and Azure? Why is it better than storing a client secret in GitHub Secrets?

<details><summary>Answer</summary>

OIDC federation lets GitHub Actions get a short-lived token from Azure AD per workflow run — no stored secrets. GitHub presents a JWT to Azure, Azure validates it against configured trust (repo, branch, environment), and issues an access token.

Better than stored client secrets because:
- No secret to rotate, leak, or expire.
- Tokens are short-lived (minutes).
- Scoped to specific repos/branches — forks can't abuse it.
- All token requests are audit-logged in Azure AD.

Setup: Azure AD app registration + federated credential pointing to GitHub's OIDC provider.
</details>

---

**D8.** Your `dotnet test` passes locally but fails on CI. The error is "Unable to find testhost." Name three common causes and how you'd debug each.

<details><summary>Answer</summary>

**Common causes:**
1. **Missing `Microsoft.NET.Test.Sdk`** — required for test discovery; without it the test host can't start. Check all test `.csproj` files.
2. **SDK version mismatch** — CI has an older SDK that doesn't support the target framework. Fix: add `global.json` to pin the SDK, or specify it in the CI runner setup.
3. **Runtime identifier mismatch** — test project targets a framework not installed on the CI agent.

**Debug steps:**
- Run `dotnet test --verbosity detailed` on CI and read the full output.
- Run `dotnet --info` on CI and compare SDK/runtime versions with local.
- Ensure every test project references `Microsoft.NET.Test.Sdk`.
</details>

---

**D9.** You rotate the Stripe secret key in Azure Key Vault. Your production app still uses the old key. Why? How do you fix it without downtime?

<details><summary>Answer</summary>

- **Root cause** — Key Vault config is loaded once at startup and cached. The app doesn't poll for changes.
- **Fix** — set `AzureKeyVaultConfigurationOptions.ReloadInterval` (e.g. 5 min); use `IOptionsMonitor<StripeSettings>` to receive live updates without restart.
- **Zero-downtime rotation** — add the new key in Key Vault, wait for the reload interval to propagate, then deactivate the old key. Or use a blue-green deployment swap.
</details>

---

**D10.** CI ran `dotnet test` and 12 tests failed on the first push — but they passed locally. What categories of bugs cause this and how do you investigate?

<details><summary>Answer</summary>

Categories:
1. **Environment-dependent**: hardcoded paths (`C:\...`), timezone assertions, culture-specific string formatting.
2. **Missing dependencies**: database not available, no test data seeded, missing env vars.
3. **Race conditions**: async tests with timing assumptions, tests sharing static state.
4. **Test ordering**: tests depend on execution order or shared mutable state.
5. **Resource conflicts**: port conflicts, file locks.

Debug: read CI log line by line, run locally with `--verbosity detailed`, reproduce in Docker.
</details>

---

**D11.** Your `dotnet test` in CI passes but `ng build` fails. The deploy job still runs. What's missing?

<details><summary>Answer</summary>

The deploy job only depends on the backend test job (or has no `needs` at all).

Fix: add separate `build-frontend` job with `ng build`, and make `deploy` need BOTH:

```yaml
deploy:
  needs: [test-backend, build-frontend]
```

If either fails, deploy is skipped.
</details>

---

### HARD

**D12.** Design a CI/CD pipeline for an e-commerce app with: staging and production environments, database migrations, feature flags, rollback capability, and zero-downtime deployment. Include the YAML structure and explain each step.

<details><summary>Answer</summary>

Pipeline: build → test → deploy-staging (auto on `develop`) → deploy-prod (manual approval on `main`).

DB migrations run as a separate step BEFORE deploy — must be backward-compatible (add columns, not rename). Feature flags (LaunchDarkly/Azure App Config) decouple deploy from release.

Zero-downtime: Azure deployment slots — deploy to staging slot, warm up, swap to production. Rollback: swap back.

Structure migrations to be additive so rollback doesn't break the DB.

```yaml
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps: [checkout, build, test]

  deploy-staging:
    needs: [build-and-test]
    if: github.ref == 'refs/heads/develop'
    environment: staging
    steps: [migrate-staging-db, deploy-to-staging-slot]

  deploy-prod:
    needs: [build-and-test]
    if: github.ref == 'refs/heads/main'
    environment: production  # requires manual approval
    steps: [migrate-prod-db, deploy-to-prod-slot, swap-slots]
```
</details>

---

**D13.** Your Key Vault is in Azure region West Europe. Your app is in East US. Every startup takes 2 seconds because of key loading latency. On a cold start, it takes 8 seconds. How do you optimize this?

<details><summary>Answer</summary>

1. **Regional replication**: create Key Vault in East US (same region as app).
2. **Azure App Configuration** as intermediary — cache secrets with Key Vault references, faster reads.
3. **Reduce secret count**: combine related secrets into one JSON blob.
4. **Startup warm-up**: Azure App Service warm-up path, health check endpoint.
5. **Cache with long `ReloadInterval`** (30min) — don't reload frequently.
6. **Use `ManagedIdentityCredential` directly** instead of `DefaultAzureCredential` (skips the auth chain).
</details>

---

**D14.** A junior developer accidentally pushes a secret to GitHub. The secret is now in the git history. What are the immediate steps (first 5 minutes)? What are the follow-up steps? Why is `git rebase` not enough?

<details><summary>Answer</summary>

**First 5 minutes:** ROTATE THE SECRET immediately (new API key, new password). Revoke the old one. The secret is compromised the moment it's pushed — bots scrape GitHub in seconds.

**Follow-up:** use BFG Repo-Cleaner or `git filter-repo` to remove from history, force-push, have all contributors re-clone.

**Why rebase isn't enough:** the old commits exist in reflog, forks, GitHub's cache, any CI that fetched the repo, and GitHub's event API.

**Prevention:** `.gitignore`, `git-secrets` pre-commit hooks, GitHub secret scanning.
</details>

---

**D15.** Your team wants to add a staging environment. Staging deploys on merge to `development`, production on merge to `main`. Both use Key Vault but with different secrets. Design the pipeline and config changes.

<details><summary>Answer</summary>

**Pipeline:** two GitHub environments (`staging`, `production`) with separate OIDC credentials. Two Key Vaults: `kv-myapp-staging`, `kv-myapp-prod`.

Workflow: push to `development` → build → test → deploy to staging (auto). PR to `main` → build → test → deploy to prod (requires approval).

Each deploy job uses `environment:` to select the right secrets. Config: `appsettings.Staging.json` + staging Key Vault, `appsettings.Production.json` + prod Key Vault. `ASPNETCORE_ENVIRONMENT` set per deployment slot.
</details>

---

**D16.** You need to add secret rotation without downtime. The current setup loads secrets at startup only. Design a solution. Compare: polling `ReloadInterval`, blue-green deploy, and `IOptionsSnapshot<T>`.

<details><summary>Answer</summary>

1. **`ReloadInterval` (e.g., 5min) + `IOptionsMonitor<T>`**: provider polls Key Vault in background, `IOptionsMonitor.OnChange` updates cached values. Simple, no restart needed. Risk: up to N minutes of stale secrets.

2. **Blue-green deploy**: deploy new instance with fresh config, swap traffic. Zero stale time but requires a deployment.

3. **`IOptionsSnapshot<T>`**: Scoped, re-reads from providers per request — but only sees changes if the provider has already reloaded.

**Best approach:** `ReloadInterval` + `IOptionsMonitor<T>` for live rotation + blue-green for major changes.
</details>

---

