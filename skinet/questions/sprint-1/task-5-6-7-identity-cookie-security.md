# Tasks 5-6-7 — Identity Password Policy, Account Lockout, Cookie Hardening

## Why this approach

All three plug into the same `IdentityOptions` and cookie middleware that ASP.NET Identity
already wires up. No new services — just configuring what's already there.

`ConfigureApplicationCookie` targets `IdentityConstants.ApplicationScheme`, which is exactly
what `AddIdentityApiEndpoints` registers internally. So the cookie settings are applied to the
right scheme automatically.

`SlidingExpiration = false` means the 24h window is absolute — the cookie does not reset its
expiry on each request. This forces re-login daily regardless of activity.

## What confused me

- Why does closing the browser not delete the cookie? Because `ExpireTimeSpan` sets a
  persistent cookie (with an explicit expiry date). Session cookies (no expiry) disappear on
  browser close — persistent cookies don't.
- What does `SameSite=Strict` block? Cross-site requests. A request from a different site
  (different eTLD+1) will not include the cookie. localhost:4200 → localhost:7096 is same-site
  (same eTLD+1), so dev works fine.
- Why `AccessFailedCount = 0` after lockout? Identity resets the counter to 0 once the account
  is locked — the lockout is tracked via `LockoutEnd`, not the counter.

## Explanation

### Password policy

Configured via `options.Password` inside `AddIdentityApiEndpoints`:

```csharp
options.Password.RequiredLength = 12;
options.Password.RequireUppercase = true;
options.Password.RequireDigit = true;
options.Password.RequireNonAlphanumeric = true;
```

Identity validates these on every `CreateAsync` / `UpdatePasswordAsync` call.

### Account lockout

```csharp
options.Lockout.MaxFailedAccessAttempts = 5;
options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
options.Lockout.AllowedForNewUsers = true;
```

After 5 failed logins, Identity sets `LockoutEnd = now + 15min` in the DB.
All login attempts during that window return the same 401 — no different status code by default.
`AccessFailedCount` resets to 0 after locking.

### Cookie hardening

```csharp
services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;        // JS cannot read the cookie
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
    options.Cookie.SameSite = SameSiteMode.Strict;           // no cross-site
    options.ExpireTimeSpan = TimeSpan.FromHours(24);         // 24h absolute
    options.SlidingExpiration = false;                       // no renewal on activity
});
```

| Flag | What it prevents |
|---|---|
| HttpOnly | XSS stealing the cookie via `document.cookie` |
| Secure | Cookie sent over HTTP (man-in-the-middle) |
| SameSite=Strict | CSRF — cookie not sent on cross-site requests |

## Exercise

1. What is the difference between `SameSite=Strict` and `SameSite=Lax`?
2. A user logs in and stays active for 25 hours with `SlidingExpiration = false`. Are they still logged in?
3. After 5 failed login attempts, `AccessFailedCount` shows 0. Is the lockout working?

## Solution

1. `Strict` — cookie never sent on any cross-site request (even top-level navigation like
   clicking a link from another site). `Lax` — cookie sent on top-level GET navigations
   (e.g. clicking a link) but not on cross-site POST/AJAX. `Strict` is more secure but can
   break OAuth flows where the IdP redirects back to your site.
2. No — `SlidingExpiration = false` means the cookie expires 24h from login, regardless of
   activity. They are logged out after 24h.
3. Yes — Identity resets `AccessFailedCount` to 0 after locking. The lockout is tracked via
   `LockoutEnd` in the DB. Query `LockoutEnd` to confirm.

## Interview Q&A

**Q: What does `HttpOnly` protect against?**
A: XSS. JavaScript running on the page cannot access `document.cookie`, so a script injected
by an attacker cannot steal the session cookie.

**Q: What is the difference between `SlidingExpiration = true` and `false`?**
A: Sliding — the cookie expiry resets on every request, so active users stay logged in
indefinitely. Absolute (`false`) — the cookie expires at a fixed time after login regardless
of activity. Absolute is more secure; sliding is more user-friendly.

**Q: How does ASP.NET Identity handle account lockout?**
A: After `MaxFailedAccessAttempts` failures, Identity sets `LockoutEnd` to
`now + DefaultLockoutTimeSpan` and resets `AccessFailedCount` to 0. All subsequent login
attempts during that window fail immediately without checking the password.

**Q: What does `SameSite=Strict` protect against?**
A: CSRF. The browser will not include the cookie in any request that originates from a
different site. An attacker's page cannot trigger authenticated requests on behalf of the user.
