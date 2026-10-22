namespace API.Controllers;

public class FallbackController(IWebHostEnvironment env) : Controller
{
    // SPA deep links land here, and PhysicalFile skips StaticFileOptions, so no-cache is set here too.
    public IActionResult Index()
    {
        Response.SetNoCache();
        return PhysicalFile(Path.Combine(env.WebRootPath, "index.html"), "text/HTML");
    }
}
