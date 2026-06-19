using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Filters;

// [ApiController] auto-validates DataAnnotations and returns a 400 ValidationProblemDetails,
// but it does NOT run FluentValidation validators. This filter is that missing trigger
// (Task 8, option A: hand-rolled, no extra dependency):
// for each bound action argument it resolves an IValidator<T> from DI, runs it, and
// short-circuits with the SAME 400 ValidationProblemDetails shape — so the Angular error
// interceptor needs no changes. Arguments without a registered validator pass straight through.
public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument));
            if (result.IsValid) continue;

            foreach (var error in result.Errors)
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
            return;
        }

        await next();
    }
}
