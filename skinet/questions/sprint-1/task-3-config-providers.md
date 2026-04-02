claude --resume deff6f44-f61f-4683-84bc-1d3cdc62ff51

# Task 3 — Config Provider Chain & Secrets Management

## Why this approach

.NET's configuration system is layered. Each provider can override the previous one. This lets you keep safe defaults in `appsettings.json` and override secrets per environment without touching code.

Provider load order (last one wins):
1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. User Secrets *(Development only — injected automatically by the SDK)*
4. Environment variables
5. Azure Key Vault *(added manually in Program.cs for non-Development)*

In prod: Key Vault overrides everything. In dev: User Secrets override `appsettings.Development.json`.

## What confused me

**Why not just use environment variables for everything?**  
You can, but Key Vault centralises rotation, audit logs, and access control. One place to revoke a secret.

**Why is `KeyVaultUrl` not itself in Key Vault?**  
Chicken-and-egg: you need the URL to connect to Key Vault. It's not a credential — it's a public endpoint address.

**Why `--` in Key Vault secret names?**  
Azure Key Vault doesn't allow `:` in secret names. The .NET provider maps `ConnectionStrings--Redis` → `ConnectionStrings:Redis` automatically.

**Why is `PublishableKey` still in appsettings.json?**  
It's intentionally public (sent to browsers). Putting it in Key Vault would be wasteful and wrong.

**Why is config split across Key Vault, Connection Strings tab, and App Settings?**  
Each location has a different purpose:

| Where | What | Why |
|---|---|---|
| Key Vault | Stripe keys | True secrets — need audit trail, rotation, vault-level protection |
| Connection Strings tab | Redis, SQL Server | Azure treats these specially — masked in portal, type-aware |
| App Settings | `KeyVaultUrl`, AppInsights | Non-sensitive config — needed to bootstrap everything else |

Connection strings don't go in Key Vault because Azure's Connection Strings tab already masks them and maps them correctly. Putting them in both places creates duplication with no benefit.

## Explanation

### Dev flow (User Secrets)

```
dotnet user-secrets init          # adds UserSecretsId to .csproj
dotnet user-secrets set "Key" "Value"
```

Secrets stored at: `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`  
Never in the project directory → never committed to git.

### Prod flow (Azure Key Vault)

```csharp
// Program.cs — runs only in non-Development
var keyVaultUrl = builder.Configuration["KeyVaultUrl"]
    ?? throw new InvalidOperationException("KeyVaultUrl app setting is missing.");
builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), new DefaultAzureCredential());
```

`DefaultAzureCredential` tries auth methods in order:
1. `AZURE_*` environment variables
2. Workload identity
3. **Managed Identity** ← what fires in Azure App Service
4. Visual Studio / VS Code credential
5. **Azure CLI** ← what fires locally if you run `az login`

In Azure App Service: enable System Managed Identity → grant it **Key Vault Secrets User** role on the vault.

### Key Vault secret naming

| Config key | Key Vault secret name |
|---|---|
| `ConnectionStrings:Redis` | `ConnectionStrings--Redis` |
| `StripeSettings:SecretKey` | `StripeSettings--SecretKey` |

### What stays in appsettings.json

- Logging config
- `KeyVaultUrl` (not a credential)
- `StripeSettings:PublishableKey` (intentionally public)

## Key flows

### Config provider chain (last wins)

```
Dev:
appsettings.json → appsettings.Development.json → User Secrets
                                                        ↑
                                          StripeSettings:SecretKey
                                          StripeSettings:WhSecret

Prod:
appsettings.json → Environment variables → Azure Key Vault
                                                  ↑
                                     ConnectionStrings:Redis
                                     ConnectionStrings:SqlServer
                                     StripeSettings:SecretKey
                                     StripeSettings:WhSecret
```

`builder.Configuration["key"]` returns the last provider that has the key. One call, source is transparent.

---

### Stripe 3-key flow

```
Browser                         Stripe servers            Your server
  |                                    |                       |
  |── card data ──────────────────────>|                       |
  |<── token (safe) ──────────────────|                       |
  |                                    |                       |
  |── token ──────────────────────────────────────────────────>|
  |                              SecretKey + token ────────────>|
  |                              payment confirmed <────────────|
  |                                    |                       |
  |                         webhook: "payment_succeeded" ──────>|
  |                                    |   verify WhSecret sig  |
  |                                    |   → trust event        |
```

- **PublishableKey** — browser authenticates to Stripe to tokenize card
- **SecretKey** — server charges the token (never in browser)
- **WhSecret** — server verifies webhook signature (proves event is from Stripe)

## Exercise

*(Created after sprint end)*

## Solution

*(Created after sprint end)*

## Interview Q&A

**Q: How do you manage secrets in a .NET app?**  
A: Dev — User Secrets (never committed). Prod — Azure Key Vault via `AddAzureKeyVault` + `DefaultAzureCredential`. The config provider chain means Key Vault values override everything else. No secrets ever touch `appsettings.json`.

**Q: What is `DefaultAzureCredential`?**  
A: A credential chain that tries multiple auth methods in order. In Azure App Service it automatically uses Managed Identity. Locally it falls back to Azure CLI. You write one line of code that works in both environments.

**Q: Why are secrets in git history still a problem after this change?**  
A: The change stops future leaks, but old commits still contain the keys. The fix is to rotate (regenerate) every compromised key after the new config is deployed and verified working.

**Q: What's the difference between User Secrets and environment variables?**  
A: Both are local overrides. User Secrets are project-scoped (per UserSecretsId), stored outside the repo. Env vars are machine-scoped. User Secrets are the .NET-idiomatic choice for dev because they don't pollute the shell environment.
