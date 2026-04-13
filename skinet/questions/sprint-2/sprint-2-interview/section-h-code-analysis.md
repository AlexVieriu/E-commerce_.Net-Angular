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

---
