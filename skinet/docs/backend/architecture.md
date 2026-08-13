# Backend Architecture

Clean architecture: 4 layers. References point one way only — Core is the innermost and depends on
nothing; API is the outermost and depends on all.

## Layers

Arrows are project references. Each layer sees only the ones above it.

```
Core                    no project references
  Entities/                     Product, Order, AppUser, ShoppingCart
  Entities/OrderAggregate/      Order and its parts, kept together
  Exceptions/                   PriceChangedException
  Interfaces/                   IGenericRepository, ISpecification
    ^
    |
Application             -> Core
  Common/Interfaces/            ICartService ... ICurrentUser
  Common/DTOs/                  wire shapes (CreateOrderDto)
  Common/Validators/            FluentValidation rules
  Common/Mappings/              entity <-> DTO
  Common/Pagination/            Pagination<T>
  Specifications/               ProductSpecification, OrderSpecification
    ^
    |
Infrastructure          -> Application
  Data/                         StoreContext, GenericRepository, UnitOfWork
  Data/Factory/                 Sqlite vs SqlServer contexts
  Config/                       EF entity type configurations
  Migrations/                   generated, per provider
  Services/                     CartService, OrderService, PaymentService
    ^
    |
API                     -> Infrastructure
  Controllers/ Middleware/ Filters/ Errors/ SignalR/ Extensions/ Settings/
  Services/                     CurrentUser (see the placement rule below)
```

Each layer answers one question:

| Layer | Question |
|---|---|
| Core | What is the business? Nouns and rules. |
| Application | What can be asked of the app? Contracts and shapes, no technology. |
| Infrastructure | How do we talk to the outside? EF, Redis, Stripe. |
| API | How does HTTP get in? Routing, pipeline, DI wiring. |

## Where an implementation goes

Interfaces live in Application. Implementations go **wherever their dependency already lives** —
usually Infrastructure, but not always.

> Does the file name a type that exists only because a request is in flight?
> Yes -> API. No -> Infrastructure.

`CurrentUser` (`src/API/Services/`) is the only implementation outside Infrastructure: it needs
`IHttpContextAccessor`, and Infrastructure cannot reference ASP.NET Core. Infrastructure consumes
`ICurrentUser`, Application declares it, API implements it.

## DB Registration (Program.cs)

SQLite in Development, SqlServer in Production via factory pattern:

```csharp
services.AddDbContext<SqliteStoreContext>(...);
services.AddScoped<StoreContext>(p => p.GetRequiredService<SqliteStoreContext>());
```

All other code depends on `StoreContext` only — never on the derived contexts.

## Rules / Constraints

- Core → no references to other layers. It does carry one NuGet package,
  `Microsoft.Extensions.Identity.Stores`, because `AppUser : IdentityUser` — Identity storage
  abstractions only, no ASP.NET Core.
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
