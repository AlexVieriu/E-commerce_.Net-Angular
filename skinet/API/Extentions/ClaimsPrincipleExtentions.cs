namespace API.Extentions;

public static class ClaimsPrincipleExtentions
{
    public static async Task<AppUser> GetUserByEmail(this UserManager<AppUser> userManager,
                                                     ClaimsPrincipal user)
    {
        var userToReturn = await userManager.Users.FirstOrDefaultAsync(
                         x => x.Email == user.GetEmail());

        if (userToReturn == null)
            throw new AuthenticationException("User not found");

        return userToReturn;
    }

    public static string GetEmail(this ClaimsPrincipal user)
    {
        var email = user.FindFirstValue(ClaimTypes.Email)
                     ?? throw new AuthenticationException("Email claim not found");

        return email;
    }
}
