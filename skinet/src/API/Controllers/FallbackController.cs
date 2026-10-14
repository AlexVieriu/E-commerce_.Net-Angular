namespace API.Controllers;

public class FallbackController(IWebHostEnvironment env) : Controller
{
    // Every SPA deep link lands here, and PhysicalFile never sees StaticFileOptions — so the header is set here too.
    public IActionResult Index()
    {
        Response.SetNoCache();
        return PhysicalFile(Path.Combine(env.WebRootPath, "index.html"), "text/HTML");
    }
}
