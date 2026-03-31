using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace API.Conventions;

// IApplicationModelConvention runs once at app startup, before any request is handled.
// It lets you modify the list of controllers and their routes before they are registered.
public class RemoveControllerConvention(Type controllerType) : IApplicationModelConvention
{
    // Apply() is called automatically by ASP.NET Core during startup
    // when you add the convention via AddControllers(options => options.Conventions.Add(...))
    public void Apply(ApplicationModel application)
    {
        // application.Controllers is the full list of discovered controllers.
        // We find the one matching the type we want to exclude.
        var controller = application.Controllers
            .FirstOrDefault(c => c.ControllerType == controllerType);

        // If found, remove it — its routes will never be registered.
        // In production this means the endpoints simply don't exist (not 404, literally gone).
        if (controller != null)
            application.Controllers.Remove(controller);
    }
}
