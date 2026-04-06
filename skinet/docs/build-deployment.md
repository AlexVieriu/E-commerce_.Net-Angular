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
| `AdminSettings:Password` | User Secrets | Azure Key Vault (`AdminSettings--Password`) |

Key Vault URL stored in `appsettings.json` as `KeyVaultUrl` (not a credential).
App Service reads Key Vault via System Managed Identity — no credentials stored anywhere.

### Azure config map

| Where | What lives there |
|---|---|
| **Key Vault** `kv-skinet-test` | `StripeSettings--SecretKey`, `StripeSettings--WhSecret`, `StripeSettings--PublishableKey`, `AdminSettings--Password` |
| **App Service → Connection Strings** | `Redis` (Custom), `SqlServerAzureConnection` (SQLServer) |
| **App Service → App Settings** | `KeyVaultUrl`, AppInsights vars (Azure-managed) |
| **`appsettings.json`** | `KeyVaultUrl`, Logging config only — nothing sensitive |

### Azure roles

| Identity | Resource | Role | Purpose |
|---|---|---|---|
| Your user | Key Vault | Secrets Officer | Manage secrets manually |
| `skinet-2025-test` (managed identity) | Key Vault | Secrets User | App reads secrets at runtime |
| `oidc-msi-aef5` | App Service | Website Contributor | CI/CD deploys code |

- Permission model: **Vault access policy** (not Azure RBAC)
- If Key Vault is unavailable at startup, app falls back to App Service settings (`try/catch` in `Program.cs`)

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
- `UseStaticFiles()` must come BEFORE `UseRateLimiter()` — otherwise Angular chunks get throttled and return 429.
- Seeding runs in all environments; idempotent by design — checks for existing data before inserting.

## Security Headers (NetEscapades.AspNetCore.SecurityHeaders)

Added via `UseSecurityHeaders()` in `Program.cs`. Applies to all responses.

### What is CSP?

Content-Security-Policy is an HTTP header that tells the browser which sources it is allowed to load resources from. It is the main defense against XSS: even if an attacker injects a `<script>` tag, the browser refuses to execute it if the source is not in the whitelist.

### Directives

| Directive | Value | Why |
|---|---|---|
| `default-src` | `'self'` | Fallback — block everything not explicitly listed |
| `style-src` | `'self' 'unsafe-inline'` | Angular Material injects styles into the DOM at runtime |
| `script-src` | `'self' 'unsafe-inline'` | Angular bootstrap uses inline scripts |
| `img-src` | `'self' data: https:` | `data:` for Material icons; `https:` for product images from external URLs |
| `font-src` | `'self' data:` | Material embeds fonts as data URIs |
| `connect-src` | `'self' wss: https:` | API calls (`self`), SignalR WebSocket (`wss:`), Stripe SDK (`https:`) |

### Permissions-Policy

Separate header that disables browser hardware/sensor APIs you don't use.
`accelerometer=()` disables the Device Accelerometer API — a browser API that exposes physical motion/tilt data from the device sensor. A shop app has no use for it; disabling it prevents any injected third-party script from silently reading it.

| Header | Value |
|---|---|
| `X-Content-Type-Options` | `nosniff` — browser won't MIME-sniff responses |
| `X-Frame-Options` | `DENY` — blocks clickjacking via iframes |
| `Referrer-Policy` | `strict-origin-when-cross-origin` — limits referrer data sent to third parties |
| `Permissions-Policy` | `accelerometer=()` — disables accelerometer sensor API |

## CORS Policy (`CorsPolicy`)

Named policy applied via `UseCors("CorsPolicy")`.

- Allowed origins: `localhost:4200` (http+https), Azure app URL
- Allowed headers: `Content-Type`, `Accept`, `X-Requested-With` (SignalR negotiate)
- Allowed methods: `GET`, `POST`, `PUT`, `DELETE`, `OPTIONS`
- Credentials: allowed (required for cookie auth)
