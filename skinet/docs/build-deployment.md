# Build & Deployment

Docker Compose for dev dependencies. Angular `ng build` output served by .NET.

## Docker Compose (dev)

**Start dependencies before `dotnet run` or `dotnet test`** — on a clean machine the tests fail with no hint
why if Redis is not up.

```bash
cp .env.example .env      # then set MSSQL_SA_PASSWORD
docker compose up -d
```

Starts:
- `redis` (port `6379`) — cart + response cache
- `sqlite` — data persisted to `./data` (the container is named `sqlite3`)
- `sqlserver` (port `1433`) — SQL Server 2025, **Developer** edition

`MSSQL_SA_PASSWORD` has no default: compose fails loudly if `.env` is missing, rather than booting on a
weak password. `.env` is git-ignored; `.env.example` is the committed template.

### Why a SQL Server container when dev runs on SQLite

Dev uses SQLite and everything else uses SQL Server (`AddApplicationServices`), so **`dotnet
run` never exercises the SqlServer migrations** — they would first execute against Azure on deploy. The
container is where you apply and verify them, and where query plans can be read on the same engine family
production uses. LocalDB also works but is Express edition (smaller buffer pool, fewer cores).

Point EF at it without touching code — `SqlServerContextFactory` already reads environment variables:

```bash
export ConnectionStrings__SqlServerAzureConnection="Server=localhost,1433;Database=skinet;User Id=sa;Password=<from .env>;TrustServerCertificate=True"
dotnet ef database update --context SqlServerStoreContext
```

The image is ~1.5 GB and there is no smaller option: every tag on `mcr.microsoft.com/mssql/server` is
Ubuntu-based (SQL Server is closed source, built against glibc), and Azure SQL Edge — the small variant —
was retired 2025-09-30.

## Development

```bash
# Backend
dotnet run --project src/API

# Frontend (separate terminal)
cd client && ng serve
```

Backend on `https://localhost:7096`, pinned by `applicationUrl` in `launchSettings.json` (added Aug 21 — before that nothing set it, so a fresh clone got Kestrel's defaults and the client could not reach the API). **The port is not free to change:** `environment.development.ts` hardcodes `https://localhost:7096` for both `apiUrl` and `hubUrl`. Angular dev server runs on `https://localhost:4200` and calls the API by absolute URL — no proxy; the API's `CorsPolicy` allows `localhost:4200`.

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

### Free-tier budget for testing on live

The database is billed in vCore-seconds, and the unit that matters is a **wake**:

- 100,000 vCore-seconds free per month.
- While awake it bills 0.5 vCore, even idle, and it sleeps only after **60 idle minutes**.
- So one wake costs ~1,800 vCore-seconds — **~55 wakes a month, ~1.8 a day** — whether the visit lasted
  two minutes or forty-five.
- Run out and the database is parked **until the 1st of next month**; the live site fails until then.

**The 60-minute delay cannot be lowered.** `az sql db update ... --auto-pause-delay 15` is refused:

```
(ProvisioningDisabled) Only default value for auto pause delay is allowed
for Free Limit database with auto pause exhaustion behavior
```

Not in the docs — only the API says it (checked 2026-09-17). Unlocking it means switching to *"Continue
using database for additional charges"*, which cannot be reverted and removes the spending cap.

**So check the live site in one sitting.** Three ad-hoc visits cost three wakes; one 45-minute session
costs one. Exploratory work goes on local SQLite. Anything on a timer that opens a connection — a health
probe, a scheduled job — burns the month in ~2.3 days.

Watch the *Free amount remaining* metric on the database; alert below 10,000.

### Free-tier bandwidth budget

F1 App Service allows **165 MB of outbound traffic a day**, reset at midnight UTC. Past it the app is
**stopped** — every request returns 403 (Forbidden) until the reset, so the live site is down, not slow.

Images are copied into the deployment artifact once, then re-sent on every visit:

```
BUILD TIME — once per deploy, costs no bandwidth
  client/public/images/*  ──ng build──→  src/API/wwwroot/images/*  ──deploy──→  App Service disk

RUN TIME — every visitor, every page load
  browser  ──GET /images/products/boot-ang1.png──→  App Service
           ←──────────── 83 KB ───────────────────
  ↑ this is what spends the 165 MB/day
```

The copy is free and happens once. The **serving** is the cost — the same bytes go out again for every
visitor, because the browser, not the build, is what pulls them.

| Draw on the quota | Cost |
|---|---|
| One `/shop` page view, cold cache (`pageSize = 10`, 800px WebP) | **1759 KB** — Lighthouse, median of 3, after T35 |
| `/health` at Azure's 60s probe | ~1.4 MB/day (1440 responses, almost all headers) |

So roughly **96 catalogue page views exhaust the day**, and a full day of health probes costs less than
one of them — page weight is the lever, the probe is not.

Static files also carry no `Cache-Control`: `Program.cs` calls `app.UseStaticFiles()` with no options, so
everything ships `ETag`/`Last-Modified` and no `max-age`. That does not mean every repeat visit
revalidates — with a `Last-Modified` and no explicit expiry a browser applies **heuristic freshness**
(RFC 9111 §4.2.2, commonly 10% of the age since that date), so freshness is the browser's guess and a
deploy has no way to invalidate it.

### Health endpoints

Azure's probe cannot be slowed below 60 seconds, so the path it pings must check nothing.

| Path | Checks | Called by | Answer |
|---|---|---|---|
| `/health` | nothing | Azure, every 60s | 200 + `Healthy` (`text/plain`) |
| `/health/ready` | SQL `CanConnectAsync` (10s cap) + Redis `PING` (5s cap) | you, CI, the deploy gate | 200/503 + per-check JSON |

- Liveness opens no connection: **0 wakes**. Nothing pings `/health/ready` — each call is one wake, so
  batch them into one sitting.
- The detail needs `HealthSettings:ReadyKey`, as header `X-Health-Key` or `?key=`. Without it: 401, empty
  body, and the checks never run, so a scanner cannot spend a wake either.
- The query form lands in logs — rotate the key, don't reuse a password.
- Not Identity on that path: it reads `AspNetUsers`, which is gone when SQL is what died. Configuration is
  already in memory.
- F1 only records the metric — no instance is removed or replaced, so the alert on it is the point.
- Changing `healthCheckPath` restarts the app. Settle the path first.
- The pings keep the app loaded, so it stops unloading after 20 idle minutes — and every cold start
  re-runs `SeedDatabaseAsync`, which wakes the database.
- Measured with Redis stopped: `/health` stayed `Healthy`; `/health/ready` gave 503 in 5.4s.

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
| `HealthSettings:ReadyKey` | User Secrets | Azure Key Vault (`HealthSettings--ReadyKey`) |

Key Vault URL stored in `appsettings.json` as `KeyVaultUrl` (not a credential).
App Service reads Key Vault via System Managed Identity — no credentials stored anywhere.

### Azure config map

| Where | What lives there |
|---|---|
| **Key Vault** `kv-skinet-test` | `StripeSettings--SecretKey`, `StripeSettings--WhSecret`, `StripeSettings--PublishableKey`, `AdminSettings--Password`, `HealthSettings--ReadyKey` |
| **App Service → Connection Strings** | `Redis` (Custom), `SqlServerAzureConnection` (SQLServer) |
| **App Service → App Settings** | `KeyVaultUrl`, AppInsights vars (Azure-managed) |
| **App Service → Health check** | Path `/health`, plus an alert rule on the *Health check status* metric |
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
dotnet user-secrets set "HealthSettings:ReadyKey" "<any-string>"
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

Routine for most packages. **Two exceptions:**

**`Stripe.net` — a major bump is an API migration, not a version bump.**
Stripe's policy: *"Each SDK version uses the API version that is current at the time of its release"*
([sdks/versioning](https://docs.stripe.com/sdks/versioning)) — and strongly-typed SDKs (.NET/Java/Go)
*cannot* target a different version, so the SDK version **is** the wire version. Never let it ride a blanket
"latest GA" refresh: read the changelog, then run the coupon + payment smoke in `testing.md`, because
**the test suite cannot see these breaks**. BUG-20 is the worked example — `fix(deps)` #50 bumped 51→52 and
silently killed every coupon for two months.

**`xunit.v3` 4.x + MTP mode** *(2026-10-14, .NET 11 RC2)*. xunit.v3 4.x runs on Microsoft Testing
Platform (MTP), so `dotnet test` must run in MTP mode
([Microsoft Learn — migrate to MTP mode](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test#migrate-to-mtp-mode-of-dotnet-test)):
- `skinet/global.json` has `"test": { "runner": "Microsoft.Testing.Platform" }` and pins the SDK; CI's
  `setup-dotnet` reads the same file (`global-json-file`).
- `TestingPlatformDotnetTestSupport` is removed from the test projects (MTP mode doesn't use it).
- The 2026-08-20 "zero tests found" problem on the .NET 10 SDK is gone: all 169 run on the .NET 11 SDK.

**.NET 11 notes** *(2026-10-14)*:
- Don't feed `decimal` test parameters from `[InlineData(99.99)]`: that's a `double`, and .NET 11 converts
  it to `decimal` exactly (`99.98999…`) — use `TheoryData` with `m` literals
  ([breaking change](https://learn.microsoft.com/dotnet/core/compatibility/core-libraries/11/decimal-biginteger-floating-point-conversions)).
- EF Core 11 defaults SQL Server to compatibility level 160; Azure SQL must be at ≥ 160 or set
  `UseCompatibilityLevel(150)` ([breaking change](https://learn.microsoft.com/ef/core/what-is-new/ef-core-11.0/breaking-changes)).
- `StackExchange.Redis` runs **4.0.127-alpha** by choice (no stable 4.x yet) — move to 4.0 stable when it ships.

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
