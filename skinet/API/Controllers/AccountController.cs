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
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem();
        }

        return Ok();
    }

    [HttpPost("logout")]
    public async Task<ActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("user-info")]
    public async Task<ActionResult> GetUserInfo()
    {
        if (User.Identity?.IsAuthenticated == false)
            return NoContent();

        var user = await signInManager.UserManager.GetUserByEmailAsync(User);

        return Ok(new
        {
            user.FirstName,
            user.LastName,
            user.Email
        });
    }

    [HttpGet]
    public ActionResult GetAuthState()
    {
        return Ok(new { IsAuthenticated = User.Identity?.IsAuthenticated });
    }

    [Authorize]
    [HttpPost("address")]
    public async Task<ActionResult<Address>> CreateOrUpdateAddress(AddressDto addressDto)
    {
        var user = await signInManager.UserManager.GetUserByEmailWithAddressAsync(User);

        if (user.Address == null)
            user.Address = addressDto.toEntity(); // create

        else
            user.Address.UpdateFromDto(addressDto); // update

        var result = await signInManager.UserManager.UpdateAsync(user);

        if (!result.Succeeded)
            return BadRequest("Problem updating the user address");

        return Ok(user.Address.toDto());
    }
}