# Task 8 — FluentValidation (auto-validation via a custom action filter)

## Why this approach

DTO rules used to live as **DataAnnotations** on the DTO itself (`[Required]`, `[Range]`). `[ApiController]` runs those automatically during model binding and returns a 400. That's fine for trivial rules but couples validation to the data contract, can't inject dependencies (e.g. "name must be unique" needs the DB), and is awkward for cross-field / conditional rules.

We migrated to **FluentValidation**: rules move into separate `AbstractValidator<TDto>` classes. They're testable in isolation, DI-friendly (async DB checks), composable (`SetValidator`), and — the architecture reason — **portable**: a validator is a plain Application-layer class with no MVC coupling, so it travels with its module if we ever extract a microservice. It's also the natural fit for the future MediatR pipeline (a `ValidationBehavior`) the empty `Commands/Behaviors/` folders are scaffolding for.

The one real decision was *how to trigger* FluentValidation, since `[ApiController]` doesn't run it. Two options: a hand-rolled action filter, or the `SharpGrip.FluentValidation.AutoValidation` package. **Chose the custom filter (option A)** — ~25 lines, no dependency, full control of the 400 shape, and high learning/interview value. The wiring is throwaway at the MediatR migration anyway, so there's nothing to lose by writing it myself.

## What confused me

- **If `[ApiController]` already validates, why do I need a filter at all?**
  Because `[ApiController]`'s automatic validation only runs **DataAnnotations / `ModelState`**. It does **not** discover or run FluentValidation validators. Once the DataAnnotations are gone, nothing runs the rules until *something* resolves `IValidator<T>` and calls it. The filter is that "something."

- **`AddValidatorsFromAssemblyContaining<T>` — why that and not registering each one?**
  It's a reflection scan that finds every `AbstractValidator<T>` in the assembly and registers each as a **scoped** `IValidator<T>`. Less boilerplate, and new validators auto-wire. The alternative (manual `AddScoped` per validator) is more explicit/greppable but you must remember each one.

- **Why is the test helper written as `file static class Validator` with a fully-qualified call inside?**
  `file` (C# 11) limits the type to that one source file. It's named `Validator`, which **shadows** `System.ComponentModel.DataAnnotations.Validator`; `file` stops it leaking, and the call inside must be fully-qualified to reach the real BCL validator past the shadow. It only survives because `RegisterDTO` still uses DataAnnotations — FluentValidation needs no such helper (`TestValidate` is built in).

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
        var services = context.HttpContext.RequestServices;

        foreach (var argument in context.ActionArguments.Values)   // each bound parameter
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType()); // IValidator<CreateProductDTO>
            if (services.GetService(validatorType) is not IValidator validator) continue;  // no validator? skip

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument));
            if (result.IsValid) continue;

            foreach (var error in result.Errors)
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
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

Key ideas:
- It works for **any** DTO via `MakeGenericType` + `GetService` — no per-type code.
- Reusing `ModelState` + `ValidationProblemDetails` means the 400 body is **identical** to what `[ApiController]` emits for DataAnnotations → the Angular error interceptor needs zero changes.
- `context.Result = ...; return;` is what "short-circuits" — `next()` is never awaited, so the action is skipped.

### 3. Wiring (in `ApplicationServicesExtensions`)

```csharp
services.AddControllers(options => options.Filters.Add<ValidationFilter>());   // global
services.AddValidatorsFromAssemblyContaining<CreateProductDtoValidator>();      // scoped scan
```

**Why scoped matters:** a validator may inject a scoped service (a repo / `DbContext`) for an async DB rule. If it were a singleton holding a scoped dependency, that's a **captive dependency** — the scoped service gets pinned for the app's lifetime. Scoped (the default) avoids it.

### 4. The pipeline order (why 401/403 happen *before* 400)

```
Request
  → Authentication / Authorization filter   ← [Authorize(Roles="Admin")] → 401/403 here
    → Action filters (ValidationFilter)      ← FluentValidation → 400 here
      → Controller action                    ← only runs if valid
        → Result / InvalidateCache filter    ← post-action (needs Redis)
```

Proven live: an unauthenticated bad POST returns **401** (never reaches the filter); logged in as admin, the same bad body returns **400** from the filter; a valid body sails past the filter into the controller (and only then tripped on Redis in the `InvalidateCache` filter — unrelated to validation).

### 5. Child validators (composability)

`CreateOrderDtoValidator` validates the nested `ShippingAddress` by delegating to a reusable child:

```csharp
RuleFor(x => x.ShippingAddress)
    .NotNull()
    .SetValidator(new ShippingAddressValidator());   // child errors surface as "ShippingAddress.City"
```

`NotNull()` first so the child validator is only run on a non-null instance.

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

3. The validator now depends on `IUnitOfWork`, which is **scoped** (one per request, wraps the request's `DbContext`). FluentValidation registers validators as scoped by default, so each request gets a fresh validator with a fresh `DbContext`. If the validator were a singleton, it would capture one `DbContext` forever — a **captive dependency** — leading to cross-request data and thread-safety bugs. (The `ExistsAsync` signature is illustrative; the point is the DI + lifetime, not the exact repo method.)

## Tests

FluentValidation ships test helpers — no reflection helper needed:

```csharp
private readonly CreateProductDtoValidator _validator = new();

[Fact]
public void ShouldPass_WhenAllFieldsAreValid()
    => _validator.TestValidate(ValidDto()).ShouldNotHaveAnyValidationErrors();

[Fact]
public void ShouldFail_WhenQuantityIsZero()
{
    var dto = ValidDto(); dto.QuantityInStock = 0;
    _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.QuantityInStock);
}

// child-validator error uses the dotted path:
_validator.TestValidate(dto).ShouldHaveValidationErrorFor("ShippingAddress.City");
```

Files: `Application.Tests/Common/DTOs/Validation_Tests.cs` (product) + `CreateOrderDto_Validation_Tests.cs` (order + nested address). Full suite green: 136 tests.

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

**3. Custom filter vs SharpGrip.AutoValidation — trade-off?**
- Custom filter: no dependency, full control of the 400 shape, transparent/learnable, ~25 lines
- SharpGrip: zero code, but a dependency and a black box
- At my scale (a few DTOs) the boilerplate it saves is negligible
- The wiring is replaced anyway when moving to a MediatR `ValidationBehavior`

**4. Why register validators as scoped?**
- So a validator can inject scoped services (repo / `DbContext`) for async rules
- A singleton validator holding a scoped `DbContext` = captive dependency (pinned for the app's lifetime)
- Scoped (the default) gives each request a fresh validator + `DbContext`

**5. In what order do auth and validation run, and why does it matter?**
- Authorization filters run **before** action filters
- So an unauthorized request gets 401/403 *before* validation ever runs
- You don't leak "which fields are invalid" to callers who can't even hit the endpoint

**6. How do you keep the frontend unaffected by the switch?**
- The filter returns a `ValidationProblemDetails` built from `ModelState`
- That's the exact shape `[ApiController]` produced for DataAnnotations
- So the existing Angular error interceptor parses it unchanged

---

claude --resume <session-id>
