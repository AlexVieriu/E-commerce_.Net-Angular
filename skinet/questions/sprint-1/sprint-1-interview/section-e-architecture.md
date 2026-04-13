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

---

**E2.** What is the difference between the Repository pattern and the Unit of Work pattern? How do they work together?

<details><summary>Answer</summary>

- **Repository** — encapsulates data access for a single entity type (CRUD operations).
- **Unit of Work** — coordinates multiple repositories in one transaction; tracks changes across entities and commits atomically.

In EF Core, `DbContext` IS the Unit of Work (change tracker) and `DbSet<T>` is a basic Repository. Custom wrappers add a clean abstraction layer on top.
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

---

**E4.** What is the difference between the MVC pattern and the API controller pattern in ASP.NET Core? Why don't you return Views from an API controller?

<details><summary>Answer</summary>

- **MVC controllers** — return `Views` (HTML rendered server-side via Razor); tightly coupled to the UI.
- **API controllers** — return data (DTOs as JSON/XML) for client-side rendering; use `[ApiController]`, no Views folder, return `IActionResult` with HTTP status codes.

Separation: the API serves data, the frontend (Angular/React) handles presentation. This enables multiple clients — web, mobile, third-party — from the same backend.
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

---

### HARD

**E6.** Compare these architectures for an e-commerce backend: (1) modular monolith, (2) microservices, (3) serverless functions. For each: how would you split orders/payments/inventory? What's the communication pattern? Where does the transaction boundary live? When would you choose each?

<details><summary>Answer</summary>

**Modular monolith:** modules (Orders, Payments, Inventory) in one app with clear boundaries (separate folders/projects). Communication: in-process method calls or MediatR. Transactions: single DB, ACID across modules. Choose: early stage, small team, simple ops.

**Microservices:** separate services with own DBs. Communication: HTTP/gRPC + message queues (RabbitMQ). Transactions: saga pattern (no distributed transactions). Choose: large team, independent scaling, fault isolation needed.

**Serverless:** functions per operation, event-driven (Lambda + SQS). Cheap at low scale, expensive at high scale. Choose: sporadic traffic, event-driven workflows.
</details>

---

