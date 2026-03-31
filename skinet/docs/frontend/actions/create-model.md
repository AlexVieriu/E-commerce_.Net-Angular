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

## Pitfalls

- Using `interface` for plain data — `type` is preferred for DTOs in this codebase.
- Putting models in a feature folder — models go in `shared/models/` only.
- Property name mismatch with backend DTO — JSON will silently not map.
