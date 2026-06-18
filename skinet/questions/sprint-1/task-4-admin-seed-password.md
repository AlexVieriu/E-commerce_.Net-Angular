# Task 4 — Admin Seed Password → Env Var / Key Vault

## Why this approach

The hardcoded `"Pa$$w0rd"` in `StoreContextSeed.cs` was visible to anyone viewing the GitHub repo. Moving it to config follows the .NET config provider chain: User Secrets (dev) → Azure Key Vault (prod). The app reads `AdminSettings:Password` at startup — the source changes per environment, the code doesn't.

We also only read the password inside the `if (!userManager.Users.Any(...))` block — so after the admin is seeded once, the password never needs to be in Key Vault again. You can delete the secret after first deploy.

## What confused me

- Why store it at all if I know the password? Because the app reads it from config on startup — it needs to be somewhere accessible to the process, not just in your head.
- Why Key Vault and not App Service Environment Variables? Passwords are credentials → Key Vault. Non-sensitive config (URLs, feature flags) → App Settings.
- Key Vault naming uses `--` as separator: `AdminSettings:Password` (code) → `AdminSettings--Password` (Key Vault).

## Explanation

### Config provider chain

.NET reads config from multiple sources in order. Later sources **override** earlier ones:

```
appsettings.json
  → appsettings.{Environment}.json
    → Environment Variables
      → User Secrets (dev only)
        → Azure Key Vault (prod only)
```

`IConfiguration["AdminSettings:Password"]` just reads the final merged value — it doesn't care where it came from.

### Program.cs refactor

Extracted two extension methods to keep `Program.cs` clean (~70 lines):

- `AddApplicationServices(config, env)` on `IServiceCollection` — all DI registrations
- `SeedDatabaseAsync()` on `WebApplication` — migrations + seed

### Angular init flow fix (discovered during testing)

The original `init.service.ts` used `forkJoin` to fetch user-info and cart **in parallel**. This crashed the app because:

1. `CartController` has `[Authorize]`
2. If not logged in, cart returns 401
3. 401 in `forkJoin` killed the entire Observable chain before Angular rendered

Fixed with a sequential flow using `switchMap`:
1. Check auth first (`getUserInfo`)
2. Only fetch cart **if authenticated**

```ts
return this.accountService.getUserInfo().pipe(
  catchError(err => err.status === 401 ? of(null) : throwError(err)),
  tap(user => { if (user) this.signalrService.createHubConnection(); }),
  switchMap(user => {
    if (!user || !cartId) return of({ user, cart: null });
    return this.cartService.getCartAsync(cartId).pipe(
      map(cart => ({ user, cart })),
      catchError(() => { localStorage.removeItem('cart_id'); return of({ user, cart: null }); })
    );
  })
);
```

**Key operators:**
- `catchError` — catches errors in the stream, returns a fallback Observable
- `tap` — side effect (SignalR connect) without changing the value
- `switchMap` — takes a value, returns a new Observable (used when next step is async)
- `of(value)` — wraps a plain value in an Observable that emits immediately

## Exercise

1. What happens if you use `map` instead of `switchMap` when the transformation returns an Observable?
2. Why does `forkJoin` fail when one of its sources errors?
3. In the config chain, if the same key exists in both `appsettings.json` and User Secrets, which wins?

## Solution

1. You get `Observable<Observable<Cart>>` instead of `Observable<Cart>` — a nested Observable that never unwraps. `switchMap` flattens it automatically.
2. `forkJoin` waits for all sources to complete. If any source errors, it immediately errors the whole thing — no partial results.
3. User Secrets wins — it's registered later in the chain and later registrations override earlier ones.

## Interview Q&A

**Q: How do you manage secrets in .NET?**
A: Config provider chain — User Secrets locally, Azure Key Vault in production via `AddAzureKeyVault` + `DefaultAzureCredential`. The app reads `IConfiguration["key"]` and doesn't know or care which source provided it. Last provider registered wins on conflicts.

**Q: What's the difference between User Secrets and environment variables?**
A: User Secrets are stored outside the project directory (`%APPDATA%/Microsoft/UserSecrets/`) so they never end up in git. Environment variables are process-level. Both override `appsettings.json`. Key Vault is for production — it's a managed secret store with access control and audit logs.

**Q: Why use `switchMap` instead of `map` for chaining HTTP calls?**
A: `map` transforms a value into another value. `switchMap` transforms a value into an Observable and flattens it. When the next step is an async operation (HTTP call), you need `switchMap` — `map` would give you `Observable<Observable<T>>` which never resolves.

**Q: What is `forkJoin` and when does it fail?**
A: `forkJoin` runs multiple Observables in parallel and emits when all complete. It fails if any source errors — it doesn't emit partial results. Use it only when all parallel operations are guaranteed to succeed or have their own error handling.

---

claude --resume 5b910fd9-a4b1-4c3c-91d8-036c9c42f908
