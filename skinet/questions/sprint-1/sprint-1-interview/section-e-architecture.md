## SECTION E — ARCHITECTURE & DESIGN PATTERNS

### EASY

**E1.** What is the Repository pattern? Why use it instead of injecting `DbContext` directly into controllers?

<details><summary>Answer</summary>

Repository abstracts data access behind an interface. Benefits:
1. Testability — mock the repository instead of DbContext.
2. Separation of concerns — controllers don't know about EF/SQL.
3. Single responsibility — data access logic lives in one place.
4. Swappable persistence — change from EF to Dapper without touching business logic.
</details>

<details><summary>Deep Dive</summary>

**The problem: leaking DbContext into your controllers**

When a controller directly injects `AppDbContext`, it has:
- Knowledge of EF Core (infrastructure concern)
- Access to ANY entity in the database (violates encapsulation)
- LINQ queries scattered across controllers (duplication, hard to optimize)
- No easy way to test without hitting a real DB

```csharp
// BAD — DbContext leaking into business layer
[HttpGet("{id}")]
public async Task<IActionResult> GetOrder(int id)
{
    var order = await _context.Orders
        .Include(o => o.Items)
        .ThenInclude(i => i.Product)
        .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
    // This EF-specific query is now inside your API layer
}
```

**Generic vs Specific repositories — the real debate**

There are two schools of thought:

**Generic repository** (`IRepository<T>`):
```csharp
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<IReadOnlyList<T>> ListAllAsync();
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task<int> CountAsync(ISpecification<T> spec);
}
```

Pro: one interface covers all entities. Con: lowest common denominator — doesn't express business-specific queries.

**Specific repository** (`IOrderRepository`):
```csharp
public interface IOrderRepository
{
    Task<Order?> GetOrderWithItemsAsync(int id);
    Task<IReadOnlyList<Order>> GetOrdersByCustomerAsync(string email);
    Task<IReadOnlyList<Order>> GetPendingOrdersAsync();
    Task<decimal> GetTotalRevenueAsync(DateTimeOffset from, DateTimeOffset to);
}
```

Pro: expresses business intent clearly. Con: one interface per aggregate, more boilerplate.

**The Specification pattern — best of both worlds**

The Specification pattern lets the generic repository accept query criteria without leaking EF into the service layer:

```csharp
// Specification defines the query criteria (no EF dependency)
public class OrdersWithItemsSpecification : BaseSpecification<Order>
{
    public OrdersWithItemsSpecification(string email)
        : base(o => o.CustomerEmail == email)
    {
        AddInclude(o => o.Items);
        AddInclude(o => o.Items.Select(i => i.Product));
        ApplyOrderBy(o => o.OrderDate);
    }
}

// Usage in service
var spec = new OrdersWithItemsSpecification(customerEmail);
var orders = await _repository.ListAsync(spec);
```

The specification encapsulates the query logic. The repository applies it to `IQueryable<T>`. The service layer never sees EF.

**When NOT to use a repository**

Repositories add value when:
- You unit test without a DB (mocking the repo)
- You have complex domain logic in services that orchestrate multiple repos
- You may swap persistence technology

Repositories are unnecessary overhead when:
- The project is a simple CRUD app with no complex business logic
- You use integration tests exclusively (test with real DB anyway)
- The team is small and the architecture is flat

Martin Fowler himself notes that EF Core's `DbContext` is already a "sophisticated Unit of Work and Repository implementation." Adding another layer on top is only justified if you gain meaningful abstraction.
</details>

---

**E2.** What is the difference between the Repository pattern and the Unit of Work pattern? How do they work together?

<details><summary>Answer</summary>

- **Repository** — encapsulates data access for a single entity type (CRUD operations).
- **Unit of Work** — coordinates multiple repositories in one transaction; tracks changes across entities and commits atomically.

In EF Core, `DbContext` IS the Unit of Work (change tracker) and `DbSet<T>` is a basic Repository. Custom wrappers add a clean abstraction layer on top.
</details>

<details><summary>Deep Dive</summary>

**The problem that Unit of Work solves**

Imagine placing an e-commerce order. Three things must happen atomically:
1. Create the `Order` record
2. Create `OrderItem` records
3. Decrement `Product.StockQuantity` for each item

Without Unit of Work, if you have three separate repositories each calling `SaveChangesAsync()`:

```csharp
await _orderRepo.AddAsync(order);      // SaveChanges #1 — order committed
await _orderItemRepo.AddRangeAsync(items); // SaveChanges #2 — items committed
await _productRepo.UpdateStockAsync(items); // SaveChanges #3 — if this throws...
// Stock was never decremented! Data is now inconsistent.
```

Each `SaveChanges` is a separate database transaction. A failure in step 3 leaves the database in a partial state — order exists, items exist, but stock wasn't updated.

**With Unit of Work: one transaction, all-or-nothing**

```csharp
public interface IUnitOfWork : IDisposable
{
    IOrderRepository Orders { get; }
    IOrderItemRepository OrderItems { get; }
    IProductRepository Products { get; }
    Task<int> CompleteAsync();  // single SaveChangesAsync()
}

// Usage in service
public async Task PlaceOrderAsync(CreateOrderDto dto)
{
    var order = new Order(dto.CustomerId, dto.ShippingAddress);
    await _uow.Orders.AddAsync(order);

    foreach (var item in dto.Items)
    {
        await _uow.OrderItems.AddAsync(new OrderItem(order.Id, item.ProductId, item.Qty));
        await _uow.Products.DecrementStockAsync(item.ProductId, item.Qty);
    }

    await _uow.CompleteAsync(); // ONE SaveChangesAsync — atomic commit or full rollback
}
```

If anything fails before `CompleteAsync()`, nothing is committed. Database remains consistent.

**How EF Core is already a Unit of Work**

EF Core's `DbContext` implements the UoW pattern internally:
- **Change tracker**: tracks every entity loaded from the DB and every entity added/modified/deleted in this context.
- **Identity map**: ensures the same entity (same primary key) is represented by one object in memory — no duplicates.
- `SaveChangesAsync()`: generates the SQL, wraps it in a single transaction, executes everything atomically.

When you write a custom `UnitOfWork` class, you're typically:
1. Exposing your repositories as properties (all sharing the SAME `DbContext` instance)
2. Wrapping `_context.SaveChangesAsync()` in your `CompleteAsync()` method
3. Adding explicit transaction control for complex scenarios

**Explicit transactions — when DbContext isn't enough**

For cross-context operations or when you need more granular control:

```csharp
using var transaction = await _context.Database.BeginTransactionAsync();
try
{
    // Multiple operations across the same DbContext
    await _context.SaveChangesAsync();

    // Notify external system (can be rolled back if this fails)
    await _outboxRepository.AddEventAsync(new OrderPlacedEvent(order.Id));
    await _context.SaveChangesAsync();

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

**Distributed transactions — when Unit of Work breaks down**

In microservices, each service has its own database. There's no shared `DbContext`. A UoW can't span service boundaries. Solutions:
- **Saga pattern**: sequence of local transactions with compensating actions on failure
- **Outbox pattern**: write event to same DB as the operation (atomic), then relay to message bus
- **Two-phase commit (2PC)**: expensive, rarely used in practice

This is one of the biggest challenges of microservices — distributed data consistency without distributed transactions.
</details>

---

### MEDIUM

**E3.** Your app has three layers: Core (domain), Application (services), Infrastructure (EF, Redis, Stripe). Draw the dependency graph. Which layer depends on which? What is the Dependency Inversion Principle and how does your architecture apply it?

<details><summary>Answer</summary>

Dependencies point inward: `Infrastructure → Application → Core`. Core depends on **nothing**.

- Interfaces (`IRepository`, `IEmailService`) live in **Core**; implementations live in **Infrastructure**.
- This is Dependency Inversion: Core (high-level) defines the abstraction; Infrastructure (low-level) implements it.
- At runtime the DI container wires `IOrderRepository` → `SqlOrderRepository`.
- **Benefits**: Core is testable in isolation; Infrastructure is swappable without touching business logic.
</details>

<details><summary>Deep Dive</summary>

**The dependency direction — "point inward"**

Inward means toward Core. Core is at the center:
```
[Infrastructure] → [Application] → [Core]
```

Infrastructure code (EF, Redis, HTTP clients) depends on Application (services, business logic), which depends on Core (domain entities, interfaces). Core has ZERO dependencies on any other layer.

This means Core can be compiled and tested in complete isolation — no database, no external APIs, no Framework dependencies.

**Interfaces in Core, implementations in Infrastructure**

```csharp
// Core/Interfaces/IOrderRepository.cs — no EF reference
namespace MyApp.Core.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id);
    Task AddAsync(Order order);
}

// Infrastructure/Persistence/OrderRepository.cs — has EF
namespace MyApp.Infrastructure.Persistence;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;
    
    public async Task<Order?> GetByIdAsync(int id) =>
        await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
}
```

If you later swap EF for Dapper, only `OrderRepository.cs` changes. The interface (contract) stays the same. Services and controllers don't change.

**Testability — the real benefit**

```csharp
// Test uses a mock repository — no database
[Fact]
public async Task PlaceOrder_WithValidInput_CreatesOrder()
{
    var mockRepo = new Mock<IOrderRepository>();
    mockRepo.Setup(r => r.AddAsync(It.IsAny<Order>()))
        .Returns(Task.CompletedTask);
    
    var service = new OrderService(mockRepo.Object);
    await service.PlaceOrderAsync(new OrderRequest { ... });
    
    mockRepo.Verify(r => r.AddAsync(It.IsAny<Order>()), Times.Once);
}
```

The service never touches the database. Tests run in milliseconds.

**Anti-pattern: interfaces in Infrastructure**

If you put `IOrderRepository` in the Infrastructure layer, Core doesn't know about the interface — it has to depend on Infrastructure (the wrong direction). You lose the swappability and testability.
</details>

---

**E4.** What is the difference between the MVC pattern and the API controller pattern in ASP.NET Core? Why don't you return Views from an API controller?

<details><summary>Answer</summary>

- **MVC controllers** — return `Views` (HTML rendered server-side via Razor); tightly coupled to the UI.
- **API controllers** — return data (DTOs as JSON/XML) for client-side rendering; use `[ApiController]`, no Views folder, return `IActionResult` with HTTP status codes.

Separation: the API serves data, the frontend (Angular/React) handles presentation. This enables multiple clients — web, mobile, third-party — from the same backend.
</details>

<details><summary>Deep Dive</summary>

**MVC — the monolithic server-rendered pattern**

MVC is an old pattern where the server handles both data and presentation:

```csharp
[HttpGet("products/{id}")]
public IActionResult GetProduct(int id)
{
    var product = _service.GetProduct(id);
    return View(product);  // Renders HTML server-side via Views/Products/GetProduct.cshtml
}
```

The View is a Razor template that generates HTML strings on the server. Each request → HTML page sent to browser. The browser displays it. For form submissions, the cycle repeats.

Advantages: simple, SEO-friendly (rendered HTML), good for monolithic server-rendered apps.
Disadvantages: full page refresh on every action, coupled rendering to backend, hard to build rich UX.

**API — decoupled data and presentation**

API controllers return raw data:

```csharp
[HttpGet("products/{id}")]
[ApiController]
public async Task<IActionResult> GetProduct(int id)
{
    var product = await _service.GetProduct(id);
    return Ok(product);  // Returns JSON: { id: 1, name: "Widget", price: 29.99 }
}
```

The client (Angular, React, mobile app) receives JSON and renders it however it wants. Same API, multiple UIs.

Advantages: flexible, reusable across many clients, enables rich interactivity (SPA), easy to build mobile apps.
Disadvantages: requires JavaScript knowledge, client must handle rendering, SEO is harder (need SSR/prerendering).

**DTO pattern — API responses**

APIs don't return domain entities directly — they return Data Transfer Objects (DTOs):

```csharp
// Domain entity (has business logic, references, unneeded details)
public class Order
{
    public int Id { get; set; }
    public User CreatedBy { get; set; }  // circular ref risk
    public List<OrderItem> Items { get; set; }
    public decimal CalculateTotal() { ... }
}

// API response DTO (flattened, safe to expose)
public record OrderDto(int Id, string CreatedByName, List<ItemDto> Items, decimal Total);
```

DTOs expose only what the client needs and hide implementation details.
</details>

---

**E5.** You need to add a "wishlist" feature. Walk through which layer each piece of code goes in: entity, repository interface, repository implementation, service, controller, DTO. Why does the repository interface live in Core and the implementation in Infrastructure?

<details><summary>Answer</summary>

- **Core:** `WishlistItem` entity + `IWishlistRepository` interface.
- **Infrastructure:** `WishlistRepository : IWishlistRepository` (EF implementation) + `WishlistConfiguration` (Fluent API).
- **Application:** `WishlistService` (business logic: add, remove, check duplicates) + `WishlistDto`.
- **API:** `WishlistController` (endpoints: GET /wishlist, POST /wishlist, DELETE /wishlist/{id}).

Interface in Core because: Core defines WHAT operations exist (abstraction), Infrastructure defines HOW (implementation). This inverts the dependency — Core never references EF.
</details>

<details><summary>Deep Dive</summary>

**Walking through a feature — step by step**

1. **Core — Define the domain**
   - `WishlistItem` entity: `{ UserId, ProductId, AddedDate }`
   - `IWishlistRepository` interface: `Task AddAsync(WishlistItem item)`, `Task<IReadOnlyList<WishlistItem>> GetByUserAsync(int userId)`

2. **Infrastructure — Implement data access**
   - `WishlistRepository` uses EF Core to query/insert
   - `WishlistConfiguration` configures the table: `HasKey(w => new { w.UserId, w.ProductId })`, `HasOne(w => w.User).WithMany()`

3. **Application — Business logic**
   - `WishlistService` orchestrates: "Check if item is already wishlisted before adding" (prevent duplicates), "Remove from wishlist", "Get all wishlist items for a user"
   - `WishlistDto` is the output: `{ ProductId, ProductName, Price, AddedDate }`

4. **API — Expose endpoints**
   - `GET /wishlist` — returns list of `WishlistDto`
   - `POST /wishlist` with `{ productId }` — adds to wishlist
   - `DELETE /wishlist/{productId}` — removes from wishlist

**Why each piece lives where**

Core entities are persistence-agnostic — they're plain C# classes with no EF annotations (unless using value converters). If EF ever goes away, Core still compiles.

The repository interface belongs in Core because it answers "what operations do we need?" — the service doesn't care if it's SQL, MongoDB, Redis, or a CSV file.

The implementation belongs in Infrastructure because it answers "how do we access data?" — this layer has EF, SQL, connection strings.
</details>

---

### HARD

**E6.** Compare these architectures for an e-commerce backend: (1) modular monolith, (2) microservices, (3) serverless functions. For each: how would you split orders/payments/inventory? What's the communication pattern? Where does the transaction boundary live? When would you choose each?

<details><summary>Answer</summary>

**Modular monolith:** modules (Orders, Payments, Inventory) in one app with clear boundaries (separate folders/projects). Communication: in-process method calls or MediatR. Transactions: single DB, ACID across modules. Choose: early stage, small team, simple ops.

**Microservices:** separate services with own DBs. Communication: HTTP/gRPC + message queues (RabbitMQ). Transactions: saga pattern (no distributed transactions). Choose: large team, independent scaling, fault isolation needed.

**Serverless:** functions per operation, event-driven (Lambda + SQS). Cheap at low scale, expensive at high scale. Choose: sporadic traffic, event-driven workflows.
</details>

<details><summary>Deep Dive</summary>

**Modular monolith — the goldilocks zone**

One codebase, multiple logical modules. No network calls between modules (fast, simple). Single database (ACID transactions across modules). Easy to develop, deploy, and debug. Scaling is all-or-nothing (scale the entire app, not individual modules).

Best for: startups, teams < 20 people, monolithic applications with clear domain boundaries.

**Microservices — the complexity tax**

Each service owns its data (DB independence → better isolation). Services communicate via HTTP/gRPC (slower but decoupled). Transactions span services via the Saga pattern (eventual consistency, complex logic).

Saga example: ordering service calls payment service, which calls inventory service. If payment fails, a compensating transaction refunds and rolls back the order.

Best for: large teams (> 50), independent scaling needs (Orders scale differently than Analytics), fault isolation critical.

**Serverless — economics matter**

Pay per invocation. At low traffic (< 1M monthly invocations), very cheap. At high traffic (> 10M), fixed infrastructure (containers, VMs) becomes cheaper. Cold starts are the Achilles heel (Lambda: 500ms+, impacts UX).

Best for: event-driven workflows (image processing, webhooks), scheduled jobs, sporadic traffic.

**For e-commerce**: modular monolith initially, migrate to microservices if Orders and Inventory scale independently. Serverless for background jobs (email sending, analytics).
</details>

---

