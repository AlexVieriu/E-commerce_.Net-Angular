## SECTION A — CORS, CSP & SECURITY HEADERS

> **Source:** Task 9 — NetEscapades security headers + CORS policy in `ApplicationServicesExtensions`.

---

### EASY

**A1.** [HIGH] What is CORS and why is it a browser-enforced mechanism (not server-enforced)?

<details>
<summary>The 5% Answer</summary>

- **CORS (Cross-Origin Resource Sharing)** is a browser security mechanism — it controls whether JS from origin X can read responses from origin Y
- The server doesn't enforce it; it just *declares* its policy via `Access-Control-Allow-*` response headers
- The browser checks those headers and **blocks the JS from reading the response** — even if the server returned 200 and fully processed the request
- Exists because browsers auto-attach cookies to requests — without CORS, any malicious site could silently call your bank API using your session cookie

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"CORS blocks requests from other sites." Wrong — the request often reaches your server and executes. The browser blocks the *response* from being read by JS.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that CORS only protects browser-origin requests — **a malicious Node script can ignore CORS entirely**. CORS is defence against browser-delivered attacks (XSS-to-API), not against all cross-origin calls.

</details>

---

**A2.** [HIGH] [OUT-OF-SCOPE] What is the `SameSite` cookie attribute? Explain `Strict`, `Lax`, and `None` — what attack does each setting mitigate or enable?

<details>
<summary>The 5% Answer</summary>

- `SameSite` controls whether the browser attaches a cookie to **cross-site** requests
- **`Strict`** — cookie never sent on any cross-site request, including top-level navigations. Completely blocks CSRF. **Downside:** clicking a link from an email or Google Search won't carry the session — user appears logged out on first load.
- **`Lax`** — cookie sent on top-level `GET` navigations (link clicks) but **not** on cross-site sub-requests (XHR, fetch, forms, images). ASP.NET Core default since .NET 6. Best balance for most apps.
- **`None; Secure`** — cookie sent on all cross-site requests, including iframes. Required for embedded flows (Stripe 3DS iframe, OAuth pop-ups). **Must** be paired with `Secure` — without HTTPS, modern browsers silently drop the cookie.

ASP.NET Core sets `SameSite=Lax` by default. To override per-cookie:
```csharp
options.Cookie.SameSite = SameSiteMode.Strict;
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"`SameSite=None` is always dangerous." Not true — it's required for embedded payment flows like Stripe's 3DS. The risk comes from pairing `None` with weak CORS, no `Secure` flag, or broad `AllowCredentials()`.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `SameSite=Lax` does NOT fully prevent CSRF. A top-level GET navigation (`<img src="https://api.skinet.com/account/delete">`) still carries the cookie. State-changing operations on GET endpoints remain vulnerable — use `SameSite=Strict` or anti-forgery tokens for those. `Lax` protects POST/XHR cross-site, not GET-based exploits.

</details>

---

### MEDIUM

**A3.** [HIGH] Your Angular app works on `localhost:4200` but after deploying to Azure Static Web Apps, login requests fail silently — nothing in the .NET logs. Diagnose and fix.

<details>
<summary>The 5% Answer</summary>

- **Root cause:** the deployed origin (`https://skinet.azurestaticapps.net`) isn't in the CORS `WithOrigins(...)` whitelist — only `http://localhost:4200` was added
- The server returns 200 and processes the request, but the response has no matching `Access-Control-Allow-Origin` header
- Browser blocks JS from reading the response client-side → **logs are empty because the block happens in the browser, not on the server**
- Fix: add the deployed origin to the CORS policy and republish

```csharp
policy.WithOrigins(
    "http://localhost:4200",
    "https://skinet.azurestaticapps.net"  // added
);
```

- **Production consequence:** silently breaks every time a new environment (staging, preview slot, custom domain) is added without a CORS update

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Enable `AllowAnyOrigin()`." That kills cookie-based auth (incompatible with `AllowCredentials()`) and opens the API to the entire internet.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that Azure preview slots have **unpredictable URLs** — the solution is a regex-based origin matcher or config-driven origin list per environment, not hardcoded domains.

</details>

---

**A4.** [MEDIUM] Why does your CSP use `unsafe-inline` for `style-src`? Isn't that a security hole?

<details>
<summary>The 5% Answer</summary>

- Yes — `unsafe-inline` disables XSS protection for styles. It's a **deliberate, documented tradeoff**
- **Why it's necessary:** Angular Material uses `ViewEncapsulation.None` and injects `<style>` tags into the DOM at runtime via JavaScript
- The original HTML has no `<style>` tags at build time → nothing to hash
- No per-request nonce is possible because this is a client-rendered SPA — no server to inject nonces
- **Real alternatives:**
  1. **Nonce-based CSP** — requires Angular SSR so the server injects a fresh nonce per request into Material's renderer
  2. **Hash-based CSP** — precompute hashes of all injected styles at build time, list them in the CSP (massive maintenance cost — rebuild on every Material update)
- Both are too expensive for this project's scope → `unsafe-inline` on `style-src` is the accepted tradeoff

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"I use `unsafe-inline` because Angular needs it." That's parroting — it's not Angular that needs it, it's **Material's runtime style injection**.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `unsafe-inline` on `style-src` is far less dangerous than on `script-src`. Style-based XSS is limited (no code execution, only data exfiltration via CSS selectors). Still, pointing out you'd tighten it in a regulated environment.

</details>

---

**A5.** [MEDIUM] Your CSP includes `connect-src 'self' wss: https:`. Why is `wss:` there? What breaks without it?

<details>
<summary>The 5% Answer</summary>

- `wss:` is the **secure WebSocket protocol** — it's what SignalR uses for the `/hubs/notifications` endpoint
- Without `wss:` in `connect-src`, the browser blocks the WebSocket upgrade
- SignalR automatically falls back to **long-polling** (slower, holds more server connections) or fails entirely if transports are restricted to WebSockets only
- `ws:` (insecure WebSocket) is **intentionally not listed** — only encrypted connections allowed

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"WebSockets need it." True but shallow — you need to know SignalR's fallback behaviour and why `ws:` (insecure) is explicitly NOT listed.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **SignalR at scale needs a backplane** (Redis or Azure SignalR Service), and that the CSP must allow the backplane's origin too if clients connect to it directly.

</details>

---

**A6.** [HIGH] Compare `AllowCredentials()` + `WithOrigins("...")` vs `AllowCredentials()` + `AllowAnyOrigin()`. When does each break?

<details>
<summary>The 5% Answer</summary>

- **`AllowAnyOrigin()` + `AllowCredentials()`** → ASP.NET throws `InvalidOperationException` at startup
  - The CORS spec forbids wildcard `*` origin with credentials — any website could make authenticated requests using the victim's cookies
  - The framework enforces this at startup, not at runtime
- **`WithOrigins("...")` + `AllowCredentials()`** → works correctly
  - The `Access-Control-Allow-Origin` header echoes the exact requesting origin
  - `Access-Control-Allow-Credentials: true` is returned only for that specific whitelisted origin

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"You can't use `AllowAnyOrigin` with credentials." Correct but incomplete — you must know *why* (session hijacking via CSRF-style attack) and that the framework enforces it at startup.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `SetIsOriginAllowed(_ => true)` as the **sneaky way to bypass this guard** — it echoes the origin dynamically and passes ASP.NET's validation but is functionally equivalent to `AllowAnyOrigin` with credentials. Dangerous code smell in code review.

</details>

---

### HARD

**A7.** [MEDIUM] Find the 3 security bugs in this CORS configuration:

```csharp
builder.Services.AddCors(o => o.AddPolicy("default", p =>
    p.SetIsOriginAllowed(_ => true)
     .AllowAnyMethod()
     .AllowAnyHeader()
     .AllowCredentials()
     .WithExposedHeaders("*")));
```

<details>
<summary>The 5% Answer</summary>

- **Bug 1 — `SetIsOriginAllowed(_ => true)` + `AllowCredentials()`**
  - Functionally identical to `AllowAnyOrigin()` + credentials, which ASP.NET *would* block at startup
  - The lambda bypasses that guard — any site on the internet can make credentialed requests with the victim's cookies

- **Bug 2 — `AllowAnyHeader()` on a credentialed policy**
  - Allows attackers to inject arbitrary custom headers (e.g. `X-User-Impersonate`, `X-Admin-Override`) without restriction
  - Should be `WithHeaders("Content-Type", "Authorization", "Accept")`

- **Bug 3 — `WithExposedHeaders("*")` with credentials** (the interviewer's trap)
  - The `*` wildcard in `Access-Control-Expose-Headers` is **silently ignored when credentials are allowed** — browsers require explicit header names
  - Looks like it exposes everything, actually exposes nothing — false sense of security

- **Corrected:**
```csharp
p.WithOrigins("https://skinet.azurestaticapps.net")
 .WithHeaders("Content-Type", "Authorization", "Accept")
 .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
 .WithExposedHeaders("Pagination")
 .AllowCredentials();
```

- **Production consequence:** the original config lets any site execute authenticated state-changing requests as your logged-in users

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Only spotting the `SetIsOriginAllowed` issue. That's the obvious one — the exposed-headers wildcard silently failing is the interviewer's trap.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that `SetIsOriginAllowed` is sometimes legitimate (reflecting a validated subdomain list), and showing how to do it safely: `SetIsOriginAllowed(origin => allowedRegex.IsMatch(origin))`.

</details>

---

**A8.** [HIGH] [OUT-OF-SCOPE] Your ASP.NET Core API is consumed by three clients: (1) your Angular SPA at `skinet.com`, (2) a React Native mobile app, (3) a third-party partner sending webhooks. Each uses a different auth mechanism. Design the CORS policy and auth strategy for each, and explain what breaks if you apply a single policy to all three.

<details>
<summary>The 5% Answer</summary>

**Client 1 — Angular SPA (`skinet.com`):**
- **Auth:** ASP.NET Identity cookie (`SameSite=Lax; HttpOnly; Secure`)
- **CORS:** required — browser enforces same-origin policy
- **Policy:** `WithOrigins("https://skinet.com").AllowCredentials().WithHeaders(...)`
- The browser attaches the cookie automatically; CORS restricts other origins from reading responses

**Client 2 — React Native mobile app:**
- **Auth:** JWT Bearer token — `Authorization: Bearer <token>`. Cookies don't work across native apps.
- **CORS:** NOT required — mobile apps are not browser origins; they make direct HTTP calls
- Rate-limit by token presence; validate JWT with the same `IssuerSigningKey` as your SPA tokens

**Client 3 — Partner webhook (server-to-server):**
- **Auth:** HMAC-signed requests (`X-Signature` header) or static API key in `X-Api-Key`
- **CORS:** NOT required — server-to-server calls bypass CORS entirely
- IP allowlisting as a second layer; verify signature before processing any payload

**Named policies in ASP.NET Core:**
```csharp
services.AddCors(o => {
    o.AddPolicy("spa", p => p
        .WithOrigins("https://skinet.com")
        .WithHeaders("Content-Type", "Authorization")
        .WithMethods("GET", "POST", "PUT", "DELETE")
        .AllowCredentials());
    // mobile and partner: no CORS policy needed
});

// Apply per-controller or globally with conditions:
app.UseCors(ctx =>
    ctx.Request.Headers.ContainsKey("Origin") ? "spa" : "none");
```

**What breaks with a single policy:**
- Applying `AllowCredentials` to partner traffic: any webhook can impersonate a browser session
- Applying SPA CORS to mobile: adds unnecessary overhead and exposes your origin whitelist
- Mixing JWT and cookie auth on the same endpoint without `[Authorize(AuthenticationSchemes)]`: either scheme can authenticate where only one should

**Production consequence:** a single broad CORS policy applied to all consumers opens session-hijacking surface for server-to-server traffic that never needs cookies.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"Apply `AllowAnyOrigin()` and let the clients deal with it." That kills `AllowCredentials()` (framework throws at startup) and exposes every authenticated endpoint to cross-origin exploitation.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning `[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]` on SPA-only endpoints and `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` on mobile-only endpoints. Without scheme-scoping, a valid JWT could authenticate a cookie-protected endpoint — a subtle auth bypass that's invisible in testing but exploitable in production.

</details>

---

**A9.** [MEDIUM] Describe a realistic attack scenario where `SetIsOriginAllowed(_ => true)` + `AllowCredentials()` is exploited. Show the exact HTTP request and the fix.

<details>
<summary>The 5% Answer</summary>

- **Setup:** victim is logged in to `https://skinet.com`. Attacker hosts `https://evil.com`

```js
fetch('https://api.skinet.com/account/email', {
  method: 'PUT',
  credentials: 'include',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email: 'attacker@evil.com' })
});
```

- Victim visits `evil.com` → browser sends **preflight OPTIONS** with `Origin: https://evil.com`
- Server lambda returns `true` → responds with:

```http
Access-Control-Allow-Origin: https://evil.com
Access-Control-Allow-Credentials: true
Access-Control-Allow-Methods: PUT
```

- Browser approves → real PUT fires with victim's session cookie attached → email changed to attacker's
- Attacker initiates password reset → **full account takeover**

- **Fix:**
```csharp
var allowed = new[] { "https://skinet.com", "https://skinet.azurestaticapps.net" };
p.SetIsOriginAllowed(origin => allowed.Contains(origin))
 .AllowCredentials()
 .WithMethods("GET", "POST", "PUT", "DELETE");
```

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
Describing CSRF generically without the `credentials: 'include'` detail or the preflight flow. The interviewer wants to see you understand the browser-server dance.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **`SameSite=Lax` cookies** (ASP.NET default since .NET 7) partially mitigate this for simple GET-based exploits, but state-changing requests with `credentials: 'include'` still succeed when the user has recently interacted with the target site. Defence in depth: `SameSite=Strict` + explicit origin whitelist + anti-forgery tokens.

</details>

---

**A10.** [HIGH] [OUT-OF-SCOPE] What is a CORS preflight? Describe the exact headers the browser sends, the headers the server must return, and the conditions that trigger a preflight.

<details>
<summary>The 5% Answer</summary>

- A preflight is an automatic `OPTIONS` request the browser sends **before** a non-simple cross-origin request, asking "will you accept this?"

- **Triggers (non-simple request conditions):**
  - Method is not GET / POST / HEAD
  - Custom headers beyond CORS-safelisted ones (`Accept`, `Accept-Language`, `Content-Language`, simple `Content-Type`)
  - `Content-Type: application/json` — NOT on the safelist, always triggers preflight

- **Preflight request:**
```http
OPTIONS /api/orders HTTP/1.1
Origin: https://skinet.com
Access-Control-Request-Method: POST
Access-Control-Request-Headers: content-type, authorization
```

- **Required server response:**
```http
HTTP/1.1 204 No Content
Access-Control-Allow-Origin: https://skinet.com
Access-Control-Allow-Methods: POST, GET, DELETE
Access-Control-Allow-Headers: content-type, authorization
Access-Control-Allow-Credentials: true
Access-Control-Max-Age: 600
```

- If any `Allow-*` header is missing or doesn't cover the request → browser blocks the real request
- `Access-Control-Max-Age: 600` caches the preflight result for 10 minutes client-side — prevents a preflight on every single API call
- **Production consequence:** forgetting `Max-Age` means every API call is preceded by an OPTIONS round-trip — on a 100-call page, that's 100 wasted network trips

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:**
"The browser sends an OPTIONS request before the real one." No detail on the headers or what triggers it.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:**
Mentioning that **you can avoid preflights entirely by keeping requests "simple"** — GET with standard headers. Relevant for high-throughput public APIs. Also mentioning that preflight caching (`Max-Age`) is capped by the browser (Chrome: 2 hours max, Firefox: 24 hours max).

</details>

---

### ⚡ Syntax Quick-Fire

**AQ1.** [HIGH] `?.` (null-conditional) vs `!.` (null-forgiving operator) — what happens at runtime when the value IS null?

<details>
<summary>The 5% Answer</summary>

- **`?.`** short-circuits on null: `user?.Name` returns `null` as `string?` — never throws.
- **`!.`** suppresses the compiler's nullable warning but does **nothing** at runtime — a null value still throws `NullReferenceException`. It is a compiler promise ("I know this isn't null"), not a null check.
- Use `?.` defensively. Use `!.` only after a checked invariant (post-`Assert.NotNull`, inside an `if (x != null)` branch) and never at system boundaries.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "They both prevent null exceptions." `!.` doesn't prevent anything at runtime.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Noting that `!.` is a code smell in public-facing methods and a legitimate shortcut only inside tight-controlled scopes (test helpers, framework-generated code). Seeing `!.` in production service code is a review flag.

</details>

---

**AQ2.** [HIGH] When do you need `ConfigureAwait(false)` in an ASP.NET Core API, and why?

<details>
<summary>The 5% Answer</summary>

- **Almost never** in ASP.NET Core. The framework has no `SynchronizationContext`, so `ConfigureAwait(false)` is a no-op — continuations already resume on the thread pool.
- **Critical in library/NuGet code** where the caller may have a `SynchronizationContext` (WinForms, WPF, Blazor WASM). Without it the continuation tries to resume on the UI thread while it's blocked calling `.Result` → **deadlock**.
- Rule: library code — always add it. ASP.NET Core app code — omit (clarity over redundancy).

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Always add it for performance." In ASP.NET Core it provides no benefit.

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Knowing that `ConfigureAwait(false)` is in the ASP.NET Core framework source itself — as a convention for library authors to follow, not for performance. Understanding *why* it's there vs cargo-culting it.

</details>

---

**AQ3.** [HIGH] C# `record` vs `class` — name the two concrete features that make `record` ideal for DTOs.

<details>
<summary>The 5% Answer</summary>

- **Value-based equality** — `Equals` and `GetHashCode` are compiler-generated from all properties. Two `record` instances with the same values are equal; two `class` instances are equal only if they're the same reference.
- **`with` expression** — non-destructive mutation: `var updated = original with { Price = 75m };` creates a new instance with one field changed. No manual copy constructor.

```csharp
record Product(int Id, string Name, decimal Price);

var p1 = new Product(1, "Shoe", 50m);
var p2 = p1 with { Price = 75m };

p1 == p2;  // false — different Price
p1 with { } == p1;  // true — all fields same
```

- **When NOT to use record:** entities with identity semantics (`Order`, `User`) where equality should be by ID, not all fields.

</details>

<details>
<summary>Red Flag</summary>

**Red Flag:** "Records are always immutable." `record class` with `set` properties is mutable. Immutability is opt-in (init-only or positional records).

</details>

<details>
<summary>Green Flag</summary>

**Green Flag:** Mentioning `record struct` (C# 10) brings value equality to structs with no heap allocation — ideal for small hot-path value objects like `Money` or `Coordinate`.

</details>

---
