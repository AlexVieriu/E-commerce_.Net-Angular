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

## Rules / Constraints

- Never inject `StoreContext` into controllers — use `IUnitOfWork`.
- Never call `SaveChangesAsync` directly — use `unitOfWork.Complete()`.
- Never reference derived contexts outside `Program.cs`.
- Always migrate both providers when changing schema.

## Common Mistakes

- Forgetting migration for one provider after a schema change.
- Not checking `Complete()` return value — `false` means nothing was saved.
- Adding a unique index on `nvarchar(max)` on SqlServer — EF narrows it to `nvarchar(450)` automatically, but the migration must include the `AlterColumn` step.
