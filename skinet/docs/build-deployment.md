# Build & Deployment

Docker Compose for dev dependencies. Angular `ng build` output served by .NET.

## Docker Compose (dev)

```bash
docker compose up -d
```

Starts:
- `redis` (port `6379`) — cart + response cache
- `sqlite3` — data persisted to `./data`

## Development

```bash
# Backend
dotnet run --project src/API

# Frontend (separate terminal)
cd client && ng serve
```

Backend on `https://localhost:5001`. Angular dev server proxies API calls.

## Production Build

```bash
cd client && ng build
```

Output goes to `wwwroot/browser`. Served by .NET via `UseStaticFiles` + `FallbackController`.

## SQLite → SqlServer Switch

Set `ASPNETCORE_ENVIRONMENT=Production`. Ensure `ConnectionStrings:DefaultConnection` points to SqlServer.
Run SqlServer migrations before first deploy:

```bash
dotnet ef database update --context SqlServerStoreContext
```

## Secrets Management

| Secret | Dev | Prod |
|---|---|---|
| `StripeSettings:SecretKey` | User Secrets | Azure Key Vault |
| `StripeSettings:WhSecret` | User Secrets (update each Stripe CLI session) | Azure Key Vault |
| `ConnectionStrings:Redis` | `appsettings.Development.json` (localhost, no password) | Azure Key Vault |
| `ConnectionStrings:SqlServerAzureConnection` | not used in dev | Azure Key Vault |
| `StripeSettings:PublishableKey` | `appsettings.json` | `appsettings.json` |

Key Vault URL stored in `appsettings.json` as `KeyVaultUrl` (not a credential).
App Service reads Key Vault via System Managed Identity — no credentials stored anywhere.

### Dev setup (first time)
```bash
cd src/API
dotnet user-secrets set "StripeSettings:SecretKey" "<sk_test_...>"
dotnet user-secrets set "StripeSettings:WhSecret" "<whsec_...>"
```

### Update WhSecret each Stripe CLI session
```bash
stripe listen --forward-to https://localhost:7096/api/payments/webhook
dotnet user-secrets set "StripeSettings:WhSecret" "<new-whsec-from-cli>"
```

## Rules / Constraints

- SQLite for Development, SqlServer for Production — set by `ASPNETCORE_ENVIRONMENT`.
- Never put secrets in `appsettings.json` or `appsettings.Development.json`.
