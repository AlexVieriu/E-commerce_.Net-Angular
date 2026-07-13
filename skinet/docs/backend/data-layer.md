# Data Layer

StoreContext, GenericRepository, UnitOfWork, migrations (SQLite + SqlServer).

## StoreContext

`StoreContext : IdentityDbContext<AppUser>`. DbSets: `Products`, `Addresses`, `DeliveryMethods`, `Orders`, `OrderItems`.
Configs via `ApplyConfigurationsFromAssembly`. DB-specific column types in `ApplyDatabaseSpecificConfigurations` (overridden by derived contexts).

## GenericRepository

| Method | Notes |
|---|---|
| `GetByIdAsync(id)` | `FindAsync` |
| `GetAllAsync(spec, token)` | Applies spec, CancellationToken |
| `GetEntityWithSpec(spec)` | First match or null |
| `GetAllAsync<TResult>(spec)` | Projects to DTO via spec's `Select` |
| `GetPagedAsync(spec)` | Returns `(Items, TotalCount)` |
| `Add / Update / Delete` | Mutate tracked state — no `SaveChanges` |
| `Exists(id)` | `bool` — synchronous check entity exists by id |
| `GetEntityWithSpec<TResult>(spec)` | First match projected to TResult |
| `GetAllAsync()` | No-arg — returns all entities, no filter |

`Update` uses `Attach` + `EntityState.Modified`.

## UnitOfWork

Caches `GenericRepository<T>` instances in `ConcurrentDictionary`. Creates on demand via reflection.

```csharp
unitOfWork.Repository<Product>().Add(product);
await unitOfWork.Complete(); // SaveChangesAsync — returns true if rows affected
```

## Migrations

```bash
dotnet ef migrations add <Name> --context SqliteStoreContext -o Migrations/SQLite
dotnet ef migrations add <Name> --context SqlServerStoreContext -o Migrations/SqlServer
```

Applied automatically on startup via `context.Database.MigrateAsync()`.

## Seeding

Two seeders, both run from `WebApplicationExtensions.SeedDatabaseAsync` after migrate:

| Seeder | When | What |
|---|---|---|
| `StoreContextSeed` | **Every startup, all envs** | Admin user (password re-synced from config — BUG-9), JSON products + delivery methods if the tables are empty |
| `BogusSeeder` | **`dotnet run -- --seed`, Development only** | Bulk demo data: 52 products, 12 customers, 25 orders |

- **`--seed` is one-shot:** `Program.cs` seeds then `return`s (doesn't serve). Passed through as `runBulkSeed`.
- **Dev-only by design** so a prod deploy never auto-pollutes the live DB; non-dev `--seed` logs a warning and skips the bulk seed.
- **Idempotent:** `BogusSeeder` no-ops if any order already exists (orders only come from it).
- **Deterministic:** `Randomizer.Seed` fixed → same dataset each run.
- Generated data respects the real constraints: product `PictureUrl` is assigned from the fixed local image set by type, orders carry a unique `PaymentIntentId` (T18) + real FK refs, customer emails are sanitised for Identity's username rules.
- Brand/type lists match the baseline JSON casing exactly — otherwise the cached `/brands` filter shows case-only duplicates.

**Reset (dev):** bulk data is throwaway — no teardown command. To clear it, wipe the dev DB and restart; the baseline reseeds itself:

```bash
dotnet ef database drop -f --context SqliteStoreContext   # or just delete the SQLite file under ./data
dotnet run --project src/API                              # baseline (admin + products) auto-reseeds; bulk gone until next --seed
```

## Rules / Constraints

- Never inject `StoreContext` into controllers — use `IUnitOfWork`.
- Never call `SaveChangesAsync` directly — use `unitOfWork.Complete()`.
- Never reference derived contexts outside `Program.cs`.
- Always migrate both providers when changing schema.
- Entities fetched via the repository (`GetByIdAsync`, `GetEntityWithSpec`) are already tracked by the
  request's scoped `StoreContext` — just mutate properties and call `unitOfWork.Complete()`;
  `SaveChangesAsync` diffs live values against the load-time snapshot (`ChangeTracker.DetectChanges()`)
  and generates the `UPDATE` itself. `Update()` (`Attach` + `EntityState.Modified`) is only for a
  **detached** entity the context has never tracked — e.g. one built from a deserialized request body.
  Calling `Update()` on an already-tracked entity is redundant.

## Common Mistakes

- Forgetting migration for one provider after a schema change.
- Not checking `Complete()` return value — `false` means nothing was saved.
- Adding a unique index on `nvarchar(max)` on SqlServer — EF narrows it to `nvarchar(450)` automatically, but the migration must include the `AlterColumn` step.
