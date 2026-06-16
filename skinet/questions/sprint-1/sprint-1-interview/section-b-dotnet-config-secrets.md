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
3. User Secrets — dev only, stored outside repo at `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`
4. Environment variables — set in Azure App Settings or OS process
5. **Azure Key Vault** — prod only, registered last so it overrides everything

- **"Last wins":** if `ConnectionStrings:Redis` appears in both `appsettings.json` and Key Vault, Key Vault's value is returned. The caller never knows which source provided it.

```csharp
builder.Configuration.AddEnvironmentVariables();
if (!builder.Environment.IsDevelopment())
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), new DefaultAzureCredential());
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Use environment variables for everything in production — they're always available." Azure App Service exposes env vars to anyone with portal access and logs them in process dumps; Key Vault has access control, audit logs, and rotation.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Mentioning `IConfigurationRoot.GetDebugView()` — shows the full merged config with per-key source annotations. Invaluable for diagnosing "why is this key the wrong value" locally; must never be called in production because it exposes every secret value in the output.

</details>

<details>
<summary>Explanation</summary>

**Understanding "last wins" in practice**

The config chain means the same key can exist in multiple places. The last registered provider wins. That sounds simple until something is returning the wrong value and you don't know why.

`GetDebugView()` solves this. Call it locally when a key has the wrong value:

```csharp
var root = (IConfigurationRoot)app.Configuration;
Console.WriteLine(root.GetDebugView());
```

Output shows every key, its final value, and exactly which provider supplied it:

```
ConnectionStrings:Redis = old-test-host:6379  (Secrets)
ConnectionStrings:Redis = localhost:6379       (JsonConfigurationProvider: appsettings.json)
```

You immediately see: User Secrets is overriding `appsettings.json` with a stale value. Without this you'd be guessing through every provider manually.

**Why never in production:** that output prints every key in the merged config — including Stripe keys, DB passwords, JWT signing keys — all in plain text. If it ends up in a log file, a crash report, or a response body, every secret is exposed at once.

This is also why where you store secrets matters. In dev, `GetDebugView()` is safe because secrets are in User Secrets on your local machine — only you can see the output. In production, secrets are either in Azure App Settings or Key Vault, and the exposure risk is completely different between them.

---

**What are Azure App Settings (env vars)?**
A list of key-value pairs stored in the Azure portal that the running app reads at startup.
`Portal → App Service → Configuration → Application Settings`

**How does a secret end up there?**
Two ways — both end up in the same place:
- A developer types the value directly in the portal (`+ New application setting`)
- A GitHub Actions pipeline writes it automatically on deploy:

```yaml
- run: |
    az webapp config appsettings set --name my-app \
      --settings StripeSettings__SecretKey=${{ secrets.STRIPE_SECRET_KEY }}
```

The GitHub Secret (`secrets.STRIPE_SECRET_KEY`) is safe inside GitHub — masked, never shown. But the moment the pipeline writes it to Azure App Settings, it becomes a plain visible value in the portal.

**The problem: who can see it?**
Anyone with "Contributor" role on the Azure subscription — every dev, DevOps, sometimes managers.
They just go to the portal, click the eye icon, and read the value in plain text.

**What is a process dump?**
When the app crashes, Azure saves a full memory snapshot of the running process. Since env vars live in memory, your Stripe key is inside that file. Anyone who downloads the dump can extract every secret from it.

**Why Key Vault is better — at a glance:**

| | Azure App Settings | Key Vault |
|---|---|---|
| Who can read it? | Any portal Contributor | Only identities you explicitly allow |
| Audit logs | None | Every read logged: who, when, from what IP |
| Secret versioning | Overwrite = gone | Every update creates a new version; rollback is one click |
| In process dumps? | Yes — stored as env var in memory | No — fetched via HTTP, not an env var |

**Where to find Key Vault features in the portal:**
- **Access control:** `Key Vault → Access control (IAM)`
- **Audit logs:** `Key Vault → Monitoring → Diagnostic settings`
- **Secret versions:** `Key Vault → Objects → Secrets → click secret name`

**Real story — without Key Vault:**
Dev #2 leaves. Before you remove their Azure access they open the portal, click the eye icon, and copy the Stripe key. They start charging cards on your account. You have no logs — you don't know when the key was read or by whom.

**With Key Vault:**
You remove dev #2 from Key Vault Access Policies on their last day. Portal access doesn't matter anymore — they can't reach the secret. Audit logs already show exactly when they last accessed it. You rotate by adding a new secret version; the old one is disabled, not deleted.

</details>

---

**B2.** [HIGH] What are the three Stripe keys, what does each do, and which belong in Key Vault?

<details>
<summary>The 5% Answer</summary>

| Key | Purpose | Where |
|---|---|---|
| **PublishableKey** | Stripe.js tokenizes card data in the browser — intentionally public | `appsettings.json` |
| **SecretKey** | Server authenticates to Stripe — creates PaymentIntents, processes charges | **Key Vault** |
| **WebhookSecret** | Server verifies HMAC signature on incoming webhook events — proves the event is from Stripe | **Key Vault** |

- **Why PublishableKey is safe in `appsettings.json`:** it's already sent to every browser. Storing it in Key Vault adds a startup dependency and latency for zero security gain.

**Complete checkout flow**

```
Browser  → POST /api/payments (basket)           → Your API
Your API → PaymentIntents.Create(amount)         → Stripe        (SecretKey)
Stripe   → { client_secret }                     → Your API → Browser

Browser  → card data (Stripe.js iframe)          → Stripe        (PublishableKey)
           card data never reaches your API
Browser  → confirmCardPayment(client_secret)     → Stripe
Stripe   → payment result                        → Browser       (UX only — can be faked)

Stripe   → webhook payment_intent.succeeded      → Your API
Your API → verify HMAC signature                 → WhSecret
Your API → mark order paid, send email           → done
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Put all three in Key Vault for maximum security." The `PublishableKey` is deliberately public — Stripe's documentation explicitly says it's safe to embed in client-side code.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing the asymmetric threat — a leaked `SecretKey` lets an attacker charge arbitrary cards in your Stripe account; a leaked `WebhookSecret` lets them forge "payment succeeded" events, creating orders without payment. Different attack surfaces, identical rotation urgency.

</details>

---

### MEDIUM

**B3.** [HIGH] A new developer clones the repo and gets `StripeSettings:SecretKey is not configured`. What are the exact steps to fix it, and why can't the fix be committed to the repo?

<details>
<summary>The 5% Answer</summary>

```bash
cd skinet/src/API
dotnet user-secrets init
dotnet user-secrets set "StripeSettings:SecretKey" "sk_test_..."
dotnet user-secrets set "StripeSettings:WhSecret" "whsec_..."
dotnet user-secrets set "AdminSettings:Password" "Pa$$w0rd12!"
```

- Stored at `%APPDATA%\Microsoft\UserSecrets\{UserSecretsId}\secrets.json` — outside the project directory, never picked up by `git add`.
- **Can't commit:** `appsettings.Development.json` is tracked by git; any committed secret lives in history forever — visible via `git log -p` even after the file is changed later.
- **If accidentally committed:** rotate all secrets immediately (assume compromised). History cleanup is secondary; rotation is the critical action.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Add the file to `.gitignore` and commit it." `.gitignore` only prevents untracked files — it cannot remove a file already in git history.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** `dotnet user-secrets list` shows every currently set secret — useful to verify a new dev's setup without reading the JSON file directly. A team onboarding doc should list exactly which secrets are needed with Stripe test keys where safe (e.g., Stripe test keys are safe to share; production keys are not).

</details>

<details>
<summary>Explanation</summary>

**What the config files look like when you clone the repo**

`appsettings.json` — committed, safe defaults only:

```json
{
  "StripeSettings": {
    "PublishableKey": "pk_test_..."
  },
  "AdminSettings": {
    "Email": "admin@skinet.com"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=skinet;..."
  }
}
```

`appsettings.Development.json` — also committed, non-secret dev overrides:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
```

`SecretKey`, `WhSecret`, and `AdminSettings:Password` appear in **neither file**. That's intentional — they're the keys that trigger the error. The new dev's job is to supply them via User Secrets (or Key Vault) without ever touching these two files.

---

**Where secrets live and how a new dev gets them — two real scenarios**

---

**Startup**

Secrets are in a shared doc (Notion, 1Password, Google Doc). The onboarding doc lists every key with test values where safe:

```
StripeSettings:SecretKey     → sk_test_... (from Notion "Dev Secrets" page)
StripeSettings:WhSecret      → whsec_...
AdminSettings:Password       → Pa$$w0rd12!
```

New dev runs:
```bash
cd skinet/src/API
dotnet user-secrets init
dotnet user-secrets set "StripeSettings:SecretKey" "sk_test_..."
dotnet user-secrets set "StripeSettings:WhSecret" "whsec_..."
dotnet user-secrets set "AdminSettings:Password" "Pa$$w0rd12!"
dotnet user-secrets list   # verify everything is set
```

Secrets live at `%APPDATA%\Microsoft\UserSecrets\<guid>\secrets.json` — on the dev's machine only, never in git.

---

**Corporation**

There is a dedicated **dev Key Vault** (`kv-myapp-dev`) with test secrets already loaded. New dev gets an Azure AD account and is added to the `Developers` AAD group, which has Key Vault Reader access.

Setup:
```bash
az login   # authenticates via Azure CLI
```

`DefaultAzureCredential` picks up the Azure CLI session locally, reads secrets directly from `kv-myapp-dev` — no `dotnet user-secrets set` needed at all.

```csharp
// Program.cs — same code, works locally and in production
builder.Configuration.AddAzureKeyVault(
    new Uri("https://kv-myapp-dev.vault.azure.net/"),
    new DefaultAzureCredential());
```

The onboarding doc says: "Run `az login`, request access to `kv-myapp-dev` via IT ticket, then `dotnet run`."

---

**Summary**

| | Startup | Corporation |
|---|---|---|
| Secrets stored in | Shared doc (Notion/1Password) | Azure Key Vault |
| New dev gets secrets | Copy-paste from doc, run `user-secrets set` | `az login` → Key Vault access via AAD group |
| `dotnet user-secrets` needed? | Yes | No |
| Risk | Shared doc can leak | AAD group access is audited and revoked on offboarding |

</details>

---

**B4.** [HIGH] [OUT-OF-SCOPE] What is `DefaultAzureCredential` and how does it behave differently in Azure App Service vs on a developer's local machine?

<details>
<summary>The 5% Answer</summary>

`DefaultAzureCredential` is a credential chain — tries multiple auth methods in order, uses the first that succeeds. Same code line authenticates differently depending on where the app runs:

| | Azure App Service | Developer's local machine |
|---|---|---|
| **Which leg fires** | Managed Identity | Azure CLI session |
| **How it works** | Azure provisions IMDS at `169.254.169.254` inside the instance — SDK calls it silently, gets a token automatically | IMDS doesn't exist locally → times out → falls through to the token cached by `az login` |
| **Credentials to manage** | None — Azure handles everything | Run `az login` once; SDK finds the cached session |
| **What breaks it** | Managed Identity not enabled in portal | Forgot to run `az login`, or token expired |

```csharp
// Same line works in both environments:
builder.Configuration.AddAzureKeyVault(
    new Uri(keyVaultUrl), new DefaultAzureCredential());
```

Full chain order (first success wins): env vars → Workload Identity → **Managed Identity** → **Azure CLI** → VS/VS Code.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Set `AZURE_CLIENT_ID` and `AZURE_CLIENT_SECRET` on the App Service." That's a service principal — credentials that must be manually rotated; Managed Identity is zero-credential and the runtime handles the token exchange.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** `DefaultAzureCredential` is slow locally — it probes IMDS (times out after seconds) before reaching Azure CLI. For faster local iteration, `new AzureCliCredential()` skips all the network checks and goes straight to the CLI session token.

</details>

<details>
<summary>Explanation</summary>

**What each credential type actually does**

---

**1. `AZURE_CLIENT_ID / SECRET / TENANT` — Service Principal**

A service principal is a "machine account" in Azure AD — like a username and password for an app instead of a person.

You register an app in Azure AD → get a Client ID and a Client Secret. You set those as environment variables. `DefaultAzureCredential` reads them and exchanges the credentials for an access token.

- Use case: CI/CD pipelines, containers that need Azure access but run outside Azure infrastructure.
- Problem: the secret expires and must be manually rotated. If it leaks, an attacker can impersonate the app.

---

**2. Workload Identity — Kubernetes pods**

Kubernetes-specific version of Managed Identity. Each pod gets a projected service account token (a file on disk). Azure Workload Identity exchanges that Kubernetes token for an Azure AD token automatically.

- No secrets to store or rotate.
- Only applies if you're running on AKS (Azure Kubernetes Service).

---

**3. Managed Identity — fires in Azure App Service**

Azure assigns a built-in identity to your App Service. Your app can call Azure services (Key Vault, Storage, etc.) without storing any credentials anywhere.

Under the hood, Azure provides a local HTTP endpoint (IMDS — see below). The SDK calls it silently to get a fresh token. You configure it once in the portal; Azure handles everything else.

- Use case: any app running inside Azure (App Service, Azure Functions, VMs).
- Best option when running on Azure — zero credentials, zero rotation.

---

**4. Azure CLI — fires locally after `az login`**

When you run `az login`, the Azure CLI saves a token on your machine (`~/.azure/`). `DefaultAzureCredential` checks for that cached session and uses it.

- Only works if you've run `az login` recently and the token hasn't expired.
- Nothing to configure in code — the SDK finds it automatically.

---

**5. Visual Studio / VS Code credential**

Same idea as Azure CLI, but uses the account you're signed into in the IDE. If your IDE is signed in to Azure, the SDK reads that session token.

- Fallback for devs who prefer not to use the CLI.
- Works transparently — just be signed in to the IDE with an account that has Key Vault access.

---

**What is IMDS?**

IMDS stands for **Instance Metadata Service**. It's a special HTTP endpoint at `169.254.169.254` that Azure runs inside every VM, App Service, and Function App.

It's only reachable from within Azure infrastructure — that's by design. Your app calls it to say "who am I?" and gets back a signed access token for the assigned Managed Identity. No credentials involved at all.

When `DefaultAzureCredential` is used locally, it tries to call IMDS first. Since `169.254.169.254` doesn't exist on your laptop, the call times out (several seconds). Only then does it fall through to Azure CLI. That timeout is why local startup feels slow — and why `new AzureCliCredential()` is faster for local development.

---

**Summary**

| Credential | Where it works | Credentials to manage? |
|---|---|---|
| Service Principal (env vars) | Anywhere | Yes — secret must be rotated |
| Workload Identity | AKS pods only | No |
| Managed Identity (IMDS) | Inside Azure | No — Azure handles it |
| Azure CLI | Local machine | No — just `az login` |
| VS / VS Code | Local machine | No — just sign in to IDE |

</details>

---

**B5.** [HIGH] [OUT-OF-SCOPE] You rotate the Stripe Secret Key in Azure Key Vault. The running app still uses the old key and charges fail. Explain why and give two production-viable fixes.

<details>
<summary>The 5% Answer</summary>

- **Why:** `AddAzureKeyVault` reads all secrets once at `builder.Build()` time. Config is immutable after that — updating Key Vault has no effect on the running process.
- **Fix 1 — restart App Service:** forces a fresh config load; appropriate for planned rotations with a maintenance window.
- **Fix 2 — `ReloadInterval`:**

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri(keyVaultUrl), new DefaultAzureCredential(),
    new AzureKeyVaultConfigurationOptions { ReloadInterval = TimeSpan.FromMinutes(5) });
```

- **Safe rotation order:** add new key → wait for reload → verify working → deactivate old key. Never deactivate the old key before the reload window — that gap causes charge failures.

**Production consequence:** without `ReloadInterval` or restart, the process uses startup-cached keys indefinitely; every charge fails with "Invalid API Key" after rotation.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "The app picks up the new key automatically." Without `ReloadInterval` or a restart, the running process keeps whatever was loaded at startup — indefinitely.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Azure App Configuration with Key Vault references provides push-notification refresh via webhooks — near-instant propagation vs a 5-minute polling minimum. For high-uptime production systems, App Configuration is the production-grade solution and absorbs Key Vault availability issues as a side effect.

</details>

---

**B6.** [MEDIUM] Why does Azure Key Vault use `--` instead of `:` as a separator in secret names? Show the full mapping for `StripeSettings:SecretKey` and `ConnectionStrings:DefaultConnection`.

<details>
<summary>The 5% Answer</summary>

- Azure Key Vault disallows `:` in secret names — valid characters are `^[a-zA-Z0-9-]+$`.
- The .NET `AddAzureKeyVault` provider automatically maps `--` → `:` when loading into `IConfiguration`.

| `IConfiguration` key | Key Vault secret name |
|---|---|
| `StripeSettings:SecretKey` | `StripeSettings--SecretKey` |
| `StripeSettings:WhSecret` | `StripeSettings--WhSecret` |
| `AdminSettings:Password` | `AdminSettings--Password` |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings--DefaultConnection` |

- Application code reads the `:` form unchanged — only the Key Vault storage name differs.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Use `/` as separator in Key Vault names." Forward slashes are also invalid in Key Vault secret names — `--` is the only valid alternative separator.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Azure App Service's Connection Strings tab (separate from App Settings) maps named strings to `ConnectionStrings:*` automatically — a connection string named `DefaultConnection` in the portal becomes `ConnectionStrings:DefaultConnection` in `IConfiguration` with no `--` mapping and the value masked in the portal UI.

</details>

---

**B7.** [HIGH] [OUT-OF-SCOPE] Ten minutes after rotating `StripeSettings:SecretKey` in Key Vault (with `ReloadInterval = 5 min`), a singleton `PaymentService` still charges with the old key. Walk through `IOptions<T>` vs `IOptionsSnapshot<T>` vs `IOptionsMonitor<T>` and name which one fixes it.

<details>
<summary>The 5% Answer</summary>

| Interface | Lifetime | Refreshes? |
|---|---|---|
| `IOptions<T>` | Singleton | **No** — captured once at first resolution |
| `IOptionsSnapshot<T>` | Scoped | Yes — recomputed per request |
| `IOptionsMonitor<T>` | Singleton | Yes — `.CurrentValue` reads latest on every call |

- **Root cause:** `PaymentService` is singleton; injects `IOptions<StripeSettings>`. The `SecretKey` string is copied into the instance at construction and reused forever regardless of Key Vault reload.
- **Fix — `IOptionsMonitor<T>.CurrentValue`:**

```csharp
public async Task Charge(decimal amount) {
    var client = new StripeClient(_stripe.CurrentValue.SecretKey); // always latest
}
```

- **Why `IOptionsSnapshot<T>` crashes in a singleton:** `IOptionsSnapshot<T>` is **scoped** (one instance per request). A singleton is resolved once and held forever. Injecting a scoped service into a singleton = **captive dependency** — the scoped instance is never released or replaced. ASP.NET Core detects this at startup (`ValidateScopes = true` in Development) and throws `InvalidOperationException` before the app starts.

- **Production consequence of using `IOptions<T>` with Key Vault reload:** `IOptions<T>` captures the config value once at construction. Key Vault reload updates `IConfiguration` in the background — but `IOptions<T>` never re-reads it. Result: the singleton holds `sk_live_OLD_KEY` forever. Charges fail with "Invalid API Key" while `IConfiguration["Stripe:SecretKey"]` already shows the new key. Fix: use `IOptionsMonitor<T>.CurrentValue` — it reads the latest value on every call.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Use `IConfiguration.GetValue<string>` directly — it reads fresh every time." Bypasses strong typing, loses `DataAnnotations` validation, and couples business logic directly to the config framework.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** `IOptionsMonitor<T>.OnChange` fires on the background polling thread — expensive handlers (rebuilding a Stripe SDK client, reconnecting a long-lived token) should dispatch to a task queue or the ThreadPool, otherwise the reload loop slows down and reduces freshness across every consumer.

</details>

---

### HARD

**B8.** [HIGH] A key exists in `appsettings.json`, `appsettings.Production.json`, an Azure App Setting (env var), AND Key Vault. Which value does the running app use in production? What breaks if Key Vault is registered before environment variables?

<details>
<summary>The 5% Answer</summary>

**Key Vault wins** — registered last = highest priority.

1. `appsettings.json` → overridden by
2. `appsettings.Production.json` → overridden by
3. Environment variables (Azure App Settings) → overridden by
4. **Key Vault** ← final value

**If Key Vault is registered before environment variables:**

```csharp
builder.Configuration.AddAzureKeyVault(...);     // registered first = lower priority
builder.Configuration.AddEnvironmentVariables(); // registered last = overrides Key Vault
```

- Any Azure App Setting for the same key now silently overrides the Key Vault value.
- A debug env var set months ago in the portal permanently masks the rotated Key Vault secret.
- **Correct order:** `AddEnvironmentVariables()` before `AddAzureKeyVault(...)`.

**Production consequence:** wrong registration order means a stale App Setting set once for debugging overrides every subsequent Key Vault rotation indefinitely.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Key Vault always wins — it's a managed service so it has higher priority." Priority is determined entirely by registration order in code, not by the nature of the service.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** `ConfigurationManager` in .NET 6+ is simultaneously a builder and a live `IConfiguration` — call `builder.Configuration["KeyVaultUrl"]` after `AddEnvironmentVariables()` but before `AddAzureKeyVault()` to read the URL from env vars and bootstrap the Key Vault connection. This is the correct solution to the "Key Vault URL can't be in Key Vault" bootstrap problem.

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
}
// Second deployment: user exists → block skipped → password never read
```

- **Why deletable after first deploy:** `CreateAsync` stores a **bcrypt hash** — the plaintext is never needed again. Identity hashes the login input and compares hashes; it never decrypts the stored value.
- **If deleted before first deploy:** `config["AdminSettings:Password"]` returns `null` → `?? throw` fires → app crashes at startup → Azure App Service shows "Application Error" but the deployment pipeline reports success.

**Production consequence:** a missing seed secret before first deploy causes a startup crash that the deployment pipeline cannot detect — a startup health check is the only automated catch.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Hardcode a strong default admin password in seed." That password is in git history forever, visible to every current and future contributor and anyone who forks the repo.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** The production-grade pattern is a **one-time temporary credential** — seed with a random password and force a reset on first admin login via `IEmailSender` confirmation. The seed password becomes a single-use bootstrap credential: compromising it grants no persistent access.

</details>

<details>
<summary>Explanation</summary>

## The problem it solves

You need an admin account in the database on first deploy. Where does the password come from?

- Hardcoded in code → sits in git history forever → anyone who clones the repo sees it
- So it must come from outside the code — that's Key Vault

## First deploy — step by step

**1. Before deploy:** put the password in Key Vault as `AdminSettings--Password`.

**2. App starts up:** `AddAzureKeyVault` loads it into `IConfiguration`. The app now has `config["AdminSettings:Password"] = "Pa$$w0rd12!"` in memory.

**3. Seeding runs:**
```csharp
if (!await userManager.Users.AnyAsync(x => x.Email == adminEmail))
```
No admin exists yet → enters the block → reads the password → calls `CreateAsync`.

**4. What `CreateAsync` actually stores:** NOT the password. It runs bcrypt on it and stores the hash: `$2a$11$xyz...`. The plaintext is thrown away immediately.

**5. After deploy:** delete the secret from Key Vault. Safe — the app never needs the plaintext again. Login works by hashing what the user types and comparing the two hashes.

## Second deploy — step by step

**1. App starts up:** Key Vault secret is gone. `config["AdminSettings:Password"]` returns `null`.

**2. Seeding runs:** admin already exists → `AnyAsync` returns `true` → **skips the block entirely** → the `?? throw` line is never reached → no crash.

## What goes wrong if you delete the secret too early

If you delete from Key Vault **before** first deploy:

- App starts, seeding runs, admin doesn't exist yet → enters the block
- `config["AdminSettings:Password"]` → `null`
- `?? throw` fires → app crashes at startup
- Azure portal shows "Application Error"
- But CI/CD pipeline already reported "deployment succeeded" — it deployed the files fine, it can't detect a startup crash

## Why `?? throw` instead of just using null

Without it, `null` flows into `CreateAsync(admin, null)` → crashes deep inside Identity with `ArgumentNullException` pointing at some internal method → no indication the root cause was a missing Key Vault secret.

With `?? throw`, the error message tells you exactly what's missing.

**One-liner to remember:** Key Vault holds the password only long enough to seed once. After that, bcrypt owns it and the plaintext can be deleted.

</details>

---

**B10.** [HIGH] Azure Key Vault is unavailable when the app starts. Walk through the exact failure, the exception type, and two production-viable mitigations. What is the silent danger of catching the startup exception, and what does `ValidateOnStart()` add to the picture?

<details>
<summary>The 5% Answer</summary>

**Failure path:** `AddAzureKeyVault` runs during `builder.Build()`. After SDK retries are exhausted:

```
Azure.RequestFailedException: Service request failed.
Status: 0 (SocketException — no route to host)
```

`builder.Build()` propagates → app crashes → deployment reports success but app never serves traffic.

- **Mitigation 1 — SDK retries (free baseline):** Azure SDK retries 3× with exponential backoff. Transient outages under ~15 s resolve before the final retry — zero code change.
- **Mitigation 2 — catch + log + continue:**

```csharp
try { builder.Configuration.AddAzureKeyVault(...); }
catch (Exception ex) {
    logger.LogCritical(ex, "Key Vault unreachable — falling back to App Service settings");
}
```

- **Silent danger of catching:** app starts with no Stripe key; payments throw `NullReferenceException` inside business logic; health endpoint returns 200. The app looks healthy while checkout is entirely broken.
- **`ValidateOnStart()` fix:**

```csharp
services.AddOptions<StripeSettings>()
    .BindConfiguration("StripeSettings")
    .ValidateDataAnnotations().ValidateOnStart();
```

Throws `OptionsValidationException` with a named field error at startup — explicit crash with a clear message instead of a silent null mid-payment.

**Production consequence:** catching Key Vault failure without `ValidateOnStart` produces a running process with broken payments — metrics green, business function down.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Set up a circuit breaker to skip Key Vault if it's down." Circuit breakers handle runtime outages; at startup, missing secrets mean the app cannot function — starting broken and serving payment errors is worse than crashing with a clear error.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Azure App Configuration with Key Vault references caches config locally and has built-in fallback to last-known-good state when Key Vault is unreachable — it absorbs Key Vault availability issues transparently for high-uptime requirements, with no code change required in the app.

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

**Bug 1 — registration order inverted:** `AddEnvironmentVariables()` registered after `AddAzureKeyVault` → env vars override Key Vault. A debug App Setting set months ago silently masks every subsequent Key Vault rotation.

**Bug 2 — no development guard:** every `dotnet run` attempts a Key Vault connection. A new dev without `az login` waits 10–15 s for `DefaultAzureCredential` timeout, then crashes with `CredentialUnavailableException`. User Secrets are never consulted because startup died first.

**Bug 3 — no null guard on required secret:** `config["AdminSettings:Password"]` returns `null` if missing → flows silently into `UserManager.CreateAsync(admin, null)` → `ArgumentNullException` deep in seeding with no indication the root cause was a missing Key Vault entry.

```csharp
builder.Configuration.AddEnvironmentVariables();      // fix 1: env vars first
if (!builder.Environment.IsDevelopment())             // fix 2: dev guard
    builder.Configuration.AddAzureKeyVault(
        new Uri(builder.Configuration["KeyVaultUrl"]!), new DefaultAzureCredential());
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("StripeSettings"));
var adminPassword = builder.Configuration["AdminSettings:Password"]
    ?? throw new InvalidOperationException("AdminSettings:Password not configured"); // fix 3
```

**Production consequence:** Bug 1 silently flips to env-var-wins in production, Bug 2 blocks every new developer, Bug 3 turns a missing Key Vault secret into a misleading `ArgumentNullException` deep in seeding logic. Each bug breaks a different environment simultaneously.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Only spotting Bug 1. The registration order is the textbook catch; the development guard (Bug 2) and the `?? throw` pattern (Bug 3) separate someone who read the docs from someone who has been paged for a broken deployment.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Replacing `Configure<T>` + `?? throw` with `services.AddOptions<AdminSettings>().BindConfiguration("AdminSettings").ValidateDataAnnotations().ValidateOnStart()` and `[Required]` on the DTO — fails the host build with a structured `OptionsValidationException` listing every missing field at once, not a whack-a-mole of one `?? throw` per key.

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

**Red Flag:** Using `ConnectionStrings_Redis` (single underscore) — maps to the literal key `ConnectionStrings_Redis`, not `ConnectionStrings:Redis`. The app silently gets `null` until the first Redis call fails at runtime.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Azure App Service automatically prefixes SQL connection strings with `SQLCONNSTR_` — a connection string named `DefaultConnection` in the portal becomes `ConnectionStrings:DefaultConnection` in `IConfiguration` without any `__` mapping needed.

</details>

---

**B-QF2.** [HIGH] What does `config["AdminSettings:Password"] ?? throw new Exception("Not configured")` do, and what's the idiomatic alternative for validating an entire settings section?

<details>
<summary>The 5% Answer</summary>

- Reads the config value and throws **immediately at startup** if `null` — fails fast with a clear message instead of a `NullReferenceException` deep in business logic.
- **Idiomatic alternative for a full section:**

```csharp
services.AddOptions<AdminSettings>()
    .BindConfiguration("AdminSettings")
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

Throws `OptionsValidationException` listing every `[Required]` field that's missing — validates the whole section at host build, not one key at a time.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Assuming `config.GetValue<string>("key")` throws for missing keys — it returns `null`. `GetRequiredSection` throws for a missing section, not a missing leaf value.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** `ValidateOnStart` combined with `[Required]` attributes catches all missing secrets in one startup failure — useful when Key Vault has a partial outage and only some secrets were loaded; the app fails loudly instead of discovering missing keys one payment attempt at a time.

</details>

---

**B-QF3.** [HIGH] What is the difference between `throw` and `throw ex` in a catch block?

<details>
<summary>The 5% Answer</summary>

- `throw` re-throws the original exception — **preserves the full stack trace**.
- `throw ex` re-throws it as a new throw point — **resets the stack trace**, losing the original call site.

```csharp
catch (Exception ex)
{
    throw;    // ✓ original stack trace intact
    throw ex; // ✗ stack trace reset — root cause hidden in logs
}
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "They're the same — both re-throw the caught exception." `throw ex` sets the origin to the catch block; the real fault location disappears from logs and exception messages.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** The only correct use of `throw ex` is when wrapping: `throw new ApplicationException("Context", ex)` — the original exception is preserved as `InnerException` and the outer exception adds diagnostic context. Bare `throw ex` has no legitimate use case.

</details>
