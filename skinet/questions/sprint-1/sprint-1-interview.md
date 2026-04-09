# Sprint 1 — Competitive Interview Questions

> Topics: CI/CD, GitHub Actions, secrets management, Identity hardening, cookies, config providers
> Target: .NET mid-level + Angular junior
> Difficulty: 20% Easy | 40% Medium | 40% Hard
> Goal: Top 1% preparation

---

## SECTION A — C# / .NET FUNDAMENTALS

### EASY

**A1.** What is the difference between `readonly` and `const` in C#? When would you use each?

**A2.** What does `sealed` do on a class? Why would you seal a class?

**A3.** What is the difference between `IEnumerable<T>` and `IList<T>`? When do you choose one over the other?

**A4.** What does `async void` do differently than `async Task`? Why is `async void` dangerous?

**A5.** Explain boxing and unboxing. Give one example where boxing happens implicitly in C#.

---

### MEDIUM

**A6.** What does this code print and why?

```csharp
string a = "hello";
string b = "hello";
string c = new string("hello".ToCharArray());

Console.WriteLine(a == b);
Console.WriteLine(a == c);
Console.WriteLine(ReferenceEquals(a, b));
Console.WriteLine(ReferenceEquals(a, c));
```

**A7.** You have a `List<Order>` with 50,000 orders. You need to find all orders where `Status == "Shipped"` and `Total > 100`, sorted by `Total` descending, and return only the top 10. Write the LINQ query. Now explain: does LINQ evaluate this lazily or eagerly? At what point does the query execute?

**A8.** What is the difference between `Task.Run()` and `Task.Factory.StartNew()`? When would you explicitly choose one over the other?

**A9.** Explain the difference between `ConfigureAwait(false)` and the default `ConfigureAwait(true)`. In what context does this matter? Does it matter in ASP.NET Core?

**A10.** What is the `IDisposable` pattern? What happens if you forget to dispose an `HttpClient` in a loop? How does `IHttpClientFactory` solve this?

**A11.** What does this code do? Is there a bug?

```csharp
public async Task<List<Product>> GetAllAsync()
{
    var tasks = productIds.Select(id => GetByIdAsync(id));
    var results = await Task.WhenAll(tasks);
    return results.ToList();
}
```

**A12.** What is the difference between `throw;` and `throw ex;` in a catch block? Why does it matter?

**A13.** You have a service registered as `Scoped`. A `Singleton` service tries to inject it. What happens? What is the name of this problem?

---

### HARD

**A14.** Explain the .NET garbage collector's generational model. What are Gen 0, Gen 1, Gen 2? What triggers a collection? What is the Large Object Heap and why does it matter?

**A15.** What is `Span<T>` and why was it introduced? How does it reduce allocations compared to `Substring()`? Write a code example that parses a comma-separated string using `Span<T>` without allocating new strings.

**A16.** You see this in a code review:

```csharp
public class OrderProcessor
{
    private static readonly HttpClient _client = new HttpClient();

    public async Task ProcessAsync(Order order)
    {
        var response = await _client.PostAsJsonAsync("/api/process", order);
        response.EnsureSuccessStatusCode();
    }
}
```

What are the two subtle problems with this code? How would you fix each?

**A17.** Explain `ValueTask<T>` vs `Task<T>`. When is `ValueTask<T>` faster? When is it dangerous? What happens if you `await` a `ValueTask<T>` twice?

**A18.** What are `record` types in C#? How do they differ from classes in terms of equality, immutability, and generated code? What is `with` expression? When would you use `record struct` vs `record class`?

**A19.** You have a hot code path that creates thousands of small temporary objects per request. The GC is causing latency spikes. What strategies can you use to reduce GC pressure? Name at least four.

---

## SECTION B — ASP.NET CORE & MIDDLEWARE

### EASY

**B1.** What is middleware in ASP.NET Core? How is it different from a filter?

**B2.** What is the difference between `AddScoped`, `AddTransient`, and `AddSingleton`? Give a real-world example for each.

**B3.** What does `app.UseAuthentication()` do? What happens if you put it AFTER `app.UseAuthorization()` in the pipeline?

**B4.** What does `[ApiController]` attribute do on a controller? Name at least three behaviors it enables.

---

### MEDIUM

**B5.** Your API returns 500 on every request. You add `app.UseDeveloperExceptionPage()`. Now you see the stack trace. In production, this is removed. How do you handle exceptions in production without leaking internals? Design an exception handling middleware.

**B6.** What is the request pipeline order for: `UseRouting`, `UseCors`, `UseAuthentication`, `UseAuthorization`, `UseEndpoints`? What breaks if you swap `UseCors` and `UseAuthentication`?

**B7.** Explain `IOptions<T>`, `IOptionsSnapshot<T>`, and `IOptionsMonitor<T>`. Which reloads on config change? Which is safe for Singleton services? Which would you use for secrets that rotate?

**B8.** What does `[FromBody]`, `[FromQuery]`, `[FromRoute]`, and `[FromHeader]` do? When does `[ApiController]` infer the source automatically? What source does it infer for complex types?

**B9.** You deploy to Azure App Service. The app starts but returns 500 on all endpoints. The log says "Unable to resolve service for type 'IOrderService'." What happened? How do you fix it?

---

### HARD

**B10.** Design the middleware pipeline for an e-commerce API that needs: exception handling, CORS, CSP headers, authentication, authorization, rate limiting, and request logging. Draw the order and explain why each piece goes where it does.

**B11.** You're building a multi-tenant SaaS app. Each tenant has its own database. How do you resolve a `DbContext` with a connection string that depends on the current request's tenant header? Which DI lifetime do you use and why?

---

## SECTION C — SECURITY (Identity, Cookies, Secrets)

### EASY

**C1.** What does `HttpOnly` on a cookie prevent? What attack does it mitigate?

**C2.** What is the difference between authentication and authorization?

**C3.** What does `SameSite=Strict` protect against?

---

### MEDIUM

**C4.** Explain the three `SameSite` cookie values: `Strict`, `Lax`, and `None`. For each, describe when the browser sends the cookie and what attack it prevents or allows.

**C5.** A user reports: "I log in successfully, but every API call after that returns 401." The login endpoint returns 200. What are the top 5 things you check?

**C6.** What is `SecurityStamp` in ASP.NET Identity? When is it updated? How does the cookie middleware use it? What happens if you set `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero`?

**C7.** What is `DefaultAzureCredential`? In Azure App Service, which auth method fires? Locally? What's the fixed order it tries?

**C8.** Why does `SlidingExpiration = false` matter for security? What's the risk if sliding expiration is enabled and an attacker steals the cookie?

**C9.** A new developer clones the repo and runs `dotnet run`. They get `StripeSettings:SecretKey is not configured`. What do they do? Why can't they put it in `appsettings.json`?

---

### HARD

**C10.** A penetration tester finds that your `SameSite=Strict` cookie is not sent when a user clicks a link from Gmail to your site. The user lands unauthenticated. The product team says this is unacceptable UX. How do you fix it? What security do you lose? How do you compensate?

**C11.** Your cookie has `SameSite=Strict`, `HttpOnly`, and `Secure`. A colleague claims the app is immune to session hijacking. List every remaining attack vector and mitigation for each.

**C12.** Explain how ASP.NET Identity's password hashing works under the hood. What algorithm does it use? What is key stretching? What is a salt? Why is `bcrypt`/`PBKDF2` better than `SHA256(password + salt)` for passwords? How many iterations does Identity use by default?

**C13.** A user reports: "I changed my password but I'm still logged in on my phone from yesterday." Why? How do you fix it? What's the tradeoff of immediate invalidation?

**C14.** Compare three approaches to storing session state: (1) encrypted cookie (your current approach), (2) server-side session with Redis, (3) JWT in localStorage. For each: where is state stored, what's the size limit, how do you revoke, what's the XSS/CSRF attack surface?

**C15.** You need to add MFA (multi-factor authentication) on top of your current cookie-based Identity setup. What changes in the backend, frontend, and user flow? What does `SignInManager.PasswordSignInAsync` return when MFA is required?

---

## SECTION D — CI/CD & DEVOPS

### EASY

**D1.** What is the difference between `npm ci` and `npm install`? Which should you use in CI?

**D2.** What triggers a GitHub Actions workflow? Name three event types.

**D3.** What does `needs: [build]` do in a GitHub Actions job?

**D4.** In the .NET config provider chain, if the same key exists in `appsettings.json` and User Secrets, which wins? Why?

---

### MEDIUM

**D5.** Your CI pipeline has three jobs: `build`, `test`, `deploy`. The `deploy` job runs even when `test` fails. What's wrong? Write the corrected YAML.

**D6.** You have secrets stored in: `appsettings.json`, `appsettings.Development.json`, environment variables, User Secrets, and Azure Key Vault. Draw the provider chain. For each provider, state: when it's used, what belongs there, and whether it's committed to git.

**D7.** What is OIDC federation in the context of GitHub Actions and Azure? Why is it better than storing a client secret in GitHub Secrets?

**D8.** Your `dotnet test` passes locally but fails on CI. The error is "Unable to find testhost." Name three common causes and how you'd debug each.

**D9.** You rotate the Stripe secret key in Azure Key Vault. Your production app still uses the old key. Why? How do you fix it without downtime?

**D10.** Your CI ran `dotnet test` and 12 tests failed on the first push — but they passed locally. What categories of bugs cause this and how do you investigate?

**D11.** Your `dotnet test` in CI passes but `ng build` fails. The deploy job still runs. What's missing?

---

### HARD

**D12.** Design a CI/CD pipeline for an e-commerce app with: staging and production environments, database migrations, feature flags, rollback capability, and zero-downtime deployment. Include the YAML structure and explain each step.

**D13.** Your Key Vault is in Azure region West Europe. Your app is in East US. Every startup takes 2 seconds because of key loading latency. On a cold start, it takes 8 seconds. How do you optimize this? Consider: caching, regional replication, startup warm-up, and configuration reload intervals.

**D14.** A junior developer accidentally pushes a secret to GitHub. The secret is now in the git history. What are the immediate steps (first 5 minutes)? What are the follow-up steps? Why is `git rebase` not enough?

**D15.** Your team wants to add a second environment (staging). Staging deploys on merge to `development`, production on merge to `main`. Both use Key Vault but with different secrets. Design the pipeline and config changes.

**D16.** You need to add secret rotation without downtime. The current setup loads secrets at startup only. Design a solution. Compare polling `ReloadInterval`, blue-green deploy, and `IOptionsSnapshot<T>`.

---

## SECTION E — ARCHITECTURE & DESIGN PATTERNS

### EASY

**E1.** What is the Repository pattern? Why use it instead of injecting `DbContext` directly into controllers?

**E2.** What is the difference between the Repository pattern and the Unit of Work pattern? How do they work together?

---

### MEDIUM

**E3.** Your app has three layers: Core (domain), Application (services), Infrastructure (EF, Redis, Stripe). Draw the dependency graph. Which layer depends on which? What is the Dependency Inversion Principle and how does your architecture apply it?

**E4.** What is the difference between the MVC pattern and the API controller pattern in ASP.NET Core? Why don't you return Views from an API controller?

**E5.** You need to add a "wishlist" feature. Walk through which layer each piece of code goes in: entity, repository interface, repository implementation, service, controller, DTO. Why does the repository interface live in Core and the implementation in Infrastructure?

---

### HARD

**E6.** Compare these architectures for an e-commerce backend: (1) modular monolith, (2) microservices, (3) serverless functions. For each: how would you split orders/payments/inventory? What's the communication pattern? Where does the transaction boundary live? When would you choose each?

---

## SECTION F — ANGULAR FUNDAMENTALS

### EASY

**F1.** What is the difference between `ngOnInit` and the constructor in an Angular component?

**F2.** What is dependency injection in Angular? How do you inject a service into a component?

**F3.** What does `providedIn: 'root'` do on a service? What's the alternative?

**F4.** What is the difference between `*ngIf` and `@if` (new control flow syntax)?

**F5.** What does the `async` pipe do? Why is it preferred over manually subscribing in the component?

---

### MEDIUM

**F6.** Explain Angular's change detection. What triggers it? What is `OnPush` change detection? When does `OnPush` NOT trigger change detection?

**F7.** What is `ViewEncapsulation` in Angular? What are the three modes? Why does Angular Material use `ViewEncapsulation.None`?

**F8.** What is the difference between `template-driven` forms and `reactive` forms? When would you choose each? Which is easier to test?

**F9.** Explain Angular routing: what is a `RouterOutlet`? What is lazy loading? How do you lazy-load a standalone component? What is a route guard?

**F10.** What does this code do? Is there a memory leak?

```typescript
@Component({...})
export class ProductListComponent implements OnInit {
  products: Product[] = [];

  constructor(private productService: ProductService) {}

  ngOnInit() {
    this.productService.getAll().subscribe(p => this.products = p);
  }
}
```

**F11.** What is an Angular interceptor? Give three real-world use cases. How do you register one with the new `provideHttpClient(withInterceptors([...]))` syntax?

---

### HARD

**F12.** Explain RxJS operators: `switchMap`, `mergeMap`, `concatMap`, `exhaustMap`. For each, give a real-world use case and explain what happens when a new observable emits before the previous one completes.

**F13.** What is tree-shaking? How does Angular's build process use it? What prevents a service from being tree-shaken? How does `providedIn: 'root'` enable tree-shaking compared to registering in `providers: []`?

---

## SECTION G — RAPID-FIRE SYNTAX (30 seconds each)

**G1.** What does `??=` do in C#? Give an example.

**G2.** What does `?.` do? What does `!` (null-forgiving operator) do? When is `!` dangerous?

**G3.** What is the difference between `string?` and `string` in a nullable-enabled context?

**G4.** What does `init` accessor do on a property? How is it different from `set`?

**G5.** What does `required` keyword do on a property in C# 11?

**G6.** What does `inject()` function do in Angular? How is it different from constructor injection?

**G7.** What does `takeUntilDestroyed()` do? Why is it important for preventing memory leaks?

**G8.** Write a one-liner: convert `string[] names` to a comma-separated string of names longer than 3 characters, sorted alphabetically.

---

## SECTION H — BEHAVIORAL & PROBLEM-SOLVING

**H1.** A senior developer insists on using JWT stored in localStorage for authentication. You believe cookies with HttpOnly are more secure. How do you make your case without being combative? What are the legitimate arguments for JWT that you should acknowledge?

**H2.** Your PR has 47 files changed. The reviewer says "This is too large to review." How do you handle this? How do you prevent it next time?

**H3.** You find a bug in production that affects 0.1% of orders. The fix is simple but the root cause is unclear. Do you deploy the fix immediately or investigate the root cause first? Justify your answer.

---

**Total: ~75 questions across 8 sections**
**Distribution: ~15 Easy (20%) | ~30 Medium (40%) | ~30 Hard (40%)**

---

> "The difference between a mid-level developer and a senior is not what they can build — it's what they can explain, defend, and debug under pressure."
