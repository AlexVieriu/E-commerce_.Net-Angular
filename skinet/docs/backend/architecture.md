# Backend Architecture

Clean architecture: 4 layers, each depends only on layers below.

## Layers

| Layer | Location | Responsibilities |
|---|---|---|
| Core | `src/Core/` | Entities, repository + spec interfaces. No dependencies. |
| Application | `src/Application/` | Service interfaces, DTOs, specs, pagination. Depends on Core only. |
| Infrastructure | `src/Infrastructure/` | EF Core, repos, services (Redis, Stripe). Depends on Core + Application. |
| API | `src/API/` | Controllers, middleware, filters, SignalR, DI. Depends on all. |

## DB Registration (Program.cs)

SQLite in Development, SqlServer in Production via factory pattern:

```csharp
services.AddDbContext<SqliteStoreContext>(...);
services.AddScoped<StoreContext>(p => p.GetRequiredService<SqliteStoreContext>());
```

All other code depends on `StoreContext` only — never on the derived contexts.

## Rules / Constraints

- Core → no references to other layers.
- Application → no references to Infrastructure or API.
- Service interfaces in Application, not Core.
- `IProductRepository` is legacy — use `IGenericRepository<Product>` + specs.
- Migrations per-provider: run SQLite and SqlServer separately.

## Common Mistakes

- Referencing `StoreContext` or Infrastructure types directly in a controller.
- Adding a service interface to Core instead of Application.
- Running only one migration when changing schema — must run both providers.
