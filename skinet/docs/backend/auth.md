# Authentication & Authorization

Cookie-based auth via ASP.NET Identity. No JWT.

## Setup

```csharp
builder.Services.AddIdentityApiEndpoints<AppUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<StoreContext>();

app.MapGroup("api").MapIdentityApi<AppUser>();
```

`MapIdentityApi` provides `/api/login`, `/api/register`. Custom endpoints in `AccountController`.

## AccountController Endpoints

| Method | Route | Auth | Notes |
|---|---|---|---|
| POST | `/api/account/register` | No | Returns `ValidationProblem` on failure |
| POST | `/api/account/logout` | No | `SignOutAsync()` |
| GET | `/api/account/user-info` | No | 204 if unauthenticated, user object if authenticated |
| GET | `/api/account/auth-status` | No | `{ isAuthenticated: bool }` |
| POST | `/api/account/address` | `[Authorize]` | Create or update address |

## ClaimsPrincipleExtensions

`API/Extensions/ClaimsPrincipleExtensions.cs` — the class name keeps the code's spelling. The first two
extend `UserManager<AppUser>` and take the `ClaimsPrincipal`.

| Method | What it does |
|---|---|
| `GetUserByEmailAsync(user)` | Loads user by email claim, throws `AuthenticationException` if not found |
| `GetUserByEmailWithAddressAsync(user)` | Same + `.Include(x => x.Address)` |
| `GetEmail(user)` | Extracts `ClaimTypes.Email`, throws if missing |
| `GetUserId(user)` | Extracts `ClaimTypes.NameIdentifier`, throws if missing |

## Object-level authorization

`[Authorize]` answers *"are you someone?"*. It does **not** answer *"is this record yours?"*. Any endpoint
that takes a record id from the client and returns user-owned data must answer the second question too —
otherwise any logged-in user can read another's data by changing an id (OWASP API1:2023, "IDOR").

**The rule here: don't check the id — remove it.** Derive the key from the caller, so there is no
parameter to forge and no check a future endpoint can forget.

```
              request body / query string  ──✗  never a source of identity
   auth cookie ──> ICurrentUser.Id ──> "cart:{userId}" ──> Redis
```

`ICurrentUser` (one property, `Id`) lives in `Application`; `CurrentUser` implements it in `API` over
`IHttpContextAccessor`, because `HttpContext` lives there and `Infrastructure` cannot reference `API`.
Registered with `AddHttpContextAccessor()` in `ApplicationServicesExtensions`.

**Why the user id and not the email:** stable across an email change, not PII in a Redis key or a log
line, and not guessable.

**It throws when there is no authenticated user.** `HttpContext` is null outside a request, so anything
running in the background — the Stripe webhook is `[AllowAnonymous]` — must not depend on `ICurrentUser`.
Failing loudly beats silently reading `cart:`.

## Roles

| Role | Usage |
|---|---|
| `Admin` | `[Authorize(Roles = "Admin")]` on `AdminController` |
| `Customer` | Seeded but not enforced |

## Password Policy

| Setting | Value |
|---|---|
| `RequiredLength` | 12 |
| `RequireUppercase` | true |
| `RequireDigit` | true |
| `RequireNonAlphanumeric` | true |

## Lockout

| Setting | Value |
|---|---|
| `MaxFailedAccessAttempts` | 5 |
| `DefaultLockoutTimeSpan` | 15 min |
| `AllowedForNewUsers` | true |

## Cookie

| Setting | Value |
|---|---|
| `HttpOnly` | true |
| `SecurePolicy` | Always |
| `SameSite` | Strict |
| `ExpireTimeSpan` | 24h |
| `SlidingExpiration` | false |

## Rules / Constraints

- Cookie-only auth — never add JWT config.
- Always use `ClaimsPrincipleExtensions` to load users.
- `user-info` returns 204 (not 401) when unauthenticated — frontend polls this on startup. **The client
  depends on it:** an empty body arrives as `null`, which is what clears the session at startup, and it is
  why `errorInterceptor` needs no exemption for the startup probe. Add `[Authorize]` here and an anonymous
  visitor gets bounced to the login page on load.
- `SlidingExpiration = false` — 24h absolute, forces re-login daily.
- Never accept a key for user-owned data from the client. Derive it from `ICurrentUser`.

## Common Mistakes

- Calling `UserManager.FindByEmailAsync` in a controller (misses `.Include(Address)`).
- Treating `[Authorize]` as ownership. It only proves the caller is *a* user — BUG-18 shipped with
  `[Authorize]` on every cart endpoint and still let anyone read anyone's cart.
- Using the email as a storage key. It is PII, it changes, and it is guessable.
