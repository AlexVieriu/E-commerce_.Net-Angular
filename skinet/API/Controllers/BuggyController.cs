using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace API.Controllers;

public class BuggyController : BaseApiController
{
    [HttpGet("unauthorized")]
    public IActionResult GetUnauthorized()
        => Unauthorized("Unauthorized(401)");

    [HttpGet("badrequest")]
    public IActionResult GetBadRequest()
        => BadRequest("Not a good request(400)");

    [HttpGet("notfound")]
    public IActionResult GetNotFound()
        => NotFound("Not found(404)");

    [HttpGet("internalservererror")]
    public IActionResult GetInternalServerError()
    => throw new Exception("Internal Server Error(500)");

    [HttpPost("validationerror")]
    public IActionResult GetValidationError(CreateProductDTO product)
    {
        return Ok();
    }

    [Authorize]
    [HttpGet("secret")]
    public IActionResult GetSecret()
    {
        var name = User.FindFirst(ClaimTypes.Name)?.Value;
        var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Ok($"Hello " + name + " with the id of " + id);
    }
}
