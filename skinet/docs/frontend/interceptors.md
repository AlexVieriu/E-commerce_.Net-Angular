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

This app's chain, both directions:

```
down:  errorInterceptor  ->  loadingInterceptor  ->  authInterceptor  ->  network
       (does nothing)        starts the counter      adds the cookie

up:    errorInterceptor  <-  loadingInterceptor  <-  authInterceptor  <-  response
       catchError: 401/409/...   stops the counter    (does nothing)
```

First on the way down is last on the way up. That is why `errorInterceptor` sees errors thrown by every
interceptor after it, not only the network's.

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
| 409 | Snackbar. Nothing else — see below |
| 404 | Navigate `/not-found` |
| 500 | Navigate `/server-error` (error in router state) |

**Snackbar message:** `err.error?.title || err.error?.message || err.error`, with a per-status fallback.
The body is `any | null` and an empty one is real (a 401 here carries `Content-Length: 0`); an unguarded
deref throws a `TypeError` *inside* the handler, and RxJS then delivers that instead of the
`HttpErrorResponse` — breaking every `instanceof HttpErrorResponse` check downstream.

**409 detail:** Two exceptions map to 409 — `PriceChangedException` (checkout price change or a retired
coupon) and `DbUpdateConcurrencyException` (admin product edit, BUG-16). The interceptor shows the message
and stops there. Reloading the stale cart and returning to `/cart` is `CheckoutConflictService`, called by
the two services that own the endpoints that can 409: `StripeService.createOrUpdatePaymentIntent()` and
`OrderService.createOrder()`. Every caller funnels through one of them — including the coupon box in
`order-summary`, which is why a component-level catch was not enough. The admin's product form is spared
by construction: it never calls either service.

**401 detail:** Injects `AccountService`, calls `clearCurrentUser()` — nulls the `currentUser` signal +
stops the SignalR hub — so the navbar drops to Login/Register when the cookie expired or was cleared
out-of-band. Redirects unless already on `/account/login` (a failed login 401s, and would otherwise
redirect to the page it is on). There is **no exemption for the startup probe**: `GET /account/user-info`
answers an anonymous or dead-cookie caller with **204**, never 401 (`backend/auth.md`), so the startup path
never reaches this branch.

## Rules / Constraints

- `authInterceptor` must be last — closest to the actual request.
- `errorInterceptor` must be first — runs last on responses.
- Register new interceptors in `app.config.ts`.
- Components handling validation errors: check `err instanceof ValidationError`.
- **An interceptor translates a status; it does not run domain work.** Reloading data, clearing state or
  routing off the back of a status belongs to the service that owns the request — it is the only one that
  knows what the status means for its own call. The same rule as `guards.md`'s "a guard answers, it does
  not navigate".
- **Never branch on a URL string.** It matches by accident (`endsWith('/orders')` also matches
  `/api/admin/orders`), detaches silently on a rename, and nothing fails when it does.
- Guard every `err.error` deref with `?.` — see the snackbar note above.
- Every branch exits the same way: `return throwError(() => err)`. A bare `throw` leaves the callback, so
  the checks below it never run and a reader has to prove that for themselves.
