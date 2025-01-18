namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AccountController(SignInManager<AppUser> signInManager) : ControllerBase
{
    [HttpPost("Register")]
    public async Task<ActionResult> Register(RegisterDTO registerDto)
    {
        var user = new AppUser()
        {
            FirstName = registerDto.FirstName,
            LastName = registerDto.LastName,
            Email = registerDto.Email,
            UserName = registerDto.Email
        };

        var result = await signInManager.UserManager.CreateAsync(user, registerDto.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Ok();
    }
}
