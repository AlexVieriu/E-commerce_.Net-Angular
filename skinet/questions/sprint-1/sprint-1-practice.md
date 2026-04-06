# Sprint 1 — Practice Exam

> CI/CD, secrets management, Identity hardening
> 20% easy | 40% medium | 40% hard
> Mix of recall, scenario, practical, and system design

---

## Manual Testing Checklist

Do these yourself before any interview. Don't skip them — interviewers ask "show me" questions.

- [ ] Open DevTools → Application → Cookies → verify `.AspNetCore.Identity.Application` has `HttpOnly`, `Secure`, `SameSite=Strict`
- [ ] Fail login 5 times → query `SELECT LockoutEnd, AccessFailedCount FROM AspNetUsers WHERE Email = '...'` → confirm `LockoutEnd` is set, `AccessFailedCount` is 0
- [ ] Try 6th login during lockout → confirm same 401 (no different error)
- [ ] Wait 15 min (or manually clear `LockoutEnd`) → confirm login works again
- [ ] Register with `"abc123"` → confirm Identity rejects it (min 12, upper, digit, special)
- [ ] Open `.github/workflows/cicd.yml` — walk through triggers, jobs, conditions OUT LOUD, no notes
- [ ] Draw the config provider chain on paper: `appsettings.json → appsettings.{Env}.json → Env vars → User Secrets → Key Vault`
- [ ] Run `dotnet user-secrets list --project src/API` → verify you know what's there
- [ ] Delete a user secret → run the app → confirm it fails → re-add it → confirm it works
- [ ] Open Azure portal → Key Vault → Secrets → verify the naming (`--` separator)

---

## Knowledge Beyond Scope

Topics you didn't implement but interviewers WILL ask about. Study these separately.

**JWT vs Cookies** — You chose cookies. Be ready to explain:
- JWT: stateless, stored in `localStorage` or memory, sent as `Authorization: Bearer`. Vulnerable to XSS (JS can read localStorage). No built-in expiry enforcement server-side without a blacklist.
- Cookie: stateful (session on server or encrypted ticket), `HttpOnly` blocks XSS. Vulnerable to CSRF (mitigated by `SameSite`). Server can revoke instantly.
- Your answer: "We used cookies because Identity's default cookie auth gives us HttpOnly + SameSite + server-side session control. JWT would've required manual token refresh, blacklisting, and wouldn't have benefited from SameSite CSRF protection."

**SQL Injection** — EF Core parameterizes all queries automatically. But know:
- What a parameterized query is: `WHERE Id = @p0` not `WHERE Id = ' + userInput + '`
- Why raw SQL (`FromSqlRaw`) is dangerous if you interpolate user input
- `FromSqlInterpolated` is safe — it parameterizes the interpolated values

**What if Key Vault is down at startup?** — `AddAzureKeyVault` runs at startup. If KV is unreachable, the app won't start. No runtime fallback. You should say: "We'd handle this with health checks and retry policies on the Key Vault client, or cache secrets locally with a short TTL."

**OIDC (OpenID Connect) vs OAuth2** — Your pipeline uses OIDC for GitHub→Azure auth. Know the difference:
- OAuth2: authorization (what can you access)
- OIDC: authentication (who are you) — built on top of OAuth2, adds an ID token
- Your pipeline: GitHub proves its identity to Azure via OIDC federation → gets a short-lived access token

---

## EASY (recall) — 20%

### E1. Question
What does `HttpOnly` on a cookie prevent?

<details><summary>Answer</summary>
JavaScript cannot read the cookie via `document.cookie`. Prevents XSS from stealing the session.
</details>

### E2. Question
What's the difference between `npm ci` and `npm install`?

<details><summary>Answer</summary>
`npm ci` installs exactly what's in `package-lock.json` and fails if out of sync. `npm install` resolves ranges and can silently bump patch versions.
</details>

### E3. Question
In the .NET config provider chain, if the same key exists in `appsettings.json` and User Secrets, which wins?

<details><summary>Answer</summary>
User Secrets — later providers override earlier ones.
</details>

### E4. Question
What does `SameSite=Strict` protect against?

<details><summary>Answer</summary>
CSRF. The browser won't include the cookie in any request originating from a different site.
</details>

### E5. Question
Why does Key Vault use `--` instead of `:` in secret names?

<details><summary>Answer</summary>
Key Vault doesn't allow `:` in names. The .NET provider maps `--` back to `:` automatically.
</details>

### E6. Practical
Run `dotnet user-secrets list --project src/API`. Write down every key from memory. Check.

---

## MEDIUM (scenario + practical) — 40%

### M1. Scenario
A new developer clones the repo and runs `dotnet run`. They get `StripeSettings:SecretKey is not configured`. What do they do?

<details><summary>Answer</summary>
Run `dotnet user-secrets set "StripeSettings:SecretKey" "sk_test_..."`. Can't put it in `appsettings.json` because that's committed to git. User Secrets are stored outside the project directory.
</details>

### M2. Scenario
A user reports they can't log in. You check the DB: `LockoutEnd` is set to 30 minutes from now, `AccessFailedCount` is 0. What happened?

<details><summary>Answer</summary>
They failed login 5 times. Identity locked the account and reset `AccessFailedCount` to 0. The lockout is tracked via `LockoutEnd`, not the counter. They need to wait until `LockoutEnd` passes.
</details>

### M3. Scenario
Your CI pipeline deploys on every merge to `development` AND `main`. QA complains that untested code hits production. What's wrong?

<details><summary>Answer</summary>
The deploy job triggers on `push` to both branches. Fix: add `if: github.ref == 'refs/heads/main'` to the deploy job, or restrict the `push` trigger to `[main]` only.
</details>

### M4. Practical
Open `.github/workflows/cicd.yml`. Without looking at it, draw the 3-job flow on paper:
- Which triggers fire which jobs?
- What condition gates the deploy job?
- What happens on a PR to `development`?

Then open the file and check your drawing.

### M5. Scenario
You rotate the Stripe secret key in Azure Key Vault. The app still uses the old key. Why?

<details><summary>Answer</summary>
`AddAzureKeyVault` loads secrets once at startup and caches them. The app must be restarted for new values to take effect. No live reload.
</details>

### M6. Practical
Modify the password policy to `RequiredLength = 6` temporarily. Try registering with `"Abc123!"`. Does it succeed? Now change it back to 12. Try the same password. Confirm it's rejected.

### M7. Scenario
You set up a mock for `ICartService` but the test fails because the mock returns `null`. The mock looks correct. What's wrong?

<details><summary>Answer</summary>
Mock setup must happen BEFORE instantiating the class under test if the dependency is used in the constructor. `CartService` calls `redis.GetDatabase()` in its constructor — if the mock isn't set up yet, it returns the default (null).
</details>

### M8. Question
`DefaultAzureCredential` tries multiple auth methods. In Azure App Service, which one fires? Locally?

<details><summary>Answer</summary>
App Service: Managed Identity. Locally: Azure CLI (`az login`) or Visual Studio credential. It tries them in a fixed order and uses the first one that succeeds.
</details>

### M9. Practical
Open DevTools → Application → Cookies. Delete the Identity cookie manually. Refresh the page. What happens? Why?

<details><summary>Answer</summary>
You're logged out. The cookie IS the session — the server decrypts it to identify you. No cookie = anonymous. The server didn't change; the client lost its credential.
</details>

### M10. Question
Why does `SlidingExpiration = false` matter for security?

<details><summary>Answer</summary>
With sliding, an active attacker who steals a cookie can keep it alive indefinitely by using it regularly. Absolute expiry forces re-authentication after 24h regardless of activity.
</details>

### M11. Scenario
Your `dotnet test` in CI passes but `ng build` fails. The deploy job still runs. What's missing?

<details><summary>Answer</summary>
The deploy job needs `needs: [test-backend, test-frontend]` to depend on BOTH jobs. If it only depends on `test-backend`, it runs even when the frontend fails.
</details>

### M12. Practical
Write the config provider chain on a whiteboard in order. For each provider, write one example of what belongs there:
- `appsettings.json` → ?
- `appsettings.Development.json` → ?
- Environment variables → ?
- User Secrets → ?
- Key Vault → ?

<details><summary>Answer</summary>
appsettings.json → Stripe PublishableKey (public), logging config.
appsettings.Development.json → local SQLite connection string.
Env vars → `ASPNETCORE_ENVIRONMENT`, Azure App Settings.
User Secrets → Stripe SecretKey, WhSecret (dev).
Key Vault → Same secrets in prod, with audit trail + access control.
</details>

---

## HARD (debug + design + senior stretch) — 40%

### H1. Debug
CI ran `dotnet test` and 12 tests failed on the first push — but they passed locally. What categories of bugs cause this and how do you investigate?

<details><summary>Answer</summary>
Common causes:
1. **Environment differences** — different OS (Linux CI vs Windows local), different .NET SDK version
2. **Missing test setup** — mocks not configured for CI environment
3. **Dependency version drift** — `npm install` resolved different versions on CI vs local
4. **Test ordering** — tests pass in isolation but fail when run together (shared state)
5. **Hardcoded paths/URLs** — `C:\Users\...` in a Linux runner

Investigation: read the CI log line by line. Group failures by category. Fix the most impactful first (often it's one root cause causing multiple failures).
</details>

### H2. Design
You need to add secret rotation without downtime. The current setup loads secrets at startup only. Design a solution.

<details><summary>Answer</summary>
Options:
1. **Azure Key Vault ConfigurationProvider with polling**: set `ReloadInterval` on `AddAzureKeyVault` options — polls KV periodically and updates `IConfiguration` in-place.
2. **Blue-green deploy**: deploy new instance with new secrets, switch traffic, kill old instance. No code change needed.
3. **`IOptionsSnapshot<T>`**: reads config per-request (scoped lifetime), so new values from a reloaded configuration appear without restart.

For a real production system: use option 1 (polling) + option 3 (`IOptionsSnapshot`) so secrets refresh automatically every N minutes.
</details>

### H3. Practical
Temporarily change `SameSite` from `Strict` to `None`. Open a different localhost port (e.g. `http://localhost:3000`) and send a fetch request to your API with `credentials: 'include'`. Does the cookie get sent? Now change back to `Strict` and repeat. What changes?

<details><summary>Answer</summary>
`SameSite=None` + `Secure`: cookie is sent cross-site (if HTTPS). `SameSite=Strict`: cookie is NOT sent. The browser blocks it because the request originates from a different site. This is the CSRF protection in action.
</details>

### H4. Senior Question
A penetration tester says: "Your `SameSite=Strict` cookie breaks when users click links from email to your site — they land unauthenticated." How do you fix this without weakening security?

<details><summary>Answer</summary>
Switch to `SameSite=Lax`. Lax allows cookies on top-level GET navigations (clicking links) but blocks cross-site POST/AJAX. You lose protection against GET-based CSRF, but GET endpoints should be idempotent anyway (no state changes on GET). Alternatively, keep `Strict` and accept the UX tradeoff — user logs in once after clicking the link.
</details>

### H5. Design
Draw the full flow from "user clicks Register" to "cookie lands in the browser." Include: Angular form → API controller → Identity UserManager → DB → response → cookie middleware → Set-Cookie header.

<details><summary>Answer</summary>

```
Angular form → POST /api/account/register { email, password }
  → AccountController.Register()
    → UserManager.CreateAsync(user, password)
      → Identity validates password policy (min 12, upper, digit, special)
      → If fail → 400 + validation errors
      → If pass → INSERT INTO AspNetUsers (PasswordHash = bcrypt(password))
    → SignInManager.SignInAsync(user)
      → Cookie middleware creates encrypted ticket
      → Set-Cookie: .AspNetCore.Identity.Application=<encrypted>; HttpOnly; Secure; SameSite=Strict; Expires=<24h>
  → 200 OK
Browser stores cookie → sent on every subsequent request
```
</details>

### H6. Senior Question
Why is `AccessFailedCount` reset to 0 after lockout instead of kept at 5? What security implication does this have?

<details><summary>Answer</summary>
Design choice: the lockout is tracked by `LockoutEnd`, not the counter. Resetting to 0 means after lockout expires, the user gets a fresh set of 5 attempts. If it stayed at 5, one more failure would immediately re-lock them (permanent lockout after first incident). The tradeoff: an attacker gets unlimited 5-attempt windows separated by 15-minute waits. Mitigation: progressive lockout (double the duration each time), or IP-based rate limiting on top.
</details>

### H7. Practical
Create a minimal GitHub Actions workflow from memory (no copy-paste) that:
- Triggers on PR to `main`
- Runs `dotnet test` on ubuntu-latest
- Has a deploy job that only runs on push to `main`

Write it in a text editor, then compare with your actual `cicd.yml`.

### H8. Senior Question
Your team wants to add a second environment (staging) to the pipeline. Staging deploys on merge to `development`, production on merge to `main`. Both use Key Vault but with different secrets. Design the pipeline and config changes.

<details><summary>Answer</summary>
Pipeline:
- Add a `deploy-staging` job: triggers on `push: [development]`, deploys to staging App Service
- Keep `deploy-production` on `push: [main]`
- Both jobs use OIDC but target different Azure subscriptions or resource groups

Config:
- Two Key Vaults: `kv-skinet-staging`, `kv-skinet-prod`
- `KeyVaultUrl` in each App Service's App Settings points to the right vault
- Same secret names in both vaults, different values
- `DefaultAzureCredential` + Managed Identity on each App Service → only has access to its own vault
</details>

### H9. Debug
A user reports: "I changed my password but I'm still logged in on my phone from yesterday." Why? How do you fix it?

<details><summary>Answer</summary>
The cookie is still valid — it was issued before the password change and hasn't expired yet (24h absolute). ASP.NET Identity has a `SecurityStamp` that's updated on password change. The cookie middleware validates the stamp periodically (default: 30 min via `ValidationInterval`). So the phone session dies within 30 minutes — but not immediately.

Fix for immediate invalidation: set `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero`. Tradeoff: every request now hits the DB to validate the stamp.
</details>

### H10. Senior Design
You're asked to implement MFA (multi-factor authentication) on top of your current cookie-based Identity setup. What changes in the backend, frontend, and user flow?

<details><summary>Answer</summary>
Backend:
- Enable TOTP: `UserManager.SetTwoFactorEnabledAsync(user, true)`
- Generate shared secret: `UserManager.GetAuthenticatorKeyAsync(user)`
- Verify code: `UserManager.VerifyTwoFactorTokenAsync(user, provider, code)`
- Login flow: `SignInManager.PasswordSignInAsync` returns `RequiresTwoFactor` instead of `Succeeded`

Frontend:
- After login returns `RequiresTwoFactor` → navigate to `/account/2fa`
- User enters TOTP code from authenticator app
- POST to `/api/account/2fa-verify` → if valid → `SignInManager.TwoFactorSignInAsync` → cookie issued

User flow: Register → scan QR code (shared secret) in Google Authenticator → login requires password + 6-digit code.
</details>

### H11. Practical
Kill the API. Run `az keyvault secret show --vault-name <your-vault> --name "StripeSettings--SecretKey"` (or check Azure portal). Now pretend Key Vault is down — comment out `AddAzureKeyVault` in `Program.cs`, remove the local User Secret for `StripeSettings:SecretKey`, and run the app. What error do you get? At what point in the startup?

### H12. Senior Question
Compare three approaches to storing session state: (1) encrypted cookie (your current approach), (2) server-side session with Redis, (3) JWT in localStorage. For each: where is state stored, what's the size limit, how do you revoke, what's the XSS/CSRF attack surface?

<details><summary>Answer</summary>

| | Encrypted cookie | Redis session | JWT in localStorage |
|---|---|---|---|
| State stored | In the cookie itself (encrypted) | Server (Redis), cookie holds session ID | Client (localStorage) |
| Size limit | ~4KB (cookie limit) | Unlimited (server-side) | ~5MB (localStorage) but tokens should be small |
| Revocation | Delete cookie client-side, or invalidate SecurityStamp server-side | Delete Redis key → instant | Must blacklist token server-side (defeats "stateless" purpose) |
| XSS surface | Safe — HttpOnly blocks JS access | Safe — HttpOnly session cookie | Vulnerable — JS can read localStorage |
| CSRF surface | Mitigated by SameSite | Mitigated by SameSite on session cookie | Immune — not auto-sent by browser |
</details>
