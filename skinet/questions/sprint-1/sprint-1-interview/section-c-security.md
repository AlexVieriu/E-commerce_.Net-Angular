## SECTION C — SECURITY (Identity, Cookies, Secrets)

### EASY

**C1.** What does `HttpOnly` on a cookie prevent? What attack does it mitigate?

<details><summary>Answer</summary>

- **Prevents** JavaScript from accessing the cookie via `document.cookie`.
- **Mitigates** XSS-based session theft — even if an attacker injects script, they can't read the auth cookie.
- The cookie is still sent automatically by the browser with every HTTP request.
</details>

---

**C2.** What is the difference between authentication and authorization?

<details><summary>Answer</summary>

- **Authentication** — WHO are you? (verifying identity — login, token, certificate).
- **Authorization** — WHAT can you do? (checking permissions — roles, policies, claims).
- Auth happens first — you must know WHO before deciding WHAT they can access.
- **401** = not authenticated. **403** = authenticated but not authorized.
</details>

---

**C3.** What does `SameSite=Strict` protect against?

<details><summary>Answer</summary>

- **Prevents** the browser from sending the cookie on any cross-site request (links, forms, images).
- **Protects against CSRF** — an attacker's page can't trigger authenticated actions because the cookie is never sent.
- **Downside** — users clicking links from external sites (email, social media) arrive unauthenticated.
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

---

**C6.** What is `SecurityStamp` in ASP.NET Identity? When is it updated? How does the cookie middleware use it? What happens if you set `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero`?

<details><summary>Answer</summary>

- **`SecurityStamp`** — a GUID that changes whenever security-sensitive data changes (password, roles, 2FA, email).
- Cookie middleware validates the stamp against the DB periodically (default every 30 min). Mismatch = sign out.
- **`TimeSpan.Zero`** — validates on every request: immediate invalidation, but a DB hit per request (performance cost).
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

---

**C8.** Why does `SlidingExpiration = false` matter for security? What's the risk if sliding expiration is enabled and an attacker steals the cookie?

<details><summary>Answer</summary>

- **Sliding = true** — resets the cookie lifetime on every request. An attacker who steals the cookie can keep it alive indefinitely by making periodic requests.
- **Sliding = false** — the cookie has a fixed absolute expiry regardless of activity, limiting the stolen cookie's window of use.

Best practice: `SlidingExpiration = false` with `ExpireTimeSpan = TimeSpan.FromHours(24)`.
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

---

**C13.** A user reports: "I changed my password but I'm still logged in on my phone from yesterday." Why? How do you fix it? What's the tradeoff?

<details><summary>Answer</summary>

- **Cause** — the cookie middleware validates the `SecurityStamp` only every 30 min by default. The password change updates the stamp, but the phone's cookie isn't re-validated until the next interval.
- **Fix** — reduce `SecurityStampValidatorOptions.ValidationInterval` (e.g. to 1 minute).
- **Tradeoff** — shorter interval = more DB hits per request; `TimeSpan.Zero` = every request (expensive at scale).
- **Better** — cache the stamp in Redis with a 1-minute TTL to get fast invalidation without per-request DB calls.
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

---

**C15.** You need to add MFA to your current cookie-based Identity setup. What changes in the backend, frontend, and user flow? What does `PasswordSignInAsync` return when MFA is required?

<details><summary>Answer</summary>

`PasswordSignInAsync` returns `SignInResult.TwoFactorRequired` — NOT a successful sign-in.

**Backend:** generate TOTP key (`GenerateNewAuthenticatorKey`), store it, verify codes with `VerifyTwoFactorTokenAsync`. Call `TwoFactorAuthenticatorSignInAsync` to issue the cookie after verification.

**Frontend:** detect `RequiresTwoFactor` in the response, redirect to 2FA page, collect code, POST to verify endpoint.

**User flow:** email+password → 2FA prompt → 6-digit code → authenticated session.

Also generate recovery codes as fallback. Store the 2FA key encrypted.
</details>

---

