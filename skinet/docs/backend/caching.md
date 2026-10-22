# Caching

Response caching via Redis. Cache keyed by request path + sorted query params.

## Redis Databases

| DB | Purpose | Key |
|---|---|---|
| `0` | Shopping cart (`CartService`) | `cart:{user id}` — derived from the caller, never sent by the client (see `auth.md`) |
| `1` | Response cache (`ResponseCacheService`) | request path + sorted query params, below |

## Cache Key Format

```
/api/products|brandId-2|typeId-1   ← list: path + one "|key-value" per query param
/api/products/72                   ← by id: path only, no "|" at all
```

The path is lowercased, so `/api/Products/72` and `/api/products/72` share one key.
Routing is case-insensitive; Redis pattern matching is not — without this, one casing
writes the cache and the other never invalidates it.

## [Cache] Attribute

```csharp
[Cache(180)]  // cache for 180 minutes
```

1. Key exists → return cached JSON (skips controller + DB).
2. Key missing → execute action, store `OkObjectResult` in Redis with TTL.

Services can't be injected into attributes — resolved via `context.HttpContext.RequestServices`.

## [InvalidateCache] Attribute

```csharp
[InvalidateCache("api/products")]  // no trailing "|" — see Rules
```

Runs the action first, then deletes all Redis keys matching the pattern. Skips only when the action throws — a 400 result still invalidates.

## IResponseCacheService

| Method | What it does |
|---|---|
| `GetCachedResponseAsync(key)` | Returns cached JSON or empty string |
| `CreateCacheResponseAsync(key, obj, ttl)` | Serializes camelCase JSON, stores with TTL |
| `RemoveCacheByPattern(pattern)` | SCAN + bulk delete matching keys |

## Rules / Constraints

- `[Cache]` only caches `OkObjectResult` — errors are never cached.
- `[InvalidateCache]` pattern must match **every** key shape for that resource, not just the common one.
  `"api/products"` clears both the list keys and the by-id keys. A trailing `"|"` clears only the list
  keys — by-id stays stale (BUG-15). A pattern that matches nothing no-ops silently: no error, no log.

## Common Mistakes

- Adding a new mutating endpoint without `[InvalidateCache]` — invalidation is TTL-only otherwise, so
  stale cached data lingers until the cache entry expires on its own, not when the underlying data changes.
