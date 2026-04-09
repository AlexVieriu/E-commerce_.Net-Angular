# Sprint 2 — Competitive Interview Questions

> Topics: CORS/CSP, Angular signals, optimistic concurrency, transactions, unit testing/Moq, Stripe flow, SignalR, error handling, Redis, price change detection
> Target: .NET mid-level + Angular junior
> Difficulty: 20% Easy | 40% Medium | 40% Hard
> Goal: Top 1% preparation

---

## SECTION A — CORS & CSP & SECURITY HEADERS

### EASY

**A1.** What is CORS? Why does it exist? What happens if you don't configure CORS on your API?

**A2.** What's the difference between CORS and CSP?

**A3.** What does `X-Frame-Options: DENY` protect against?

**A4.** What does `X-Content-Type-Options: nosniff` do?

---

### MEDIUM

**A5.** Your Angular app works on `localhost:4200` but after deploying to Azure, API calls fail silently. No errors in the .NET logs. What's wrong?

**A6.** What is a preflight request? When does the browser send one? What headers does it include? What must the server respond with for the preflight to succeed?

**A7.** You add `Content-Security-Policy: default-src 'self'` to your API. Your Angular app's Material components break. The console shows CSP violation errors for inline styles. Explain why and give two solutions with different security trade-offs.

**A8.** Why does `AllowCredentials()` in CORS matter for cookie auth? What happens if you remove it?

**A9.** Compare these two CORS configurations. Which is insecure and why?

```csharp
// Config A
builder.Services.AddCors(o => o.AddPolicy("default", p =>
    p.WithOrigins("https://myapp.com")
     .AllowAnyHeader()
     .AllowCredentials()));

// Config B
builder.Services.AddCors(o => o.AddPolicy("default", p =>
    p.AllowAnyOrigin()
     .AllowAnyHeader()
     .AllowCredentials()));
```

**A10.** Write the `Permissions-Policy` header value that disables accelerometer, camera, and microphone. What does each API do and why disable them?

---

### HARD

**A11.** Design a CSP policy for an e-commerce app that uses: Angular (self-hosted), Angular Material (inline styles), Stripe Elements (loads from `js.stripe.com`), Google Fonts, and your own API. Write the full CSP header value. Explain each directive.

**A12.** You're auditing a codebase and find:

```csharp
app.UseCors(x => x
    .SetIsOriginAllowed(_ => true)
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials());
```

Explain exactly what `SetIsOriginAllowed(_ => true)` does, why it's dangerous, and write a realistic attack scenario. Then write the corrected version.

**A13.** Angular Material uses `ViewEncapsulation.None` which injects global `<style>` tags. Why does this conflict with CSP? What would a production-grade solution look like? Explain the nonce-based approach.

**A14.** You use `SameSite=Strict` for CSRF protection. A colleague argues you should add anti-forgery tokens (`[ValidateAntiForgeryToken]`) too. Are they right? When would one not be enough?

---

## SECTION B — EF CORE & CONCURRENCY

### EASY

**B1.** What does `[ConcurrencyCheck]` do in EF Core?

**B2.** What are the three value states in `DbUpdateConcurrencyException`?

**B3.** What is the difference between `FirstOrDefault()` and `SingleOrDefault()` in EF Core? When would you use each?

**B4.** What does `Include()` do in EF Core? What SQL does it generate?

**B5.** What is a migration in EF Core? What happens if you modify an entity but forget to create a migration?

---

### MEDIUM

**B6.** You have `[ConcurrencyCheck]` on `QuantityInStock`. Two requests read `stock = 5` simultaneously. Request A saves `stock = 4`. Request B tries to save `stock = 4`. What SQL does EF generate for Request B? What happens? What exception is thrown?

**B7.** What does `AsNoTracking()` do? When should you use it? What breaks if you use `AsNoTracking()` and then try to update the entity? What happens to `[ConcurrencyCheck]` if you read with `AsNoTracking()`?

**B8.** Compare `[ConcurrencyCheck]` and `[Timestamp]` (rowversion). When would you choose each? Which databases support `[Timestamp]`? What SQL does each generate?

**B9.** You call `SaveChangesAsync()` and it throws `DbUpdateConcurrencyException`. You want to implement "client wins" resolution. Write the code to reload the database values and force your changes through.

**B10.** What is the N+1 query problem? Give an example with Orders and OrderItems. How does `Include()` fix it? What's the downside of over-using `Include()`?

**B11.** What does this code do? Is there a performance problem?

```csharp
var products = await context.Products.ToListAsync();
var expensive = products.Where(p => p.Price > 100).ToList();
```

**B12.** What is the Specification pattern? Why use it instead of putting query logic directly in the repository? What are the trade-offs?

**B13.** What is the difference between `IQueryable<T>` and `IEnumerable<T>` when used with EF Core? If your repository returns `IEnumerable<Product>` and the caller does `.Where(p => p.Price > 100)`, where does the filtering happen? Why is this a performance disaster?

---

### HARD

**B14.** You have a Unit of Work with `BeginTransactionAsync()`. Inside the transaction, you decrement stock (with concurrency check), create an order, and delete the cart. The stock update succeeds, the order insert succeeds, but cart deletion fails with a network timeout to Redis. What happens to the SQL transaction? What happens to the stock? Design the correct rollback behavior.

**B15.** EF Core's `InMemoryDatabase` doesn't enforce foreign keys, unique constraints, or concurrency tokens. You wrote a concurrency test using InMemory and it passed — but the same scenario fails in production. Explain why InMemory gave a false positive. What testing database should you use instead? What are the trade-offs?

**B16.** You have 10 million products. Your query filters by `Category`, sorts by `Price`, and paginates (`Skip(100).Take(20)`). The query takes 3 seconds. Explain the execution plan, identify the bottleneck, and design the correct index. What's the difference between a covering index and a filtered index here?

**B17.** Compare three EF Core concurrency strategies. For each, give the generated SQL and a use case:
1. No concurrency control (default)
2. `[ConcurrencyCheck]` on a specific column
3. `[Timestamp]` (rowversion)

**B18.** Design a retry strategy for `DbUpdateConcurrencyException` in a background order processor. Include: max retries, backoff, what to reload, when to give up. Write the code.

---

## SECTION C — ANGULAR SIGNALS

### EASY

**C1.** What is a signal in Angular? How is it different from a BehaviorSubject?

**C2.** What does `computed()` do? When does it re-evaluate?

**C3.** Why does mutating a signal object not trigger re-render in Angular?

---

### MEDIUM

**C4.** What does this code output? Why?

```typescript
const count = signal(0);
const doubled = computed(() => count() * 2);
const isEven = computed(() => doubled() % 2 === 0);

console.log(isEven()); // ?
count.set(3);
console.log(isEven()); // ?
count.set(4);
console.log(isEven()); // ?
```

**C5.** You have a signal holding a shopping cart:

```typescript
const cart = signal<CartItem[]>([]);
```

Write a function `addItem(item: CartItem)` that correctly triggers re-render. Write a version that DOESN'T trigger re-render. Explain the difference.

**C6.** What is `effect()` in Angular signals? When does it run? What are the rules about writing to signals inside an effect? Why does Angular restrict this?

**C7.** You have a component using signals. You want to make an HTTP call when a signal changes. Should you use `effect()` or `computed()`? Why? What's the danger of using `effect()` for side effects that call APIs?

**C8.** A signal holds a nested object: `signal({ user: { name: '', address: { city: '' } } })`. You spread the outer object but mutate `address.city` directly. Does the template update? Explain the inner reference problem and the fix.

**C9.** Create a signal with `{ address: boolean, payment: boolean }`. Write a `computed` that returns `true` only when both are `true`. Write a function that correctly sets `address` to `true`. Demonstrate what happens if you mutate instead of spread.

---

### HARD

**C10.** You have deeply nested state:

```typescript
const store = signal({
  user: {
    profile: { name: '', address: { city: '', zip: '' } },
    preferences: { theme: 'dark', notifications: true }
  },
  cart: { items: [], total: 0 }
});
```

Write a function that updates ONLY `user.profile.address.city` to `'London'` while correctly triggering re-render. Now explain: if a `computed()` signal reads ONLY `store().cart.total`, does updating the city cause it to re-evaluate? Why?

**C11.** Compare signals with RxJS for state management in Angular. When would you still use RxJS over signals? Give three concrete scenarios where signals are insufficient and RxJS is necessary.

**C12.** Design a signal-based state management solution for a checkout flow with three steps: address, payment, review. Each step has validation state. The "Place Order" button is enabled only when all three steps are valid. Show the signal declarations, computed signals, and update functions. Handle the case where the user goes back and changes the address (payment should NOT be invalidated).

**C13.** You're migrating from Zone.js to zoneless Angular. Your app has 50 components using `OnPush`. After removing Zone.js, some components stop updating. Why? What do you need to change?

---

## SECTION D — TESTING (Unit Tests, Moq, Test Quality)

### EASY

**D1.** What is the difference between a unit test and an integration test? Which is faster? Which gives more confidence?

**D2.** What is the AAA pattern (Arrange, Act, Assert)?

**D3.** What does `Mock<T>` do in Moq? What is the difference between `Setup` and `Verify`?

**D4.** What three properties make a test valuable vs noise?

**D5.** When should you use `[ExcludeFromCodeCoverage]`? When is it a code smell?

---

### MEDIUM

**D6.** You have this test. Is it a good test? Why or why not?

```csharp
[Fact]
public async Task CreateOrder_ShouldWork()
{
    var result = await _service.CreateOrderAsync("buyer@test.com", _orderDto);
    Assert.NotNull(result);
}
```

**D7.** Your Moq `Setup` uses `It.IsAny<TimeSpan?>()` but the production code calls an overload that takes `Expiration` (not `TimeSpan?`). The test passes. You delete the production method. The test still passes. Explain the full chain of events. How do you prevent this?

**D8.** Write a test for this method. Include happy path and at least two edge cases:

```csharp
public decimal GetTotal()
{
    return Subtotal - Discount + DeliveryMethod.Price;
}
```

**D9.** What is the difference between `mockDb.Setup(...)` and `mockDb.As<IDatabaseAsync>().Setup(...)`? When do you need the `As<>` cast? What happens if you use the wrong one?

**D10.** You want to test that `OrderService.CreateOrderAsync` decrements stock correctly. You use EF Core InMemory database. You seed two products with stock=10. After creating an order with qty=2 for product 1, you assert `product1.QuantityInStock == 8`. The test passes. Is this test reliable? What can go wrong?

**D11.** What is mutation testing? How does it differ from code coverage? If you have 100% code coverage, does that mean your tests are good? Give an example where 100% coverage still misses a bug.

**D12.** You have a Moq `Setup(...).ReturnsAsync(false)` but no `Verify`. You delete the production method. The test still passes. Why?

**D13.** `Order.GetTotal()` returns `-5` when discount is `60`, subtotal is `50`, shipping is `5`. You don't know if this is intentional. What do you do? Write the test.

**D14.** What coverage target do you set for each layer (Core, Application, Infrastructure, API controllers) and why?

---

### HARD

**D15.** Design a test for optimistic concurrency. You need to prove that two simultaneous orders for the last item result in exactly one success and one failure. You cannot use Thread.Sleep. How do you simulate concurrency in a unit test? What database provider do you use? Why can't you use InMemory?

**D16.** A colleague writes this test and says "It validates the happy path":

```csharp
[Fact]
public async Task SetCart_ReturnsCart()
{
    _mockDb.Setup(x => x.StringSetAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<RedisValue>(),
        It.IsAny<TimeSpan?>(),
        It.IsAny<When>(),
        It.IsAny<CommandFlags>()))
        .ReturnsAsync(true);

    var result = await _cartService.SetCartAsync(new ShoppingCart { Id = "cart1" });

    result.Should().NotBeNull();
    result!.Id.Should().Be("cart1");
}
```

Delete `SetCartAsync` from the service. Does the test break? Why or why not? Write the corrected test.

**D17.** Your test suite has 128 tests. 126 pass, 2 fail intermittently (sometimes pass, sometimes fail on CI). These are called "flaky tests." Name five common causes of flaky tests and how to fix each.

**D18.** You need to test `PaymentService` but it calls `new PaymentIntentService()` directly — no DI seam. You can't modify the production code (it's a third-party SDK pattern). What are your options? Compare at least three approaches with trade-offs.

**D19.** A colleague's PR has a `CartService_Tests.cs` failure-path test with no `Verify`. You run the tests — they pass. You delete `SetCartAsync` entirely — they still pass. Walk through why and what you'd say in code review.

**D20.** You need to test that calling `CreateOrderAsync` twice with the same cart returns the existing order, not a duplicate. Your first approach: seed an order into EF InMemory, then call the service once. The test fails — a new order is created. Why does manual seeding fail? What's the correct approach?

---

## SECTION E — ERROR HANDLING & MIDDLEWARE

### MEDIUM

**E1.** You have a custom `ExceptionMiddleware` that catches exceptions and maps them to HTTP status codes. An `InvalidOperationException` maps to 400, everything else to 500. A developer throws `ArgumentNullException` from a service. What status code does the client get? Is this correct?

**E2.** What does this middleware do? Trace the flow for a request that throws `InvalidOperationException("Insufficient stock")`.

```csharp
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (InvalidOperationException ex)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
        }
    }
}
```

**E3.** Trace the full error chain when a concurrency conflict happens during checkout:
```
[EF Core] → [OrderService] → [ExceptionMiddleware] → [HTTP Response] → [error.interceptor] → [user sees ???]
```

---

### HARD

**E4.** Your exception middleware catches `DbUpdateConcurrencyException` and returns 409 Conflict. But in some cases, the caller should retry automatically (background job), and in others, the user should be told to refresh (API call). How do you design the error response to support both consumers?

---

## SECTION F — STRIPE, SIGNALR & CHECKOUT FLOW

### MEDIUM

**F1.** Draw the full Stripe payment flow from button click to order creation:
```
Browser → Stripe.js → Stripe API → Your API → OrderService → DB → Stripe webhook → SignalR → Angular
```
Include: what tokens are passed, where the SecretKey is used, what the webhook verifies.

**F2.** Two webhook callbacks arrive for the same `PaymentIntentId`. How do you prevent duplicate orders?

**F3.** Why is the cart deleted AFTER the transaction commits, not inside it? What happens if cart deletion fails after commit?

**F4.** What is `IdempotencyKey` in Stripe? Why do you use `cart.Id` as the key? What happens if two requests use the same key?

---

### HARD

**F5.** Your Angular app suddenly shows the checkout spinner forever. The payment succeeded (you see the charge in Stripe Dashboard). What's the debugging checklist? (Give at least 6 steps.)

**F6.** `PaymentService` calls `new PaymentIntentService()` and `new RefundService()` directly. You want to unit test it. What are the three options, and which is right for this project?

**F7.** Design a "stock reservation" system: when a user starts checkout, reserve their items for 15 minutes. If they don't complete, release the reservation. Design the data model, reserve/release flows, and the background job. How do you handle a reservation expiring exactly when checkout completes?

---

## SECTION G — PRICE CHANGE DETECTION

### MEDIUM

**G1.** A user adds an item at $50 to their cart. Before checkout, the admin changes the price to $60. The user clicks "Place Order." What should happen? What HTTP status code? What does the user see?

**G2.** Why return 409 (Conflict) instead of 400 (Bad Request) for a price mismatch? What's the semantic difference?

---

## SECTION H — CODE ANALYSIS & DEBUGGING

### MEDIUM

**H1.** This code has a race condition. Identify it and explain how `[ConcurrencyCheck]` fixes it:

```csharp
public async Task PlaceOrder(int productId, int quantity)
{
    var product = await _context.Products.FindAsync(productId);
    if (product.QuantityInStock >= quantity)
    {
        product.QuantityInStock -= quantity;
        await _context.SaveChangesAsync();
    }
}
```

**H2.** This Angular code doesn't update the template. Why? Fix it.

```typescript
@Component({
  template: `<p>{{ user().name }}</p>
             <button (click)="rename()">Rename</button>`
})
export class UserComponent {
  user = signal({ name: 'Alex', age: 25 });

  rename() {
    this.user.update(u => {
      u.name = 'Bob';
      return u;
    });
  }
}
```

---

### HARD

**H3.** This test passes but is worthless. Explain exactly why, step by step:

```csharp
[Fact]
public async Task DeleteCart_WhenCalled_ReturnsTrue()
{
    _mockDb.Setup(x => x.KeyDeleteAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<CommandFlags>()))
        .ReturnsAsync(true);

    var result = await _cartService.DeleteCartAsync("cart-1");

    result.Should().BeTrue();
}
```

Now: what if the production code calls `KeyDeleteAsync` through the `IDatabaseAsync` explicit interface? What changes?

**H4.** Read this code carefully. There are three bugs. Find all three:

```csharp
public async Task<Order?> CreateOrderAsync(string buyerEmail, CreateOrderDto dto)
{
    var cart = await _cartService.GetCartAsync(dto.CartId);
    var items = new List<OrderItem>();

    foreach (var cartItem in cart.Items)
    {
        var product = await _context.Products.FindAsync(cartItem.ProductId);
        var item = new OrderItem(product.Name, product.Price, cartItem.Quantity);
        items.Add(item);
        product.QuantityInStock -= cartItem.Quantity;
    }

    var order = new Order(buyerEmail, dto.ShippingAddress, dto.DeliveryMethodId, items);
    _context.Orders.Add(order);
    await _context.SaveChangesAsync();

    return order;
}
```

**H5.** This Angular interceptor handles errors. What happens if the login page itself makes an API call that returns 401? Describe the infinite loop and how to fix it.

```typescript
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError(error => {
      if (error.status === 401) {
        inject(Router).navigateByUrl('/account/login');
      }
      if (error.status === 400) {
        inject(SnackbarService).error(error.error.message);
      }
      return throwError(() => error);
    })
  );
};
```

---

## SECTION I — SYSTEM DESIGN SCENARIOS

### HARD

**I1.** You're designing the checkout flow for an e-commerce app. Walk through every step from "user clicks Place Order" to "user sees confirmation." Include: client validation, API call, stock check, payment processing, order creation, stock decrement, cart cleanup, real-time notification, and error handling at each step. What happens if the payment succeeds but the order insert fails?

**I2.** Two users add the last item to their carts. Both click "Place Order" at the same time. Trace the exact execution path for both requests through: stock read, stock check, payment intent, `SaveChangesAsync` with `[ConcurrencyCheck]`, exception handling, and user notification. What does each user see?

**I3.** Your app gets featured on Reddit and traffic spikes 50x. The database starts timing out. Your Redis cart store is fine. Walk through your scaling strategy: what do you scale first? What changes in architecture? When do you introduce caching, read replicas, CDN, and queue-based processing?

**I4.** Design an order status tracking system. An order goes through: Pending -> PaymentReceived -> Processing -> Shipped -> Delivered. Each transition should: validate the transition is legal, record the timestamp, notify the buyer via SignalR, and send an email. Design the state machine, the notification system, and the audit trail. How do you prevent an order from going from "Pending" directly to "Delivered"?

**I5.** Your e-commerce app grows to 1000 concurrent users. The optimistic concurrency approach means popular products get frequent `DbUpdateConcurrencyException` during flash sales. Design a better solution. Compare: retry with jitter, atomic SQL decrement, Redis-based stock reservation, and queue-based ordering.

---

## SECTION J — RAPID-FIRE SYNTAX (30 seconds each)

**J1.** What is `@if` vs `*ngIf` in Angular 17+? Why did Angular add the new syntax?

**J2.** What does `inject()` function do in Angular? How is it different from constructor injection?

**J3.** What does `takeUntilDestroyed()` do? Why is it important for preventing memory leaks?

**J4.** What is the difference between `subscribe()` and the `async` pipe? Which one requires manual cleanup?

**J5.** What does `@defer` do in Angular? When would you use it?

**J6.** What is `ChangeDetectionStrategy.OnPush`? What are the three things that trigger change detection in an OnPush component?

---

## SECTION K — BEHAVIORAL & PROBLEM-SOLVING

**K1.** You join a project with 0% test coverage. You have one sprint to improve it. Where do you start? How do you decide what to test first? What coverage target is realistic?

**K2.** You're debugging a race condition that only reproduces under high load. You can't reproduce it locally. What tools and strategies do you use?

**K3.** A colleague pushes a test with no `Verify` and says "it passes, ship it." How do you handle this in code review without being condescending?

---

**Total: ~120 questions across 11 sections**
**Distribution: ~24 Easy (20%) | ~48 Medium (40%) | ~48 Hard (40%)**

---

> "A test that can't fail is worse than no test — it gives you false confidence."
