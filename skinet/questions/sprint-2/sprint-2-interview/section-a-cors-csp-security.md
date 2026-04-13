## SECTION A — CORS & CSP & SECURITY HEADERS

### EASY

**A1.** What is CORS? Why does it exist? What happens if you don't configure CORS on your API?

<details><summary>Answer</summary>

CORS (Cross-Origin Resource Sharing) is a browser security mechanism that blocks JavaScript from making requests to a different origin (different domain, port, or protocol) than the page that loaded it. It exists to enforce the **Same-Origin Policy** — preventing a malicious site from silently calling your bank's API using the victim's cookies. Without CORS configured on your API, every cross-origin AJAX call from your Angular app will be blocked by the browser before it even reaches your server. The .NET API never sees the request.
</details>

---

**A2.** What's the difference between CORS and CSP?

<details><summary>Answer</summary>

- **CORS** — controls which *external origins* can call your API. Server-side configuration that tells browsers "I accept requests from these origins." Protects your **API** from being called by unauthorized sites.
- **CSP** (Content Security Policy) — controls what resources your **own web page** is allowed to load: scripts, styles, images, fonts, frames. Protects your **frontend** from injected malicious content (XSS).

Both are enforced by the browser, but they solve completely different problems.
</details>

---

**A3.** What does `X-Frame-Options: DENY` protect against?

<details><summary>Answer</summary>

It protects against **clickjacking** — an attack where your page is embedded inside an invisible `<iframe>` on a malicious site. The attacker overlays their UI on top of your page, tricking users into clicking your buttons (e.g., "Confirm Transfer") while thinking they're clicking something else. `DENY` tells the browser: never render this page inside any frame, anywhere.
</details>

---

**A4.** What does `X-Content-Type-Options: nosniff` do?

<details><summary>Answer</summary>

It prevents **MIME type sniffing**. Without it, browsers try to "guess" the content type of a response by inspecting its bytes, even if the server declared a different `Content-Type`. An attacker could upload an HTML file disguised as an image (`Content-Type: image/png`), and without `nosniff`, some browsers would execute it as HTML/script. With `nosniff`, the browser strictly trusts the declared `Content-Type` and refuses to execute mismatched content.
</details>

---

### MEDIUM

**A5.** Your Angular app works on `localhost:4200` but after deploying to Azure, API calls fail silently. No errors in the .NET logs. What's wrong?

<details><summary>Answer</summary>

The deployed origin (`https://myapp.azurestaticapps.net` or similar) isn't in your .NET CORS allowed origins list. Your localhost origin was allowed during development. The browser sends the preflight/request, the server responds without the correct `Access-Control-Allow-Origin` header, and the browser blocks the JS from reading the response. Because the browser blocks it client-side before processing the actual response body, your .NET logs may show nothing (for simple requests) or a 200 response that Angular never sees.
</details>

---

**A6.** What is a preflight request? When does the browser send one? What headers does it include? What must the server respond with for the preflight to succeed?

<details><summary>Answer</summary>

A preflight is an automatic `OPTIONS` request the browser sends before a "non-simple" cross-origin request. It asks the server: "Will you accept this kind of request?" The server must respond with the right `Access-Control-Allow-*` headers. Only if the preflight succeeds does the browser send the real request.

- **Triggers** — any request with a custom header (`Authorization`, `Content-Type: application/json`), or a method other than GET/POST/HEAD.
- **Preflight request headers** — `Origin`, `Access-Control-Request-Method`, `Access-Control-Request-Headers`
- **Required server response** — `Access-Control-Allow-Origin`, `Access-Control-Allow-Methods`, `Access-Control-Allow-Headers` (and optionally `Access-Control-Max-Age`)
</details>

---

**A7.** You add `Content-Security-Policy: default-src 'self'` to your API. Your Angular app's Material components break. The console shows CSP violation errors for inline styles. Explain why and give two solutions with different security trade-offs.

<details><summary>Answer</summary>

Angular Material injects `<style>` tags directly into the DOM at runtime via JavaScript. CSP `default-src 'self'` covers `style-src`, which by default blocks all inline styles. The browser sees these dynamically injected `<style>` tags and blocks them.

**Solution 1 — `'unsafe-inline'`** (weaker): `style-src 'self' 'unsafe-inline'` — allows all inline styles everywhere. Easy to implement but defeats much of the XSS protection CSP provides for styles.

**Solution 2 — Nonce** (stronger): Server generates a random nonce per request, adds it to the CSP header and to every `<style>` tag. Only `<style>` tags with the matching nonce are allowed. Harder to implement with Angular but provides real protection.
</details>

---

**A8.** Why does `AllowCredentials()` in CORS matter for cookie auth? What happens if you remove it?

<details><summary>Answer</summary>

By default, browsers strip cookies and `Authorization` headers from cross-origin requests. `AllowCredentials()` on the server, combined with `withCredentials: true` on the client, tells the browser: "include cookies and auth headers in this cross-origin request." Without it, your cookie-based auth never works across origins — the browser sends the request without the session cookie, the server sees an unauthenticated request, and returns 401.
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

---

**A13.** Angular Material uses `ViewEncapsulation.None` which injects global `<style>` tags. Why does this conflict with CSP? What would a production-grade solution look like? Explain the nonce-based approach.

<details><summary>Answer</summary>

`ViewEncapsulation.None` means Angular doesn't scope component styles — instead it dumps them as plain `<style>` tags into the document's `<head>` at runtime, injected by JavaScript. CSP's `style-src` directive (or `default-src` fallback) blocks inline styles unless `'unsafe-inline'`, a hash, or a nonce is present. Since Angular injects these styles dynamically via JS (not in the original HTML), static hashes don't work. A production-grade solution uses **Angular SSR with nonces**: the server generates a fresh nonce per request, embeds it in the CSP header, and Angular's server-side renderer attaches it to each `<style>` tag it outputs.
</details>

---

**A14.** You use `SameSite=Strict` for CSRF protection. A colleague argues you should add anti-forgery tokens (`[ValidateAntiForgeryToken]`) too. Are they right? When would one not be enough?

<details><summary>Answer</summary>

Your colleague is right. `SameSite=Strict` is strong but has gaps:
- it's a client-side browser feature — doesn't work if the cookie predates the SameSite attribute, the user's browser is old, or the attacker controls a subdomain

Anti-forgery tokens (synchronizer token pattern) are server-side and independent of cookie attributes — they provide defence-in-depth. Neither alone covers all attack scenarios.
</details>

---
