# Interceptors

Functional interceptors in `app.config.ts`. Order: error → loading → auth (request); reversed on response.

## What an interceptor is

- A function run **for every `HttpClient` request**, sitting between the calling code and the network.
- `req` is the **outgoing** request — the one heading to the backend. It is immutable: change it by
  `req.clone({ ... })`.
- `next(req)` hands the request to the next interceptor, or to the backend if none are left, and returns
  an Observable of the **response** event stream.
- So an interceptor works in two directions: it can modify the request on the way down, and pipe operators
  onto the response stream on the way back up.
- Interceptors are chained in the order they are listed in the providers.
- Rarely, one may skip `next()` and answer the request itself — every interceptor after it is then skipped.

Sources: [HTTP interceptors](https://angular.dev/guide/http/interceptors) ·
`@angular/common` `HttpInterceptorFn` / `HttpHandlerFn` TSDoc.

## authInterceptor

Clones every request with `withCredentials: true`. Sends the Identity cookie automatically.

## loadingInterceptor

`busyService.busy()` and `next(req)` run **inside a `defer` factory**; `busyService.idle()` is piped on
**after** it, in `finalize`. That order is the point: a synchronous throw from a later interceptor arrives
as an error notification on a stream that already carries `finalize`, so the counter returns to 0 instead
of stranding at 1 for the session. With `busy()` on a bare line, `.pipe(...)` was never reached.

`finalize` also fires on **unsubscribe** — a route change mid-request clears the bar with no response and
no error. 500ms artificial delay in development only; `delay` does not delay errors, so a failed request
is not slowed.

The counter is a signal (`BusyService`) because the app is zoneless — a plain field would never re-render
the bar. It is a **count, not a boolean**: with requests overlapping, the bar clears when the last one
ends, not the first.

## errorInterceptor

| Status | Action |
|---|---|
| 400 (validation) | Throws `ValidationError(errors[])` |
| 400 (other) | Snackbar |
| 401 | Clear auth state (`clearCurrentUser`) + redirect `/account/login` |
| 403 | Snackbar "Forbidden" |
| 409 | Snackbar; **checkout only** also reloads the cart from Redis + navigates `/cart` |
| 404 | Navigate `/not-found` |
| 500 | Navigate `/server-error` (error in router state) |

**Snackbar message:** `err.error.title || err.error.message || err.error`

**409 detail:** Two exceptions map to 409 — `PriceChangedException` (checkout price change) and `DbUpdateConcurrencyException` (admin product edit, BUG-16). Both show the snackbar. Only checkout also injects `CartService`, calls `getCartAsync()` to refresh the signal with updated Redis prices, then navigates `/cart` — gated on `req.method === 'POST' && (req.url.endsWith('/orders') || req.url.endsWith('/payments'))` — `/payments` was added by BUG-19, where a retired coupon 409s on the intent call. Without that gate, an admin losing an edit conflict would be thrown from the product form into the shopping cart.

⚠️ The gate is a URL string match, so renaming or versioning the orders or payments route silently detaches it (no test pins it). Tracked as a follow-up in *Pull-in candidates* (`Execution plan.md`) — move the branch out of the interceptor into the checkout flow.

**401 detail:** Injects `AccountService`, calls `clearCurrentUser()` — nulls the `currentUser` signal + stops the SignalR hub — so the navbar drops to Login/Register when the cookie expired or was cleared out-of-band. Skips the redirect for the `account/user-info` probe and when already on `/account/login`.

## Rules / Constraints

- `authInterceptor` must be last — closest to the actual request.
- `errorInterceptor` must be first — runs last on responses.
- Register new interceptors in `app.config.ts`.
- Components handling validation errors: check `err instanceof ValidationError`.
