## SECTION G — PRICE CHANGE DETECTION

### MEDIUM

**G1.** A user adds an item at $50 to their cart. Before checkout, the admin changes the price to $60. The user clicks "Place Order." What should happen? What HTTP status code? What does the user see?

<details><summary>Answer</summary>

The `OrderService` should compare each cart item's stored price against the current product price in the DB. If any mismatch is found:

- Return **409 Conflict** — the cart state conflicts with current product state
- Response body: `{ "type": "PriceConflict", "message": "Prices have changed. Please review your cart." }`
- Angular: show a dialog listing the changed items with old → new prices, with a "Review Cart" button
- User must acknowledge the change before proceeding — the cart is updated with the new prices

The order is **not created**. The user is shown the updated price and must re-confirm.
</details>

<details><summary>Deep Dive</summary>

**Where the check lives**

The check belongs in `OrderService.CreateOrderAsync`, not in the controller and not in the cart. The controller doesn't have access to current prices. The cart has stale prices by design (it's a snapshot). The service is the enforcement point.

```csharp
public async Task<OrderResult> CreateOrderAsync(string buyerEmail, string cartId, ...)
{
    var cart = await _cartService.GetCartAsync(cartId);
    var priceConflicts = new List<PriceConflictItem>();

    foreach (var cartItem in cart.Items)
    {
        var product = await _context.Products.FindAsync(cartItem.ProductId);
        if (product == null) throw new ProductNotFoundException(cartItem.ProductId);

        if (product.Price != cartItem.Price)
        {
            priceConflicts.Add(new PriceConflictItem
            {
                ProductName = product.Name,
                CartPrice = cartItem.Price,
                CurrentPrice = product.Price
            });
        }
    }

    if (priceConflicts.Any())
        throw new PriceConflictException(priceConflicts);

    // ... proceed with order creation
}
```

**The exception + middleware chain**

```csharp
// Domain exception
public class PriceConflictException : Exception
{
    public List<PriceConflictItem> Conflicts { get; }
    public PriceConflictException(List<PriceConflictItem> conflicts)
        : base("Cart prices are outdated") => Conflicts = conflicts;
}

// ExceptionMiddleware maps it
catch (PriceConflictException ex)
{
    var problem = new ProblemDetails
    {
        Status = 409,
        Type = "https://yourapi.com/errors/price-conflict",
        Title = "Prices have changed",
    };
    problem.Extensions["conflicts"] = ex.Conflicts;
    context.Response.StatusCode = 409;
    await context.Response.WriteAsJsonAsync(problem);
}
```

**Angular handles it**

```typescript
// checkout.component.ts
placeOrder() {
  this.orderService.createOrder(this.orderDto).subscribe({
    error: (err) => {
      if (err.status === 409 && err.error.type?.includes('price-conflict')) {
        this.conflicts = err.error.extensions.conflicts;
        this.showPriceConflictModal = true; // component shows updated prices
      }
    }
  });
}
```

**What "update the cart" means**

After showing the conflict dialog, update the cart item prices to the current values:

```csharp
// CartService — called after user acknowledges
await _cartService.UpdatePricesAsync(cartId, updatedPrices);
```

The user then clicks "Place Order" again with the corrected cart. On the second attempt, prices match → order is created.

**Should you auto-accept the price increase?**

No. Silently charging the user more than they expected is a trust violation — and potentially illegal in some jurisdictions (consumer protection laws require disclosed pricing before charge). Always get explicit confirmation.
</details>

---

**G2.** Why return 409 (Conflict) instead of 400 (Bad Request) for a price mismatch? What's the semantic difference?

<details><summary>Answer</summary>

- **400 Bad Request** — the client sent malformed or invalid data. The request itself is wrong (missing field, wrong format, validation failure). The client should fix their request.
- **409 Conflict** — the request is syntactically valid but conflicts with the current state of the server. The client's data was correct when they assembled it, but the server's state has changed.

A price mismatch is a **state conflict** — the cart was built with $50 (correct at that moment), but the server now says $60. The request is well-formed. The problem is the world changed between cart creation and checkout. That's 409.

Using 400 would mislead the client into thinking their request was badly formed and they need to re-enter data. 409 tells them to sync with the server's current state.
</details>

<details><summary>Deep Dive</summary>

**HTTP status code semantics — the full picture**

| Code | Meaning | When to use |
|------|---------|-------------|
| 400 | Bad Request | Malformed JSON, missing required field, validation error |
| 401 | Unauthorized | Not authenticated |
| 403 | Forbidden | Authenticated but not authorized |
| 404 | Not Found | Resource doesn't exist |
| 409 | Conflict | Valid request, server state conflict (version mismatch, price change, stock conflict) |
| 422 | Unprocessable Entity | Semantically invalid (e.g. discount > 100%) |
| 429 | Too Many Requests | Rate limiting |
| 500 | Internal Server Error | Unexpected exception |

**Why consistency in status codes matters to Angular**

Angular's `error.interceptor` switches on `error.status`. If you use 400 for everything, the interceptor can't distinguish "you sent bad data" from "the price changed" without parsing the response body every time. With distinct status codes, the interceptor routes each case to the correct UX action:

```typescript
case 400: showValidationErrors(error.error); break;
case 409: showConflictResolution(error.error); break;
```

**409 vs 422 — when to use each for business validation**

- `409` = state conflict (something changed on the server side — the client didn't do anything wrong at request time)
- `422` = semantic validation (the data is internally inconsistent — e.g. `endDate < startDate`)

Price mismatch → 409. Discount exceeding order total → 422. Both are "valid JSON, invalid semantics," but the cause differs: one is server state drift, the other is client logic error.

**Real-world usage: GitHub uses 409 for merge conflicts**

When a GitHub API call tries to merge a PR that's since fallen behind main, GitHub returns 409. Same principle: the request was valid when formed, the server's state moved on.
</details>

---
