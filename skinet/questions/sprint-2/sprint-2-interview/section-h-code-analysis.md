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

<details><summary>Answer</summary>

**The race condition:** two concurrent requests both read `QuantityInStock = 1`. Both pass the `>= quantity` check (assuming quantity=1). Both decrement, resulting in `QuantityInStock = -1`. One item sold twice.

**How `[ConcurrencyCheck]` fixes it:**

```csharp
public class Product
{
    [ConcurrencyCheck]
    public int QuantityInStock { get; set; }
}
```

EF Core now generates: `UPDATE Products SET QuantityInStock = @new WHERE Id = @id AND QuantityInStock = @original`

- Request 1 updates from 1 → 0. Succeeds (1 row affected).
- Request 2 tries to update from 1 → 0, but the DB now shows 0. 0 rows affected → EF throws `DbUpdateConcurrencyException`.
- Catch the exception → return 409 Conflict → user sees "item sold out."
</details>

<details><summary>Deep Dive</summary>

**The read-check-write gap**

```
t=0ms:  Request A reads stock=1 ─────────────────────────────┐
t=1ms:  Request B reads stock=1 ────────────────────────┐    │
t=2ms:  Request A: 1 >= 1? Yes ─ decrement ─ SaveChanges│    │
t=3ms:  Request B: 1 >= 1? Yes ─ decrement ─ SaveChanges┘    │
t=4ms:  DB: stock=-1 ← oversell ─────────────────────────────┘
```

The gap between `FindAsync` and `SaveChangesAsync` is the danger zone. Any concurrent request can read the same stale value.

**The SQL EF Core generates with `[ConcurrencyCheck]`**

```sql
-- Without [ConcurrencyCheck]
UPDATE "Products" SET "QuantityInStock" = 0
WHERE "Id" = 1;

-- With [ConcurrencyCheck]
UPDATE "Products" SET "QuantityInStock" = 0
WHERE "Id" = 1 AND "QuantityInStock" = 1;
-- Returns 0 rows affected when concurrent update wins → DbUpdateConcurrencyException
```

**Full implementation with exception handling**

```csharp
public async Task<Result> PlaceOrderAsync(int productId, int quantity)
{
    var product = await _context.Products.FindAsync(productId);
    if (product == null) return Result.NotFound();
    if (product.QuantityInStock < quantity) return Result.InsufficientStock();

    product.QuantityInStock -= quantity;

    try
    {
        await _context.SaveChangesAsync();
        return Result.Success();
    }
    catch (DbUpdateConcurrencyException)
    {
        // Reload the current state and recheck
        await _context.Entry(product).ReloadAsync();
        return product.QuantityInStock < quantity
            ? Result.InsufficientStock()   // 409 — someone else got it
            : Result.Conflict();           // 409 — retry
    }
}
```

**Alternative: atomic SQL decrement (no ORM overhead)**

```csharp
var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
    "UPDATE Products SET QuantityInStock = QuantityInStock - {0} WHERE Id = {1} AND QuantityInStock >= {0}",
    quantity, productId);

if (rowsAffected == 0) return Result.InsufficientStock(); // concurrent update won
```

This skips the read-check-write pattern entirely. The condition and decrement happen atomically in one SQL statement. No `DbUpdateConcurrencyException` needed — rows affected is the signal.
</details>

---

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

<details><summary>Answer</summary>

**Why it doesn't update:** the signal holds an object reference. `update()` mutates the SAME object (`u.name = 'Bob'`) and returns the same reference. Angular's signal equality check uses `===` — same reference → no change detected → template does not re-render.

**Fix:** return a NEW object instead of mutating the existing one:

```typescript
rename() {
  this.user.update(u => ({ ...u, name: 'Bob' }));
}
```

The spread creates a new object at a new memory address. `===` comparison fails → signal sees a change → template re-renders with `Bob`.
</details>

<details><summary>Deep Dive</summary>

**Signal equality — how it works under the hood**

Angular signals use a versioned comparison. When you call `.set()` or `.update()`, Angular compares the new value to the previous using `Object.is()` (same as `===` for objects). If equal, the signal is considered unchanged and no consumers are notified.

```javascript
Object.is({ name: 'Bob' }, { name: 'Bob' }) // false — different object references
Object.is(obj, obj)                          // true — same reference (your bug)
```

**Three ways to trigger a signal update with an object**

```typescript
// 1. Spread (most common)
this.user.update(u => ({ ...u, name: 'Bob' }));

// 2. Structured clone (deep clone — for nested objects)
this.user.update(u => structuredClone({ ...u, name: 'Bob' }));

// 3. set() with a new object
this.user.set({ ...this.user(), name: 'Bob' });
```

**Custom equality function — opt out of reference checking**

If you WANT mutation to trigger updates, provide a custom equality function:

```typescript
user = signal({ name: 'Alex', age: 25 }, {
  equal: (a, b) => a.name === b.name && a.age === b.age
});
```

Now Angular compares properties, not references. Mutation that changes `name` will be detected. Use sparingly — deep equality checks have a cost and make signal behavior less predictable.

**The same issue with arrays**

```typescript
items = signal<string[]>(['a', 'b']);

// BUG — mutates the same array
addItem() {
  this.items.update(arr => { arr.push('c'); return arr; }); // same reference
}

// CORRECT — new array
addItem() {
  this.items.update(arr => [...arr, 'c']);
}
```

**Why this matters for `ChangeDetectionStrategy.OnPush`**

OnPush components only re-check when signals emit new values or `@Input()` references change. A mutated-but-same-reference signal produces the same "no change" outcome — the component stays stale. Immutability is essential for OnPush to work correctly.
</details>

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

<details><summary>Answer</summary>

**Why it's worthless — step by step:**

1. `Setup` configures what `KeyDeleteAsync` returns *if called* — it's not an assertion that it WILL be called.
2. `DeleteCartAsync` is invoked.
3. `result.Should().BeTrue()` only checks the return value of the SERVICE method — not whether Redis was called.
4. **No `Verify`** — nothing asserts that `KeyDeleteAsync` was actually invoked.
5. If `DeleteCartAsync` is rewritten to return `true` without calling Redis at all, this test still passes.
6. If `DeleteCartAsync` is deleted entirely → compile error, but that's the compiler, not the test.

The test proves "the method returns `true`" and nothing else. It does not prove Redis was called, the correct key was used, or any deletion actually happened.

**If production code calls through `IDatabaseAsync` explicit interface:**

`_mockDb` is `Mock<IDatabase>`. The `Setup` targets `IDatabase.KeyDeleteAsync`. If the code calls `((IDatabaseAsync)_db).KeyDeleteAsync(...)`, Moq looks for a setup on the `IDatabaseAsync` interface face of the mock — finds none → returns default (`false`). Now `result.Should().BeTrue()` FAILS. The test was broken all along but for the wrong reasons — it was passing only because the setup accidentally matched the non-async overload being called directly on `IDatabase`.
</details>

<details><summary>Deep Dive</summary>

**The correct test — with Verify**

```csharp
[Fact]
public async Task DeleteCart_WhenCalled_CallsKeyDeleteWithCorrectKey()
{
    // Arrange
    _mockDb.Setup(x => x.KeyDeleteAsync(
        It.IsAny<RedisKey>(),
        It.IsAny<CommandFlags>()))
        .ReturnsAsync(true);

    // Act
    var result = await _cartService.DeleteCartAsync("cart-1");

    // Assert — return value
    result.Should().BeTrue();

    // Assert — Redis was actually called with the right key
    _mockDb.Verify(x => x.KeyDeleteAsync(
        It.Is<RedisKey>(k => k == "cart-1"),
        It.IsAny<CommandFlags>()),
        Times.Once); // ← this is what makes the test meaningful
}
```

Now: delete the Redis call from `DeleteCartAsync` → `Verify` throws `MockException` → test fails → regression caught.

**Fixing the IDatabaseAsync interface mismatch**

```csharp
// If production code holds IDatabaseAsync reference:
public class CartService
{
    private readonly IDatabaseAsync _db; // IDatabaseAsync, not IDatabase
    public CartService(IDatabaseAsync db) { _db = db; }
}

// Test must target the correct interface face:
var mockDb = new Mock<IDatabase>(); // create mock of IDatabase

// WRONG — sets up on IDatabase, but code calls IDatabaseAsync
mockDb.Setup(x => x.KeyDeleteAsync(...)).ReturnsAsync(true);

// CORRECT — cast to IDatabaseAsync
mockDb.As<IDatabaseAsync>()
      .Setup(x => x.KeyDeleteAsync(...))
      .ReturnsAsync(true);

// And Verify on the same face:
mockDb.As<IDatabaseAsync>()
      .Verify(x => x.KeyDeleteAsync(It.Is<RedisKey>(k => k == "cart-1"), It.IsAny<CommandFlags>()),
              Times.Once);
```

**MockBehavior.Strict — catch the wrong interface immediately**

```csharp
var mockDb = new Mock<IDatabase>(MockBehavior.Strict);
// Any call without a matching Setup throws MockException immediately
// Exposes the interface mismatch the moment the test runs
```

**What to say in code review**

> "This test would pass even if we removed the Redis call entirely — there's no `Verify`. Add `_mockDb.Verify(x => x.KeyDeleteAsync(...), Times.Once)` to prove the deletion actually happened. Also check that the setup targets the right interface — if the service holds `IDatabaseAsync`, you need `mockDb.As<IDatabaseAsync>().Setup(...)`."
</details>

---

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

<details><summary>Answer</summary>

**Bug 1: `cart` can be null — no null check.**
`GetCartAsync` returns `null` if the cart doesn't exist (expired, wrong ID). `cart.Items` throws `NullReferenceException`. Fix: `if (cart == null) return null;` (or throw `CartNotFoundException`).

**Bug 2: `product` can be null — no null check.**
`FindAsync(cartItem.ProductId)` returns `null` if the product was deleted. `product.Name` throws `NullReferenceException`. Fix: `if (product == null) throw new ProductNotFoundException(cartItem.ProductId);`.

**Bug 3: No stock validation before decrement.**
`product.QuantityInStock -= cartItem.Quantity` can go negative. There's no check that `product.QuantityInStock >= cartItem.Quantity`. Two items ordered when only one is in stock results in `QuantityInStock = -1`. Fix: add `if (product.QuantityInStock < cartItem.Quantity) throw new InsufficientStockException(...)` before the decrement.
</details>

<details><summary>Deep Dive</summary>

**Fixed version with all three bugs addressed**

```csharp
public async Task<Order?> CreateOrderAsync(string buyerEmail, CreateOrderDto dto)
{
    // Bug 1 fix: null check on cart
    var cart = await _cartService.GetCartAsync(dto.CartId);
    if (cart == null) return null;

    var items = new List<OrderItem>();

    foreach (var cartItem in cart.Items)
    {
        // Bug 2 fix: null check on product
        var product = await _context.Products.FindAsync(cartItem.ProductId);
        if (product == null)
            throw new ProductNotFoundException(cartItem.ProductId);

        // Bug 3 fix: stock validation before decrement
        if (product.QuantityInStock < cartItem.Quantity)
            throw new InsufficientStockException(product.Id, cartItem.Quantity, product.QuantityInStock);

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

**Hidden bug 4: price isn't validated against current DB price**

The code uses `product.Price` (the current DB price), which is correct. But it doesn't validate against `cartItem.Price` (the price when the user added to cart). If the price changed, the order is created at the NEW price — possibly more than the user expected. This is the price conflict bug from Section G.

**Hidden bug 5: N+1 query**

`FindAsync(cartItem.ProductId)` inside a `foreach` loop hits the DB once per cart item. A cart with 10 items = 10 separate DB calls. Fix: load all products in one query before the loop:

```csharp
var productIds = cart.Items.Select(i => i.ProductId).ToList();
var products = await _context.Products
    .Where(p => productIds.Contains(p.Id))
    .ToDictionaryAsync(p => p.Id);

foreach (var cartItem in cart.Items)
{
    if (!products.TryGetValue(cartItem.ProductId, out var product))
        throw new ProductNotFoundException(cartItem.ProductId);
    // ...
}
```

**Hidden bug 6: Cart is not deleted after order creation**

The code creates the order but leaves the cart in Redis. The user could theoretically re-use the same cart to create another order. After a successful commit, add: `await _cartService.DeleteCartAsync(dto.CartId);`
</details>

---

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

<details><summary>Answer</summary>

**The infinite loop:**

1. User is on `/account/login`
2. Login form calls `POST /api/account/login` with wrong password → API returns 401
3. Interceptor catches 401 → `router.navigateByUrl('/account/login')`
4. Angular navigates to `/account/login` again → component reinitialises → might make another API call → 401 → navigate again → repeat

Even without an explicit API call on init, Angular re-renders the login component each time, which can trigger `ngOnInit` HTTP calls (e.g. "check if token exists" or "load OAuth config").

**Fix:** skip the 401 navigation if the request URL is the login endpoint, or if the user is already on the login page:

```typescript
if (error.status === 401 && !req.url.includes('/account/login')) {
  inject(Router).navigateByUrl('/account/login');
}
```
</details>

<details><summary>Deep Dive</summary>

**Why `router.navigateByUrl` alone doesn't cause infinite navigation**

Angular Router won't re-navigate if you're already on the target URL (it's a no-op). But the issue is that the interceptor fires BEFORE navigation completes, and the login page's own API calls each trigger 401 → navigate → re-render → API call → 401 → ... The navigation itself may not repeat, but the component lifecycle does.

**More robust fix: check both URL and current route**

```typescript
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);

  return next(req).pipe(
    catchError(error => {
      if (error.status === 401) {
        const isLoginRequest = req.url.includes('/account/login');
        const isAlreadyOnLogin = router.url === '/account/login';

        if (!isLoginRequest && !isAlreadyOnLogin) {
          router.navigateByUrl('/account/login');
        }
      }
      if (error.status === 400) {
        inject(SnackbarService).error(error.error?.message ?? 'Bad request');
      }
      return throwError(() => error);
    })
  );
};
```

**401 vs 403 — make sure you're handling the right one**

- 401: not authenticated (no token, expired token) → redirect to login
- 403: authenticated but not authorized (wrong role) → show "Access Denied" page, NOT login

Many interceptors incorrectly redirect 403 to login — confusing the user who IS logged in but doesn't have permission.

```typescript
if (error.status === 401 && !req.url.includes('/account/login')) {
  router.navigateByUrl('/account/login');
}
if (error.status === 403) {
  router.navigateByUrl('/forbidden');
}
```

**The `error.error.message` null access**

`error.error.message` will throw if the 400 response body is not `{ "message": "..." }`. Safe access:

```typescript
if (error.status === 400) {
  const message = error.error?.message ?? error.error?.title ?? 'Bad request';
  inject(SnackbarService).error(message);
}
```

**Testing interceptors**

```typescript
// Use HttpTestingController to simulate error responses
TestBed.configureTestingModule({
  providers: [provideHttpClient(withInterceptors([errorInterceptor])), provideHttpClientTesting()]
});
const httpTesting = TestBed.inject(HttpTestingController);

service.getData().subscribe({ error: () => {} });
httpTesting.expectOne('/api/data').flush({ error: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
// Assert: router.navigateByUrl was called
```
</details>

---
