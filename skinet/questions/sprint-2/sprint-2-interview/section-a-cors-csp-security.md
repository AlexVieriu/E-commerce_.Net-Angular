## SECTION A — CORS & CSP & SECURITY HEADERS

### EASY

**A1.** What is CORS? Why does it exist? What happens if you don't configure CORS on your API?

<details><summary>Answer</summary>

**CORS** (Cross-Origin Resource Sharing) is a **browser security mechanism** that blocks JavaScript from making requests to a different origin (different domain, port, or protocol) than the page that loaded it.

- **Why it exists:** enforces the **Same-Origin Policy** — prevents a malicious site from silently calling your bank's API using the victim's cookies.
- **Without CORS configured:** every cross-origin AJAX call from your Angular app is blocked by the browser **before it reaches your server**. The .NET API never sees the request — the browser rejects it client-side.
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

Protects against **clickjacking** — the page is embedded in an invisible `<iframe>` on a malicious site; the attacker overlays a fake UI on top, tricking users into clicking real buttons (e.g. "Confirm Transfer").

- **`DENY`** — never render this page inside any frame, on any site (including same-origin).
- **`SAMEORIGIN`** — allow framing only from the same origin.

Modern replacement: `Content-Security-Policy: frame-ancestors 'none'` (more flexible, same effect).
</details>

---

**A4.** What does `X-Content-Type-Options: nosniff` do?

<details><summary>Answer</summary>

Prevents **MIME type sniffing** — some browsers "guess" the content type by inspecting response bytes, ignoring the declared `Content-Type`.

**Attack without `nosniff`:** attacker uploads an HTML file with `Content-Type: image/png`. Browser sniffs the bytes, detects HTML, and executes it as a script → XSS.

**With `nosniff`:** browser strictly trusts the declared `Content-Type` and refuses to execute content that doesn't match. Required on all responses serving user-uploaded content.
</details>

---

### MEDIUM

**A5.** Your Angular app works on `localhost:4200` but after deploying to Azure, API calls fail silently. No errors in the .NET logs. What's wrong?

<details><summary>Answer</summary>

The deployed origin (e.g. `https://myapp.azurestaticapps.net`) is **not in the .NET CORS allowed origins list** — only `localhost` was added during development.

**What happens:**
1. Browser sends preflight or request with `Origin: https://myapp.azurestaticapps.net`.
2. Server responds **without** `Access-Control-Allow-Origin`.
3. Browser blocks JavaScript from reading the response — client-side rejection.
4. .NET logs show nothing (or a 200 Angular never sees) because the blocking is in the browser, not the server.

**Fix:** add the production origin to your CORS policy and redeploy.
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

By default, browsers **strip cookies and `Authorization` headers** from cross-origin requests.

**`AllowCredentials()` + `withCredentials: true` on the client** tells the browser: include cookies and auth headers in this cross-origin request.

**Without it:**
- Browser sends the request without the session cookie.
- Server sees an unauthenticated request → returns 401.
- Cookie-based auth **never works** across origins.

**Constraint:** `AllowCredentials()` requires a specific origin — you **cannot** combine it with `AllowAnyOrigin()` (wildcard `*`).
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

- **Config B is both insecure AND invalid at runtime.**
  - .NET throws `InvalidOperationException` at startup: `AllowAnyOrigin()` + `AllowCredentials()` is forbidden by the CORS spec.
  - Conceptually: any website on the internet could make credentialed requests with the user's cookies, calling your API as the authenticated user.
- **Config A is correct** — credentials are only accepted from a specific, trusted origin.

```csharp
// ✅ Correct
p.WithOrigins("https://myapp.com").AllowAnyHeader().AllowCredentials()

// ❌ Runtime exception + security hole
p.AllowAnyOrigin().AllowAnyHeader().AllowCredentials()
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

`SetIsOriginAllowed(_ => true)` is a lambda that always returns `true` — every origin on the internet is allowed.

**Why it's more dangerous than `AllowAnyOrigin()`:**
- `AllowAnyOrigin()` sets `*` — .NET **blocks** combining it with `AllowCredentials()`.
- `SetIsOriginAllowed(_ => true)` **bypasses that guard** by echoing the exact origin the browser sent, while still passing .NET's own validation check. Combined with `AllowCredentials()`, any website can make fully authenticated credentialed requests using the victim's cookies.

**Attack scenario:** attacker hosts `evil.com`. Victim is logged in to `myapp.com`. `evil.com` script calls `POST /api/orders` — browser sends the victim's cookie, your API accepts it, order placed.

**Corrected version:**
```csharp
app.UseCors(x => x
    .WithOrigins("https://myapp.com", "https://staging.myapp.com")
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials());
```
</details>

---

**A13.** Angular Material uses `ViewEncapsulation.None` which injects global `<style>` tags. Why does this conflict with CSP? What would a production-grade solution look like? Explain the nonce-based approach.

<details><summary>Answer</summary>

`ViewEncapsulation.None` — Angular doesn't scope component styles; instead it dumps them as plain `<style>` tags into `<head>` **at runtime via JavaScript**.

**The conflict:** CSP's `style-src` (or `default-src` fallback) blocks inline styles unless `'unsafe-inline'`, a hash, or a nonce is present. Static hashes can't work because Angular injects styles dynamically — the original HTML has no `<style>` tags to hash.

**Production-grade solution — nonce-based approach:**
1. Server generates a **random nonce** per request (e.g. `crypto.randomBytes(16).toString('base64')`).
2. Nonce is embedded in the CSP header: `style-src 'nonce-abc123'`.
3. Angular's **SSR renderer** attaches the nonce to each `<style>` tag it outputs.
4. Browser only executes `<style>` tags whose nonce matches the header.

**Requires Angular SSR** — cannot be done with client-side-only rendering because there's no server to generate the nonce per request.
</details>

---

**A14.** You use `SameSite=Strict` for CSRF protection. A colleague argues you should add anti-forgery tokens (`[ValidateAntiForgeryToken]`) too. Are they right? When would one not be enough?

<details><summary>Answer</summary>

Your colleague is right. `SameSite=Strict` is strong but has gaps:
- It's a **client-side browser feature** — doesn't protect if the browser is old, the cookie predates the `SameSite` attribute, or the attacker controls a subdomain of your domain.
- A subdomain cookie scope (`Domain=.example.com`) means a compromised subdomain can still send your cookie on same-site requests.

**Anti-forgery tokens (synchronizer token pattern)** are **server-side and independent of cookie attributes** — they provide defence-in-depth. The server generates a unique token per session, embeds it in the form/header, and rejects requests where it's missing or wrong.

**Rule:** neither alone covers all attack scenarios — use both for sensitive state-changing operations.
</details>

---
