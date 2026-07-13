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
- Infrastructure → no references to API, even one-directionally useful ones (SignalR, `NotificationHub`,
  etc. all live in `API`). `API` already references `Infrastructure`, so the reverse would be circular
  and won't compile. An Infrastructure service that needs to trigger a SignalR push can't do it itself —
  the push happens in the controller, which calls into the service for the actual business logic first.

## Common Mistakes

- Referencing `StoreContext` or Infrastructure types directly in a controller.
- Adding a service interface to Core instead of Application.
- Running only one migration when changing schema — must run both providers.
- Trying to move a SignalR/notification call into an Infrastructure service "to keep it all in one
  place" — it can't reference `NotificationHub` from there (see Rules above).
