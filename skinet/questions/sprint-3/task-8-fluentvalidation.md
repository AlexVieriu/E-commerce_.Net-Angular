# Task 8 — FluentValidation (auto-validation via a custom action filter)

## Why this approach

- DTO rules used to live as **DataAnnotations** on the DTO itself (`[Required]`, `[Range]`), auto-run by `[ApiController]` during model binding → 400.
- Fine for trivial rules, but couples validation to the data contract, can't inject dependencies (e.g. "name must be unique" needs the DB), and is awkward for cross-field / conditional rules.
- Migrated to **FluentValidation**: rules move into separate `AbstractValidator<TDto>` classes — testable in isolation, DI-friendly (async DB checks), composable (`SetValidator`).
- Architecture reason — **portable**: a validator is a plain Application-layer class with no MVC coupling, so it travels with its module if a microservice is ever extracted. Also the natural fit for a future MediatR `ValidationBehavior` pipeline (the empty `Commands/Behaviors/` folders scaffold for this).
- The one real decision: *how to trigger* FluentValidation, since `[ApiController]` doesn't run it. Two options — a hand-rolled action filter, or the `SharpGrip.FluentValidation.AutoValidation` package.
- **Chose the custom filter (option A)** — ~25 lines, no dependency, full control of the 400 shape, high learning/interview value. The wiring is throwaway at the MediatR migration anyway, so nothing lost by writing it myself.

## What confused me — the actual gaps from the cold test / LEARN session

**Triage — drill only what pays off.** OWN = explain cold for interviews · RECOGNIZE = re-derive on the spot, don't memorize · LET GO = trivia, the one-line summary is enough.

| Gap | Tier | Where |
|---|---|---|
| FluentValidation vs DataAnnotations / why a filter at all | **OWN** | `ValidationFilter.cs` + wiring |
| Scoped registration / captive dependency | **OWN** | `ApplicationServicesExtensions.cs` |
| `ModelState.IsValid` is computed, not stored | **OWN** | `ValidationFilter.cs`, final `if` |
| Unit tests: Arrange/Act/Assert, `[Fact]` vs `[Theory]`, `TestValidate` | **OWN** | `Validation_Tests.cs` |
| `continue` vs `break` / `return` | RECOGNIZE | `ValidationFilter.cs`, guard clauses |
| `is not IValidator validator` pattern matching | RECOGNIZE | `ValidationFilter.cs`, `GetService` line |
| `CascadeMode.Continue` (runs all rules) | RECOGNIZE | concept |
| `typeof(IValidator<>)` + `MakeGenericType` (open generics) | LET GO | `ValidationFilter.cs`, `MakeGenericType` line |
| `System.Type` "business card" | LET GO | same line |
| `ValidationContext<object>` type-erasure internals | LET GO | `ValidationFilter.cs`, `ValidateAsync` line |
| `file static class Validator` shadowing | LET GO | `Validation_Tests.cs` top (task-19 leftover) |

> **For the deadline:** ~4 OWN items here, not 11. The reflection / type-erasure stuff that ate the most time *today* is exactly what to let go tomorrow — understanding it once was the point; retaining it isn't. The **Interview Q&A** section at the bottom is the real revision surface.

The architecture was clear; the **syntax inside the filter** was the fuzzy part. Gaps below in reading order, grouped by where they live — each tagged `(tier · location)`.

**In the filter — top to bottom**

- **`continue`** *(RECOGNIZE · guard clauses)* — skips the rest of *this* loop iteration and jumps to the next item; not `break` (stops the loop) or `return` (exits the method). Used as an early guard so the loop body stays flat.

- **`typeof(IValidator<>)` → `MakeGenericType`** *(LET GO · the `MakeGenericType` line)* — `typeof` always returns a `System.Type`, never an instance. `<>` is an **open generic** (T unfilled); `.MakeGenericType(argument.GetType())` fills T at runtime → a `Type` for `IValidator<CreateProductDTO>`. Still just a `Type`, not a validator.

- **What a `System.Type` holds** *(LET GO · same line)* — a data object *describing* a type (`.Name`, `.IsInterface`, `.GetGenericArguments()` — strings/booleans). No `.ValidateAsync()` on it. It's a **business card**: hand it to DI, get the real object back.

- **`… is not IValidator validator`** *(RECOGNIZE · the `GetService` line)* — one line, three jobs: `GetService` returns the instance or `null`; `is not IValidator validator` tests **and** casts; null/wrong-type → `continue`, else `validator` is live below. *Real-world:* action takes `(CreateProductDTO dto, int id)` — no validator for `int`, so that lap hits `null` → skip, no crash.

- **`new ValidationContext<object>(argument)`** *(LET GO · the `ValidateAsync` line)* — a type-erasure adapter. I hold the non-generic `IValidator` (T known only at runtime), whose `ValidateAsync` takes an `IValidationContext`, not a raw object — so I box the argument. *Real-world:* a courier who only accepts parcels in their official box.

- **Getting the typed object back** *(LET GO · FluentValidation internals)* — `object` is just the compile-time label; the value is still really a `CreateProductDTO`. The concrete validator casts `InstanceToValidate` back to its T (safe — I built it *from* `argument.GetType()`). An apple in a "FRUIT" box is still an apple.

- **`ValidateAsync`** *(RECOGNIZE · the `ValidateAsync` line)* — runs **every** `RuleFor` and returns a `ValidationResult` (`IsValid` + `Errors`). Default `CascadeMode.Continue` collects *all* failures, not just the first → one 400 lists every bad field. `Async` so a rule can hit I/O (`MustAsync`).

- **`!ModelState.IsValid`** *(OWN · the final `if`)* — `IsValid` is computed, not stored: `false` if *any* entry is Invalid. `AddModelError(...)` is what flips an entry to Invalid. No validator → no `AddModelError` → stays valid → `next()` runs.

**In the tests**

- **Unit-test basics** *(OWN · `Validation_Tests.cs`)* — every test is **Arrange → Act → Assert**. `[Fact]` = one case; `[Theory]` + `[InlineData]` = same body, many inputs. Helpers: `TestValidate(dto).ShouldNotHaveAnyValidationErrors()` / `.ShouldHaveValidationErrorFor(x => x.Price)`. Baseline trick: `ValidDto()` is fully valid; each test breaks **one** field.

- **`file static class Validator`** *(LET GO · top of the test file)* — `file` (C# 11) scopes the type to one file; it **shadows** the BCL `Validator` (hence the fully-qualified call). Exists only for the task-19 DataAnnotations tests — FluentValidation uses `TestValidate`.

## Explanation

### 1. DataAnnotations vs FluentValidation

| | DataAnnotations | FluentValidation |
|---|---|---|
| Where rules live | Attributes **on** the DTO | Separate `AbstractValidator<T>` class |
| Auto-run by `[ApiController]` | **Yes** | **No** (needs a trigger) |
| Inject dependencies (DB checks) | No (hacks only) | Yes (constructor injection) |
| Cross-field / conditional | Awkward | `When()`, `RuleFor`, custom rules |
| Unit testing | Manual reflection helper | First-class `TestValidate(...)` |
| Coupling | Welded to the contract | Decoupled, portable |

### 2. The trigger — `ValidationFilter` (the core, line by line)

```csharp
public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;          // request-scoped DI, NOT the root container

        foreach (var argument in context.ActionArguments.Values)     // each bound parameter
        {
            if (argument is null) continue;                          // guard: skip this lap

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType()); // open → closed Type
            if (services.GetService(validatorType) is not IValidator validator) continue; // resolve + cast, or skip

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument)); // box → validate
            if (result.IsValid) continue;                            // clean → skip

            foreach (var error in result.Errors)
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage); // mark entries Invalid
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
            return;                       // short-circuit: the controller action never runs
        }

        await next();                     // all valid → proceed to the action
    }
}
```

The four `continue`s are the guard chain: **null arg → skip · no validator → skip · valid → skip.** Only the *invalid + has-a-validator* case falls through to `AddModelError`. That's why the body stays flat.

Other key ideas:
- It works for **any** DTO via `MakeGenericType` + `GetService` — no per-type code.
- `RequestServices` is the **per-request** scoped container, not the root one built in `Program.cs` — so resolving a scoped validator (and its scoped `DbContext`) is correct, no captive-dependency bug.
- Reusing `ModelState` + `ValidationProblemDetails` means the 400 body is **identical** to what `[ApiController]` emits for DataAnnotations → the Angular error interceptor needs zero changes.

### 3. Wiring (in `ApplicationServicesExtensions`)

```csharp
services.AddControllers(options => options.Filters.Add<ValidationFilter>());   // global
services.AddValidatorsFromAssemblyContaining<CreateProductDtoValidator>();      // scoped scan
```

`AddValidatorsFromAssemblyContaining<T>` reflection-scans the assembly, finds every `AbstractValidator<T>`, and registers each as a **scoped** `IValidator<T>` — new validators auto-wire, no per-type `AddScoped`.

**Why scoped matters:** a validator may inject a scoped service (a repo / `DbContext`) for an async DB rule. A singleton holding a scoped dependency is a **captive dependency** — the scoped service gets pinned for the app's lifetime. Scoped (the default) avoids it.

### 4. Common built-in validators (the ones actually used)

| Validator | Checks | Used on |
|---|---|---|
| `NotEmpty()` | not null **and** not `""` **and** not whitespace | required strings (Name, City…) |
| `NotNull()` | not null only (`""` passes) | required objects (`ShippingAddress`) |
| `GreaterThan(0)` | value `>` 0 | `Price` |
| `GreaterThanOrEqualTo(1)` | value `>=` 1 | `QuantityInStock` |
| `SetValidator(new X())` | delegate to a child validator | nested `ShippingAddress` |

The trap: **`NotNull` vs `NotEmpty`.** `"name": ""` sails past `NotNull` but is caught by `NotEmpty`. Required text → `NotEmpty`; required object → `NotNull`. (`Must` / `MustAsync` is the escape hatch for any custom or DB rule.)

### 5. The pipeline order (why 401/403 happen *before* 400)

```
Request
  → Authentication / Authorization filter   ← [Authorize(Roles="Admin")] → 401/403 here
    → Action filters (ValidationFilter)      ← FluentValidation → 400 here
      → Controller action                    ← only runs if valid
        → Result / InvalidateCache filter    ← post-action (needs Redis)
```

Proven live: an unauthenticated bad POST returns **401** (never reaches the filter); logged in as admin, the same bad body returns **400** from the filter; a valid body sails into the controller.

### 6. Child validators (composability)

`CreateOrderDtoValidator` validates the nested `ShippingAddress` by delegating to a reusable child:

```csharp
RuleFor(x => x.ShippingAddress)
    .NotNull()
    .SetValidator(new ShippingAddressValidator());   // child errors surface as "ShippingAddress.City"
```

`SetValidator` runs the parent's rules **and** the child's, merging both into one `ValidationResult`; the child's errors get the parent path prefixed (`ShippingAddress.City`). `NotNull()` comes first because **`SetValidator` does nothing on a null child** — drop the `NotNull()` and a null address slips through clean. *(Verified against FluentValidation docs.)*

## Exercise

Add a rule: **product `Name` must be unique** (no existing product with the same name). This requires hitting the database from inside the validator.

1. What does `CreateProductDtoValidator`'s constructor need now?
2. Which FluentValidation method runs an async DB check?
3. Why does the **scoped** registration suddenly matter for this rule, when it didn't for the simple `NotEmpty()` rules?

## Solution

```csharp
public class CreateProductDtoValidator : AbstractValidator<CreateProductDTO>
{
    public CreateProductDtoValidator(IUnitOfWork uow)            // 1. inject the repo
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MustAsync(async (name, ct) =>                       // 2. async DB rule
                !await uow.Repository<Product>().ExistsAsync(p => p.Name == name))
            .WithMessage("A product with this name already exists");
        // ...rest unchanged
    }
}
```

3. The validator now depends on `IUnitOfWork`, which is **scoped** (one per request, wraps the request's `DbContext`). FluentValidation registers validators as scoped by default, so each request gets a fresh validator with a fresh `DbContext`. If the validator were a singleton, it would capture one `DbContext` forever — a **captive dependency** — leading to cross-request data and thread-safety bugs. This is also exactly why the filter resolves from `RequestServices` (the request scope) rather than the root container. (The `ExistsAsync` signature is illustrative; the point is the DI + lifetime.)

## Tests

A unit test is **Arrange → Act → Assert**. FluentValidation ships test helpers, so no reflection helper is needed. The baseline `ValidDto()` returns a fully-valid product; each failing test breaks one field.

| Intent | Test name | Assert |
|---|---|---|
| Happy path | `ShouldPass_WhenAllFieldsAreValid` | `ShouldNotHaveAnyValidationErrors()` |
| One failing field (`[Theory]` 0/-1/-99.99) | `ShouldFail_WhenPriceIsNotPositive` | `ShouldHaveValidationErrorFor(x => x.Price)` |
| Another failing field | `ShouldFail_WhenNameIsEmpty` | `ShouldHaveValidationErrorFor(x => x.Name)` |

```csharp
private readonly CreateProductDtoValidator _validator = new();

[Fact]
public void ShouldPass_WhenAllFieldsAreValid()
    => _validator.TestValidate(ValidDto()).ShouldNotHaveAnyValidationErrors();

[Theory]
[InlineData(0)]
[InlineData(-1)]
public void ShouldFail_WhenPriceIsNotPositive(decimal badPrice)
{
    var dto = ValidDto(); dto.Price = badPrice;          // arrange: break one field
    _validator.TestValidate(dto)                          // act
              .ShouldHaveValidationErrorFor(x => x.Price);// assert
}

// child-validator error uses the dotted path:
// _validator.TestValidate(dto).ShouldHaveValidationErrorFor("ShippingAddress.City");
```

`[Fact]` = one case; `[Theory]` + `[InlineData]` = same body, many inputs. File: `Application.Tests/Common/DTOs/Validation_Tests.cs` — the `CreateProductDto_Validation_Tests` block is the task-8 part (the `RegisterDto_` block + the `file static class Validator` helper came with task-19's DataAnnotations tests). Full suite green.

## Interview Q&A

**1. Why FluentValidation over DataAnnotations?**
- Separation of concerns — rules live in their own class, not welded to the DTO
- DI-friendly — can inject a repo / `DbContext` for async DB rules
- Expressive — cross-field, conditional (`When`), and collection rules
- First-class testability (`TestValidate`) and portability across modules/services
- DataAnnotations stay fine for trivial cases

**2. `[ApiController]` already returns 400 for invalid models — why a filter?**
- `[ApiController]`'s auto-validation only runs **DataAnnotations** via `ModelState`
- It never discovers or runs FluentValidation validators
- The action filter resolves `IValidator<T>` and runs it — without it, the rules are dead code

**3. How does the filter validate *any* DTO with no per-type code?**
- `typeof(IValidator<>).MakeGenericType(argument.GetType())` builds the closed `IValidator<T>` `Type` at runtime
- `GetService(thatType)` resolves the registered validator; cast to the non-generic `IValidator`
- The argument is boxed in `ValidationContext<object>` because the non-generic `ValidateAsync` only takes an `IValidationContext`

**4. `continue` vs `break` vs `return`?**
- `continue` — skip the rest of *this* iteration, go to the next item
- `break` — stop the *whole* loop
- `return` — exit the *whole* method
- The filter uses early `continue`s as guard clauses to keep the loop body flat

**5. Why register validators as scoped?**
- So a validator can inject scoped services (repo / `DbContext`) for async rules
- A singleton validator holding a scoped `DbContext` = captive dependency (pinned for the app's lifetime)
- Scoped (the default) gives each request a fresh validator + `DbContext`; the filter resolves from `RequestServices` (the request scope) for the same reason

**6. Does FluentValidation stop at the first failed rule?**
- No — default `CascadeMode` is `Continue`: it runs every rule and collects all failures
- So one 400 reports every invalid field at once
- You can opt into fail-fast with `CascadeMode.Stop`

**7. In what order do auth and validation run, and why does it matter?**
- Authorization filters run **before** action filters → 401/403 happens *before* validation
- You don't leak "which fields are invalid" to callers who can't even hit the endpoint

**8. How do you keep the frontend unaffected by the switch?**
- The filter returns a `ValidationProblemDetails` built from `ModelState` — the exact shape `[ApiController]` produced for DataAnnotations
- So the existing Angular error interceptor parses it unchanged

**9. What is composability, and where does this task show it?**
- Composability = building bigger behavior by combining small, self-contained pieces through a stable interface — without modifying the pieces
- Here: `SetValidator(new ShippingAddressValidator())` snaps a standalone address validator into `CreateOrderDtoValidator`; the same brick reuses anywhere an address appears
- Payoff: no rule duplication, single responsibility, test the child in isolation, fix a rule once
- It's "**has-a**, not is-a" — favor **composition over inheritance** (an Order *has an* Address, it isn't one)
- Other shapes: `RuleForEach(...).SetValidator(...)` for collections, `Include(other)` to merge rule sets

## Sources

- [Model validation in ASP.NET Core MVC | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation) — `[ApiController]` auto-400 is DataAnnotations-only
- [Filters in ASP.NET Core | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/filters) — authorization filters run before action filters
- [Dependency injection in ASP.NET Core | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection) — captive dependency, service lifetimes
- [Access HttpContext in ASP.NET Core | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-context) — `RequestServices` = per-request scope
- [ModelStateDictionary.cs (dotnet/aspnetcore)](https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Abstractions/src/ModelBinding/ModelStateDictionary.cs) — `IsValid` is a computed property, not a stored flag
- [Dependency Injection — FluentValidation docs](https://docs.fluentvalidation.net/en/latest/di.html) — `AddValidatorsFromAssemblyContaining` defaults to scoped
- [Setting the Cascade mode — FluentValidation docs](https://docs.fluentvalidation.net/en/latest/cascade.html) — `CascadeMode.Continue` default (runs all rules)
- [Built-in Validators — FluentValidation docs](https://docs.fluentvalidation.net/en/latest/built-in-validators.html) — `NotNull` vs `NotEmpty`
- [Creating your first validator — FluentValidation docs](https://docs.fluentvalidation.net/en/latest/start.html) — `SetValidator` for complex properties
- [Test Extensions — FluentValidation docs](https://docs.fluentvalidation.net/en/latest/testing.html) — `TestValidate`
- [Access Modifiers - C# reference | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/access-modifiers) — `file` modifier (C# 11)

---

claude --resume 6badbcf9-2a86-493d-ae6b-1ab21596cd74
