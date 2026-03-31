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

## ClaimsPrincipalExtensions

| Method | What it does |
|---|---|
| `GetUserByEmailAsync(user)` | Loads user by email claim, throws `AuthenticationException` if not found |
| `GetUserByEmailWithAddressAsync(user)` | Same + `.Include(x => x.Address)` |
| `GetEmail(user)` | Extracts `ClaimTypes.Email`, throws if missing |

## Roles

| Role | Usage |
|---|---|
| `Admin` | `[Authorize(Roles = "Admin")]` on `AdminController` |
| `Customer` | Seeded but not enforced |

## Rules / Constraints

- Cookie-only auth — never add JWT config.
- Always use `ClaimsPrincipalExtensions` to load users.
- `user-info` returns 204 (not 401) when unauthenticated — frontend polls this on startup.

## Common Mistakes

- Calling `UserManager.FindByEmailAsync` in a controller (misses `.Include(Address)`).
