# Task 3 — Config Provider Chain & Secrets Management

## Plan

- Move Stripe keys + connection strings out of `appsettings.json`
- Dev: User Secrets via `dotnet user-secrets set`
- Prod: Azure Key Vault via `AddAzureKeyVault` + `DefaultAzureCredential`
- Deploy and verify before rotating any keys (keys are in git history)

## Todo

- [x] Init User Secrets on API project
- [x] Move `StripeSettings:SecretKey`, `StripeSettings:WhSecret` to User Secrets (dev)
- [x] Create Azure Key Vault, add secrets with `--` separator naming
- [x] Add `AddAzureKeyVault` to `Program.cs` (non-Development only)
- [x] Enable Managed Identity on App Service, grant Key Vault Secrets User role
- [x] Verify Azure deploy works, then rotate any keys that were in git history

---

## Why this approach

.NET's configuration system is layered — each provider overrides the previous one. This lets
you keep safe defaults in `appsettings.json` and override secrets per environment without
touching code. `IConfiguration["key"]` returns the final merged value; the caller doesn't
know or care which source provided it.

Provider load order (last one wins):
1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. User Secrets *(Development only)*
4. Environment variables
5. Azure Key Vault *(added manually — prod only)*

## What confused me

**Why not just use environment variables for everything?**
You can, but Key Vault centralises rotation, audit logs, and access control. One place to
revoke a secret across all deployments.

**Why is `KeyVaultUrl` not itself in Key Vault?**
Chicken-and-egg: you need the URL to connect to Key Vault. It's not a credential — it's a
public endpoint address. It goes in App Settings.

**Why `--` in Key Vault secret names?**
Azure Key Vault doesn't allow `:` in secret names. The .NET provider maps
`ConnectionStrings--Redis` → `ConnectionStrings:Redis` automatically.

**Why is `PublishableKey` still in `appsettings.json`?**
It's intentionally public — sent to browsers by Stripe. Putting it in Key Vault would be
wasteful and wrong.

**Why is config split across Key Vault, Connection Strings tab, and App Settings?**

| Where | What | Why |
|---|---|---|
| Key Vault | Stripe keys | True secrets — audit trail, rotation, vault-level protection |
| Connection Strings tab | Redis, SQL Server | Azure masks these in portal, type-aware |
| App Settings | `KeyVaultUrl` | Non-sensitive — needed to bootstrap everything else |

## Explanation

### Dev flow (User Secrets)

```
dotnet user-secrets init          # adds UserSecretsId to .csproj
dotnet user-secrets set "Key" "Value"
```

Stored at: `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`
Never in the project directory → never committed to git.

### Prod flow (Azure Key Vault)

```csharp
// Program.cs — runs only in non-Development
builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), new DefaultAzureCredential());
```

`DefaultAzureCredential` tries auth methods in order:
1. `AZURE_*` environment variables
2. Workload identity
3. **Managed Identity** ← fires in Azure App Service
4. Visual Studio / VS Code credential
5. **Azure CLI** ← fires locally if you run `az login`

### Key Vault secret naming

| Config key | Key Vault secret name |
|---|---|
| `StripeSettings:SecretKey` | `StripeSettings--SecretKey` |
| `StripeSettings:WhSecret` | `StripeSettings--WhSecret` |
| `AdminSettings:Password` | `AdminSettings--Password` |

### Stripe 3-key flow

```
Browser                         Stripe servers            Your server
  |                                    |                       |
  |── card data ──────────────────────>|                       |
  |<── token (safe) ──────────────────|                       |
  |── token ──────────────────────────────────────────────────>|
  |                              SecretKey + token ────────────>|
  |                              payment confirmed <────────────|
  |                         webhook: "payment_succeeded" ──────>|
  |                                    |   verify WhSecret sig  |
```

- **PublishableKey** — browser authenticates to Stripe to tokenize card
- **SecretKey** — server charges the token (never in browser)
- **WhSecret** — server verifies webhook signature (proves event is from Stripe)

## Exercise

1. A new developer clones the repo and runs the app locally. They get `StripeSettings:SecretKey is not configured`. What do they need to do and why can't you just commit the key to `appsettings.Development.json`?
2. You rotate the Stripe secret key in Key Vault. Do you need to restart the App Service? Why?
3. A key exists in both `appsettings.json` and User Secrets. Which value does `IConfiguration["key"]` return?

## Solution

1. They need to run `dotnet user-secrets set "StripeSettings:SecretKey" "sk_test_..."`. You can't commit it to `appsettings.Development.json` because that file is tracked by git — the secret would be in the repo and in git history for anyone who clones it.
2. Yes — `AddAzureKeyVault` reads secrets once at startup and caches them in the config pipeline. A new secret value isn't picked up until the app restarts (or you add a reload mechanism).
3. User Secrets wins — it's registered later in the chain and later registrations override earlier ones.

## Interview Q&A

**Q: How do you manage secrets in a .NET app?**
A: Dev — User Secrets (never committed, stored outside the repo). Prod — Azure Key Vault via
`AddAzureKeyVault` + `DefaultAzureCredential`. The config provider chain means Key Vault
values override everything else. No secrets ever touch `appsettings.json`.

**Q: What is `DefaultAzureCredential`?**
A: A credential chain that tries multiple auth methods in order. In Azure App Service it
automatically uses Managed Identity. Locally it falls back to Azure CLI. You write one line
of code that works in both environments.

**Q: Why are secrets in git history still a problem after this change?**
A: The change stops future leaks, but old commits still contain the keys. Fix: rotate
(regenerate) every compromised key after the new config is deployed and verified working.

**Q: What's the difference between User Secrets and environment variables?**
A: Both are local overrides. User Secrets are project-scoped (per `UserSecretsId`), stored
outside the repo at `%APPDATA%\Microsoft\UserSecrets\`. Env vars are machine-scoped. User
Secrets are the .NET-idiomatic choice for dev because they don't pollute the shell environment.

**Q: Why does Key Vault use `--` instead of `:` in secret names?**
A: Key Vault doesn't allow `:` in secret names. The `AddAzureKeyVault` provider maps `--`
back to `:` automatically, so `StripeSettings--SecretKey` in Key Vault becomes
`StripeSettings:SecretKey` in `IConfiguration`.

---

claude --resume deff6f44-f61f-4683-84bc-1d3cdc62ff51
