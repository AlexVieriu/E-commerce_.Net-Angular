# Frontend Error Handling

All HTTP errors handled centrally by `errorInterceptor` — components don't catch HTTP errors directly.
Full status-code → action table lives in [`interceptors.md`](interceptors.md); this doc covers what
happens downstream of that interceptor.

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

- Never catch `HttpErrorResponse` in a component.
- Use `ValidationError instanceof` check for form-level errors (400 with `errors` field).
- `error` in `server-error` must be typed `ApiErrorResponse | undefined` — not `any`.
- Read router state via `history.state`, not `router.getCurrentNavigation()` (returns null post-construction).
