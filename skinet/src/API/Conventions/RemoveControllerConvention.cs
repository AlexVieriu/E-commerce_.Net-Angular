namespace API.Conventions;

// Runs once at startup and drops one controller before its routes are registered.
public class RemoveControllerConvention(Type controllerType) : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        var controller = application.Controllers
            .FirstOrDefault(c => c.ControllerType == controllerType);

        // Its routes never exist; a request to them falls through to the SPA fallback.
        if (controller != null)
            application.Controllers.Remove(controller);
    }
}
