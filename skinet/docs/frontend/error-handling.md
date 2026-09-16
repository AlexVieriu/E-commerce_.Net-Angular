# Frontend Error Handling

Every HTTP error is messaged centrally by `errorInterceptor` — a component never shows the message itself.
Full status-code → action table lives in [`interceptors.md`](interceptors.md); this doc covers what
happens downstream of that interceptor.

**Who catches what**

| Layer | Job |
|---|---|
| `errorInterceptor` | translate the status: snackbar, or a destination. Nothing domain-specific |
| the service owning the request | recover — reload stale data, then re-throw (`CheckoutConflictService` on a 409) |
| the component | only to *suppress* a message the interceptor already showed, or to handle a non-HTTP error |

## ValidationError

Thrown instead of returning so components can use `instanceof`:

```typescript
error: (err: unknown) => {
  if (err instanceof ValidationError) this.validationErrors.set(err.errors);
}
```

## server-error Component

Reads error from `history.state` (set when error interceptor calls `router.navigateByUrl('/server-error', { state: { error } })`):

```typescript
readonly error: ApiErrorResponse | undefined = history.state['error'];
```

`ApiErrorResponse` type: `shared/models/apiErrorResponse.ts`.

## Rules / Constraints

- Never catch `HttpErrorResponse` in a component **to message it** — the interceptor already did, and the
  customer gets the same thing twice. Catching it to stay silent is fine and is what
  `checkout.component.ts` does on a 409/401 it knows is already handled.
- Use `ValidationError instanceof` check for form-level errors (400 with `errors` field).
- `error` in `server-error` must be typed `ApiErrorResponse | undefined` — not `any`.
- Read router state via `history.state`, not `router.getCurrentNavigation()` (returns null post-construction).
