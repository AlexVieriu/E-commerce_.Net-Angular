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
        => Ok();
}
