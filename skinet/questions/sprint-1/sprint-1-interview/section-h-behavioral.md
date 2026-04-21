## SECTION H — BEHAVIORAL & SITUATIONAL

> **Source:** Sprint 1 tasks — CI pipeline setup (Task 1), secrets management (Tasks 3–4), ASP.NET Identity (Tasks 5–7), Clean Architecture (Tasks 8–14).

---

### MEDIUM

**H1.** [HIGH] Tell me about a time you set up a CI pipeline from scratch. What was the hardest part?

<details>
<summary>The 5% Answer</summary>

**Context (Task 1 — GitHub Actions pipeline):**
Set up a 3-job GitHub Actions pipeline: `build-and-test` → `build-angular` → `deploy`. On the first run, 12 tests failed that had been passing locally for weeks.

**Root cause (the hardest part):**
The `StringSetAsync` call in `CartService` used an overload that doesn't exist on the `IDatabase` interface in StackExchange.Redis — it existed on the concrete `RedisDatabase` class. Locally, the concrete type was injected via the DI container; CI used a mock that only implemented the interface. The overload mismatch compiled fine but threw `MissingMethodException` at runtime. Finding it required reading the stack trace carefully — it wasn't a test logic failure, it was an interface contract violation that CI exposed first.

**What I'd do differently:**
- Register the Redis mock against the interface contract specifically (not the concrete class) from day one — the mismatch would have been caught locally
- Run `dotnet test` in CI against every PR, not just the main branch — the tests had already diverged before the pipeline existed

**Talking points:**
- Concrete pipeline structure: 3 jobs, dependency ordering, `needs:` keyword
- OIDC authentication to Azure (no stored secrets in GitHub)
- `npm ci` vs `npm install` distinction for reproducible Angular builds
- Difference between solution-level test (`dotnet test`) and project-level publish

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"The hardest part was writing the YAML." YAML syntax is mechanical — any senior can learn it in an hour. The real challenge is designing the pipeline correctly: job dependencies, caching strategies, secret handling, and environment parity between CI and local development.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **pipeline-as-code feedback loops** — the first pipeline run being a "production smoke test" for your existing codebase. CI exposes environmental assumptions baked into local development that everyone quietly agreed to ignore. The first run failing is expected; what you learn from it is what matters.

</details>

---

**H2.** [HIGH] [OUT-OF-SCOPE] Describe a time you found sensitive credentials exposed in a codebase. What did you do?

<details>
<summary>The 5% Answer</summary>

**Context (Tasks 3–4 — secrets management):**
Found a hardcoded admin seed password directly in `Program.cs` and Stripe keys in `appsettings.json` — both committed to the repository. The Stripe keys were test keys (not production), but the admin password was used for the actual dev database.

**Immediate actions (in order):**
1. Rotated the credentials first — new password generated, Stripe test key revoked and regenerated — before fixing the code. The exposure window mattered more than clean code.
2. Moved secrets to `dotnet user-secrets` for local development and Azure Key Vault for the deployed environment.
3. Added `appsettings.json` to `.gitignore` for any values that could vary by environment (not the file itself, which has safe defaults, but a companion `appsettings.Local.json`).
4. Added a pre-commit hook that scans for common secret patterns (`sk_live_`, connection strings with passwords).

**What I didn't do:**
Rewrite git history to remove the committed values — the effort is high, the rotated credential is already invalid, and rewriting history on a shared branch causes more disruption than the expired secret warrants.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"I fixed the code and committed the fix." Fixing the code without rotating the credential first leaves the old value valid and exposed. Anyone who cloned the repo before the fix still has working credentials. Rotation first, fix second.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning the distinction between **test keys and production keys** — different urgency. A committed Stripe test key has limited blast radius (test mode only). A committed production database connection string with credentials is an immediate incident requiring stakeholder notification. Triage the severity before treating all credential exposures the same.

</details>

---

**H3.** [HIGH] Walk me through how you manage secrets across development, staging, and production environments.

<details>
<summary>The 5% Answer</summary>

**Three-environment secret strategy (this project):**

**Local development:**
```bash
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "AdminSettings:Password" "Dev_admin_123!"
```
User Secrets store in `%APPDATA%/Microsoft/UserSecrets/<project-id>/secrets.json` — never in the project directory, never committed.

**CI/CD (GitHub Actions):**
Secrets stored in GitHub repository secrets, injected as environment variables:
```yaml
env:
  ASPNETCORE_Stripe__SecretKey: ${{ secrets.STRIPE_SECRET_KEY }}
```
OIDC authentication to Azure eliminates stored deployment credentials.

**Production (Azure App Service):**
Azure Key Vault with Managed Identity:
```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{kvName}.vault.azure.net/"),
    new DefaultAzureCredential()
);
```
`DefaultAzureCredential` uses Managed Identity in App Service, Azure CLI locally. Key Vault secret `Stripe--SecretKey` maps to `Stripe:SecretKey` via the `--` → `:` separator convention.

**Provider chain (last wins):** `appsettings.json` → Environment Variables → User Secrets → Azure Key Vault

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"We use environment variables everywhere." Environment variables on a developer's machine are visible to all processes, logged by some shells, and can leak into crash dumps. User Secrets scopes the value to a specific project and user account. The distinction matters for multi-project machines.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **key rotation** without downtime — Azure Key Vault supports versioned secrets. New version is pushed; `DefaultAzureCredential` reads "latest" on the next config reload. App Service can be configured to reload config on secret version change via App Configuration or by restarting the app during a maintenance window.

</details>

---

**H4.** [HIGH] Tell me about a test that was passing for months but wasn't actually testing anything meaningful.

<details>
<summary>The 5% Answer</summary>

**Context (Task 1 — CI failures):**
`CartServiceTests` had a mock setup for `StringSetAsync` that matched a specific overload signature. The production code called a different overload. The mock silently matched nothing — it returned the default (`false`) without executing, and the test assertion was checking the return value of the mock itself, not a real behavior.

The test passed because:
1. The mock returned `false` (default for bool)
2. The assertion was `Assert.True(result)` — which should have failed
3. But the mock was set up with `Returns(true)` — which was never triggered because the wrong overload was called
4. The production call returned `false` from a no-op mock, and the test checked `_mockDb.Verify(...)` which... also matched nothing but didn't throw

The test was asserting that a mock behaved as mocked — not that the real system did anything.

**The real fix:** injecting against the interface (`IDatabase`) and running the test against a real Redis TestContainers instance. The concrete type distinction disappeared — you can't call a non-existent interface method.

**Lesson:** a test that only asserts mock behavior proves nothing about production code.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"We added more assertions to fix it." Adding assertions to a test that's already asserting the wrong thing doesn't fix the test — it makes the wrong behavior more elaborately wrong. The issue was the abstraction boundary between test and production, not the number of assertions.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **mutation testing** (e.g., Stryker.NET) as the systematic answer to "are my tests actually testing anything?" Mutation testing changes production code (flips booleans, removes return values) and checks whether any test fails. A test suite with 90% coverage but 30% mutation score means 70% of the mutations go undetected — the tests are not exercising the logic they cover.

</details>

---

**H5.** [HIGH] [OUT-OF-SCOPE] A senior developer told you to wrap every database call in `BeginTransactionAsync` "for safety." How did you handle the disagreement?

<details>
<summary>The 5% Answer</summary>

**The technical counter-argument:**
EF Core wraps every `SaveChangesAsync()` call in an implicit transaction automatically. Explicitly calling `BeginTransactionAsync` before every operation:
1. Adds overhead — an extra round-trip to open the transaction
2. Introduces bugs if the transaction isn't committed/rolled back in every code path (exceptions in `finally` blocks, async cancellation)
3. Is redundant for the common case of a single `SaveChangesAsync` at the end of a unit of work

Explicit transactions are appropriate when:
- Multiple `SaveChangesAsync` calls must be atomic (across multiple units of work)
- Mixing EF with raw SQL (`ExecuteSqlRawAsync`) that must be in the same transaction
- Distributed operations across multiple contexts

**How I handled it:**
- Asked the senior dev what scenario they were protecting against specifically
- Walked through EF Core's implicit transaction documentation
- Proposed: add explicit transactions only in the 2 places where we had multi-`SaveChanges` operations
- Outcome: we agreed on explicit transactions for those 2 locations, implicit for everything else

**What I didn't do:** capitulate silently or push back without showing evidence.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"I just did what they said — they're the senior." Seniority isn't correctness. The right move is to understand their concern (what failure scenario are they imagining?) and then show evidence either supporting or refuting it. Blind deference produces bad code and doesn't help the senior either.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning the **Unit of Work pattern** as the architectural answer: `UnitOfWork.Complete()` calls `SaveChangesAsync` once at the end of a business operation, wrapping all repository changes in a single implicit EF transaction. The senior dev's concern (partial writes) is already addressed by the pattern — the conversation becomes "let me show you how it's already protected."

</details>

---

**H6.** [MEDIUM] Describe a time you had to balance a security requirement against user experience. What tradeoff did you make and why?

<details>
<summary>The 5% Answer</summary>

**Context (Tasks 5–7 — cookie security configuration):**
Two competing tradeoffs:

**1. `SlidingExpiration = false` (security win, UX cost):**
Absolute 24h session means an active user gets logged out mid-session at hour 24 — potentially during checkout. The security benefit: a stolen session token expires predictably regardless of usage. The UX cost: surprise logouts. Decision: accepted the cost because e-commerce with stored addresses and order history is high-value — PCI-DSS-aligned absolute expiry is defensible to users ("for your security, sessions expire after 24 hours").

**2. `SameSite = Strict` (security win, UX cost):**
Breaks "click email link → land logged in" flows. A user clicking their order confirmation email lands on the login page even though they have an active session. Decision: accepted because this project has no OAuth and the target audience (B2C shoppers) rarely use email links to navigate back to active sessions. The full CSRF protection outweighs the occasional login prompt.

**Framework for the conversation:**
Quantify the UX cost (how many users hit this? how often?) and the security benefit (what attack does this prevent? how likely?). A CSRF attack is low-probability but high-impact. A surprise logout is high-probability but low-impact. The math favors security here.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Security always wins." That's not a tradeoff — it's an avoidance of the decision. If security always won, every app would require hardware tokens, biometric re-auth every 5 minutes, and no persistent sessions. Real security decisions are cost-benefit analysis, not absolutes.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that the right answer changes with business context — a banking app and a recipe site have different risk profiles. For banking: `SlidingExpiration = false`, short absolute expiry (15 min), `SameSite = Strict`, re-auth for sensitive actions. For a recipe site: `SlidingExpiration = true`, long expiry, `SameSite = Lax`. Neither is wrong — the tradeoff is explicit and context-driven.

</details>

---

## ⚡ Syntax Quick-Fire

**H-QF1.** [HIGH] Credentials found exposed in a git repo — what is the very first action, before any code fix?

<details>
<summary>The 5% Answer</summary>

**Rotate the credential immediately** — make the exposed value invalid before touching any code. Anyone who cloned the repo before the fix already has the credential. The code fix removes it from future clones; rotation is what stops active abuse.

**Sequence:**
1. Rotate (invalidate the old credential, generate new)
2. Verify the old credential is dead (test it — confirm 401)
3. Update the new credential in Key Vault / User Secrets
4. Fix the code (remove from committed files)
5. Audit access logs for unauthorized use during the exposure window

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Fix the code first, then rotate." The exposed credential remains valid during the time it takes to commit, review, and merge the fix. Anyone who already cloned the repo has an indefinitely-valid credential until rotation happens.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Distinguishing severity by credential type — a committed Stripe test key has limited blast radius (test mode only, no real charges). A committed production database connection string is an immediate incident requiring stakeholder notification and an audit of what data was accessible. Triage before treating all exposures identically.

</details>

---

**H-QF2.** [HIGH] EF Core — does `SaveChangesAsync()` run in a transaction by default?

<details>
<summary>The 5% Answer</summary>

**Yes** — EF Core wraps every `SaveChangesAsync()` call in an **implicit transaction** automatically. All pending inserts, updates, and deletes are committed atomically or rolled back together.

Explicit `BeginTransactionAsync()` is only needed when:
- Multiple `SaveChangesAsync()` calls across separate units of work must be atomic
- Mixing EF with raw SQL (`ExecuteSqlRawAsync`) in the same transaction
- Distributed operations spanning multiple `DbContext` instances

```csharp
// No explicit transaction needed — SaveChangesAsync is already atomic
await _unitOfWork.Repository<Order>().AddAsync(order);
await _unitOfWork.Repository<OrderItem>().AddAsync(item);
await _unitOfWork.Complete();  // single SaveChangesAsync — implicit transaction
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Wrapping every `SaveChangesAsync()` in `BeginTransactionAsync()` "for safety." It's redundant for single-save operations, adds a round-trip overhead, and introduces bugs if the transaction isn't committed in every code path (exceptions, early returns, async cancellation).

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing the Unit of Work pattern is the architectural expression of "one implicit transaction per business operation" — `UnitOfWork.Complete()` calls `SaveChangesAsync` once at the end, making all repository changes in the request atomic without any explicit transaction management.

</details>
