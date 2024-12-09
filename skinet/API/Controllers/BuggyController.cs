namespace API.Controllers;

public class BuggyController : BaseApiController
{
    [HttpGet("unauthorized")]
    public IActionResult GetUnauthorized()
        => Unauthorized();


    [HttpGet("badrequest")]
    public IActionResult GetBadRequest()
        => BadRequest("Not a good request");


    [HttpGet("notfound")]
    public IActionResult GetNotFound()
        => NotFound();

    [HttpGet("internalservererror")]
    public IActionResult GetInternalServerError()
    => throw new Exception("Internal Server Error");

    [HttpPost("validationerror")]
    public IActionResult GetValidationError(Product product)
        => Ok();
}
