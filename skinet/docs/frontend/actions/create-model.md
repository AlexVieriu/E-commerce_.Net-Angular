# How to: Create a Model

## When to use this

Adding a TypeScript type that mirrors a backend DTO.

## Steps

1. Create `shared/models/<domain>.ts` (one file per domain concept).
2. Match backend DTO property names exactly — `HttpClient` deserializes JSON as-is (camelCase).
3. Use `type`, not `interface`, for plain data shapes.
4. Export it. Import wherever needed.

## Example

Backend DTO:
```csharp
public class ApiErrorResponse(int statusCode, string message, string? details)
```

Frontend model (`shared/models/apiErrorResponse.ts`):
```typescript
export type ApiErrorResponse = {
  statusCode: number;
  message: string;
  details?: string;
}
```

## Why `type` and not `interface`

`interface` allows **declaration merging** — two `interface Product` declarations in scope silently fuse
into one. For a model that mirrors a backend contract, that's a footgun: the type would claim fields the
API never sends. `type` cannot be merged — what you declare is what you get.

ESLint's `@typescript-eslint/consistent-type-definitions` disagrees and wants `interface`. It is
**deliberately disabled** in `eslint.config.js` for this reason (T54). Don't "fix" it.

## Pitfalls

- Using `interface` for plain data — `type` is preferred for DTOs in this codebase (see above).
- Putting models in a feature folder — models go in `shared/models/` only.
- Property name mismatch with backend DTO — JSON will silently not map.
- **A hand-written model is an assertion, not a guarantee.** BUG-11/BUG-13 were caused by models that
  claimed `string` where the API sends `string?`. Match the backend's nullability *exactly*.
