# Sprint D — smoke tests

One file per task, bug, or fix. Run a file when you finish that item; run them all before the sprint's
deploy gate. Automated coverage and the gate checklist live in [`../../testing.md`](../../testing.md).

| File | Covers |
|---|---|
| [`task-43-db-indexes.md`](task-43-db-indexes.md) | Query indexes — measured before/after at 200k rows, plus 4 browser runs |

## Before any file

| Service | Start it | Verify |
|---|---|---|
| Redis | `docker compose up -d redis` (from `skinet/`) | `docker exec redis redis-cli PING` → `PONG` |
| API | `dotnet run --project src/API/API.csproj` | `https://localhost:7096/api/products` → 200 |
| Client | `npx ng serve` (from `skinet/client/`) | `https://localhost:4200` → 200 |

Files that touch SQL Server also need `docker compose up -d sqlserver` and a `.env` holding
`MSSQL_SA_PASSWORD` — see [`../../build-deployment.md`](../../build-deployment.md).
