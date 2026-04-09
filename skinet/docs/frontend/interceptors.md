# Interceptors

Functional interceptors in `app.config.ts`. Order: error → loading → auth (request); reversed on response.

## authInterceptor

Clones every request with `withCredentials: true`. Sends the Identity cookie automatically.

## loadingInterceptor

`busyService.busy()` before request, `busyService.idle()` after (`finalize`).
500ms artificial delay in development only.

## errorInterceptor

| Status | Action |
|---|---|
| 400 (validation) | Throws `ValidationError(errors[])` |
| 400 (other) | Snackbar |
| 401 | Redirect `/account/login` |
| 403 | Snackbar "Forbidden" |
| 409 | Snackbar + reload cart from Redis + navigate `/cart` |
| 404 | Navigate `/not-found` |
| 500 | Navigate `/server-error` (error in router state) |

**Snackbar message:** `err.error.title || err.error.message || err.error`

**409 detail:** Injects `CartService`, calls `getCartAsync()` to refresh the signal with updated prices from Redis before navigating. Used exclusively for price change detection.

## Rules / Constraints

- `authInterceptor` must be last — closest to the actual request.
- `errorInterceptor` must be first — runs last on responses.
- Register new interceptors in `app.config.ts`.
- Components handling validation errors: check `err instanceof ValidationError`.
