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

---

**G2.** Why return 409 (Conflict) instead of 400 (Bad Request) for a price mismatch? What's the semantic difference?

<details><summary>Answer</summary>

- **400 Bad Request** — the client sent malformed or invalid data. The request itself is wrong (missing field, wrong format, validation failure). The client should fix their request.
- **409 Conflict** — the request is syntactically valid but conflicts with the current state of the server. The client's data was correct when they assembled it, but the server's state has changed.

A price mismatch is a **state conflict** — the cart was built with $50 (correct at that moment), but the server now says $60. The request is well-formed. The problem is the world changed between cart creation and checkout. That's 409.

Using 400 would mislead the client into thinking their request was badly formed and they need to re-enter data. 409 tells them to sync with the server's current state.
</details>

---
