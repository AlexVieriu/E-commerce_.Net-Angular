# Task 9 — CORS explicit + CSP security headers

## Why this approach

CORS and security headers are server-enforced browser policies. CORS controls cross-origin requests; security headers tell the browser what it's allowed to load and how to handle your content. Both are set at the HTTP response level, not in application logic.

We use `NetEscapades.AspNetCore.SecurityHeaders` instead of NWebsec because NWebsec hasn't been meaningfully maintained since ~2019 and has no active .NET 10 support. NetEscapades is actively maintained and gives a fluent API for building CSP directives without string concatenation.

---

## What confused me

- **Why `unsafe-inline` in CSP?** Sounds like it defeats the purpose. It does weaken script/style CSP, but Angular Material injects styles directly into the DOM at runtime — there's no nonce-based workaround without rebuilding the Material render pipeline. For a dev project it's the accepted tradeoff. A stricter approach would use nonces in a custom Angular renderer.
- **Why `wss:` in `connect-src`?** SignalR opens a WebSocket connection. Without `wss:` in the whitelist, the browser blocks the upgrade and SignalR falls back to long-polling (or fails entirely).
- **`AllowAnyMethod()` vs explicit methods** — CORS method restriction has low security value when you control both client and server. The real protection is the origin whitelist + `AllowCredentials()`. Explicit methods add friction for no meaningful gain unless you're exposing a public read-only API.

---

## Explanation

### CORS (Cross-Origin Resource Sharing)

A browser security mechanism. When JavaScript on `http://localhost:4200` calls `https://localhost:7096/api/...`, the browser sees different origins (port differs) and sends a preflight `OPTIONS` request. The server responds with which origins, methods, and headers are allowed. If the response matches, the real request proceeds.

Key config in `ApplicationServicesExtensions.cs`:
```csharp
opt.AddPolicy("CorsPolicy", policy =>
{
    policy
        .WithOrigins("http://localhost:4200", "https://localhost:4200", "https://skinet-...")
        .WithHeaders("Content-Type", "Accept")
        .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
        .AllowCredentials(); // required for cookie auth
});
```

`AllowCredentials()` is essential — without it the browser strips cookies from cross-origin requests and Identity auth breaks.

### CSP (Content-Security-Policy)

An HTTP response header that whitelists sources the browser is allowed to load resources from. Primary defense against XSS: even if an attacker injects a `<script>` tag, the browser refuses to execute it if the source isn't whitelisted.

```csharp
csp.AddDefaultSrc().Self();           // fallback: block unlisted sources
csp.AddStyleSrc().Self().UnsafeInline();  // Material injects styles at runtime
csp.AddScriptSrc().Self().UnsafeInline(); // Angular bootstrap uses inline scripts
csp.AddImgSrc().Self().Data().OverHttps(); // data: for Material icons, https: for product images
csp.AddFontSrc().Self().Data();           // Material embeds fonts as data URIs
csp.AddConnectSrc().Self().From("wss:").OverHttps(); // API + SignalR WS + Stripe SDK
```

### Other headers (set by `AddDefaultSecurityHeaders()`)

| Header | Value | Protects against |
|---|---|---|
| `X-Content-Type-Options: nosniff` | Browser won't MIME-sniff | Drive-by downloads via content-type confusion |
| `X-Frame-Options: DENY` | Page can't be embedded in an iframe | Clickjacking |
| `Referrer-Policy: strict-origin-when-cross-origin` | Limits referrer data sent cross-origin | Referrer leakage |

### Permissions-Policy

Separate from CSP. Disables browser hardware/sensor APIs your app doesn't use. `accelerometer=()` disables the Device Accelerometer API — allows JS to read physical tilt/motion from the device sensor. No shop needs that; disabling it prevents injected third-party scripts from silently reading it.

---

## Exercise

1. Open DevTools → Network → pick any API response. Verify these headers are present: `Content-Security-Policy`, `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`.
2. Temporarily remove `.UnsafeInline()` from `AddStyleSrc()`. Run the app. What breaks in the UI and why?
3. In Postman, send a request with `Origin: http://evil.com`. Check the response — does `Access-Control-Allow-Origin` appear? Why not?
4. Temporarily remove `AllowCredentials()` from the CORS policy. Try to log in. What happens and why?

---

## Solution

1. Headers visible in every API response once `UseSecurityHeaders()` is in the pipeline.
2. Angular Material components lose their styles — buttons, inputs, cards render unstyled. Because Material uses `ViewEncapsulation.None` and injects `<style>` blocks directly; CSP blocks inline styles without `unsafe-inline`.
3. No `Access-Control-Allow-Origin` header in the response. The browser sees an origin not in the whitelist and blocks the request entirely. CORS is enforced by the browser, not the server — the server just declares policy.
4. Login POST succeeds but the auth cookie is not sent on subsequent requests. The browser strips credentials on cross-origin requests unless both client and server explicitly opt in. Identity thinks you're anonymous on every request.

---

## Interview Q&A

**Q: What is CORS and why do you need `AllowCredentials()`?**
CORS is a browser mechanism that restricts cross-origin requests. `AllowCredentials()` tells the browser it's allowed to include cookies on cross-origin requests — required for cookie-based auth where the API and frontend are on different origins.

**Q: What is CSP and what does it protect against?**
Content-Security-Policy is an HTTP header that whitelists which sources the browser can load resources from. It's the main defense against XSS — even if an attacker injects a script, the browser refuses to execute it if the source isn't whitelisted.

**Q: Why does your CSP use `unsafe-inline`? Doesn't that defeat the purpose?**
Yes, it weakens the script and style directives. It's a deliberate tradeoff — Angular Material injects styles into the DOM at runtime and Angular's build output includes inline bootstrap scripts. Removing `unsafe-inline` would require implementing nonce-based CSP at the Angular renderer level. For a dev/CV project that's not worth the complexity. In a high-security production app you'd use nonces or hash-based CSP.

**Q: What's the difference between CORS and CSP?**
CORS controls which origins are allowed to make requests *to* your server. CSP controls which sources your browser is allowed to load resources *from*. CORS is enforced by the browser on outgoing requests; CSP is enforced by the browser on incoming content.

---

claude --resume 995fca5a-6c08-4805-abbf-e8e860fc5616
