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

## Indexes

Configured in `Config/`, never by data annotation. One `OnModelCreating` serves both providers, so the two
migration sets differ only in provider syntax.

| Index | Table | Serves |
|---|---|---|
| `(Brand, Type)` | Products | shop grid filter + the paging `COUNT(*)` |
| `Name` | Products | the landing page — no filter at all, just `ORDER BY Name` |
| `Price` | Products | the same page with the sort flipped |
| `(BuyerEmail, OrderDate DESC)` | Orders | buyer's order list — filters on one, sorts on the other |
| `PaymentIntentId` unique | Orders | idempotency (T18) |

🔴 **`HasMaxLength` comes before `HasIndex`.** SQL Server can't key an index on `nvarchar(max)`. Leave the
length off and EF picks `nvarchar(450)` for you — and the SqlServer migration carries an `AlterColumn` that
first runs on deploy. Widths: `Brand` 30, `Type` 30, `Name` 100, `BuyerEmail` 256.

- **Sort direction is part of the key.** `(BuyerEmail, OrderDate)` on its own still sorts;
  `.IsDescending(false, true)` matches the `ORDER BY` and removes the Sort operator.
- **Mirror every `HasMaxLength` in the FluentValidation validator** — otherwise an over-long value is a 500
  (`String or binary data would be truncated`), not a 400.
- Measured before/after: [`../smoke-tests/sprint-d/task-43-db-indexes.md`](../smoke-tests/sprint-d/task-43-db-indexes.md).

## Migrations

```bash
dotnet ef migrations add <Name> --context SqliteStoreContext -o Migrations/SQLite
dotnet ef migrations add <Name> --context SqlServerStoreContext -o Migrations/SqlServer
```

Applied automatically on startup via `context.Database.MigrateAsync()` — but **gated by environment**
(`WebApplicationExtensions.cs:13`): Development applies the SQLite set, everything else the SqlServer set.

⚠️ **So `dotnet run` locally never applies a SqlServer migration.** Apply it against the compose container
before it ever reaches Azure (see `build-deployment.md` for the connection string):

```bash
dotnet ef database update --context SqlServerStoreContext
```

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
- 🔴 **Never `Update()` a detached entity that carries a concurrency token.** `Attach` has no load-time
  snapshot, so EF takes the values you handed it as *both* current and original — the token's `WHERE`
  compares the submitted value against itself. On a match it silently skips the check; on a mismatch it
  throws `DbUpdateConcurrencyException` with no concurrency involved (BUG-16 — `QuantityInStock` is
  `[ConcurrencyCheck]`). Load with `GetByIdAsync`, map onto the tracked entity, save.

## Common Mistakes

- Forgetting migration for one provider after a schema change.
- Not checking `Complete()` return value — `false` means nothing was saved.
- Adding a unique index on `nvarchar(max)` on SqlServer — EF narrows it to `nvarchar(450)` automatically, but the migration must include the `AlterColumn` step.
- 🔴 **A migration without its `.Designer.cs` does not exist.** That file carries the `[Migration]`
  attribute EF scans for; the `.cs` alone is dead code. `20260407100224_AddUniqueIndexOnPaymentIntentId`
  lost its Designer and silently never ran on SqlServer — the unique index was missing from Azure for
  four months. Nothing warns you: `has-pending-model-changes` reads the snapshot, not the migration list.
  **Check `__EFMigrationsHistory` against the folder** after any deploy that adds a migration.
