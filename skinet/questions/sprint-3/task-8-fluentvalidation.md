# Task 8 — FluentValidation (auto-validation via a custom action filter)

## Why this approach

DTO rules used to live as **DataAnnotations** on the DTO itself (`[Required]`, `[Range]`). `[ApiController]` runs those automatically during model binding and returns a 400. That's fine for trivial rules but couples validation to the data contract, can't inject dependencies (e.g. "name must be unique" needs the DB), and is awkward for cross-field / conditional rules.

We migrated to **FluentValidation**: rules move into separate `AbstractValidator<TDto>` classes. They're testable in isolation, DI-friendly (async DB checks), composable (`SetValidator`), and — the architecture reason — **portable**: a validator is a plain Application-layer class with no MVC coupling, so it travels with its module if we ever extract a microservice. It's also the natural fit for the future MediatR pipeline (a `ValidationBehavior`) the empty `Commands/Behaviors/` folders are scaffolding for.

The one real decision was *how to trigger* FluentValidation, since `[ApiController]` doesn't run it. Two options: a hand-rolled action filter, or the `SharpGrip.FluentValidation.AutoValidation` package. **Chose the custom filter (option A)** — ~25 lines, no dependency, full control of the 400 shape, and high learning/interview value. The wiring is throwaway at the MediatR migration anyway, so there's nothing to lose by writing it myself.

## What confused me — the actual gaps from the cold test / LEARN session

The architecture was clear when I built it. The cold rebuild surfaced that the **syntax inside the filter** was where I was fuzzy — I could explain *what* the filter does but stalled writing it line by line. These are those gaps.

- **`continue` — I genuinely didn't know what it did.**
  `continue` skips the *rest of the current loop iteration* and jumps to the next item. It does **not** exit the loop (`break`) and does **not** exit the method (`return`). Think "skip this lap, run the next." The filter uses it as a **guard clause**: `if (argument is null) continue;` means "nothing to do for this argument, move on." Stacking several early `continue`s keeps the loop body flat instead of nesting four `if`s deep.

- **`typeof(IValidator<>)` — doesn't this make `validatorType` an `IValidator`?** No.
  `typeof(X)` *always* returns a `System.Type` (metadata), never an instance of X. `typeof(IValidator<>)` is an **open generic** — `T` is left unfilled (the `<>`). `.MakeGenericType(argument.GetType())` fills `T` at runtime with the argument's real type → returns a `Type` representing `IValidator<CreateProductDTO>`. It's still just a `Type` object, not a validator.

- **What does a `System.Type` actually *hold*? (the "blueprint" wasn't clicking.)**
  A `Type` is a data object *describing* a type — not the type itself, and definitely not an instance. It has properties like `.Name` (`"IValidator\`1"`), `.FullName`, `.IsInterface` (`true`), `.IsGenericType` (`true`), `.GetGenericArguments()` (`[CreateProductDTO]`). Just strings and booleans. There is **no `.ValidateAsync()` on it** — it can't validate anything. Analogy: it's a **business card** for a type, not the person. You hand the card to DI and DI gives you back the real object.

- **`services.GetService(validatorType) is not IValidator validator` — what's happening in one line?**
  Three things at once (C# pattern matching): (1) `GetService` takes the `Type` "business card" and returns the real registered instance as `object?` — or `null` if none; (2) `is not IValidator validator` tests *and* casts it to the usable `IValidator` interface; (3) if it's null or the wrong type → `continue` (no validator for this argument, skip it). If it matches, the variable `validator` is live for the rest of the loop. **Real-world:** POST to an action taking `(CreateProductDTO dto, int id)` — DI has a validator for the DTO but not for `int`, so the `int` iteration hits `null` → `continue`, no crash.

- **`new ValidationContext<object>(argument)` — "wrap the object" didn't cut it. Why is it needed?**
  Type erasure. I resolved the validator as the **non-generic** `IValidator` (I can't write `IValidator<CreateProductDTO>` — the type is only known at runtime). The non-generic `IValidator.ValidateAsync` does **not** accept a raw object — only an `IValidationContext`. So I must box the argument into a `ValidationContext<object>` (the only one I can build, since `argument` is compile-time `object`). It implements `IValidationContext` → the door fits. **Real-world:** a courier who only accepts parcels in their official box — I have a loose item, so I put it in the box.

- **How does the validator get the *right* typed object back out?**
  The object is stored in the context as `InstanceToValidate`. `object` is only the *compile-time* label — the thing in memory is still really a `CreateProductDTO` (casting to `object` hides the type from the compiler, not from the runtime). The concrete `CreateProductDtoValidator` knows its own `T`, casts `InstanceToValidate` back to `CreateProductDTO` (safe, because I built the validator *from* `argument.GetType()` — they always match), then runs the rules. Apple in a box labelled "FRUIT" is still an apple.

- **What does `ValidateAsync` actually do?**
  Walks **every** `RuleFor` in the validator against the object and returns a `ValidationResult` (`IsValid` bool + `Errors` list of `ValidationFailure`, each with `PropertyName` + `ErrorMessage`). By default `CascadeMode` is `Continue`, so it runs *all* rules and collects *every* failure — not just the first — so the user sees all bad fields in one 400. `Async` because a rule *can* hit I/O (`MustAsync` DB check); if none do, it just doesn't await anything. *(Verified against FluentValidation docs: default cascade = `Continue`; `ValidationResult` = `IsValid` + `Errors`.)*

- **First time writing unit tests — what am I even asserting?** (the genuinely new one)
  Every test is **Arrange → Act → Assert**: set up a situation, run the thing, claim what should happen. `[Fact]` = one fixed case; `[Theory]` + `[InlineData(x)]` = same test body run with several inputs. FluentValidation's helpers: `_validator.TestValidate(dto).ShouldNotHaveAnyValidationErrors()` (happy path) and `.ShouldHaveValidationErrorFor(x => x.Price)` (a field should error). The **baseline trick**: a `ValidDto()` helper returns a fully-valid object; each failing test breaks *one* field, so a red test points at exactly one rule.

- **Why is the test helper `file static class Validator`?**
  `file` (C# 11) limits the type to that one source file. It's named `Validator`, which **shadows** `System.ComponentModel.DataAnnotations.Validator`; `file` stops it leaking, and the call inside is fully-qualified to reach the real BCL validator past the shadow. It only exists because `RegisterDTO` still uses DataAnnotations (from task-19) — FluentValidation needs no such helper (`TestValidate` is built in).

- **How does `!context.ModelState.IsValid` *know* it's invalid?**
  `IsValid` is **not a stored flag** — it's a computed getter returning `false` if *any* `ModelState` entry is Invalid. `AddModelError(...)` is what marks an entry Invalid, so my loop's `AddModelError` calls are the trigger. No validator → no `AddModelError` → all entries stay Valid → `IsValid` stays `true` → `next()` runs.

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

---

claude --resume 6badbcf9-2a86-493d-ae6b-1ab21596cd74
