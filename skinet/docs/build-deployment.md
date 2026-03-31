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

## Rules / Constraints

- SQLite for Development, SqlServer for Production — set by `ASPNETCORE_ENVIRONMENT`.
- Stripe keys and connection strings go in user secrets / env vars — never in `appsettings.json`.
