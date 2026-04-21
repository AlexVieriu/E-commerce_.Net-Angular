## SECTION B — .NET CONFIGURATION & SECRETS MANAGEMENT

> **Source:** Tasks 3–4 — config provider chain, User Secrets, Azure Key Vault, `DefaultAzureCredential`, admin seed password.

---

### EASY

**B1.** [HIGH] What is the .NET configuration provider chain? List the 5 providers in registration order and explain what "last wins" means.

<details>
<summary>The 5% Answer</summary>

Registration order (later provider overrides earlier on key conflicts):

1. `appsettings.json` — safe defaults, committed to git, no secrets
2. `appsettings.{Environment}.json` — environment-specific non-secrets
3. Environment variables — set in Azure App Settings or OS process
4. **User Secrets** — dev only, stored outside repo at `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`
5. **Azure Key Vault** — prod only, registered last so it overrides everything

**"Last wins":** if `ConnectionStrings:Redis` appears in both `appsettings.json` and Key Vault, Key Vault's value is returned. `IConfiguration["ConnectionStrings:Redis"]` returns the merged final value — the caller never knows which source provided it.

```csharp
// Program.cs — Key Vault registered last = highest priority
builder.Configuration.AddEnvironmentVariables();
if (!builder.Environment.IsDevelopment())
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), new DefaultAzureCredential());
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Use environment variables for everything in production." Azure App Service exposes env vars in the portal to anyone with App Service access and logs them in process dumps. Key Vault has access control, audit logs, and secret versioning.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `IConfigurationRoot.GetDebugView()` — shows the full merged config with source annotations. Invaluable for diagnosing "why is this key the wrong value" in development. Never call it in production — it exposes all secret values to whoever can see the output.

</details>

---

**B2.** [HIGH] What are the three Stripe keys? What does each one do, and which belong in Key Vault?

<details>
<summary>The 5% Answer</summary>

| Key | Purpose | Where |
|---|---|---|
| **PublishableKey** | Stripe.js uses it in the browser to tokenize card data. Intentionally public. | `appsettings.json` |
| **SecretKey** | Server authenticates to Stripe — creates PaymentIntents, processes charges. Never goes near the browser. | **Key Vault** |
| **WebhookSecret (WhSecret)** | Server verifies the HMAC signature on incoming webhook events — proves the event is from Stripe, not spoofed. | **Key Vault** |

```
Browser → card data → Stripe (PublishableKey)
Stripe  → token     → Browser
Browser → token     → Your API
API     → token + SecretKey → Stripe (charge)
Stripe  → webhook   → Your API → verify with WhSecret
```

**Why PublishableKey is fine in `appsettings.json`:** it's already sent to every browser that visits the app. Protecting it in Key Vault creates an unnecessary startup dependency for a non-secret.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Put all three in Key Vault for maximum security." PublishableKey is deliberately public — Stripe's documentation explicitly says it's safe to embed in client-side code. Protecting it in Key Vault adds latency at startup for zero security gain.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning the asymmetric threat: a leaked `SecretKey` lets an attacker charge arbitrary cards in your Stripe account. A leaked `WhSecret` lets them forge "payment succeeded" webhook events — creating orders without payment. Both require immediate rotation. Different attack surfaces, same urgency.

</details>

---

### MEDIUM

**B3.** [HIGH] A new developer clones the repo and gets `StripeSettings:SecretKey is not configured`. What are the exact steps to fix it, and why can't the fix be committed to the repo?

<details>
<summary>The 5% Answer</summary>

**Steps:**
```bash
cd skinet/src/API
dotnet user-secrets init   # adds UserSecretsId to .csproj (once per project)
dotnet user-secrets set "StripeSettings:SecretKey" "sk_test_..."
dotnet user-secrets set "StripeSettings:WhSecret" "whsec_..."
dotnet user-secrets set "AdminSettings:Password" "Pa$$w0rd12!"
```

**Why it can't be committed:**
`appsettings.Development.json` is tracked by git. Any committed secret is:
- In the repository, visible to anyone with read access
- In git history forever — `git log -p` shows the value even after the file is changed
- Cloned to every developer machine, CI server, and anyone who forks the repo

User Secrets stores data at `%APPDATA%\Microsoft\UserSecrets\{UserSecretsId}\secrets.json` — outside the project directory, never picked up by `git add`.

**If accidentally committed:** rotate all secrets immediately (assume compromised), then optionally clean git history as a secondary step. The rotation is the critical action.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Add the file to `.gitignore` and commit it." `.gitignore` only prevents **untracked** files. If the file was ever committed (even once), it's in history. `.gitignore` cannot remove committed history.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that a team onboarding doc should list exactly which secrets need to be set, with test values where safe (e.g., Stripe test keys). `dotnet user-secrets list` shows all currently set secrets — useful to verify a dev's setup without reading the JSON file directly.

</details>

---

**B4.** [HIGH] [OUT-OF-SCOPE] What is `DefaultAzureCredential` and how does it behave differently in Azure App Service vs on a developer's local machine?

<details>
<summary>The 5% Answer</summary>

`DefaultAzureCredential` is a **credential chain** that tries multiple auth methods in order:

1. `AZURE_CLIENT_ID/SECRET/TENANT` environment variables (service principal)
2. Workload Identity (Kubernetes pods)
3. **Managed Identity** ← fires automatically in Azure App Service
4. Azure CLI (`az login` session) ← fires locally
5. Visual Studio / VS Code credential

**In Azure App Service:**
The compute has a system-assigned Managed Identity. `DefaultAzureCredential` detects Azure's Instance Metadata Service (IMDS at `169.254.169.254`) and retrieves a token automatically. Zero configuration — no credentials to rotate.

**Locally:**
IMDS is unavailable. Falls through to Azure CLI. If you've run `az login`, `DefaultAzureCredential` uses your developer credential.

One line of code works in both environments:
```csharp
new DefaultAzureCredential()
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Set `AZURE_CLIENT_ID` and `AZURE_CLIENT_SECRET` on the App Service." That's the service principal approach — it creates a credential that must be rotated. Managed Identity is zero-credential; the App Service runtime handles the token exchange without any stored secret.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `DefaultAzureCredential` can be slow locally because it tries all methods in sequence (including IMDS, which times out after several seconds). For faster local dev, use `new AzureCliCredential()` directly — it skips the network checks and goes straight to the CLI token.

</details>

---

**B5.** [HIGH] [OUT-OF-SCOPE] You rotate the Stripe Secret Key in Azure Key Vault. The running app still uses the old key and charges fail. Explain why and give two production-viable fixes.

<details>
<summary>The 5% Answer</summary>

**Why:** `AddAzureKeyVault` reads all secrets **once at startup** — at `app.Build()` time. The config pipeline is immutable after that. Updating a secret in Key Vault has no effect on the running process.

**Fix 1 — Restart the App Service (immediate, manual):**
Forces a fresh config load. Appropriate for planned rotations with a maintenance window.

**Fix 2 — Polling with `ReloadInterval` (automated):**
```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri(keyVaultUrl),
    new DefaultAzureCredential(),
    new AzureKeyVaultConfigurationOptions
    {
        ReloadInterval = TimeSpan.FromMinutes(5)
    }
);
```
The config refreshes from Key Vault every 5 minutes without a restart. New requests use the updated key; in-flight requests finish with the old one.

**Safe rotation pattern:**
1. Add new key to Key Vault → app still uses old key (not yet reloaded)
2. Wait for `ReloadInterval` or restart
3. Verify new key works (test charge or monitor errors)
4. Deactivate old key in Stripe dashboard

Never deactivate the old key before the app reloads — that window causes charge failures.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"The app picks up the new key automatically." It does not. Without `ReloadInterval` or a restart, the running process uses whatever was loaded at startup — indefinitely.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **Azure App Configuration** with Key Vault references as the production-grade solution — it provides push-notification refresh via webhooks, near-instant propagation across all app instances, and feature flag management in the same service. For high-uptime production systems, polling with a 5-minute interval is the minimum; App Configuration gives sub-second propagation.

</details>

---

**B6.** [MEDIUM] Why does Azure Key Vault use `--` instead of `:` as a separator in secret names? Show the full mapping for `StripeSettings:SecretKey` and `ConnectionStrings:DefaultConnection`.

<details>
<summary>The 5% Answer</summary>

Azure Key Vault does not allow `:` in secret names — it's an invalid character. Secret names must match `^[a-zA-Z0-9-]+$`. The .NET `AddAzureKeyVault` provider automatically maps `--` → `:` when loading into `IConfiguration`.

| `IConfiguration` key | Key Vault secret name |
|---|---|
| `StripeSettings:SecretKey` | `StripeSettings--SecretKey` |
| `StripeSettings:WhSecret` | `StripeSettings--WhSecret` |
| `AdminSettings:Password` | `AdminSettings--Password` |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings--DefaultConnection` |

Application code reads `IConfiguration["StripeSettings:SecretKey"]` — unchanged. Only the Key Vault storage name differs.

For 3-level nesting: `Parent:Child:Key` → `Parent--Child--Key`. The provider applies the mapping for all levels.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Use `/` as a separator in Key Vault names." Forward slashes are also invalid in Key Vault secret names. The only valid alternative separator is `--`.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that Azure App Service **Connection Strings** (the tab in App Settings) maps to `ConnectionStrings:*` automatically — a connection string named `DefaultConnection` in the Azure portal becomes `ConnectionStrings:DefaultConnection` in `IConfiguration` without any `--` mapping. For connection strings specifically, the App Service tab can replace Key Vault while still masking the value in the portal.

</details>

---

**B7.** [HIGH] [OUT-OF-SCOPE] You set `AzureKeyVaultConfigurationOptions.ReloadInterval = TimeSpan.FromMinutes(5)`. Ten minutes after rotating `StripeSettings:SecretKey`, a singleton `PaymentService` still charges with the old key. Walk through `IOptions<T>` vs `IOptionsSnapshot<T>` vs `IOptionsMonitor<T>` and name which one fixes this.

<details>
<summary>The 5% Answer</summary>

| Interface | Lifetime | Refreshes? | Use for |
|---|---|---|---|
| `IOptions<T>` | Singleton | **No** — captured once at first resolution | Values that never change at runtime |
| `IOptionsSnapshot<T>` | Scoped | Yes — recomputed per request/scope | Per-request config (tenant, feature flags) |
| `IOptionsMonitor<T>` | Singleton | Yes — exposes `.CurrentValue` + `.OnChange` | Long-lived services needing live updates |

**Why the bug happens:**
`PaymentService` is registered as a singleton. It injects `IOptions<StripeSettings>`. `IOptions<T>` is resolved **once** at the service's first construction — the `SecretKey` string is copied into the `StripeSettings` instance and that instance is reused for the lifetime of the app. Key Vault's `ReloadInterval` rebuilds the `IConfiguration` tree, but `IOptions<T>` doesn't re-read from it.

**The fix — `IOptionsMonitor<T>`:**
```csharp
public class PaymentService
{
    private readonly IOptionsMonitor<StripeSettings> _stripe;

    public PaymentService(IOptionsMonitor<StripeSettings> stripe)
        => _stripe = stripe;

    public async Task Charge(decimal amount)
    {
        var client = new StripeClient(_stripe.CurrentValue.SecretKey);  // reads latest
        // ...
    }
}
```

`IOptionsMonitor<T>.CurrentValue` is evaluated on every call against the current state of the config pipeline. `OnChange` exposes a callback for change events when you need to proactively rebuild expensive derived state (an HTTP client, a cached token, a Stripe webhook registration).

**Why `IOptionsSnapshot<T>` in a singleton crashes:**
`IOptionsSnapshot<T>` is **scoped**. Injecting it into a singleton is a **captive dependency**. The DI container's default scope-validation (`ServiceProviderOptions.ValidateScopes = true` in Development) throws at startup:
```
InvalidOperationException: Cannot consume scoped service 'IOptionsSnapshot<StripeSettings>'
from singleton 'PaymentService'.
```

Production without `ValidateScopes` silently resolves the scoped service once (the first request's scope), then pins it for the entire singleton lifetime — the `StripeSettings` instance belongs to a long-since-completed HTTP request, and if that scope held any other scoped service (like `DbContext`), the context survives the request and corrupts state across concurrent calls.

**Production consequence:** rotating the Stripe key without restarting the app leaves `IOptions<StripeSettings>.SecretKey` frozen at the old value. Every charge fails with Stripe's "Invalid API Key" error; logs show both the old key in use and the new key correctly loaded in Key Vault. Hours of debugging on a config pipeline that's actually working — the bug is in how a specific consumer resolves options.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Use `IConfiguration` directly with `GetValue<string>` — reads fresh every time." Bypasses strong typing, loses `DataAnnotations` validation, and forces `IConfiguration` into every service (tying business logic to the config framework). `IOptionsMonitor<T>` is the DI-idiomatic fresh-read with type safety preserved.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Knowing that `IOptionsMonitor<T>.OnChange(handler)` fires on the thread that triggered the reload — for Key Vault `ReloadInterval`, that's a background polling thread. Expensive handlers (rebuilding a Stripe webhook registration, reconnecting a long-lived SDK client) should dispatch to a task queue or the ThreadPool; otherwise the reload loop slows down, reducing freshness across every consumer.

</details>

---

### HARD

**B8.** [HIGH] A key exists in `appsettings.json`, `appsettings.Production.json`, an Azure App Setting (env var), AND Key Vault. Which value does the running app use in production, and what breaks if Key Vault is registered before environment variables?

<details>
<summary>The 5% Answer</summary>

**In production (Key Vault registered last):** Key Vault's value wins.

Chain in registration order:
1. `appsettings.json` → overridden by
2. `appsettings.Production.json` → overridden by
3. Environment variables (Azure App Settings) → overridden by
4. Key Vault (registered last) → **final value**

**What breaks if Key Vault is registered before environment variables:**
```csharp
// WRONG — env vars override Key Vault
builder.Configuration.AddAzureKeyVault(...);          // registered first = lower priority
builder.Configuration.AddEnvironmentVariables();      // registered last = overrides Key Vault
```

Now an Azure App Setting (env var) for the same key overrides Key Vault. If someone set a test value in App Settings for debugging and forgot to remove it, that test value silently overrides the correct Key Vault value in production.

**Correct order:**
```csharp
builder.Configuration.AddEnvironmentVariables();      // env vars registered first
builder.Configuration.AddAzureKeyVault(...);          // Key Vault wins — registered last
```

**Production consequence:** wrong registration order means Azure App Settings can silently override Key Vault. A debugging env var set by a developer 6 months ago would persist indefinitely, overriding the rotated Key Vault value.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Key Vault always wins — it's a managed service so it has higher priority." Priority is entirely determined by registration order in code, not by the nature of the service.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Pointing to `ConfigurationManager` in .NET 6+ — `WebApplicationBuilder.Configuration` is both a builder AND a live `IConfiguration` simultaneously. You can call `builder.Configuration["KeyVaultUrl"]` after registering environment variables but before registering Key Vault, reading the URL from env vars to bootstrap the Key Vault connection. This is the correct pattern for the "Key Vault URL can't be in Key Vault" bootstrap problem.

</details>

---

**B9.** [HIGH] Walk through the admin seed password flow: why it only seeds once, why the Key Vault secret can be deleted after first deploy, and what happens to app startup if it's deleted before first deploy.

<details>
<summary>The 5% Answer</summary>

**Why it seeds only once — the guard:**
```csharp
if (!await userManager.Users.AnyAsync(x => x.Email == adminEmail))
{
    var password = config["AdminSettings:Password"]
        ?? throw new Exception("AdminSettings:Password not configured");
    await userManager.CreateAsync(admin, password);
    await userManager.AddToRoleAsync(admin, "Admin");
}
// On second deployment: user exists → block skipped → password never read
```

**Why the secret can be deleted after first deploy:**
Once `CreateAsync` stores the bcrypt hash in `AspNetIdentity`, the plaintext is never needed again. Identity validates passwords by hashing the input and comparing hashes — it doesn't decrypt the stored hash back to plaintext. Retaining the plaintext in Key Vault is unnecessary risk.

**If the secret is deleted before first deploy:**
`config["AdminSettings:Password"]` returns `null` → the `?? throw` fires → `SeedDatabaseAsync()` throws at startup → the app never serves requests → Azure App Service shows "Application Error (0x00000000)" and the deployment appears to succeed but the app is down.

**Deployment checklist pattern:**
1. Add `AdminSettings--Password` to Key Vault as part of the deployment runbook
2. Deploy and verify the app starts
3. Delete the Key Vault secret (it's no longer needed)
4. Document this in the runbook so the next environment deployment doesn't miss it

**Production consequence:** if the Key Vault secret is deleted before the first deployment completes seeding, the app crashes at startup with no user-facing message — the deployment pipeline reports success but the app never serves traffic. A monitoring alert on startup failure is the only automated catch for this.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Hardcode a strong default admin password in seed." That password is in git history forever, visible to every current and future contributor, and in every fork of the repository.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning the **one-time temporary credential pattern**: seed with a random password, force password reset on first admin login via an `IEmailSender` confirmation link. The seed password becomes a single-use bootstrap credential — compromising it gives no persistent access. This is the production-grade pattern used by most SaaS bootstraps.

</details>

---

**B10.** [HIGH] Azure Key Vault is unavailable when the app starts. Walk through the exact failure, the exception type, and two production-viable mitigations. What is the silent danger of catching the startup exception and what does `.NET 10` `ValidateOnStart()` add to the picture?

<details>
<summary>The 5% Answer</summary>

**What happens:**
`AddAzureKeyVault` is called during `builder.Configuration.Build()`. If Key Vault is unreachable (network partition, mis-configured URI, Managed Identity not yet assigned), the Azure SDK throws after retries are exhausted:

```
Azure.RequestFailedException: Service request failed.
Status: 0 (SocketException — no route to host)
  at Azure.Security.KeyVault.Secrets.SecretClient.GetPropertiesOfSecretsAsync(...)
```

`builder.Build()` propagates this → `Program.cs` crashes → Azure App Service marks the instance as failed. The **deployment reports success** (the artifact was pushed), but the app never serves traffic.

**Mitigation 1 — rely on Azure SDK's built-in retry (correct baseline):**
Azure SDK retries transient failures 3 times with exponential backoff by default. Transient outages under ~15 seconds resolve before the final retry. This handles the majority of cases with zero code change.

**Mitigation 2 — graceful degradation with critical key validation (this project's pattern):**
```csharp
// Program.cs — from build-deployment.md
if (!builder.Environment.IsDevelopment())
{
    try
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultUrl), new DefaultAzureCredential());
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Key Vault unreachable at startup — falling back to App Service settings");
        // App continues with lower-priority App Service env vars / connection strings
    }
}
```

**The silent danger of catching without validation:**
If you catch the exception and continue, the app starts with no Stripe key. Every payment attempt throws `NullReferenceException` at `StripeClient` construction — users can't checkout. The app appears healthy (process running, health endpoint returns 200) but the core business function is broken. Monitoring won't catch it unless you explicitly probe the payment flow.

**`.NET 10` `ValidateOnStart()` addition:**
```csharp
// Validates required config keys at startup — explicit crash with clear message instead of silent null
services.AddOptions<StripeSettings>()
    .BindConfiguration("StripeSettings")
    .ValidateDataAnnotations()
    .ValidateOnStart();   // .NET 6+, standard in .NET 10 projects
```

With `ValidateOnStart`, if Key Vault is caught and the fallback doesn't have `StripeSettings:SecretKey`, the app throws `OptionsValidationException` with a named field error — not a `NullReferenceException` deep in payment logic.

**Production consequence:** catching Key Vault startup failure without `ValidateOnStart` produces a silently broken app — operational metrics look fine while payment processing is entirely down.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Set up a circuit breaker to skip Key Vault if it's down." A circuit breaker pattern makes sense for runtime outages (mid-request), not startup. At startup, missing secrets means the app cannot function — starting anyway and serving broken responses is worse than crashing with a clear error.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **Azure App Configuration** as the production-grade alternative — it supports push-based refresh (webhooks on Key Vault secret changes), caches config locally, and has a built-in fallback to last-known-good state if the service is unreachable. For high-availability requirements, App Configuration sits in front of Key Vault and absorbs Key Vault availability issues transparently.

</details>

---

**B11.** [HIGH] Find all 3 bugs in this `Program.cs` configuration setup and write the corrected version:

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["KeyVaultUrl"]!),
    new DefaultAzureCredential());
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<StripeSettings>(
    builder.Configuration.GetSection("StripeSettings"));

var adminPassword = builder.Configuration["AdminSettings:Password"];
```

<details>
<summary>The 5% Answer</summary>

**Bug 1 — Provider registration order inverted:**
`AddEnvironmentVariables()` is registered **after** `AddAzureKeyVault`. Last provider wins, so any Azure App Setting (env var) for a key like `StripeSettings:SecretKey` silently overrides the Key Vault value. A debug env var set 6 months ago in the App Service portal stays authoritative indefinitely, masking rotated Key Vault secrets.

**Bug 2 — Key Vault registered unconditionally:**
No `if (!builder.Environment.IsDevelopment())` guard. Every local `dotnet run` attempts to connect to Key Vault. A new developer without `az login` set up hits a 10–15 second `DefaultAzureCredential` chain timeout on every startup, then crashes with `Azure.Identity.CredentialUnavailableException`. User Secrets (which is the intended dev path) is never consulted because startup died before the rest of the config pipeline ran.

**Bug 3 — Required secret read without null guard:**
`config["AdminSettings:Password"]` returns `null` if the secret is missing from every source. The value flows into `UserManager.CreateAsync(admin, null)` which throws `ArgumentNullException: Value cannot be null. (Parameter 'password')` — a low-level error with no indication that the real cause was a missing Key Vault entry. Fails fast with a confusing stack trace instead of a clear startup message.

**Corrected version:**
```csharp
// 1. Env vars registered FIRST — Key Vault overrides them, not the other way around
builder.Configuration.AddEnvironmentVariables();

// 2. Key Vault only in non-Development (User Secrets handles local)
if (!builder.Environment.IsDevelopment())
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(builder.Configuration["KeyVaultUrl"]!),
        new DefaultAzureCredential());
}

builder.Services.Configure<StripeSettings>(
    builder.Configuration.GetSection("StripeSettings"));

// 3. Required secret — fail fast with a clear message
var adminPassword = builder.Configuration["AdminSettings:Password"]
    ?? throw new InvalidOperationException(
        "AdminSettings:Password not configured. " +
        "Local: run `dotnet user-secrets set AdminSettings:Password <value>`. " +
        "Deployed: add the `AdminSettings--Password` secret to Key Vault.");
```

**Production consequence:** the original version silently flips to env-var-wins in production (Bug 1), blocks every new dev from running the app locally (Bug 2), and turns a missing Key Vault secret into a `UserManager.CreateAsync` crash deep inside seeding logic (Bug 3). Each bug degrades a different environment; together they guarantee at least one broken environment at any time.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Only spotting Bug 1. The order issue is the most "textbook" — any senior who's read the config docs catches it. The Development guard (Bug 2) and the `?? throw` pattern (Bug 3) are the operational lessons that separate someone who read the docs from someone who has actually deployed this configuration and been paged for it.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `services.AddOptions<AdminSettings>().BindConfiguration("AdminSettings").ValidateDataAnnotations().ValidateOnStart()` with `[Required]` on the DTO as the idiomatic replacement for both `GetSection(...).Configure` and the `?? throw` pattern. `ValidateOnStart` fails the entire host build with a structured `OptionsValidationException` listing all missing fields at once — one startup failure lists every missing secret, not a whack-a-mole of one `?? throw` at a time.

</details>

---

## ⚡ Syntax Quick-Fire

**B-QF1.** [HIGH] An environment variable named `ConnectionStrings__Redis` — what `IConfiguration` key does it map to?

<details>
<summary>The 5% Answer</summary>

`ConnectionStrings:Redis` — **double underscore `__`** is the env-var hierarchy separator, mapping to `:`. Single underscore is a literal character.

| Source | Raw name | `IConfiguration` key |
|---|---|---|
| Env var | `ConnectionStrings__Redis` | `ConnectionStrings:Redis` |
| Key Vault secret | `ConnectionStrings--Redis` | `ConnectionStrings:Redis` |
| `appsettings.json` | `"ConnectionStrings": { "Redis": "..." }` | `ConnectionStrings:Redis` |

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Using `ConnectionStrings_Redis` (single underscore) — maps to the literal key `ConnectionStrings_Redis`, not `ConnectionStrings:Redis`. The app silently gets `null` for the connection string with no error until the first Redis call fails at runtime.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing that Azure App Service automatically prefixes SQL connection strings with `SQLCONNSTR_` — a connection string named `DefaultConnection` in the App Service portal becomes `ConnectionStrings:DefaultConnection` in `IConfiguration` without any `__` mapping needed.

</details>

---

**B-QF2.** [HIGH] What does `config["AdminSettings:Password"] ?? throw new Exception("Not configured")` do?

<details>
<summary>The 5% Answer</summary>

Reads the config value and throws **immediately at startup** if it is `null` — the null-coalescing throw pattern for required configuration keys. Fails fast with a clear message rather than a `NullReferenceException` deep inside business logic.

```csharp
var password = config["AdminSettings:Password"]
    ?? throw new Exception("AdminSettings:Password not configured");
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Assuming `config.GetValue<string>("key")` throws for missing keys — it returns `null`. `GetRequiredSection` throws for missing **sections**, not missing leaf values. The `?? throw` is the only reliable guard for a single required value.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Preferring `services.AddOptions<AdminSettings>().BindConfiguration("AdminSettings").ValidateDataAnnotations().ValidateOnStart()` with `[Required]` on the DTO — validates the entire section at startup with a structured error per property, not a one-off `?? throw` per key.

</details>

---

**B-QF3.** [HIGH] Is the Stripe `PublishableKey` a secret? Should it go in Azure Key Vault?

<details>
<summary>The 5% Answer</summary>

No — `PublishableKey` is **intentionally public**. Stripe.js uses it in the browser to tokenize card data directly with Stripe's servers. Every visitor receives it in the JavaScript bundle.

- **Key Vault:** `SecretKey` and `WhSecret` — leaked means API/webhook access
- **`appsettings.json`:** `PublishableKey` — safe to commit, already sent to every browser

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Put all three in Key Vault for consistency." The `PublishableKey` is designed to be public — Stripe's docs say so explicitly. Adding a Key Vault dependency at startup for a non-secret introduces unnecessary latency and a new failure mode.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Understanding the asymmetric threat — a leaked `PublishableKey` lets an attacker do what every user does (tokenize a card). A leaked `SecretKey` lets them charge arbitrary amounts to any card in your Stripe account. The distinction drives the storage decision.

</details>
