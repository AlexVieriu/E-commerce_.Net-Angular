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

Backend on `https://localhost:7096`. Angular dev server runs on `https://localhost:4200` and calls the API by absolute URL from `environment.development.ts` — no proxy; the API's `CorsPolicy` allows `localhost:4200`.

## Manual API testing (Scalar)

Dev only: **`https://localhost:7096/scalar/v1`** — interactive OpenAPI UI for hitting endpoints by hand (preferred over curl/PowerShell). For auth-gated routes, run `POST /login` with the `useCookies` query param enabled to set the auth cookie, then call the protected endpoint in the same session (the browser carries the cookie automatically — no token to copy). Admin: `admin@test.com` / user-secret `AdminSettings:Password`.

## Lint & Analyzers (the quality gate)

Both halves are **strict** — a violation fails the PR. Added in T54.

| | Frontend | Backend |
|---|---|---|
| Tool | ESLint (`angular-eslint`) + Prettier | Roslyn analyzers (built into the SDK) |
| Config | `client/eslint.config.js`, `.prettierrc.json` | `Directory.Build.props`, `.editorconfig` |
| Run it | `npm run lint` · `npm run format` | `dotnet build -c Release` |
| Gate | CI runs `lint` + `format:check` | `TreatWarningsAsErrors` — **Release only** |

**Why Release only:** CI builds Release, so a Roslyn warning fails the PR there — while your local
Debug `dotnet run` stays warning-only and doesn't block you mid-edit.

**`NoWarn` must append, never assign.** `<NoWarn>CS8618</NoWarn>` in a `.csproj` *overwrites* what
`Directory.Build.props` set. Always `<NoWarn>$(NoWarn);CS8618</NoWarn>`.

**`EnforceCodeStyleInBuild` is currently near-inert.** It runs the `IDExxxx` *style* rules at build, but
an IDE rule only surfaces if `.editorconfig` raises its severity to `warning`/`error` — and ours sets
none. It's future-proofing, not enforcement. Turning style rules on is a separate decision (it would
surface a fresh batch of violations).

**Suppressions, and why they exist** (each is written down at the code, not hidden in config):

| Rule | Where | Why |
|---|---|---|
| `CA1707` | `tests/` only | Test names are `Method_Scenario_Expected` — the underscores *are* the convention |
| `CA1304/1311/1862` | `ProductSpecification` | It's an **EF expression tree** → runs as SQL. EF **throws** on `StringComparison` overloads |
| `CA1716` | `ISpecification.Select` | Reserved word in VB/F# only; this solution is C#-only |
| `consistent-type-definitions` | `eslint.config.js` | Models stay `type` — `interface` allows declaration merging (see `actions/create-model.md`) |
| all | `**/Migrations/**` | EF-generated; marked `generated_code = true` in `.editorconfig` |

**Not suppressed — fixed:** `CA1848` (use `LoggerMessage` delegates). Its docs say *"Do not suppress a
warning from this rule"*, and there were only 5 call sites, so `PaymentsController` and
`ExceptionMiddleware` use source-generated `[LoggerMessage]` partial methods instead. The template is
parsed once at startup rather than on every call, and args aren't boxed. Both classes must be `partial`.

## Production Build

```bash
cd client && ng build
```

Output goes to `src/API/wwwroot` (set by `angular.json` `outputPath`). Served same-origin by .NET via `UseStaticFiles` + `FallbackController`, so prod uses the relative `apiUrl: 'api/'` from `environment.ts`.

## Production environment (live)

Deployed to App Service **`skinet-2025-test`** (RG `skinet-2025-rg-test`, Poland Central). Push to `main` → GitHub Actions builds + deploys → live on the `…azurewebsites.net` URL.

| Concern | Reality |
|---|---|
| **Database** | Serverless Azure SQL `skinet-test` (free tier). **Auto-pauses when idle** — the first request after idle resumes it (~30–60s). `UseSqlServer` has `EnableRetryOnFailure` so startup survives the transient `40613` instead of 500.30 (BUG-8). |
| **Redis** | **Upstash** (serverless). In App Service → Connection strings as `Redis` (Custom): `host:6379,password=…,ssl=True,abortConnect=False`. **Not** in Key Vault. |
| **Admin user** | `admin@test.com`, password from Key Vault `AdminSettings:Password`, **re-synced on every startup** — rotate the secret + restart to change it (BUG-9). |
| **Security headers** | CSP allows `js.stripe.com` (script/frame), `hooks.stripe.com` (frame), `api.stripe.com` (connect); COEP overridden to `unsafe-none` so Stripe Elements load (BUG-7). Fonts are self-hosted, not the Google CDN (BUG-6). |
| **Stripe webhook** | Stripe Dashboard endpoint → `…azurewebsites.net/api/payments/webhook`; signing secret = Key Vault `StripeSettings:WhSecret`. |

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
| `ConnectionStrings:Redis` | `appsettings.Development.json` (localhost, no password) | App Service → Connection strings (Upstash) |
| `ConnectionStrings:SqlServerAzureConnection` | not used in dev | App Service → Connection strings (SQLServer) |
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

- Permission model: **Azure RBAC** (`Key Vault Secrets User` role on the managed identity), not vault access policy
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
- Baseline seeding (admin + JSON products) runs in all environments; idempotent — checks for existing data first. Bulk demo data (`BogusSeeder`) is opt-in via `dotnet run -- --seed` and Development-only. See `docs/backend/data-layer.md`.

## Dependency upgrades

Routine for most packages. **`Stripe.net` is the exception — a major bump is an API migration, not a version bump.**

Stripe's policy: *"Each SDK version uses the API version that is current at the time of its release"*
([sdks/versioning](https://docs.stripe.com/sdks/versioning)) — and strongly-typed SDKs (.NET/Java/Go)
*cannot* target a different version, so the SDK version **is** the wire version. Never let it ride a blanket
"latest GA" refresh: read the changelog, then run the coupon + payment smoke in `testing.md`, because
**the test suite cannot see these breaks**. BUG-20 is the worked example — `fix(deps)` #50 bumped 51→52 and
silently killed every coupon for two months.

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
- Allowed headers: any (`AllowAnyHeader`) — SignalR sends multiple custom headers; origin whitelist is the real protection
- Allowed methods: `GET`, `POST`, `PUT`, `DELETE`, `OPTIONS`
- Credentials: allowed (required for cookie auth)
